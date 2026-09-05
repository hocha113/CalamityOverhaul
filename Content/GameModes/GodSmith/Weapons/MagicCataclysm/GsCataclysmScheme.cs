using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 魔法·终局族共享方案：普攻命中积攒灾变计量（攻击方本地量，
    /// 目前只有幽灵法杖的副魂里程碑读它），并提供数值行伤害加成
    /// </summary>
    internal abstract class GsCataclysmScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "MagicCataclysm";

        //==================== 灾变参数面 ====================

        /// <summary>每次普攻命中积攒的计量</summary>
        public virtual int ChargePerHit => 3;

        /// <summary>计量上限</summary>
        public virtual int ChargeMax => 100;

        /// <summary>数值行：普攻伤害加成</summary>
        protected abstract float PassiveDamageBonus { get; }

        //==================== 数值行 ====================

        public sealed override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1f + PassiveDamageBonus;

        //==================== 计量积攒（命中钩子只在攻击方端执行，计量即攻击方本地量） ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router)
            => GainCharge(Main.player[proj.owner], target);

        public sealed override void GsOnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone)
            => GainCharge(player, target);

        private void GainCharge(Player player, NPC target) {
            //假人与友方不喂计量
            if (target.friendly || target.immortal || target.type == NPCID.TargetDummy) {
                return;
            }
            player.GetModPlayer<GsCataclysmPlayer>().AddCharge(ChargePerHit, ChargeMax, TargetItemID);
        }
    }
}
