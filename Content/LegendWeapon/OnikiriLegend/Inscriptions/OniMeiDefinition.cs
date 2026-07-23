using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>铭位，一位一铭</summary>
    public enum OniMeiSlotKind : byte
    {
        /// <summary>茎铭，刀的名字，改铭即改名</summary>
        Nakago,
        /// <summary>樋位，血槽走势</summary>
        Hi,
        /// <summary>雕位，刀身彫物</summary>
        Horimono,
    }

    /// <summary>
    /// 铭文静态定义，运行期不可变；子类即注册（<see cref="OniMeiRegistry"/> 反射扫描）。<br/>
    /// 表现层占位：仅名讳/文案/字形/铭位，效果字段待效果层再补；Key 从此稳定，改名即断档
    /// </summary>
    public abstract class OniMeiDefinition : ILocalizedModType
    {
        public Mod Mod => CWRMod.Instance;
        /// <summary>内部名，本地化键第三段</summary>
        public string Name => GetType().Name;
        public string FullName => Mod.Name + "/" + Name;
        public string LocalizationCategory => "OniMei";

        /// <summary>稳定键，存档/网络据此挂接，默认类型名</summary>
        public virtual string Key => GetType().Name;
        /// <summary>名册排序，越小越前</summary>
        public virtual int SortOrder => 0;
        /// <summary>凿于何位</summary>
        public abstract OniMeiSlotKind SlotKind { get; }
        /// <summary>金象嵌阶，字形点亮走金而非绯红</summary>
        public virtual bool IsGoldTier => false;

        //====本地化====
        /// <summary>铭名</summary>
        public LocalizedText DisplayName { get; private set; }
        /// <summary>来历残句</summary>
        public LocalizedText Origin { get; private set; }
        /// <summary>赋效文案（表现层仅展示）</summary>
        public LocalizedText Power { get; private set; }

        internal void LoadLocalization() {
            DisplayName = this.GetLocalization("DisplayName", () => "???");
            Origin = this.GetLocalization("Origin", () => "...");
            Power = this.GetLocalization("Power", () => "...");
        }
    }
}
