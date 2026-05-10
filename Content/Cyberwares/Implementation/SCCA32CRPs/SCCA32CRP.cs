using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SCCA32CRPs
{
    /// <summary>
    /// 亚意识战斗反射处理器 SCCA-32 CRP
    /// <br/>左臂槽位的高阶神经-肌肉协处理器，将攻击预测与肌纤维反射绕过大脑直接下达
    /// <list type="bullet">
    ///   <item>持续提供 <see cref="CritChanceBonus"/>% 全伤害类暴击率，
    ///         以及小幅 <see cref="MoveSpeedBonus"/> 移动速度</item>
    ///   <item>遭受致命攻击时存在 <see cref="DodgeChance"/> 概率触发<b>反射闪避</b>，
    ///         完全免疫该次伤害</item>
    ///   <item>触发反射后进入 <see cref="ReflexBoostFrames"/> 帧的反射亢奋状态：
    ///         额外提升暴击率与移动速度，并伴随短暂无敌窗口</item>
    /// </list>
    /// 反射闪避存在 <see cref="DodgeCooldownFrames"/> 帧的内部冷却，
    /// 防止在密集伤害下被反复触发导致"实质无敌"
    /// </summary>
    internal class SCCA32CRP : BaseCyberware
    {
        /// <summary>
        /// 持续提供的暴击率加成（百分比）
        /// </summary>
        public const int CritChanceBonus = 8;

        /// <summary>
        /// 持续提供的移动速度加成（基于原版 1.0 标准）
        /// </summary>
        public const float MoveSpeedBonus = 0.08f;

        /// <summary>
        /// 反射闪避的触发概率（0~1）
        /// </summary>
        public const float DodgeChance = 0.18f;

        /// <summary>
        /// 反射闪避后的内部冷却（帧）
        /// </summary>
        public const int DodgeCooldownFrames = 60 * 4;

        /// <summary>
        /// 反射亢奋状态持续帧数
        /// </summary>
        public const int ReflexBoostFrames = 60 * 3;

        /// <summary>
        /// 亢奋状态额外提供的暴击率加成
        /// </summary>
        public const int BoostExtraCrit = 12;

        /// <summary>
        /// 亢奋状态额外提供的移动速度加成
        /// </summary>
        public const float BoostExtraMoveSpeed = 0.25f;

        /// <summary>
        /// 反射成功瞬间的无敌帧（防止下一次连击直接打死）
        /// </summary>
        public const int DodgeImmunityFrames = 30;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.LeftArm;

        public override int CapacityCost => 4;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(0, 10, 0, 0);
        }

        /// <summary>
        /// 查询指定玩家是否装备了 <see cref="SCCA32CRP"/>，未装备返回 null
        /// </summary>
        public static SCCA32CRP GetEquipped(Player player) {
            if (player == null || !player.active) {
                return null;
            }
            CyberwarePlayer cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
            if (cyberPlayer?.EquippedCyberwares == null) {
                return null;
            }
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cyberPlayer.EquippedCyberwares[i]?.ModItem is SCCA32CRP crp) {
                    return crp;
                }
            }
            return null;
        }
    }
}
