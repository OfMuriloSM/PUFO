
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PUFO.App.Services;

internal static class DetectorTelaCheia
{
    private const uint MonitorMaisProximo = 2;
    private const int BordaVisivelDwm = 9;
    private const int Tolerancia = 2;

    private const int GwlStyle = -16;
    private const int WsCaption = 0x00C00000;

    public static bool Existe(IntPtr janelaPUFO)
    {
        IntPtr janela = GetForegroundWindow();

        if (janela == IntPtr.Zero ||
            janela == janelaPUFO ||
            janela == GetShellWindow())
        {
            return false;
        }

        if (!IsWindowVisible(janela) || IsIconic(janela))
            return false;

        var nomeClasse = new StringBuilder(256);
        GetClassName(janela, nomeClasse, nomeClasse.Capacity);

        if (nomeClasse.ToString() is "Progman"
            or "WorkerW"
            or "Shell_TrayWnd")
        {
            return false;
        }

        // Uma janela normalmente maximizada não deve
        // ser confundida com uma aplicação fullscreen.
        int estilo = GetWindowLong(janela, GwlStyle);

        bool maximizada = IsZoomed(janela);
        bool possuiTitulo = (estilo & WsCaption) == WsCaption;

        if (maximizada && possuiTitulo)
            return false;

        IntPtr monitor = MonitorFromWindow(
            janela,
            MonitorMaisProximo
        );

        if (monitor == IntPtr.Zero)
            return false;

        var info = new MonitorInfo
        {
            Tamanho = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitor, ref info))
            return false;

        Rect retanguloJanela;

        int resultado = DwmGetWindowAttribute(
            janela,
            BordaVisivelDwm,
            out retanguloJanela,
            Marshal.SizeOf<Rect>()
        );

        if (resultado != 0 &&
            !GetWindowRect(janela, out retanguloJanela))
        {
            return false;
        }

        Rect tela = info.Monitor;

        return
            retanguloJanela.Esquerda
                <= tela.Esquerda + Tolerancia &&

            retanguloJanela.Superior
                <= tela.Superior + Tolerancia &&

            retanguloJanela.Direita
                >= tela.Direita - Tolerancia &&

            retanguloJanela.Inferior
                >= tela.Inferior - Tolerancia;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Esquerda;
        public int Superior;
        public int Direita;
        public int Inferior;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Tamanho;
        public Rect Monitor;
        public Rect AreaTrabalho;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr janela);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr janela);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr janela);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(
        IntPtr janela,
        int indice
    );

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(
        IntPtr janela,
        uint flags
    );

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(
        IntPtr monitor,
        ref MonitorInfo info
    );

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(
        IntPtr janela,
        out Rect retangulo
    );

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(
        IntPtr janela,
        StringBuilder nome,
        int tamanho
    );

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr janela,
        int atributo,
        out Rect valor,
        int tamanho
    );
}
