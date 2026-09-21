using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DotArchitect.Modules.Identity.Domain;
using DotArchitect.Modules.Identity.Features.Register;
using DotArchitect.Modules.Identity.Features.Login;
using DotArchitect.Modules.Identity.Features.GetUser;
using DotArchitect.Modules.Identity.Features.Logout;
using DotArchitect.Modules.Identity.Features.RefreshToken;
using DotArchitect.Modules.Identity.Persistence;
using DotArchitect.Modules.Identity.Services;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Identity;

public static class IdentityModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("DotArchitect");

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

        builder.Services.AddScoped<RegisterHandler>();
        builder.Services.AddScoped<LoginHandler>();
        builder.Services.AddScoped<GetUserHandler>();
        builder.Services.AddScoped<RefreshTokenHandler>();
        builder.Services.AddScoped<LogoutHandler>();
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
