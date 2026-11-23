using System.IO;
using System.Runtime.InteropServices;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using System.Text.Json;
using Vosk;

namespace PUFO.App.Services.Voz;

internal static class ReconhecimentoLocal
{
    internal static string PastaModelo => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PUFO", "modelos", "vosk-model-small-pt-0.3");
    internal static bool TemModelo => File.Exists(Path.Combine(PastaModelo, "final.mdl"));
    internal static RecognizerInfo? Nativo => SpeechRecognitionEngine.InstalledRecognizers()
        .FirstOrDefault(r => r.Culture.Name.Equals("pt-BR", StringComparison.OrdinalIgnoreCase));

    internal static async Task<string> OuvirAsync(string entrada, bool usarWindows, TimeSpan limite, Action<bool> atividade, CancellationToken token)
    {
        if (usarWindows && Nativo is null) throw new InvalidOperationException("Reconhecedor Windows pt-BR indisponível. Selecione Vosk.");
        if (!usarWindows && (!TemModelo || RuntimeInformation.ProcessArchitecture != Architecture.X64))
            throw new InvalidOperationException("O Vosk requer Windows x64 e o modelo português. Consulte Assistente nas Configurações.");
        byte[] pcm;
        try { pcm = await MicrofoneWindows.CapturarAsync(entrada, limite, atividade, token); }
        catch (Exception e) when (e is COMException or UnauthorizedAccessException or IOException or ArgumentException)
        { throw new InvalidOperationException("Microfone indisponível. Confira a entrada e a permissão de microfone do Windows.", e); }
        try
        {
            return usarWindows ? await DecodificarWindowsAsync(pcm, token) :
                await Task.Run(() => DecodificarVosk(pcm, token), token);
        }
        finally { Array.Clear(pcm); }
    }

    internal static string DecodificarVosk(byte[] pcm, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Vosk.Vosk.SetLogLevel(-1);
        using var modelo = new Model(PastaModelo);
        using var reconhecedor = new VoskRecognizer(modelo, MicrofoneWindows.Frequencia);
        var partes = new List<string>();
        var bloco = new byte[8000];
        for (int inicio = 0; inicio < pcm.Length; inicio += bloco.Length)
        {
            token.ThrowIfCancellationRequested();
            int tamanho = Math.Min(bloco.Length, pcm.Length - inicio);
            Array.Copy(pcm, inicio, bloco, 0, tamanho);
            if (reconhecedor.AcceptWaveform(bloco, tamanho)) Adicionar(reconhecedor.Result());
        }
        Adicionar(reconhecedor.FinalResult());
        return string.Join(" ", partes);

        void Adicionar(string json)
        {
            using var resultado = JsonDocument.Parse(json);
            if (resultado.RootElement.TryGetProperty("text", out var texto) && !string.IsNullOrWhiteSpace(texto.GetString()))
                partes.Add(texto.GetString()!);
        }
    }

    private static async Task<string> DecodificarWindowsAsync(byte[] pcm, CancellationToken token)
    {
        using var dados = new MemoryStream(pcm, false);
        using var motor = new SpeechRecognitionEngine(Nativo!);
        motor.LoadGrammar(new DictationGrammar());
        motor.SetInputToAudioStream(dados, new SpeechAudioFormatInfo(16000, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
        var fim = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        motor.RecognizeCompleted += (_, e) =>
        {
            if (e.Error is not null) fim.TrySetException(e.Error);
            else if (e.Cancelled) fim.TrySetCanceled();
            else fim.TrySetResult(e.Result is { Confidence: >= 0.6f } resultado ? resultado.Text : "");
        };
        token.ThrowIfCancellationRequested();
        motor.RecognizeAsync(RecognizeMode.Single);
        using var registro = token.Register(motor.RecognizeAsyncCancel);
        return await fim.Task;
    }
}
