using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace PUFO.App.Services;

internal sealed class ClimaOpenMeteo(HttpClient http)
{
    private static readonly CultureInfo Portugues = CultureInfo.GetCultureInfo("pt-BR");

    internal async Task<string> ConsultarAsync(string localidade, bool chuva, CancellationToken token)
    {
        string[] partes = localidade.Split(',', StringSplitOptions.TrimEntries);
        if (partes[0].Length < 2) return "Informe a cidade nas Configurações.";
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(token);
        prazo.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            string url = "https://geocoding-api.open-meteo.com/v1/search?language=pt&count=100&name=" + Uri.EscapeDataString(partes[0]);
            using var busca = await ObterAsync(url, prazo.Token);
            if (!busca.RootElement.TryGetProperty("results", out var lista)) return "Cidade não encontrada.";
            var cidades = lista.EnumerateArray().Where(c => Igual(Campo(c, "name"), partes[0]) &&
                partes.Skip(1).All(p => new[] { "admin1", "country", "country_code" }.Any(k => Igual(Campo(c, k), p)))).ToArray();
            if (cidades.Length == 0) return "Cidade não encontrada. Confira o nome e o estado.";
            if (cidades.Length > 1)
            {
                var exemplos = cidades.Select(c => $"{Campo(c, "name")}, {Campo(c, "admin1")}, {Campo(c, "country_code")}")
                    .Distinct().Take(2);
                return "Há mais de uma cidade. Especifique a localidade, como " + string.Join(" ou ", exemplos) + ".";
            }
            var cidade = cidades[0];
            double lat = cidade.GetProperty("latitude").GetDouble(), lon = cidade.GetProperty("longitude").GetDouble();
            if (!double.IsFinite(lat) || !double.IsFinite(lon) || Math.Abs(lat) > 90 || Math.Abs(lon) > 180)
                return "A cidade retornou coordenadas inválidas.";
            string consulta = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&timezone=auto&forecast_days=1");
            using var previsao = await ObterAsync(consulta + (chuva ? "&daily=precipitation_probability_max" : "&current=temperature_2m"), prazo.Token);
            string nome = Campo(cidade, "name");
            if (chuva)
            {
                var valores = previsao.RootElement.GetProperty("daily").GetProperty("precipitation_probability_max");
                if (valores.GetArrayLength() > 0 && valores[0].TryGetInt32(out int chance) && chance is >= 0 and <= 100)
                    return $"Hoje, a chance máxima de chuva em {nome} é de {chance}%.";
            }
            else if (previsao.RootElement.GetProperty("current").GetProperty("temperature_2m").TryGetDouble(out double temperatura) && double.IsFinite(temperatura))
                return $"Em {nome}, a temperatura é de {temperatura.ToString("0.#", Portugues)} °C.";
            return "A previsão está indisponível para esta cidade.";
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return "A consulta do clima demorou demais."; }
        catch (Exception e) when (e is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { return "Não consegui consultar a previsão agora."; }
    }

    private async Task<JsonDocument> ObterAsync(string url, CancellationToken token)
    {
        using var resposta = await http.GetAsync(url, token);
        resposta.EnsureSuccessStatusCode();
        using var dados = await resposta.Content.ReadAsStreamAsync(token);
        return await JsonDocument.ParseAsync(dados, cancellationToken: token);
    }

    private static string Campo(JsonElement item, string nome) =>
        item.TryGetProperty(nome, out var valor) ? valor.GetString() ?? "" : "";

    private static bool Igual(string a, string b) =>
        Portugues.CompareInfo.Compare(a, b, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;
}
