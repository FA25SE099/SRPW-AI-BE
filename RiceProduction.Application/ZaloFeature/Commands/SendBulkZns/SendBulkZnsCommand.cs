using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models.Zalo;

namespace RiceProduction.Application.ZaloFeature.Commands.SendBulkZns;

public record SendBulkZnsCommand : IRequest<BulkZnsSummary>
{
    public List<BulkZnsRequest> Requests { get; init; } = new();
    public string AccessToken { get; init; } = string.Empty;
    public int MaxConcurrency { get; init; } = 5;
    public int MaxRetries { get; init; } = 3;
}

public class SendBulkZnsCommandHandler : IRequestHandler<SendBulkZnsCommand, BulkZnsSummary>
{
    private readonly IZaloZnsService _zaloZnsService;
    private readonly ILogger<SendBulkZnsCommandHandler> _logger;

    public SendBulkZnsCommandHandler(
        IZaloZnsService zaloZnsService,
        ILogger<SendBulkZnsCommandHandler> logger)
    {
        _zaloZnsService = zaloZnsService;
        _logger = logger;
    }

    public async Task<BulkZnsSummary> Handle(SendBulkZnsCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing bulk ZNS send command with {Count} requests", request.Requests.Count);

        var summary = await _zaloZnsService.SendBulkZnsAsync(
            request.Requests,
            request.AccessToken,
            request.MaxConcurrency,
            request.MaxRetries,
            cancellationToken);

        _logger.LogInformation(
            "Bulk ZNS command completed: Success={Success}, Failed={Failed}",
            summary.SuccessCount, summary.FailedCount);

        return summary;
    }
}
