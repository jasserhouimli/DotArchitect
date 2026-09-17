namespace FieldOps.Modules.Customers.Features.CreateCustomer;

public record CreateCustomerRequest(
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? ZipCode,
    string? Notes
);
