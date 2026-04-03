using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTunnelUI.Models;
using DevTunnelUI.Services;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace DevTunnelUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly SetupService _setupService = new();
    private readonly DevTunnelService _tunnelService = new();
    private readonly GatewayService _gatewayService = new();

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Checking DevTunnel installation...";

    [ObservableProperty]
    private TunnelInfo? _selectedTunnel;

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
        IsInstalled = await _setupService.CheckIfInstalled();
        if (IsInstalled)
        {
            StatusMessage = "DevTunnel is installed.";
            await LoadTunnels();
        }
        else
        {
            StatusMessage = "DevTunnel is not installed.";
        }
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
            IsInstalled = true;
            StatusMessage = "Installation successful.";
        }
        else
        {
            StatusMessage = "Installation failed.";
        }
        IsBusy = false;
    }

    [RelayCommand]
    private async Task Login()
    {
        IsBusy = true;
        StatusMessage = "Logging in...";
        await _setupService.Login();
        await LoadTunnels();
        IsBusy = false;
    }

    [RelayCommand]
    private void AddRule()
    {
        if (SelectedTunnel == null || string.IsNullOrEmpty(NewPath) || string.IsNullOrEmpty(NewTarget)) return;

        SelectedTunnel.Rules.Add(new GatewayRule { Path = NewPath, Destination = NewTarget });
        UpdateGateway();
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
        Tunnels.Clear();
        var tunnels = await _tunnelService.ListTunnels();
        foreach (var t in tunnels)
        {
            Tunnels.Add(t);
        }
    }
}
