// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Components.Server.Circuits
{
    internal class CircuitRegistry
    {
        private readonly ComponentsServerOptions _options;
        private readonly ILogger _logger;
        private readonly PostEvictionCallbackRegistration _postEvictionCallback;
        private readonly ConcurrentDictionary<string, CircuitHost> _activeCircuits;
        private readonly MemoryCache _inactiveCircuits;

        public CircuitRegistry(
            IOptions<ComponentsServerOptions> options,
            ILogger<CircuitRegistry> logger)
        {
            _options = options.Value;
            _logger = logger;

            _activeCircuits = new ConcurrentDictionary<string, CircuitHost>(StringComparer.Ordinal);

            _inactiveCircuits = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = _options.MaxRetainedDisconnectedCircuits,
            });

            _postEvictionCallback = new PostEvictionCallbackRegistration
            {
                EvictionCallback = OnEntryEvicted,
            };
        }

        public void Register(CircuitHost circuitHost)
        {
            _activeCircuits.TryAdd(circuitHost.CircuitId, circuitHost);
        }

        public void MarkInactive(CircuitHost circuitHost)
        {
            if (!_activeCircuits.TryRemove(circuitHost.CircuitId, out circuitHost))
            {
                throw new InvalidOperationException($"Circuit with identifier {circuitHost.CircuitId} is not registered.");
            }

            var entryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.Add(_options.DisconnectedCircuitRetentionPeriod),
                Size = 1,
                PostEvictionCallbacks = { _postEvictionCallback },
            };

            MemoryCache.Set(circuitHost.CircuitId, circuitHost, entryOptions);

        }

        public bool TryGetCircuit(string circuitId, out CircuitHost host)
        {
            if (_activeCircuits.TryGetValue(circuitId, out host))
            {
                return true;
            }

            if (_inactiveCircuits.TryGetValue(circuitId, out host))
            {
                _activeCircuits.TryAdd(circuitId, host);
                _inactiveCircuits.Remove(circuitId);

                return true;
            }

            host = null;
            return false;
        }

        private void OnEntryEvicted(object key, object value, EvictionReason reason, object state)
        {
            switch (reason)
            {
                case EvictionReason.Expired:
                case EvictionReason.Capacity:
                    // Kick off the dispose in the background, but don't wait for it to finish.
                    _ = DisposeCircuitHost((CircuitHost)value);
                    break;

                case EvictionReason.Removed:
                    // The entry was explicitly removed as part of TryGetInactiveCircuit. Nothing to do here.
                    return;

                default:
                    Debug.Fail($"Unexpected {nameof(EvictionReason)} {reason}");
                    break;
            }
        }

        private async Task DisposeCircuitHost(CircuitHost value)
        {
            try
            {
                await value.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.UnhandledExceptionDisposingCircuitHost(ex);
            }
        }
    }
}
