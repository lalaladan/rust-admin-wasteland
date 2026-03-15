using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("WebPanel Map", "Admin", "1.0.1")]
    [Description("Отправляет координаты игроков по тихому запросу (без спама)")]
    class WebPanelMap : RustPlugin
    {
        // Создаем скрытую команду, которую будет дергать наш Node.js
        [ConsoleCommand("webpanel.map")]
        private void MapCommand(ConsoleSystem.Arg arg)
        {
            // Отвечаем только RCON-клиентам
            if (arg.Connection != null) return; 

            if (BasePlayer.activePlayerList.Count == 0) 
            {
                arg.ReplyWith("[]");
                return;
            }

            var players = new List<object>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                players.Add(new {
                    SteamID = player.UserIDString,
                    Name = player.displayName,
                    X = Mathf.RoundToInt(player.transform.position.x),
                    Z = Mathf.RoundToInt(player.transform.position.z)
                });
            }

            // ReplyWith отправляет ответ ТОЛЬКО тому, кто запросил (нашей панели), без спама в консоль!
            arg.ReplyWith(JsonConvert.SerializeObject(players));
        }
    }
}