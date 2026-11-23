using System.Buffers.Binary;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace PUFO.App.Services.Voz;

internal sealed record EntradaVoz(string Id, string Nome);

internal static class MicrofoneWindows
{
    internal const int Frequencia = 16000;
    internal static IReadOnlyList<EntradaVoz> Listar()
    {
        using var enumerador = new MMDeviceEnumerator();
        using var entradas = enumerador.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        var lista = new List<EntradaVoz>();
        foreach (var entrada in entradas)
        {
            using (entrada) lista.Add(new(entrada.ID, entrada.FriendlyName));
        }
        return lista;
    }

    private static bool TemSinal(ReadOnlySpan<byte> amostras)
    {
        for (int i = 0; i + 1 < amostras.Length; i += 2)
            if (Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(amostras[i..])) >= 350) return true;
        return false;
    }

    internal static Task<byte[]> CapturarAsync(string id, TimeSpan limite, Action<bool> atividade, CancellationToken token) =>
        Task.Run(async () =>
        {
            using var enumerador = new MMDeviceEnumerator();
            using var entrada = id.Length > 0 ? enumerador.GetDevice(id) :
                enumerador.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            if (entrada.DataFlow != DataFlow.Capture || entrada.State != DeviceState.Active)
                throw new InvalidOperationException("Microfone indisponível.");
            using var captura = new WasapiRecorderBuilder().WithDevice(entrada).WithSharedMode()
                .WithFormat(new WaveFormat(Frequencia, 16, 1)).Build();
            using var buffer = new MemoryStream();
            using var prazo = CancellationTokenSource.CreateLinkedTokenSource(token);
            prazo.CancelAfter(limite);
            int bytesSilencio = 0;
            bool ativou = false, ouviu = false;
            try
            {
                await foreach (var pacote in captura.CaptureAsync(prazo.Token))
                {
                    if (pacote.Data.IsEmpty) continue;
                    if (!ativou) { ativou = true; atividade(true); }
                    buffer.Write(pacote.Data.Span);
                    bool som = TemSinal(pacote.Data.Span);
                    ouviu |= som;
                    bytesSilencio = som ? 0 : bytesSilencio + pacote.Data.Length;
                    if ((ouviu && bytesSilencio >= Frequencia * 2 * 0.9) || buffer.Length >= limite.TotalSeconds * Frequencia * 2)
                        break;
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
            finally { if (ativou) atividade(false); }
            token.ThrowIfCancellationRequested();
            if (!ouviu) throw new TimeoutException("Tempo de escuta encerrado.");
            return buffer.ToArray();
        }, token);
}
