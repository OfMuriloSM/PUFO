using System.Windows.Media;
using Windows.Media.Control;

namespace PUFO.App.Models;

internal enum ComandoMidia { Reproduzir, Pausar, Alternar, Anterior, Proxima, Posicao }

internal sealed record SessaoMidia(int Id, string Origem)
{
    public string Titulo { get; init; } = "";
    public string Artista { get; init; } = "";
    public ImageSource? Capa { get; init; }
    public string Rotulo => string.IsNullOrWhiteSpace(Titulo) ? Origem : Titulo;
    public string Detalhe => string.IsNullOrWhiteSpace(Artista) ? Origem : Artista;
    public GlobalSystemMediaTransportControlsSessionPlaybackStatus Estado { get; init; }
    public bool Tocando => Estado == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
    public bool Play { get; init; }
    public bool Pause { get; init; }
    public bool Toggle { get; init; }
    public bool Anterior { get; init; }
    public bool Proxima { get; init; }
    public bool Seek { get; init; }
    public TimeSpan Inicio { get; init; }
    public TimeSpan Fim { get; init; }
    public TimeSpan MinSeek { get; init; }
    public TimeSpan MaxSeek { get; init; }
    public TimeSpan Posicao { get; init; }
    public double Taxa { get; init; } = 1;
    public DateTimeOffset Atualizado { get; init; }
    public bool TemTempo => Fim > Inicio;
    public bool PodeBuscar => Seek && TemTempo && MaxSeek > MinSeek;
    public ComandoMidia AcaoPrincipal => Tocando && Pause ? ComandoMidia.Pausar :
        !Tocando && Play ? ComandoMidia.Reproduzir : ComandoMidia.Alternar;
    public bool Permite(ComandoMidia comando) => comando switch
    {
        ComandoMidia.Reproduzir => Play, ComandoMidia.Pausar => Pause,
        ComandoMidia.Alternar => Toggle, ComandoMidia.Anterior => Anterior,
        ComandoMidia.Proxima => Proxima, ComandoMidia.Posicao => PodeBuscar, _ => false
    };
    public double SegundosAtuais(DateTimeOffset agora)
    {
        double decorrido = Tocando && Atualizado > DateTimeOffset.UnixEpoch && double.IsFinite(Taxa)
            ? Math.Max(0, (agora - Atualizado).TotalSeconds) * Taxa : 0;
        return Math.Clamp((Posicao - Inicio).TotalSeconds + decorrido, 0, Math.Max(0, (Fim - Inicio).TotalSeconds));
    }
}

internal sealed class SelecaoMidia
{
    private int? _manual;
    public SessaoMidia? Atual { get; private set; }
    public void Escolher(int? id) => _manual = id;
    public void Atualizar(IReadOnlyList<SessaoMidia> sessoes, int? ativa)
    {
        var escolhida = sessoes.FirstOrDefault(s => s.Id == _manual);
        if (escolhida is null) _manual = null;
        Atual = escolhida ?? sessoes.FirstOrDefault(s => s.Id == ativa)
            ?? sessoes.FirstOrDefault(s => s.Id == Atual?.Id)
            ?? sessoes.FirstOrDefault(s => s.Tocando) ?? sessoes.FirstOrDefault();
    }
}
