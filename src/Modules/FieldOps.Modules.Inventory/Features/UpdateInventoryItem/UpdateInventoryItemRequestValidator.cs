using FluentValidation;

namespace FieldOps.Modules.Inventory.Features.UpdateInventoryItem;

public class UpdateInventoryItemRequestValidator : AbstractValidator<UpdateInventoryItemRequest>
{
    public UpdateInventoryItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(256).WithMessage("Name must not exceed 256 characters")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.Sku)
            .MaximumLength(100).WithMessage("SKU must not exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Sku));

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("Quantity must be non-negative")
            .When(x => x.Quantity.HasValue);

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Unit price must be non-negative")
            .When(x => x.UnitPrice.HasValue);

        RuleFor(x => x.ReorderLevel)
            .GreaterThanOrEqualTo(0).WithMessage("Reorder level must be non-negative")
            .When(x => x.ReorderLevel.HasValue);
    }
}
