namespace PUFO.App.Models;

internal enum TemaPufo { Preto }

internal sealed record ConfiguracoesPufo
{
    internal const int AtrasoMinimo = 100;
    internal const int AtrasoMaximo = 2000;
    internal const int VelocidadeMinima = 50;
    internal const int VelocidadeMaxima = 200;

    public int HoverMs { get; init; } = 500;
    public int FechamentoMs { get; init; } = 500;
    public bool Animacoes { get; init; } = true;
    public int VelocidadeAnimacoes { get; init; } = 100;
    public bool PreviaContextual { get; init; } = true;
    public bool MixerExpandido { get; init; }
    public bool MidiaAtiva { get; init; } = true;
    public bool MidiaNaPrevia { get; init; } = true;
    public bool PrioridadeMidia { get; init; }
    public TemaPufo Tema { get; init; } = TemaPufo.Preto;

    internal ConfiguracoesPufo Normalizar() => this with
    {
        HoverMs = Math.Clamp(HoverMs, AtrasoMinimo, AtrasoMaximo),
        FechamentoMs = Math.Clamp(FechamentoMs, AtrasoMinimo, AtrasoMaximo),
        VelocidadeAnimacoes = Math.Clamp(VelocidadeAnimacoes, VelocidadeMinima, VelocidadeMaxima),
        Tema = Enum.IsDefined(Tema) ? Tema : TemaPufo.Preto
    };
}
