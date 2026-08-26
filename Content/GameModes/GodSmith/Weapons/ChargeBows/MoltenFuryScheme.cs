using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.ChargeBows
{
    /// <summary>
    /// 熔岩之怒（前困难毕业弓）：原版「木箭化烈焰箭」保留。
    /// T2 熔核箭：命中喷 3 颗短命熔浆珠（各 40%，弧线洒落）；
    /// T3 火山术：命中点上方落 5 道熔雨（各 50%，原版烈焰箭自带引燃）+ 落点熔火迸发。
    /// 熔雨以敌为心从上空落下，悬空目标同样成立
    /// </summary>
    internal class GsMoltenFury : GsChargeBowScheme
    {
        public override int TargetItemID => ItemID.MoltenFury;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw. Full-drawn molten cores spray magma beads on impact; an overdrawn volcanic shot calls five molten rain bolts down on the mark";
        internal override float DpsTarget => 1.0f;
        internal override Color TrailMain => new(255, 140, 50);
        internal override Color TrailHot => new(255, 220, 130);
        internal override Color TrailDeep => new(130, 44, 26);

        internal override int TransformShootType(int pickedType, int tier)
            => pickedType == ProjectileID.WoodenArrowFriendly ? ProjectileID.FireArrow : pickedType;

        internal override void OnQualityHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) {
            //熔核箭：命中喷洒短命熔浆珠（owner 端生成，承签打标，寿命修剪在各端 PostAI 首帧统一做）
            int beadDamage = Math.Max(1, (int)(proj.damage * 0.4f));
            for (int i = 0; i < 3; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-3.5f, 3.5f), -Main.rand.NextFloat(3f, 6f));
                int beadType = ProjectileID.GreekFire1 + Main.rand.Next(3);
                StampNext(tier, KindMagmaBead);
                Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, vel,
                    beadType, beadDamage, 0.5f, proj.owner);
            }

            if (tier < 3) {
                return;
            }
            //火山术：以敌为心的上空熔雨，高度阶梯错时落地
            int rainDamage = Math.Max(1, (int)(proj.damage * 0.5f));
            for (int i = 0; i < 5; i++) {
                Vector2 pos = new(target.Center.X + Main.rand.NextFloat(-60f, 60f),
                    target.Center.Y - 90f - i * 26f);
                StampNext(tier, KindMoltenRain);
                Projectile.NewProjectile(proj.GetSource_FromThis(), pos, new Vector2(0f, 10f),
                    ProjectileID.FireArrow, rainDamage, proj.knockBack * 0.4f, proj.owner);
            }
            //落点熔火迸发（客户端演出）
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_LavaFire>(target.Center + Main.rand.NextVector2Circular(14f, 8f),
                        new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.5f, 1.5f)),
                        TrailMain, Main.rand.NextFloat(0.5f, 0.8f))?.SetLifetime(28, 40);
                }
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, TrailMain, 0.2f)?.Configure(10, 0.8f);
            }
        }

        internal override void ArrowPostAI(Projectile proj, GodSmithProjRouter router, int tier, int kind) {
            if (kind != KindMagmaBead) {
                return;
            }
            //短命熔浆珠：各端首帧统一修剪寿命（timeLeft 不进生成包，出生窗口只有生成端可见）
            TrimState state = router.GetOrCreateState<TrimState>();
            if (!state.Done) {
                state.Done = true;
                if (proj.timeLeft > 50) {
                    proj.timeLeft = 50;
                }
            }
        }

        private class TrimState
        {
            public bool Done;
        }
    }
}
