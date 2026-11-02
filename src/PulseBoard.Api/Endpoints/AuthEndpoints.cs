using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PulseBoard.Api.Auth;
using PulseBoard.Api.Contracts;
using PulseBoard.Api.Domain;
using PulseBoard.Api.Infrastructure;

namespace PulseBoard.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ValidationProblem>> (
            LoginRequest request, AppDbContext db, TokenService tokens, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["credentials"] = ["Username and password are required."],
                });
            }

            var user = await db.Users.SingleOrDefaultAsync(u => u.Username == request.Username.ToLowerInvariant(), ct);
            if (user is null)
            {
                return TypedResults.Unauthorized();
            }

            var verdict = new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verdict == PasswordVerificationResult.Failed)
            {
                return TypedResults.Unauthorized();
            }

            var (token, expiresAt) = tokens.Issue(user);
            return TypedResults.Ok(new LoginResponse(token, user.Username, user.Role, expiresAt));
        })
        .WithName("Login")
        .WithSummary("Exchange username and password for a JWT")
        .AllowAnonymous();

        group.MapGet("/me", (HttpContext http) => TypedResults.Ok(new
        {
            username = http.User.Identity?.Name,
            role = http.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
        }))
        .WithName("Me")
        .WithSummary("Who am I, according to the token")
        .RequireAuthorization(Policies.CanView);

        return app;
    }
}
