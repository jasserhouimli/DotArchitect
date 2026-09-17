using FluentValidation;

namespace FieldOps.Modules.WorkOrders.Features.AssignTechnician;

public class AssignTechnicianRequestValidator : AbstractValidator<AssignTechnicianRequest>
{
    public AssignTechnicianRequestValidator()
    {
        RuleFor(x => x.TechnicianId)
            .NotEmpty().WithMessage("TechnicianId is required");
    }
}
