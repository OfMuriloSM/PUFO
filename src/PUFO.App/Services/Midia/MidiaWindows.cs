using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PUFO.App.Models;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Sessao = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using Gerenciador = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;

namespace PUFO.App.Services.Midia;

// Events can arrive on arbitrary threads. All catalog mutations and notifications use the owning dispatcher.
internal sealed class MidiaWindows : IDisposable
{
    private readonly Dictionary<Sessao, Entrada> _entradas = [];
    private readonly CancellationTokenSource _vida = new();
    private Gerenciador? _gerenciador;
    private int _sequencia, _catalogoAgendado;
    private bool _encerrado;
    internal event Action<IReadOnlyList<SessaoMidia>, int?>? Mudou;
    internal event Action<string>? Falhou;

    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _retentativa;
    private bool _iniciando;

    internal MidiaWindows(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _retentativa = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(30) };
        _retentativa.Tick += AoTentarNovamente;
    }

    private void AoTentarNovamente(object? sender, EventArgs e)
    {
        _retentativa.Stop();
        _ = IniciarAsync();
    }

    internal async Task IniciarAsync()
    {
        if (_encerrado || _iniciando || _gerenciador is not null) return;
        _iniciando = true;
        try
        {
            var gerenciador = await Gerenciador.RequestAsync().AsTask(_vida.Token);
            if (_encerrado) return;
            _gerenciador = gerenciador;
            gerenciador.SessionsChanged += AoMudarSessoes;
            gerenciador.CurrentSessionChanged += AoMudarAtual;
            AtualizarCatalogo();
        }
        catch (Exception)
        {
            if (!_encerrado) RecuperarCatalogo("O Windows não disponibilizou o serviço de mídia.");
        }
        finally { _iniciando = false; }
    }

    private void AoMudarSessoes(Gerenciador sender, SessionsChangedEventArgs args) => AgendarCatalogo();
    private void AoMudarAtual(Gerenciador sender, CurrentSessionChangedEventArgs args) => AgendarCatalogo();
    private void AgendarCatalogo()
    {
        if (Interlocked.Exchange(ref _catalogoAgendado, 1) == 0)
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                Interlocked.Exchange(ref _catalogoAgendado, 0);
                AtualizarCatalogo();
            }));
    }

    private int? _ativa;
    private void AtualizarCatalogo()
    {
        if (_encerrado || _gerenciador is null) return;
        try
        {
            var sessoes = _gerenciador.GetSessions().ToHashSet();
            foreach (var antiga in _entradas.Keys.Where(s => !sessoes.Contains(s)).ToArray())
            {
                _entradas[antiga].Dispose();
                _entradas.Remove(antiga);
            }
            foreach (var sessao in sessoes)
            {
                if (_entradas.ContainsKey(sessao)) continue;
                Entrada? entrada = null;
                try
                {
                    entrada = new Entrada(this, sessao, ++_sequencia);
                    entrada.Iniciar();
                    _entradas.Add(sessao, entrada);
                }
                catch (Exception) { entrada?.Dispose(); }
            }
            var atual = _gerenciador.GetCurrentSession();
            _ativa = atual is not null && _entradas.TryGetValue(atual, out var encontrada) ? encontrada.Dados.Id : null;
            Publicar();
        }
        catch (Exception) { RecuperarCatalogo("Não foi possível consultar as sessões de mídia."); }
    }

    private void Publicar()
    {
        if (!_encerrado) Mudou?.Invoke(_entradas.Values.Select(e => e.Dados).ToArray(), _ativa);
    }

    internal async Task<bool> ExecutarAsync(int id, ComandoMidia comando, TimeSpan posicao)
    {
        var entrada = _entradas.Values.FirstOrDefault(e => e.Dados.Id == id);
        if (_encerrado || entrada is null || !entrada.Dados.Permite(comando)) return false;
        var s = entrada.Nativa;
        try
        {
            var operacao = comando switch
            {
                ComandoMidia.Reproduzir => s.TryPlayAsync(),
                ComandoMidia.Pausar => s.TryPauseAsync(),
                ComandoMidia.Alternar => s.TryTogglePlayPauseAsync(),
                ComandoMidia.Anterior => s.TrySkipPreviousAsync(),
                ComandoMidia.Proxima => s.TrySkipNextAsync(),
                ComandoMidia.Posicao => s.TryChangePlaybackPositionAsync(Math.Clamp(posicao.Ticks, entrada.Dados.MinSeek.Ticks, entrada.Dados.MaxSeek.Ticks)),
                _ => throw new ArgumentOutOfRangeException(nameof(comando))
            };
            return await operacao.AsTask(_vida.Token);
        }
        catch (Exception) { return false; }
    }

    private void RecuperarCatalogo(string mensagem)
    {
        LimparCatalogo();
        Publicar();
        Falhou?.Invoke(mensagem);
        if (!_encerrado) _retentativa.Start();
    }

    private void LimparCatalogo()
    {
        if (_gerenciador is { } gerente)
        {
            _gerenciador = null;
            Desinscrever(() => gerente.SessionsChanged -= AoMudarSessoes);
            Desinscrever(() => gerente.CurrentSessionChanged -= AoMudarAtual);
        }
        foreach (var entrada in _entradas.Values) entrada.Dispose();
        _entradas.Clear();
        _ativa = null;
    }

    // Um servidor WinRT que já encerrou pode recusar até a remoção de um handler.
    private static void Desinscrever(Action remover)
    {
        try { remover(); }
        catch (Exception) { }
    }

    public void Dispose()
    {
        if (_encerrado) return;
        _encerrado = true;
        _retentativa.Stop();
        _retentativa.Tick -= AoTentarNovamente;
        _vida.Cancel();
        LimparCatalogo();
        _vida.Dispose();
    }

    private sealed class Entrada(MidiaWindows dono, Sessao sessao, int id) : IDisposable
    {
        private const int Transporte = 1, Metadados = 2;
        private int _pendente, _revisao;
        private bool _lendo, _removida;
        private IRandomAccessStreamReference? _ultimaMiniatura;
        private readonly CancellationTokenSource _cancelar = new();
        internal Sessao Nativa => sessao;
        internal SessaoMidia Dados { get; private set; } = new(id, sessao.SourceAppUserModelId);
        internal void Iniciar()
        {
            sessao.PlaybackInfoChanged += Playback;
            sessao.TimelinePropertiesChanged += Timeline;
            sessao.MediaPropertiesChanged += Propriedades;
            Agendar(Transporte | Metadados);
        }
        private void Playback(Sessao s, PlaybackInfoChangedEventArgs e) => Agendar(Transporte);
        private void Timeline(Sessao s, TimelinePropertiesChangedEventArgs e) => Agendar(Transporte);
        private void Propriedades(Sessao s, MediaPropertiesChangedEventArgs e) => Agendar(Metadados);

        private void Agendar(int flags)
        {
            if (Interlocked.Or(ref _pendente, flags) != 0) return;
            dono._dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Atualizar));
        }
        private void Atualizar()
        {
            int flags = Interlocked.Exchange(ref _pendente, 0);
            if (_removida) return;
            if ((flags & Transporte) != 0)
            {
                try
                {
                    var p = sessao.GetPlaybackInfo();
                    var c = p.Controls;
                    Dados = Dados with { Estado = p.PlaybackStatus, Taxa = p.PlaybackRate ?? 1,
                        Play = c.IsPlayEnabled, Pause = c.IsPauseEnabled, Toggle = c.IsPlayPauseToggleEnabled,
                        Proxima = c.IsNextEnabled, Anterior = c.IsPreviousEnabled, Seek = c.IsPlaybackPositionEnabled };
                }
                catch (Exception)
                {
                    Dados = Dados with { Estado = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed,
                        Play = false, Pause = false, Toggle = false, Anterior = false, Proxima = false, Seek = false };
                }
                try
                {
                    var t = sessao.GetTimelineProperties();
                    Dados = Dados with { Inicio = t.StartTime, Fim = t.EndTime, MinSeek = t.MinSeekTime,
                        MaxSeek = t.MaxSeekTime, Posicao = t.Position, Atualizado = t.LastUpdatedTime };
                }
                catch (Exception) { Dados = Dados with { Inicio = default, Fim = default, MinSeek = default, MaxSeek = default }; }
                dono.Publicar();
            }
            if ((flags & Metadados) != 0)
            {
                _revisao++;
                if (!_lendo) _ = LerMetadadosAsync();
            }
        }

        private async Task LerMetadadosAsync()
        {
            _lendo = true;
            int revisao = _revisao;
            try
            {
                while (!_removida)
                {
                    revisao = _revisao;
                    var p = await sessao.TryGetMediaPropertiesAsync().AsTask(_cancelar.Token);
                    if (_removida) return;
                    if (revisao != _revisao) continue;
                    string titulo = p?.Title ?? "";
                    string artista = !string.IsNullOrWhiteSpace(p?.Artist) ? p.Artist : p?.Subtitle ?? "";
                    if (Dados.Titulo != titulo || Dados.Artista != artista)
                    {
                        _ultimaMiniatura = null;
                        Dados = Dados with { Titulo = titulo, Artista = artista, Capa = null };
                        dono.Publicar();
                    }
                    var capa = p?.Thumbnail;
                    if (Equals(capa, _ultimaMiniatura) && (capa is null || Dados.Capa is not null)) return;
                    var token = _cancelar.Token;
                    var imagem = capa is null ? null : await Task.Run(() => LerCapaAsync(capa, token), token);
                    if (_removida) return;
                    if (revisao != _revisao) continue;
                    _ultimaMiniatura = capa;
                    Dados = Dados with { Titulo = titulo, Artista = artista, Capa = imagem };
                    dono.Publicar();
                    return;
                }
            }
            catch (Exception)
            {
                if (!_removida && revisao == _revisao) { Dados = Dados with { Titulo = "", Artista = "", Capa = null }; dono.Publicar(); }
            }
            finally
            {
                _lendo = false;
                if (!_removida && revisao != _revisao) _ = LerMetadadosAsync();
            }
        }

        private static async Task<ImageSource?> LerCapaAsync(IRandomAccessStreamReference referencia, CancellationToken token)
        {
            try
            {
                using var stream = await referencia.OpenReadAsync().AsTask(token);
                if (stream.Size is 0 or > 8 * 1024 * 1024) return null;
                using var origem = stream.AsStreamForRead();
                using var bytes = new MemoryStream();
                await origem.CopyToAsync(bytes, token);
                bytes.Position = 0;
                var imagem = new BitmapImage();
                imagem.BeginInit();
                imagem.CacheOption = BitmapCacheOption.OnLoad;
                imagem.DecodePixelWidth = 96;
                imagem.StreamSource = bytes;
                imagem.EndInit();
                imagem.Freeze();
                return imagem;
            }
            catch (Exception) { return null; }
        }

        public void Dispose()
        {
            _removida = true;
            _ultimaMiniatura = null;
            _cancelar.Cancel();
            Desinscrever(() => sessao.PlaybackInfoChanged -= Playback);
            Desinscrever(() => sessao.TimelinePropertiesChanged -= Timeline);
            Desinscrever(() => sessao.MediaPropertiesChanged -= Propriedades);
            _cancelar.Dispose();
        }
    }
}
