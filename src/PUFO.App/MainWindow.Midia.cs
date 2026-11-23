using System.Windows;
using System.Windows.Threading;
using PUFO.App.Models;
using PUFO.App.Services.Midia;

namespace PUFO.App;

public partial class MainWindow
{
    private readonly SelecaoMidia _selecaoMidia = new();
    private readonly DispatcherTimer _relogioMidia = new() { Interval = TimeSpan.FromSeconds(1) };
    private IReadOnlyList<SessaoMidia> _sessoesMidia = [];
    private int? _ativaWindows;
    private string? _erroMidia;
    private MidiaWindows? _fonteMidia;
    private bool _usarFonteNativa;
    private bool MostrarMidiaNaPrevia => _configuracoes.MidiaAtiva && _configuracoes.MidiaNaPrevia && _selecaoMidia.Atual?.Tocando == true;

    private void PrepararMidia(bool conectar)
    {
        _usarFonteNativa = conectar;
        MidiaCompleta.Selecionou += id =>
        {
            _selecaoMidia.Escolher(id);
            ReceberSessoes(_sessoesMidia, _ativaWindows);
        };
        MidiaCompacta.Comando += ExecutarComandoMidia;
        MidiaCompleta.Comando += ExecutarComandoMidia;
        _relogioMidia.Tick += (_, _) => MidiaCompleta.AtualizarTempo();
        MidiaCompleta.IsVisibleChanged += (_, _) => AtualizarRelogioMidia();
        Loaded += (_, _) => ConfigurarFonteMidia();
    }

    private void AtualizarRelogioMidia() => _relogioMidia.IsEnabled =
        !_encerrado && MidiaCompleta.IsVisible && _selecaoMidia.Atual is { Tocando: true, TemTempo: true };

    private void ConfigurarFonteMidia()
    {
        if (!_configuracoes.MidiaAtiva)
        {
            EncerrarMidia();
            ReceberSessoes([], null);
            return;
        }
        if (!_usarFonteNativa || _fonteMidia is not null) return;
        _fonteMidia = new MidiaWindows(Dispatcher);
        _fonteMidia.Mudou += ReceberSessoes;
        _fonteMidia.Falhou += AoFalharMidia;
        _ = _fonteMidia.IniciarAsync();
    }

    private void AoFalharMidia(string mensagem)
    {
        _erroMidia = mensagem;
        _janelaConfiguracoes?.AtualizarMidia(_selecaoMidia.Atual, mensagem);
    }

    internal void ReceberSessoes(IReadOnlyList<SessaoMidia> sessoes, int? ativa)
    {
        _erroMidia = null;
        _sessoesMidia = sessoes;
        _ativaWindows = ativa;
        _selecaoMidia.Atualizar(sessoes, ativa);
        ApresentarMidia();
    }

    private void ApresentarMidia()
    {
        var atual = _configuracoes.MidiaAtiva ? _selecaoMidia.Atual : null;
        Reproducoes.Visibility = atual is null ? Visibility.Collapsed : Visibility.Visible;
        Reproducoes.Content = $"Reproduções · {_sessoesMidia.Count}";
        bool mostrarPrevia = ExibirCartaoNaPrevia;
        bool revelarPainel = atual is not null;
        bool mudouPainel = RevelaMidia.IsHitTestVisible != revelarPainel;
        bool alterouLayout = (MidiaCompacta.Visibility == Visibility.Visible) != mostrarPrevia || mudouPainel;
        MidiaCompacta.Aplicar(atual, _sessoesMidia);
        MidiaCompleta.Aplicar(atual, _sessoesMidia);
        MidiaCompacta.Visibility = mostrarPrevia ? Visibility.Visible : Visibility.Collapsed;
        if (mudouPainel)
            Revelar(RevelaMidia, MidiaCompleta, revelarPainel, animar: _estadoIlha == EstadoIlha.Completo);
        _janelaConfiguracoes?.AtualizarMidia(atual, _erroMidia);
        AtualizarRelogioMidia();
        if (alterouLayout)
        {
            AtualizarConteudo();
            AgendarMedicao();
        }
    }

    private async void ExecutarComandoMidia(int id, ComandoMidia comando, TimeSpan posicao)
    {
        var fonte = _fonteMidia;
        if (fonte is null || _selecaoMidia.Atual?.Id != id) return;
        bool aceito = await fonte.ExecutarAsync(id, comando, posicao);
        if (_fonteMidia != fonte || _selecaoMidia.Atual?.Id != id) return;
        MidiaCompleta.Avisar(aceito ? "" : "A sessão não aceitou o comando.");
    }

    private void EncerrarMidia()
    {
        _relogioMidia.Stop();
        if (_fonteMidia is null) return;
        _fonteMidia.Mudou -= ReceberSessoes;
        _fonteMidia.Falhou -= AoFalharMidia;
        _fonteMidia.Dispose();
        _fonteMidia = null;
    }
}
