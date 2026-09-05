using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 贝特西之怒重铸（P13 左键 rider）。材质身份：龙王孽焰（贝特西吐息的咒火）。<br/>
    /// ①左键 rider：命中带贝特西之咒的敌人额外积 2 点龙焰计量，
    /// 每第 4 次咒火命中翻出一小团孽火爆②施法有掷矛前压响应。孽火爆 0.25×/4 ≈ +6%，计入包络
    /// </summary>
    internal class GsBetsysWrath : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.ApprenticeStaffT3;

        protected override string GsDescFallback =>
            "Reforged: every 4th cursed hit bursts into hexfire";
        public override int ChargePerHit => 4;

        protected override float PassiveDamageBonus => 0.08f;

        /// <summary>原版龙焰弹类型</summary>
        private static int WrathType => ContentSamples.ItemsByType[ItemID.ApprenticeStaffT3].shoot;

        /// <summary>咒火命中计数（owner 端命中钩子消费，本机契约）</summary>
        private int cursedHits;

        //==================== 动画法：掷矛前压 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //掷矛前压：矛身前送 4px 带下压，读作把怒火掷出去（绝对剖面 −0.08·p 下压，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(player.direction * 4f, 1f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, -0.08f * progress, -0.08f * ((player.itemAnimation + 1) / n));
        }

        //==================== 左键 rider：咒火积怒 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积龙焰
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != WrathType) {
                return;
            }
            //咒火积怒：怒火在被诅咒者身上烧得更旺（原版弹自带贝特西之咒）
            if (!proj.IsOwnedByLocalPlayer() || !target.HasBuff(BuffID.BetsysCurse)) {
                return;
            }
            Main.player[proj.owner].GetModPlayer<GsCataclysmPlayer>().AddCharge(2, ChargeMax, TargetItemID);
            cursedHits++;
            if (cursedHits % 4 != 0) {
                return;
            }
            //孽火爆：小团咒火翻起（真弹幕跨端可见；Misc 源不承签）
            Player owner = Main.player[proj.owner];
            Projectile.NewProjectile(owner.GetSource_Misc("GsCataclysmRider"), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsCataclysmRiderBurstProj>(),
                Math.Max(1, (int)(proj.damage * 0.25f)), 2f, proj.owner,
                60f, GsCataclysmRiderBurstProj.ThemeEmber);
        }
    }
}
