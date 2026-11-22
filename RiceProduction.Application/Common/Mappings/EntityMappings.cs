using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Mappings;

public static class EntityMappings
{
    public static FarmerDTO ToDto(this Farmer farmer) =>
        PopulateFarmerDto(farmer, new FarmerDTO());

    public static FarmerDetailDTO ToDetailDto(this Farmer farmer)
    {
        var dto = PopulateFarmerDto(farmer, new FarmerDetailDTO());
        dto.Plots = farmer.OwnedPlots?
            .Select(p => p.ToDetailDto())
            .ToList() ?? new List<PlotDetailDTO>();
        dto.Groups = farmer.OwnedPlots?
            .SelectMany(p => p.GroupPlots ?? Enumerable.Empty<GroupPlot>())
            .Select(gp => gp.Group)
            .Where(g => g != null)
            .Distinct()
            .Select(g => g!.ToDetailDto())
            .ToList() ?? new List<GroupDetailDTO>();
        return dto;
    }

    public static GroupDTO ToDto(this Group group, bool includeProductionPlans = true) =>
        PopulateGroupDto(group, new GroupDTO(), includeProductionPlans);

    public static GroupDetailDTO ToDetailDto(this Group group, bool includeProductionPlans = true)
    {
        var dto = PopulateGroupDto(group, new GroupDetailDTO(), includeProductionPlans);
        dto.Plots = group.GroupPlots?
            .Where(gp => gp.Plot != null)
            .Select(gp => gp.Plot.ToDto())
            .ToList() ?? new List<PlotDTO>();
        return dto;
    }

    public static PlotDTO ToDto(this Plot plot) =>
        PopulatePlotDto(plot, new PlotDTO());

    public static PlotDetailDTO ToDetailDto(this Plot plot)
    {
        var dto = PopulatePlotDto(plot, new PlotDetailDTO());
        dto.PlotCultivations = plot.PlotCultivations?
            .Select(pc => pc.ToDto())
            .ToList() ?? new List<PlotCultivationDTO>();
        dto.ProductionPlans = plot.GroupPlots?
            .Select(gp => gp.Group)
            .FirstOrDefault()?
            .ProductionPlans?
            .Select(pp => pp.ToDto())
            .ToList() ?? new List<ProductionPlanDTO>();
        return dto;
    }

    public static SeasonDTO ToDto(this Season season) => new()
    {
        SeasonId = season.Id,
        SeasonName = season.SeasonName,
        StartDate = season.StartDate,
        EndDate = season.EndDate,
        SeasonType = season.SeasonType,
        IsActive = season.IsActive
    };

    public static PlotCultivationDTO ToDto(this PlotCultivation plotCultivation) => new()
    {
        PlotCultivationId = plotCultivation.Id,
        PlotId = plotCultivation.PlotId,
        SeasonId = plotCultivation.SeasonId,
        RiceVarietyId = plotCultivation.RiceVarietyId,
        PlantingDate = plotCultivation.PlantingDate,
        ActualYield = plotCultivation.ActualYield,
        Status = plotCultivation.Status,
        CultivationTasks = plotCultivation.CultivationTasks?
            .Select(t => t.ToDto())
            .ToList() ?? new List<CultivationTaskDTO>()
    };

    public static CultivationTaskDTO ToDto(this CultivationTask task) => new()
    {
        CultivationTaskId = task.Id,
        ProductionPlanTaskId = task.ProductionPlanTaskId ?? Guid.Empty,
        AssignedToUserId = task.AssignedToUserId,
        AssignedToVendorId = task.AssignedToVendorId,
        ExecutionOrder = task.ExecutionOrder,
        IsContingency = task.IsContingency,
        ContingencyReason = task.ContingencyReason,
        ActualStartDate = task.ActualStartDate,
        ActualEndDate = task.ActualEndDate,
        ActualMaterialCost = task.ActualMaterialCost,
        ActualServiceCost = task.ActualServiceCost,
        CompletedAt = task.CompletedAt,
        VerifiedBy = task.VerifiedBy,
        VerifiedAt = task.VerifiedAt,
        WeatherConditions = task.WeatherConditions,
        InterruptionReason = task.InterruptionReason
    };

    public static ProductionPlanDTO ToDto(
        this ProductionPlan plan,
        bool includeGroup = true,
        bool includeSubmitter = true,
        bool includeApprover = true) =>
        PopulateProductionPlanDto(plan, new ProductionPlanDTO(), includeGroup, includeSubmitter, includeApprover);

    public static ProductionPlanDetailDTO ToDetailDto(
        this ProductionPlan plan,
        bool includeGroup = true,
        bool includeSubmitter = true,
        bool includeApprover = true)
    {
        var dto = PopulateProductionPlanDto(plan, new ProductionPlanDetailDTO(), includeGroup, includeSubmitter, includeApprover);
        dto.ProductionStages = plan.CurrentProductionStages?
            .Select(s => s.ToDto())
            .ToList() ?? new List<ProductionStageDTO>();
        return dto;
    }

    public static ProductionStageDTO ToDto(this ProductionStage stage) =>
        PopulateProductionStageDto(stage, new ProductionStageDTO(), MapProductionPlanTasks(stage.ProductionPlanTasks));

    public static ProductionStageDetailDTO ToDetailDto(this ProductionStage stage)
    {
        var tasks = MapProductionPlanTasks(stage.ProductionPlanTasks);
        var dto = PopulateProductionStageDto(stage, new ProductionStageDetailDTO(), tasks);
        dto.ProductionPlanTasks = tasks;
        return dto;
    }

    public static ProductionPlanTaskDTO ToDto(this ProductionPlanTask task) => new()
    {
        ProductionPlanTaskId = task.Id,
        TaskName = task.TaskName,
        Description = task.Description,
        TaskType = task.TaskType,
        ScheduledDate = task.ScheduledDate,
        ScheduledEndDate = task.ScheduledEndDate,
        Status = task.Status,
        Priority = task.Priority,
        SequenceOrder = task.SequenceOrder
    };

    public static ProductionPlanTaskMaterialDTO ToDto(this ProductionPlanTaskMaterial taskMaterial) => new()
    {
        ProductionPlanTaskMaterialId = taskMaterial.Id,
        ProductionPlanTaskId = taskMaterial.ProductionPlanTaskId,
        MaterialId = taskMaterial.MaterialId,
        QuantityPerHa = taskMaterial.QuantityPerHa,
        EstimatedAmount = taskMaterial.EstimatedAmount,
        ProductionPlanTask = taskMaterial.ProductionPlanTask?.ToDto()!,
        Material = taskMaterial.Material?.ToDto()!
    };

    public static MaterialDTO ToDto(this Material material) => new()
    {
        MaterialId = material.Id,
        Name = material.Name,
        AmmountPerMaterial = material.AmmountPerMaterial,
        Unit = material.Unit,
        PricePerMaterial = material.MaterialPrices?
            .Select(m => m.PricePerMaterial)
            .FirstOrDefault() ?? 0m
    };

    public static MaterialPriceDTO ToDto(this MaterialPrice materialPrice) => new()
    {
        MaterialPriceId = materialPrice.Id,
        PricePerMaterial = materialPrice.PricePerMaterial
    };

    public static SupervisorDTO ToDto(
        this Supervisor supervisor,
        bool includeGroupProductionPlans = true,
        bool includeAssignments = true) => new()
    {
        SupervisorId = supervisor.Id,
        ClusterId = supervisor.ClusterId,
        SupervisorName = supervisor.FullName ?? string.Empty,
        MaxFarmerCapacity = supervisor.MaxFarmerCapacity,
        CurrentFarmerCount = supervisor.CurrentFarmerCount,
        SupervisedGroups = supervisor.SupervisedGroups?
            .Select(g => g.ToDto(includeProductionPlans: includeGroupProductionPlans))
            .ToList() ?? new List<GroupDTO>(),
        AssignedTasks = supervisor.AssignedTasks?
            .Select(t => t.ToDto())
            .ToList() ?? new List<CultivationTaskDTO>(),
        SupervisorAssignments = includeAssignments
            ? supervisor.SupervisorAssignments?
                .Select(a => a.ToDto(includeSupervisor: false))
                .ToList() ?? new List<SupervisorFarmerAssignmentDTO>()
            : new List<SupervisorFarmerAssignmentDTO>()
    };

    public static SupervisorFarmerAssignmentDTO ToDto(
        this SupervisorFarmerAssignment assignment,
        bool includeSupervisor = true) => new()
    {
        SupervisorFarmerAssignmentId = assignment.Id,
        SupervisorId = assignment.SupervisorId,
        FarmerId = assignment.FarmerId,
        AssignedAt = assignment.AssignedAt,
        IsActive = assignment.IsActive,
        AssignmentNotes = assignment.AssignmentNotes,
        Priority = assignment.Priority,
        Supervisor = includeSupervisor && assignment.Supervisor != null
            ? assignment.Supervisor.ToDto(includeAssignments: false)
            : null!,
        Farmer = assignment.Farmer != null
            ? assignment.Farmer.ToDto()
            : null!
    };

    public static AgronomyExpertDTO ToDto(
        this AgronomyExpert expert,
        bool includeApprovedPlans = true) => new()
    {
        AgronomyExpertId = expert.Id,
        ApprovedProductionPlans = includeApprovedPlans
            ? expert.ApprovedProductionPlans?
                .Select(p => p.ToDto(includeApprover: false))
                .ToList() ?? new List<ProductionPlanDTO>()
            : new List<ProductionPlanDTO>()
    };

    private static TDto PopulateFarmerDto<TDto>(Farmer farmer, TDto dto) where TDto : FarmerDTO
    {
        dto.FarmerId = farmer.Id;
        dto.FullName = farmer.FullName;
        dto.Address = farmer.Address;
        dto.PhoneNumber = farmer.PhoneNumber;
        dto.IsActive = farmer.IsActive;
        dto.IsVerified = farmer.IsVerified;
        dto.LastActivityAt = farmer.LastActivityAt;
        dto.FarmCode = farmer.FarmCode;
        dto.PlotCount = farmer.OwnedPlots?.Count ?? 0;
        return dto;
    }

    private static TDto PopulateGroupDto<TDto>(Group group, TDto dto, bool includeProductionPlans) where TDto : GroupDTO
    {
        dto.GroupId = group.Id;
        dto.ClusterId = group.ClusterId;
        dto.SupervisorId = group.SupervisorId;
        dto.Status = group.Status;
        dto.Area = group.Area?.ToString();
        dto.TotalArea = group.TotalArea;
        dto.ProductionPlans = includeProductionPlans
            ? group.ProductionPlans?
                .Select(p => p.ToDto(includeGroup: false, includeSubmitter: false, includeApprover: false))
                .ToList() ?? new List<ProductionPlanDTO>()
            : new List<ProductionPlanDTO>();
        return dto;
    }

    private static TDto PopulatePlotDto<TDto>(Plot plot, TDto dto) where TDto : PlotDTO
    {
        dto.PlotId = plot.Id;
        dto.FarmerId = plot.FarmerId;
        dto.FarmerName = plot.Farmer?.FullName;
        dto.BoundaryGeoJson = plot.Boundary?.ToString();
        dto.CoordinateGeoJson = plot.Coordinate?.ToString();
        dto.SoThua = plot.SoThua;
        dto.SoTo = plot.SoTo;
        dto.Area = plot.Area;
        dto.SoilType = plot.SoilType;
        dto.Status = plot.Status;
        dto.VarietyName = plot.PlotCultivations?
            .OrderByDescending(pc => pc.PlantingDate)
            .Select(pc => pc.RiceVariety?.VarietyName)
            .FirstOrDefault() ?? string.Empty;
        dto.Seasons = plot.PlotCultivations?
            .Where(pc => pc.Season != null)
            .Select(pc => pc.Season)
            .Distinct()
            .Select(s => s.ToDto())
            .ToList() ?? new List<SeasonDTO>();
        return dto;
    }

    private static TDto PopulateProductionPlanDto<TDto>(
        ProductionPlan plan,
        TDto dto,
        bool includeGroup,
        bool includeSubmitter,
        bool includeApprover) where TDto : ProductionPlanDTO
    {
        dto.ProductionPlanId = plan.Id;
        dto.GroupId = plan.GroupId;
        dto.StandardPlanId = plan.StandardPlanId;
        dto.PlanName = plan.PlanName;
        dto.BasePlantingDate = plan.BasePlantingDate;
        dto.Status = plan.Status;
        dto.TotalArea = plan.TotalArea;
        dto.SubmittedAt = plan.SubmittedAt;
        dto.ApprovedAt = plan.ApprovedAt;
        dto.ApprovedBy = plan.ApprovedBy;
        dto.SubmittedBy = plan.SubmittedBy;
        dto.CurrentProductionStageId = plan.CurrentProductionStages?.FirstOrDefault()?.Id;
        dto.Group = includeGroup ? plan.Group?.ToDto(includeProductionPlans: false) : null;
        dto.Submitter = includeSubmitter ? plan.Submitter?.ToDto(includeGroupProductionPlans: false) : null;
        dto.Approver = includeApprover ? plan.Approver?.ToDto(includeApprovedPlans: false) : null;
        dto.CurrentProductionStages = plan.CurrentProductionStages?
            .Select(s => s.ToDto())
            .ToList() ?? new List<ProductionStageDTO>();
        return dto;
    }

    private static TDto PopulateProductionStageDto<TDto>(
        ProductionStage stage,
        TDto dto,
        List<ProductionPlanTaskDTO> tasks) where TDto : ProductionStageDTO
    {
        dto.ProductionStageId = stage.Id;
        dto.StageName = stage.StageName;
        dto.Description = stage.Description;
        dto.SequenceOrder = stage.SequenceOrder;
        dto.TypicalDurationDays = stage.TypicalDurationDays;
        dto.IsActive = stage.IsActive;
        dto.Notes = stage.Notes;
        dto.ProductionPlanTasks = tasks;
        return dto;
    }

    private static List<ProductionPlanTaskDTO> MapProductionPlanTasks(ICollection<ProductionPlanTask>? tasks) =>
        tasks?.Select(t => t.ToDto()).ToList() ?? new List<ProductionPlanTaskDTO>();
}
