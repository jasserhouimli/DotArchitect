using FluentValidation;

namespace FieldOps.Modules.Invoicing.Features.UpdateInvoice;

public class UpdateInvoiceRequestValidator : AbstractValidator<UpdateInvoiceRequest>
{
    public UpdateInvoiceRequestValidator()
    {
        RuleFor(x => x.TaxRate)
            .InclusiveBetween(0, 100).WithMessage("Tax rate must be between 0 and 100")
            .When(x => x.TaxRate.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
