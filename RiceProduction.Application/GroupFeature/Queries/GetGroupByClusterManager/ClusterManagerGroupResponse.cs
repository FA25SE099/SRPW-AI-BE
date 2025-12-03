using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;

namespace RiceProduction.Application.GroupFeature.Queries.GetGroupByClusterManager;

public class ClusterManagerGroupResponse
{
    public Guid GroupId { get; set; }
    public GroupStatus Status { get; set; }
    public decimal? TotalArea { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public DateTime? PlantingDate { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    public string SupervisorName { get; set; } = string.Empty;
    public int TotalPlots { get; set; }
    public int ActivePlans { get; set; } // Số ProductionPlans đang hoạt động
}