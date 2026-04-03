using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yarp.ReverseProxy.Configuration;

namespace DevTunnelUI.Services;

public class GatewayRule
{
    public string Path { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
}

public class TunnelMapping
{
    public string TunnelId { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public List<GatewayRule> Rules { get; set; } = new();
}

public class GatewayService
{
    private IHost? _host;
    private readonly InMemoryConfigProvider _configProvider = new();
    private readonly ConcurrentDictionary<string, TunnelMapping> _mappings = new();

    public void UpdateMapping(TunnelMapping mapping)
    {
        _mappings[mapping.TunnelId] = mapping;
        RebuildConfig();
    }

    private void RebuildConfig()
    {
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();

        foreach (var mapping in _mappings.Values)
        {
            foreach (var rule in mapping.Rules)
            {
                var routeId = $"{mapping.TunnelId}_{rule.Path.Replace("/", "_")}";
                var clusterId = $"cluster_{routeId}";

                routes.Add(new RouteConfig
                {
                    RouteId = routeId,
                    ClusterId = clusterId,
                    Match = new RouteMatch
                    {
                        Path = rule.Path
                    }
                });

                clusters.Add(new ClusterConfig
                {
                    ClusterId = clusterId,
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        { "default", new DestinationConfig { Address = rule.Destination } }
                    }
                });
            }
        }

        _configProvider.Update(routes, clusters);
    }

    public async Task Start(int port = 5000)
    {
        if (_host != null) return;

        _host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls($"http://localhost:{port}");
                webBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<IProxyConfigProvider>(_configProvider);
                    services.AddReverseProxy();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapReverseProxy();
                    });
                });
            })
            .Build();

        await _host.StartAsync();
    }

    public async Task Stop()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
            _host = null;
        }
    }
}

public class InMemoryConfigProvider : IProxyConfigProvider
{
    private volatile InMemoryConfig _config = new(new List<RouteConfig>(), new List<ClusterConfig>());

    public IProxyConfig GetConfig() => _config;

    public void Update(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        var oldConfig = _config;
        _config = new InMemoryConfig(routes, clusters);
        oldConfig.SignalChange();
    }

    private class InMemoryConfig : IProxyConfig
    {
        private readonly CancellationTokenSource _cts = new();

        public InMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        {
            Routes = routes;
            Clusters = clusters;
            ChangeToken = new Microsoft.Extensions.Primitives.CancellationChangeToken(_cts.Token);
        }

        public IReadOnlyList<RouteConfig> Routes { get; }
        public IReadOnlyList<ClusterConfig> Clusters { get; }
        public Microsoft.Extensions.Primitives.IChangeToken ChangeToken { get; }

        internal void SignalChange() => _cts.Cancel();
    }
}
