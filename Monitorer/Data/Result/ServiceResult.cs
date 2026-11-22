namespace Monitorer.Data.Result;

/// <summary>What services return to controllers and pages: a status plus a value or an error message.</summary>
public class ServiceResult<T>
{
    public ResultStatus Status { get; }
    public T? Value { get; }
    public string? Message { get; }

    public bool IsSuccess => Status is ResultStatus.Ok or ResultStatus.Created or ResultStatus.Changed;

    private ServiceResult(ResultStatus status, T? value, string? message)
    {
        Status = status;
        Value = value;
        Message = message;
    }

    public static ServiceResult<T> Ok(T value) => new(ResultStatus.Ok, value, null);
    public static ServiceResult<T> Created(T value) => new(ResultStatus.Created, value, null);
    public static ServiceResult<T> Changed(T value) => new(ResultStatus.Changed, value, null);
    public static ServiceResult<T> Unauthorized(string message) => new(ResultStatus.Unauthorized, default, message);
    public static ServiceResult<T> Forbidden(string message) => new(ResultStatus.Forbidden, default, message);
    public static ServiceResult<T> NotFound(string message) => new(ResultStatus.NotFound, default, message);
    public static ServiceResult<T> NotAllowed(string message) => new(ResultStatus.NotAllowed, default, message);
    public static ServiceResult<T> InvalidValue(string message) => new(ResultStatus.InvalidValue, default, message);
    public static ServiceResult<T> Conflict(string message) => new(ResultStatus.Conflict, default, message);
    public static ServiceResult<T> Locked(string message) => new(ResultStatus.Locked, default, message);
}
