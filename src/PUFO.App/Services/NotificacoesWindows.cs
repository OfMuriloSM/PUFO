using System.Runtime.InteropServices;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace PUFO.App.Services;

internal static class NotificacoesWindows
{
    [DllImport("kernel32.dll")]
    private static extern int GetCurrentPackageFullName(ref uint tamanho, nint nome);

    internal static async Task<ConsultaPrevia> ConsultarAsync(bool autorizar)
    {
        uint tamanho = 0;
        if (GetCurrentPackageFullName(ref tamanho, 0) != 122) // ERROR_INSUFFICIENT_BUFFER indica identidade de pacote.
            return new("Esta edição sem pacote não tem acesso às notificações.", []);
        try
        {
            var leitor = UserNotificationListener.Current;
            var acesso = autorizar ? await leitor.RequestAccessAsync() : leitor.GetAccessStatus();
            if (acesso == UserNotificationListenerAccessStatus.Unspecified)
                return new("Permita o acesso para ver as notificações recentes.", [], true);
            if (acesso != UserNotificationListenerAccessStatus.Allowed)
                return new("O Windows não concedeu acesso às notificações.", []);
            var publicadas = await leitor.GetNotificationsAsync(NotificationKinds.Toast);
            var itens = new List<ItemConsulta>();
            foreach (var n in publicadas.OrderByDescending(n => n.CreationTime).Take(8))
            {
                var texto = n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric)?
                    .GetTextElements().FirstOrDefault()?.Text;
                itens.Add(new(n.AppInfo.DisplayInfo.DisplayName, texto ?? ""));
            }
            return new(itens.Count == 0 ? "Nenhuma notificação recente." : "", itens);
        }
        catch (Exception e) when (e is ExternalException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            return new("O acesso está indisponível nesta instalação ou foi revogado.", []);
        }
    }
}
