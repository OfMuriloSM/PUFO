using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using PUFO.App.Services.Voz;

namespace PUFO.App;

public partial class MainWindow
{
    private readonly SessaoVoz _sessaoVoz = new();
    internal Func<Action<bool>, CancellationToken, Task<string>> ReconhecerMouse { get; set; } =
        (atividade, token) => ReconhecimentoLocal.OuvirAsync("", false, TimeSpan.FromSeconds(7), atividade, token);
    private bool VozVisivel => _sessaoVoz.Fase != FaseVoz.Inativa;

    private void PrepararVoz()
    {
        _sessaoVoz.PodeIniciar = () => IsVisible && !_ocultoPorTelaCheia && !_encerrado;
        Raiz.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(AoMouseCentral), true);
        Raiz.AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler((_, e) =>
        {
            if (e.ChangedButton == MouseButton.Middle) e.Handled = true;
        }), true);
        _sessaoVoz.Mudou += AoMudarVoz;
    }

    private async void AoMouseCentral(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || _ocultoPorTelaCheia || !IsVisible) return;
        e.Handled = true;
        await _sessaoVoz.ExecutarAsync(ReconhecerMouse);
    }

    private void AoMudarVoz(FaseVoz fase, string mensagem)
    {
        _janelaConfiguracoes?.MostrarAtividadeVoz(fase);
        if (_encerrado) return;
        bool mudouSuperficie = (PainelVoz.Visibility == Visibility.Visible) != VozVisivel;
        if (VozVisivel)
        {
            PararTemporizadoresInteracao();
            _esperaModulo.Stop();
            _timerContexto.Stop();
            _timerCliqueFora.Stop();
        }
        MensagemVoz.Text = mensagem;
        SimboloVoz.Data = (System.Windows.Media.Geometry)FindResource(fase switch
        {
            FaseVoz.Ouvindo => "MicrofoneVoz", FaseVoz.Respondendo => "RespostaVoz",
            FaseVoz.Erro => "AvisoVoz", _ => "EsperaVoz"
        });
        AnimarMicrofone();
        AtualizarConteudo();
        AnimarDimensoes(mudouSuperficie);
        if (!VozVisivel && !_ocultoPorTelaCheia)
        {
            if (_estadoIlha != EstadoIlha.Repouso) _timerCliqueFora.Start();
            if (_estadoIlha == EstadoIlha.Previa && !PonteiroSobreIlha()) _timerFechamento.Start();
            if (_previaContextual) _timerContexto.Start();
        }
    }

    private void AnimarMicrofone()
    {
        SimboloVoz.BeginAnimation(OpacityProperty, null);
        if (_sessaoVoz.Fase != FaseVoz.Ouvindo || !AnimacoesPermitidas) return;
        SimboloVoz.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.5,
            TimeSpan.FromMilliseconds(65000d / _configuracoes.VelocidadeAnimacoes))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
    }
}
