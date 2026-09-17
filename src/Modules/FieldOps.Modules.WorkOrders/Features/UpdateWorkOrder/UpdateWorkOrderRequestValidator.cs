using FluentValidation;

namespace FieldOps.Modules.WorkOrders.Features.UpdateWorkOrder;

public class UpdateWorkOrderRequestValidator : AbstractValidator<UpdateWorkOrderRequest>
{
    public UpdateWorkOrderRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(256).WithMessage("Title must not exceed 256 characters")
            .When(x => !string.IsNullOrEmpty(x.Title));

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
