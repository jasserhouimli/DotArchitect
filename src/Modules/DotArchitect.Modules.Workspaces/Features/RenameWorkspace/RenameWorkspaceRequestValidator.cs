using FluentValidation;

namespace DotArchitect.Modules.Workspaces.Features.RenameWorkspace;

public class RenameWorkspaceRequestValidator : AbstractValidator<RenameWorkspaceRequest>
{
    public RenameWorkspaceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Workspace name is required.")
            .MaximumLength(200).WithMessage("Workspace name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");
    }
}
