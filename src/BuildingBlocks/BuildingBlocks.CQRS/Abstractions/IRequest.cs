namespace BuildingBlocks.CQRS.Abstractions;

/// <summary>
/// Base marker for commands and queries. <typeparamref name="TResponse"/> is what the handler returns.
/// </summary>
public interface IRequest<TResponse>;

/// <summary>A write operation. Commands mutate state and are named imperatively (CreateOrder).</summary>
public interface ICommand<TResponse> : IRequest<TResponse>;

/// <summary>A read operation. Queries never mutate state.</summary>
public interface IQuery<TResponse> : IRequest<TResponse>;
