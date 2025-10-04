namespace RiceProduction.Application.Common.Models;
public class Result<T>
{
    public bool Succeeded { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public IEnumerable<string> Errors { get; set; } = new List<string>();

    public static Result<T> Success(T data, string? message = null)
    {
        return new Result<T>
        {
            Succeeded = true,
            Data = data,
            Message = message
        };
    }

    public static Result<T> Failure(IEnumerable<string> errors, string? message = null)
    {
        return new Result<T>
        {
            Succeeded = false,
            Message = message,
            Errors = errors
        };
    }

    public static Result<T> Failure(string error, string? message = null)
    {
        return new Result<T>
        {
            Succeeded = false,
            Message = message,
            Errors = new[] { error }
        };
    }
}

public class Result
{
    public bool Succeeded { get; set; }
    public string? Message { get; set; }
    public IEnumerable<string> Errors { get; set; } = new List<string>();

    public static Result Success(string? message = null)
    {
        return new Result
        {
            Succeeded = true,
            Message = message
        };
    }

    public static Result Failure(IEnumerable<string> errors, string? message = null)
    {
        return new Result
        {
            Succeeded = false,
            Message = message,
            Errors = errors
        };
    }

    public static Result Failure(string error, string? message = null)
    {
        return new Result
        {
            Succeeded = false,
            Message = message,
            Errors = new[] { error }
        };
    }
}
