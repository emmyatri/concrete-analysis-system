using Microsoft.AspNetCore.Http;
using Vulcan.Domain.Interfaces;

namespace Vulcan.Api.Technicians;

public sealed class CurrentTechnician(
    IHttpContextAccessor httpContextAccessor) : ICurrentTechnician
{
    public string? Name =>
        httpContextAccessor.HttpContext is { } context
            ? TechnicianHeader.Read(context.Request)?.Trim()
            : null;
}
