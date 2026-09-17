using FluentValidation;

namespace FieldOps.Modules.Inventory.Features.RecordTransaction;

public class RecordTransactionRequestValidator : AbstractValidator<RecordTransactionRequest>
{
    public RecordTransactionRequestValidator()
    {
        RuleFor(x => x.InventoryItemId)
            .NotEmpty().WithMessage("InventoryItemId is required");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero");
    }
}
