using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Shell;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using PUFO.App;
using PUFO.App.Controls;
using PUFO.App.Models;
using PUFO.App.Services.Midia;
using Status = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;

internal static partial class Program
{
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint janela, uint mensagem, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint GetWindowDpiAwarenessContext(nint janela);

    [DllImport("user32.dll")]
    private static extern bool AreDpiAwarenessContextsEqual(nint primeiro, nint segundo);

    private static void TestarBarra(SettingsWindow janela)
    {
        Check(WindowChrome.GetWindowChrome(janela) is { CaptionHeight: > 0, UseAeroCaptionButtons: false } &&
            janela.ResizeMode == ResizeMode.CanResize, "Barra personalizada mantém área nativa de arrasto e redimensionamento");
        nint hwnd = new WindowInteropHelper(janela).Handle;
        Check(AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(hwnd), -4), "Janela usa PerMonitorV2 para DPI por monitor");
        int Hit(FrameworkElement elemento, double x, double y)
        {
            var ponto = elemento.PointToScreen(new System.Windows.Point(x, y));
            int coordenadas = ((int)ponto.Y << 16) | ((int)ponto.X & 0xffff);
            return (int)SendMessage(hwnd, 0x84, 0, coordenadas);
        }
        var fechar = (Button)janela.FindName("Fechar");
        Check(Hit(janela, 150, 20) == 2 && Hit(fechar, fechar.ActualWidth / 2, fechar.ActualHeight / 2) == 1 &&
            Hit(janela, 2, janela.ActualHeight / 2) == 10, "Windows distingue título arrastável, botão e borda redimensionável");
        void Clicar(string nome)
        {
            ((Button)janela.FindName(nome)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(100);
        }
        Clicar("Maximizar");
        Check(janela.WindowState == WindowState.Maximized, "Botão personalizado maximiza");
        var area = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea;
        var centroFechar = fechar.PointToScreen(new System.Windows.Point(fechar.ActualWidth / 2, fechar.ActualHeight / 2));
        var abas = (TabControl)janela.FindName("Categorias");
        var cantoInferior = abas.PointToScreen(new System.Windows.Point(abas.ActualWidth - 1, abas.ActualHeight - 1));
        Check(area.Contains((int)centroFechar.X, (int)centroFechar.Y) && area.Contains((int)cantoInferior.X, (int)cantoInferior.Y),
            "Janela maximizada mantém controles na área útil do monitor atual");
        Clicar("Maximizar");
        Check(janela.WindowState == WindowState.Normal, "Botão personalizado restaura");
        Clicar("Minimizar");
        Check(janela.WindowState == WindowState.Minimized, "Botão personalizado minimiza");
        janela.WindowState = WindowState.Normal;
    }

    private static void TestarMidia(MainWindow window)
    {
        var painel = (CartaoMidia)window.FindName("MidiaCompleta");
        var previa = (CartaoMidia)window.FindName("MidiaCompacta");
        window.ReceberSessoes([], null);
        Check(painel.Visibility == Visibility.Collapsed && previa.Visibility == Visibility.Collapsed, "Sem sessões não há player vazio");
        var origem = DateTimeOffset.UtcNow;
        var a = new SessaoMidia(1, "Player A") { Estado = Status.Playing, Titulo = "Faixa A", Pause = true, Proxima = true,
            Inicio = TimeSpan.FromSeconds(10), Fim = TimeSpan.FromSeconds(110), Posicao = TimeSpan.FromSeconds(30), Atualizado = origem };
        var b = new SessaoMidia(2, "Player A") { Estado = Status.Paused, Play = true, Titulo = "Vídeo B" };
        var selecao = new SelecaoMidia();
        selecao.Atualizar([a, b], b.Id);
        Check(selecao.Atual == b, "Sessões da mesma origem preservam identidades e priorizam a sessão do Windows");
        selecao.Escolher(a.Id);
        selecao.Atualizar([a, b with { Titulo = "Outro título" }], b.Id);
        Check(selecao.Atual == a, "Metadados de outra sessão preservam a escolha manual");
        selecao.Atualizar([b], b.Id);
        Check(selecao.Atual == b, "Encerramento da sessão manual volta à sessão disponível");
        State(window, "Repouso");
        window.ReceberSessoes([a,b], a.Id);
        Check((EstadoIlha)Campo(window, "_estadoIlha")! == EstadoIlha.Repouso && !previa.IsVisible, "Descoberta de reprodução mantém repouso compacto");
        State(window, "Previa");
        Pump(310);
        Check(!previa.IsVisible && ((ItemsControl)window.FindName("Atalhos")).IsVisible, "Hover padrão preserva a fileira compacta durante reprodução");
        var atalhos = (ItemsControl)window.FindName("Atalhos");
        var moduloAudio = atalhos.Items.Cast<ModuloPrevia>().Single(m => m.Id == "audio");
        var apresentador = (ContentPresenter)atalhos.ItemContainerGenerator.ContainerFromItem(moduloAudio);
        ((Button)System.Windows.Media.VisualTreeHelper.GetChild(apresentador, 0)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check(previa.IsVisible && ((Image)previa.FindName("Capa")).Source is null, "Prévia de reprodução aceita capa ausente");
        State(window, "Completo");
        Pump(320);
        int transicao = (int)Campo(window, "_transicao")!;
        for (int i = 0; i < 20; i++) window.ReceberSessoes([a with { Titulo = $"Faixa {i}" }, b], a.Id);
        Pump(100);
        Check(((TextBlock)painel.FindName("Titulo")).Text == "Faixa 19" && transicao == (int)Campo(window, "_transicao")!,
            "Atualizações rápidas trocam metadados sem reiniciar animações");
        Check(((Button)painel.FindName("Proxima")).IsEnabled && !((Button)painel.FindName("Anterior")).IsEnabled &&
            !((Slider)painel.FindName("Progresso")).IsEnabled, "Capacidades reais governam anterior, próxima e seek");
        ComandoMidia? enviado = null;
        painel.Comando += (_, comando, _) => enviado = comando;
        ((Button)painel.FindName("Principal")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check(enviado == ComandoMidia.Pausar && a.SegundosAtuais(origem.AddSeconds(5)) == 25, "Sessão em reprodução oferece pausa e avança a posição local");
        window.ReceberSessoes([b], b.Id);
        ((Button)painel.FindName("Principal")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check(enviado == ComandoMidia.Reproduzir && previa.Visibility == Visibility.Collapsed, "Sessão pausada oferece play e sai da prévia");
        var comSeek = a with { Seek = true, MinSeek = a.Inicio, MaxSeek = a.Fim };
        window.ReceberSessoes([comSeek,b], a.Id);
        Check(((Slider)painel.FindName("Progresso")).IsEnabled && painel.IsVisible && ((Slider)window.FindName("SliderVolume")).IsVisible,
            "Seek e controle geral de áudio coexistem no painel");
        void Escolher(int indice)
        {
            ((Button)painel.FindName("BotaoEscolha")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            var opcoes = (ItemsControl)painel.FindName("Opcoes");
            opcoes.UpdateLayout();
            var item = (ContentPresenter)opcoes.ItemContainerGenerator.ContainerFromIndex(indice);
            var botao = (Button)item.ContentTemplate.FindName("Selecionar", item);
            botao.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }
        Escolher(2);
        window.ReceberSessoes([comSeek with { Titulo = "Faixa seguinte" }, b], a.Id);
        Check(((TextBlock)painel.FindName("Titulo")).Text == b.Titulo, "Seletor visual conserva a escolha enquanto o outro player troca de faixa");
        Escolher(0);
        Check(((TextBlock)painel.FindName("Titulo")).Text == "Faixa seguinte", "Seletor visual permite voltar a seguir o Windows");
        Pump(320);
        Renderizar(window, "pufo-midia");
        window.ReceberSessoes([], null);
        Pump(320);
        Check(painel.Visibility == Visibility.Collapsed, "Remover última sessão recolhe somente mídia");
    }

    private static void ConsultarMidiaWindows(MainWindow window)
    {
        using var fonte = new MidiaWindows(window.Dispatcher);
        int? quantidade = null;
        string? falha = null;
        IReadOnlyList<SessaoMidia> observadas = [];
        fonte.Mudou += (sessoes, _) => { quantidade = sessoes.Count; observadas = sessoes; };
        fonte.Falhou += erro => falha = erro;
        window.Dispatcher.InvokeAsync(() => fonte.IniciarAsync());
        for (int i = 0; i < 50 && quantidade is null && falha is null; i++) Pump(100);
        if (quantidade is > 0)
        {
            for (int i = 0; i < 15 && observadas.All(s => s.Titulo.Length == 0); i++) Pump(100);
            Console.WriteLine($"GSMTC real: {observadas.Count(s => s.Tocando)} em reprodução, {observadas.Count(s => s.Titulo.Length > 0)} com título publicado.");
        }
        Console.WriteLine(falha is null && quantidade.HasValue ? $"GSMTC real: {quantidade} sessão(ões)." : $"GSMTC: {falha ?? "sem resposta no prazo"}");
        Check(quantidade.HasValue || falha is not null, "Consulta nativa GSMTC retorna catálogo ou indisponibilidade explícita sem enviar comandos");
    }
}
