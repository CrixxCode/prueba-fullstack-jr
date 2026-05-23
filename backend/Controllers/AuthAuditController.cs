using backend.Data;
using backend.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/audit/auth")]
[Authorize(Roles = "admin")]
public class AuthAuditController : ApiControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _context;

    public AuthAuditController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<AuthAuditLogListResponseDto>> GetAuthAuditLogs(
        [FromQuery] AuthAuditQueryDto queryDto
    )
    {
        if (queryDto.FromUtc.HasValue &&
            queryDto.ToUtc.HasValue &&
            queryDto.FromUtc.Value > queryDto.ToUtc.Value)
        {
            return BadRequestError(
                "El rango de fechas es invalido. fromUtc no puede ser mayor que toUtc.",
                code: "validation_error"
            );
        }

        var page = queryDto.Page <= 0 ? 1 : queryDto.Page;
        var size = queryDto.Size <= 0 ? DefaultPageSize : queryDto.Size;

        if (size > MaxPageSize)
        {
            size = MaxPageSize;
        }

        var query = _context.AuthAuditLogs
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryDto.EventType))
        {
            var eventType = queryDto.EventType.Trim().ToLowerInvariant();
            query = query.Where(log => log.EventType == eventType);
        }

        if (queryDto.IsSuccess.HasValue)
        {
            query = query.Where(log => log.IsSuccess == queryDto.IsSuccess.Value);
        }

        if (queryDto.UserId.HasValue)
        {
            query = query.Where(log => log.UserId == queryDto.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryDto.Email))
        {
            var normalizedEmail = queryDto.Email.Trim().ToLowerInvariant();
            query = query.Where(log => log.Email == normalizedEmail);
        }

        if (queryDto.FromUtc.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= queryDto.FromUtc.Value);
        }

        if (queryDto.ToUtc.HasValue)
        {
            query = query.Where(log => log.CreatedAt <= queryDto.ToUtc.Value);
        }

        var totalItems = await query.CountAsync();

        var sortedQuery = ApplySorting(query, queryDto.SortBy, queryDto.SortDir);

        var logs = (await sortedQuery
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync())
            .Select(log => new AuthAuditLogResponseDto
            {
                Id = log.Id,
                EventType = log.EventType,
                IsSuccess = log.IsSuccess,
                UserId = log.UserId,
                Email = log.Email,
                FailureReason = log.FailureReason,
                IpAddress = log.IpAddress,
                UserAgent = log.UserAgent,
                CreatedAt = DateTime.SpecifyKind(log.CreatedAt, DateTimeKind.Utc)
            })
            .ToList();

        var response = new AuthAuditLogListResponseDto
        {
            Page = page,
            Size = size,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)size),
            Items = logs
        };

        return Ok(response);
    }

    private static IOrderedQueryable<backend.Models.AuthAuditLog> ApplySorting(
        IQueryable<backend.Models.AuthAuditLog> query,
        string? sortBy,
        string? sortDir
    )
    {
        var normalizedSortBy = string.IsNullOrWhiteSpace(sortBy)
            ? "createdat"
            : sortBy.Trim().ToLowerInvariant();

        var isDescending = !string.Equals(
            sortDir?.Trim(),
            "asc",
            StringComparison.OrdinalIgnoreCase
        );

        return normalizedSortBy switch
        {
            "eventtype" => isDescending
                ? query.OrderByDescending(log => log.EventType).ThenByDescending(log => log.Id)
                : query.OrderBy(log => log.EventType).ThenBy(log => log.Id),
            "issuccess" => isDescending
                ? query.OrderByDescending(log => log.IsSuccess).ThenByDescending(log => log.Id)
                : query.OrderBy(log => log.IsSuccess).ThenBy(log => log.Id),
            "email" => isDescending
                ? query.OrderByDescending(log => log.Email).ThenByDescending(log => log.Id)
                : query.OrderBy(log => log.Email).ThenBy(log => log.Id),
            "userid" => isDescending
                ? query.OrderByDescending(log => log.UserId).ThenByDescending(log => log.Id)
                : query.OrderBy(log => log.UserId).ThenBy(log => log.Id),
            "created_at" or "createdat" => isDescending
                ? query.OrderByDescending(log => log.CreatedAt).ThenByDescending(log => log.Id)
                : query.OrderBy(log => log.CreatedAt).ThenBy(log => log.Id),
            _ => isDescending
                ? query.OrderByDescending(log => log.CreatedAt).ThenByDescending(log => log.Id)
                : query.OrderBy(log => log.CreatedAt).ThenBy(log => log.Id)
        };
    }
}
