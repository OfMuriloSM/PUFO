using System.Net.Http;
using System.Windows;
using PUFO.App.Services;
using PUFO.App.Services.Voz;
using Windows.Media.SpeechSynthesis;

namespace PUFO.App;

public partial class SettingsWindow
{
    private static readonly HttpClient HttpClima = new();
    private ReconhecimentoLocal Reconhecimento { get; }
    internal SessaoVoz? SessaoVoz { get; set; }

    private void CarregarOpcoesVoz()
    {
        try
        {
            EntradaTeste.ItemsSource = new[] { new EntradaVoz("", "Microfone padrão do Windows") }.Concat(MicrofoneWindows.Listar()).ToArray();
            EntradaTeste.SelectedIndex = 0;
            VozTeste.ItemsSource = FalaWindows.Vozes();
            VozTeste.SelectedIndex = 0;
        }
        catch (Exception) { MostrarAviso("Não foi possível listar todos os dispositivos de voz."); }
        TestarFala.IsEnabled = VozTeste.SelectedItem is not null;
        ModeloLocal.Text = ReconhecimentoLocal.TemModelo ? "Modelo português disponível." :
            $"Extraia vosk-model-small-pt-0.3 do catálogo oficial Vosk em {ReconhecimentoLocal.PastaModelo}.";
        if (!TestarFala.IsEnabled) ModeloLocal.Text += " Nenhuma voz pt-BR disponível para resposta falada.";
    }

    internal void MostrarAtividadeVoz(FaseVoz fase)
    {
        EstadoMicrofone.Text = fase == FaseVoz.Ouvindo ? "Microfone em captura" : "Microfone inativo";
    }

    private async void AoTestarEntrada(object sender, RoutedEventArgs e)
    {
        if (SessaoVoz is null) return;
        string id = (EntradaTeste.SelectedItem as EntradaVoz)?.Id ?? "";
        bool windows = MotorTeste.SelectedIndex == 1;
        await SessaoVoz.ExecutarAsync((atividade, token) => Reconhecimento.OuvirAsync(id, windows, TimeSpan.FromSeconds(7), atividade, token), origem: this);
    }

    private async void AoTestarFala(object sender, RoutedEventArgs e)
    {
        if (SessaoVoz is null || VozTeste.SelectedItem is not VoiceInformation voz) return;
        string texto = TextoParaFalar.Text.Trim();
        if (texto.Length == 0) return;
        await SessaoVoz.ExecutarAsync((_, _) => Task.FromResult(texto), async (resposta, token) =>
        {
            if (!await FalaWindows.DizerAsync(resposta, voz.Id, 0.7, token))
                MostrarAviso("A voz selecionada está indisponível.");
        }, origem: this);
    }

    private async Task ConsultarClimaAsync(bool chuva)
    {
        if (SessaoVoz is null) return;
        string cidade = CidadeConsulta.Text;
        await SessaoVoz.ExecutarAsync((_, token) => new ClimaOpenMeteo(HttpClima).ConsultarAsync(cidade, chuva, token), origem: this);
    }

    private async void AoConsultarTemperatura(object sender, RoutedEventArgs e) => await ConsultarClimaAsync(false);
    private async void AoConsultarChuva(object sender, RoutedEventArgs e) => await ConsultarClimaAsync(true);
}
