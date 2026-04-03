using Avalonia.Controls;
using Avalonia.Input;
using DevTunnelUI.ViewModels;
using DevTunnelUI.Models;

namespace DevTunnelUI.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private void OnTunnelSelected(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is TunnelInfo tunnel && DataContext is MainViewModel vm)
        {
            vm.SelectedTunnel = tunnel;
        }
    }
}
