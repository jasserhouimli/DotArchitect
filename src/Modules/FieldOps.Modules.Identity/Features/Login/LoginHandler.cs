using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FieldOps.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FieldOps.Modules.Identity.Features.Login;

public static class LoginHandler
{
    public static async Task<LoginResponse?> Handle(
        LoginRequest request,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IConfiguration config,
        CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
            return null;

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);

        if (!result.Succeeded)
            return null;

        var token = GenerateToken(user, config);

        return new LoginResponse(token, Guid.Parse(user.Id), user.Email!, user.FullName);
    }

    private static string GenerateToken(User user, IConfiguration config)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key not configured")));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
