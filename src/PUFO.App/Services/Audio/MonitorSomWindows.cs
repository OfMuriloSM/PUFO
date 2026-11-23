using System.Windows.Media;
using NAudio.CoreAudioApi;

namespace PUFO.App.Services.Audio;

internal sealed record FonteSom(string IdSaida, ChaveSessao Chave, string Nome, string Saida, ImageSource? Icone);
internal sealed record MedicaoSom(IReadOnlyList<FonteSom> Fontes, bool Parcial);

// Uma instância por visita ao painel. Leituras e descarte são sequenciais, fora do Dispatcher.
internal sealed class MonitorSomWindows : IDisposable
{
    private readonly Dictionary<string, MixerWindows> _medidores = [];
    private IReadOnlyList<SaidaAudio> _saidas = [];
    private long _revisarSaidas;

    internal MedicaoSom Ler()
    {
        bool parcial = false;
        long agora = Environment.TickCount64;
        if (agora >= _revisarSaidas)
        {
            var saidas = SeletorSaidaWindows.Listar();
            parcial = saidas is null;
            _saidas = saidas ?? [];
            _revisarSaidas = parcial ? agora : agora + 3000;
            var ids = _saidas.Select(s => s.Id).ToHashSet();
            foreach (var id in _medidores.Keys.Where(id => !ids.Contains(id)).ToArray())
            {
                _medidores.Remove(id, out var antigo);
                antigo!.Dispose();
            }
        }

        var fontes = new List<FonteSom>();
        try
        {
            using var enumerador = new MMDeviceEnumerator();
            foreach (var saida in _saidas)
            {
                try
                {
                    using var dispositivo = enumerador.GetDevice(saida.Id);
                    if (!_medidores.TryGetValue(saida.Id, out var medidor))
                        _medidores.Add(saida.Id, medidor = new MixerWindows());
                    // O pico de sessão pode preceder o mute e o ganho da saída.
                    var mestre = dispositivo.AudioEndpointVolume;
                    if (mestre.Mute || mestre.MasterVolumeLevelScalar <= 0)
                    {
                        medidor.Suspender();
                        continue;
                    }
                    var grupos = medidor.LerNoDispositivo(dispositivo);
                    parcial |= medidor.LeituraParcial || grupos.Any(g => g.Atividade == AtividadeAudio.SemMedidor);
                    fontes.AddRange(grupos.Where(g => g.Atividade == AtividadeAudio.Detectado)
                        .Select(g => new FonteSom(saida.Id, g.Chave, g.Nome, saida.Nome, g.Icone)));
                }
                catch (Exception e) when (ServicoAudioWindows.FalhaCoreAudio(e)) { parcial = true; }
            }
        }
        catch (Exception e) when (ServicoAudioWindows.FalhaCoreAudio(e)) { parcial = true; }
        return new(fontes, parcial);
    }

    public void Dispose()
    {
        foreach (var medidor in _medidores.Values) medidor.Dispose();
        _medidores.Clear();
    }
}
