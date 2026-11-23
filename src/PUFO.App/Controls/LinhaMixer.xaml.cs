using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using PUFO.App.Services.Audio;

namespace PUFO.App.Controls;

public partial class LinhaMixer : System.Windows.Controls.UserControl
{
    private GrupoAudio? _grupo;
    internal event Action<ChaveSessao, int>? AlterouVolume;
    internal event Action<ChaveSessao, bool>? AlterouMudo;

    public LinhaMixer() => InitializeComponent();

    internal void Aplicar(GrupoAudio grupo)
    {
        if (_grupo?.Nome != grupo.Nome)
        {
            Nome.Text = grupo.Nome;
            Nome.ToolTip = grupo.Nome;
            AutomationProperties.SetName(Volume, $"Volume de {grupo.Nome}");
        }
        if (_grupo is null || _grupo.Icone != grupo.Icone)
        {
            Imagem.Source = grupo.Icone;
            Fallback.Visibility = grupo.Icone is null ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_grupo is null || _grupo.Mudo != grupo.Mudo || _grupo.MudoMisto != grupo.MudoMisto || _grupo.Nome != grupo.Nome)
        {
            IconeMudo.SetResourceReference(System.Windows.Shapes.Path.DataProperty, grupo.Mudo ? "Muted" : "Speaker");
            string acao = grupo.Mudo ? "Ativar áudio" : "Silenciar";
            Mute.ToolTip = grupo.MudoMisto ? "Silenciar todas as sessões deste grupo" : acao;
            AutomationProperties.SetName(Mute, $"{acao}: {grupo.Nome}");
        }
        if (_grupo?.Atividade != grupo.Atividade)
            Atividade.Text = grupo.Atividade switch
            {
                AtividadeAudio.Detectado => "Com som",
                AtividadeAudio.Silenciado => "Mudo",
                AtividadeAudio.SemMedidor => "Sem medidor",
                _ => "Disponível"
            };
        _grupo = grupo;
        if (Volume.Sincronizar(grupo.Volume)) MostrarPercentual(grupo.Volume, grupo.VolumeMisto);
    }

    private void MostrarPercentual(int percentual, bool misto)
    {
        string texto = misto ? $"~{percentual}%" : $"{percentual}%";
        if (Percentual.Text != texto) Percentual.Text = texto;
        string? dica = misto ? "Média das sessões com volumes diferentes" : null;
        if (!Equals(Percentual.ToolTip, dica)) Percentual.ToolTip = dica;
    }

    private void AoAlterarVolume(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Volume is null || Volume.Atualizando || _grupo is null) return;
        int percentual = (int)Math.Round(e.NewValue);
        MostrarPercentual(percentual, false);
        AlterouVolume?.Invoke(_grupo.Chave, percentual);
    }

    private void AoAlternarMudo(object sender, RoutedEventArgs e)
    {
        if (_grupo is not null) AlterouMudo?.Invoke(_grupo.Chave, !_grupo.Mudo);
    }
}
