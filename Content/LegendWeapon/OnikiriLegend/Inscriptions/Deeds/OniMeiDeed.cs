using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions.Deeds
{
    /// <summary>刀縁判定的信道，注册期按此分桶，事件只走对应那一桶</summary>
    internal enum OniMeiDeedChannel : byte
    {
        /// <summary>以鬼切招式了结一个目标</summary>
        Kill,
        /// <summary>一次疾走的穿身结算（<see cref="OniMeiDeedContext.Amount"/> = 穿过的不同主体数）</summary>
        DashPierce,
        /// <summary>斩断一张面影纸型</summary>
        OmokageSever,
        /// <summary>樱流巡航的逐帧推进</summary>
        SakuraTick,
        /// <summary>持刀逐帧，连续态从 <see cref="OniMeiDeedTracker"/> 读</summary>
        HeldTick,
    }

    /// <summary>了结那一刀是哪个招式落的，供「用某招杀某物」型刀縁分辨</summary>
    internal enum OniMeiDeedKillSource : byte
    {
        /// <summary>非鬼切主刀（副斩、灼地等）</summary>
        Secondary,
        /// <summary>连段</summary>
        Combo,
        /// <summary>残心</summary>
        Zanshin,
        /// <summary>灭世一闪</summary>
        Annihilate,
        /// <summary>终结</summary>
        Finale,
        /// <summary>疾走墨痕</summary>
        FlashMark,
        /// <summary>里世界肢解</summary>
        Sever,
    }

    /// <summary>刀縁进度的记法，决定木牌上怎么读</summary>
    internal enum OniMeiDeedProgressKind : byte
    {
        /// <summary>累计计数，读作 n / 需求</summary>
        Count,
        /// <summary>一次性壮举，读作未成 / 已成</summary>
        Feat,
    }

    /// <summary>一次刀縁判定的现场，各信道只填自己用得上的字段</summary>
    internal readonly struct OniMeiDeedContext(Player player, OniMeiDeedTracker tracker,
        NPC npc = null, int amount = 0, OniMeiDeedKillSource killSource = OniMeiDeedKillSource.Secondary)
    {
        public readonly Player Player = player;
        public readonly OniMeiDeedTracker Tracker = tracker;
        /// <summary>Kill 信道的目标（已归主体）</summary>
        public readonly NPC Npc = npc;
        /// <summary>信道自定的量：DashPierce = 穿身主体数</summary>
        public readonly int Amount = amount;
        public readonly OniMeiDeedKillSource KillSource = killSource;
    }

    /// <summary>
    /// 刀縁：一枚铭的解锁条件。子类即注册（<see cref="OniMeiDeedRegistry"/> 反射扫描），
    /// 判定一律由 <see cref="OniMeiDeedEvents"/> 在 owner 端推进；
    /// <see cref="Key"/> 从此稳定，改名即断档
    /// </summary>
    internal abstract class OniMeiDeed
    {
        /// <summary>稳定键，存档/网络据此挂接，默认类型名</summary>
        public virtual string Key => GetType().Name;
        /// <summary>达成后解锁的铭 Key，须存在于 <see cref="OniMeiRegistry"/></summary>
        public abstract string MeiKey { get; }
        /// <summary>听哪条信道</summary>
        public abstract OniMeiDeedChannel Channel { get; }
        public virtual OniMeiDeedProgressKind ProgressKind => OniMeiDeedProgressKind.Feat;
        /// <summary>Count 型的需求量；Feat 恒按 1 处理</summary>
        public virtual int NeedCount => 1;
        /// <summary>名册排序，越小越前；缺省跟随所解锁铭的排序</summary>
        public virtual int SortOrder => 0;

        /// <summary>
        /// 本次事件为该縁推进多少，0 = 不符。owner 端调用，禁在此写任何全局状态
        /// </summary>
        public abstract int Test(in OniMeiDeedContext context);

        /// <summary>
        /// 去重记号：同一记号只算一次（如「不同种类的首领」）。0 = 不去重，逐次累计
        /// </summary>
        public virtual int MarkOf(in OniMeiDeedContext context) => 0;
    }
}
