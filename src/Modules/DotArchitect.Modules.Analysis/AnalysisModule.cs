using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DotArchitect.Modules.Analysis.Persistence;
using DotArchitect.Modules.Analysis.Features.UploadSolution;
using DotArchitect.Modules.Analysis.Features.GetAnalysis;
using DotArchitect.Modules.Analysis.Features.GetAnalysisProjects;
using DotArchitect.Modules.Analysis.Features.GetAnalysisWarnings;
using DotArchitect.Modules.Analysis.Features.ListAnalyses;

namespace DotArchitect.Modules.Analysis;

public static class AnalysisModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("DotArchitect");

        builder.Services.AddDbContext<AnalysisDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddScoped<AnalysisDbContext>();
        builder.Services.AddScoped<UploadSolutionHandler>();
        builder.Services.AddScoped<GetAnalysisHandler>();
        builder.Services.AddScoped<GetAnalysisProjectsHandler>();
        builder.Services.AddScoped<GetAnalysisWarningsHandler>();
        builder.Services.AddScoped<ListAnalysesHandler>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        UploadSolutionEndpoint.Map(app);
        GetAnalysisEndpoint.Map(app);
        ListAnalysesEndpoint.Map(app);
    }
}
