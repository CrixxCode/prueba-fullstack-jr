using System.ComponentModel.DataAnnotations;
using backend.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace backend.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger
    )
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}", context.TraceIdentifier);

            if (context.Response.HasStarted)
            {
                throw;
            }

            var (statusCode, code, message) = MapException(ex);

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var response = ApiError.Create(context, code, message);
            await context.Response.WriteAsJsonAsync(response);
        }
    }

    private static (int statusCode, string code, string message) MapException(Exception ex)
    {
        return ex switch
        {
            ValidationException validationException => (
                StatusCodes.Status400BadRequest,
                "validation_error",
                BuildValidationExceptionMessage(validationException)
            ),
            ArgumentException argumentException => (
                StatusCodes.Status400BadRequest,
                "bad_request",
                BuildArgumentExceptionMessage(argumentException)
            ),
            BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "bad_request",
                "No se pudo procesar la solicitud HTTP. Verifica el formato del cuerpo y los parametros enviados."
            ),
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "unauthorized",
                "No autorizado. Debes iniciar sesion para acceder a este recurso."
            ),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "not_found",
                "No se encontro el recurso solicitado."
            ),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "conflict",
                "No se pudieron guardar los cambios porque el recurso fue modificado por otro proceso."
            ),
            DbUpdateException dbUpdateException => (
                StatusCodes.Status409Conflict,
                "conflict",
                BuildDbUpdateMessage(dbUpdateException)
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "Ocurrio un error interno inesperado. Intenta de nuevo mas tarde."
            )
        };
    }

    private static string BuildValidationExceptionMessage(ValidationException ex)
    {
        var validationMessage = ex.ValidationResult?.ErrorMessage;

        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return validationMessage;
        }

        return "La solicitud contiene datos invalidos. Revisa los campos enviados.";
    }

    private static string BuildArgumentExceptionMessage(ArgumentException ex)
    {
        return string.IsNullOrWhiteSpace(ex.Message)
            ? "La solicitud contiene parametros invalidos."
            : ex.Message;
    }

    private static string BuildDbUpdateMessage(DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlException &&
            (sqlException.Number == 2601 || sqlException.Number == 2627))
        {
            return "No se pudieron guardar los cambios porque ya existe un registro con los mismos datos unicos.";
        }

        return "No se pudieron guardar los cambios en la base de datos.";
    }
}
