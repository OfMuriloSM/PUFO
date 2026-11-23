namespace PUFO.App.Services;

internal sealed record ItemConsulta(string Nome, string Detalhe);
internal sealed record ConsultaPrevia(string Mensagem, IReadOnlyList<ItemConsulta> Itens, bool PodeAutorizar = false);
