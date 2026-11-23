using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PUFO.App.Services;

internal static class AtalhosWindows
{
    // SC_TASKLIST pede ao shell para ativar o Iniciar, sem sintetizar teclas.
    internal static void Iniciar(nint janela) => DefWindowProcW(janela, 0x0112, 0xF130, 0);

    internal static bool AbrirAjustes(string uri)
    {
        try { using var processo = Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); return true; }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException) { return false; }
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint DefWindowProcW(nint janela, uint mensagem, nuint comando, nint parametro);
}
