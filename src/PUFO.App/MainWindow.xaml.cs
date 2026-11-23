
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;

using WinForms = System.Windows.Forms;

namespace PUFO.App;

public partial class MainWindow : Window
{

private const int GWL_EXSTYLE = -20;
private const int WS_EX_TOOLWINDOW = 0x00000080;
private const int WS_EX_APPWINDOW = 0x00040000;

private const uint SWP_NOSIZE = 0x0001;
private const uint SWP_NOMOVE = 0x0002;
private const uint SWP_NOZORDER = 0x0004;
private const uint SWP_NOACTIVATE = 0x0010;
private const uint SWP_FRAMECHANGED = 0x0020;

[DllImport("user32.dll", EntryPoint = "GetWindowLongW",
    SetLastError = true)]
private static extern int GetWindowLong(
    IntPtr janela, int indice);

[DllImport("user32.dll", EntryPoint = "SetWindowLongW",
    SetLastError = true)]
private static extern int SetWindowLong(
    IntPtr janela, int indice, int valor);

[DllImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
private static extern bool SetWindowPos(
    IntPtr janela,
    IntPtr inserirDepois,
    int x, int y, int largura, int altura,
    uint flags);

protected override void OnSourceInitialized(EventArgs e)
{
    base.OnSourceInitialized(e);

    IntPtr janela = new System.Windows.Interop.WindowInteropHelper(this).Handle;

    int estiloAtual = GetWindowLong(janela, GWL_EXSTYLE);

    int novoEstilo =
        (estiloAtual | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;

    SetWindowLong(janela, GWL_EXSTYLE, novoEstilo);

    SetWindowPos(
        janela,
        IntPtr.Zero,
        0, 0, 0, 0,
        SWP_NOMOVE | SWP_NOSIZE |
        SWP_NOZORDER | SWP_NOACTIVATE |
        SWP_FRAMECHANGED);
}

    // Estado da ilha
    private EstadoIlha _estadoIlha = EstadoIlha.Repouso;
    private bool _bloquearHoverAteSair;

    // Temporizadores
    private readonly DispatcherTimer _timerHover = new();
    private readonly DispatcherTimer _timerFechamento = new();
    private readonly DispatcherTimer _timerTelaCheia = new();

    // Conteúdos das futuras funcionalidades
    private UIElement? _conteudoPrevia;
    private UIElement? _conteudoCompleto;

    // Bandeja do Windows
    private readonly WinForms.NotifyIcon _iconeBandeja = new();

    // Suspensão da interface
    private bool _ocultoPorTelaCheia;

    public MainWindow()
    {
        InitializeComponent();

        ConfigurarTemporizadores();
        ConfigurarBandeja();

        MudarEstado(EstadoIlha.Repouso);

        Loaded += (_, _) => CentralizarJanela();
        SizeChanged += (_, _) => CentralizarJanela();
        Deactivated += AoDesativar;
        Closed += AoFecharAplicacao;

        _timerTelaCheia.Start();
    }

    // ========================================
    // CONFIGURAÇÃO
    // ========================================

    private void ConfigurarTemporizadores()
    {
        _timerHover.Interval = TimeSpan.FromSeconds(1);
        _timerHover.Tick += AoTerminarHover;

        _timerFechamento.Interval =
            TimeSpan.FromMilliseconds(500);

        _timerFechamento.Tick += AoTerminarFechamento;

        _timerTelaCheia.Interval =
            TimeSpan.FromMilliseconds(750);

        _timerTelaCheia.Tick += (_, _) =>
            VerificarTelaCheia();
    }

    private void ConfigurarBandeja()
    {
        _iconeBandeja.Icon = SystemIcons.Application;
        _iconeBandeja.Text = "PUFO";

        var menu = new WinForms.ContextMenuStrip();

        menu.Items.Add(
            "Sair do PUFO",
            null,
            (_, _) => Dispatcher.Invoke(() => Close())
        );

        _iconeBandeja.ContextMenuStrip = menu;
        _iconeBandeja.Visible = true;
    }

    // ========================================
    // EVENTOS DO MOUSE
    // ========================================

    private void AoEntrarNaArea(
        object sender,
        System.Windows.Input.MouseEventArgs e)
    {
        // Cancela qualquer fechamento pendente.
        _timerFechamento.Stop();

        if (_bloquearHoverAteSair)
            return;

        if (_estadoIlha == EstadoIlha.Repouso)
        {
            _timerHover.Stop();
            _timerHover.Start();
        }
    }

    private void AoSairDaArea(
        object sender,
        System.Windows.Input.MouseEventArgs e)
    {
        // Aguarda a atualização do hit-test do WPF.
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (PonteiroSobreIlha())
                    return;

                _timerHover.Stop();
                _bloquearHoverAteSair = false;

                if (_estadoIlha == EstadoIlha.Previa)
                {
                    _timerFechamento.Stop();
                    _timerFechamento.Start();
                }
            }),
            DispatcherPriority.Input
        );
    }

    private void AoClicarEsquerdo(
        object sender,
        MouseButtonEventArgs e)
    {
        PararTemporizadoresInteracao();

        if (_estadoIlha != EstadoIlha.Completo)
        {
            MudarEstado(EstadoIlha.Completo);
        }

        e.Handled = true;
    }

    private void AoClicarDireito(
        object sender,
        MouseButtonEventArgs e)
    {
        if (_estadoIlha != EstadoIlha.Repouso)
        {
            FecharManualmente();
        }

        e.Handled = true;
    }

    private void AoDesativar(
        object? sender,
        EventArgs e)
    {
        // Fechamento quando outra janela recebe foco.
        if (_estadoIlha == EstadoIlha.Completo)
        {
            FecharManualmente();
        }
    }

    // ========================================
    // TEMPORIZADORES
    // ========================================

    private void AoTerminarHover(
        object? sender,
        EventArgs e)
    {
        _timerHover.Stop();

        if (_bloquearHoverAteSair)
            return;

        if (_estadoIlha == EstadoIlha.Repouso
            && PonteiroSobreIlha())
        {
            MudarEstado(EstadoIlha.Previa);
        }
    }

    private void AoTerminarFechamento(
        object? sender,
        EventArgs e)
    {
        _timerFechamento.Stop();

        if (_estadoIlha == EstadoIlha.Previa
            && !PonteiroSobreIlha())
        {
            MudarEstado(EstadoIlha.Repouso);
        }
    }

    private void PararTemporizadoresInteracao()
    {
        _timerHover.Stop();
        _timerFechamento.Stop();
    }

    private bool PonteiroSobreIlha()
    {
        return AreaAtivacao.IsMouseOver
            || Capsula.IsMouseOver;
    }

    // ========================================
    // ESTADOS
    // ========================================

    private void FecharManualmente()
    {
        PararTemporizadoresInteracao();

        _bloquearHoverAteSair = true;

        MudarEstado(EstadoIlha.Repouso);

        // Se a região desaparecer debaixo do
        // cursor, libera após atualizar o layout.
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (!PonteiroSobreIlha())
                    _bloquearHoverAteSair = false;
            }),
            DispatcherPriority.Background
        );
    }

    private void MudarEstado(EstadoIlha novoEstado)
    {
        _estadoIlha = novoEstado;

        PararTemporizadoresInteracao();

        bool repouso = novoEstado == EstadoIlha.Repouso;

        BarraRepouso.Visibility = repouso
            ? Visibility.Visible
            : Visibility.Collapsed;

        Capsula.Visibility = repouso
            ? Visibility.Collapsed
            : Visibility.Visible;

        AtualizarConteudo();
    }

    // ========================================
    // CONTEÚDO DINÂMICO
    // ========================================

    internal void DefinirConteudo(
        EstadoIlha estado,
        UIElement? conteudo)
    {
        switch (estado)
        {
            case EstadoIlha.Previa:
                _conteudoPrevia = conteudo;
                break;

            case EstadoIlha.Completo:
                _conteudoCompleto = conteudo;
                break;

            default:
                return;
        }

        AtualizarConteudo();
    }

    private void AtualizarConteudo()
    {
        if (_estadoIlha == EstadoIlha.Repouso)
        {
            ConteudoIlha.Content = null;
            return;
        }

        UIElement? conteudo = _estadoIlha switch
        {
            EstadoIlha.Previa => _conteudoPrevia,
            EstadoIlha.Completo => _conteudoCompleto,
            _ => null
        };

        ConteudoIlha.Content = conteudo;

        bool vazio = conteudo is null;

        if (vazio)
        {
            Capsula.Width = 34;
            Capsula.Height = 34;
            Capsula.Padding = new Thickness(0);
            Capsula.CornerRadius = new CornerRadius(17);
        }
        else
        {
            // Auto: o tamanho será definido pelo conteúdo.
            Capsula.Width = double.NaN;
            Capsula.Height = double.NaN;

            Capsula.Padding = new Thickness(12, 8, 12, 8);
            Capsula.CornerRadius = new CornerRadius(18);
        }
    }

    // ========================================
    // POSICIONAMENTO
    // ========================================

    private void CentralizarJanela()
    {
        if (ActualWidth <= 0)
            return;

        Left = (SystemParameters.PrimaryScreenWidth
            - ActualWidth) / 2;

        Top = 0;
    }

    // ========================================
    // TELA CHEIA
    // ========================================

    private void VerificarTelaCheia()
    {
        var janelaPUFO = new WindowInteropHelper(this).Handle;

        bool telaCheia =
            Services.DetectorTelaCheia.Existe(janelaPUFO);

        if (telaCheia == _ocultoPorTelaCheia)
            return;

        _ocultoPorTelaCheia = telaCheia;

        if (telaCheia)
        {
            PararTemporizadoresInteracao();
            _bloquearHoverAteSair = false;

            MudarEstado(EstadoIlha.Repouso);
            Hide();
        }
        else
        {
            Show();
            CentralizarJanela();
        }
    }

    // ========================================
    // ENCERRAMENTO
    // ========================================

    private void AoFecharAplicacao(
        object? sender,
        EventArgs e)
    {
        _timerHover.Stop();
        _timerFechamento.Stop();
        _timerTelaCheia.Stop();

        _iconeBandeja.Visible = false;
        _iconeBandeja.ContextMenuStrip?.Dispose();
        _iconeBandeja.Dispose();
    }
}
