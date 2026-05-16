using System.Reflection;

namespace PartnerIntegrationBFF.Shared.Extensions;

public interface IEndpoint {
    void Map(IEndpointRouteBuilder app);
}

public static class EndpointExtensions {
    public static void MapEndpoints(this WebApplication app, Assembly assembly) {
        var endpointTypes = assembly.GetTypes()
            .Where(t => typeof(IEndpoint).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in endpointTypes) {
            var endpoint = (IEndpoint)Activator.CreateInstance(type)!;
            endpoint.Map(app);
        }
    }
}
