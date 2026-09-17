using FluentValidation;

namespace FieldOps.Modules.Invoicing.Features.CreateInvoice;

public class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required");

        RuleFor(x => x.TaxRate)
            .InclusiveBetween(0, 100).WithMessage("Tax rate must be between 0 and 100");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
