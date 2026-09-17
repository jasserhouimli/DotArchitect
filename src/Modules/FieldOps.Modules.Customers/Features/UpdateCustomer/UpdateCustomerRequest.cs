namespace FieldOps.Modules.Customers.Features.UpdateCustomer;

public record UpdateCustomerRequest(
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? ZipCode,
    string? Notes
);
