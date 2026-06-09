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

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>
    /// 延伸枪托（远端中继）：大幅延长光束射程与初速。寿命耗尽自然消散的光束会在终点留下中继节点，
    /// 后续经过的光束会被节点接力，从节点处再续发一段，把火力一程程接到更远的地方。
    /// </summary>
    internal sealed class ExtenderStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //延伸银灰
        public override Color TintColor => new(190, 200, 210);

        private const int MaxRelays = 3;
        private const float RelaySpacing = 140f;

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamLifeMul += 0.65f;
            ctx.BeamSpeedMul += 0.3f;
            ctx.DamageMul += -0.05f;
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            //仅在光束自然寿终时留下中继，避免撞墙/穿透即触发
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer || timeLeft > 0) return;
            int type = ModContent.ProjectileType<SHPCRelayNodeProj>();
            if (SHPCNaturalFx.CountOwned(beam.Projectile.owner, type) >= MaxRelays) return;
            if (SHPCNaturalFx.HasOwnedNear(beam.Projectile.owner, type, beam.Projectile.Center, RelaySpacing)) return;
            float dir = beam.Projectile.velocity.ToRotation();
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center, Vector2.Zero, type,
                Math.Max(beam.Projectile.damage, 1), 0f, beam.Projectile.owner, ai0: dir);
        }
    }

    /// <summary>
    /// 中继节点：静止的接力点。附近有 SHPC 光束经过时，沿其方向从节点续发一段派生束，使用次数有限。
    /// </summary>
    internal sealed class SHPCRelayNodeProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int Lifetime = 300;
        private const int ScanInterval = 5;
        private const int MaxRelays = 2;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.rotation += 0.06f;
            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.7f, 0.8f) * 0.4f);
            int frame = (int)Main.GameUpdateCount + Projectile.whoAmI;
            if (frame % ScanInterval == 0 && Projectile.owner == Main.myPlayer && Projectile.localAI[0] < MaxRelays) {
                TryRelay();
            }
            if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 8 == 0) {
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f), Vector2.Zero, new Color(190, 210, 230), Main.rand.NextFloat(0.3f, 0.5f)).Configure(new Color(120, 150, 180), Main.rand.Next(8, 14));
            }
        }

        private void TryRelay() {
            int beamType = ModContent.ProjectileType<CyberTraceBeamProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.owner != Projectile.owner || other.type != beamType) continue;
                if (Vector2.DistanceSquared(other.Center, Projectile.Center) > 40f * 40f) continue;
                Vector2 dir = other.velocity.SafeNormalize(Projectile.ai[0].ToRotationVector2());
                SHPCNaturalFx.SpawnDerivedBeam(Projectile, Projectile.Center, dir * Math.Max(other.velocity.Length(), 11f), Math.Max(Projectile.damage, 1), 1.2f, 0.6f, theme: 0);
                Projectile.localAI[0]++;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item43 with { Volume = 0.3f, Pitch = -0.1f }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, new Color(180, 210, 240, 0), 0.05f).Configure(0.05f, 0.24f, 10);
                }
                return;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null) {
                Vector2 screen = Projectile.Center - Main.screenPosition;
                Main.spriteBatch.Draw(star, screen, null, new Color(190, 215, 240, 0), Projectile.rotation, star.Size() * 0.5f, 0.13f, SpriteEffects.None, 0f);
            }
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            float remain = MathHelper.Clamp(Projectile.localAI[0] < MaxRelays ? 1f : 0.4f, 0f, 1f);
            Vector2 screen = Projectile.Center - Main.screenPosition;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, screen, new Color(170, 200, 235, 0) * 0.5f * remain, new Color(80, 110, 150, 0) * 0.3f * remain, 0.4f, 0f, 2);
        }
    }
}
