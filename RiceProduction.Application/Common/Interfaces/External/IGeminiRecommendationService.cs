using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Response;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Interfaces.External;

public interface IGeminiRecommendationService
{
    Task<PestRecommendationResponse> GetRecommendationAsync(PestRecommendationRequest request);
    Task<bool> TestConnectionAsync();
}
