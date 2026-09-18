using FieldOps.Modules.Customers.Domain;
using FieldOps.Modules.Customers.Features.CreateCustomer;
using FieldOps.Modules.Customers.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Tests.Unit.Customers;

public class CreateCustomerHandlerTests
{
    private readonly CustomersDbContext _db;
    private readonly CreateCustomerHandler _handler;

    public CreateCustomerHandlerTests()
    {
        var options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new CustomersDbContext(options);
        _handler = new CreateCustomerHandler(_db);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesCustomer()
    {
        var request = new CreateCustomerRequest(
            "Acme Corp", "acme@test.com", "555-0100",
            "123 Main St", "Springfield", "IL", "62701", "Important client");

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("Acme Corp", result.Value!.Name);
        Assert.Equal("acme@test.com", result.Value.Email);

        var saved = await _db.Customers.FindAsync(result.Value.Id);
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task Handle_MinimalFields_CreatesCustomer()
    {
        var request = new CreateCustomerRequest(
            "Simple Corp", null, null, null, null, null, null, null);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Email);
        Assert.Null(result.Value.Phone);
    }
}
