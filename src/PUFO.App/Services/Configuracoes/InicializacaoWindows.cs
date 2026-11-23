using System.IO;
using System.Reflection;
using System.Security;
using Microsoft.Win32;

namespace PUFO.App.Services.Configuracoes;

internal sealed record EstadoInicializacao(bool Ativo, bool PodeAlterar, string Descricao);

internal sealed class InicializacaoWindows
{
    private const string ChaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string NomeValor = "PUFO";
    private static readonly string CaminhoInstalado = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PUFO", "PUFO.App.exe");
    private static string Comando => $"\"{CaminhoInstalado}\"";

    internal static bool DistribuicaoInstalada =>
        string.Equals(Environment.ProcessPath, CaminhoInstalado, StringComparison.OrdinalIgnoreCase) &&
        typeof(InicializacaoWindows).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Any(atributo => atributo.Key == "PufoDistribuicaoInstalada" && atributo.Value == "true");

    internal EstadoInicializacao Consultar()
    {
        try
        {
            using var chave = Registry.CurrentUser.OpenSubKey(ChaveRun);
            object? valor = chave?.GetValue(NomeValor, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            bool ativo = EhNosso(valor);
            if (valor is not null && !ativo) return new(false, false, "Já existe uma entrada PUFO administrada fora do aplicativo.");
            if (ativo && !DistribuicaoInstalada) return new(true, true, "Você pode remover a entrada criada pela versão instalada.");
            if (!DistribuicaoInstalada || Comando.Length > 260)
                return new(false, false, "Disponível para a versão instalada. Esta execução não pode ser registrada.");
            return new(ativo, true, "Abrir o PUFO ao entrar na sua conta do Windows.");
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException)
        {
            return new(false, false, "O Windows não permitiu consultar a inicialização.");
        }
    }

    internal bool Alterar(bool ativar)
    {
        try
        {
            if (ativar && (!DistribuicaoInstalada || Comando.Length > 260)) return false;
            using var chave = ativar ? Registry.CurrentUser.CreateSubKey(ChaveRun) : Registry.CurrentUser.OpenSubKey(ChaveRun, writable: true);
            if (chave is null) return !ativar;
            object? valor = chave.GetValue(NomeValor, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (valor is not null && !EhNosso(valor)) return false;
            if (ativar) chave.SetValue(NomeValor, Comando, RegistryValueKind.String);
            else if (valor is not null) chave.DeleteValue(NomeValor, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException) { return false; }
    }

    private static bool EhNosso(object? valor) => valor is string comando &&
        string.Equals(comando, Comando, StringComparison.OrdinalIgnoreCase);
}
