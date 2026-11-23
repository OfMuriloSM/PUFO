namespace PUFO.App.Services.Voz;

internal enum FaseVoz { Inativa, Preparando, Ouvindo, Processando, Respondendo, Erro }

internal sealed class SessaoVoz
{
    private CancellationTokenSource? _atual;
    internal FaseVoz Fase { get; private set; }
    internal Func<bool>? PodeIniciar { get; set; }
    internal bool Ocupada => _atual is not null;
    internal event Action<FaseVoz, string>? Mudou;
    internal void Cancelar() => _atual?.Cancel();

    // Executada no Dispatcher. Cancelar impede trabalho novo até a liberação do dispositivo.
    internal async Task ExecutarAsync(Func<Action<bool>, CancellationToken, Task<string>> obter,
        Func<string, CancellationToken, Task>? falar = null)
    {
        if (_atual is not null) { Cancelar(); return; }
        if (PodeIniciar?.Invoke() == false) return;
        using var vida = _atual = new CancellationTokenSource();
        var token = vida.Token;
        try
        {
            Mostrar(FaseVoz.Preparando, "Preparando…");
            var contexto = SynchronizationContext.Current;
            var texto = await obter(ativa =>
            {
                void Publicar()
                {
                    if (ReferenceEquals(_atual, vida) && !token.IsCancellationRequested && Fase is FaseVoz.Preparando or FaseVoz.Ouvindo)
                        Mostrar(ativa ? FaseVoz.Ouvindo : FaseVoz.Processando, ativa ? "Ouvindo…" : "Reconhecendo…");
                }
                if (contexto is null || ReferenceEquals(SynchronizationContext.Current, contexto)) Publicar();
                else contexto.Post(_ => Publicar(), null);
            }, token);
            token.ThrowIfCancellationRequested();
            Mostrar(FaseVoz.Respondendo, string.IsNullOrWhiteSpace(texto) ? "Não entendi. Tente novamente." : texto);
            if (falar is not null && texto.Length > 0) await falar(texto, token);
            await Task.Delay(2400, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception erro)
        {
            if (!token.IsCancellationRequested)
            {
                string mensagem = erro switch
                {
                    TimeoutException => "Tempo de escuta encerrado.",
                    InvalidOperationException when erro.Message.StartsWith("O Vosk") || erro.Message.StartsWith("Microfone") ||
                        erro.Message.StartsWith("Reconhecedor Windows") => erro.Message,
                    _ => "Voz indisponível. Confira as Configurações."
                };
                Mostrar(FaseVoz.Erro, mensagem);
                try { await Task.Delay(3200, token); } catch (OperationCanceledException) { }
            }
        }
        finally { _atual = null; Mostrar(FaseVoz.Inativa, ""); }
    }

    private void Mostrar(FaseVoz fase, string mensagem)
    {
        Fase = fase;
        Mudou?.Invoke(fase, mensagem);
    }
}
