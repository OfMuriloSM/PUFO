using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PUFO.App.Services.Configuracoes;
using Size = System.Windows.Size;
using WinForms = System.Windows.Forms;

namespace PUFO.App;

public partial class MainWindow : Window
{
    private EstadoIlha _estadoIlha = EstadoIlha.Repouso;
    private bool _bloquearHoverAteSair;
    private bool _ocultoPorTelaCheia;
    private bool _inicializado;
    private bool _medicaoAgendada;
    private bool _previaContextual;
    private bool _cliqueAnterior;
    private bool _escAnterior;
    private bool _encerrado;
    private int _transicao;
    private Size _tamanhoDestino;
    private UIElement? _conteudoPrevia;
    private UIElement? _conteudoCompleto;
    private bool _previaPersonalizada;
    private bool _completoPersonalizado;
    private readonly DispatcherTimer _timerHover = new();
    private readonly DispatcherTimer _timerFechamento = new();
    private readonly DispatcherTimer _timerTelaCheia = new();
    private readonly DispatcherTimer _timerAudioSync = new();
    private readonly DispatcherTimer _timerContexto = new();
    private readonly DispatcherTimer _timerCliqueFora = new();
    private readonly WinForms.NotifyIcon _iconeBandeja = new();

    public MainWindow() : this(new ServicoConfiguracoes()) { }

    internal MainWindow(ServicoConfiguracoes servicoConfiguracoes, bool conectarMidia = true)
    {
        _servicoConfiguracoes = servicoConfiguracoes;
        _configuracoes = servicoConfiguracoes.Carregar();
        InitializeComponent();
        HostPrevia.Content = ViewPrevia;
        HostCompleto.Content = RolagemCompleto;
        _inicializado = true;
        ConfigurarTemporizadores();
        ConfigurarPreferencias();
        ConfigurarBandeja();
        PrepararModulos();
        PrepararVoz();
        PrepararMidia(conectarMidia);
        Loaded += (_, _) => { AtualizarAudioUi(); MudarEstado(EstadoIlha.Repouso); };
        SizeChanged += (_, _) => CentralizarJanela();
        DpiChanged += (_, _) => AgendarMedicao();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += AoMudarMonitores;
        Deactivated += (_, _) => { if (!VozVisivel && !_abrindoConfiguracoesDaIlha && _estadoIlha == EstadoIlha.Completo) FecharManualmente(); };
        Closed += AoFecharAplicacao;
        _timerTelaCheia.Start();
        _timerAudioSync.Start();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        // Tool windows stay out of Alt+Tab as well as the taskbar.
        SetWindowLong(hwnd, -20, (GetWindowLong(hwnd, -20) | 0x80) & ~0x40000);
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x01 | 0x02 | 0x04 | 0x10 | 0x20);
        HwndSource.FromHwnd(hwnd)?.AddHook(FiltrarHitTest);
    }

    private void ConfigurarTemporizadores()
    {
        _timerHover.Tick += (_, _) =>
        {
            _timerHover.Stop();
            if (!_bloquearHoverAteSair && !_ocultoPorTelaCheia && _estadoIlha == EstadoIlha.Repouso && PonteiroSobreIlha())
                MudarEstado(EstadoIlha.Previa);
        };
        _timerFechamento.Tick += (_, _) =>
        {
            if (InteragindoComPrevia) return;
            _timerFechamento.Stop();
            if (_estadoIlha == EstadoIlha.Previa && !PonteiroSobreIlha()) MudarEstado(EstadoIlha.Repouso);
        };
        _timerTelaCheia.Interval = TimeSpan.FromMilliseconds(400);
        _timerTelaCheia.Tick += (_, _) => VerificarTelaCheia();
        _timerAudioSync.Interval = TimeSpan.FromMilliseconds(250);
        _timerAudioSync.Tick += (_, _) => AtualizarAudioUi();
        _timerContexto.Interval = TimeSpan.FromSeconds(2);
        _timerContexto.Tick += (_, _) =>
        {
            if (PonteiroSobreIlha() || InteragindoComPrevia) return;
            _timerContexto.Stop();
            _previaContextual = false;
            if (_estadoIlha == EstadoIlha.Previa) MudarEstado(EstadoIlha.Repouso);
        };
        // Observe clicks without capturing or swallowing another application's input.
        _timerCliqueFora.Interval = TimeSpan.FromMilliseconds(40);
        _timerCliqueFora.Tick += (_, _) =>
        {
            bool esc = (GetAsyncKeyState(0x1B) & 0x8000) != 0;
            if (esc && !_escAnterior)
            {
                FecharManualmente();
                return;
            }
            _escAnterior = esc;
            if (_estadoIlha != EstadoIlha.Completo) return;
            bool clicando = (GetAsyncKeyState(1) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0;
            if (clicando && !_cliqueAnterior && !PonteiroSobreIlha()) FecharManualmente();
            _cliqueAnterior = clicando;
        };
    }

    private void ConfigurarBandeja()
    {
        _iconeBandeja.Icon = System.Drawing.SystemIcons.Application;
        _iconeBandeja.Text = "PUFO";
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Configurações", null, (_, _) => Dispatcher.Invoke(() => AbrirConfiguracoes()));
        menu.Items.Add("Mostrar PUFO", null, (_, _) => Dispatcher.Invoke(() =>
        {
            VerificarTelaCheia();
            if (_ocultoPorTelaCheia) return;
            Show();
            Activate();
            MudarEstado(EstadoIlha.Completo);
        }));
        menu.Items.Add("Sair do PUFO", null, (_, _) => Dispatcher.Invoke(Close));
        _iconeBandeja.ContextMenuStrip = menu;
        _iconeBandeja.Visible = true;
    }

    private void AoEntrarNaArea(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _timerFechamento.Stop();
        if (VozVisivel) return;
        if (!_bloquearHoverAteSair && _estadoIlha == EstadoIlha.Repouso)
        {
            _timerHover.Stop();
            _timerHover.Start();
        }
    }

    private void AoSairDaArea(object sender, System.Windows.Input.MouseEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (VozVisivel || PonteiroSobreIlha()) return;
            _timerHover.Stop();
            _bloquearHoverAteSair = false;
            if (_estadoIlha == EstadoIlha.Previa)
            {
                _timerFechamento.Stop();
                _timerFechamento.Start();
            }
        }, DispatcherPriority.Input);
    }

    private bool PonteiroSobreIlha()
    {
        if (!IsVisible) return false;
        var posicao = WinForms.Control.MousePosition;
        var ponto = Capsula.PointFromScreen(new System.Windows.Point(posicao.X, posicao.Y));
        var faixa = AreaAtivacao.PointFromScreen(new System.Windows.Point(posicao.X, posicao.Y));
        return GeometriaCapsula().FillContains(ponto) || new Rect(AreaAtivacao.RenderSize).Contains(faixa);
    }

    private RectangleGeometry GeometriaCapsula() => new(
        new Rect(0, 0, Capsula.ActualWidth, Capsula.ActualHeight),
        Capsula.CornerRadius.TopLeft, Capsula.CornerRadius.TopLeft);

    private IntPtr FiltrarHitTest(IntPtr hwnd, int mensagem, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (mensagem == 0x0084) // WM_NCHITTEST: even the shadow must let underlying apps receive clicks.
        {
            long packed = lParam.ToInt64();
            var ponto = Capsula.PointFromScreen(new System.Windows.Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)));
            var faixa = AreaAtivacao.PointFromScreen(new System.Windows.Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)));
            if (!GeometriaCapsula().FillContains(ponto) && !new Rect(AreaAtivacao.RenderSize).Contains(faixa))
            {
                handled = true;
                return new IntPtr(-1); // HTTRANSPARENT
            }
        }
        return IntPtr.Zero;
    }

    private void AoClicarEsquerdoNaArea(object sender, MouseButtonEventArgs e)
    {
        if (VozVisivel) { e.Handled = true; return; }
        if (_estadoIlha == EstadoIlha.Previa && HostModulo.IsMouseOver) return;
        if (_estadoIlha != EstadoIlha.Completo) MudarEstado(EstadoIlha.Completo);
        e.Handled = true;
    }

    private void AoClicarDireitoNaArea(object sender, MouseButtonEventArgs e)
    {
        if (VozVisivel) _sessaoVoz.Cancelar();
        else if (_estadoIlha != EstadoIlha.Repouso) FecharManualmente();
        e.Handled = true;
    }

    private void AoPressionarTecla(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (VozVisivel) { if (e.Key == Key.Escape) _sessaoVoz.Cancelar(); e.Handled = true; return; }
        if (e.Key == Key.F6)
        {
            MudarEstado(EstadoIlha.Previa);
            _navegacaoTeclado = true;
            Atalhos.UpdateLayout();
            Atalhos.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            e.Handled = true;
            return;
        }
        if (_estadoIlha == EstadoIlha.Previa) _navegacaoTeclado = true;
        if (e.Key != Key.Escape) return;
        FecharManualmente();
        e.Handled = true;
    }

    private void PararTemporizadoresInteracao() { _timerHover.Stop(); _timerFechamento.Stop(); }
    private void FecharManualmente()
    {
        _bloquearHoverAteSair = true;
        _previaContextual = false;
        _timerContexto.Stop();
        MudarEstado(EstadoIlha.Repouso);
        Dispatcher.BeginInvoke(() => { if (!PonteiroSobreIlha()) _bloquearHoverAteSair = false; }, DispatcherPriority.Background);
    }

    private void MudarEstado(EstadoIlha novoEstado)
    {
        bool mudou = _estadoIlha != novoEstado;
        if (_estadoIlha == EstadoIlha.Completo && mudou) RecolherPaineisAudio();
        if (novoEstado != EstadoIlha.Previa)
        {
            LimparModulo();
            _previaContextual = false;
            _timerContexto.Stop();
        }
        _estadoIlha = novoEstado;
        PararTemporizadoresInteracao();
        if (novoEstado == EstadoIlha.Completo)
        {
            _cliqueAnterior = true;
            if (mudou && _configuracoes.MixerExpandido && !_completoPersonalizado)
            {
                DefinirMixerAberto(true, animar: false);
                AtualizarMixer();
            }
        }
        if (novoEstado == EstadoIlha.Repouso) _timerCliqueFora.Stop();
        else
        {
            _escAnterior = (GetAsyncKeyState(0x1B) & 0x8000) != 0;
            _timerCliqueFora.Start();
        }
        AtualizarConteudo();
        AnimarDimensoes(mudou);
        if (novoEstado == EstadoIlha.Previa && !PonteiroSobreIlha() && !_previaContextual) _timerFechamento.Start();
    }

    // Modules may replace either surface; null deliberately produces the empty dot.
    internal void DefinirConteudo(EstadoIlha estado, UIElement? conteudo)
    {
        if (estado == EstadoIlha.Previa) { _previaPersonalizada = true; _conteudoPrevia = conteudo; }
        else if (estado == EstadoIlha.Completo) { _completoPersonalizado = true; _conteudoCompleto = conteudo; }
        else return;
        AtualizarConteudo();
        AgendarMedicao();
    }

    private void AtualizarConteudo()
    {
        PainelVoz.Visibility = VozVisivel ? Visibility.Visible : Visibility.Collapsed;
        ViewRepouso.Visibility = !VozVisivel && _estadoIlha == EstadoIlha.Repouso ? Visibility.Visible : Visibility.Collapsed;
        HostPrevia.Visibility = !VozVisivel && _estadoIlha == EstadoIlha.Previa ? Visibility.Visible : Visibility.Collapsed;
        HostCompleto.Visibility = !VozVisivel && _estadoIlha == EstadoIlha.Completo ? Visibility.Visible : Visibility.Collapsed;
        HostPrevia.Content = _previaPersonalizada ? _conteudoPrevia : ViewPrevia;
        RolagemCompleto.Content = _completoPersonalizado ? _conteudoCompleto : ViewCompleto;
        HostCompleto.Content = RolagemCompleto.Content is null ? null : RolagemCompleto;
        ApresentarModulo();
    }

    private void AoMudarTamanhoConteudo(object sender, SizeChangedEventArgs e)
    {
        AgendarMedicao();
    }

    private void AgendarMedicao()
    {
        if (!_inicializado || _encerrado || _medicaoAgendada) return;
        _medicaoAgendada = true;
        Dispatcher.BeginInvoke(() =>
        {
            _medicaoAgendada = false;
            if (!_encerrado) AnimarDimensoes(false);
        }, DispatcherPriority.Loaded);
    }

    private void AnimarDimensoes(bool trocarEstado)
    {
        if (!_inicializado) return;
        double maxWidth = Math.Max(48, SystemParameters.PrimaryScreenWidth - 48);
        double maxHeight = Math.Max(48, SystemParameters.WorkArea.Height - 40);
        ViewCompleto.MinWidth = Math.Min(270, maxWidth - 42);
        RolagemCompleto.MaxHeight = maxHeight - 2;
        ConteudoIlha.MaxWidth = maxWidth - 2;
        ConteudoIlha.MaxHeight = maxHeight - 2;
        ConteudoIlha.Measure(new Size(maxWidth - 2, maxHeight - 2));
        bool vazio = _estadoIlha == EstadoIlha.Previa ? HostPrevia.Content is null : _estadoIlha == EstadoIlha.Completo && HostCompleto.Content is null;
        vazio &= !VozVisivel;
        var tamanho = vazio ? new Size(18, 18) : new Size(Math.Clamp(ConteudoIlha.DesiredSize.Width + 2, 18, maxWidth), Math.Clamp(ConteudoIlha.DesiredSize.Height + 2, 18, maxHeight));
        if (!trocarEstado && Math.Abs(tamanho.Width - _tamanhoDestino.Width) < 0.5 && Math.Abs(tamanho.Height - _tamanhoDestino.Height) < 0.5) return;
        _tamanhoDestino = tamanho;
        int revisao = ++_transicao;
        double raioFinal = _estadoIlha == EstadoIlha.Completo && !vazio ? 26 : Math.Min(22, tamanho.Height / 2);
        double y = !VozVisivel && _estadoIlha == EstadoIlha.Repouso ? -12 : 8;
        // The section reveal already animates measured height. A second easing curve here would trail the content.
        if (!AnimacoesPermitidas || (!trocarEstado && _estadoIlha == EstadoIlha.Completo))
        {
            AplicarTamanho(tamanho);
            Capsula.CornerRadius = new CornerRadius(raioFinal);
            if (!AnimacoesPermitidas) FinalizarAnimacoes(y);
            AtualizarRecorte();
            CentralizarJanela();
            return;
        }
        double larguraAtual = Capsula.ActualWidth > 0 ? Capsula.ActualWidth : Capsula.Width;
        double alturaAtual = Capsula.ActualHeight > 0 ? Capsula.ActualHeight : Capsula.Height;
        if (Math.Abs(larguraAtual - tamanho.Width) >= 0.5) Animar(Capsula, WidthProperty, larguraAtual, tamanho.Width);
        else Capsula.BeginAnimation(WidthProperty, null);
        var altura = CriarAnimacao(alturaAtual, tamanho.Height);
        altura.Completed += (_, _) =>
        {
            if (revisao != _transicao) return;
            AplicarTamanho(tamanho);
            AtualizarRecorte();
        };
        Capsula.Width = tamanho.Width;
        Capsula.Height = tamanho.Height;
        // Use the actual height animation clock for corner interpolation as well.
        double raioInicial = Capsula.CornerRadius.TopLeft;
        altura.CurrentTimeInvalidated += (sender, _) =>
        {
            if (revisao != _transicao || sender is not AnimationClock { CurrentProgress: double progresso }) return;
            var easing = altura.EasingFunction;
            double t = easing?.Ease(progresso) ?? progresso;
            Capsula.CornerRadius = new CornerRadius(raioInicial + (raioFinal - raioInicial) * t);
            AtualizarRecorte();
        };
        Capsula.BeginAnimation(HeightProperty, altura);
        if (trocarEstado)
            CapsulaTranslate.BeginAnimation(TranslateTransform.YProperty, CriarAnimacao(CapsulaTranslate.Y, y));
        if (trocarEstado && _estadoIlha != EstadoIlha.Repouso)
            Animar(ConteudoIlha, OpacityProperty, Math.Min(ConteudoIlha.Opacity, 0.25), 1);
        CentralizarJanela();
    }

    private void AplicarTamanho(Size tamanho)
    {
        Capsula.BeginAnimation(WidthProperty, null);
        Capsula.BeginAnimation(HeightProperty, null);
        Capsula.Width = tamanho.Width;
        Capsula.Height = tamanho.Height;
    }

    private void FinalizarAnimacoes(double y)
    {
        CapsulaTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        CapsulaTranslate.Y = y;
        ConteudoIlha.BeginAnimation(OpacityProperty, null);
        ConteudoIlha.Opacity = 1;
    }

    private void AoRedimensionarCapsula(object sender, SizeChangedEventArgs e) => AtualizarRecorte();

    private void AtualizarRecorte() => Recorte.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, Capsula.ActualWidth - 2), Math.Max(0, Capsula.ActualHeight - 2)), Capsula.CornerRadius.TopLeft, Capsula.CornerRadius.TopLeft);
    private bool AnimacoesPermitidas => _configuracoes.Animacoes && SystemParameters.ClientAreaAnimation;

    private DoubleAnimation CriarAnimacao(double inicio, double destino) => new(inicio, destino,
        TimeSpan.FromMilliseconds(AnimacoesPermitidas ? 26000d / _configuracoes.VelocidadeAnimacoes : 0))
    {
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
    };
    private void Animar(UIElement alvo, DependencyProperty propriedade, double inicio, double destino) => alvo.BeginAnimation(propriedade, CriarAnimacao(inicio, destino));
    private void CentralizarJanela()
    {
        if (ActualWidth > 0) Left = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
    }

    private void VerificarTelaCheia()
    {
        bool telaCheia = Services.DetectorTelaCheia.Existe(new WindowInteropHelper(this).Handle);
        if (telaCheia == _ocultoPorTelaCheia) return;
        _ocultoPorTelaCheia = telaCheia;
        if (telaCheia)
        {
            _sessaoVoz.Cancelar();
            _timerContexto.Stop();
            _previaContextual = false;
            MudarEstado(EstadoIlha.Repouso);
            _bloquearHoverAteSair = false;
            Hide();
        }
        else { Show(); CentralizarJanela(); }
    }

    private void AoMudarMonitores(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => { CentralizarJanela(); AgendarMedicao(); });

    private void AoFecharAplicacao(object? sender, EventArgs e)
    {
        _encerrado = true;
        _sessaoVoz.Cancelar();
        PararMedicao();
        _esperaModulo.Stop();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= AoMudarMonitores;
        foreach (var timer in new[] { _timerHover, _timerFechamento, _timerTelaCheia, _timerAudioSync, _timerContexto, _timerCliqueFora }) timer.Stop();
        EncerrarConfiguracoes();
        EncerrarMidia();
        _mixer.Dispose();
        _iconeBandeja.Visible = false;
        _iconeBandeja.ContextMenuStrip?.Dispose();
        _iconeBandeja.Dispose();
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr janela, int indice);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr janela, int indice, int valor);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int tecla);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr janela, IntPtr depois, int x, int y, int largura, int altura, uint flags);
}
