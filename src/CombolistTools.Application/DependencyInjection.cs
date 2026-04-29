using CombolistTools.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CombolistTools.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(new ProcessingOptions());
        services.AddSingleton<DuplicateRemoverService>();
        services.AddSingleton<FileMergeService>();
        services.AddSingleton<FileSplitService>();
        return services;
    }
}
