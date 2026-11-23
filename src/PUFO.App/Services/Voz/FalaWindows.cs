using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace PUFO.App.Services.Voz;

internal static class FalaWindows
{
    internal static IReadOnlyList<VoiceInformation> Vozes() => SpeechSynthesizer.AllVoices
        .Where(v => v.Language.Equals("pt-BR", StringComparison.OrdinalIgnoreCase)).ToArray();

    internal static async Task<bool> DizerAsync(string texto, string id, double volume, CancellationToken token)
    {
        var vozes = Vozes();
        var voz = id.Length == 0 ? vozes.FirstOrDefault() : vozes.FirstOrDefault(v => v.Id == id);
        if (voz is null) return false;
        using var sintetizador = new SpeechSynthesizer { Voice = voz };
        using var fala = await sintetizador.SynthesizeTextToStreamAsync(texto).AsTask(token);
        using var fonte = MediaSource.CreateFromStream(fala, fala.ContentType);
        using var player = new MediaPlayer { Volume = Math.Clamp(volume, 0, 1), CommandManager = { IsEnabled = false } };
        var fim = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        player.MediaEnded += (_, _) => fim.TrySetResult();
        player.MediaFailed += (_, _) => fim.TrySetException(new InvalidOperationException("Saída de voz indisponível."));
        player.Source = fonte;
        token.ThrowIfCancellationRequested();
        try
        {
            player.Play();
            await fim.Task.WaitAsync(TimeSpan.FromSeconds(25), token);
            return true;
        }
        finally { player.Pause(); player.Source = null; }
    }
}
