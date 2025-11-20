using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class ValidationBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehaviour<TRequest, TResponse>> _logger;

    public ValidationBehaviour(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehaviour<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            var validationResults = await Task.WhenAll(
                _validators.Select(v =>
                    v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Any())
            {
                // Log all validation failures
                _logger.LogWarning("Validation failed for request {RequestType} {@Request}. {FailureCount} error(s) found.",
                    typeof(TRequest).Name,
                    request,
                    failures.Count);

                foreach (var failure in failures)
                {
                    _logger.LogDebug("Validation error - Property: {PropertyName} | Error: {ErrorMessage} | AttemptedValue: {AttemptedValue}",
                        failure.PropertyName,
                        failure.ErrorMessage,
                        failure.AttemptedValue);
                }

                // Optionally log as Error if you consider validation failures critical
                // _logger.LogError(...);

                throw new ValidationException(failures);
            }
        }

        return await next();
    }
}