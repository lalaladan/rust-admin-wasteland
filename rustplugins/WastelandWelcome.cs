using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("WastelandWelcome", "DanRomanov", "1.0.0")]
    [Description("Выводит приветственное сообщение в чат, когда игрок просыпается")]
    class WastelandWelcome : RustPlugin
    {
        // Список строк сообщения
        private readonly List<string> welcomeMessage = new List<string>
        {
            "<size=20><color=#74c365>★ ДОБРО ПОЖАЛОВАТЬ НА WASTELAND ★</color></size>",
            "Рады видеть вас на проекте!",
            " ",
            "<color=#a9a9a9>Используйте команды для навигации:</color>",
            " ",
            "• <color=#55ff55>/commands</color> — список всех команд сервера",
            "• <color=#55ff55>/rules</color> — правила и ограничения",
            "• <color=#55ff55>/info</color> — инфо о вайпах и соцсети",
            "• <color=#5dbcd2>/report</color> — пожаловаться на игрока",
            "• <color=#5dbcd2>/discord</color> — получить ссылку на канал Discord"
        };

        // Хук: срабатывает, когда игрок нажимает "Any Key" после загрузки
        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;

            // Небольшая задержка (0.5 сек), чтобы сообщение не потерялось при рывке камеры
            timer.Once(0.5f, () =>
            {
                if (player != null && player.IsConnected)
                {
                    foreach (var line in welcomeMessage)
                    {
                        player.ChatMessage(line);
                    }
                }
            });
        }
    }
}