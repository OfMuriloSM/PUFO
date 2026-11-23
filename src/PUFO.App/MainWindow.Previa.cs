using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PUFO.App.Controls;
using PUFO.App.Models;
using PUFO.App.Services;
using PUFO.App.Services.Audio;
using Button = System.Windows.Controls.Button;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace PUFO.App;

public partial class MainWindow
{
    private readonly DispatcherTimer _esperaModulo = new() { Interval = TimeSpan.FromMilliseconds(140) };
    private Button? _iconePendente;
    private ModuloPrevia? _moduloSelecionado;
    private bool _navegacaoTeclado;
    private CancellationTokenSource? _leituraAtiva;
    private IReadOnlyList<FonteSom> _fontesAtivas = [];

    private bool AudioPreviaAberto => _estadoIlha == EstadoIlha.Previa && _moduloSelecionado?.Painel == PainelAudioPrevia;
    private bool ExibirCartaoNaPrevia => MostrarMidiaNaPrevia &&
        (AudioPreviaAberto || _previaContextual || (_moduloSelecionado is null && _configuracoes.PrioridadeMidia));
    private bool InteragindoComPrevia => SliderPrevia.EmArrasto || ViewPrevia.IsMouseCaptureWithin ||
        (_navegacaoTeclado && IsActive && ViewPrevia.IsKeyboardFocusWithin);

    private void PrepararModulos()
    {
        var wifi = new PainelConsulta("Wi-Fi", "Redes no Windows", "ms-settings:network-wifi", _ => Task.Run(WifiWindows.Listar));
        var notificacoes = new PainelConsulta("Notificações", "Notificações no Windows", "ms-settings:notifications", NotificacoesWindows.ConsultarAsync);
        // A ordem dos descritores também define a ordem de navegação por teclado.
        Atalhos.ItemsSource = new ModuloPrevia[]
        {
            new("windows", "Iniciar", "M 2,3 H 8 V 9 H 2 Z M 11,3 H 17 V 9 H 11 Z M 2,12 H 8 V 18 H 2 Z M 11,12 H 17 V 18 H 11 Z",
                Acao: () => { FecharManualmente(); AtalhosWindows.Iniciar(new WindowInteropHelper(this).Handle); }, Preenchido: true),
            new("audio", "Áudio", ((Geometry)FindResource("Speaker")).ToString(System.Globalization.CultureInfo.InvariantCulture), PainelAudioPrevia),
            new("wifi", "Wi-Fi", "M 1,6 Q 10,-1 19,6 M 4,10 Q 10,5 16,10 M 7,14 Q 10,11 13,14 M 9.6,17 H 10.4", wifi),
            new("notificacoes", "Notificações", "M 3,15 Q 5,13 5,9 V 7 A 5,5 0 0 1 15,7 V 9 Q 15,13 17,15 Z M 8,18 Q 10,20 12,18", notificacoes),
            new("configuracoes", "Configurações", "M 8,2 H 12 L 13,4 15,5 17,5 19,8 17,10 17,12 19,14 17,17 14,16 12,17 12,19 8,19 7,17 5,16 3,17 1,14 3,12 3,10 1,8 3,5 5,5 7,4 Z M 13,10.5 A 3,3 0 1 1 7,10.5 A 3,3 0 1 1 13,10.5 Z",
                Acao: AbrirConfiguracoesDaIlha)
        };
        HostModulo.Content = null;
        _esperaModulo.Tick += (_, _) =>
        {
            _esperaModulo.Stop();
            if (_iconePendente is { IsMouseOver: true, DataContext: ModuloPrevia modulo }) SelecionarModulo(modulo);
            _iconePendente = null;
        };
        PreviewMouseMove += (_, e) =>
        {
            if (!_navegacaoTeclado) return;
            _navegacaoTeclado = false;
            if (_estadoIlha == EstadoIlha.Previa && !PonteiroSobreIlha()) _timerFechamento.Start();
        };
    }

    private void AoApontarModulo(object sender, MouseEventArgs e)
    {
        if (_estadoIlha != EstadoIlha.Previa || ViewPrevia.IsMouseCaptureWithin) return;
        _timerFechamento.Stop();
        _esperaModulo.Stop();
        _iconePendente = (Button)sender;
        _esperaModulo.Start();
    }

    private void AoDeixarIcone(object sender, MouseEventArgs e)
    {
        if (sender != _iconePendente) return;
        _esperaModulo.Stop();
        _iconePendente = null;
    }

    private void AoFocarModulo(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_navegacaoTeclado && sender is Button { DataContext: ModuloPrevia modulo }) SelecionarModulo(modulo);
    }

    private void AoAcionarModulo(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { DataContext: ModuloPrevia { Disponivel: true } modulo }) return;
        if (modulo.Acao is not null) modulo.Acao();
        else
        {
            SelecionarModulo(modulo);
            if (_navegacaoTeclado) HostModulo.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }
    }

    private void SelecionarModulo(ModuloPrevia modulo)
    {
        if (_estadoIlha != EstadoIlha.Previa || !modulo.Disponivel || SliderPrevia.EmArrasto || SliderPrevia.IsMouseCaptureWithin) return;
        var proximo = modulo.Painel is null ? null : modulo;
        if (_moduloSelecionado == proximo && !_previaContextual) return;
        bool lendoAudio = AudioPreviaAberto;
        _moduloSelecionado = proximo;
        _previaContextual = false;
        _timerContexto.Stop();
        ApresentarModulo();
        if (lendoAudio) PararMedicao();
        if (AudioPreviaAberto) IniciarMedicao();
        if (proximo is not null && AnimacoesPermitidas)
            Animar(HostModulo, OpacityProperty, 0.55, 1);
        AgendarMedicao();
    }

    private void ApresentarModulo()
    {
        var painel = _moduloSelecionado?.Painel ?? (_previaContextual ? PainelAudioPrevia : null);
        if (!ReferenceEquals(HostModulo.Content, painel)) HostModulo.Content = painel;
        HostModulo.Visibility = painel is null ? Visibility.Collapsed : Visibility.Visible;
        DetalhesAudioPrevia.Visibility = AudioPreviaAberto ? Visibility.Visible : Visibility.Collapsed;
        MidiaCompacta.Visibility = ExibirCartaoNaPrevia ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LimparModulo()
    {
        PararMedicao();
        _moduloSelecionado = null;
        _navegacaoTeclado = false;
        _esperaModulo.Stop();
        _iconePendente = null;
    }

    private void IniciarMedicao()
    {
        PararMedicao();
        EstadoSom.Text = "Medindo atividade…";
        var leitura = _leituraAtiva = new CancellationTokenSource();
        _ = MedirEnquantoVisivelAsync(leitura);
    }

    private async Task MedirEnquantoVisivelAsync(CancellationTokenSource leitura)
    {
        var token = leitura.Token;
        try
        {
            await Task.Run(async () =>
            {
                using var monitor = new MonitorSomWindows();
                while (!token.IsCancellationRequested)
                {
                    var resultado = monitor.Ler();
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (!token.IsCancellationRequested) AtualizarFontesAtivas(resultado);
                    }, DispatcherPriority.Background, token);
                    await Task.Delay(600, token);
                }
            }, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            if (ReferenceEquals(_leituraAtiva, leitura)) AtualizarFontesAtivas(new([], true));
        }
        finally
        {
            if (ReferenceEquals(_leituraAtiva, leitura)) _leituraAtiva = null;
            leitura.Dispose();
        }
    }

    private void PararMedicao()
    {
        _leituraAtiva?.Cancel();
        _leituraAtiva = null;
        _fontesAtivas = [];
        FontesAtivas.ItemsSource = null;
    }

    internal void AtualizarFontesAtivas(MedicaoSom medicao)
    {
        if (!_fontesAtivas.SequenceEqual(medicao.Fontes)) FontesAtivas.ItemsSource = _fontesAtivas = medicao.Fontes;
        EstadoSom.Text = medicao.Fontes.Count > 0 ? "Som detectado" :
            medicao.Parcial ? "Medição parcial · nenhum pico detectado" : "Nenhum som detectado";
    }

    private void AoAbrirReproducoes(object sender, RoutedEventArgs e)
    {
        MudarEstado(EstadoIlha.Completo);
        MidiaCompleta.AbrirSessoes();
    }

    private void AoAbrirMixerDaPrevia(object sender, RoutedEventArgs e)
    {
        MudarEstado(EstadoIlha.Completo);
        DefinirMixerAberto(true);
        AtualizarMixer();
        BtnMixer.Focus();
    }

    private void AoAbrirSaidasDaPrevia(object sender, RoutedEventArgs e)
    {
        MudarEstado(EstadoIlha.Completo);
        DefinirSaidasAbertas(true);
        AtualizarSaidas();
        BtnSaida.Focus();
    }
}
