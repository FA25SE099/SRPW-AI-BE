using RiceProduction.Application.Common.Models.Zalo;

namespace RiceProduction.Application.Common.Interfaces;

public interface IZaloZnsService
{
    Task<ZnsResponse> SendZnsAsync(ZnsRequest request, string accessToken, CancellationToken cancellationToken = default);
    
    Task<BulkZnsSummary> SendBulkZnsAsync(
        List<BulkZnsRequest> requests, 
        string accessToken,
        int maxConcurrency = 5,
        int maxRetries = 3,
        CancellationToken cancellationToken = default);
    
    Task<BulkZnsSummary> SendBulkZnsWithProgressAsync(
        List<BulkZnsRequest> requests,
        string accessToken,
        IProgress<BulkZnsProgress>? progress = null,
        int maxConcurrency = 5,
        int maxRetries = 3,
        CancellationToken cancellationToken = default);
}
