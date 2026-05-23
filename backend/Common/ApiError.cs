namespace backend.Common;

public sealed class ApiError
{
    public bool Success { get; init; } = false;

    public string Code { get; init; } = "error";

    public string Message { get; init; } = "Ocurrio un error inesperado al procesar la solicitud.";

    public string? TraceId { get; init; }

    public IDictionary<string, string[]>? Errors { get; init; }

    public static ApiError Create(
        HttpContext httpContext,
        string code,
        string message,
        IDictionary<string, string[]>? errors = null
    )
    {
        return new ApiError
        {
            Code = code,
            Message = message,
            TraceId = httpContext.TraceIdentifier,
            Errors = errors is { Count: > 0 }
                ? new Dictionary<string, string[]>(errors)
                : null
        };
    }
}
