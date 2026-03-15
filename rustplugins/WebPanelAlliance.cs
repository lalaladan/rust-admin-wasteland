using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("WebPanel Alliance", "Admin", "1.2.0")]
    [Description("Отправляет логи авторизаций и команд для Web-панели (Тихий режим)")]
    class WebPanelAlliance : RustPlugin
    {
        // Корзина для логов
        private List<object> allianceBuffer = new List<object>();

        void OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (player == null || privilege == null) return;
            SendLog(player, "Авторизация в шкафу", privilege.transform.position);
        }

        void OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (player == null || privilege == null) return;
            SendLog(player, "Очистил шкаф", privilege.transform.position);
        }

        void OnCodeEntered(CodeLock codeLock, BasePlayer player, bool isGuest)
        {
            if (player == null || codeLock == null) return;
            SendLog(player, isGuest ? "Ввел гостевой код" : "Ввел основной код", codeLock.transform.position);
        }

        object CanAssignBed(BasePlayer player, SleepingBag bag, ulong targetPlayerId)
        {
            if (player == null || bag == null) return null;

            string assigneeName = targetPlayerId.ToString();
            var offlinePlayer = covalence.Players.FindPlayerById(targetPlayerId.ToString());
            if (offlinePlayer != null) assigneeName = offlinePlayer.Name;

            string teamStatus = " [БЕЗ КОМАНДЫ]";
            if (player.currentTeam != 0) 
            {
                var team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                if (team != null)
                {
                    if (team.members.Contains(targetPlayerId)) teamStatus = " [В ОДНОЙ КОМАНДЕ]";
                    else teamStatus = " [В РАЗНЫХ КОМАНДАХ!]";
                }
            }

            SendLog(player, $"Передал спальник: {assigneeName}{teamStatus}", bag.transform.position);
            return null; 
        }

        void OnTeamCreate(BasePlayer player)
        {
            if (player == null) return;
            SendLog(player, "Создал новую команду", player.transform.position);
        }

        void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (player == null || team == null) return;
            
            string leaderName = team.teamLeader.ToString();
            BasePlayer leader = BasePlayer.FindAwakeOrSleeping(team.teamLeader.ToString());
            if (leader != null) leaderName = leader.displayName;
            
            SendLog(player, $"Вступил в команду (Лидер: {leaderName})", player.transform.position);
        }

        void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (player == null || team == null) return;
            SendLog(player, "Покинул команду", player.transform.position);
        }

        void OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player, ulong target)
        {
            if (player == null || team == null) return;
            
            string targetName = target.ToString();
            BasePlayer targetPlayer = BasePlayer.FindAwakeOrSleeping(target.ToString());
            if (targetPlayer != null) targetName = targetPlayer.displayName;
            
            SendLog(player, $"Кикнул из команды: {targetName}", player.transform.position);
        }

        private void SendLog(BasePlayer player, string action, Vector3 pos)
        {
            var data = new {
                SteamID = player.UserIDString,
                Name = player.displayName,
                Action = action,
                Location = FormatPos(pos)
            };
            // Вместо спама в консоль кладем в корзину
            allianceBuffer.Add(data); 
        }

        private string FormatPos(Vector3 pos) => $"{Mathf.RoundToInt(pos.x)}, {Mathf.RoundToInt(pos.y)}, {Mathf.RoundToInt(pos.z)}";

        // Скрытая команда для панели
        [ConsoleCommand("webpanel.alliance")]
        private void CmdWebPanelAlliance(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return; 
            
            arg.ReplyWith(JsonConvert.SerializeObject(allianceBuffer));
            allianceBuffer.Clear(); 
        }
    }
}