using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PUFO.App.Models;

namespace PUFO.App.Services.Configuracoes;

internal sealed class ServicoConfiguracoes(string? pasta = null)
{
    private static readonly JsonSerializerOptions Formato = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    internal string Pasta { get; } = pasta ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PUFO");
    internal string Arquivo => Path.Combine(Pasta, "settings.json");
    private string Backup => Arquivo + ".bak";

    internal ConfiguracoesPufo Carregar() => Ler(Arquivo) ?? Ler(Backup) ?? new();

    private static ConfiguracoesPufo? Ler(string arquivo)
    {
        try
        {
            using var stream = File.OpenRead(arquivo);
            return JsonSerializer.Deserialize<ConfiguracoesPufo>(stream, Formato)?.Normalizar();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    internal bool Salvar(ConfiguracoesPufo configuracoes)
    {
        string temporario = Path.Combine(Pasta, $"settings.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(Pasta);
            using (var stream = new FileStream(temporario, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, configuracoes.Normalizar(), Formato);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(Arquivo))
            {
                // A corrupt primary must not replace the last readable backup.
                if (Ler(Arquivo) is not null) File.Replace(temporario, Arquivo, Backup);
                else File.Replace(temporario, Arquivo, null);
            }
            else File.Move(temporario, Arquivo);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        finally
        {
            try { File.Delete(temporario); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
