using RiceProduction.Application.Common.Models.Request;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Interfaces.External;

public interface IAiReportService
{
    Task<PlanRecommendationResponse> SuggestTasksAsync(PlanRecommendationRequest request);
    Task<bool> TestConnectionAsync();
}
