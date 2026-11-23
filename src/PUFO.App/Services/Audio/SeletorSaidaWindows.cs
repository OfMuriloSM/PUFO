using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace PUFO.App.Services.Audio;

internal sealed record SaidaAudio(string Id, string Nome);

internal static class SeletorSaidaWindows
{
    internal static IReadOnlyList<SaidaAudio>? Listar()
    {
        var saidas = new Dictionary<string, SaidaAudio>(StringComparer.Ordinal);
        try
        {
            using var enumerador = new MMDeviceEnumerator();
            using var dispositivos = enumerador.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var dispositivo in dispositivos)
            {
                using (dispositivo)
                {
                    try { saidas.TryAdd(dispositivo.ID, new(dispositivo.ID, dispositivo.FriendlyName)); }
                    catch (Exception e) when (ServicoAudioWindows.FalhaCoreAudio(e)) { }
                }
            }
            return saidas.Values.OrderBy(saida => saida.Nome, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(saida => saida.Id, StringComparer.Ordinal).ToArray();
        }
        catch (Exception e) when (ServicoAudioWindows.FalhaCoreAudio(e)) { return null; }
    }

    internal static bool SolicitarTroca(string id)
    {
        object? objeto = null;
        try
        {
            if (Listar()?.Any(saida => saida.Id == id) != true) return false;
            var tipo = Type.GetTypeFromCLSID(new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9"), throwOnError: true)!;
            objeto = Activator.CreateInstance(tipo);
            var politica = (IPolicyConfig)objeto!;
            // Keep the independently selected communications device. The UI confirms Multimedia by reading Core Audio again.
            if (politica.SetDefaultEndpoint(id, Role.Multimedia) < 0) return false;
            return politica.SetDefaultEndpoint(id, Role.Console) >= 0;
        }
        catch (Exception e) when (ServicoAudioWindows.FalhaCoreAudio(e) || e is InvalidCastException or System.Reflection.TargetInvocationException)
        {
            return false;
        }
        finally
        {
            if (objeto is not null && Marshal.IsComObject(objeto)) Marshal.ReleaseComObject(objeto);
        }
    }

    internal static bool Confirmar(string id)
    {
        try
        {
            using var enumerador = new MMDeviceEnumerator();
            using var multimidia = enumerador.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            using var console = enumerador.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            return multimidia.ID == id && console.ID == id;
        }
        catch (Exception e) when (ServicoAudioWindows.FalhaCoreAudio(e)) { return false; }
    }

    internal static bool AbrirConfiguracoes()
    {
        try
        {
            using var processo = Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true });
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
    }

    // Private ABI used on Windows 10/11. The ten reserved slots preserve SetDefaultEndpoint's vtable position.
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat();
        [PreserveSig] int GetDeviceFormat();
        [PreserveSig] int ResetDeviceFormat();
        [PreserveSig] int SetDeviceFormat();
        [PreserveSig] int GetProcessingPeriod();
        [PreserveSig] int SetProcessingPeriod();
        [PreserveSig] int GetShareMode();
        [PreserveSig] int SetShareMode();
        [PreserveSig] int GetPropertyValue();
        [PreserveSig] int SetPropertyValue();
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, Role role);
    }
}
