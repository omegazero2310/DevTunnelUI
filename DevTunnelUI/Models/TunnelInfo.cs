using System.Collections.Generic;
using System.Collections.ObjectModel;
using DevTunnelUI.Services;

namespace DevTunnelUI.Models;

public class TunnelInfo
{
    public string TunnelId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? PublicUrl { get; set; }
    public string? Expiration { get; set; }
    public bool IsActive { get; set; }
    public List<int> Ports { get; set; } = new();
    public ObservableCollection<GatewayRule> Rules { get; set; } = new();
}
