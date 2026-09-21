using FluentValidation;

namespace DotArchitect.Modules.Design.Features.AddProject;

public class AddProjectRequestValidator : AbstractValidator<AddProjectRequest>
{
    public AddProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RelativePath).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.TemplateType).NotEmpty().Must(t =>
            new[] { "WebApi", "ClassLibrary", "TestProject" }.Contains(t))
            .WithMessage("Template must be WebApi, ClassLibrary, or TestProject.");
        RuleFor(x => x.TargetFramework).NotEmpty().MaximumLength(50);
    }
}
