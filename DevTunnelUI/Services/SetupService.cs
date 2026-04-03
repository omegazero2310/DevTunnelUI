using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevTunnelUI.Services;

public class SetupService
{
    public async Task<bool> CheckIfInstalled()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "devtunnel",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
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

    public async Task<bool> InstallDevTunnel()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await RunCommand("winget", "install Microsoft.devtunnel");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await RunCommand("curl", "-sL https://aka.ms/install-devtunnel | bash");
        }
        return false;
    }

    public async Task<bool> Login()
    {
        // For login, we need to show the terminal or capture the output if it requires browser interaction.
        // On Windows, starting 'devtunnel user login' usually opens a browser.
        return await RunCommand("devtunnel", "user login");
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
                    UseShellExecute = true, // Use shell execute for interactive commands like login/install
                    CreateNoWindow = false
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
}
