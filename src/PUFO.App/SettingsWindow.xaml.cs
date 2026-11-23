using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using PUFO.App.Models;
using PUFO.App.Services.Audio;
using PUFO.App.Services.Configuracoes;

namespace PUFO.App;

public partial class SettingsWindow : Window
{
    private readonly InicializacaoWindows _inicializacao;
    private readonly string _pasta;
    private ConfiguracoesPufo? _configuracoes;
    private AudioStatus? _audio;
    private bool _sincronizandoInicializacao;

    internal event Action<ConfiguracoesPufo>? PreferenciasAlteradas;

    internal SettingsWindow(ConfiguracoesPufo configuracoes, string pasta, InicializacaoWindows inicializacao)
    {
        _pasta = pasta;
        _inicializacao = inicializacao;
        InitializeComponent();
        TempoHover.Maximum = TempoFechamento.Maximum = ConfiguracoesPufo.AtrasoMaximo;
        TempoHover.Minimum = TempoFechamento.Minimum = ConfiguracoesPufo.AtrasoMinimo;
        Velocidade.Maximum = ConfiguracoesPufo.VelocidadeMaxima;
        Velocidade.Minimum = ConfiguracoesPufo.VelocidadeMinima;
        TempoHover.Value = configuracoes.HoverMs;
        TempoFechamento.Value = configuracoes.FechamentoMs;
        Velocidade.Value = configuracoes.VelocidadeAnimacoes;
        AnimarIlha.IsChecked = configuracoes.Animacoes;
        PreviaAudio.IsChecked = configuracoes.PreviaContextual;
        AbrirMixer.IsChecked = configuracoes.MixerExpandido;
        HabilitarMidia.IsChecked = configuracoes.MidiaAtiva;
        PreviaMidia.IsChecked = configuracoes.MidiaNaPrevia;
        PriorizarMidia.IsChecked = configuracoes.PrioridadeMidia;
        _configuracoes = configuracoes;
        AtualizarOpcoesMidia();
        StateChanged += (_, _) =>
        {
            bool maximizada = WindowState == WindowState.Maximized;
            Maximizar.ToolTip = maximizada ? "Restaurar" : "Maximizar";
            System.Windows.Automation.AutomationProperties.SetName(Maximizar, (string)Maximizar.ToolTip);
            SimboloMaximizar.Data = System.Windows.Media.Geometry.Parse(maximizada
                ? "M 5,4 V 1 H 14 V 10 H 11 M 2,5 H 10 V 13 H 2 Z" : "M 2,2 H 13 V 13 H 2 Z");
        };
        Versao.Text = typeof(SettingsWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "PUFO";
        AtualizarEstadoInicializacao();
        AtualizarAudio(null);
        AtualizarVelocidade();
    }

    private void AoMinimizar(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void AoMaximizar(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }
    private void AoFechar(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    private void AtualizarOpcoesMidia()
    {
        PreviaMidia.IsEnabled = HabilitarMidia.IsChecked == true;
        PriorizarMidia.IsEnabled = PreviaMidia.IsEnabled && PreviaMidia.IsChecked == true;
    }
    internal void AtualizarMidia(SessaoMidia? sessao, string? erro)
    {
        string texto = HabilitarMidia.IsChecked != true ? "Módulo desativado" : erro ??
            (sessao is null ? "Nenhuma sessão disponível" : sessao.Rotulo == sessao.Origem ? sessao.Origem : $"{sessao.Rotulo} · {sessao.Origem}");
        if (MidiaAtual.Text != texto) MidiaAtual.Text = texto;
    }

    internal void AtualizarAudio(AudioStatus? audio)
    {
        if (_audio?.NomeDispositivo == audio?.NomeDispositivo && _audio?.Disponivel == audio?.Disponivel && SaidaAtual.Text.Length > 0) return;
        _audio = audio;
        SaidaAtual.Text = audio?.NomeDispositivo ?? "Nenhuma saída de áudio";
        AtualizarModulos();
    }

    private void AtualizarModulos()
    {
        bool disponivel = _audio?.Disponivel == true;
        string estado = disponivel ? "Disponível" : "Aguardando saída de áudio";
        Modulos.ItemsSource = new ModuloExibido[]
        {
            new("Mídia", _configuracoes?.MidiaAtiva == true ? "Ativo" : "Desativado nas preferências"),
            new("Volume do sistema", estado),
            new("Mixer de aplicativos", estado),
            new("Prévia contextual", _configuracoes?.PreviaContextual == true
                ? (disponivel ? "Ativa" : estado) : "Desativada nas preferências")
        };
    }

    private void AoAlterarValor(object sender, RoutedPropertyChangedEventArgs<double> e) => AplicarPreferencias();
    private void AoAlterarPreferencia(object sender, RoutedEventArgs e) => AplicarPreferencias();

    private void AplicarPreferencias()
    {
        if (_configuracoes is not { } anteriores) return;
        var atuais = anteriores with
        {
            HoverMs = (int)TempoHover.Value,
            FechamentoMs = (int)TempoFechamento.Value,
            Animacoes = AnimarIlha.IsChecked == true,
            VelocidadeAnimacoes = (int)Velocidade.Value,
            PreviaContextual = PreviaAudio.IsChecked == true,
            MixerExpandido = AbrirMixer.IsChecked == true,
            MidiaAtiva = HabilitarMidia.IsChecked == true,
            MidiaNaPrevia = PreviaMidia.IsChecked == true,
            PrioridadeMidia = PriorizarMidia.IsChecked == true
        };
        if (atuais == anteriores) return;
        _configuracoes = atuais;
        if (atuais.Animacoes != anteriores.Animacoes) AtualizarVelocidade();
        AtualizarOpcoesMidia();
        if (atuais.PreviaContextual != anteriores.PreviaContextual || atuais.MidiaAtiva != anteriores.MidiaAtiva) AtualizarModulos();
        PreferenciasAlteradas?.Invoke(atuais);
    }

    private void AtualizarVelocidade()
    {
        bool ativo = _configuracoes?.Animacoes == true;
        Velocidade.IsEnabled = ativo;
        RotuloVelocidade.Opacity = ativo ? 1 : 0.5;
        if (!ativo && Categorias.Template?.FindName("PART_SelectedContentHost", Categorias) is ContentPresenter conteudo)
            conteudo.BeginAnimation(OpacityProperty, null);
    }

    private void AoMudarCategoria(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, Categorias) && AbaAssistente?.IsSelected == true) CarregarOpcoesVoz();
        if (!ReferenceEquals(e.OriginalSource, Categorias) || _configuracoes?.Animacoes != true ||
            !SystemParameters.ClientAreaAnimation || Categorias.Template?.FindName("PART_SelectedContentHost", Categorias) is not ContentPresenter conteudo) return;
        var animacao = new DoubleAnimation(0.7, 1, TimeSpan.FromMilliseconds(15000d / _configuracoes.VelocidadeAnimacoes))
        {
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        conteudo.BeginAnimation(OpacityProperty, animacao);
    }

    private void AtualizarEstadoInicializacao()
    {
        var estado = _inicializacao.Consultar();
        _sincronizandoInicializacao = true;
        try { IniciarWindows.IsChecked = estado.Ativo; }
        finally { _sincronizandoInicializacao = false; }
        IniciarWindows.IsEnabled = estado.PodeAlterar;
        EstadoInicializacao.Text = estado.Descricao;
    }

    private void AoMudarInicializacao(object sender, RoutedEventArgs e)
    {
        if (_sincronizandoInicializacao || _configuracoes is null) return;
        bool sucesso = _inicializacao.Alterar(IniciarWindows.IsChecked == true);
        AtualizarEstadoInicializacao();
        if (!sucesso) EstadoInicializacao.Text = "Não foi possível alterar a inicialização do PUFO.";
    }

    private void AoAbrirPasta(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_pasta);
            using var processo = Process.Start(new ProcessStartInfo(_pasta) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            MostrarAviso("Não foi possível abrir a pasta de configurações.");
        }
    }

    internal void MostrarAviso(string texto)
    {
        Aviso.Text = texto;
        Aviso.Visibility = texto.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private sealed record ModuloExibido(string Nome, string Estado);
}
