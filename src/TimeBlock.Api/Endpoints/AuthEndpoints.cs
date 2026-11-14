using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeBlock.Api.Services;
using TimeBlock.Core.Contracts;
using TimeBlock.Core.Data;
using TimeBlock.Core.Entities;
using TimeBlock.Core.Services;

namespace TimeBlock.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async Task<Results<Ok<AuthResponse>, ValidationProblem, Conflict<string>>> (
                RegisterRequest request,
                TimeBlockDbContext db,
                TokenService tokens) =>
            {
                var email = request.Email.Trim().ToLowerInvariant();
                if (!email.Contains('@'))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["A valid email is required."] });
                }

                if (request.Password.Length < 8)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["password"] = ["Password must be at least 8 characters."] });
                }

                if (await db.Users.AnyAsync(u => u.Email == email))
                {
                    return TypedResults.Conflict("Email already registered.");
                }

                var user = new User
                {
                    Email = email,
                    DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? email : request.DisplayName.Trim(),
                    PasswordHash = PasswordHasher.Hash(request.Password)
                };
                db.Users.Add(user);

                if (!string.IsNullOrWhiteSpace(request.InviteCode))
                {
                    var target = await db.Groups.FirstOrDefaultAsync(g => g.InviteCode == request.InviteCode.Trim().ToUpperInvariant());
                    if (target is not null)
                    {
                        db.GroupMembers.Add(new GroupMember { GroupId = target.Id, UserId = user.Id });
                    }
                }

                await db.SaveChangesAsync();
                return TypedResults.Ok(new AuthResponse(tokens.CreateToken(user), user.ToUserDto()));
            })
            .AllowAnonymous();

        group.MapPost("/login", async Task<Results<Ok<AuthResponse>, NotFound>> (
                LoginRequest request,
                TimeBlockDbContext db,
                TokenService tokens) =>
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLowerInvariant());
                if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
                {
                    return TypedResults.NotFound();
                }

                return TypedResults.Ok(new AuthResponse(tokens.CreateToken(user), user.ToUserDto()));
            })
            .AllowAnonymous();

        group.MapGet("/me", async (CurrentUser me, TimeBlockDbContext db) =>
        {
            var user = await db.Users.FindAsync(me.UserId);
            return user is null ? Results.NotFound() : Results.Ok(user.ToUserDto());
        });
    }
}
