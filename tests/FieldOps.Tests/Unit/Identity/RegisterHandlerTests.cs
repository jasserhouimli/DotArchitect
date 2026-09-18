using FieldOps.Modules.Identity.Domain;
using FieldOps.Modules.Identity.Features.Register;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace FieldOps.Tests.Unit.Identity;

public class RegisterHandlerTests
{
    private readonly Mock<UserManager<User>> _userManagerMock;
    private readonly RegisterHandler _handler;

    public RegisterHandlerTests()
    {
        var store = new Mock<IUserStore<User>>();
        _userManagerMock = new Mock<UserManager<User>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _handler = new RegisterHandler(_userManagerMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        var request = new RegisterRequest("test@test.com", "Test123!", "John Doe");

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);

        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<User>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(request.Email, result.Value!.Email);
        Assert.Equal(request.FullName, result.Value.FullName);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ReturnsFailure()
    {
        var request = new RegisterRequest("existing@test.com", "Test123!", "John Doe");
        var existingUser = new User { Id = Guid.NewGuid().ToString(), Email = request.Email };

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(existingUser);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("Email already registered", result.Error);
    }

    [Fact]
    public async Task Handle_InvalidPassword_ReturnsFailure()
    {
        var request = new RegisterRequest("test@test.com", "weak", "John Doe");

        _userManagerMock.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);

        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<User>(), request.Password))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }
}
