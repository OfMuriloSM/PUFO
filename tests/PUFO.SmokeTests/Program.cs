using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using PUFO.App.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using PUFO.App;
using PUFO.App.Services.Audio;
using PUFO.App.Models;
using PUFO.App.Services.Configuracoes;

internal static partial class Program
{
    private static int _checks;
    [STAThread]
    private static int Main()
    {
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        MainWindow? window = null;
        var pasta = Directory.CreateTempSubdirectory("pufo-smoke-");
        var preferencias = new ServicoConfiguracoes(pasta.FullName);
        try
        {
            window = new MainWindow(preferencias, conectarMidia: false);
            window.Show();
            Pump();
            var capsule = (Border)window.FindName("Capsula");
            Check(capsule.TransformToAncestor(window).Transform(new System.Windows.Point()).Y < 0 && capsule.ActualWidth < 160 && capsule.ActualHeight < 30, "Repouso compacto parcialmente oculto");
            Check(window.Topmost && !window.ShowInTaskbar, "Topmost e ausencia na barra de tarefas");
            int style = GetWindowLong(new WindowInteropHelper(window).Handle, -20);
            Check((style & 0x80) != 0 && (style & 0x40000) == 0, "ToolWindow: ausencia do Alt+Tab");
            // Freeze interaction timers so live cursor/foreground cannot change deterministic layout checks.
            foreach (var field in typeof(MainWindow).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                if (field.GetValue(window) is DispatcherTimer timer) timer.Stop();
            SetContent(window, "Previa", null);
            State(window, "Previa");
            Pump(320);
            Check(Math.Abs(capsule.ActualWidth - 18) < 1 && Math.Abs(capsule.ActualHeight - 18) < 1, "Previa vazia vira bolinha");
            var content = new Border { Width = 218, Height = 42 };
            SetContent(window, "Previa", content);
            Pump();
            Check(Math.Abs(capsule.ActualWidth - 220) < 1 && Math.Abs(capsule.ActualHeight - 44) < 1, "Previa mede conteudo real");
            content.Width = 286;
            content.Height = 66;
            Pump();
            Check(Math.Abs(capsule.ActualWidth - 288) < 1 && Math.Abs(capsule.ActualHeight - 68) < 1, "Conteudo alterado redimensiona capsula");
            State(window, "Completo");
            Pump();
            var full = (Grid)window.FindName("ViewCompleto");
            Check(capsule.ActualHeight >= full.DesiredSize.Height, "Painel comporta todos os controles");
            Check(window.Left >= 0 && window.Left + window.ActualWidth <= SystemParameters.PrimaryScreenWidth + 1, "Painel dentro da tela");
            Check(Math.Abs(window.Left + window.ActualWidth / 2 - SystemParameters.PrimaryScreenWidth / 2) < 1, "Centro horizontal estavel");
            Renderizar(window, "pufo-completo");
            TestarAudio(window, capsule);
            State(window, "Repouso");
            State(window, "Completo");
            State(window, "Previa");
            State(window, "Repouso");
            Pump();
            Check(capsule.ActualWidth < 160 && capsule.ActualHeight < 30, "Transicoes interrompidas terminam no estado correto");
            Check(!capsule.HasAnimatedProperties, "Animacoes de tamanho liberam relogios");
            var status = new ServicoAudioWindows().ObterStatus();
            Check(status.VolumePercentual is >= 0 and <= 100 && !string.IsNullOrWhiteSpace(status.NomeDispositivo), "NAudio retorna status valido ou ausencia segura");
            Check(((DispatcherTimer)Campo(window, "_timerHover")!).Interval == TimeSpan.FromMilliseconds(500), "Hover aguarda 500 ms");
            Check(((DispatcherTimer)Campo(window, "_timerFechamento")!).Interval == TimeSpan.FromMilliseconds(500), "Fechamento aguarda 500 ms");
            var saidas = SeletorSaidaWindows.Listar();
            Check(saidas is null || saidas.Select(saida => saida.Id).Distinct().Count() == saidas.Count, "Enumeração de saídas sem IDs duplicados");
            using (var mixer = new MixerWindows())
            {
                var grupos = mixer.Ler(status.IdDispositivo);
                Check(grupos is null || grupos.All(grupo => grupo.Volume is >= 0 and <= 100), "Leitura WASAPI de sessões sem escrever no Windows");
                Console.WriteLine($"Sessões controláveis agrupadas nesta máquina: {grupos?.Count.ToString() ?? "leitura indisponível"}.");
            }
            TestarAtividade();
            TestarPreviaModular(window);
            TestarMidia(window);
            TestarConfiguracoes(window, capsule, preferencias);
            ConsultarMidiaWindows(window);
            TestarVoz(window);
            Console.WriteLine($"PASS: {_checks} verificacoes. Audio disponivel: {status.Disponivel}.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally
        {
            window?.Close();
            app.Shutdown();
            foreach (var arquivo in pasta.GetFiles()) arquivo.Delete();
            pasta.Delete();
        }
    }
    private static void TestarAudio(MainWindow window, Border capsule)
    {
        var geral = (SliderSincronizado)window.FindName("SliderVolume");
        var previa = (SliderSincronizado)window.FindName("SliderPrevia");
        var btnMixer = (Button)window.FindName("BtnMixer");
        var linhas = (StackPanel)window.FindName("LinhasMixer");
        var semSessoes = (TextBlock)window.FindName("MixerVazio");
        var mute = (Button)window.FindName("BtnMuteCompleto");
        ApplyStatus(window, new AudioStatus("Duas saídas de teste", 70, false, true, "teste:A"));
        Pump(320);
        var alturaInicial = capsule.ActualHeight;
        btnMixer.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check(AutomationProperties.GetName(btnMixer) == "Recolher mixer" && semSessoes.Visibility == Visibility.Visible,
            "Botão abre o mixer embutido com estado vazio");

        var grupo = Guid.NewGuid();
        var navegador = MixerWindows.Identificar(false, 123, grupo, "A", "n1");
        var outraSessaoMesmoGrupo = MixerWindows.Identificar(false, 123, grupo, "B", "n2");
        var outroProcesso = MixerWindows.Identificar(false, 456, grupo, "A", "n3");
        var outroGrupo = MixerWindows.Identificar(false, 123, Guid.Empty, "B", "n4");
        var sistema = MixerWindows.Identificar(true, 0, Guid.Empty, "", "s");
        Check(navegador == outraSessaoMesmoGrupo && navegador != outroGrupo && navegador != outroProcesso && sistema.Sistema,
            "Agrupamento respeita identidade Core Audio, processo e sons do sistema");
        Check(MixerWindows.Identificar(false, 0, grupo, "A", "p1") != MixerWindows.Identificar(false, 0, grupo, "A", "p2"),
            "Sessões de origem desconhecida mantêm controles independentes");
        SessaoAudio[] sessoes = [new("n1", navegador, "Navegador", 30, false), new("n2", outraSessaoMesmoGrupo, "Navegador", 50, true),
            new("s", sistema, "Sons do sistema", 75, false)];
        var grupos = MixerWindows.Agrupar(sessoes.Concat([sessoes[0]]));
        var agregado = grupos.Single(g => g.Chave == navegador);
        Check(grupos.Count == 2 && agregado.Volume == 40 && agregado.VolumeMisto && agregado.MudoMisto,
            "Mixer consolida duplicatas e sinaliza volume e mute mistos");
        AplicarGrupos(window, grupos);
        Pump(320);
        Check(capsule.ActualHeight > alturaInicial && linhas.Children.Count == 2 && window.FindName("SliderVolume") == geral,
            "Mixer expande para baixo com a mesma barra geral");
        Renderizar(window, "pufo-mixer");
        AplicarGrupos(window, grupos.Concat(Enumerable.Range(1, 12).Select(i =>
            new GrupoAudio(new(false, (uint)i, "teste"), $"Aplicativo {i}", 50, false, false, false, null))).ToArray());
        Pump(320);
        var rolagem = (ScrollViewer)window.FindName("RolagemMixer");
        Check(rolagem.ScrollableHeight > 0 && rolagem.ActualHeight <= 215 && capsule.ActualHeight < SystemParameters.WorkArea.Height,
            "Lista longa tem rolagem interna e mantém o painel dentro da tela");
        AplicarGrupos(window, grupos);
        Pump(320);
        var linha = linhas.Children.OfType<LinhaMixer>().First();
        var volumeLinha = (SliderSincronizado)linha.FindName("Volume");
        int escritas = 0;
        linha.AlterouVolume += (_, _) => escritas++;
        Arrastar(volumeLinha, true);
        AplicarGrupos(window, [agregado with { Volume = 65 }]);
        Check(linhas.Children.Count == 1 && linhas.Children[0] == linha && volumeLinha.Value == 40 && escritas == 0,
            "Sessão removida sem recriar linha restante nem interromper seu arrasto");
        Arrastar(volumeLinha, false);
        AplicarGrupos(window, [agregado with { Volume = 65 }]);
        Check(volumeLinha.Value == 65 && escritas == 0, "Sessão reconcilia volume após arrasto sem reescrita");
        int revisao = (int)Campo(window, "_transicao")!;
        Arrastar(geral, true);
        for (int volume = 69; volume >= 55; volume--)
            ApplyStatus(window, new AudioStatus("Duas saídas de teste", volume, false, true, "teste:A"));
        Check(geral.Value == 70 && previa.Value == 55 && (int)Campo(window, "_transicao")! == revisao,
            "Volume geral rápido não reinicia animação nem move o slider em arrasto");
        Arrastar(geral, false);
        ApplyStatus(window, new AudioStatus("Duas saídas de teste", 55, true, true, "teste:A"));
        Check(geral.Value == 55 && AutomationProperties.GetName(mute) == "Ativar áudio", "Volume e mute externos sincronizados após liberar arrasto");
        var revelaMixer = (PainelRevelavel)window.FindName("RevelaMixer");
        var painelMixer = (StackPanel)window.FindName("PainelMixer");
        var conteudoMixer = (Grid)window.FindName("ConteudoIlha");
        btnMixer.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        if (SystemParameters.ClientAreaAnimation)
        {
            Pump(75);
            Check(revelaMixer.Progresso is > 0 and < 1 && painelMixer.Visibility == Visibility.Visible &&
                Math.Abs(capsule.ActualHeight - (conteudoMixer.DesiredSize.Height + 2)) <= 12,
                "Recolhimento do mixer mantém o conteúdo e a cápsula acompanha seu tamanho");
        }
        Pump(320);
        Check(AutomationProperties.GetName(btnMixer) == "Expandir mixer" && geral.Value == 55 &&
            Math.Abs(capsule.ActualHeight - alturaInicial) < 1 && (EstadoIlha)Campo(window, "_estadoIlha")! == EstadoIlha.Completo,
            "Recolher mixer preserva volume e estado geral da ilha");
        TestarSeletor(window, capsule);
        ApplyStatus(window, new AudioStatus("Nova saída", 37, false, true, "teste:B"));
        Check(geral.Value == 37 && linhas.Children.Count == 0, "Troca de saída descarta sessões da saída anterior");
        ApplyStatus(window, AudioStatus.Indisponivel);
        Check(!geral.IsEnabled && linhas.Children.Count == 0, "Desconexão limpa mixer e desabilita controles gerais");
        ApplyStatus(window, new AudioStatus("Nova saída", 21, false, true, "teste:B"));
        Check(geral.IsEnabled && geral.Value == 21, "Reconexão restaura volume e controles");

        State(window, "Repouso");
        typeof(MainWindow).GetField("_previaPersonalizada", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, false);
        ApplyStatus(window, new AudioStatus("Nova saída", 20, false, true, "teste:B"));
        Pump(320);
        revisao = (int)Campo(window, "_transicao")!;
        var conteudo = (Grid)window.FindName("ConteudoIlha");
        for (int volume = 19; volume >= 10; volume--)
            ApplyStatus(window, new AudioStatus("Nova saída", volume, false, true, "teste:B"));
        Pump(320);
        Check((EstadoIlha)Campo(window, "_estadoIlha")! == EstadoIlha.Previa && previa.Value == 10 && conteudo.Opacity == 1 &&
            (int)Campo(window, "_transicao")! == revisao, "Prévia contextual continua visível sem repetir o fade em reduções sucessivas");
    }

    private static void TestarSeletor(MainWindow window, Border capsule)
    {
        var botao = (Button)window.FindName("BtnSaida");
        var lista = (ItemsControl)window.FindName("ListaSaidas");
        var revela = (PainelRevelavel)window.FindName("RevelaSaidas");
        double alturaFechada = capsule.ActualHeight;
        botao.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        typeof(MainWindow).GetMethod("AplicarSaidas", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [new SaidaAudio[] { new("teste:A", "Alto-falantes de teste"), new("teste:B", "Fones de teste") }]);
        Pump(320);
        var opcoes = lista.Items.OfType<OpcaoSaidaAudio>().ToArray();
        Check(opcoes.Length == 2 && opcoes[0].Padrao && !opcoes[1].Padrao && revela.Progresso == 1 && capsule.ActualHeight > alturaFechada,
            "Seletor embutido marca apenas a saída confirmada e cresce por conteúdo");
        Renderizar(window, "pufo-saidas");
        botao.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(320);
        Check(revela.Progresso == 0 && Math.Abs(capsule.ActualHeight - alturaFechada) < 1,
            "Seletor recolhido devolve toda a altura à cápsula");
    }

    private static void TestarConfiguracoes(MainWindow window, Border capsule, ServicoConfiguracoes servico)
    {
        var padroes = new ConfiguracoesPufo();
        Check(servico.Carregar() == padroes && !File.Exists(servico.Arquivo), "Instalação nova usa padrões sem gravar arquivo");
        State(window, "Completo");
        window.Activate();
        ((Button)window.FindName("Engrenagem")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var configuracoes = (SettingsWindow)Campo(window, "_janelaConfiguracoes")!;
        Check(configuracoes.IsVisible && (EstadoIlha)Campo(window, "_estadoIlha")! == EstadoIlha.Completo,
            "Engrenagem abre configurações e preserva o painel");
        dynamic bandeja = Campo(window, "_iconeBandeja")!;
        bandeja.ContextMenuStrip.Items[0].PerformClick();
        Check(ReferenceEquals(configuracoes, Campo(window, "_janelaConfiguracoes")), "Bandeja reativa a mesma janela da engrenagem");
        TestarBarra(configuracoes);
        Check(ReferenceEquals(configuracoes, window.AbrirConfiguracoes()) && configuracoes.ShowInTaskbar && configuracoes.Owner is null,
            "Configurações reutilizam uma janela independente com botão na barra de tarefas");
        int estilo = GetWindowLong(new WindowInteropHelper(configuracoes).Handle, -20);
        Check((estilo & 0x80) == 0, "Configurações ficam fora do estilo ToolWindow usado pela ilha");
        window.Hide();
        Check(configuracoes.IsVisible && !window.IsVisible, "Ocultar a ilha mantém Configurações aberta");
        window.Show();
        Pump(150);
        var categorias = (TabControl)configuracoes.FindName("Categorias");
        categorias.SelectedIndex = 1;
        ((Slider)configuracoes.FindName("TempoHover")).Value = 300;
        ((Slider)configuracoes.FindName("TempoFechamento")).Value = 800;
        ((Slider)configuracoes.FindName("Velocidade")).Value = 150;
        Check(((DispatcherTimer)Campo(window, "_timerHover")!).Interval.TotalMilliseconds == 300 &&
            ((DispatcherTimer)Campo(window, "_timerFechamento")!).Interval.TotalMilliseconds == 800 && !File.Exists(servico.Arquivo),
            "Preferências alteram os timers imediatamente e adiam a gravação");
        Pump(180);
        Renderizar(configuracoes, "pufo-configuracoes");
        ((CheckBox)configuracoes.FindName("AnimarIlha")).IsChecked = false;
        categorias.SelectedIndex = 2;
        ((CheckBox)configuracoes.FindName("PreviaAudio")).IsChecked = false;
        ((CheckBox)configuracoes.FindName("AbrirMixer")).IsChecked = true;
        State(window, "Repouso");
        ApplyStatus(window, new AudioStatus("Saída de teste", 20, false, true, "teste:A"));
        ApplyStatus(window, new AudioStatus("Saída de teste", 22, false, true, "teste:A"));
        Check((EstadoIlha)Campo(window, "_estadoIlha")! == EstadoIlha.Repouso &&
            ((TextBlock)configuracoes.FindName("SaidaAtual")).Text == "Saída de teste", "Prévia automática desativada mantém a ilha em repouso e atualiza a saída nas configurações");
        State(window, "Completo");
        var revelaMixer = (PainelRevelavel)window.FindName("RevelaMixer");
        Check(!capsule.HasAnimatedProperties && revelaMixer.Progresso == 1 && !revelaMixer.HasAnimatedProperties &&
            ((Slider)window.FindName("SliderVolume")).Value == 22, "Transições desligadas e mixer aberto por preferência preservam controle manual de áudio");
        Pump(700);
        var esperado = padroes with { HoverMs = 300, FechamentoMs = 800, VelocidadeAnimacoes = 150, Animacoes = false, PreviaContextual = false, MixerExpandido = true };
        Check(new ServicoConfiguracoes(servico.Pasta).Carregar() == esperado, "Preferências são gravadas e recarregadas de JSON");
        categorias.SelectedIndex = 3;
        var faixa = new SessaoMidia(7, "Fonte de teste") { Titulo = "Faixa de teste",
            Estado = global::Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing };
        window.ReceberSessoes([faixa], faixa.Id);
        Check(((TextBlock)configuracoes.FindName("MidiaAtual")).Text == "Faixa de teste · Fonte de teste",
            "Configurações mostram a faixa selecionada e sua origem");
        ((CheckBox)configuracoes.FindName("PreviaAudio")).IsChecked = true;
        var prioridade = (CheckBox)configuracoes.FindName("PriorizarMidia");
        prioridade.IsChecked = true;
        State(window, "Repouso");
        ApplyStatus(window, new AudioStatus("Saída de teste", 23, false, true, "teste:A"));
        Check((EstadoIlha)Campo(window, "_estadoIlha")! == EstadoIlha.Repouso, "Prioridade de mídia impede abertura automática por volume");
        prioridade.IsChecked = false;
        ApplyStatus(window, new AudioStatus("Saída de teste", 24, false, true, "teste:A"));
        window.UpdateLayout();
        Check(((CartaoMidia)window.FindName("MidiaCompacta")).IsVisible && ((Slider)window.FindName("SliderPrevia")).IsVisible,
            "Volume contextual e faixa em reprodução compartilham a prévia");
        ((CheckBox)configuracoes.FindName("PreviaAudio")).IsChecked = false;
        ((CheckBox)configuracoes.FindName("PreviaMidia")).IsChecked = false;
        Check(((CartaoMidia)window.FindName("MidiaCompacta")).Visibility == Visibility.Collapsed &&
            ((CartaoMidia)window.FindName("MidiaCompleta")).Visibility == Visibility.Visible, "Desativar mídia na prévia preserva o painel completo");
        ((CheckBox)configuracoes.FindName("PriorizarMidia")).IsChecked = true;
        ((CheckBox)configuracoes.FindName("HabilitarMidia")).IsChecked = false;
        ((Button)configuracoes.FindName("Fechar")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump(80);
        esperado = esperado with { MidiaAtiva = false, MidiaNaPrevia = false, PrioridadeMidia = true };
        Check(!configuracoes.IsVisible && servico.Carregar() == esperado, "Fechar pela barra salva as três preferências de mídia");
        var reaberta = window.AbrirConfiguracoes();
        Check(!ReferenceEquals(configuracoes, reaberta) && ((Slider)reaberta.FindName("TempoHover")).Value == 300,
            "Janela reaberta mostra as preferências aplicadas");
        reaberta.Close();
        Check(!InicializacaoWindows.DistribuicaoInstalada && !new InicializacaoWindows().Alterar(true),
            "Build de desenvolvimento recusa registro de inicialização");
        var limitada = esperado with { HoverMs = -20, FechamentoMs = 30000, VelocidadeAnimacoes = 0, Tema = (TemaPufo)99 };
        Check(servico.Salvar(limitada) && servico.Carregar() == limitada.Normalizar(), "Gravação limita atrasos e velocidade e normaliza tema inválido");
        File.WriteAllText(servico.Arquivo, "{ truncado");
        Check(servico.Carregar() == esperado, "JSON corrompido recupera a última cópia válida");
        File.WriteAllText(servico.Arquivo + ".bak", "{ inválido");
        Check(servico.Carregar() == padroes, "Sem cópia legível, configurações voltam aos padrões");
        File.WriteAllText(servico.Arquivo, """{"HoverMs":99999,"FechamentoMs":-300,"VelocidadeAnimacoes":9999} """);
        var lido = servico.Carregar();
        Check(lido.HoverMs == 2000 && lido.FechamentoMs == 100 && lido.VelocidadeAnimacoes == 200,
            "Valores editados manualmente também são limitados na leitura");
    }

    private static void Arrastar(Slider slider, bool inicio)
    {
        if (inicio) slider.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
        else slider.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
    }

    private static object? Campo(MainWindow window, string nome) => typeof(MainWindow)
        .GetField(nome, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);

    private static void AplicarGrupos(MainWindow window, IReadOnlyList<GrupoAudio> grupos) => typeof(MainWindow)
        .GetMethod("AplicarGruposMixer", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [grupos]);

    private static void Renderizar(Window window, string nome)
    {
        Directory.CreateDirectory("artifacts");
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create($"artifacts/{nome}.png");
        encoder.Save(file);
    }

    private static void ApplyStatus(MainWindow window, AudioStatus status) => typeof(MainWindow).GetMethod("AplicarStatusAudio", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [status]);
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + label);
        _checks++;
        Console.WriteLine("PASS: " + label);
    }
    private static void State(MainWindow window, string state)
    {
        var method = typeof(MainWindow).GetMethod("MudarEstado", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(window, [Enum.Parse(method.GetParameters()[0].ParameterType, state)]);
        foreach (var name in new[] { "_timerFechamento", "_timerCliqueFora" })
            ((DispatcherTimer)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Stop();
    }
    private static void SetContent(MainWindow window, string state, UIElement? content)
    {
        var method = typeof(MainWindow).GetMethod("DefinirConteudo", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(window, [Enum.Parse(method.GetParameters()[0].ParameterType, state), content]);
    }
    private static void Pump(int milliseconds = 700)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
}
