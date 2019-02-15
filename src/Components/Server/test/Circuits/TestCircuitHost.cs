// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Components.Browser;
using Microsoft.AspNetCore.Components.Browser.Rendering;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;

namespace Microsoft.AspNetCore.Components.Server.Circuits
{
    internal static class TestCircuitHost
    {
        public static CircuitHost Create(
            IServiceScope serviceScope = null,
            RemoteRenderer remoteRenderer = null,
            CircuitHandler[] handlers = null)
        {
            serviceScope = serviceScope ?? Mock.Of<IServiceScope>();
            var clientProxy = new DelegatingClientProxy { Client = Mock.Of<IClientProxy>() };
            var renderRegistry = new RendererRegistry();
            var jsRuntime = Mock.Of<IJSRuntime>();

            if (remoteRenderer == null)
            {
                remoteRenderer = new RemoteRenderer(
                    Mock.Of<IServiceProvider>(),
                    new RendererRegistry(),
                    jsRuntime,
                    clientProxy,
                    Renderer.CreateDefaultDispatcher(),
                    NullLogger.Instance);
            }

            handlers = handlers ?? Array.Empty<CircuitHandler>();
            return new CircuitHost(
                serviceScope,
                clientProxy,
                renderRegistry,
                remoteRenderer,
                configure: _ => { },
                jsRuntime: jsRuntime,
                handlers);
        }
    }
}
