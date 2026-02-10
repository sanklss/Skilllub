using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SkilllubLearnbox.Services;
using Microsoft.Extensions.Logging;

namespace SkilllubLearnbox.Attributes;
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeRolesAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _allowedRoles;

    public AuthorizeRolesAttribute(params string[] roles)
    {
        _allowedRoles = roles;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var jwtService = context.HttpContext.RequestServices.GetService<JwtService>();
        var logger = context.HttpContext.RequestServices.GetService<ILogger<AuthorizeRolesAttribute>>();

        try
        {
            var token = context.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            if (string.IsNullOrEmpty(token))
            {
                logger?.LogWarning("Access attempt without token to {Endpoint}", context.HttpContext.Request.Path);
                context.Result = new UnauthorizedObjectResult(new { success = false, error = "Токен не предоставлен" });
                return;
            }

            if (jwtService == null)
            {
                logger?.LogError("JwtService is not available in DI container");
                context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
                return;
            }

            var principal = jwtService.ValidateToken(token);
            if (principal == null)
            {
                logger?.LogWarning("Invalid token provided for {Endpoint}", context.HttpContext.Request.Path);
                context.Result = new UnauthorizedObjectResult(new { success = false, error = "Невалидный токен" });
                return;
            }

            var userRole = principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(userRole) || !_allowedRoles.Contains(userRole))
            {
                logger?.LogWarning("Access forbidden for role {UserRole} to {Endpoint}. Required roles: {RequiredRoles}",
                    userRole, context.HttpContext.Request.Path, string.Join(", ", _allowedRoles));
                context.Result = new ForbidResult();
                return;
            }

            context.HttpContext.Items["UserId"] = principal.FindFirst("userId")?.Value;
            context.HttpContext.Items["UserRole"] = userRole;

            logger?.LogDebug("User {UserId} with role {UserRole} authorized for {Endpoint}",
                context.HttpContext.Items["UserId"], userRole, context.HttpContext.Request.Path);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Authorization error for endpoint {Endpoint}", context.HttpContext.Request.Path);
            context.Result = new UnauthorizedObjectResult(new { success = false, error = "Ошибка авторизации" });
        }
    }
}