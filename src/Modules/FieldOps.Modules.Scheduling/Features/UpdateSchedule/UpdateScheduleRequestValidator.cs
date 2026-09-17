using FluentValidation;

namespace FieldOps.Modules.Scheduling.Features.UpdateSchedule;

public class UpdateScheduleRequestValidator : AbstractValidator<UpdateScheduleRequest>
{
    public UpdateScheduleRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(256).WithMessage("Title must not exceed 256 characters")
            .When(x => !string.IsNullOrEmpty(x.Title));

        RuleFor(x => x.EndTime)
            .GreaterThan(x => x.StartTime).WithMessage("EndTime must be after StartTime")
            .When(x => x.StartTime.HasValue && x.EndTime.HasValue);

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
