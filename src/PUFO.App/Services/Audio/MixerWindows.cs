using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace PUFO.App.Services.Audio;

internal readonly record struct ChaveSessao(bool Sistema, uint Processo, string Grupo);
internal sealed record SessaoAudio(string Instancia, ChaveSessao Chave, string Nome, int Volume, bool Mudo, ImageSource? Icone = null, AtividadeAudio Atividade = AtividadeAudio.Disponivel);
internal sealed record GrupoAudio(ChaveSessao Chave, string Nome, int Volume, bool Mudo, bool VolumeMisto, bool MudoMisto, ImageSource? Icone, AtividadeAudio Atividade = AtividadeAudio.Disponivel);

internal sealed class MixerWindows : IDisposable
{
    private readonly Dictionary<string, (AudioSessionControl Controle, ChaveSessao Chave)> _sessoes = new();
    private readonly Dictionary<ChaveSessao, Task<(string Nome, ImageSource? Icone)>> _identidades = new();
    private readonly DetectorAtividade _atividade = new();
    private string? _endpoint;
    internal bool LeituraParcial { get; private set; }

    internal IReadOnlyList<GrupoAudio>? Ler(string? endpoint)
    {
        if (_endpoint != endpoint) Limpar();
        _endpoint = endpoint;
        if (endpoint is null) return [];

        return ServicoAudioWindows.ComSaida<IReadOnlyList<GrupoAudio>?>(dispositivo =>
        {
            if (dispositivo.ID != endpoint) { Limpar(); return []; }
            return LerNoDispositivo(dispositivo);
        }, null);
    }

    internal IReadOnlyList<GrupoAudio> LerNoDispositivo(MMDevice dispositivo)
    {
        LeituraParcial = false;
        var gerenciador = dispositivo.AudioSessionManager;
        // The manager owns its SessionCollection. Each indexed AudioSessionControl has independent ownership.
        var colecao = gerenciador.Sessions;
        int quantidade = colecao.Count;
        var dados = new List<SessaoAudio>(quantidade);
        for (int i = 0; i < quantidade; i++)
        {
            AudioSessionControl? sessao = null;
            try
            {
                sessao = colecao[i];
                var instancia = sessao.GetSessionInstanceIdentifier;
                var estado = sessao.State;
                if (string.IsNullOrEmpty(instancia) || estado == AudioSessionState.AudioSessionStateExpired) continue;
                var chave = _sessoes.TryGetValue(instancia, out var anterior) ? anterior.Chave : Identificar(sessao, instancia);
                var volume = sessao.SimpleAudioVolume;
                var (nome, icone) = Metadados(sessao, chave);
                bool mudo = volume.Mute;
                float nivel = volume.Volume;
                bool ativa = estado == AudioSessionState.AudioSessionStateActive && nivel > 0;
                float? pico = null;
                if (ativa && !mudo)
                {
                    try { pico = sessao.AudioMeterInformation?.MasterPeakValue; }
                    catch (Exception e) when (ServicoAudioWindows.FalhaCoreAudio(e) || e is InvalidCastException) { }
                }
                var atividade = _atividade.Observar(instancia, ativa, mudo, pico, Environment.TickCount64);
                dados.Add(new(instancia, chave, nome, ServicoAudioWindows.Percentual(nivel), mudo, icone, atividade));
                if (anterior.Controle is null)
                {
                    _sessoes.Add(instancia, (sessao, chave));
                    sessao = null;
                }
            }
            catch (Exception e) when (ServicoAudioWindows.FalhaCoreAudio(e)) { LeituraParcial = true; }
            finally { sessao?.Dispose(); }
        }

        var instancias = dados.Select(dado => dado.Instancia).ToHashSet(StringComparer.Ordinal);
        foreach (var id in _sessoes.Keys.Where(id => !instancias.Contains(id)).ToArray())
        {
            _sessoes.Remove(id, out var removida);
            removida.Controle.Dispose();
            _atividade.Remover(id);
        }
        var grupos = Agrupar(dados);
        var chaves = grupos.Select(grupo => grupo.Chave).ToHashSet();
        foreach (var chave in _identidades.Keys.Where(chave => !chaves.Contains(chave)).ToArray())
            _identidades.Remove(chave);
        return grupos;
    }

    internal static ChaveSessao Identificar(bool sistema, uint pid, Guid agrupamento, string identificador, string instancia)
    {
        if (sistema) return new(true, 0, "sistema");
        if (pid == 0) return new(false, 0, instancia);
        var grupo = agrupamento != Guid.Empty ? $"grupo:{agrupamento}" : $"sessão:{identificador}";
        return new(false, pid, agrupamento == Guid.Empty && string.IsNullOrEmpty(identificador) ? instancia : grupo);
    }

    private static ChaveSessao Identificar(AudioSessionControl sessao, string instancia)
    {
        try
        {
            if (sessao.IsSystemSoundsSession) return Identificar(true, 0, Guid.Empty, "", instancia);
            return Identificar(false, sessao.GetProcessID, sessao.GetGroupingParam(), sessao.GetSessionIdentifier, instancia);
        }
        catch (Exception e) when (ServicoAudioWindows.FalhaCoreAudio(e)) { return new(false, 0, instancia); }
    }

    private (string Nome, ImageSource? Icone) Metadados(AudioSessionControl sessao, ChaveSessao chave)
    {
        if (chave.Sistema) return ("Sons do sistema", null);
        if (!_identidades.TryGetValue(chave, out var tarefa))
        {
            string nomeSessao;
            try { nomeSessao = sessao.DisplayName; }
            catch (Exception e) when (ServicoAudioWindows.FalhaCoreAudio(e)) { nomeSessao = ""; }
            tarefa = Task.Run(() => IdentificarProcesso(chave.Processo, nomeSessao));
            _identidades.Add(chave, tarefa);
        }
        return tarefa.IsCompletedSuccessfully ? tarefa.Result : ("Aplicativo de áudio", null);
    }

    private static (string Nome, ImageSource? Icone) IdentificarProcesso(uint pid, string nomeSessao)
    {
        string nome = string.IsNullOrWhiteSpace(nomeSessao) || nomeSessao.StartsWith('@') ? "Aplicativo de áudio" : nomeSessao;
        if (pid == 0 || pid > int.MaxValue) return (nome, null);
        try
        {
            using var processo = Process.GetProcessById((int)pid);
            if (nome == "Aplicativo de áudio") nome = processo.ProcessName;
            string? caminho = processo.MainModule?.FileName;
            if (caminho is null || !Path.IsPathFullyQualified(caminho) || caminho.StartsWith(@"\\")) return (nome, null);
            var descricao = FileVersionInfo.GetVersionInfo(caminho).FileDescription;
            if (!string.IsNullOrWhiteSpace(descricao) && nome == processo.ProcessName) nome = descricao;
            using var icone = System.Drawing.Icon.ExtractAssociatedIcon(caminho);
            if (icone is null) return (nome, null);
            var origem = Imaging.CreateBitmapSourceFromHIcon(icone.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(24, 24));
            var imagem = new FormatConvertedBitmap(origem, PixelFormats.Bgra32, null, 0);
            int stride = imagem.PixelWidth * 4;
            var pixels = new byte[stride * imagem.PixelHeight];
            imagem.CopyPixels(pixels, stride, 0);
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte cinza = (byte)((pixels[i] * 11 + pixels[i + 1] * 59 + pixels[i + 2] * 30) / 100);
                pixels[i] = pixels[i + 1] = pixels[i + 2] = cinza;
            }
            var resultado = BitmapSource.Create(imagem.PixelWidth, imagem.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            resultado.Freeze();
            return (nome, resultado);
        }
        catch (Exception e) when (e is ArgumentException or IOException or System.Security.SecurityException || ServicoAudioWindows.FalhaCoreAudio(e))
        {
            return (nome, null);
        }
    }

    internal static IReadOnlyList<GrupoAudio> Agrupar(IEnumerable<SessaoAudio> sessoes) => sessoes
        .DistinctBy(sessao => sessao.Instancia).GroupBy(sessao => sessao.Chave)
        .Select(grupo => new GrupoAudio(grupo.Key, grupo.First().Nome,
            (int)Math.Round(grupo.Average(sessao => sessao.Volume)), grupo.All(sessao => sessao.Mudo),
            grupo.Min(sessao => sessao.Volume) != grupo.Max(sessao => sessao.Volume),
            grupo.Any(sessao => sessao.Mudo) && !grupo.All(sessao => sessao.Mudo), grupo.First().Icone,
            grupo.All(s => s.Mudo) ? AtividadeAudio.Silenciado :
            grupo.Any(s => !s.Mudo && s.Atividade == AtividadeAudio.Detectado) ? AtividadeAudio.Detectado :
            grupo.Any(s => !s.Mudo && s.Atividade == AtividadeAudio.SemMedidor) ? AtividadeAudio.SemMedidor : AtividadeAudio.Disponivel))
        .OrderBy(grupo => grupo.Nome, StringComparer.CurrentCultureIgnoreCase).ToArray();

    internal void DefinirVolume(ChaveSessao chave, int percentual) => Alterar(chave, volume => volume.Volume = ServicoAudioWindows.Escalar(percentual));
    internal void DefinirMudo(ChaveSessao chave, bool mudo) => Alterar(chave, volume => volume.Mute = mudo);

    private void Alterar(ChaveSessao chave, Action<SimpleAudioVolume> alterar)
    {
        if (_endpoint is null) return;
        ServicoAudioWindows.ComSaida(dispositivo =>
        {
            if (dispositivo.ID != _endpoint) return false;
            foreach (var sessao in _sessoes.Values)
            {
                if (sessao.Chave != chave) continue;
                try
                {
                    if (sessao.Controle.State != AudioSessionState.AudioSessionStateExpired)
                        alterar(sessao.Controle.SimpleAudioVolume);
                }
                catch (Exception e) when (ServicoAudioWindows.FalhaCoreAudio(e)) { }
            }
            return true;
        }, false);
    }

    internal void Suspender()
    {
        foreach (var sessao in _sessoes.Values) sessao.Controle.Dispose();
        _sessoes.Clear();
        _atividade.Limpar();
    }

    internal void Limpar()
    {
        Suspender();
        _identidades.Clear();
        _endpoint = null;
    }

    public void Dispose() => Limpar();
}
