// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Components.Server.Circuits
{
    internal class CircuitClientProxy : IClientProxy
    {
        private CancellationTokenSource _clientCancellationTokenSource;

        public CircuitClientProxy(IClientProxy clientProxy, string connectionId)
        {
            Client = clientProxy;
            ConnectionId = connectionId;

            _clientCancellationTokenSource = new CancellationTokenSource();
        }

        public bool IsConnected { get; set; } = true;

        public string ConnectionId { get; private set; }

        public IClientProxy Client { get; private set; }

        public void Transfer(IClientProxy clientProxy, string connectionId)
        {
            var oldTokenSource = _clientCancellationTokenSource;
            oldTokenSource.Cancel();
            _clientCancellationTokenSource = new CancellationTokenSource();

            Client = clientProxy;
            ConnectionId = connectionId;
            IsConnected = true;
        }

        public Task SendCoreAsync(string method, object[] args, CancellationToken cancellationToken = default)
        {
            var combinedToken = _clientCancellationTokenSource.Token;
            if (cancellationToken.CanBeCanceled)
            {
                combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                    _clientCancellationTokenSource.Token,
                    cancellationToken).Token;
            }

            return Client.SendCoreAsync(method, args, combinedToken);
        }

        private class ObjectPoolPolicy : IPooledObjectPolicy<CancellationTokenSource>
        {
            public CancellationTokenSource Create() => new CancellationTokenSource();

            public bool Return(CancellationTokenSource obj)
            {
                if (obj.IsCancellationRequested)
                {
                    return false;
                }

                return true;
            }
        }
    }
}
