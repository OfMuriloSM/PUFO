using System.Windows;
using PUFO.App.Services;

namespace PUFO.App.Controls;

public partial class PainelConsulta : System.Windows.Controls.UserControl
{
    private readonly Func<bool, Task<ConsultaPrevia>> _consultar;
    private readonly string _uri;
    private int _revisao;
    private Task<ConsultaPrevia>? _pendente;

    internal PainelConsulta(string nome, string textoAjustes, string uri, Func<bool, Task<ConsultaPrevia>> consultar)
    {
        InitializeComponent();
        Titulo.Text = nome;
        Ajustes.Content = textoAjustes;
        _uri = uri;
        _consultar = consultar;
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) _ = AtualizarAsync(false);
            else
            {
                _revisao++;
                Itens.ItemsSource = null;
                Autorizar.Visibility = Visibility.Collapsed;
            }
        };
    }

    private async Task AtualizarAsync(bool autorizar)
    {
        int revisao = ++_revisao;
        Atualizar.IsEnabled = Autorizar.IsEnabled = false;
        if (Itens.Items.Count == 0) MostrarMensagem("Consultando…");
        ConsultaPrevia dados;
        var consulta = _pendente;
        try
        {
            if (consulta is null || consulta.IsCompleted) _pendente = consulta = _consultar(autorizar);
            dados = await consulta;
        }
        catch (Exception) { dados = new("Não foi possível consultar o Windows.", []); }
        finally { if (ReferenceEquals(_pendente, consulta)) _pendente = null; }
        if (revisao != _revisao || !IsVisible) return;
        MostrarMensagem(dados.Mensagem);
        Itens.ItemsSource = dados.Itens;
        Rolagem.Visibility = dados.Itens.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Autorizar.Visibility = dados.PodeAutorizar ? Visibility.Visible : Visibility.Collapsed;
        Atualizar.IsEnabled = Autorizar.IsEnabled = true;
    }

    private void MostrarMensagem(string mensagem)
    {
        Mensagem.Text = mensagem;
        Mensagem.Visibility = mensagem.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void AoAtualizar(object sender, RoutedEventArgs e) => await AtualizarAsync(false);
    private async void AoAutorizar(object sender, RoutedEventArgs e) => await AtualizarAsync(true);
    private void AoAbrirAjustes(object sender, RoutedEventArgs e)
    {
        if (!AtalhosWindows.AbrirAjustes(_uri)) MostrarMensagem("Abra esta página pelas Configurações do Windows.");
    }
}
