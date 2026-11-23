using System.Runtime.InteropServices;
using System.Text;

namespace PUFO.App.Services;

internal static class WifiWindows
{
    internal sealed record Rede(string Nome, uint Sinal, bool Conectada);

    internal static ConsultaPrevia Listar()
    {
        try
        {
            uint erro = WlanOpenHandle(2, 0, out _, out var cliente);
            if (erro != 0) return new(MensagemErro(erro), []);
            try { return ConsultarInterfaces(cliente); }
            finally { WlanCloseHandle(cliente, 0); }
        }
        catch (DllNotFoundException) { return new("O serviço Wi-Fi está indisponível.", []); }
    }

    private static ConsultaPrevia ConsultarInterfaces(nint cliente)
    {
        uint erro = WlanEnumInterfaces(cliente, 0, out var interfaces);
        if (erro != 0) return new(MensagemErro(erro), []);
        var redes = new List<Rede>();
        var erros = new HashSet<string>();
        try
        {
            if (Marshal.ReadInt32(interfaces) == 0) return new("Nenhum adaptador Wi-Fi disponível.", []);
            foreach (var adaptador in LerLista<InterfaceWlan>(interfaces))
            {
                var id = adaptador.Id;
                if (RadioDesligado(cliente, id))
                {
                    erros.Add("O rádio Wi-Fi está desligado.");
                    continue;
                }
                erro = WlanGetAvailableNetworkList(cliente, ref id, 0, 0, out var lista);
                if (erro != 0) { erros.Add(MensagemErro(erro)); continue; }
                try
                {
                    foreach (var item in LerLista<RedeWlan>(lista))
                    {
                        int tamanho = (int)Math.Min(item.TamanhoSsid, 32u);
                        string nome = tamanho == 0 ? "Rede oculta" : Encoding.UTF8.GetString(item.Ssid, 0, tamanho);
                        redes.Add(new(nome, item.Sinal, (item.Flags & 1) != 0));
                    }
                }
                finally { WlanFreeMemory(lista); }
            }
        }
        finally { WlanFreeMemory(interfaces); }
        return Organizar(redes, string.Join(" ", erros));
    }

    private static bool RadioDesligado(nint cliente, Guid id)
    {
        const uint radioState = 4;
        if (WlanQueryInterface(cliente, ref id, radioState, 0, out uint bytes, out var dados, 0) != 0) return false;
        try
        {
            if (bytes < 4) return false;
            int quantidade = Marshal.ReadInt32(dados);
            if (quantidade <= 0 || quantidade > (bytes - 4) / 12) return false;
            for (int i = 0; i < quantidade; i++)
            {
                // Cada PHY publica índice, estado de software e estado de hardware.
                int offset = 4 + i * 12;
                if (Marshal.ReadInt32(dados, offset + 4) != 2 && Marshal.ReadInt32(dados, offset + 8) != 2)
                    return false;
            }
            return true;
        }
        finally { WlanFreeMemory(dados); }
    }

    internal static ConsultaPrevia Organizar(IEnumerable<Rede> redes, string aviso)
    {
        var linhas = redes.GroupBy(r => r.Nome, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(r => r.Conectada).ThenByDescending(r => r.Sinal).First())
            .OrderByDescending(r => r.Conectada).ThenByDescending(r => r.Sinal).ThenBy(r => r.Nome)
            .Select(r => new ItemConsulta(r.Nome, (r.Conectada ? "Conectada · " : "") + $"{r.Sinal}% de sinal")).ToArray();
        return new(linhas.Length == 0 && aviso.Length == 0 ? "Nenhuma rede visível." : aviso, linhas);
    }

    internal static string MensagemErro(uint codigo) => codigo switch
    {
        5 => "Acesso às redes bloqueado pelo Windows. Verifique a permissão de localização.",
        0x80342002 => "O rádio Wi-Fi está desligado.",
        1062 => "O serviço WLAN está parado.",
        _ => "Não foi possível consultar as redes Wi-Fi."
    };

    // Ambas as listas começam com DWORD dwNumberOfItems e DWORD dwIndex.
    private static IEnumerable<T> LerLista<T>(nint lista) where T : struct
    {
        int quantidade = Marshal.ReadInt32(lista);
        int passo = Marshal.SizeOf<T>();
        for (int i = 0; i < quantidade; i++)
            yield return Marshal.PtrToStructure<T>(lista + 8 + checked(i * passo));
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct InterfaceWlan
    {
        public Guid Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Descricao;
        public uint Estado;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RedeWlan
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Perfil;
        public uint TamanhoSsid;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Ssid;
        public uint TipoBss, NumeroBssids, Conectavel, Motivo, NumeroPhys;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] Phys;
        public uint MaisPhys, Sinal, Segura, Autenticacao, Cifra, Flags, Reservado;
    }

    [DllImport("wlanapi.dll")] private static extern uint WlanOpenHandle(uint versao, nint reservado, out uint negociada, out nint cliente);
    [DllImport("wlanapi.dll")] private static extern uint WlanCloseHandle(nint cliente, nint reservado);
    [DllImport("wlanapi.dll")] private static extern uint WlanEnumInterfaces(nint cliente, nint reservado, out nint interfaces);
    [DllImport("wlanapi.dll")] private static extern uint WlanQueryInterface(nint cliente, ref Guid id, uint opcode, nint reservado, out uint tamanho, out nint dados, nint tipo);
    [DllImport("wlanapi.dll")] private static extern uint WlanGetAvailableNetworkList(nint cliente, ref Guid id, uint flags, nint reservado, out nint lista);
    [DllImport("wlanapi.dll")] private static extern void WlanFreeMemory(nint lista);
}
