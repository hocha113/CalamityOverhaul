using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>
    /// 水晶握把（克苏鲁之脑）：暴击命中会在战场凝出弱点晶面，后续光束经过时被折射、优先打向附近敌人；
    /// 右键引爆会击碎爆心附近的晶面，化作一阵水晶碎片雨。
    /// </summary>
    internal sealed class CrystalGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //水晶幻紫
        public override Color TintColor => new(200, 130, 255);

        private const int MaxFacets = 4;
        private const float FacetSpacing = 90f;
        private const float ShatterRange = 560f;

        public override void Apply(ref ShootContext ctx) {
            ctx.ManaCostMul += -0.16f;
            ctx.CritAdd += 4;
            ctx.ChargeTimeMul += 0.12f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer || !hit.Crit) return;
            int facetType = ModContent.ProjectileType<SHPCCrystalFacetProj>();
            if (SHPCNaturalFx.CountOwned(beam.Projectile.owner, facetType) >= MaxFacets) return;
            if (SHPCNaturalFx.HasOwnedNear(beam.Projectile.owner, facetType, target.Center, FacetSpacing)) return;
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                target.Center, Vector2.Zero, facetType,
                Math.Max(beam.Projectile.damage / 2, 1), 0f, beam.Projectile.owner);
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            int facetType = ModContent.ProjectileType<SHPCCrystalFacetProj>();
            float r2 = ShatterRange * ShatterRange;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != orb.Projectile.owner || p.type != facetType) continue;
                if (Vector2.DistanceSquared(p.Center, orb.Projectile.Center) > r2) continue;
                //碎片雨：朝四周迸射数道短程派生束
                for (int s = 0; s < 4; s++) {
                    float ang = MathHelper.TwoPi * s / 4f + Main.rand.NextFloat(-0.3f, 0.3f);
                    SHPCNaturalFx.SpawnDerivedBeam(p, p.Center, ang.ToRotationVector2() * 12f, Math.Max(p.damage, 1), 1.6f, 0.4f);
                }
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = 0.5f }, p.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(p.Center, Vector2.Zero, new Color(210, 150, 255, 0), 0.05f).Configure(0.05f, 0.3f, 14);
                }
                p.Kill();
            }
        }
    }

    /// <summary>
    /// 弱点晶面：静止悬浮的折射晶体。SHPC 光束经过时把它折射成一道射向最近敌人的派生束。
    /// </summary>
    internal sealed class SHPCCrystalFacetProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int Lifetime = 300;
        private const int ScanInterval = 4;

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        private int MaxRefractions => 3;

        public override void AI() {
            Projectile.rotation += 0.04f;
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, new Vector3(0.7f, 0.45f, 1f) * 0.55f);
            int frame = (int)Main.GameUpdateCount + Projectile.whoAmI;
            if (frame % ScanInterval == 0 && Projectile.owner == Main.myPlayer && Projectile.localAI[0] < MaxRefractions) {
                TryRefract();
            }
            if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 10 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(16f, 16f), Main.rand.NextVector2Circular(0.4f, 0.4f), new Color(225, 190, 255), Main.rand.NextFloat(0.3f, 0.6f)).Configure(new Color(150, 90, 230), Main.rand.Next(14, 24), Main.rand.NextFloat(-0.15f, 0.15f), 0.7f);
            }
        }

        private void TryRefract() {
            int beamType = ModContent.ProjectileType<CyberTraceBeamProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.owner != Projectile.owner || other.type != beamType) continue;
                if (Vector2.DistanceSquared(other.Center, Projectile.Center) > 44f * 44f) continue;
                NPC target = Projectile.Center.FindClosestNPC(700f, false, true);
                Vector2 dir = target != null
                    ? (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
                    : other.velocity.SafeNormalize(Vector2.UnitX);
                SHPCNaturalFx.SpawnDerivedBeam(Projectile, Projectile.Center, dir * 13f, Math.Max(Projectile.damage, 1), 2f, 0.35f);
                Projectile.localAI[0]++;
                Projectile.timeLeft -= 24;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item101 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, new Color(210, 150, 255, 0), 0.05f).Configure(0.05f, 0.26f, 12);
                }
                return;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null) {
                Vector2 screen = Projectile.Center - Main.screenPosition;
                float pulse = 0.85f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.18f);
                Main.spriteBatch.Draw(star, screen, null, new Color(210, 160, 255, 0) * pulse, Projectile.rotation, star.Size() * 0.5f, 0.16f, SpriteEffects.None, 0f);
            }
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 screen = Projectile.Center - Main.screenPosition;
            //三色微偏仿色散
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, screen + new Vector2(-2f, 0f), new Color(255, 80, 120, 0) * 0.45f, new Color(120, 30, 60, 0) * 0.2f, 0.45f, 0f, 2);
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, screen + new Vector2(2f, 0f), new Color(120, 120, 255, 0) * 0.45f, new Color(40, 40, 140, 0) * 0.2f, 0.45f, 0f, 2);
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, screen, new Color(220, 180, 255, 0) * 0.55f, new Color(120, 70, 200, 0) * 0.3f, 0.4f, 0f, 2);
        }
    }
}
