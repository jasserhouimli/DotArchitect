namespace FieldOps.Modules.Customers.Features.GetCustomers;

public record GetCustomersQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null
);
