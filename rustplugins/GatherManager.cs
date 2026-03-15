using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("Gather Manager", "Mughisi", "2.2.83")]
    [Description("Increases the amount of items gained from gathering resources (UI Added, RU, Full Filter)")]
    class GatherManager : RustPlugin
    {
        #region Fields & UI State
        [PluginReference]
        private Plugin ImageLibrary;

        private const string UIMainPanel = "GatherManagerMainUI";
        private const string UIContentPanel = "GatherManagerContentUI";
        private const string AdminPerm = "gathermanager.admin";

        private class UIState
        {
            public string Category = "Dispenser";
            public int Page = 0;
        }
        private Dictionary<ulong, UIState> openUIs = new Dictionary<ulong, UIState>();
        private readonly string[] uiCategories = { "Dispenser", "Pickup", "Quarry", "Excavator", "Survey" };

        private readonly Dictionary<string, string> categoryNamesRU = new Dictionary<string, string>
        {
            { "Dispenser", "Добыча (Инструментом)" },
            { "Pickup", "Подбор (С земли)" },
            { "Quarry", "Карьеры" },
            { "Excavator", "Экскаватор" },
            { "Survey", "Гео-заряды" }
        };
        #endregion

        #region Configuration Data
        private bool configChanged;

        private const string DefaultChatPrefix = "Gather Manager";
        private const string DefaultChatPrefixColor = "#008000ff";

        public string ChatPrefix { get; private set; }
        public string ChatPrefixColor { get; private set; }

        private static readonly Dictionary<string, object> DefaultGatherResourceModifiers = new Dictionary<string, object>();
        private static readonly Dictionary<string, object> DefaultGatherDispenserModifiers = new Dictionary<string, object>();
        private static readonly Dictionary<string, object> DefaultQuarryResourceModifiers = new Dictionary<string, object>();
        private static readonly Dictionary<string, object> DefaultPickupResourceModifiers = new Dictionary<string, object>();
        private static readonly Dictionary<string, object> DefaultSurveyResourceModifiers = new Dictionary<string, object>();

        private const float DefaultMiningQuarryResourceTickRate = 5f;
        private const float DefaultExcavatorResourceTickRate = 3f;
        private const float DefaultExcavatorTimeForFullResources = 120f;
        private const float DefaultExcavatorBeltSpeedMax = 0.1f;

        public Dictionary<string, float> GatherResourceModifiers { get; private set; }
        public Dictionary<string, float> GatherDispenserModifiers { get; private set; }
        public Dictionary<string, float> QuarryResourceModifiers { get; private set; }
        public Dictionary<string, float> ExcavatorResourceModifiers { get; private set; }
        public Dictionary<string, float> PickupResourceModifiers { get; private set; }
        public Dictionary<string, float> SurveyResourceModifiers { get; private set; }
        public float MiningQuarryResourceTickRate { get; private set; }
        public float ExcavatorResourceTickRate { get; private set; }
        public float ExcavatorTimeForFullResources { get; private set; }
        public float ExcavatorBeltSpeedMax { get; private set; }

        private const string DefaultNotAllowed = "У вас нет прав для использования этой команды.";
        private const string DefaultInvalidArgumentsGather = "Неверные аргументы! Используйте gather.rate <type:dispenser|pickup|quarry|excavator|survey> <ресурс> <множитель>";
        private const string DefaultInvalidArgumentsDispenser = "Неверные аргументы! Используйте dispenser.scale <dispenser:tree|ore|corpse> <множитель>";
        private const string DefaultInvalidArgumentsSpeed = "Неверные аргументы! Используйте quarry.rate <время между добычей в секундах>";
        private const string DefaultInvalidModifier = "Неверный множитель! Новый множитель всегда должен быть больше 0!";
        private const string DefaultInvalidSpeed = "Нельзя установить скорость ниже 1 секунды!";
        private const string DefaultModifyResource = "Вы установили рейт для {0} на x{1} из {2}.";
        private const string DefaultModifyResourceRemove = "Вы сбросили рейт для {0} из {1}.";
        private const string DefaultModifySpeed = "Карьер теперь будет добывать ресурсы каждые {0} секунд.";
        private const string DefaultInvalidResource = "{0} - недействительный ресурс. Введите gather.resources для списка.";
        private const string DefaultModifyDispenser = "Вы установили количество ресурсов для {0} на x{1}";
        private const string DefaultInvalidDispenser = "{0} - недействительный источник. Введите gather.dispensers для списка.";

        private const string DefaultHelpText = "/gather - Показывает подробную информацию о рейтах.";
        private const string DefaultHelpTextPlayer = "Рейты на сервере изменены на следующие:";
        private const string DefaultHelpTextAdmin = "Для изменения рейтов используйте команду:\r\ngather.rate <type:dispenser|pickup|quarry|survey> <ресурс> <множитель>\r\nИзменить количество ресурсов в источнике:\r\ndispenser.scale <dispenser:tree|ore|corpse> <множитель>\r\nВремя цикла карьера:\r\nquarry.tickrate <секунды>";
        private const string DefaultHelpTextPlayerGains = "Ресурсы из {0}:";
        private const string DefaultHelpTextPlayerMiningQuarrySpeed = "Время между циклами карьера: {0} сек.";
        private const string DefaultHelpTextPlayerDefault = "Стандартные значения.";
        private const string DefaultDispensers = "Источников (деревья/руда)";
        private const string DefaultCharges = "Гео-зарядов";
        private const string DefaultQuarries = "Карьеров";
        private const string DefaultExcavators = "Экскаваторов";
        private const string DefaultPickups = "Подбора с земли";

        public string NotAllowed { get; private set; }
        public string InvalidArgumentsGather { get; private set; }
        public string InvalidArgumentsDispenser { get; private set; }
        public string InvalidArgumentsSpeed { get; private set; }
        public string InvalidModifier { get; private set; }
        public string InvalidSpeed { get; private set; }
        public string ModifyResource { get; private set; }
        public string ModifyResourceRemove { get; private set; }
        public string ModifySpeed { get; private set; }
        public string InvalidResource { get; private set; }
        public string ModifyDispenser { get; private set; }
        public string InvalidDispenser { get; private set; }
        public string HelpText { get; private set; }
        public string HelpTextPlayer { get; private set; }
        public string HelpTextAdmin { get; private set; }
        public string HelpTextPlayerGains { get; private set; }
        public string HelpTextPlayerDefault { get; private set; }
        public string HelpTextPlayerMiningQuarrySpeed { get; private set; }
        public string Dispensers { get; private set; }
        public string Charges { get; private set; }
        public string Quarries { get; private set; }
        public string Excavators { get; private set; }
        public string Pickups { get; private set; }
        #endregion

        private readonly List<string> subcommands = new List<string>() { "dispenser", "pickup", "quarry", "excavator", "survey" };
        private readonly Hash<string, ItemDefinition> validResources = new Hash<string, ItemDefinition>();
        private readonly Hash<string, ResourceDispenser.GatherType> validDispensers = new Hash<string, ResourceDispenser.GatherType>();

        private void Init()
        {
            permission.RegisterPermission(AdminPerm, this);
            LoadConfigValues();
        }

        private void OnServerInitialized()
        {
            var resourceDefinitions = ItemManager.itemList;
            foreach (var def in resourceDefinitions.Where(def => def.category == ItemCategory.Food || def.category == ItemCategory.Resources))
                validResources.Add(def.displayName.english.ToLower(), def);

            validDispensers.Add("tree", ResourceDispenser.GatherType.Tree);
            validDispensers.Add("ore", ResourceDispenser.GatherType.Ore);
            validDispensers.Add("corpse", ResourceDispenser.GatherType.Flesh);
            validDispensers.Add("flesh", ResourceDispenser.GatherType.Flesh);

            foreach (var excavator in UnityEngine.Object.FindObjectsOfType<ExcavatorArm>())
            {
                if (ExcavatorResourceTickRate != DefaultMiningQuarryResourceTickRate)
                {
                    excavator.CancelInvoke("ProcessResources");
                    excavator.InvokeRepeating("ProcessResources", ExcavatorResourceTickRate, ExcavatorResourceTickRate);
                }
                if (ExcavatorBeltSpeedMax != DefaultExcavatorBeltSpeedMax)
                {
                    excavator.beltSpeedMax = ExcavatorBeltSpeedMax;
                }
                if (ExcavatorTimeForFullResources != DefaultExcavatorTimeForFullResources)
                {
                    excavator.timeForFullResources = ExcavatorTimeForFullResources;
                }
            }
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, UIMainPanel);
            }

            foreach (var excavator in UnityEngine.Object.FindObjectsOfType<ExcavatorArm>())
            {
                if (ExcavatorResourceTickRate != DefaultMiningQuarryResourceTickRate)
                {
                    excavator.CancelInvoke("ProcessResources");
                    excavator.InvokeRepeating("ProcessResources", DefaultMiningQuarryResourceTickRate, DefaultMiningQuarryResourceTickRate);
                }
                if (ExcavatorBeltSpeedMax != DefaultExcavatorBeltSpeedMax)
                {
                    excavator.beltSpeedMax = DefaultExcavatorBeltSpeedMax;
                }
                if (ExcavatorTimeForFullResources != DefaultExcavatorTimeForFullResources)
                {
                    excavator.timeForFullResources = DefaultExcavatorTimeForFullResources;
                }
            }
        }

        protected override void LoadDefaultConfig() => PrintWarning("Создан новый конфигурационный файл.");

        #region Original Commands
        [ChatCommand("gather")]
        private void Gather(BasePlayer player, string command, string[] args)
        {
            var help = HelpTextPlayer;
            if (GatherResourceModifiers.Count == 0 && SurveyResourceModifiers.Count == 0 && PickupResourceModifiers.Count == 0 && QuarryResourceModifiers.Count == 0)
                help += HelpTextPlayerDefault;
            else
            {
                if (GatherResourceModifiers.Count > 0)
                {
                    var dispensers = string.Format(HelpTextPlayerGains, Dispensers);
                    dispensers = GatherResourceModifiers.Aggregate(dispensers, (current, entry) => current + ("\r\n    " + entry.Key + ": x" + entry.Value));
                    help += "\r\n" + dispensers;
                }
                if (PickupResourceModifiers.Count > 0)
                {
                    var pickups = string.Format(HelpTextPlayerGains, Pickups);
                    pickups = PickupResourceModifiers.Aggregate(pickups, (current, entry) => current + ("\r\n    " + entry.Key + ": x" + entry.Value));
                    help += "\r\n" + pickups;
                }
                if (QuarryResourceModifiers.Count > 0)
                {
                    var quarries = string.Format(HelpTextPlayerGains, Quarries);
                    quarries = QuarryResourceModifiers.Aggregate(quarries, (current, entry) => current + ("\r\n    " + entry.Key + ": x" + entry.Value));
                    help += "\r\n" + quarries;
                }
                if (ExcavatorResourceModifiers.Count > 0)
                {
                    var excavators = string.Format(HelpTextPlayerGains, Excavators);
                    excavators = ExcavatorResourceModifiers.Aggregate(excavators, (current, entry) => current + ("\r\n    " + entry.Key + ": x" + entry.Value));
                    help += "\r\n" + excavators;
                }
                if (SurveyResourceModifiers.Count > 0)
                {
                    var charges = string.Format(HelpTextPlayerGains, Charges);
                    charges = SurveyResourceModifiers.Aggregate(charges, (current, entry) => current + ("\r\n    " + entry.Key + ": x" + entry.Value));
                    help += "\r\n" + charges;
                }
            }

            if (MiningQuarryResourceTickRate != DefaultMiningQuarryResourceTickRate)
                help += "\r\n" + string.Format(HelpTextPlayerMiningQuarrySpeed, MiningQuarryResourceTickRate);

            SendMessage(player, help);
            if (!player.IsAdmin) return;
            SendMessage(player, HelpTextAdmin);
        }

        private void SendHelpText(BasePlayer player) => SendMessage(player, HelpText);

        [ConsoleCommand("gather.rate")]
        private void GatherRate(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin && !permission.UserHasPermission(arg.Player().UserIDString, AdminPerm))
            {
                arg.ReplyWith(NotAllowed);
                return;
            }

            var subcommand = arg.GetString(0).ToLower();
            if (!arg.HasArgs(3) || !subcommands.Contains(subcommand))
            {
                arg.ReplyWith(InvalidArgumentsGather);
                return;
            }

            if (!validResources.ContainsKey(arg.GetString(1).ToLower()) && arg.GetString(1) != "*")
            {
                arg.ReplyWith(string.Format(InvalidResource, arg.GetString(1)));
                return;
            }

            var resource = arg.GetString(1) == "*" ? "*" : validResources[arg.GetString(1).ToLower()].displayName.english;
            var modifier = arg.GetFloat(2, -1);
            var remove = false;
            if (modifier < 0)
            {
                if (arg.GetString(2).ToLower() == "remove")
                    remove = true;
                else
                {
                    arg.ReplyWith(InvalidModifier);
                    return;
                }
            }

            switch (subcommand)
            {
                case "dispenser":
                    if (remove) { GatherResourceModifiers.Remove(resource); arg.ReplyWith(string.Format(ModifyResourceRemove, resource, Dispensers)); }
                    else { GatherResourceModifiers[resource] = modifier; arg.ReplyWith(string.Format(ModifyResource, resource, modifier, Dispensers)); }
                    SetConfigValue("Options", "GatherResourceModifiers", GatherResourceModifiers);
                    break;
                case "pickup":
                    if (remove) { PickupResourceModifiers.Remove(resource); arg.ReplyWith(string.Format(ModifyResourceRemove, resource, Pickups)); }
                    else { PickupResourceModifiers[resource] = modifier; arg.ReplyWith(string.Format(ModifyResource, resource, modifier, Pickups)); }
                    SetConfigValue("Options", "PickupResourceModifiers", PickupResourceModifiers);
                    break;
                case "quarry":
                    if (remove) { QuarryResourceModifiers.Remove(resource); arg.ReplyWith(string.Format(ModifyResourceRemove, resource, Quarries)); }
                    else { QuarryResourceModifiers[resource] = modifier; arg.ReplyWith(string.Format(ModifyResource, resource, modifier, Quarries)); }
                    SetConfigValue("Options", "QuarryResourceModifiers", QuarryResourceModifiers);
                    break;
                case "excavator":
                    if (remove) { ExcavatorResourceModifiers.Remove(resource); arg.ReplyWith(string.Format(ModifyResourceRemove, resource, Excavators)); }
                    else { ExcavatorResourceModifiers[resource] = modifier; arg.ReplyWith(string.Format(ModifyResource, resource, modifier, Excavators)); }
                    SetConfigValue("Options", "ExcavatorResourceModifiers", ExcavatorResourceModifiers);
                    break;
                case "survey":
                    if (remove) { SurveyResourceModifiers.Remove(resource); arg.ReplyWith(string.Format(ModifyResourceRemove, resource, Charges)); }
                    else { SurveyResourceModifiers[resource] = modifier; arg.ReplyWith(string.Format(ModifyResource, resource, modifier, Charges)); }
                    SetConfigValue("Options", "SurveyResourceModifiers", SurveyResourceModifiers);
                    break;
            }
        }

        [ConsoleCommand("gather.resources")]
        private void GatherResources(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin) { arg.ReplyWith(NotAllowed); return; }
            arg.ReplyWith(validResources.Aggregate("Доступные ресурсы:\r\n", (current, resource) => current + (resource.Value.displayName.english + "\r\n")) + "* (Для всех ресурсов, которые не настроены отдельно)");
        }

        [ConsoleCommand("gather.dispensers")]
        private void GatherDispensers(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin) { arg.ReplyWith(NotAllowed); return; }
            arg.ReplyWith(validDispensers.Aggregate("Доступные источники:\r\n", (current, dispenser) => current + (dispenser.Value.ToString("G") + "\r\n")));
        }

        [ConsoleCommand("dispenser.scale")]
        private void DispenserRate(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin && !permission.UserHasPermission(arg.Player().UserIDString, AdminPerm)) { arg.ReplyWith(NotAllowed); return; }
            if (!arg.HasArgs(2)) { arg.ReplyWith(InvalidArgumentsDispenser); return; }
            if (!validDispensers.ContainsKey(arg.GetString(0).ToLower())) { arg.ReplyWith(string.Format(InvalidDispenser, arg.GetString(0))); return; }
            var dispenser = validDispensers[arg.GetString(0).ToLower()].ToString("G");
            var modifier = arg.GetFloat(1, -1);
            if (modifier < 0) { arg.ReplyWith(InvalidModifier); return; }
            GatherDispenserModifiers[dispenser] = modifier;
            SetConfigValue("Options", "GatherDispenserModifiers", GatherDispenserModifiers);
            arg.ReplyWith(string.Format(ModifyDispenser, dispenser, modifier));
        }

        [ConsoleCommand("quarry.tickrate")]
        private void MiningQuarryTickRate(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin) { arg.ReplyWith(NotAllowed); return; }
            if (!arg.HasArgs()) { arg.ReplyWith(InvalidArgumentsSpeed); return; }
            var modifier = arg.GetFloat(0, -1);
            if (modifier < 1) { arg.ReplyWith(InvalidSpeed); return; }
            MiningQuarryResourceTickRate = modifier;
            SetConfigValue("Options", "MiningQuarryResourceTickRate", MiningQuarryResourceTickRate);
            arg.ReplyWith(string.Format(ModifySpeed, modifier));
            var quarries = UnityEngine.Object.FindObjectsOfType<MiningQuarry>();
            foreach (var quarry in quarries.Where(quarry => quarry.IsOn()))
            {
                quarry.CancelInvoke("ProcessResources");
                quarry.InvokeRepeating("ProcessResources", MiningQuarryResourceTickRate, MiningQuarryResourceTickRate);
            }
        }

        [ConsoleCommand("excavator.tickrate")]
        private void ExcavatorTickRate(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin) { arg.ReplyWith(NotAllowed); return; }
            if (!arg.HasArgs()) { arg.ReplyWith(InvalidArgumentsSpeed); return; }
            var modifier = arg.GetFloat(0, -1);
            if (modifier < 1) { arg.ReplyWith(InvalidSpeed); return; }
            ExcavatorResourceTickRate = modifier;
            SetConfigValue("Options", "ExcavatorResourceTickRate", ExcavatorResourceTickRate);
            arg.ReplyWith(string.Format(ModifySpeed, modifier));
            var excavators = UnityEngine.Object.FindObjectsOfType<MiningQuarry>();
            foreach (var excavator in excavators.Where(excavator => excavator.IsOn()))
            {
                excavator.CancelInvoke("ProcessResources");
                excavator.InvokeRepeating("ProcessResources", ExcavatorResourceTickRate, ExcavatorResourceTickRate);
            }
        }
        #endregion

        #region Gathering Logic
        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (!entity.ToPlayer()) return;
            var gatherType = dispenser.gatherType.ToString("G");
            var amount = item.amount;
            float modifier;
            if (GatherResourceModifiers.TryGetValue(item.info.displayName.english, out modifier)) item.amount = (int)(item.amount * modifier);
            else if (GatherResourceModifiers.TryGetValue("*", out modifier)) item.amount = (int)(item.amount * modifier);
            if (!GatherResourceModifiers.ContainsKey(gatherType)) return;
            var dispenserModifier = GatherDispenserModifiers[gatherType];
            try
            {
                dispenser.containedItems.Single(x => x.itemid == item.info.itemid).amount += amount - item.amount / dispenserModifier;
                if (dispenser.containedItems.Single(x => x.itemid == item.info.itemid).amount < 0)
                {
                    item.amount += (int)dispenser.containedItems.Single(x => x.itemid == item.info.itemid).amount;
                }
            }
            catch { }
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item) => OnDispenserGather(dispenser, entity, item);

        private void OnGrowableGathered(GrowableEntity growable, Item item, BasePlayer player)
        {
            float modifier;
            if (GatherResourceModifiers.TryGetValue(item.info.displayName.english, out modifier)) item.amount = (int)(item.amount * modifier);
            else if (GatherResourceModifiers.TryGetValue("*", out modifier)) item.amount = (int)(item.amount * modifier);
        }

        private void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            float modifier;
            if (QuarryResourceModifiers.TryGetValue(item.info.displayName.english, out modifier)) item.amount = (int)(item.amount * modifier);
            else if (QuarryResourceModifiers.TryGetValue("*", out modifier)) item.amount = (int)(item.amount * modifier);
        }

        private void OnExcavatorGather(ExcavatorArm excavator, Item item)
        {
            float modifier;
            if (ExcavatorResourceModifiers.TryGetValue(item.info.displayName.english, out modifier)) item.amount = (int)(item.amount * modifier);
            else if (ExcavatorResourceModifiers.TryGetValue("*", out modifier)) item.amount = (int)(item.amount * modifier);
        }

        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            foreach (ItemAmount item in collectible.itemList)
            {
                float modifier;
                if (PickupResourceModifiers.TryGetValue(item.itemDef.displayName.english, out modifier)) item.amount = (int)(item.amount * modifier);
                else if (PickupResourceModifiers.TryGetValue("*", out modifier)) item.amount = (int)(item.amount * modifier);
            }
        }

        private void OnSurveyGather(SurveyCharge surveyCharge, Item item)
        {
            float modifier;
            if (SurveyResourceModifiers.TryGetValue(item.info.displayName.english, out modifier)) item.amount = (int)(item.amount * modifier);
            else if (SurveyResourceModifiers.TryGetValue("*", out modifier)) item.amount = (int)(item.amount * modifier);
        }

        private void OnMiningQuarryEnabled(MiningQuarry quarry)
        {
            if (MiningQuarryResourceTickRate == DefaultMiningQuarryResourceTickRate) return;
            quarry.CancelInvoke("ProcessResources");
            quarry.InvokeRepeating("ProcessResources", MiningQuarryResourceTickRate, MiningQuarryResourceTickRate);
        }
        #endregion

        #region Config Logic
        private void LoadConfigValues()
        {
            ChatPrefix = GetConfigValue("Settings", "ChatPrefix", DefaultChatPrefix);
            ChatPrefixColor = GetConfigValue("Settings", "ChatPrefixColor", DefaultChatPrefixColor);

            var gatherResourceModifiers = GetConfigValue("Options", "GatherResourceModifiers", DefaultGatherResourceModifiers);
            var gatherDispenserModifiers = GetConfigValue("Options", "GatherDispenserModifiers", DefaultGatherDispenserModifiers);
            var quarryResourceModifiers = GetConfigValue("Options", "QuarryResourceModifiers", DefaultQuarryResourceModifiers);
            var excavatorResourceModifiers = GetConfigValue("Options", "ExcavatorResourceModifiers", quarryResourceModifiers);
            var pickupResourceModifiers = GetConfigValue("Options", "PickupResourceModifiers", DefaultPickupResourceModifiers);
            var surveyResourceModifiers = GetConfigValue("Options", "SurveyResourceModifiers", DefaultSurveyResourceModifiers);

            MiningQuarryResourceTickRate = GetConfigValue("Options", "MiningQuarryResourceTickRate", DefaultMiningQuarryResourceTickRate);
            ExcavatorResourceTickRate = GetConfigValue("Options", "ExcavatorResourceTickRate", DefaultExcavatorResourceTickRate);
            ExcavatorBeltSpeedMax = GetConfigValue("Options", "ExcavatorBeltSpeedMax", DefaultExcavatorBeltSpeedMax);
            ExcavatorTimeForFullResources = GetConfigValue("Options", "ExcavatorTimeForFullResources", DefaultExcavatorTimeForFullResources);

            GatherResourceModifiers = ParseDict(gatherResourceModifiers);
            GatherDispenserModifiers = ParseDict(gatherDispenserModifiers);
            QuarryResourceModifiers = ParseDict(quarryResourceModifiers);
            ExcavatorResourceModifiers = ParseDict(excavatorResourceModifiers);
            PickupResourceModifiers = ParseDict(pickupResourceModifiers);
            SurveyResourceModifiers = ParseDict(surveyResourceModifiers);

            NotAllowed = GetConfigValue("Messages", "NotAllowed", DefaultNotAllowed);
            InvalidArgumentsGather = GetConfigValue("Messages", "InvalidArgumentsGather", DefaultInvalidArgumentsGather);
            InvalidArgumentsDispenser = GetConfigValue("Messages", "InvalidArgumentsDispenserType", DefaultInvalidArgumentsDispenser);
            InvalidArgumentsSpeed = GetConfigValue("Messages", "InvalidArgumentsMiningQuarrySpeed", DefaultInvalidArgumentsSpeed);
            InvalidModifier = GetConfigValue("Messages", "InvalidModifier", DefaultInvalidModifier);
            InvalidSpeed = GetConfigValue("Messages", "InvalidMiningQuarrySpeed", DefaultInvalidSpeed);
            ModifyResource = GetConfigValue("Messages", "ModifyResource", DefaultModifyResource);
            ModifyResourceRemove = GetConfigValue("Messages", "ModifyResourceRemove", DefaultModifyResourceRemove);
            ModifySpeed = GetConfigValue("Messages", "ModifyMiningQuarrySpeed", DefaultModifySpeed);
            InvalidResource = GetConfigValue("Messages", "InvalidResource", DefaultInvalidResource);
            ModifyDispenser = GetConfigValue("Messages", "ModifyDispenser", DefaultModifyDispenser);
            InvalidDispenser = GetConfigValue("Messages", "InvalidDispenser", DefaultInvalidDispenser);
            HelpText = GetConfigValue("Messages", "HelpText", DefaultHelpText);
            HelpTextAdmin = GetConfigValue("Messages", "HelpTextAdmin", DefaultHelpTextAdmin);
            HelpTextPlayer = GetConfigValue("Messages", "HelpTextPlayer", DefaultHelpTextPlayer);
            HelpTextPlayerGains = GetConfigValue("Messages", "HelpTextPlayerGains", DefaultHelpTextPlayerGains);
            HelpTextPlayerDefault = GetConfigValue("Messages", "HelpTextPlayerDefault", DefaultHelpTextPlayerDefault);
            HelpTextPlayerMiningQuarrySpeed = GetConfigValue("Messages", "HelpTextMiningQuarrySpeed", DefaultHelpTextPlayerMiningQuarrySpeed);
            Dispensers = GetConfigValue("Messages", "Dispensers", DefaultDispensers);
            Quarries = GetConfigValue("Messages", "MiningQuarries", DefaultQuarries);
            Excavators = GetConfigValue("Messages", "Excavators", DefaultExcavators);
            Charges = GetConfigValue("Messages", "SurveyCharges", DefaultCharges);
            Pickups = GetConfigValue("Messages", "Pickups", DefaultPickups);

            if (!configChanged) return;
            PrintWarning("Конфигурационный файл обновлен.");
            SaveConfig();
        }

        private Dictionary<string, float> ParseDict(Dictionary<string, object> dict)
        {
            var res = new Dictionary<string, float>();
            foreach (var entry in dict)
            {
                if (float.TryParse(entry.Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float rate))
                    res.Add(entry.Key, rate);
            }
            return res;
        }

        private T GetConfigValue<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                configChanged = true;
            }
            if (data.TryGetValue(setting, out value)) return (T)Convert.ChangeType(value, typeof(T));
            value = defaultValue;
            data[setting] = value;
            configChanged = true;
            return (T)Convert.ChangeType(value, typeof(T));
        }

        private void SetConfigValue<T>(string category, string setting, T newValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            if (data != null && data.ContainsKey(setting))
            {
                data[setting] = newValue;
                configChanged = true;
            }
            SaveConfig();
        }

        private void SendMessage(BasePlayer player, string message, params object[] args) => player?.SendConsoleCommand("chat.add", 0, -1, string.Format($"<color={ChatPrefixColor}>{ChatPrefix}</color>: {message}", args), 1.0);
        #endregion

        #region Full In-Game UI
        [ChatCommand("rateui")]
        private void cmdRateUI(BasePlayer player, string command, string[] args) => cmdGatherUI(player, command, args);

        [ChatCommand("gatherui")]
        private void cmdGatherUI(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, AdminPerm))
            {
                SendReply(player, "<color=#ff5555>У вас нет прав для использования этой команды.</color>");
                return;
            }

            if (openUIs.ContainsKey(player.userID))
                CuiHelper.DestroyUi(player, UIMainPanel);
            
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
                Text = { Text = "<b>МЕНЕДЖЕР РЕЙТОВ (GATHER MANAGER)</b>", FontSize = 22, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
                RectTransform = { AnchorMin = "0.02 0.92", AnchorMax = "0.8 0.98" }
            }, UIMainPanel);

            container.Add(new CuiButton {
                Button = { Command = "gatherui.close", Color = "0.8 0.2 0.2 1" },
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
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.92 0.92" } 
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

            float height = 0.08f;
            float spacing = 0.01f;
            float startY = 0.98f;

            for (int i = 0; i < uiCategories.Length; i++)
            {
                string cat = uiCategories[i];
                bool isSelected = (cat == state.Category);
                string color = isSelected ? "0.4 0.6 0.2 1" : "0.25 0.25 0.25 1";
                string catNameDisplay = categoryNamesRU.ContainsKey(cat) ? categoryNamesRU[cat] : cat;

                float yMax = startY - (i * (height + spacing));
                float yMin = yMax - height;

                container.Add(new CuiButton {
                    Button = { Command = $"gatherui.setcategory {cat}", Color = color },
                    RectTransform = { AnchorMin = $"0.05 {yMin}", AnchorMax = $"0.95 {yMax}" },
                    Text = { Text = catNameDisplay, FontSize = 13, Align = TextAnchor.MiddleCenter }
                }, "CategoryPanel");
            }
        }

        private void DrawItems(CuiElementContainer container, UIState state)
        {
            container.Add(new CuiPanel {
                Image = { Color = "0.12 0.12 0.12 1" },
                RectTransform = { AnchorMin = "0.21 0.02", AnchorMax = "0.99 0.98" }
            }, UIContentPanel, "ItemsPanel");

            var allItems = validResources.Values.ToList();

            // --- ПОЛНАЯ ФИЛЬТРАЦИЯ ПО КАТЕГОРИЯМ ---
            if (state.Category == "Quarry" || state.Category == "Excavator" || state.Category == "Survey")
            {
                var allowedMining = new HashSet<string> { "stones", "metal.ore", "sulfur.ore", "hq.metal.ore", "crude.oil" };
                allItems = allItems.Where(x => allowedMining.Contains(x.shortname)).ToList();
            }
            else if (state.Category == "Pickup")
            {
                var allowedPickup = new HashSet<string> { 
                    "wood", "stones", "metal.ore", "sulfur.ore", 
                    "cloth", "mushroom", "pumpkin", "corn", "potato", 
                    "red.berry", "blue.berry", "yellow.berry", "white.berry", "green.berry", "black.berry",
                    "seed.hemp", "seed.pumpkin", "seed.corn", "seed.potato",
                    "bone.fragments", "horse.dung", "scrap"
                };
                allItems = allItems.Where(x => allowedPickup.Contains(x.shortname)).ToList();
            }
            else if (state.Category == "Dispenser")
            {
                var allowedDispenser = new HashSet<string> { 
                    "wood", "stones", "metal.ore", "sulfur.ore", "hq.metal.ore",
                    "fat.animal", "bone.fragments", "cloth", "leather", 
                    "meat.boar", "meat.bear", "meat.wolf", "meat.deer", "meat.pork", "humanmeat.raw", "chicken.raw", "horsemeat.raw",
                    "skull.human", "skull.wolf"
                };
                allItems = allItems.Where(x => allowedDispenser.Contains(x.shortname)).ToList();
            }

            allItems = allItems.OrderBy(x => x.displayName.english).ToList();
            
            var globalItem = new ItemDefinition();
            globalItem.shortname = "*";
            globalItem.displayName = new Translate.Phrase { english = "ВСЕ ПРЕДМЕТЫ (*)" };
            allItems.Insert(0, globalItem);

            int itemsPerPage = 8;
            int totalPages = Mathf.CeilToInt(allItems.Count / (float)itemsPerPage);
            if (state.Page >= totalPages) state.Page = Math.Max(0, totalPages - 1);

            var pageItems = allItems.Skip(state.Page * itemsPerPage).Take(itemsPerPage).ToList();

            float rowHeight = 0.10f;
            float spacing = 0.015f;
            float startY = 0.96f;

            string catNameDisplay = categoryNamesRU.ContainsKey(state.Category) ? categoryNamesRU[state.Category] : state.Category;

            container.Add(new CuiLabel { Text = { Text = "Предмет", FontSize = 14, Color = "0.6 0.6 0.6 1", Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = "0.1 0.96", AnchorMax = "0.3 1" } }, "ItemsPanel");
            container.Add(new CuiLabel { Text = { Text = $"Рейт: <color=#aaffaa>{catNameDisplay}</color>", FontSize = 13, Color = "0.8 0.8 0.8 1", Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.33 0.96", AnchorMax = "0.5 1" } }, "ItemsPanel");
            container.Add(new CuiLabel { Text = { Text = "Ввод множителя (Enter)", FontSize = 14, Color = "0.6 0.6 0.6 1", Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.52 0.96", AnchorMax = "0.72 1" } }, "ItemsPanel");
            container.Add(new CuiLabel { Text = { Text = "Быстрые действия", FontSize = 14, Color = "0.6 0.6 0.6 1", Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.75 0.96", AnchorMax = "0.98 1" } }, "ItemsPanel");

            for (int i = 0; i < pageItems.Count; i++)
            {
                var itemDef = pageItems[i];
                string resourceName = itemDef.shortname == "*" ? "*" : itemDef.displayName.english;
                float currentMod = GetCurrentModifier(state.Category, resourceName);

                float yMax = startY - 0.04f - (i * (rowHeight + spacing));
                float yMin = yMax - rowHeight;
                string rowName = $"Row_{i}";

                string bgColor = itemDef.shortname == "*" ? "0.25 0.20 0.15 1" : "0.18 0.18 0.18 1";

                container.Add(new CuiPanel {
                    Image = { Color = bgColor },
                    RectTransform = { AnchorMin = $"0.01 {yMin}", AnchorMax = $"0.99 {yMax}" }
                }, "ItemsPanel", rowName);

                string imageId = "0";
                if (itemDef.shortname != "*" && ImageLibrary != null)
                {
                    bool hasImage = Convert.ToBoolean(ImageLibrary.Call("HasImage", itemDef.shortname, 0UL) ?? false);
                    if (hasImage) imageId = (string)ImageLibrary.Call("GetImage", itemDef.shortname, 0UL) ?? "0";
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
                else if (itemDef.shortname == "*")
                {
                     container.Add(new CuiLabel {
                        Text = { Text = "<b>*</b>", FontSize = 28, Color = "0.8 0.6 0.2 1", Align = TextAnchor.MiddleCenter },
                        RectTransform = { AnchorMin = "0.01 0.1", AnchorMax = "0.08 0.9" }
                    }, rowName);
                }

                container.Add(new CuiLabel {
                    Text = { Text = itemDef.displayName.english, FontSize = 14, Align = TextAnchor.MiddleLeft },
                    RectTransform = { AnchorMin = "0.10 0", AnchorMax = "0.33 1" }
                }, rowName);

                container.Add(new CuiLabel {
                    Text = { Text = $"<color=#aaffaa>x{currentMod.ToString("0.##", CultureInfo.InvariantCulture)}</color>", FontSize = 18, Align = TextAnchor.MiddleCenter },
                    RectTransform = { AnchorMin = "0.35 0", AnchorMax = "0.48 1" }
                }, rowName);

                container.Add(new CuiPanel {
                    Image = { Color = "0.05 0.05 0.05 1" },
                    RectTransform = { AnchorMin = "0.52 0.2", AnchorMax = "0.72 0.8" }
                }, rowName, $"{rowName}_InputBg");

                container.Add(new CuiElement {
                    Parent = $"{rowName}_InputBg",
                    Name = $"{rowName}_Input",
                    Components = {
                        new CuiInputFieldComponent { 
                            Text = currentMod.ToString(CultureInfo.InvariantCulture), 
                            FontSize = 14, 
                            Align = TextAnchor.MiddleCenter, 
                            Command = $"gatherui.setinput {itemDef.shortname}",
                            NeedsKeyboard = true
                        },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });

                CreateUIButton(container, rowName, "x2", $"gatherui.setstack {itemDef.shortname} 2.0", "0.74 0.2", "0.79 0.8", "0.2 0.5 0.8 1");
                CreateUIButton(container, rowName, "x3", $"gatherui.setstack {itemDef.shortname} 3.0", "0.80 0.2", "0.86 0.8", "0.2 0.5 0.8 1");
                CreateUIButton(container, rowName, "x5", $"gatherui.setstack {itemDef.shortname} 5.0", "0.87 0.2", "0.93 0.8", "0.6 0.4 0.2 1");
                CreateUIButton(container, rowName, "Сброс", $"gatherui.setstack {itemDef.shortname} 1.0", "0.94 0.2", "0.99 0.8", "0.4 0.4 0.4 1");
            }

            if (state.Page > 0)
                CreateUIButton(container, "ItemsPanel", "< Назад", $"gatherui.setpage {state.Page - 1}", "0.3 0.02", "0.45 0.08", "0.3 0.3 0.3 1");
            
            container.Add(new CuiLabel {
                Text = { Text = $"Страница {state.Page + 1} из {totalPages}", FontSize = 14, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.45 0.02", AnchorMax = "0.65 0.08" }
            }, "ItemsPanel");

            if (state.Page < totalPages - 1)
                CreateUIButton(container, "ItemsPanel", "Вперед >", $"gatherui.setpage {state.Page + 1}", "0.65 0.02", "0.8 0.08", "0.3 0.3 0.3 1");
        }

        private void CreateUIButton(CuiElementContainer container, string parent, string text, string command, string anchorMin, string anchorMax, string color)
        {
            container.Add(new CuiButton {
                Button = { Command = command, Color = color },
                RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                Text = { Text = text, FontSize = 13, Align = TextAnchor.MiddleCenter }
            }, parent);
        }

        private float GetCurrentModifier(string category, string resourceName)
        {
            Dictionary<string, float> dict = GetCategoryDict(category);
            if (dict != null && dict.TryGetValue(resourceName, out float val)) return val;
            return 1f; 
        }

        private Dictionary<string, float> GetCategoryDict(string category)
        {
            switch (category)
            {
                case "Dispenser": return GatherResourceModifiers;
                case "Pickup": return PickupResourceModifiers;
                case "Quarry": return QuarryResourceModifiers;
                case "Excavator": return ExcavatorResourceModifiers;
                case "Survey": return SurveyResourceModifiers;
                default: return null;
            }
        }

        private string GetCategoryConfigKey(string category)
        {
            switch (category)
            {
                case "Dispenser": return "GatherResourceModifiers";
                case "Pickup": return "PickupResourceModifiers";
                case "Quarry": return "QuarryResourceModifiers";
                case "Excavator": return "ExcavatorResourceModifiers";
                case "Survey": return "SurveyResourceModifiers";
                default: return null;
            }
        }

        [ConsoleCommand("gatherui.close")]
        private void cmdCloseUI(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                CuiHelper.DestroyUi(arg.Player(), UIMainPanel);
                openUIs.Remove(arg.Player().userID);
            }
        }

        [ConsoleCommand("gatherui.setcategory")]
        private void cmdSetCategory(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, AdminPerm)) || arg.Args == null || arg.Args.Length < 1) return;

            if (openUIs.TryGetValue(player.userID, out var state))
            {
                state.Category = arg.Args[0];
                state.Page = 0; 
                DrawContentUI(player);
            }
        }

        [ConsoleCommand("gatherui.setpage")]
        private void cmdSetPage(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, AdminPerm)) || arg.Args == null || arg.Args.Length < 1) return;

            if (openUIs.TryGetValue(player.userID, out var state) && int.TryParse(arg.Args[0], out int newPage))
            {
                state.Page = newPage;
                DrawContentUI(player);
            }
        }

        [ConsoleCommand("gatherui.setinput")]
        private void cmdSetInput(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, AdminPerm)) || arg.Args == null || arg.Args.Length < 2) return;

            string shortname = arg.Args[0];
            if (float.TryParse(arg.Args[1].Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out float rate))
            {
                UpdateRateFromUI(player, shortname, rate);
            }
            else
            {
                SendReply(player, "<color=#ff5555>Ошибка:</color> Введите корректное число (например: 2.5).");
            }
        }

        [ConsoleCommand("gatherui.setstack")]
        private void cmdSetStackConsole(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, AdminPerm)) || arg.Args == null || arg.Args.Length < 2) return;

            string shortname = arg.Args[0];
            if (float.TryParse(arg.Args[1].Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out float rate))
            {
                UpdateRateFromUI(player, shortname, rate);
            }
        }

        private void UpdateRateFromUI(BasePlayer player, string shortname, float newRate)
        {
            if (newRate < 0f) newRate = 0f;
            if (!openUIs.TryGetValue(player.userID, out var state)) return;

            string resourceName = "*";
            if (shortname != "*")
            {
                var itemDef = ItemManager.FindItemDefinition(shortname);
                if (itemDef != null)
                {
                    resourceName = itemDef.displayName.english;
                }
                else
                {
                    return;
                }
            }

            var dict = GetCategoryDict(state.Category);
            var configKey = GetCategoryConfigKey(state.Category);

            if (dict == null || configKey == null) return;

            if (newRate == 1f && resourceName != "*")
            {
                if (dict.ContainsKey(resourceName)) dict.Remove(resourceName);
            }
            else
            {
                if (dict.ContainsKey(resourceName)) dict[resourceName] = newRate;
                else dict.Add(resourceName, newRate);
            }

            SetConfigValue("Options", configKey, dict);
            DrawContentUI(player);
        }
        #endregion
    }
}