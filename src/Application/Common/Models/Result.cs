namespace OsuStocks.Application.Common.Models;

public interface IResult
{
    bool IsSuccess { get; }
    Error? Error { get; }
}

public class Result : IResult
{
    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public Error? Error { get; }

    public static Result Success()
    {
        return new Result(true, null);
    }

    public static Result Failure(string code, string message)
    {
        return new Result(false, new Error(code, message));
    }

    public static Result<T> Success<T>(T value)
    {
        return Result<T>.Success(value);
    }

    public static Result<T> Failure<T>(string code, string message)
    {
        return Result<T>.Failure(code, message);
    }
}

public sealed class Result<T> : Result
{
    private Result(bool isSuccess, T? value, Error? error)
        : base(isSuccess, error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value)
    {
        return new Result<T>(true, value, null);
    }

    public static new Result<T> Failure(string code, string message)
    {
        return new Result<T>(false, default, new Error(code, message));
    }
}
