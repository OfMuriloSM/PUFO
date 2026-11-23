using System.Windows;
using System.Windows.Threading;
using PUFO.App.Models;
using PUFO.App.Services.Configuracoes;

namespace PUFO.App;

public partial class MainWindow
{
    private readonly ServicoConfiguracoes _servicoConfiguracoes;
    private readonly InicializacaoWindows _inicializacaoWindows = new();
    private readonly DispatcherTimer _timerSalvarConfiguracoes = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private ConfiguracoesPufo _configuracoes;
    private SettingsWindow? _janelaConfiguracoes;
    private bool _configuracoesPendentes;

    private bool _abrindoConfiguracoesDaIlha;
    private void AoAbrirConfiguracoesNaIlha(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        AbrirConfiguracoesDaIlha();
    }

    private void AbrirConfiguracoesDaIlha()
    {
        _abrindoConfiguracoesDaIlha = true;
        try { AbrirConfiguracoes(); }
        finally { _abrindoConfiguracoesDaIlha = false; }
    }

    internal SettingsWindow AbrirConfiguracoes()
    {
        if (_janelaConfiguracoes is null)
        {
            var janela = new SettingsWindow(_configuracoes, _servicoConfiguracoes.Pasta, _inicializacaoWindows);
            _janelaConfiguracoes = janela;
            janela.AtualizarAudio(_ultimoStatus);
            janela.AtualizarMidia(_selecaoMidia.Atual, _erroMidia);
            janela.PreferenciasAlteradas += AplicarPreferencias;
            janela.Closed += (_, _) =>
            {
                janela.PreferenciasAlteradas -= AplicarPreferencias;
                _janelaConfiguracoes = null;
                SalvarPreferencias();
            };
            janela.Show();
        }
        if (_janelaConfiguracoes.WindowState == WindowState.Minimized)
            _janelaConfiguracoes.WindowState = WindowState.Normal;
        _janelaConfiguracoes.Activate();
        return _janelaConfiguracoes;
    }

    private void ConfigurarPreferencias()
    {
        AtualizarIntervalos();
        _timerSalvarConfiguracoes.Tick += (_, _) => SalvarPreferencias();
    }

    private void AtualizarIntervalos()
    {
        _timerHover.Interval = TimeSpan.FromMilliseconds(_configuracoes.HoverMs);
        _timerFechamento.Interval = TimeSpan.FromMilliseconds(_configuracoes.FechamentoMs);
    }

    private void AplicarPreferencias(ConfiguracoesPufo preferencias)
    {
        preferencias = preferencias.Normalizar();
        var anteriores = _configuracoes;
        if (preferencias == anteriores) return;
        _configuracoes = preferencias;
        if (preferencias.MidiaAtiva != anteriores.MidiaAtiva) ConfigurarFonteMidia();
        if (preferencias.MidiaNaPrevia != anteriores.MidiaNaPrevia || preferencias.PrioridadeMidia != anteriores.PrioridadeMidia)
            ApresentarMidia();
        AtualizarIntervalos();
        if (anteriores.PreviaContextual && !preferencias.PreviaContextual && _previaContextual)
        {
            _previaContextual = false;
            _timerContexto.Stop();
            if (!PonteiroSobreIlha() && !SliderPrevia.IsMouseCaptureWithin) MudarEstado(EstadoIlha.Repouso);
            else
            {
                AtualizarConteudo();
                AgendarMedicao();
            }
        }
        if (!preferencias.Animacoes && anteriores.Animacoes)
        {
            Revelar(RevelaMixer, PainelMixer, _mixerAberto, animar: false);
            Revelar(RevelaSaidas, PainelSaidas, _saidasAbertas, animar: false);
            Revelar(RevelaMidia, MidiaCompleta, RevelaMidia.IsHitTestVisible, animar: false);
            HostModulo.BeginAnimation(OpacityProperty, null);
            _tamanhoDestino = default;
            AnimarDimensoes(false);
        }
        if (preferencias.MixerExpandido != anteriores.MixerExpandido && _estadoIlha == EstadoIlha.Completo && !_completoPersonalizado)
        {
            DefinirMixerAberto(preferencias.MixerExpandido);
            if (_mixerAberto) AtualizarMixer();
        }
        _configuracoesPendentes = true;
        _timerSalvarConfiguracoes.Stop();
        _timerSalvarConfiguracoes.Start();
    }

    private void SalvarPreferencias()
    {
        _timerSalvarConfiguracoes.Stop();
        if (!_configuracoesPendentes) return;
        if (_servicoConfiguracoes.Salvar(_configuracoes))
        {
            _configuracoesPendentes = false;
            _janelaConfiguracoes?.MostrarAviso("");
        }
        else _janelaConfiguracoes?.MostrarAviso("Não foi possível salvar as configurações.");
    }

    private void EncerrarConfiguracoes()
    {
        SalvarPreferencias();
        _janelaConfiguracoes?.Close();
    }
}
