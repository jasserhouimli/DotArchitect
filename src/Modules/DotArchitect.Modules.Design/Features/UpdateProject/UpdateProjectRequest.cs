namespace DotArchitect.Modules.Design.Features.UpdateProject;

public record UpdateProjectRequest(string Name, string RelativePath, string TemplateType, string TargetFramework);
