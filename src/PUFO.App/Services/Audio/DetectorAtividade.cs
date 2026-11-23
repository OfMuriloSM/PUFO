namespace PUFO.App.Services.Audio;

internal enum AtividadeAudio { Disponivel, Detectado, SemMedidor, Silenciado }

internal sealed class DetectorAtividade
{
    private readonly Dictionary<string, long> _ultimoPico = new(StringComparer.Ordinal);
    private const float LimiarEntrada = 0.003f;
    private const float LimiarManutencao = 0.0015f;
    private const long RetencaoMs = 1800;

    internal AtividadeAudio Observar(string id, bool ativa, bool mudo, float? pico, long agora)
    {
        if (mudo || !ativa || pico is null || !float.IsFinite(pico.Value))
        {
            _ultimoPico.Remove(id);
            if (mudo) return AtividadeAudio.Silenciado;
            return ativa ? AtividadeAudio.SemMedidor : AtividadeAudio.Disponivel;
        }
        bool recente = _ultimoPico.TryGetValue(id, out var ultimo) && agora - ultimo < RetencaoMs;
        if (pico >= (recente ? LimiarManutencao : LimiarEntrada))
        {
            _ultimoPico[id] = agora;
            return AtividadeAudio.Detectado;
        }
        return recente ? AtividadeAudio.Detectado : AtividadeAudio.Disponivel;
    }

    internal void Remover(string id) => _ultimoPico.Remove(id);
    internal void Limpar() => _ultimoPico.Clear();
}
