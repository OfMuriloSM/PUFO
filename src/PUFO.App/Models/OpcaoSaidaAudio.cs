using System.ComponentModel;
using PUFO.App.Services.Audio;

namespace PUFO.App.Models;

internal sealed class OpcaoSaidaAudio(SaidaAudio saida) : INotifyPropertyChanged
{
    public string Id => saida.Id;
    public string Nome => saida.Nome;
    public string NomeAcessivel => Padrao ? $"{Nome}, saída padrão" : Nome;
    public bool Padrao { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void AtualizarPadrao(string? id)
    {
        bool padrao = Id == id;
        if (Padrao == padrao) return;
        Padrao = padrao;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Padrao)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NomeAcessivel)));
    }
}

