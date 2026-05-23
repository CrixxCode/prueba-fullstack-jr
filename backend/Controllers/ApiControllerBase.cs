using backend.Common;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected BadRequestObjectResult BadRequestError(
        string message,
        string code = "bad_request",
        IDictionary<string, string[]>? errors = null
    )
    {
        return BadRequest(ApiError.Create(HttpContext, code, message, errors));
    }

    protected UnauthorizedObjectResult UnauthorizedError(
        string message,
        string code = "unauthorized"
    )
    {
        return Unauthorized(ApiError.Create(HttpContext, code, message));
    }

    protected NotFoundObjectResult NotFoundError(
        string message,
        string code = "not_found"
    )
    {
        return NotFound(ApiError.Create(HttpContext, code, message));
    }

    protected ConflictObjectResult ConflictError(
        string message,
        string code = "conflict"
    )
    {
        return Conflict(ApiError.Create(HttpContext, code, message));
    }

    protected ObjectResult ForbiddenError(
        string message = "No tienes permisos para realizar esta accion.",
        string code = "forbidden"
    )
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            ApiError.Create(HttpContext, code, message)
        );
    }
}
