using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTunnelUI.Models;
using DevTunnelUI.Services;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace DevTunnelUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly SetupService _setupService = new();
    private readonly DevTunnelService _tunnelService = new();
    private readonly GatewayService _gatewayService = new();

    private const string GatewayConfigPath = "gateway_configs.json";
    private readonly Dictionary<string, Process> _runningHosts = new();
    private Dictionary<string, TunnelMapping> _savedMappings = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Starting up...";

    [ObservableProperty]
    private bool _isInitializing = true;

    [ObservableProperty]
    private bool _isHostReady;

    [ObservableProperty]
    private bool _needsInstall;

    [ObservableProperty]
    private bool _needsLogin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedTunnel))]
    private TunnelInfo? _selectedTunnel;

    public bool HasSelectedTunnel => SelectedTunnel != null;

    [ObservableProperty]
    private string _newHostName = "";

    [ObservableProperty]
    private int _newHostPort = 8080;

    [ObservableProperty]
    private bool _isPersistentUrl;

    [ObservableProperty]
    private string _newPath = "/";

    [ObservableProperty]
    private string _newTarget = "http://localhost:5000";

    public ObservableCollection<TunnelInfo> Tunnels { get; } = new();

    public MainViewModel()
    {
        _ = CheckStatus();
    }

    [RelayCommand]
    private async Task CheckStatus()
    {
        IsBusy = true;
        IsInitializing = true;
        IsHostReady = false;
        StatusMessage = "Checking DevTunnel installation...";
        
        bool isInstalled = await _setupService.CheckIfInstalled();
        if (!isInstalled)
        {
            NeedsInstall = true;
            NeedsLogin = false;
            StatusMessage = "DevTunnel CLI is missing. Please install.";
            IsBusy = false;
            return;
        }

        NeedsInstall = false;
        NeedsLogin = true;
        StatusMessage = "DevTunnel CLI is installed. Please log in.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task Install()
    {
        IsBusy = true;
        StatusMessage = "Installing DevTunnel...";
        var success = await _setupService.InstallDevTunnel();
        if (success)
        {
            NeedsInstall = false;
            NeedsLogin = true;
            StatusMessage = "Installation successful. Please log in.";
        }
        else
        {
            StatusMessage = "Installation failed. Please try again.";
        }
        IsBusy = false;
    }

    [RelayCommand]
    private async Task Login()
    {
        IsBusy = true;
        StatusMessage = "Logging in...";
        await _setupService.Login();
        
        IsInitializing = false;
        IsHostReady = true;
        StatusMessage = "Ready.";
        
        await LoadGatewayConfigs();
        await LoadTunnels();
        
        IsBusy = false;
    }

    [RelayCommand]
    private async Task CreateHost()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = "Creating host...";

        var oldTunnels = await _tunnelService.ListTunnels();
        await _tunnelService.CreateTunnel(NewHostName, null, IsPersistentUrl);
        var newTunnels = await _tunnelService.ListTunnels();

        // Find the newly created tunnel ID
        var newTunnel = newTunnels.FirstOrDefault(nt => !oldTunnels.Any(ot => ot.TunnelId == nt.TunnelId));
        string? tunnelId = newTunnel?.TunnelId;

        // If we didn't find a new one (maybe updating existing), fallback to matching by name
        if (string.IsNullOrEmpty(tunnelId))
        {
             var matched = newTunnels.FirstOrDefault(t => !string.IsNullOrEmpty(NewHostName) && t.TunnelId.Contains(NewHostName));
             tunnelId = matched?.TunnelId;
        }

        if (!string.IsNullOrEmpty(tunnelId))
        {
            await _tunnelService.AddPort(tunnelId, NewHostPort);
            var process = await _tunnelService.Host(tunnelId);
            if (process != null)
            {
                _runningHosts[tunnelId] = process;
            }
        }

        await LoadTunnels();
        StatusMessage = "Host created.";
        IsBusy = false;

        // Reset inputs
        NewHostName = "";
        IsPersistentUrl = false;
    }

    [RelayCommand]
    private async Task ShutdownHost(TunnelInfo tunnel)
    {
        if (tunnel == null) return;
        IsBusy = true;
        StatusMessage = "Shutting down host...";

        if (_runningHosts.TryGetValue(tunnel.TunnelId, out var process))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch { }
            _runningHosts.Remove(tunnel.TunnelId);
        }

        await _tunnelService.DeleteTunnel(tunnel.TunnelId);

        if (SelectedTunnel?.TunnelId == tunnel.TunnelId)
        {
            SelectedTunnel = null;
        }

        await LoadTunnels();
        StatusMessage = "Host shut down.";
        IsBusy = false;
    }

    [RelayCommand]
    private void AddRule()
    {
        if (SelectedTunnel == null || string.IsNullOrEmpty(NewPath) || string.IsNullOrEmpty(NewTarget)) return;

        SelectedTunnel.Rules.Add(new GatewayRule { Path = NewPath, Destination = NewTarget });
        UpdateGateway();
        
        NewPath = "/";
        NewTarget = "http://localhost:5000";
    }

    [RelayCommand]
    private void RemoveRule(GatewayRule rule)
    {
        if (SelectedTunnel == null) return;
        SelectedTunnel.Rules.Remove(rule);
        UpdateGateway();
    }

    private void UpdateGateway()
    {
        foreach (var t in Tunnels)
        {
            if (t.Rules.Any())
            {
                _gatewayService.UpdateMapping(new TunnelMapping
                {
                    TunnelId = t.TunnelId,
                    PublicUrl = t.PublicUrl ?? "",
                    Rules = t.Rules.ToList()
                });
            }
        }
    }

    [RelayCommand]
    private async Task StartGateway()
    {
        StatusMessage = "Starting Gateway...";
        await _gatewayService.Start();
        StatusMessage = "Gateway running on port 5000.";
    }

    private async Task LoadTunnels()
    {
        var fetchedTunnels = await _tunnelService.ListTunnels();
        
        var toRemove = Tunnels.Where(t => !fetchedTunnels.Any(ft => ft.TunnelId == t.TunnelId)).ToList();
        foreach (var t in toRemove) Tunnels.Remove(t);

        foreach (var ft in fetchedTunnels)
        {
            var existing = Tunnels.FirstOrDefault(t => t.TunnelId == ft.TunnelId);
            if (existing != null)
            {
                existing.Expiration = ft.Expiration;
                existing.PublicUrl = ft.PublicUrl;
            }
            else
            {
                if (_savedMappings.TryGetValue(ft.TunnelId, out var savedMapping))
                {
                    foreach (var rule in savedMapping.Rules)
                    {
                        ft.Rules.Add(rule);
                    }
                }
                Tunnels.Add(ft);
            }
        }
        UpdateGateway();
    }

    [RelayCommand]
    private async Task SaveGatewayConfigs()
    {
        try
        {
            var mappings = Tunnels.Where(t => t.Rules.Any())
                .Select(t => new TunnelMapping
                {
                    TunnelId = t.TunnelId,
                    PublicUrl = t.PublicUrl ?? "",
                    Rules = t.Rules.ToList()
                }).ToList();

            var json = JsonSerializer.Serialize(mappings);
            await File.WriteAllTextAsync(GatewayConfigPath, json);
            StatusMessage = "Gateway routing configuration saved.";
        }
        catch
        {
            StatusMessage = "Failed to save gateway config.";
        }
    }

    [RelayCommand]
    private async Task CopyUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                await desktop.MainWindow.Clipboard!.SetTextAsync(url);
                StatusMessage = "URL copied to clipboard.";
            }
        }
        catch { }
    }

    private async Task LoadGatewayConfigs()
    {
        try
        {
            if (File.Exists(GatewayConfigPath))
            {
                var json = await File.ReadAllTextAsync(GatewayConfigPath);
                var mappings = JsonSerializer.Deserialize<List<TunnelMapping>>(json);
                if (mappings != null)
                {
                    _savedMappings = mappings.ToDictionary(m => m.TunnelId);
                }
            }
        }
        catch { }
    }
}
