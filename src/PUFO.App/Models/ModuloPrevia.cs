using System.Windows;

namespace PUFO.App.Models;

internal sealed record ModuloPrevia(string Id, string Nome, string Icone, FrameworkElement? Painel = null,
    Action? Acao = null, bool Disponivel = true, bool Preenchido = false);
