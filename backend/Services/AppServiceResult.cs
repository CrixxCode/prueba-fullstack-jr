namespace backend.Services;

public sealed class AppServiceResult<T> where T : class
{
    private AppServiceResult(T data)
    {
        Data = data;
    }

    private AppServiceResult(AppServiceError error)
    {
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Data { get; }

    public AppServiceError? Error { get; }

    public static AppServiceResult<T> Success(T data) => new(data);

    public static AppServiceResult<T> BadRequest(
        string message,
        string code = "bad_request"
    ) => new(new AppServiceError(AppServiceErrorType.BadRequest, message, code));

    public static AppServiceResult<T> Unauthorized(
        string message,
        string code = "unauthorized"
    ) => new(new AppServiceError(AppServiceErrorType.Unauthorized, message, code));

    public static AppServiceResult<T> Forbidden(
        string message = "No tienes permisos para realizar esta accion.",
        string code = "forbidden"
    ) => new(new AppServiceError(AppServiceErrorType.Forbidden, message, code));

    public static AppServiceResult<T> NotFound(
        string message,
        string code = "not_found"
    ) => new(new AppServiceError(AppServiceErrorType.NotFound, message, code));

    public static AppServiceResult<T> Conflict(
        string message,
        string code = "conflict"
    ) => new(new AppServiceError(AppServiceErrorType.Conflict, message, code));
}

public sealed record AppServiceError(
    AppServiceErrorType Type,
    string Message,
    string Code
);

public enum AppServiceErrorType
{
    BadRequest,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict
}

