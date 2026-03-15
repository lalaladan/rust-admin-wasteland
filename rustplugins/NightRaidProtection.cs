using System;
using Rust;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("NightRaidProtection", "Gemini", "1.9.0")]
    class NightRaidProtection : RustPlugin
    {
        private const int StartHour = 0;
        private const int EndHour = 8;
        private const float DamageMultiplier = 0.5f;
        private const string LayerName = "RaidProtUI_Fixed"; 

        private bool IsNightProtectionActive()
        {
            DateTime mskTime = DateTime.UtcNow.AddHours(3);
            return mskTime.Hour >= StartHour && mskTime.Hour < EndHour;
        }

        void OnServerInitialized()
        {
            timer.Every(30f, UpdateAllPlayersUI);
            UpdateAllPlayersUI();
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!IsNightProtectionActive()) return;
            if (!(entity is BuildingBlock || entity is Door || entity is SimpleBuildingBlock)) return;

            if (info != null && info.damageTypes.Has(DamageType.Explosion))
                info.damageTypes.ScaleAll(DamageMultiplier);
        }

        #region UI

        private void UpdateAllPlayersUI()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DrawProtectionUI(player);
        }

        void OnPlayerConnected(BasePlayer player) => timer.Once(3f, () => DrawProtectionUI(player));

        private void DrawProtectionUI(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, LayerName);

            bool isActive = IsNightProtectionActive();
            string message = isActive 
                ? "<color=#55ff55CC>РЕЙД-ЗАЩИТА: АКТИВНА</color>" 
                : "РЕЙД-ЗАЩИТА: 00:00 - 08:00 (МСК)";

            var container = new CuiElementContainer();

            // Подняли AnchorMin Y с 0.002 до 0.01, чтобы текст не пропадал
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" }, 
                RectTransform = { AnchorMin = "0.7 0.01", AnchorMax = "0.94 0.03" }
            }, "Hud", LayerName);

            container.Add(new CuiLabel
            {
                Text = { 
                    Text = message, 
                    FontSize = 8, 
                    Align = TextAnchor.LowerRight, 
                    Font = "robotocondensed-bold.ttf",
                    Color = "1 1 1 0.3" 
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, LayerName);

            CuiHelper.AddUi(player, container);
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, LayerName);
        }

        #endregion
    }
}