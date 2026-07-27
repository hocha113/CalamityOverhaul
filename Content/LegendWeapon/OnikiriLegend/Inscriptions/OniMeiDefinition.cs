using Terraria.Localization;

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
    /// 效果经 <see cref="ModifyCombatProfile"/> 汇入 <see cref="OniMeiCombatProfile"/>，
    /// 原铭「鬼切」不覆写=严格基准；Key 从此稳定，改名即断档
    /// </summary>
    public abstract class OniMeiDefinition
    {
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
        /// <summary>赋效文案（真实机制说明）</summary>
        public LocalizedText Power { get; private set; }
        /// <summary>代价文案（真实负担说明，原铭无代价="———"）</summary>
        public LocalizedText Burden { get; private set; }
        /// <summary>物品悬浮短摘要（赋效;代价）</summary>
        public LocalizedText Summary { get; private set; }

        internal bool HasLocalization
            => DisplayName != null && Origin != null && Power != null && Burden != null && Summary != null;

        /// <summary>铭文案由同 Key 拓本物品统一注册，本定义只保留只读视图</summary>
        internal void BindLocalization(OniMeiRubbingItem rubbing) {
            DisplayName = rubbing.DisplayName;
            Origin = rubbing.Origin;
            Power = rubbing.Power;
            Burden = rubbing.Burden;
            Summary = rubbing.Tooltip;
        }

        //====效果====
        /// <summary>
        /// 汇入三槽合成战斗档（<see cref="OniMeiCombat.Resolve"/> 逐槽调用）。<br/>
        /// 倍率一律"叠乘/累加"，禁止直接赋值覆盖其他槽；原铭「鬼切」与空铭不覆写
        /// </summary>
        public virtual void ModifyCombatProfile(ref OniMeiCombatProfile profile) { }
    }
}
