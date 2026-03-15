using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("CombatLog Info", "Admin", "2.0.0")]
    [Description("Отправляет комбат-логи напрямую в Web-панель без спама")]
    class CombatLogInfo : RustPlugin
    {
        private List<object> combatBuffer = new List<object>();
        private List<int> usedHashes = new List<int>();

        // Очищаем старые хэши, чтобы плагин не жрал оперативку сервера
        void OnServerSave()
        {
            if (usedHashes.Count > 50000)
            {
                usedHashes = usedHashes.Skip(usedHashes.Count - 5000).ToList();
            }
        }

        // Ловим смерть игрока и собираем историю его выстрелов
        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            var initiator = info?.InitiatorPlayer;
            if (initiator != null && initiator.userID.IsSteamId())
            {
                GenCombatLog(initiator);
            }
            else
            {
                GenCombatLog(player);
            }
            return null;
        }

        private void GenCombatLog(BasePlayer forPlayer)
        {
            if (forPlayer.IsNpc || !forPlayer.userID.IsSteamId()) return;

            var cLog = CombatLog.Get(forPlayer.userID);
            
            // Ждем пару секунд, пока движок Rust запишет лог
            timer.Once(ConVar.Server.combatlogdelay + 1, () =>
            {
                if (forPlayer == null || cLog == null) return;
                AddEntries(forPlayer, cLog);
            });
        }

        // Кладем логи в нашу тихую корзину
        private void AddEntries(BasePlayer forPlayer, Queue<CombatLog.Event> cLog)
        {
            foreach (CombatLog.Event evt in cLog)
            {
                var hash = evt.GetHashCode();
                // Проверяем, чтобы не отправлять одни и те же пули дважды
                if (!usedHashes.Contains(hash))
                {
                    var entry = CLogEntry.from(forPlayer, evt);
                    if (entry != null)
                    {
                        usedHashes.Add(hash);
                        combatBuffer.Add(entry); // 👈 ИСПРАВЛЕНА ОШИБКА ЗДЕСЬ
                    }
                }
            }
        }

        // Скрытая команда для Node.js (отдает логи и очищает корзину)
        [ConsoleCommand("webpanel.combatlog")]
        private void CmdWebPanelCombatLog(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return; 
            
            arg.ReplyWith(JsonConvert.SerializeObject(combatBuffer));
            combatBuffer.Clear(); 
        }

        // --- Вспомогательные методы форматирования ---
        static private PInfo UintFind(ulong netId, string defaultName)
        {
            BasePlayer player = null;
            try { player = BasePlayer.activePlayerList.FirstOrDefault(x => x.net.ID.Value == netId); } catch {}
            
            string finalName = player != null ? player.displayName : (defaultName == "0" ? "Unknown" : defaultName);
            return new PInfo { Name = finalName, SteamId = player != null ? player.UserIDString : netId.ToString() };
        }

        static public void RoundOrLimitFloat(ref float value) => value = (value > 1000000) ? value % 1000000 : value;

        public class PInfo
        {
            public string Name;
            public string SteamId;
        }

        // Максимально урезанная структура только с теми данными, которые нужны сайту
        public class CLogEntry
        {
            public string AttackerSteamId;
            public string TargetSteamId;
            public string Weapon;
            public string AttackerName; 
            public string TargetName;
            public string Area;
            public float Distance;
            public float HealthOld;
            public float HealthNew;
            
            public static CLogEntry from(BasePlayer forPlayer, CombatLog.Event evt)
            {
                var pInfo = new PInfo { Name = forPlayer.displayName, SteamId = forPlayer.UserIDString };
                var attacker = evt.attacker == "you" ? pInfo : UintFind(evt.attacker_id, evt.attacker);
                var target = evt.target == "you" ? pInfo : UintFind(evt.target_id, evt.target);
                
                RoundOrLimitFloat(ref evt.health_new);
                RoundOrLimitFloat(ref evt.health_old);
                
                return new CLogEntry
                {
                    AttackerSteamId = attacker.SteamId,
                    TargetSteamId = target.SteamId,
                    AttackerName = attacker.Name,
                    TargetName = target.Name,
                    Area = HitAreaUtil.Format(evt.area).ToLower(),
                    Distance = evt.distance,
                    HealthNew = evt.health_new,
                    HealthOld = evt.health_old,
                    Weapon = evt.weapon
                };
            }
        }
    }
}