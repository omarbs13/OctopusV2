using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using Pos.Application.Licensing;

namespace Pos.Infrastructure.Licensing;

/// <summary>
/// ID de máquina: SHA-256 de una sal fija más el identificador del sistema operativo (Linux
/// <c>/etc/machine-id</c>, Windows <c>MachineGuid</c>) o, como reserva, la MAC de la primera interfaz
/// física (011, research §1). Se calcula una vez.
/// </summary>
public sealed class MachineIdProvider : IMachineIdProvider
{
    private const string Salt = "Pos.MachineId.v1:";

    private readonly Lazy<string> _id = new(Compute);

    public string GetMachineId() => _id.Value;

    internal static string Hash(string identifier) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Salt + identifier))).ToLowerInvariant();

    private static string Compute() => Hash(ReadOperatingSystemId() ?? ReadMacAddress() ?? Environment.MachineName);

    private static string? ReadOperatingSystemId()
    {
        if (OperatingSystem.IsWindows())
        {
            return ReadWindowsMachineGuid();
        }

        foreach (var file in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
        {
            try
            {
                var text = File.ReadAllText(file).Trim();
                if (text.Length > 0)
                {
                    return text;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Se prueba la siguiente fuente.
            }
        }

        return null;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static string? ReadWindowsMachineGuid()
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid") as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static string? ReadMacAddress()
    {
        try
        {
            return System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType is System.Net.NetworkInformation.NetworkInterfaceType.Ethernet
                    or System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .Select(n => n.GetPhysicalAddress().ToString())
                .FirstOrDefault(a => a.Length > 0);
        }
        catch (System.Net.NetworkInformation.NetworkInformationException)
        {
            return null;
        }
    }
}
