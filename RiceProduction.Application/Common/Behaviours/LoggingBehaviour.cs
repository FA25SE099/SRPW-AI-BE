using MediatR.Pipeline;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using System.Diagnostics;

namespace RiceProduction.Application.Common.Behaviours;

public class LoggingBehaviour<TRequest> : IRequestPreProcessor<TRequest> where TRequest : notnull
{
    private readonly ILogger _logger;
    private readonly IUser _user;
    private readonly IIdentityService _identityService;

    public LoggingBehaviour(ILogger<TRequest> logger, IUser user, IIdentityService identityService)
    {
        _logger = logger;
        _user = user;
        _identityService = identityService;
    }

    public async Task Process(TRequest request, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _user.Id?.ToString() ?? string.Empty;
        string? userName = string.Empty;

        if (_user.Id.HasValue)
        {
            userName = await _identityService.GetUserNameAsync(_user.Id.Value);
        }

        // Add to current activity
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("user.id", userId);
            activity.SetTag("user.name", userName);
            activity.SetTag("request.name", requestName);
        }

        _logger.LogInformation(
            "RiceProduction Request: {Name} {@UserId} {@UserName} {@Request}",
            requestName, userId, userName, request);
    }
}
