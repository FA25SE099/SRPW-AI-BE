using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Telemetry;

namespace RiceProduction.Application.Common.Behaviours;

public class PerformanceBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly Stopwatch _timer;
    private readonly ILogger<TRequest> _logger;

    public PerformanceBehaviour(ILogger<TRequest> logger)
    {
        _timer = new Stopwatch();
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var isCommand = requestName.EndsWith("Command");
        var isQuery = requestName.EndsWith("Query");

        // Start OpenTelemetry activity
        using var activity = ApplicationMetrics.StartActivity(
            $"{(isCommand ? "Command" : "Query")}.{requestName}",
            ActivityKind.Internal);

        activity?.SetTag("request.type", isCommand ? "Command" : "Query");
        activity?.SetTag("request.name", requestName);
        activity?.SetTag("request.full_name", typeof(TRequest).FullName);

        _timer.Start();
        Exception? caughtException = null;

        try
        {
            var response = await next();

            _timer.Stop();
            var elapsedMilliseconds = _timer.ElapsedMilliseconds;

            // Record metrics
            if (isCommand)
            {
                ApplicationMetrics.RecordCommandExecution(requestName, elapsedMilliseconds, true);
            }
            else if (isQuery)
            {
                ApplicationMetrics.RecordQueryExecution(requestName, elapsedMilliseconds, true);
            }

            // Log slow requests (> 500ms)
            if (elapsedMilliseconds > 500)
            {
                _logger.LogWarning(
                    "Long Running Request: {Name} ({ElapsedMilliseconds} ms) {@Request}",
                    requestName,
                    elapsedMilliseconds,
                    request);

                activity?.SetTag("performance.slow", true);
                activity?.SetTag("performance.threshold_ms", 500);
            }

            activity?.SetTag("duration_ms", elapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation(
                "Request: {Name} completed in {ElapsedMilliseconds} ms",
                requestName,
                elapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _timer.Stop();
            var elapsedMilliseconds = _timer.ElapsedMilliseconds;

            caughtException = ex;

            // Record failure metrics
            if (isCommand)
            {
                ApplicationMetrics.RecordCommandExecution(requestName, elapsedMilliseconds, false);
            }
            else if (isQuery)
            {
                ApplicationMetrics.RecordQueryExecution(requestName, elapsedMilliseconds, false);
            }


            if (activity != null && ex != null)
            {
                activity.SetTag("exception.type", ex.GetType().ToString());
                activity.SetTag("exception.message", ex.Message);
                activity.SetTag("exception.stacktrace", ex.StackTrace);
            }
 

            _logger.LogError(
                ex,
                "Request Failed: {Name} ({ElapsedMilliseconds} ms)",
                requestName,
                elapsedMilliseconds);

            throw;
        }
    }
}