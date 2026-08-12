using CalamityOverhaul.Common;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.MachineModules
{
    /// <summary>模块可安装的机器种类,模块用 flags 声明兼容面</summary>
    [Flags]
    public enum MachineModuleTarget
    {
        None = 0,
        /// <summary>采矿机</summary>
        MiningMachine = 1 << 0,
        /// <summary>热能发电机</summary>
        ThermalGenerator = 1 << 1,
        /// <summary>风力发电机</summary>
        WindGenerator = 1 << 2,
        /// <summary>水力发电机</summary>
        HydroGenerator = 1 << 3,
        /// <summary>电动焚化炉</summary>
        Incinerator = 1 << 4,
    }

    /// <summary>机器升级模块的最小契约:声明自己能装进哪些机器</summary>
    public interface IMachineModule
    {
        MachineModuleTarget ModuleTargets { get; }
    }

    /// <summary>热力发电机效果:燃速同时作用于燃烧时间递减与产热速率(总能量守恒)</summary>
    public interface IThermalModule
    {
        /// <summary>燃速乘数(大于 1 烧得快火更旺,小于 1 细水长流)</summary>
        float BurnRateMult { get; }
        /// <summary>燃料产热乘数(每份燃料总热量)</summary>
        float HeatYieldMult { get; }
        /// <summary>散热乘数(小于 1 为保温)</summary>
        float DissipationMult { get; }
    }

    /// <summary>无燃料发电机(风/水)效果</summary>
    public interface IGeneratorModule
    {
        /// <summary>输出乘数</summary>
        float OutputMult { get; }
        /// <summary>工况下限抬升(风力 windFactor 的下限,0 表示不抬)</summary>
        float ConditionFloor { get; }
        /// <summary>起转爬升乘数(水力转速爬升)</summary>
        float SpinUpMult { get; }
    }

    /// <summary>焚化炉效果</summary>
    public interface IIncineratorModule
    {
        /// <summary>熔炼速度乘数</summary>
        float SmeltSpeedMult { get; }
        /// <summary>每 tick 能耗乘数</summary>
        float SmeltEnergyMult { get; }
        /// <summary>双倍产出概率 0~1</summary>
        float DoubleOutputChance { get; }
    }

    /// <summary>物流行为件:自动进料(从近旁存储抽料)/自动出料(产物直入近旁存储)</summary>
    public interface ILogisticsModule
    {
        bool AutoFeed { get; }
        bool AutoEject { get; }
    }

    /// <summary>通用储能扩容</summary>
    public interface IStorageModule
    {
        /// <summary>储能上限乘数</summary>
        float CapacityMult { get; }
    }

    /// <summary>
    /// 机器模块架:任何 TP 持有一个即可获得模块槽。<br/>
    /// 存档键沿用矿机的 <c>_Module{i}</c>(旧档直读);网络字段一律追加在宿主包尾;
    /// 聚合结果按域缓存,脏标记驱动。UI 编辑走"本地改 + SendData 推送"的既有客户端权威模型
    /// </summary>
    public class MachineModuleRack
    {
        /// <summary>宿主机器种类,决定 <see cref="Accepts"/> 的判定</summary>
        public readonly MachineModuleTarget HostKind;
        private Item[] slots;
        private bool dirty = true;

        public MachineModuleRack(MachineModuleTarget hostKind) {
            HostKind = hostKind;
        }

        #region 聚合结果(Refresh 后有效)
        public float StorageMult { get; private set; } = 1f;
        public float ThermalBurnRate { get; private set; } = 1f;
        public float ThermalHeatYield { get; private set; } = 1f;
        public float ThermalDissipation { get; private set; } = 1f;
        public float GenOutputMult { get; private set; } = 1f;
        public float GenConditionFloor { get; private set; }
        public float GenSpinUpMult { get; private set; } = 1f;
        public float IncSpeedMult { get; private set; } = 1f;
        public float IncEnergyMult { get; private set; } = 1f;
        public float IncDoubleChance { get; private set; }
        public bool AutoFeed { get; private set; }
        public bool AutoEject { get; private set; }
        #endregion

        public Item[] EnsureSlots(int count) {
            if (slots == null || slots.Length != count) {
                Item[] old = slots;
                slots = new Item[count];
                for (int i = 0; i < count; i++) {
                    slots[i] = new Item();
                }
                if (old != null) {
                    for (int i = 0; i < Math.Min(old.Length, count); i++) {
                        slots[i] = old[i] ?? new Item();
                    }
                }
                dirty = true;
            }
            return slots;
        }

        /// <summary>这台机器收不收这枚物品</summary>
        public bool Accepts(Item item)
            => item?.ModItem is IMachineModule module && (module.ModuleTargets & HostKind) != 0;

        /// <summary>同类模块每台限一枚</summary>
        public bool HasType(int itemType, int ignoreSlot = -1) {
            if (slots == null) {
                return false;
            }
            for (int i = 0; i < slots.Length; i++) {
                if (i != ignoreSlot && slots[i] != null && !slots[i].IsAir && slots[i].type == itemType) {
                    return true;
                }
            }
            return false;
        }

        public void MarkDirty() => dirty = true;

        /// <summary>聚合全部域的模块效果,脏标记驱动,宿主每帧调用无负担</summary>
        public void Refresh() {
            if (!dirty) {
                return;
            }
            dirty = false;

            float storage = 1f;
            float burnRate = 1f, heatYield = 1f, dissipation = 1f;
            float genOutput = 1f, genFloor = 0f, genSpinUp = 1f;
            float incSpeed = 1f, incEnergy = 1f, incDouble = 0f;
            bool feed = false, eject = false;

            if (slots != null) {
                foreach (Item item in slots) {
                    if (item == null || item.IsAir || item.ModItem is not IMachineModule) {
                        continue;
                    }
                    if (item.ModItem is IStorageModule s) {
                        storage *= s.CapacityMult;
                    }
                    if (item.ModItem is IThermalModule t) {
                        burnRate *= t.BurnRateMult;
                        heatYield *= t.HeatYieldMult;
                        dissipation *= t.DissipationMult;
                    }
                    if (item.ModItem is IGeneratorModule g) {
                        genOutput *= g.OutputMult;
                        genFloor = Math.Max(genFloor, g.ConditionFloor);
                        genSpinUp *= g.SpinUpMult;
                    }
                    if (item.ModItem is IIncineratorModule inc) {
                        incSpeed *= inc.SmeltSpeedMult;
                        incEnergy *= inc.SmeltEnergyMult;
                        incDouble = 1f - (1f - incDouble) * (1f - MathHelper.Clamp(inc.DoubleOutputChance, 0f, 1f));
                    }
                    if (item.ModItem is ILogisticsModule log) {
                        feed |= log.AutoFeed;
                        eject |= log.AutoEject;
                    }
                }
            }

            StorageMult = storage;
            ThermalBurnRate = burnRate;
            ThermalHeatYield = heatYield;
            ThermalDissipation = dissipation;
            GenOutputMult = genOutput;
            GenConditionFloor = genFloor;
            GenSpinUpMult = genSpinUp;
            IncSpeedMult = incSpeed;
            IncEnergyMult = incEnergy;
            IncDoubleChance = incDouble;
            AutoFeed = feed;
            AutoEject = eject;
        }

        #region 存档与网络(键名与字节序沿用矿机既有格式)
        public void Save(TagCompound tag, int count) {
            EnsureSlots(count);
            for (int i = 0; i < slots.Length; i++) {
                tag[$"_Module{i}"] = ItemIO.Save(slots[i] ?? new Item());
            }
        }

        public void Load(TagCompound tag, int count, string owner) {
            EnsureSlots(count);
            for (int i = 0; i < slots.Length; i++) {
                slots[i] = CWRSaveData.LoadItemFromTag(tag, $"_Module{i}", owner);
            }
            dirty = true;
        }

        public void Send(Terraria.ModLoader.ModPacket data, int count) {
            EnsureSlots(count);
            for (int i = 0; i < slots.Length; i++) {
                ItemIO.Send(slots[i] ?? new Item(), data, true);
            }
        }

        public void Receive(BinaryReader reader, int count) {
            EnsureSlots(count);
            for (int i = 0; i < slots.Length; i++) {
                slots[i] = ItemIO.Receive(reader, true);
            }
            dirty = true;
        }
        #endregion

        /// <summary>拆机时倒出全部模块(权威端调用,dropper 负责生成与同步)</summary>
        public void DropAll(Action<Item> dropper) {
            if (slots == null) {
                return;
            }
            foreach (Item item in slots) {
                if (item != null && !item.IsAir) {
                    dropper(item.Clone());
                    item.TurnToAir();
                }
            }
            dirty = true;
        }
    }

    /// <summary>模块物品的共用文案(tooltip 标签行/适用行/安装提示/插座交互反馈)</summary>
    internal class MachineModuleText : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Items";

        internal static LocalizedText TagText;
        internal static LocalizedText TargetsLine;
        internal static LocalizedText HowToText;
        internal static LocalizedText SocketOnly;
        internal static LocalizedText SocketDuplicate;
        internal static LocalizedText SocketEmptyHint;
        internal static LocalizedText SlotLabel;
        internal static LocalizedText NameMining;
        internal static LocalizedText NameThermal;
        internal static LocalizedText NameWind;
        internal static LocalizedText NameHydro;
        internal static LocalizedText NameIncinerator;

        public override void SetStaticDefaults() {
            TagText = this.GetLocalization(nameof(TagText), () => "Machine Upgrade Module");
            TargetsLine = this.GetLocalization(nameof(TargetsLine), () => "Fits: {0}");
            HowToText = this.GetLocalization(nameof(HowToText), () => "Right-click a compatible machine and slot this into its panel");
            SocketOnly = this.GetLocalization(nameof(SocketOnly), () => "This machine can't take that module!");
            SocketDuplicate = this.GetLocalization(nameof(SocketDuplicate), () => "A module of this type is already installed!");
            SocketEmptyHint = this.GetLocalization(nameof(SocketEmptyHint), () => "Insert an upgrade module");
            SlotLabel = this.GetLocalization(nameof(SlotLabel), () => "Modules");
            NameMining = this.GetLocalization(nameof(NameMining), () => "Mining Machine");
            NameThermal = this.GetLocalization(nameof(NameThermal), () => "Thermal Generator");
            NameWind = this.GetLocalization(nameof(NameWind), () => "Wind Turbine");
            NameHydro = this.GetLocalization(nameof(NameHydro), () => "Hydro Generator");
            NameIncinerator = this.GetLocalization(nameof(NameIncinerator), () => "Incinerator");
        }

        /// <summary>把 flags 展开成机器名列表</summary>
        internal static string DescribeTargets(MachineModuleTarget targets) {
            List<string> names = [];
            if ((targets & MachineModuleTarget.MiningMachine) != 0) names.Add(NameMining.Value);
            if ((targets & MachineModuleTarget.ThermalGenerator) != 0) names.Add(NameThermal.Value);
            if ((targets & MachineModuleTarget.WindGenerator) != 0) names.Add(NameWind.Value);
            if ((targets & MachineModuleTarget.HydroGenerator) != 0) names.Add(NameHydro.Value);
            if ((targets & MachineModuleTarget.Incinerator) != 0) names.Add(NameIncinerator.Value);
            return names.Count > 0 ? string.Join('/', names) : "-";
        }
    }
}
