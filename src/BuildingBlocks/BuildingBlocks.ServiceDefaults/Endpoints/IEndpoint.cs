namespace BuildingBlocks.ServiceDefaults.Endpoints;

/// <summary>
/// A vertical slice's HTTP entry point. Each feature folder owns one endpoint
/// class that maps its route(s) and delegates to the slice's command/query.
/// </summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
