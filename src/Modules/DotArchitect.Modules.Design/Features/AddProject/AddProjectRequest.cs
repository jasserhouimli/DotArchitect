namespace DotArchitect.Modules.Design.Features.AddProject;

public record AddProjectRequest(string Name, string RelativePath, string TemplateType, string TargetFramework = "net10.0");
