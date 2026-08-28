using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 光辉飞盘重铸。材质：光能悬盘。签名行为：①命中敌人时向最近的另一敌折射续飞，
    /// 至多三次，每折射一次伤害递减 10% ②折射瞬间光束闪爆与高频鸣响 ③青光能弧残影
    /// </summary>
    internal class GsLightDisc : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.LightDisc;

        internal override int BoomerProjType => ModContent.ProjectileType<GsLightDiscProj>();

        internal override int MaxAirborne => 6;   //与原版六盘上限对齐

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "On hitting a foe it refracts toward the nearest other enemy, up to three bounces,\n" +
            "losing 10% damage per bounce\n" +
            "Right click while it flies: command it to dash toward your cursor";
    }

    /// <summary>光能盘体：光弧折射</summary>
    internal class GsLightDiscProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.LightDisc;

        protected override Color GlowColor => new(95, 230, 255);

        protected override Color TrailColor => new(70, 190, 255);

        protected override float OutDrag => 0.975f;
        protected override int DashTime => 24;
        protected override bool HoverOnFirstHit => false;
        protected override float GhostBaseAlpha => 0.32f;
        protected override SoundStyle HitSound => SoundID.Item10 with { Volume = 0.4f, Pitch = 0.4f };

        /// <summary>已折射次数（owner 权威）</summary>
        private int refractCount;

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Projectile.IsOwnedByLocalPlayer() || Phase == PhaseReturn) {
                return;
            }
            //光弧折射：向最近的另一敌续飞
            if (refractCount < 3) {
                NPC next = FindRefractTarget(target);
                if (next != null) {
                    refractCount++;
                    Projectile.damage = Math.Max(1, (int)(Projectile.damage * 0.9f));
                    Projectile.velocity = (next.Center - Projectile.Center)
                        .SafeNormalize(Vector2.UnitX * spinDir) * DashSpeed;
                    EnterPhase(PhaseDash, Owner);
                    RefractFX(target);
                    return;
                }
            }
            EnterPhase(PhaseReturn, Owner);
        }

        private NPC FindRefractTarget(NPC exclude) {
            NPC best = null;
            float bestDist = 540f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == exclude.whoAmI || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float d = npc.Distance(Projectile.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        private void RefractFX(NPC from) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.6f, Pitch = 0.6f }, from.Center);
            PRTLoader.NewParticle<PRT_Light>(from.Center, Vector2.Zero, GlowColor, 0.42f)?.Configure(10, 0.95f);
            //折射向的光束火花束
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(from.Center,
                    dir.RotatedByRandom(0.18) * Main.rand.NextFloat(7f, 12f), GlowColor,
                    Main.rand.NextFloat(0.45f, 0.7f))?.Configure(false, Main.rand.Next(8, 13));
            }
        }
    }
}
