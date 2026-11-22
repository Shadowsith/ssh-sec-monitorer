namespace Configurator.Data.Result;

/// <summary>What services return to the view models: a value or an error message.</summary>
public class ServiceResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Message { get; }

    private ServiceResult(bool isSuccess, T? value, string? message)
    {
        IsSuccess = isSuccess;
        Value = value;
        Message = message;
    }

    public static ServiceResult<T> Ok(T value) => new(true, value, null);
    public static ServiceResult<T> Fail(string message) => new(false, default, message);
}
