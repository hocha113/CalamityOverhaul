using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 星云烈焰重铸（P13 左键 rider）。材质身份：星云引信火（新星引爆的火种）。<br/>
    /// ①左键 rider：「引信」，星云喷发强化弹（原版概率打出的大弹）命中必定点燃一枚
    /// 微新星爆②施法有急促后坐响应。
    /// 微新星与强化弹联动 ≈ +5%，底伤加成自 10% 回缩至 8%
    /// </summary>
    internal class GsNebulaBlaze : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.NebulaBlaze;

        protected override string GsDescFallback =>
            "Reforged: nebula eruption bolts now always ignite a micro nova where they strike";
        public override int ChargePerHit => 4;

        /// <summary>微新星是机制收益，底伤加成回缩（公约 §5）</summary>
        protected override float PassiveDamageBonus => 0.08f;

        /// <summary>原版普通弹类型</summary>
        private static int BlazeType => ContentSamples.ItemsByType[ItemID.NebulaBlaze].shoot;

        //==================== 动画法：急促后坐 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //快弹道法器的急促后坐：3px 快速回坐（确定性输入，各端一致）
            float elapsed = 1f - player.itemAnimation / (float)player.itemAnimationMax;
            float kick = MathF.Exp(-6f * elapsed);
            Vector2 aimDir = player.itemRotation.ToRotationVector2() * player.direction;
            player.itemLocation -= aimDir * (3f * kick);
        }

        //==================== 左键 rider：强化弹微新星 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积烈焰
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            bool eruption = proj.type == ProjectileID.NebulaBlaze2;
            if (proj.type != BlazeType && !eruption) {
                return;
            }
            //引信兑现：强化弹命中必燃微新星（真弹幕跨端可见；Misc 源不承签）
            if (!eruption || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            Player owner = Main.player[proj.owner];
            Projectile.NewProjectile(owner.GetSource_Misc("GsCataclysmRider"), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsCataclysmRiderBurstProj>(),
                Math.Max(1, (int)(proj.damage * 0.4f)), 3f, proj.owner,
                80f, GsCataclysmRiderBurstProj.ThemeNova);
        }
    }
}
