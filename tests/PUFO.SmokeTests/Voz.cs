using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using PUFO.App;
using PUFO.App.Services;
using PUFO.App.Services.Voz;
using Windows.Media.SpeechSynthesis;

internal static partial class Program
{
    private static void TestarVoz(MainWindow janela)
    {
        using var contexto = new ContextoVoz(janela);
        var sessao = (SessaoVoz)Campo(janela, "_sessaoVoz")!;
        var painel = (DockPanel)janela.FindName("PainelVoz");
        var reconhecer = janela.ReconhecerMouse;
        int capturas = 0;
        janela.ReconhecerMouse = async (atividade, token) =>
        {
            capturas++;
            atividade(true);
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { atividade(false); }
            return "";
        };
        bool Mouse3(UIElement origem)
        {
            var evento = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Middle)
                { RoutedEvent = Mouse.PreviewMouseDownEvent };
            origem.RaiseEvent(evento);
            return evento.Handled;
        }
        foreach (string estado in new[] { "Repouso", "Previa", "Completo" })
        {
            State(janela, estado);
            Pump(300);
            var origem = (UIElement)janela.FindName(estado == "Completo" ? "BtnMixer" : estado == "Previa" ? "Atalhos" : "ViewRepouso");
            Check(Mouse3(origem), $"Mouse3 tratado em {estado}");
            Pump(100);
            Check(painel.IsVisible && sessao.Fase == FaseVoz.Ouvindo, $"Captura simulada indicada em {estado}");
            if (estado == "Repouso") { Pump(240); Renderizar(janela, "pufo-voz"); }
            Mouse3(painel);
            Pump(80);
            Check(!sessao.Ocupada && !painel.IsVisible && Campo(janela, "_estadoIlha")!.ToString() == estado,
                $"Segundo Mouse3 cancela e restaura {estado}");
        }
        Check(capturas == 3, "Um acionamento abre uma única captura");
        var tardio = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fases = new List<FaseVoz>();
        void Registrar(FaseVoz fase, string mensagem) => fases.Add(fase);
        sessao.Mudou += Registrar;
        janela.ReconhecerMouse = (atividade, _) =>
        {
            atividade(false);
            return tardio.Task;
        };
        Mouse3((UIElement)janela.FindName("BtnMixer"));
        Check(sessao.Fase == FaseVoz.Processando, "Resultado pendente mantém a ilha em processamento");
        Mouse3(painel);
        tardio.SetResult("Resultado atrasado");
        Pump(100);
        sessao.Mudou -= Registrar;
        Check(!sessao.Ocupada && !fases.Contains(FaseVoz.Respondendo),
            "Resultado entregue após cancelar por Mouse3 não publica resposta");
        janela.ReconhecerMouse = (_, _) => Task.FromException<string>(new TimeoutException());
        Mouse3((UIElement)janela.FindName("BtnMixer"));
        Check(sessao.Fase == FaseVoz.Erro, "Timeout produz feedback visual");
        sessao.Cancelar(); Pump(60);
        janela.ReconhecerMouse = (_, _) => Task.FromException<string>(new InvalidOperationException("Microfone indisponível."));
        Mouse3((UIElement)janela.FindName("BtnMixer"));
        Check(sessao.Fase == FaseVoz.Erro && ((TextBlock)janela.FindName("MensagemVoz")).Text.Contains("Microfone"), "Falha de microfone mantém a ilha utilizável");
        sessao.Cancelar(); Pump(60);
        bool interrompida = false;
        var resposta = sessao.ExecutarAsync((_, _) => Task.FromResult("Resposta de teste"), async (_, token) =>
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { interrompida = true; }
        });
        Check(sessao.Fase == FaseVoz.Respondendo, "Resposta simulada ocupa a mesma cápsula");
        Mouse3(painel); Pump(60);
        Check(resposta.IsCompletedSuccessfully && interrompida && !sessao.Ocupada, "Mouse3 interrompe a síntese antes de aceitar nova captura");
        var campo = typeof(MainWindow).GetField("_ocultoPorTelaCheia", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        campo.SetValue(janela, true);
        Check(!Mouse3((UIElement)janela.FindName("BtnMixer")) && !sessao.Ocupada, "Fullscreen bloqueia Mouse3");
        campo.SetValue(janela, false);
        janela.ReconhecerMouse = reconhecer;

        Task.Run(TestarClima).GetAwaiter().GetResult();
        Task.Run(ValidarDecodificadorLocal).GetAwaiter().GetResult();
    }

    // Eventos físicos WPF já são entregues dentro deste contexto.
    private sealed class ContextoVoz : IDisposable
    {
        private readonly SynchronizationContext? _anterior = SynchronizationContext.Current;
        internal ContextoVoz(Window janela) => SynchronizationContext.SetSynchronizationContext(
            new System.Windows.Threading.DispatcherSynchronizationContext(janela.Dispatcher));
        public void Dispose() => SynchronizationContext.SetSynchronizationContext(_anterior);
    }

    private sealed class RespostasHttp(params string[] respostas) : HttpMessageHandler
    {
        internal int Consultas;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Consultas >= respostas.Length) throw new HttpRequestException();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respostas[Consultas++]) });
        }
    }

    private static async Task TestarClima()
    {
        const string cidades = """{"results":[{"name":"Teste","admin1":"Norte","country_code":"BR","latitude":1,"longitude":2},{"name":"Teste","admin1":"Sul","country_code":"BR","latitude":3,"longitude":4}]}""";
        var handler = new RespostasHttp(cidades, cidades, """{"daily":{"precipitation_probability_max":[75]}}""", "{}", cidades, """{"current":{"temperature_2m":null}}""");
        using var http = new HttpClient(handler);
        var clima = new ClimaOpenMeteo(http);
        Check((await clima.ConsultarAsync("", false, default)).Contains("Informe") && handler.Consultas == 0, "Clima sem cidade não faz requisição");
        Check((await clima.ConsultarAsync("Teste", false, default)).Contains("mais de uma"), "Clima detecta cidade ambígua");
        Check((await clima.ConsultarAsync("Teste, Sul, BR", true, default)).Contains("75%"), "Chuva usa a probabilidade fornecida para a localidade escolhida");
        Check((await clima.ConsultarAsync("Teste", false, default)).Contains("não encontrada"), "Busca vazia informa cidade ausente");
        Check(!(await clima.ConsultarAsync("Teste, Sul", false, default)).Contains("°C"), "Previsão vazia não inventa temperatura");
        Check((await clima.ConsultarAsync("Teste", false, default)).Contains("Não consegui"), "Falha HTTP simulada mantém resposta curta");
    }

    private static async Task ValidarDecodificadorLocal()
    {
        var voz = FalaWindows.Vozes().FirstOrDefault();
        if (!ReconhecimentoLocal.TemModelo || voz is null || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64)
        {
            Console.WriteLine("Teste opcional de transcrição local indisponível sem modelo x64 e voz pt-BR.");
            return;
        }
        using var sintetizador = new SpeechSynthesizer { Voice = voz };
        using var fala = await sintetizador.SynthesizeTextToStreamAsync("Que horas são?");
        using var stream = fala.AsStreamForRead();
        using var leitor = new WaveFileReader(stream);
        var amostras = leitor.ToSampleProvider();
        if (amostras.WaveFormat.Channels == 2) amostras = new StereoToMonoSampleProvider(amostras);
        var pcm = new SampleToWaveProvider16(new WdlResamplingSampleProvider(amostras, 16000));
        using var buffer = new MemoryStream();
        var bloco = new byte[8192];
        int lidos;
        while ((lidos = pcm.Read(bloco)) > 0) buffer.Write(bloco, 0, lidos);
        string texto = ReconhecimentoLocal.DecodificarVosk(buffer.ToArray(), default);
        Console.WriteLine($"Frase sintética reconhecida em memória: {texto}");
        Check(texto.Length > 0, "Voz brasileira e modelo Vosk reais processam amostra sintética sem reprodução nem microfone");
    }
}
