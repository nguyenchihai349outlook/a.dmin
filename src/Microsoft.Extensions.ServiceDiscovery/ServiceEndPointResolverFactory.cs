// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.ServiceDiscovery.Abstractions;
using Microsoft.Extensions.ServiceDiscovery.PassThrough;

namespace Microsoft.Extensions.ServiceDiscovery;

/// <summary>
/// Creates service endpoint resolvers.
/// </summary>
public partial class ServiceEndPointResolverFactory(
    IEnumerable<IServiceEndPointResolverProvider> resolvers,
    ILogger<CompositeServiceEndPointResolver> resolverLogger,
    IOptions<ServiceEndPointResolverOptions> options,
    TimeProvider timeProvider)
{
    private readonly IServiceEndPointResolverProvider[] _resolverProviders = resolvers
        .Where(r => r is not PassThroughServiceEndPointResolverProvider)
        .Concat(resolvers.Where(static r => r is PassThroughServiceEndPointResolverProvider)).ToArray();
    private readonly ILogger<CompositeServiceEndPointResolver> _logger = resolverLogger;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IOptions<ServiceEndPointResolverOptions> _options = options;

    /// <summary>
    /// Creates a service endpoint resolver for the provided service name.
    /// </summary>
    public CompositeServiceEndPointResolver CreateResolver(string serviceName)
    {
        ArgumentNullException.ThrowIfNull(serviceName);

        List<IServiceEndPointResolver>? resolvers = null;
        foreach (var factory in _resolverProviders)
        {
            if (factory.TryCreateResolver(serviceName, out var resolver))
            {
                resolvers ??= [];
                resolvers.Add(resolver);
            }
        }

        if (resolvers is not { Count: > 0 })
        {
            throw new InvalidOperationException($"No resolver which supports the provided service name, '{serviceName}', has been configured.");
        }

        Log.CreatingResolver(_logger, serviceName, resolvers);
        return new CompositeServiceEndPointResolver(
            resolvers: [.. resolvers],
            logger: _logger,
            serviceName: serviceName,
            timeProvider: _timeProvider,
            options: _options);
    }
}
