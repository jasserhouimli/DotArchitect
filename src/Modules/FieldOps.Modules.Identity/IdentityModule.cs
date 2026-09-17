using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FieldOps.Modules.Identity.Domain;
using FieldOps.Modules.Identity.Features.Register;
using FieldOps.Modules.Identity.Features.Login;
using FieldOps.Modules.Identity.Features.GetUser;
using FieldOps.Modules.Identity.Features.Logout;
using FieldOps.Modules.Identity.Features.RefreshToken;
using FieldOps.Modules.Identity.Persistence;
using FieldOps.Modules.Identity.Services;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Identity;

public static class IdentityModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("FieldOps");

        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddIdentityCore<User>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddSignInManager<SignInManager<User>>();

        builder.Services.AddValidatorsFromAssemblyContaining<IdentityDbContext>();
        builder.Services.AddScoped<IdentityDbContext>();
        builder.Services.AddScoped<TokenService>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        RegisterEndpoint.Map(app);
        LoginEndpoint.Map(app);
        GetUserEndpoint.Map(app);
        RefreshTokenEndpoint.Map(app);
        LogoutEndpoint.Map(app);
    }
}
