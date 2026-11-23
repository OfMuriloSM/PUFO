using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using PUFO.App;
using PUFO.App.Controls;
using PUFO.App.Models;
using PUFO.App.Services;
using PUFO.App.Services.Audio;

internal static partial class Program
{
    private static void TestarAtividade()
    {
        var detector = new DetectorAtividade();
        Check(detector.Observar("a", true, false, 0, 0) == AtividadeAudio.Disponivel, "Sessão ativa com pico zero fica disponível");
        Check(detector.Observar("a", true, false, 0.15f, 100) == AtividadeAudio.Detectado &&
            detector.Observar("b", true, false, 0.6f, 100) == AtividadeAudio.Detectado, "Dois aplicativos podem apresentar som ao mesmo tempo");
        Check(detector.Observar("a", true, false, 0, 1200) == AtividadeAudio.Detectado &&
            detector.Observar("a", true, false, 0, 2000) == AtividadeAudio.Disponivel, "Retenção de pico tolera pausas e expira durante o silêncio");
        Check(detector.Observar("b", true, true, 1, 200) == AtividadeAudio.Silenciado &&
            detector.Observar("b", true, false, 0, 300) == AtividadeAudio.Disponivel, "Mute limpa o pico anteriormente detectado");
        Check(detector.Observar("c", true, false, null, 100) == AtividadeAudio.SemMedidor &&
            detector.Observar("d", false, false, 1, 100) == AtividadeAudio.Disponivel, "Medidor indisponível e sessão inativa têm estados próprios");
        var chave = new ChaveSessao(false, 7, "grupo");
        var agregado = MixerWindows.Agrupar([new("a", chave, "Teste", 100, false), new("b", chave, "Teste", 100, true, null, AtividadeAudio.Detectado)]).Single();
        Check(agregado.MudoMisto && agregado.Atividade == AtividadeAudio.Disponivel, "Volume em 100% e pico de uma sessão silenciada não indicam som do grupo");
        using var monitor = new MonitorSomWindows();
        var medicao = monitor.Ler();
        Check(medicao.Fontes.All(f => f.IdSaida.Length > 0 && f.Saida.Length > 0), "Leitura nativa dos picos associa cada fonte detectada a uma saída");
        Console.WriteLine($"WASAPI real: {medicao.Fontes.Count} fonte(s) com pico nesta amostra. Medição parcial: {medicao.Parcial}.");
    }

    private static void TestarPreviaModular(MainWindow janela)
    {
        State(janela, "Previa");
        Pump(320);
        var atalhos = (ItemsControl)janela.FindName("Atalhos");
        var host = (ContentControl)janela.FindName("HostModulo");
        var capsula = (Border)janela.FindName("Capsula");
        var modulos = atalhos.Items.Cast<ModuloPrevia>().ToArray();
        Check(modulos.Select(m => m.Id).SequenceEqual(["windows", "audio", "wifi", "notificacoes", "configuracoes"]) &&
            host.Content is null && capsula.ActualHeight < 45, "Prévia inicial compacta com cinco módulos em ordem");
        Renderizar(janela, "pufo-previa-modular");
        double largura = capsula.ActualWidth;
        atalhos.ItemsSource = modulos.Take(3).Reverse().ToArray();
        Pump(320);
        Check(capsula.ActualWidth < largura - 45 && ((ModuloPrevia)atalhos.Items[0]).Id == "wifi", "Reordenar e remover descritores redimensiona a mesma cápsula");

        var prometida = new TaskCompletionSource<ConsultaPrevia>();
        var wifi = new PainelConsulta("Wi-Fi", "Redes no Windows", "ms-settings:network-wifi", _ => prometida.Task);
        // A enumeração visível usa uma resposta controlada para não acionar consentimento de localização.
        atalhos.ItemsSource = modulos.Select(m => m.Id == "wifi" ? m with { Painel = wifi } : m).ToArray();
        void Clicar(string id)
        {
            atalhos.UpdateLayout();
            var item = atalhos.Items.Cast<ModuloPrevia>().Single(m => m.Id == id);
            var container = (ContentPresenter)atalhos.ItemContainerGenerator.ContainerFromItem(item);
            var botao = (Button)VisualTreeHelper.GetChild(container, 0);
            botao.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }

        Clicar("wifi");
        Clicar("notificacoes");
        var notif = host.Content as PainelConsulta ?? throw new InvalidOperationException("Painel de notificações ausente");
        prometida.SetResult(WifiWindows.Organizar([new("Rede B", 90, false), new("Rede A", 45, true), new("Rede A", 85, false)], ""));
        Pump(320);
        Check(!wifi.IsVisible && notif.IsVisible && ((TextBlock)notif.FindName("Mensagem")).Text.Contains("sem pacote") &&
            !((Button)notif.FindName("Autorizar")).IsVisible, "Painel de notificações da edição sem pacote informa o limite sem solicitar acesso");
        Check(((ItemsControl)wifi.FindName("Itens")).Items.Count == 0 && ReferenceEquals(host.Content, notif),
            "Resposta Wi-Fi atrasada não substitui o módulo atual");
        Clicar("wifi");
        Pump(320);
        var itensWifi = ((ItemsControl)wifi.FindName("Itens")).Items.Cast<ItemConsulta>().ToArray();
        Check(itensWifi.Length == 2 && itensWifi[0] == new ItemConsulta("Rede A", "Conectada · 45% de sinal"), "Rede conectada precede redes mais fortes e perfis duplicados");
        Check(WifiWindows.MensagemErro(5).Contains("localização") && WifiWindows.MensagemErro(0x80342002).Contains("desligado"),
            "Wi-Fi diferencia permissão e rádio desligado");
        ApplyStatus(janela, new AudioStatus("Alto-falantes de teste", 51, false, true, "teste:A"));
        ApplyStatus(janela, new AudioStatus("Alto-falantes de teste", 52, false, true, "teste:A"));
        Check(ReferenceEquals(host.Content, wifi) && (EstadoIlha)Campo(janela, "_estadoIlha")! == EstadoIlha.Previa,
            "Alterações externas de volume preservam a consulta Wi-Fi");
        Renderizar(janela, "pufo-previa-wifi");
        Clicar("audio");
        typeof(MainWindow).GetMethod("PararMedicao", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(janela, null);
        FonteSom[] fontes = [new("a", new(false, 123, "grupo"), "Player de teste", "Alto-falantes de teste", null),
            new("b", new(false, 456, "grupo"), "Navegador de teste", "Fones de teste", null)];
        janela.AtualizarFontesAtivas(new(fontes, false));
        Pump(320);
        var fontesUi = (ItemsControl)janela.FindName("FontesAtivas");
        var audio = host.Content;
        var itensAnteriores = fontesUi.ItemsSource;
        int revisao = (int)Campo(janela, "_transicao")!;
        janela.AtualizarFontesAtivas(new(fontes.ToArray(), false));
        Check(fontesUi.Items.Count == 2 && ReferenceEquals(itensAnteriores, fontesUi.ItemsSource) && revisao == (int)Campo(janela, "_transicao")!,
            "Duas fontes em saídas diferentes mantêm suas linhas entre amostras equivalentes");
        Renderizar(janela, "pufo-previa-audio");
        var slider = (SliderSincronizado)janela.FindName("SliderPrevia");
        Arrastar(slider, true);
        Clicar("wifi");
        Check(slider.EmArrasto && ReferenceEquals(audio, host.Content), "Troca contextual preserva slider em arrasto");
        Arrastar(slider, false);
        Clicar("configuracoes");
        var janelaConfig = (SettingsWindow)Campo(janela, "_janelaConfiguracoes")!;
        Check(janelaConfig.IsVisible && ReferenceEquals(janelaConfig, janela.AbrirConfiguracoes()) &&
            (EstadoIlha)Campo(janela, "_estadoIlha")! == EstadoIlha.Previa, "Engrenagem modular usa a janela existente e mantém o estado Prévia");
        janelaConfig.Close();
        janela.Activate();
        var f6 = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(janela)!, 0, Key.F6)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        janela.RaiseEvent(f6);
        var containerAudio = (ContentPresenter)atalhos.ItemContainerGenerator.ContainerFromIndex(1);
        ((Button)VisualTreeHelper.GetChild(containerAudio, 0)).Focus();
        Check(ReferenceEquals(host.Content, audio) && atalhos.IsKeyboardFocusWithin, "F6 e foco de teclado ativam um módulo");
        void ClicarControle(string nome) => ((Button)janela.FindName(nome)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        ClicarControle("AbrirMixerPrevia");
        Check((EstadoIlha)Campo(janela, "_estadoIlha")! == EstadoIlha.Completo && (bool)Campo(janela, "_mixerAberto")!,
            "Atalho Mixer abre os controles individuais existentes");
        State(janela, "Previa");
        Clicar("audio");
        ClicarControle("AbrirSaidasPrevia");
        Check((bool)Campo(janela, "_saidasAbertas")!, "Atalho Saídas abre o seletor existente");
        State(janela, "Previa");
        janela.ReceberSessoes([new(8, "Player A"), new(9, "Player B")], 9);
        Clicar("audio");
        ClicarControle("Reproducoes");
        var midia = (CartaoMidia)janela.FindName("MidiaCompleta");
        Check(((ScrollViewer)midia.FindName("Lista")).IsVisible && ((ItemsControl)midia.FindName("Opcoes")).Items.Count == 3,
            "Atalho Reproduções oferece as duas sessões e o modo automático");
        janela.ReceberSessoes([], null);
        atalhos.ItemsSource = modulos;
        State(janela, "Repouso");
        Pump(320);
        Check(host.Content is null, "Recolhimento libera o conteúdo contextual");
    }
}
