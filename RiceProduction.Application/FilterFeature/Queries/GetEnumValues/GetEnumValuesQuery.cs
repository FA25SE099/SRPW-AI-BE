using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FilterFeature.Queries.GetEnumValues;

public class GetEnumValuesQuery : IRequest<Result<EnumValuesResponse>>
{
    public string? EnumType { get; set; }
}

public class EnumValuesResponse
{
    public List<EnumValueDto>? PlotStatuses { get; set; }
    public List<EnumValueDto>? TaskStatuses { get; set; }
    public List<EnumValueDto>? TaskTypes { get; set; }
    public List<EnumValueDto>? GroupStatuses { get; set; }
    public List<EnumValueDto>? TaskPriorities { get; set; }
    public List<EnumValueDto>? AlertStatuses { get; set; }
    public List<EnumValueDto>? AlertSeverities { get; set; }
    public List<EnumValueDto>? MaterialTypes { get; set; }
    public List<EnumValueDto>? CultivationStatuses { get; set; }
}

public class EnumValueDto
{
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
