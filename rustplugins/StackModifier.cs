using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using UnityEngine;
using System.Collections;
using Facepunch;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Stack Modifier", "Mabel", "2.0.11")]
    [Description("Modify item stack sizes with full UI and permissions")]
    public class StackModifier : RustPlugin
    {
        #region Fields
        [PluginReference]
        private Plugin ImageLibrary;

        private const string UIMainPanel = "StackModifierMainUI";
        private const string UIContentPanel = "StackModifierContentUI";
        private const string AdminPerm = "stackmodifier.admin";

        private static Dictionary<string, int> _defaults = null;
        private static Dictionary<string, int> _FB = new Dictionary<string, int>();

        private class UIState
        {
            public string Category = "Resources";
            public int Page = 0;
        }
        private Dictionary<ulong, UIState> openUIs = new Dictionary<ulong, UIState>();

        private readonly HashSet<string> _exclude = new HashSet<string>
        {
            "water", "water.radioactive", "water.salt", "ammo.snowballgun",
            "motorbike", "motorbike_sidecar", "bicycle", "trike", "rowboat", "rhib",
            "parachute.deployed", "minigunammopack", "minihelicopter.repair",
            "scraptransportheli.repair", "habrepair", "submarinesolo", "submarineduo",
            "workcart", "mlrs", "snowmobile", "snowmobiletomaha", "wagon", "locomotive",
            "attackhelicopter", "tugboat", "vehicle.chassis.2mod", "vehicle.chassis.3mod",
            "vehicle.chassis.4mod", "vehicle.chassis", "vehicle.module", "weaponrack.light",
            "weaponrack.doublelight", "batteringram", "batteringram.head.repair",
            "ballista.static", "ballista.mounted", "catapult", "siegetower",
            "nucleus", "clothing.mannequin", "catapult.ammo.bee", "cannonball",
            "catapult.ammo.incendiary", "ballista.bolt.hammerhead", "dart.incapacitate",
            "ballista.bolt.incendiary", "ammo.rocket.mlrs"
        };

        private readonly Dictionary<string, string> _corrections = new Dictionary<string, string>
        {
            {"sunglasses02black", "Sunglasses Style 2"}, {"sunglasses02camo", "Sunglasses Camo"},
            {"sunglasses02red", "Sunglasses Red"}, {"sunglasses03black", "Sunglasses Style 3"},
            {"sunglasses03chrome", "Sunglasses Chrome"}, {"sunglasses03gold", "Sunglasses Gold"},
            {"twitchsunglasses", "Sunglasses Purple"}, {"hazmatsuit_scientist_peacekeeper", "Peacekeeper Scientist Suit"},
            {"skullspikes.candles", "Skull Spikes Candles"}, {"skullspikes.pumpkin", "Skull Spikes Pumpkin"},
            {"skull.trophy.jar", "Skull Trophy Jar"}, {"skull.trophy.jar2", "Skull Trophy Jar 2"},
            {"skull.trophy.table", "Skull Trophy Table"}, {"innertube.horse", "Inner Tube Horse"},
            {"innertube.unicorn", "Inner Tube Unicorn"}, {"sled.xmas", "Xmas Sled"},
            {"discofloor.largetiles", "Disco Floor Large"},
        };
        #endregion

        #region Config
        private PluginConfig _config;

        private IEnumerator CheckConfig()
        {
            Puts("Checking Configuration Settings");
            yield return CoroutineEx.waitForSeconds(0.30f);

            foreach (ItemDefinition item in ItemManager.itemList)
            {
                if (_exclude.Contains(item.shortname)) continue;

                string categoryName = item.category.ToString();
                if (!_config.StackCategoryMultipliers.ContainsKey(categoryName))
                    _config.StackCategoryMultipliers[categoryName] = 0;

                if (!_config.StackCategories.TryGetValue(categoryName, out var stackCategory))
                    _config.StackCategories[categoryName] = stackCategory = new Dictionary<string, _Items>();

                if (!stackCategory.ContainsKey(item.shortname))
                {
                    stackCategory.Add(item.shortname, new _Items
                    {
                        ShortName = item.shortname,
                        ItemId = item.itemid,
                        DisplayName = item.displayName.english,
                        Modified = item.stackable,
                    });
                }
                else
                {
                    stackCategory[item.shortname].ItemId = item.itemid;
                }

                if (_corrections.ContainsKey(item.shortname))
                    stackCategory[item.shortname].DisplayName = _corrections[item.shortname];

                if (stackCategory[item.shortname].Disable)
                {
                    item.stackable = 1;
                }
                else if (_config.StackCategoryMultipliers[categoryName] > 0 &&
                         stackCategory[item.shortname].Modified == _defaults[item.shortname])
                {
                    item.stackable *= _config.StackCategoryMultipliers[categoryName];
                }
                else if (stackCategory[item.shortname].Modified > 0 &&
                         stackCategory[item.shortname].Modified != _defaults[item.shortname])
                {
                    item.stackable = stackCategory[item.shortname].Modified;
                }

                if (item.stackable == 0)
                {
                    if (stackCategory[item.shortname].Modified <= 0)
                        stackCategory[item.shortname].Modified = _defaults[item.shortname];

                    item.stackable = _defaults[item.shortname];
                }
            }

            SaveConfig();
            Puts("Successfully updated all server stack sizes.");
            Updating = null;
            yield return null;
        }

        internal class PluginConfig : SerializableConfiguration
        {
            [JsonProperty("Disable Ammo/Fuel duplication fix (Recommended false)")]
            public bool DisableFix;

            [JsonProperty("Enable VendingMachine Ammo Fix (Recommended)")]
            public bool VendingMachineAmmoFix = true;

            [JsonProperty("Category Stack Multipliers", Order = 4)]
            public Dictionary<string, int> StackCategoryMultipliers = new Dictionary<string, int>();

            [JsonProperty("Stack Categories", Order = 5)]
            public Dictionary<string, Dictionary<string, _Items>> StackCategories = new Dictionary<string, Dictionary<string, _Items>>();
        }

        public class _Items
        {
            public string ShortName;
            public int ItemId;
            public string DisplayName;
            public int Modified;
            public bool Disable;
        }

        #region Updater
        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);
            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));
            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>().ToDictionary(prop => prop.Name, prop => ToObject(prop.Value));
                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();
                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;
            foreach (var key in currentWithDefaults.Keys)
            {
                if (currentRaw.TryGetValue(key, out var currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }
            return changed;
        }
        #endregion
        #endregion

        #region Oxide Core

        private void Init()
        {
            permission.RegisterPermission(AdminPerm, this);
        }

        protected override void LoadDefaultConfig() => _config = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
                else if (MaybeUpdateConfig(_config))
                {
                    SaveConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private Coroutine Updating = null;

        private void Unload()
        {
            if (Updating != null) ServerMgr.Instance.StopCoroutine(Updating);
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, UIMainPanel);
            }

            RestoreVanillaStackSizes();
            _defaults = null;
        }

        private void OnServerShutdown()
        {
            SaveConfig();
            _defaults = null;
        }

        private void InitializeFB()
        {
            _FB.Clear();
            foreach (ItemDefinition itemDefinition in ItemManager.GetItemDefinitions())
                _FB[itemDefinition.shortname] = itemDefinition.stackable;
        }

        private void OnServerInitialized()
        {
            LoadDefaultStackSizes();
            InitializeFB();
            
            foreach (ItemDefinition item in ItemManager.itemList)
            {
                string categoryName = item.category.ToString();
                if (!_config.StackCategoryMultipliers.ContainsKey(categoryName) || _config.StackCategoryMultipliers[categoryName] < 1)
                    _config.StackCategoryMultipliers[categoryName] = 1;
            }

            Updating = ServerMgr.Instance.StartCoroutine(CheckConfig());
            SaveDefaultStackSizes();
        }

        private void SaveDefaultStackSizes()
        {
            if (_FB == null) _FB = new Dictionary<string, int>();
            foreach (ItemDefinition itemDefinition in ItemManager.GetItemDefinitions())
            {
                if (_FB.ContainsKey(itemDefinition.shortname)) continue;
                _FB[itemDefinition.shortname] = itemDefinition.stackable;
            }
            Interface.Oxide.DataFileSystem.WriteObject("Stackmodifier_Defaults", _FB);
        }

        void LoadDefaultStackSizes()
        {
            _FB = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, int>>("Stackmodifier_Defaults");
            if (_FB == null) _FB = new Dictionary<string, int>();
            _defaults = _FB;
        }

        private void RestoreVanillaStackSizes()
        {
            if (_defaults == null || !_defaults.Any()) return;
            foreach (ItemDefinition itemDefinition in ItemManager.GetItemDefinitions())
            {
                if (!_defaults.ContainsKey(itemDefinition.shortname)) continue;
                itemDefinition.stackable = _defaults[itemDefinition.shortname];
            }
        }
        #endregion

        #region Stacking Logic (Original)
        Item OnItemSplit(Item item, int amount)
        {
            if (amount <= 0 || item.amount < amount) return null;

            if (item.skin == 2591851360 || item.skin == 2817854052 || item.skin == 2892143123 || item.skin == 2892142979 ||
                item.skin == 2892142846 || item.skin == 2817854377 || item.skin == 2817854677 || item.skin == 2888602635 ||
                item.skin == 2888602942 || item.skin == 2888603247 || item.skin == 2445048695 || item.skin == 2445033042)
                return null;

            var armorSlotComponent = item.info.GetComponent<ItemModContainerArmorSlot>();
            if (armorSlotComponent != null)
            {
                Item newArmorItem = ItemManager.CreateByItemID(item.info.itemid);
                if (newArmorItem == null) return null;

                int capacity = item.contents?.capacity ?? 0;
                armorSlotComponent.CreateAtCapacity(capacity, newArmorItem);

                if (item.contents != null && newArmorItem.contents != null)
                {
                    foreach (var nItem in item.contents.itemList)
                    {
                        Item cArmor = ItemManager.CreateByItemID(nItem.info.itemid, nItem.amount);
                        if (cArmor != null)
                        {
                            newArmorItem.contents.AddItem(cArmor.info, cArmor.amount);
                            cArmor.MarkDirty();
                        }
                    }
                }

                item.amount -= amount;
                newArmorItem.name = item.name;
                newArmorItem.skin = item.skin;
                newArmorItem.amount = amount;
                newArmorItem.MarkDirty();
                item.MarkDirty();
                return newArmorItem;
            }

            if (item.GetHeldEntity()?.GetComponentInChildren<BaseLiquidVessel>() != null)
            {
                Item liquidContainer = ItemManager.CreateByName(item.info.shortname);
                if (liquidContainer == null) return null;

                liquidContainer.amount = amount;
                item.amount -= amount;
                item.MarkDirty();

                Item water = item.contents.FindItemByItemID(-1779180711);
                if (water != null) liquidContainer.contents.AddItem(ItemManager.FindItemDefinition(-1779180711), water.amount);
                return liquidContainer;
            }

            Item newItem = ItemManager.CreateByItemID(item.info.itemid);
            if (newItem == null) return null;

            BaseProjectile.Magazine newItemMag = newItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
            if (newItem.contents?.itemList.Count == 0 && (_config.DisableFix || newItemMag?.contents == 0))
            {
                newItem.Remove();
                return null;
            }

            item.amount -= amount;
            newItem.name = item.name;
            newItem.amount = amount;
            if (item.skin != 0) newItem.skin = item.skin;
            item.MarkDirty();

            if (item.IsBlueprint()) newItem.blueprintTarget = item.blueprintTarget;

            if (newItem.contents?.itemList.Count > 0) item.contents.Clear();
            newItem.MarkDirty();

            if (_config.VendingMachineAmmoFix && item.GetRootContainer()?.entityOwner is VendingMachine) return newItem;
            if (_config.DisableFix) return newItem;

            if (newItem.GetHeldEntity() is FlameThrower flameThrower) flameThrower.ammo = 0;
            if (newItem.GetHeldEntity() is Chainsaw chainsaw) chainsaw.ammo = 0;

            BaseProjectile.Magazine itemMagDefault = newItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
            if (itemMagDefault != null && itemMagDefault.contents > 0) itemMagDefault.contents = 0;

            return newItem;
        }

        object OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            var item = card.GetItem();
            if (item == null || item.isBroken || item.amount <= 1) return null;

            int division = item.amount / 1;
            for (int i = 0; i < division; i++)
            {
                Item x = item.SplitItem(1);
                if (x != null && !x.MoveToContainer(player.inventory.containerMain, -1, false) && (item.parent == null || !x.MoveToContainer(item.parent)))
                    x.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
            }
            return null;
        }
        #endregion

        #region Full In-Game UI

        [ChatCommand("stack")]
        private void cmdStackUI(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPerm))
            {
                SendReply(player, "<color=#ff5555>У вас нет прав для использования этой команды.</color>");
                return;
            }

            if (openUIs.ContainsKey(player.userID))
            {
                CuiHelper.DestroyUi(player, UIMainPanel);
            }
            
            openUIs[player.userID] = new UIState();
            DrawBaseUI(player);
            DrawContentUI(player);
        }

        private void DrawBaseUI(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel {
                Image = { Color = "0.1 0.1 0.1 0.98" },
                RectTransform = { AnchorMin = "0.1 0.1", AnchorMax = "0.9 0.9" },
                CursorEnabled = true
            }, "Overlay", UIMainPanel);

            container.Add(new CuiLabel {
                Text = { Text = "<b>МЕНЕДЖЕР СТАКОВ (STACK MODIFIER)</b>", FontSize = 22, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
                RectTransform = { AnchorMin = "0.02 0.92", AnchorMax = "0.8 0.98" }
            }, UIMainPanel);

            container.Add(new CuiButton {
                Button = { Command = "stackui.close", Color = "0.8 0.2 0.2 1" },
                RectTransform = { AnchorMin = "0.95 0.92", AnchorMax = "0.99 0.98" },
                Text = { Text = "<b>X</b>", FontSize = 18, Align = TextAnchor.MiddleCenter }
            }, UIMainPanel);

            CuiHelper.AddUi(player, container);
        }

        private void DrawContentUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UIContentPanel);
            
            if (!openUIs.TryGetValue(player.userID, out var state)) return;

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.92" }
            }, UIMainPanel, UIContentPanel);

            DrawCategories(container, state);
            DrawItems(container, state);

            CuiHelper.AddUi(player, container);
        }

        private void DrawCategories(CuiElementContainer container, UIState state)
        {
            container.Add(new CuiPanel {
                Image = { Color = "0.15 0.15 0.15 1" },
                RectTransform = { AnchorMin = "0.01 0.02", AnchorMax = "0.20 0.98" }
            }, UIContentPanel, "CategoryPanel");

            var categories = _config.StackCategories.Keys.OrderBy(x => x).ToList();
            float height = 0.06f;
            float spacing = 0.01f;
            float startY = 0.98f;

            for (int i = 0; i < categories.Count; i++)
            {
                string cat = categories[i];
                bool isSelected = (cat == state.Category);
                string color = isSelected ? "0.4 0.6 0.2 1" : "0.25 0.25 0.25 1";

                float yMax = startY - (i * (height + spacing));
                float yMin = yMax - height;

                container.Add(new CuiButton {
                    Button = { Command = $"stackui.setcategory {cat}", Color = color },
                    RectTransform = { AnchorMin = $"0.05 {yMin}", AnchorMax = $"0.95 {yMax}" },
                    Text = { Text = cat, FontSize = 14, Align = TextAnchor.MiddleCenter }
                }, "CategoryPanel");
            }
        }

        private void DrawItems(CuiElementContainer container, UIState state)
        {
            container.Add(new CuiPanel {
                Image = { Color = "0.12 0.12 0.12 1" },
                RectTransform = { AnchorMin = "0.21 0.02", AnchorMax = "0.99 0.98" }
            }, UIContentPanel, "ItemsPanel");

            if (!_config.StackCategories.ContainsKey(state.Category)) return;

            var allItems = _config.StackCategories[state.Category].Values
                .Where(x => !x.Disable && _defaults.ContainsKey(x.ShortName))
                .OrderBy(x => x.DisplayName)
                .ToList();

            int itemsPerPage = 8;
            int totalPages = Mathf.CeilToInt(allItems.Count / (float)itemsPerPage);
            if (state.Page >= totalPages) state.Page = Math.Max(0, totalPages - 1);

            var pageItems = allItems.Skip(state.Page * itemsPerPage).Take(itemsPerPage).ToList();

            float rowHeight = 0.10f;
            float spacing = 0.015f;
            float startY = 0.96f;

            container.Add(new CuiLabel { Text = { Text = "Предмет", FontSize = 14, Color = "0.6 0.6 0.6 1", Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = "0.1 0.96", AnchorMax = "0.3 1" } }, "ItemsPanel");
            container.Add(new CuiLabel { Text = { Text = "Текущий стак", FontSize = 14, Color = "0.6 0.6 0.6 1", Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.35 0.96", AnchorMax = "0.5 1" } }, "ItemsPanel");
            container.Add(new CuiLabel { Text = { Text = "Ручной ввод (Нажмите Enter)", FontSize = 14, Color = "0.6 0.6 0.6 1", Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.52 0.96", AnchorMax = "0.72 1" } }, "ItemsPanel");
            container.Add(new CuiLabel { Text = { Text = "Быстрые действия", FontSize = 14, Color = "0.6 0.6 0.6 1", Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.75 0.96", AnchorMax = "0.98 1" } }, "ItemsPanel");

            for (int i = 0; i < pageItems.Count; i++)
            {
                var item = pageItems[i];
                float yMax = startY - 0.04f - (i * (rowHeight + spacing));
                float yMin = yMax - rowHeight;
                string rowName = $"Row_{i}";

                container.Add(new CuiPanel {
                    Image = { Color = "0.18 0.18 0.18 1" },
                    RectTransform = { AnchorMin = $"0.01 {yMin}", AnchorMax = $"0.99 {yMax}" }
                }, "ItemsPanel", rowName);

                string imageId = "0";
                if (ImageLibrary != null)
                {
                    bool hasImage = Convert.ToBoolean(ImageLibrary.Call("HasImage", item.ShortName, 0UL) ?? false);
                    if (hasImage)
                    {
                        imageId = (string)ImageLibrary.Call("GetImage", item.ShortName, 0UL) ?? "0";
                    }
                }

                if (imageId != "0" && !string.IsNullOrEmpty(imageId))
                {
                    container.Add(new CuiElement {
                        Parent = rowName,
                        Components = {
                            new CuiRawImageComponent { Png = imageId },
                            new CuiRectTransformComponent { AnchorMin = "0.01 0.1", AnchorMax = "0.08 0.9" }
                        }
                    });
                }

                container.Add(new CuiLabel {
                    Text = { Text = item.DisplayName, FontSize = 14, Align = TextAnchor.MiddleLeft },
                    RectTransform = { AnchorMin = "0.10 0", AnchorMax = "0.33 1" }
                }, rowName);

                container.Add(new CuiLabel {
                    Text = { Text = $"<color=#aaffaa>{item.Modified}</color>", FontSize = 16, Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0.35 0", AnchorMax = "0.48 1" }
                }, rowName);

                // Отрисовка подложки для поля ввода
                container.Add(new CuiPanel {
                    Image = { Color = "0.05 0.05 0.05 1" },
                    RectTransform = { AnchorMin = "0.52 0.2", AnchorMax = "0.72 0.8" }
                }, rowName, $"{rowName}_InputBg");

                // Само поле ввода (теперь показывает текущий стак по умолчанию)
                container.Add(new CuiElement {
                    Parent = $"{rowName}_InputBg",
                    Name = $"{rowName}_Input",
                    Components = {
                        new CuiInputFieldComponent { 
                            Text = item.Modified.ToString(), 
                            FontSize = 14, 
                            Align = TextAnchor.MiddleCenter, 
                            Command = $"stackui.setinput {item.ShortName}",
                            NeedsKeyboard = true
                        },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });

                CreateUIButton(container, rowName, "x2", $"stackui.setstack {item.ShortName} {item.Modified * 2}", "0.74 0.2", "0.79 0.8", "0.2 0.5 0.8 1");
                CreateUIButton(container, rowName, "x10", $"stackui.setstack {item.ShortName} {item.Modified * 10}", "0.80 0.2", "0.86 0.8", "0.2 0.5 0.8 1");
                CreateUIButton(container, rowName, "Max", $"stackui.setstack {item.ShortName} 65000", "0.87 0.2", "0.93 0.8", "0.6 0.2 0.2 1");
                
                int defStack = _defaults.ContainsKey(item.ShortName) ? _defaults[item.ShortName] : 1;
                CreateUIButton(container, rowName, "Def", $"stackui.setstack {item.ShortName} {defStack}", "0.94 0.2", "0.99 0.8", "0.4 0.4 0.4 1");
            }

            if (state.Page > 0)
            {
                CreateUIButton(container, "ItemsPanel", "< Назад", $"stackui.setpage {state.Page - 1}", "0.3 0.02", "0.45 0.08", "0.3 0.3 0.3 1");
            }
            
            container.Add(new CuiLabel {
                Text = { Text = $"Страница {state.Page + 1} из {totalPages}", FontSize = 14, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.45 0.02", AnchorMax = "0.65 0.08" }
            }, "ItemsPanel");

            if (state.Page < totalPages - 1)
            {
                CreateUIButton(container, "ItemsPanel", "Вперед >", $"stackui.setpage {state.Page + 1}", "0.65 0.02", "0.8 0.08", "0.3 0.3 0.3 1");
            }
        }

        private void CreateUIButton(CuiElementContainer container, string parent, string text, string command, string anchorMin, string anchorMax, string color)
        {
            container.Add(new CuiButton {
                Button = { Command = command, Color = color },
                RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                Text = { Text = text, FontSize = 13, Align = TextAnchor.MiddleCenter }
            }, parent);
        }

        #endregion

        #region Console Commands for UI

        [ConsoleCommand("stackui.close")]
        private void cmdCloseUI(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                CuiHelper.DestroyUi(arg.Player(), UIMainPanel);
                openUIs.Remove(arg.Player().userID);
            }
        }

        [ConsoleCommand("stackui.setcategory")]
        private void cmdSetCategory(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, AdminPerm) || arg.Args == null || arg.Args.Length < 1) return;

            if (openUIs.TryGetValue(player.userID, out var state))
            {
                state.Category = arg.Args[0];
                state.Page = 0; 
                DrawContentUI(player);
            }
        }

        [ConsoleCommand("stackui.setpage")]
        private void cmdSetPage(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, AdminPerm) || arg.Args == null || arg.Args.Length < 1) return;

            if (openUIs.TryGetValue(player.userID, out var state) && int.TryParse(arg.Args[0], out int newPage))
            {
                state.Page = newPage;
                DrawContentUI(player);
            }
        }

        [ConsoleCommand("stackui.setinput")]
        private void cmdSetInput(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, AdminPerm) || arg.Args == null || arg.Args.Length < 2) return;

            string shortname = arg.Args[0];
            if (int.TryParse(arg.Args[1], out int amount))
            {
                UpdateStackSize(shortname, amount);
                DrawContentUI(player); 
            }
            else
            {
                SendReply(player, "<color=#ff5555>Ошибка:</color> Введите корректное число.");
            }
        }

        [ConsoleCommand("stackui.setstack")]
        private void cmdSetStackConsole(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || !permission.UserHasPermission(player.UserIDString, AdminPerm) || arg.Args == null || arg.Args.Length < 2) return;

            string shortname = arg.Args[0];
            if (int.TryParse(arg.Args[1], out int amount))
            {
                UpdateStackSize(shortname, amount);
                DrawContentUI(player); 
            }
        }

        private void UpdateStackSize(string shortname, int newStack)
        {
            if (newStack <= 0) newStack = 1;

            ItemDefinition itemDef = ItemManager.FindItemDefinition(shortname);
            if (itemDef == null) return;

            string categoryName = itemDef.category.ToString();

            if (_config.StackCategories.ContainsKey(categoryName) && _config.StackCategories[categoryName].ContainsKey(shortname))
            {
                _config.StackCategories[categoryName][shortname].Modified = newStack;
            }

            itemDef.stackable = newStack;
            SaveConfig();
        }
        #endregion
    }
}