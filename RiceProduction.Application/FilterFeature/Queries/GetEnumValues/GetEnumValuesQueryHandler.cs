using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.FilterFeature.Queries.GetEnumValues;

public class GetEnumValuesQueryHandler : IRequestHandler<GetEnumValuesQuery, Result<EnumValuesResponse>>
{
    public async Task<Result<EnumValuesResponse>> Handle(GetEnumValuesQuery request, CancellationToken cancellationToken)
    {
        var response = new EnumValuesResponse();

        // If no specific enum type requested, return all
        if (string.IsNullOrWhiteSpace(request.EnumType))
        {
            response.GroupStatuses = GetEnumValues<GroupStatus>();
            response.PlotStatuses = GetEnumValues<PlotStatus>();
            response.TaskStatuses = GetEnumValues<RiceProduction.Domain.Enums.TaskStatus>();
            response.TaskTypes = GetEnumValues<TaskType>();
            response.TaskPriorities = GetEnumValues<TaskPriority>();
            response.AlertStatuses = GetEnumValues<AlertStatus>();
            response.AlertSeverities = GetEnumValues<AlertSeverity>();
            response.MaterialTypes = GetEnumValues<MaterialType>();
            response.CultivationStatuses = GetEnumValues<CultivationStatus>();
        }
        else
        {
            // Return specific enum based on request
            var enumType = request.EnumType.ToLower().Trim();
            
            switch (enumType)
            {
                case "groupstatus":
                case "group":
                    response.GroupStatuses = GetEnumValues<GroupStatus>();
                    break;

                case "plotstatus":
                case "plot":
                    response.PlotStatuses = GetEnumValues<PlotStatus>();
                    break;
                    
                case "taskstatus":
                case "task":
                    response.TaskStatuses = GetEnumValues<RiceProduction.Domain.Enums.TaskStatus>();
                    break;
                    
                case "tasktype":
                    response.TaskTypes = GetEnumValues<TaskType>();
                    break;
                    
                case "taskpriority":
                case "priority":
                    response.TaskPriorities = GetEnumValues<TaskPriority>();
                    break;
                    
                case "alertstatus":
                    response.AlertStatuses = GetEnumValues<AlertStatus>();
                    break;
                    
                case "alertseverity":
                case "severity":
                    response.AlertSeverities = GetEnumValues<AlertSeverity>();
                    break;
                    
                case "materialtype":
                case "material":
                    response.MaterialTypes = GetEnumValues<MaterialType>();
                    break;
                    
                case "cultivationstatus":
                case "cultivation":
                    response.CultivationStatuses = GetEnumValues<CultivationStatus>();
                    break;
                    
                default:
                    return Result<EnumValuesResponse>.Failure(
                        $"Unknown enum type: {request.EnumType}. Supported types: PlotStatus, TaskStatus, TaskType, TaskPriority, AlertStatus, AlertSeverity, MaterialType, CultivationStatus",
                        "InvalidEnumType");
            }
        }

        return Result<EnumValuesResponse>.Success(response, "Enum values retrieved successfully.");
    }

    private List<EnumValueDto> GetEnumValues<TEnum>() where TEnum : Enum
    {
        return Enum.GetValues(typeof(TEnum))
            .Cast<TEnum>()
            .Select(e => new EnumValueDto
            {
                Value = Convert.ToInt32(e),
                Name = e.ToString(),
                DisplayName = FormatDisplayName(e.ToString())
            })
            .ToList();
    }

    private string FormatDisplayName(string name)
    {
        // Convert PascalCase to Display Name with spaces
        // e.g., "PendingPolygon" -> "Pending Polygon"
        return System.Text.RegularExpressions.Regex.Replace(name, "([A-Z])", " $1").Trim();
    }
}
