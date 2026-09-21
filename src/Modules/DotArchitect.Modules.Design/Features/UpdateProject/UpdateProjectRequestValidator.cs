using FluentValidation;

namespace DotArchitect.Modules.Design.Features.UpdateProject;

public class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RelativePath).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.TemplateType).NotEmpty().Must(t =>
            new[] { "WebApi", "ClassLibrary", "TestProject" }.Contains(t));
        RuleFor(x => x.TargetFramework).NotEmpty().MaximumLength(50);
    }
}
