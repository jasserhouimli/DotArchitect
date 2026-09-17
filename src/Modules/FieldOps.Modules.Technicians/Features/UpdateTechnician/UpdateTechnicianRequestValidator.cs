using FluentValidation;

namespace FieldOps.Modules.Technicians.Features.UpdateTechnician;

public class UpdateTechnicianRequestValidator : AbstractValidator<UpdateTechnicianRequest>
{
    public UpdateTechnicianRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.HourlyRate)
            .GreaterThan(0).WithMessage("Hourly rate must be greater than 0");
    }
}
