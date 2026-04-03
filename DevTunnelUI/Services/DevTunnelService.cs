using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using DevTunnelUI.Models;

namespace DevTunnelUI.Services;

public class DevTunnelService
{
    public async Task<List<TunnelInfo>> ListTunnels()
    {
        var json = await RunCommandWithOutput("devtunnel", "list -j");
        if (string.IsNullOrEmpty(json)) return new List<TunnelInfo>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var tunnels = new List<TunnelInfo>();
            if (doc.RootElement.TryGetProperty("tunnels", out var tunnelsElement))
            {
                foreach (var item in tunnelsElement.EnumerateArray())
                {
                    var tunnel = new TunnelInfo
                    {
                        TunnelId = item.GetProperty("tunnelId").GetString() ?? "",
                        Name = item.GetProperty("name").GetString(),
                        Expiration = item.GetProperty("tunnelExpiration").GetString(),
                        IsActive = true
                    };

                    if (item.TryGetProperty("ports", out var portsElement) && portsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var port in portsElement.EnumerateArray())
                        {
                            if (port.TryGetProperty("uri", out var uriElement))
                            {
                                tunnel.PublicUrl = uriElement.GetString() ?? "";
                                break; 
                            }
                        }
                    }
                    tunnels.Add(tunnel);
                }
            }
            return tunnels;
        }
        catch (JsonException)
        {
            return new List<TunnelInfo>();
        }
    }

    public async Task<bool> CreateTunnel(string name, string expiration)
    {
        var args = $"create";
        if (!string.IsNullOrEmpty(name)) args += $" --name {name}";
        if (!string.IsNullOrEmpty(expiration)) args += $" -e {expiration}";
        
        return await RunCommand("devtunnel", args);
    }

    public async Task<bool> DeleteTunnel(string id)
    {
        return await RunCommand("devtunnel", $"delete {id} --yes");
    }

    public async Task<bool> AddPort(string id, int port)
    {
        return await RunCommand("devtunnel", $"port create {id} -p {port}");
    }

    public async Task<Process?> Host(string id)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "devtunnel",
                    Arguments = $"host {id}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            return process;
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> RunCommand(string fileName, string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> RunCommandWithOutput(string fileName, string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }
        catch
        {
            return string.Empty;
        }
    }
}
