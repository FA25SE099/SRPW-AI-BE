using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Infrastructure.Data;

namespace RiceProduction.Infrastructure.Services;

public class PostGISGroupFormationService : IPostGISGroupFormationService
{
    private readonly ApplicationDbContext _context;

    public PostGISGroupFormationService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Form groups using PostGIS spatial clustering with coherence checks
    /// </summary>
    public async Task<PostGISGroupFormationResult> FormGroupsAsync(
        PostGISGroupingParameters parameters,
        Guid? clusterId = null,
        Guid? seasonId = null,
        CancellationToken cancellationToken = default)
    {
        var sql = @"
WITH plot_data AS (
    SELECT
        p.""Id"" AS plot_id,
        p.""Boundary"" AS boundary,
        ST_Centroid(p.""Boundary"") AS centroid,
        p.""Area"" AS area,
        pc.""PlantingDate"" AS planting_date,
        pc.""Id"" AS cultivation_id,
        pc.""RiceVarietyId"" AS rice_variety_id,
        pc.""SeasonId"" AS season_id,
        f.""ClusterId"" AS cluster_id
    FROM ""Plots"" p
    INNER JOIN ""PlotCultivations"" pc ON p.""Id"" = pc.""PlotId""
    INNER JOIN ""Farmers"" f ON p.""FarmerId"" = f.""Id""
    WHERE p.""Boundary"" IS NOT NULL
      AND (@ClusterId IS NULL OR f.""ClusterId"" = @ClusterId)
      AND (@SeasonId IS NULL OR pc.""SeasonId"" = @SeasonId)
),

-- 1. DBSCAN clustering per rice variety
spatial_clusters AS (
    SELECT
        pd.*,
ST_ClusterDBSCAN(centroid, eps := @ProximityThreshold, minpoints := @MinPlotsPerGroup)
    OVER () AS spatial_cluster_id    FROM plot_data pd
),

-- 2. Exact cluster diameter – works on Geometry
diameter_calc AS (
    SELECT
        sc.*,
        CASE
            WHEN COUNT(*) OVER (PARTITION BY rice_variety_id, spatial_cluster_id) <= 1 THEN 0.0
            ELSE COALESCE(
                ST_Length(
                    ST_LongestLine(
                        ST_Collect(centroid) OVER (PARTITION BY rice_variety_id, spatial_cluster_id),
                        ST_Collect(centroid) OVER (PARTITION BY rice_variety_id, spatial_cluster_id)
                    )
                ),
                0.0
            )
        END AS cluster_diameter
    FROM spatial_clusters sc
),

-- 3. Keep compact clusters + isolated points
valid_clusters AS (
    SELECT *
    FROM diameter_calc
    WHERE spatial_cluster_id IS NULL
       OR cluster_diameter <= (@ProximityThreshold * 2)
),

-- 4. Temporal bucketing
with_dates AS (
    SELECT
        *,
        FLOOR(EXTRACT(EPOCH FROM planting_date) / (86400.0 * @PlantingDateTolerance))::bigint AS date_bucket
    FROM valid_clusters
),

-- 5. Candidate groups
candidate_groups AS (
    SELECT
        rice_variety_id,
        spatial_cluster_id,
        date_bucket,
        COUNT(*) AS plot_count,
        SUM(area) AS total_area,
        MIN(planting_date) AS planting_window_start,
        MAX(planting_date) AS planting_window_end,
        -- Safe median for timestamp (works everywhere)
        (ARRAY_AGG(planting_date ORDER BY planting_date))
            [CARDINALITY(ARRAY_AGG(planting_date ORDER BY planting_date)) / 2 + 1] AS median_planting_date,
        ARRAY_AGG(plot_id ORDER BY planting_date)        AS plot_ids,
        ARRAY_AGG(cultivation_id ORDER BY planting_date) AS cultivation_ids,
        ST_Union(boundary)                               AS combined_boundary,
        ST_Centroid(ST_Union(boundary))                  AS group_centroid
    FROM with_dates
    WHERE spatial_cluster_id IS NOT NULL
    GROUP BY rice_variety_id, spatial_cluster_id, date_bucket
),

-- 6. Final groups after all business rules
final_groups AS (
    SELECT
        ROW_NUMBER() OVER (ORDER BY rice_variety_id, spatial_cluster_id, date_bucket) AS group_number,
        rice_variety_id,
        plot_count,
        total_area,
        planting_window_start,
        planting_window_end,
        median_planting_date,
        plot_ids,
        cultivation_ids,
        ST_Buffer(combined_boundary, @BorderBuffer) AS group_boundary,
        group_centroid
    FROM candidate_groups
    WHERE total_area >= @MinGroupArea
      AND total_area <= @MaxGroupArea
      AND plot_count <= @MaxPlotsPerGroup
),

-- 7. Plots that made it into final groups
plots_in_groups AS (
    SELECT UNNEST(plot_ids) AS plot_id
    FROM final_groups
),

-- 8. Ungrouped plots with clear reason
ungrouped_plots AS (
    SELECT
        pd.plot_id,
        pd.cultivation_id,
        pd.rice_variety_id,
        pd.planting_date,
        pd.centroid,
        pd.area,
       CASE
    WHEN sc.spatial_cluster_id IS NULL                           THEN 'IsolatedLocation'
    WHEN dc.cluster_diameter > (@ProximityThreshold * 2)         THEN 'TooSpreadOut'
    WHEN wd.plot_id IS NULL                                      THEN 'PlantingDateTooFar'
    WHEN cg.rice_variety_id IS NULL                              THEN 'NoValidGroup'
    WHEN cg.plot_count > @MaxPlotsPerGroup                       THEN 'TooManyPlots'
    WHEN cg.plot_count < @MinPlotsPerGroup                       THEN 'TooFewPlots'
    WHEN cg.total_area < @MinGroupArea                           THEN 'TooSmallArea'
    WHEN cg.total_area > @MaxGroupArea                           THEN 'TooLargeArea'
    ELSE 'OtherReason'
END AS ungrouped_reason,
        nearest.group_number        AS nearest_group_number,
        nearest.dist                AS distance_to_nearest_group
    FROM plot_data pd
    LEFT JOIN spatial_clusters sc   ON pd.plot_id = sc.plot_id
    LEFT JOIN diameter_calc dc      ON pd.plot_id = dc.plot_id
    LEFT JOIN with_dates wd        ON pd.plot_id = wd.plot_id
    LEFT JOIN candidate_groups cg    ON wd.rice_variety_id = cg.rice_variety_id
                                      AND wd.spatial_cluster_id = cg.spatial_cluster_id
                                      AND wd.date_bucket = cg.date_bucket
    LEFT JOIN plots_in_groups pig   ON pd.plot_id = pig.plot_id
    LEFT JOIN LATERAL (
        SELECT fg.group_number, ST_Distance(pd.centroid, fg.group_centroid) AS dist
        FROM final_groups fg
        WHERE fg.rice_variety_id = pd.rice_variety_id
        ORDER BY ST_Distance(pd.centroid, fg.group_centroid)
        LIMIT 1
    ) nearest ON true
    WHERE pig.plot_id IS NULL
)

-- FINAL OUTPUT – exactly one row per real plot
SELECT
    'GROUPED'   AS result_type,
    group_number,
    rice_variety_id,
    plot_count,
    total_area,
    planting_window_start,
    planting_window_end,
    median_planting_date,
    plot_ids,
    cultivation_ids,
    group_boundary,
    group_centroid,
    NULL::uuid      AS plot_id,
    NULL::uuid      AS cultivation_id,
    NULL::timestamp AS planting_date,
    NULL::geometry  AS centroid,
    NULL::numeric   AS area,
    NULL::text      AS ungrouped_reason,
    NULL::integer   AS nearest_group_number,
    NULL::double precision AS distance_to_nearest_group
FROM final_groups

UNION ALL

SELECT
    'UNGROUPED' AS result_type,
    NULL::bigint AS group_number,
    rice_variety_id,
    1           AS plot_count,
    area        AS total_area,
    planting_date AS planting_window_start,
    planting_date AS planting_window_end,
    planting_date AS median_planting_date,
    ARRAY[plot_id]        AS plot_ids,
    ARRAY[cultivation_id] AS cultivation_ids,
    NULL::geometry AS group_boundary,
    NULL::geometry AS group_centroid,
    plot_id,
    cultivation_id,
    planting_date,
    centroid,
    area,
    ungrouped_reason,
    nearest_group_number,
    distance_to_nearest_group
FROM ungrouped_plots

ORDER BY result_type DESC, group_number NULLS LAST, plot_id;
";

        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        // Add parameters
        AddParameter(command, "@ClusterId", clusterId.HasValue ? clusterId.Value : DBNull.Value);
        AddParameter(command, "@SeasonId", seasonId.HasValue ? seasonId.Value : DBNull.Value);
        AddParameter(command, "@ProximityThreshold", parameters.ProximityThreshold);
        AddParameter(command, "@PlantingDateTolerance", parameters.PlantingDateTolerance);
        AddParameter(command, "@MinGroupArea", parameters.MinGroupArea);
        AddParameter(command, "@MaxGroupArea", parameters.MaxGroupArea);
        AddParameter(command, "@MinPlotsPerGroup", parameters.MinPlotsPerGroup);
        AddParameter(command, "@MaxPlotsPerGroup", parameters.MaxPlotsPerGroup);
        AddParameter(command, "@BorderBuffer", parameters.BorderBuffer);

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        var result = new PostGISGroupFormationResult();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var resultType = reader.GetString(0);

            if (resultType == "GROUPED")
            {
                var group = new PostGISProposedGroup
                {
                    GroupNumber = reader.GetInt32(1),
                    RiceVarietyId = reader.GetGuid(2),
                    PlotCount = reader.GetInt32(3),
                    TotalArea = reader.GetDecimal(4),
                    PlantingWindowStart = reader.GetDateTime(5),
                    PlantingWindowEnd = reader.GetDateTime(6),
                    MedianPlantingDate = reader.GetDateTime(7),
                    PlotIds = ParseGuidArray(reader.GetValue(8)),
                    CultivationIds = ParseGuidArray(reader.GetValue(9)),
                    GroupBoundary = reader.IsDBNull(10) ? null : reader.GetFieldValue<Polygon>(10),
                    GroupCentroid = reader.IsDBNull(11) ? null : reader.GetFieldValue<Point>(11)
                };
                result.Groups.Add(group);
            }
            else // UNGROUPED
            {
                var ungrouped = new PostGISUngroupedPlotInfo
                {
                    PlotId = reader.GetGuid(12),
                    CultivationId = reader.GetGuid(13),
                    RiceVarietyId = reader.GetGuid(2),
                    PlantingDate = reader.GetDateTime(14),
                    Centroid = reader.IsDBNull(15) ? null : reader.GetFieldValue<Point>(15),
                    Area = reader.GetDecimal(16),
                    UngroupedReason = reader.GetString(17),
                    NearestGroupNumber = reader.IsDBNull(18) ? null : reader.GetInt32(18),
                    DistanceToNearestGroup = reader.IsDBNull(19) ? null : reader.GetDouble(19)
                };
                result.UngroupedPlots.Add(ungrouped);
            }
        }

        return result;
    }

    private void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private List<Guid> ParseGuidArray(object value)
    {
        if (value == null || value is DBNull)
            return new List<Guid>();

        // PostgreSQL returns arrays as object[]
        if (value is Guid[] guidArray)
            return guidArray.ToList();

        if (value is object[] objArray)
            return objArray.Cast<Guid>().ToList();

        return new List<Guid>();
    }
}

