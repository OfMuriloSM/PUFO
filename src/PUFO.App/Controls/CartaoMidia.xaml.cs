using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PUFO.App.Models;
using Status = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;

namespace PUFO.App.Controls;

public partial class CartaoMidia : System.Windows.Controls.UserControl
{
    internal event Action<int, ComandoMidia, TimeSpan>? Comando;
    internal event Action<int?>? Selecionou;
    private SessaoMidia? _sessao;
    private bool _ajustando;
    private Opcao[] _opcoes = [];
    private sealed record Opcao(int? Id, string Nome, string Detalhe = "");
    public bool Compacto { get; set; }
    public CartaoMidia()
    {
        InitializeComponent();
        Progresso.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler((_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left && _sessao?.PodeBuscar == true) _ajustando = true;
        }), true);
        Progresso.AddHandler(Mouse.MouseUpEvent, new MouseButtonEventHandler((_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left && _ajustando) ConfirmarPosicao();
        }), true);
        Progresso.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, e) =>
        {
            if (e.Canceled) _ajustando = false;
            else if (_ajustando) ConfirmarPosicao();
        }), true);
        Progresso.PreviewKeyDown += (_, e) => { if (TeclaPosicao(e.Key)) _ajustando = true; };
        Progresso.KeyUp += (_, e) => { if (TeclaPosicao(e.Key) && _ajustando) ConfirmarPosicao(); };
        Progresso.LostKeyboardFocus += (_, _) => _ajustando = false;
        Progresso.MouseLeave += (_, _) => { if (!Progresso.IsMouseCaptureWithin) _ajustando = false; };
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) return;
            _ajustando = false;
            if (_sessao is null) Capa.Source = null;
        };
    }
    private static string RotuloEstado(Status estado) => estado switch
    {
        Status.Playing => "Reproduzindo", Status.Paused => "Pausado", Status.Stopped => "Parado", _ => "Mídia"
    };
    private static bool TeclaPosicao(Key key) => key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageDown or Key.PageUp;
    private void ConfirmarPosicao()
    {
        _ajustando = false;
        if (_sessao is { PodeBuscar: true } s)
            Comando?.Invoke(s.Id, ComandoMidia.Posicao, s.Inicio + TimeSpan.FromSeconds(Progresso.Value));
    }
    internal void Aplicar(SessaoMidia? s, IReadOnlyList<SessaoMidia> sessoes)
    {
        if (_sessao?.Id != s?.Id || _sessao?.Titulo != s?.Titulo)
        {
            _ajustando = false;
            Avisar("");
        }
        _sessao = s;
        if (s is null)
        {
            Lista.Visibility = Visibility.Collapsed;
            Opcoes.ItemsSource = _opcoes = [];
            if (!IsVisible) Capa.Source = null;
            return;
        }
        Capa.Source = s.Capa;
        double lado = Compacto ? 26 : 36;
        Moldura.Width = Moldura.Height = lado;
        RecorteCapa.Rect = new Rect(0, 0, lado, lado);
        Divisor.Visibility = Compacto ? Visibility.Collapsed : Visibility.Visible;
        Titulo.Text = s.Rotulo;
        string estado = RotuloEstado(s.Estado);
        Detalhe.Text = s.Detalhe == s.Rotulo ? estado : !Compacto && !s.Tocando ? $"{s.Detalhe} · {estado}" : s.Detalhe;
        ToolTip = $"{s.Rotulo} · {s.Origem}";
        Glifo.Data = (Geometry)FindResource(s.Tocando ? "PauseMidia" : "PlayMidia");
        string acao = s.Tocando ? "Pausar" : "Reproduzir";
        Principal.ToolTip = acao;
        AutomationProperties.SetName(Principal, acao);
        Principal.IsEnabled = s.Permite(s.AcaoPrincipal);
        Anterior.IsEnabled = s.Anterior;
        Proxima.IsEnabled = s.Proxima;
        Anterior.Visibility = Proxima.Visibility = Compacto ? Visibility.Collapsed : Visibility.Visible;
        if (Compacto) { Tempo.Visibility = Escolha.Visibility = Visibility.Collapsed; return; }
        Tempo.Visibility = s.TemTempo ? Visibility.Visible : Visibility.Collapsed;
        Progresso.IsEnabled = s.PodeBuscar;
        if (!s.PodeBuscar) _ajustando = false;
        Duracao.Text = Formatar(s.Fim - s.Inicio);
        AtualizarTempo();
        Escolha.Visibility = sessoes.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        if (sessoes.Count <= 1) Lista.Visibility = Visibility.Collapsed;
        NomeSessao.Text = $"Sessão · {s.Origem}";
        if (sessoes.Count > 1)
        {
            var opcoes = sessoes.Select(item => new Opcao(item.Id, item.Rotulo, $"{item.Origem} · {RotuloEstado(item.Estado)}"))
                .Prepend(new Opcao(null, "Seguir sessão ativa do Windows")).ToArray();
            if (!_opcoes.SequenceEqual(opcoes)) Opcoes.ItemsSource = _opcoes = opcoes;
        }
        AutomationProperties.SetHelpText(Principal, estado);
        Detalhe.ToolTip = estado;
    }
    internal void AtualizarTempo()
    {
        if (_sessao is not { TemTempo: true } s || Compacto || _ajustando) return;
        double segundos = s.SegundosAtuais(DateTimeOffset.UtcNow);
        Progresso.Maximum = (s.Fim - s.Inicio).TotalSeconds;
        Progresso.Value = segundos;
        Posicao.Text = Formatar(TimeSpan.FromSeconds(segundos));
    }
    private static string Formatar(TimeSpan t) => t.TotalHours >= 1
        ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    internal void Avisar(string mensagem)
    {
        Aviso.Text = mensagem;
        Aviso.Visibility = mensagem.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
    private void Enviar(ComandoMidia comando)
    {
        if (_sessao is { } s && s.Permite(comando)) Comando?.Invoke(s.Id, comando, default);
    }
    private void AoPrincipal(object sender, RoutedEventArgs e) { if (_sessao is { } s) Enviar(s.AcaoPrincipal); }
    private void AoAnterior(object sender, RoutedEventArgs e) => Enviar(ComandoMidia.Anterior);
    private void AoProxima(object sender, RoutedEventArgs e) => Enviar(ComandoMidia.Proxima);
    internal void AbrirSessoes()
    {
        if (Escolha.Visibility == Visibility.Visible)
        {
            Lista.Visibility = Visibility.Visible;
            BotaoEscolha.Focus();
        }
        else Principal.Focus();
    }

    private void AoAbrirLista(object sender, RoutedEventArgs e) => Lista.Visibility = Lista.IsVisible ? Visibility.Collapsed : Visibility.Visible;
    private void AoEscolher(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Opcao item }) Selecionou?.Invoke(item.Id);
        Lista.Visibility = Visibility.Collapsed;
    }
}
