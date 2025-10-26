namespace RiceProduction.Infrastructure.Implementation.NotificationImplementation.SpeedSMS;

/// <summary>
/// Exception thrown when an SMS operation encounters a temporary failure
/// </summary>
public class TemporaryFailureException : Exception
{
    public TemporaryFailureException(string message) : base(message)
    {
    }

    public TemporaryFailureException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}
