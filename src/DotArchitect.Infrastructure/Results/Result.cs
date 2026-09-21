namespace DotArchitect.Infrastructure.Results;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public int StatusCode { get; }

    private Result(T? value, string? error, bool isSuccess, int statusCode)
    {
        Value = value;
        Error = error;
        IsSuccess = isSuccess;
        StatusCode = statusCode;
    }

    public static Result<T> Success(T value, int statusCode = 200)
        => new(value, null, true, statusCode);

    public static Result<T> Failure(string error, int statusCode = 400)
        => new(default, error, false, statusCode);
}

public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public int StatusCode { get; }

    private Result(string? error, bool isSuccess, int statusCode)
    {
        Error = error;
        IsSuccess = isSuccess;
        StatusCode = statusCode;
    }

    public static Result Success(int statusCode = 200)
        => new(null, true, statusCode);

    public static Result Failure(string error, int statusCode = 400)
        => new(error, false, statusCode);
}
