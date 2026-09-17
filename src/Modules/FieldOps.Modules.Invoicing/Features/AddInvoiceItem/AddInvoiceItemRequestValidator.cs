using FluentValidation;

namespace FieldOps.Modules.Invoicing.Features.AddInvoiceItem;

public class AddInvoiceItemRequestValidator : AbstractValidator<AddInvoiceItemRequest>
{
    public AddInvoiceItemRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Unit price must be non-negative");
    }
}
