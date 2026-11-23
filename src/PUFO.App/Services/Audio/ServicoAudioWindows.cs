using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace PUFO.App.Services.Audio;

public sealed class ServicoAudioWindows
{
    private string? _idNome;
    private string? _nome;

    public AudioStatus ObterStatus()
    {
        return ComSaida(dispositivo =>
        {
            var id = dispositivo.ID;
            if (_idNome != id)
            {
                _nome = dispositivo.FriendlyName;
                _idNome = id;
            }

            var volume = dispositivo.AudioEndpointVolume;
            return new AudioStatus(_nome!, Percentual(volume.MasterVolumeLevelScalar), volume.Mute, true, id);
        }, AudioStatus.Indisponivel);
    }

    public void DefinirVolumePercentual(int percentual, string? idEsperado = null) =>
        Alterar(idEsperado, volume => volume.MasterVolumeLevelScalar = Escalar(percentual));

    public void AjustarVolume(int delta, string? idEsperado = null) =>
        Alterar(idEsperado, volume => volume.MasterVolumeLevelScalar = Escalar(Percentual(volume.MasterVolumeLevelScalar) + delta));

    public void AlternarMudo(string? idEsperado = null) => Alterar(idEsperado, volume => volume.Mute = !volume.Mute);

    private static void Alterar(string? idEsperado, Action<AudioEndpointVolume> alterar)
    {
        ComSaida(dispositivo =>
        {
            if (idEsperado is not null && dispositivo.ID != idEsperado) return false;
            alterar(dispositivo.AudioEndpointVolume);
            return true;
        }, false);
    }

    internal static T ComSaida<T>(Func<MMDevice, T> executar, T fallback)
    {
        try
        {
            using var enumerador = new MMDeviceEnumerator();
            if (!enumerador.TryGetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out var dispositivo) || dispositivo is null)
                return fallback;
            using (dispositivo) return executar(dispositivo);
        }
        catch (Exception e) when (FalhaCoreAudio(e)) { return fallback; }
    }

    internal static bool FalhaCoreAudio(Exception e) => e is ExternalException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException;
    internal static int Percentual(float volume) => Math.Clamp((int)Math.Round(volume * 100), 0, 100);
    internal static float Escalar(int percentual) => Math.Clamp(percentual, 0, 100) / 100f;
}

public sealed record AudioStatus(string NomeDispositivo, int VolumePercentual, bool EstaMudo,
    bool Disponivel = true, string? IdDispositivo = null)
{
    internal static AudioStatus Indisponivel { get; } = new("Nenhuma saída de áudio", 0, true, false);
}
