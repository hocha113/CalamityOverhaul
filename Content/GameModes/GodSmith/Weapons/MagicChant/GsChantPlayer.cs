using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>本次施法的拍型（施法序列内消费，见 <see cref="GsChantScheme"/>）</summary>
    internal enum ChantBeat : byte
    {
        /// <summary>平拍：首发或错过节拍窗</summary>
        Straight,
        /// <summary>正拍：节拍窗内施法，加层并返蓝</summary>
        OnBeat,
        /// <summary>强化咏唱：满层武装后的签名招</summary>
        Empower,
    }

    /// <summary>
    /// 施法节拍族的本地玩家态。全部字段只在本地玩家路径读写（myPlayer 守门），
    /// 不入存档不入网络：层数只影响 owner 端的伤害烘焙与弹幕打标，天然联机安全。
    /// 换绑武器即全清
    /// </summary>
    internal class GsChantPlayer : ModPlayer
    {
        /// <summary>当前共鸣层数</summary>
        internal int Resonance;

        /// <summary>节拍状态绑定的武器物品 ID，换武器清层</summary>
        internal int BoundItemType;

        /// <summary>下一个节拍窗开启时刻（武器就绪帧）</summary>
        internal uint WindowOpenAt;

        /// <summary>节拍窗关闭时刻；0 = 尚未开过窗</summary>
        internal uint WindowCloseAt;

        /// <summary>失拍后下一次掉层时刻</summary>
        internal uint NextDecayAt;

        /// <summary>掉层节奏（帧），按施法时的实际用时烘焙</summary>
        internal int DecayPeriod;

        /// <summary>满层已武装：下一次施法打出强化咏唱</summary>
        internal bool EmpowerArmed;

        /// <summary>本次施法拍型，ModifyShootStats 时结算、同帧 Shoot/打标窗口消费</summary>
        internal ChantBeat CurrentBeat;

        /// <summary>施法瞬间的层数快照（强化清层前记录，供弹幕打标）</summary>
        internal int ResonanceAtCast;

        /// <summary>最近一次实际扣除的魔力（正拍返蓝的基数）</summary>
        internal int LastManaConsumed;

        /// <summary>待打形态码：NewProjectile 前设置，OnSpawn 打标窗口消费后自动清零</summary>
        internal float PendingForm;

        /// <summary>待打形态参数，与 <see cref="PendingForm"/> 配对</summary>
        internal float PendingParam;

        //==================== 绑定武器专用通用寄存器（换绑清零，语义由各方案注释） ====================

        /// <summary>通用计数 A（激光枪连拍计数 / 碧水喷洒蓄势帧）</summary>
        internal int CounterA;

        /// <summary>通用时刻 A（激光枪扫射窗 / 碧水涨潮窗关闭时刻）</summary>
        internal uint TimerA;

        /// <summary>通用时刻 B（激光枪疲软窗关闭时刻）</summary>
        internal uint TimerB;

        /// <summary>通用锚点（气象痛风眼标记位置）</summary>
        internal Vector2 AnchorPos;

        /// <summary>锚点失效时刻</summary>
        internal uint AnchorUntil;

        /// <summary>确保节拍状态绑定在当前武器上，换绑即全清</summary>
        internal void EnsureBound(int itemType) {
            if (BoundItemType == itemType) {
                return;
            }
            BoundItemType = itemType;
            Resonance = 0;
            WindowOpenAt = 0;
            WindowCloseAt = 0;
            NextDecayAt = 0;
            DecayPeriod = 0;
            EmpowerArmed = false;
            CurrentBeat = ChantBeat.Straight;
            ResonanceAtCast = 0;
            PendingForm = 0f;
            PendingParam = 0f;
            CounterA = 0;
            TimerA = 0;
            TimerB = 0;
            AnchorPos = Vector2.Zero;
            AnchorUntil = 0;
        }
    }
}
