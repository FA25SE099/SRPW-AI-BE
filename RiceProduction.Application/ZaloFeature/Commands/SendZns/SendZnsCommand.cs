using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models.Zalo;

namespace RiceProduction.Application.ZaloFeature.Commands.SendZns;

public record SendZnsCommand : IRequest<ZnsResponse>
{
    public string Phone { get; init; } = string.Empty;
    public string TemplateId { get; init; } = string.Empty;
    public Dictionary<string, string> TemplateData { get; init; } = new();
    public string? SendingMode { get; init; }
    public string TrackingId { get; init; } = Guid.NewGuid().ToString();
    public string AccessToken { get; init; } = string.Empty;
}

public class SendZnsCommandHandler : IRequestHandler<SendZnsCommand, ZnsResponse>
{
    private readonly IZaloZnsService _zaloZnsService;

    public SendZnsCommandHandler(IZaloZnsService zaloZnsService)
    {
        _zaloZnsService = zaloZnsService;
    }

    public async Task<ZnsResponse> Handle(SendZnsCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Phone))
        {
            throw new ArgumentException("Phone number is required", nameof(request.Phone));
        }

        if (string.IsNullOrEmpty(request.TemplateId))
        {
            throw new ArgumentException("Template ID is required", nameof(request.TemplateId));
        }

        if (string.IsNullOrEmpty(request.AccessToken))
        {
            throw new ArgumentException("Access token is required", nameof(request.AccessToken));
        }

        var znsRequest = new ZnsRequest
        {
            Phone = request.Phone,
            TemplateId = request.TemplateId,
            TemplateData = request.TemplateData,
            SendingMode = request.SendingMode,
            TrackingId = request.TrackingId
        };

        return await _zaloZnsService.SendZnsAsync(znsRequest, request.AccessToken, cancellationToken);
    }
}
