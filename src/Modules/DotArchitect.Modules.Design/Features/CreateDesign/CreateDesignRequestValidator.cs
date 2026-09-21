using FluentValidation;

namespace DotArchitect.Modules.Design.Features.CreateDesign;

public class CreateDesignRequestValidator : AbstractValidator<CreateDesignRequest>
{
    public CreateDesignRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Design name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");
    }
}
