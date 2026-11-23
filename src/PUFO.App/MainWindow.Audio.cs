using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using PUFO.App.Controls;
using PUFO.App.Models;
using PUFO.App.Services.Audio;

namespace PUFO.App;

public partial class MainWindow
{
    private readonly ServicoAudioWindows _servicoAudio = new();
    private readonly MixerWindows _mixer = new();
    private readonly Dictionary<ChaveSessao, LinhaMixer> _linhasMixer = new();
    private AudioStatus? _ultimoStatus;
    private IReadOnlyList<SaidaAudio> _saidas = [];
    private string? _saidaPendente;
    private long _confirmarSaidaAte;
    private long _proximaLeituraSaidas;
    private long _proximaLeituraMixer;
    private IReadOnlyList<OpcaoSaidaAudio> _opcoesSaida = [];
    private bool _saidasAbertas;
    private bool _mixerAberto;

    private void AtualizarAudioUi()
    {
        if (!_inicializado || _encerrado) return;
        AplicarStatusAudio(_servicoAudio.ObterStatus());
        VerificarTrocaSaida();
        long agora = Environment.TickCount64;
        if (_mixerAberto && agora >= _proximaLeituraMixer) AtualizarMixer();
        if (_saidasAbertas && agora >= _proximaLeituraSaidas) AtualizarSaidas();
    }

    private void AplicarStatusAudio(AudioStatus status)
    {
        var anterior = _ultimoStatus;
        _ultimoStatus = status;
        _janelaConfiguracoes?.AtualizarAudio(status);
        bool mudouSaida = anterior?.IdDispositivo != status.IdDispositivo;
        bool mudouDisponibilidade = anterior?.Disponivel != status.Disponivel;
        bool mudouVolume = anterior?.VolumePercentual != status.VolumePercentual;
        bool mudouMute = anterior?.EstaMudo != status.EstaMudo;

        if (mudouSaida || mudouDisponibilidade)
        {
            SliderVolume.CancelarArrasto();
            SliderPrevia.CancelarArrasto();
        }

        // Reconcile on every read, including the first read after a captured drag ends.
        if (SliderVolume.Sincronizar(status.VolumePercentual)) AtualizarPercentual(TxtVolumeCompleto, status.VolumePercentual);
        if (SliderPrevia.Sincronizar(status.VolumePercentual)) AtualizarPercentual(TxtVolumePreview, status.VolumePercentual);

        if (mudouDisponibilidade)
        {
            SliderVolume.IsEnabled = SliderPrevia.IsEnabled = BtnMutePreview.IsEnabled = BtnMuteCompleto.IsEnabled = status.Disponivel;
            if (!status.Disponivel) _previaContextual = false;
            AtualizarConteudo();
            AgendarMedicao();
        }
        if (anterior?.NomeDispositivo != status.NomeDispositivo)
        {
            DispositivoPrevia.Text = TxtDispositivoCompleto.Text = status.NomeDispositivo;
            DispositivoPrevia.ToolTip = status.NomeDispositivo;
            AutomationProperties.SetName(BtnSaida, $"Selecionar saída. Atual: {status.NomeDispositivo}");
            BtnSaida.ToolTip = status.NomeDispositivo;
        }
        if (mudouMute || (anterior?.VolumePercentual == 0) != (status.VolumePercentual == 0))
        {
            var icone = (Geometry)FindResource(status.EstaMudo || status.VolumePercentual == 0 ? "Muted" : "Speaker");
            IconeMutePreview.Data = IconeMuteCompleto.Data = icone;
            string acao = status.EstaMudo ? "Ativar áudio" : "Silenciar áudio";
            BtnMutePreview.ToolTip = BtnMuteCompleto.ToolTip = acao;
            AutomationProperties.SetName(BtnMutePreview, acao);
            AutomationProperties.SetName(BtnMuteCompleto, acao);
        }
        if (mudouSaida || mudouDisponibilidade)
        {
            _mixer.Limpar();
            AplicarGruposMixer([]);
            _proximaLeituraMixer = _proximaLeituraSaidas = 0;
            SelecionarSaidaConfirmada();
        }
        else if (_configuracoes.PreviaContextual && !(MostrarMidiaNaPrevia && _configuracoes.PrioridadeMidia) && status.Disponivel && (mudouVolume || mudouMute)) MostrarVolumeContextual();
    }

    private static void AtualizarPercentual(TextBlock texto, int volume)
    {
        string valor = $"{volume}%";
        if (texto.Text != valor) texto.Text = valor;
    }

    private void MostrarVolumeContextual()
    {
        if (VozVisivel || _moduloSelecionado is not null || _estadoIlha == EstadoIlha.Completo || _previaPersonalizada || _ocultoPorTelaCheia ||
            _bloquearHoverAteSair || _ultimoStatus?.Disponivel != true) return;

        _timerContexto.Stop();
        _timerContexto.Start();
        if (_previaContextual && _estadoIlha == EstadoIlha.Previa) return;
        _previaContextual = true;
        if (_estadoIlha == EstadoIlha.Repouso) MudarEstado(EstadoIlha.Previa);
        else
        {
            AtualizarConteudo();
            AgendarMedicao();
        }
    }

    private void BtnMutePreview_Click(object sender, RoutedEventArgs e) => AlternarMudo(e);
    private void BtnMuteCompleto_Click(object sender, RoutedEventArgs e) => AlternarMudo(e);
    private void AlternarMudo(RoutedEventArgs e)
    {
        if (_ultimoStatus is { Disponivel: true } status)
        {
            _servicoAudio.AlternarMudo(status.IdDispositivo);
            AtualizarAudioUi();
        }
        e.Handled = true;
    }

    private void BtnFecharCompleto_Click(object sender, RoutedEventArgs e) => FecharManualmente();

    private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_inicializado || sender is not SliderSincronizado slider || slider.Atualizando ||
            _ultimoStatus is not { Disponivel: true } status) return;
        int percentual = (int)Math.Round(e.NewValue);
        AtualizarPercentual(slider == SliderPrevia ? TxtVolumePreview : TxtVolumeCompleto, percentual);
        _servicoAudio.DefinirVolumePercentual(percentual, status.IdDispositivo);
    }

    private void AoAlternarMixer(object sender, RoutedEventArgs e)
    {
        DefinirMixerAberto(!_mixerAberto);
        if (_mixerAberto) AtualizarMixer();
        e.Handled = true;
    }

    private void DefinirMixerAberto(bool aberto, bool animar = true)
    {
        if (_mixerAberto == aberto)
        {
            if (!animar) Revelar(RevelaMixer, PainelMixer, aberto, animar: false);
            return;
        }
        _mixerAberto = aberto;
        Revelar(RevelaMixer, PainelMixer, aberto, animar);
        IconeMixer.Data = (Geometry)FindResource(aberto ? "ChevronUp" : "ChevronDown");
        string acao = aberto ? "Recolher mixer" : "Expandir mixer";
        BtnMixer.ToolTip = acao;
        AutomationProperties.SetName(BtnMixer, acao);
        if (!aberto) _mixer.Suspender();
        AgendarMedicao();
    }

    private void AtualizarMixer()
    {
        _proximaLeituraMixer = Environment.TickCount64 + 600;
        var grupos = _mixer.Ler(_ultimoStatus?.IdDispositivo);
        AplicarGruposMixer(grupos ?? []);
    }

    private void AplicarGruposMixer(IReadOnlyList<GrupoAudio> grupos)
    {
        var atuais = grupos.Select(grupo => grupo.Chave).ToHashSet();
        foreach (var chave in _linhasMixer.Keys.Where(chave => !atuais.Contains(chave)).ToArray())
        {
            _linhasMixer.Remove(chave, out var linha);
            LinhasMixer.Children.Remove(linha!);
        }
        foreach (var grupo in grupos)
        {
            if (!_linhasMixer.TryGetValue(grupo.Chave, out var linha))
            {
                linha = new LinhaMixer();
                linha.AlterouVolume += _mixer.DefinirVolume;
                linha.AlterouMudo += (chave, mudo) => { _mixer.DefinirMudo(chave, mudo); AtualizarMixer(); };
                _linhasMixer.Add(grupo.Chave, linha);
                LinhasMixer.Children.Add(linha);
            }
            linha.Aplicar(grupo);
        }
        MixerVazio.Visibility = grupos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RolagemMixer.Visibility = grupos.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AoAlternarSaidas(object sender, RoutedEventArgs e)
    {
        DefinirSaidasAbertas(!_saidasAbertas);
        if (_saidasAbertas) AtualizarSaidas();
    }

    private void DefinirSaidasAbertas(bool abertas, bool animar = true)
    {
        if (_saidasAbertas == abertas)
        {
            if (!animar) Revelar(RevelaSaidas, PainelSaidas, abertas, animar: false);
            return;
        }
        _saidasAbertas = abertas;
        IconeSaidas.Data = (Geometry)FindResource(abertas ? "ChevronUp" : "ChevronDown");
        Revelar(RevelaSaidas, PainelSaidas, abertas, animar);
        AgendarMedicao();
    }

    private void Revelar(PainelRevelavel painel, FrameworkElement conteudo, bool abrir, bool animar)
    {
        double inicio = painel.Progresso;
        double destino = abrir ? 1 : 0;
        painel.IsHitTestVisible = abrir;
        KeyboardNavigation.SetTabNavigation(painel, abrir ? KeyboardNavigationMode.Continue : KeyboardNavigationMode.None);
        if (abrir) conteudo.Visibility = Visibility.Visible;
        painel.Progresso = destino;
        if (!animar || inicio == destino || !AnimacoesPermitidas)
        {
            painel.BeginAnimation(PainelRevelavel.ProgressoProperty, null);
            conteudo.Visibility = abrir ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        var animacao = CriarAnimacao(inicio, destino);
        animacao.FillBehavior = FillBehavior.Stop;
        animacao.Completed += (_, _) =>
        {
            if (painel.IsHitTestVisible == abrir && !abrir) conteudo.Visibility = Visibility.Collapsed;
        };
        painel.BeginAnimation(PainelRevelavel.ProgressoProperty, animacao);
    }

    private void AtualizarSaidas()
    {
        _proximaLeituraSaidas = Environment.TickCount64 + 1200;
        if (SeletorSaidaWindows.Listar() is { } saidas) AplicarSaidas(saidas);
    }

    private void AplicarSaidas(IReadOnlyList<SaidaAudio> saidas)
    {
        if (!_saidas.SequenceEqual(saidas))
        {
            _saidas = saidas;
            _opcoesSaida = saidas.Select(saida => new OpcaoSaidaAudio(saida)).ToArray();
            SelecionarSaidaConfirmada();
            ListaSaidas.ItemsSource = _opcoesSaida;
        }
        if (_saidaPendente is null && MensagemSaida.Text is "" or "Nenhuma saída ativa.")
            MensagemSaida.Text = saidas.Count == 0 ? "Nenhuma saída ativa." : "";
    }

    private void SelecionarSaidaConfirmada()
    {
        foreach (var opcao in _opcoesSaida) opcao.AtualizarPadrao(_ultimoStatus?.IdDispositivo);
    }

    private void AoSelecionarSaida(object sender, RoutedEventArgs e)
    {
        if (!_inicializado || sender is not System.Windows.Controls.Button { DataContext: OpcaoSaidaAudio saida } || saida.Padrao) return;
        _saidaPendente = saida.Id;
        _confirmarSaidaAte = Environment.TickCount64 + 1500;
        MensagemSaida.Text = "Verificando saída…";
        if (!SeletorSaidaWindows.SolicitarTroca(saida.Id)) _confirmarSaidaAte = 0;
        AtualizarAudioUi();
    }

    private void VerificarTrocaSaida()
    {
        if (_saidaPendente is not { } id) return;
        if (_ultimoStatus?.IdDispositivo == id && SeletorSaidaWindows.Confirmar(id))
        {
            _saidaPendente = null;
            MensagemSaida.Text = "";
            SelecionarSaidaConfirmada();
        }
        else if (Environment.TickCount64 >= _confirmarSaidaAte)
        {
            _saidaPendente = null;
            MensagemSaida.Text = "Troca não confirmada. Use Som no Windows.";
            SelecionarSaidaConfirmada();
        }
    }

    private void AoAbrirSomWindows(object sender, RoutedEventArgs e)
    {
        if (!SeletorSaidaWindows.AbrirConfiguracoes()) MensagemSaida.Text = "Abra Configurações → Sistema → Som.";
    }

    private void RecolherPaineisAudio()
    {
        DefinirMixerAberto(false, animar: false);
        DefinirSaidasAbertas(false, animar: false);
    }
}

