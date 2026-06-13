using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SCCA32CRPs
{
    /// <summary>
    /// SCCA-32 CRP，左臂槽位神经反射处理器
    /// <br/>常驻 +CritChanceBonus% 暴击、+MoveSpeedBonus 移速
    /// <br/>致命攻击 DodgeChance 概率反射闪避，亢奋 ReflexBoostFrames 帧额外暴击/移速+短暂无敌
    /// <br/>闪避内部冷却 DodgeCooldownFrames 帧，防密集伤害实质无敌
    /// </summary>
    internal class SCCA32CRP : BaseCyberware
    {
        /// <summary>常驻暴击率加成%</summary>
        public const int CritChanceBonus = 8;

        /// <summary>常驻移速加成</summary>
        public const float MoveSpeedBonus = 0.08f;

        /// <summary>反射闪避概率 0~1</summary>
        public const float DodgeChance = 0.18f;

        /// <summary>闪避内部冷却帧</summary>
        public const int DodgeCooldownFrames = 60 * 4;

        /// <summary>反射亢奋持续帧</summary>
        public const int ReflexBoostFrames = 60 * 3;

        /// <summary>亢奋额外暴击%</summary>
        public const int BoostExtraCrit = 12;

        /// <summary>亢奋额外移速</summary>
        public const float BoostExtraMoveSpeed = 0.25f;

        /// <summary>闪避成功无敌帧</summary>
        public const int DodgeImmunityFrames = 30;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.LeftArm;

        public override int CapacityCost => 4;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(0, 10, 0, 0);
        }

        /// <summary>查询玩家是否装备本义体，未装备返回 null</summary>
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
