using FieldOps.Modules.Identity.Domain;
using FieldOps.Modules.Identity.Features.Login;
using FieldOps.Modules.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace FieldOps.Tests.Unit.Identity;

public class LoginHandlerTests
{
    private readonly Mock<UserManager<User>> _userManagerMock;
    private readonly Mock<SignInManager<User>> _signInManagerMock;
    private readonly Mock<TokenService> _tokenServiceMock;
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        var store = new Mock<IUserStore<User>>();
        _userManagerMock = new Mock<UserManager<User>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var contextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var userClaimsPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<User>>();
        var options = new Mock<Microsoft.Extensions.Options.IOptions<IdentityOptions>>();
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<SignInManager<User>>>();

        _signInManagerMock = new Mock<SignInManager<User>>(
            _userManagerMock.Object, contextAccessor.Object, userClaimsPrincipalFactory.Object,
            options.Object, logger.Object, null!, null!);

        _tokenServiceMock = new Mock<TokenService>(null!, null!);
        _handler = new LoginHandler(_userManagerMock.Object, _signInManagerMock.Object, _tokenServiceMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsTokens()
    {
        var request = new LoginRequest("test@test.com", "Test123!");
        var user = new User { Id = Guid.NewGuid().ToString(), Email = request.Email, FullName = "John" };

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, request.Password, false))
            .ReturnsAsync(SignInResult.Success);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshToken { Token = "refresh-token" });

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("access-token", result.Value!.AccessToken);
        Assert.Equal("refresh-token", result.Value.RefreshToken);
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsFailure()
    {
        var request = new LoginRequest("test@test.com", "wrong");
        var user = new User { Id = Guid.NewGuid().ToString(), Email = request.Email };

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, request.Password, false))
            .ReturnsAsync(SignInResult.Failed);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task Handle_NonexistentUser_ReturnsFailure()
    {
        var request = new LoginRequest("noone@test.com", "Test123!");

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }
}
