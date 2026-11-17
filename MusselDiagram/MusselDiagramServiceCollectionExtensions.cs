namespace MusselDiagram;

using Microsoft.Extensions.DependencyInjection;

public static class MusselDiagramServiceCollectionExtensions
{
    public static IServiceCollection AddMusselDiagram(this IServiceCollection services, Action<MusselDiagramOptions>? configure = null)
    {
        var options = new MusselDiagramOptions();
        configure?.Invoke(options);
        return services.AddSingleton(options);
    }
}
