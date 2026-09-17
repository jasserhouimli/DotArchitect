using FluentValidation;

namespace FieldOps.Modules.Technicians.Features.AddTechnicianSkill;

public class AddTechnicianSkillRequestValidator : AbstractValidator<AddTechnicianSkillRequest>
{
    public AddTechnicianSkillRequestValidator()
    {
        RuleFor(x => x.SkillName)
            .NotEmpty().WithMessage("Skill name is required")
            .MaximumLength(100).WithMessage("Skill name must not exceed 100 characters");

        RuleFor(x => x.ProficiencyLevel)
            .InclusiveBetween(1, 5).WithMessage("Proficiency level must be between 1 and 5");
    }
}
