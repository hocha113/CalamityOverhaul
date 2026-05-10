using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 积云枪管：光束留下可被后续 SHPC 弹幕充能的云核，满能后降下雷雨。
    /// </summary>
    internal sealed class CumulusBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(165, 215, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.10f;
            ctx.DamageMul += -0.6f;
            ctx.BeamLifeMul += 0.16f;
            ctx.ManaCostMul += 0.25f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if ((Main.GameUpdateCount + (uint)beam.Projectile.whoAmI) % 42 != 0) return;
            int damage = Math.Max(beam.Projectile.damage / 3, 1);
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center + Main.rand.NextVector2Circular(30f, 18f),
                Main.rand.NextVector2Circular(0.8f, 0.5f),
                ModContent.ProjectileType<SHPCCumulusNodeProj>(),
                damage, 0f, beam.Projectile.owner);
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.25f, Pitch = 0.4f }, beam.Projectile.Center);
            }
        }
    }

    /// <summary>
    /// 积云核心：用 9 张 Fog 椭圆叠出云团，DiffusionCircle 标识充能比例，满能时先导主闪电 + 3 道支线
    /// </summary>
    internal sealed class SHPCCumulusNodeProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        //9 个云块的固定相对位置/旋转/缩放（首帧 seed 后稳定，避免每帧抖动）
        private Vector2[] blobOffsets;
        private float[] blobRotations;
        private float[] blobScales;
        private float seedAngle;

        public override void SetDefaults() {
            Projectile.width = 72;
            Projectile.height = 44;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧种子化云块布局
            if (blobOffsets == null) {
                seedAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                blobOffsets = new Vector2[9];
                blobRotations = new float[9];
                blobScales = new float[9];
                for (int i = 0; i < 9; i++) {
                    float a = seedAngle + i * (MathHelper.TwoPi / 9f) + Main.rand.NextFloat(-0.3f, 0.3f);
                    float r = Main.rand.NextFloat(0.55f, 1f);
                    blobOffsets[i] = new Vector2(MathF.Cos(a) * 40f * r, MathF.Sin(a) * 24f * r);
                    blobRotations[i] = Main.rand.NextFloat(MathHelper.TwoPi);
                    blobScales[i] = Main.rand.NextFloat(0.55f, 0.95f);
                }
            }
            Projectile.velocity *= 0.94f;
            Projectile.localAI[0] = MathF.Min(Projectile.localAI[0] + PassiveCharge(), 100f);
            //充能 ≥ 80 时进入预放电期，飞溅微闪粒子
            if (Main.netMode != NetmodeID.Server && Projectile.localAI[0] > 80f && Main.GameUpdateCount % 4 == 0) {
                PRTLoader.AddParticle(new PRT_Sparkle(
                    Projectile.Center + Main.rand.NextVector2Circular(36f, 18f),
                    Main.rand.NextVector2Circular(0.6f, 0.6f),
                    new Color(220, 240, 255), new Color(120, 130, 200),
                    Main.rand.NextFloat(0.3f, 0.6f), Main.rand.Next(10, 20),
                    Main.rand.NextFloat(-0.2f, 0.2f), 0.7f));
            }
            else if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 6 == 0) {
                Color color = Projectile.localAI[0] > 70f ? new Color(210, 240, 255) : new Color(150, 190, 220);
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    Projectile.Center + Main.rand.NextVector2Circular(44f, 20f),
                    Main.rand.NextVector2Circular(0.5f, 0.5f),
                    color, new Color(90, 130, 170),
                    Main.rand.NextFloat(0.5f, 1.1f), Main.rand.Next(18, 34)));
            }
            if (Projectile.localAI[0] < 100f) return;
            if (Projectile.owner == Main.myPlayer) {
                ReleaseRain();
            }
            Projectile.Kill();
        }

        private float PassiveCharge() {
            float charge = 0.08f;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.owner != Projectile.owner || other.whoAmI == Projectile.whoAmI) continue;
                if (other.type != ModContent.ProjectileType<CyberTraceBeamProj>()
                    && other.type != ModContent.ProjectileType<CyberChargeOrbProj>()) continue;
                if (Vector2.DistanceSquared(other.Center, Projectile.Center) > 130f * 130f) continue;
                charge += 3.2f;
            }
            return charge;
        }

        private void ReleaseRain() {
            //先导主闪电：从云核到下方 220px，使用 CyberDataArc shader（自带噪声折线）
            int arcDmg = Math.Max(Projectile.damage * 2, 1);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDataArcProj>(),
                arcDmg, 0f, Projectile.owner,
                ai0: Main.rand.NextFloat(-30f, 30f), ai1: 220f);
            //三道支线 CyberTraceBeam
            for (int i = -1; i <= 1; i++) {
                Vector2 spawn = Projectile.Center + new Vector2(i * 36f, -10f);
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawn, Vector2.UnitY * 12f,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    Math.Max(Projectile.damage, 1), 0f, Projectile.owner, ai0: Main.rand.Next(3), ai1: 0.2f);
                if (idx >= 0 && idx < Main.maxProjectiles
                    && Main.projectile[idx].ModProjectile is CyberTraceBeamProj beam) {
                    beam.IsDerived = true;
                    beam.LifeMul = 0.35f;
                    beam.SpeedMul = 1.25f;
                }
            }
            //云核处与地面落点环
            if (Main.netMode != NetmodeID.Server) {
                PRTLoader.AddParticle(new PRT_StarPulseRing(
                    Projectile.Center, Vector2.Zero,
                    new Color(220, 240, 255, 0), 0.05f, 0.7f, 24));
                PRTLoader.AddParticle(new PRT_StarPulseRing(
                    Projectile.Center + Vector2.UnitY * 220f, Vector2.Zero,
                    new Color(180, 210, 255, 0), 0.05f, 0.55f, 22));
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = -0.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.4f, Pitch = 0.2f }, Projectile.Center);
            }
            SHPCNaturalFx.Shake(2.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog == null || blobOffsets == null) return false;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float charge01 = MathHelper.Clamp(Projectile.localAI[0] / 100f, 0f, 1f);
            //云体颜色：基础白，按充能向蓝白渐变
            Color cloudCore = Color.Lerp(new Color(245, 248, 255), new Color(200, 220, 255), charge01);
            Color cloudEdge = Color.Lerp(new Color(160, 165, 175), new Color(110, 130, 180), charge01);
            Vector2 fogOrigin = fog.Size() * 0.5f;
            //9 个 Fog 团块，每帧轻微飘移
            float drift = (float)Main.timeForVisualEffects * 0.02f;
            for (int i = 0; i < blobOffsets.Length; i++) {
                Vector2 offset = blobOffsets[i] + new Vector2(MathF.Sin(drift + i) * 1.6f, MathF.Cos(drift + i * 0.7f) * 1.2f);
                Color c = Color.Lerp(cloudCore, cloudEdge, i / 9f);
                Main.spriteBatch.Draw(fog, baseScreen + offset, null, c * 0.95f,
                    blobRotations[i] + drift * 0.3f, fogOrigin, blobScales[i], SpriteEffects.None, 0f);
            }
            //充能进度环：DiffusionCircle 染上代表充能的色
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            if (ring != null && charge01 > 0.05f) {
                Color ringCol = Color.Lerp(new Color(150, 200, 255), new Color(180, 130, 255), charge01) * (charge01 * 0.7f + 0.2f);
                ringCol.A = 0;
                Vector2 ringOrigin = ring.Size() * 0.5f;
                Main.spriteBatch.Draw(ring, baseScreen, null, ringCol,
                    drift * 1.5f, ringOrigin, 0.32f + charge01 * 0.18f, SpriteEffects.None, 0f);
            }
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float charge01 = MathHelper.Clamp(Projectile.localAI[0] / 100f, 0f, 1f);
            if (charge01 < 0.6f) return;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            //充能 ≥ 80 显出辉光（蓝白 → 雷紫）
            float t = (charge01 - 0.6f) / 0.4f;
            Color inner = Color.Lerp(new Color(200, 220, 255, 0), new Color(160, 130, 255, 0), t) * t;
            Color outer = Color.Lerp(new Color(80, 110, 180, 0), new Color(60, 30, 130, 0), t) * t * 0.6f;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen, inner, outer, 1.4f + t * 0.5f, 0f, 3);
            //准发亮电闪烁
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null && t > 0.65f) {
                float pulse = 0.6f + 0.4f * MathF.Sin((float)Main.timeForVisualEffects * 0.45f);
                Color starCol = new Color(220, 230, 255, 0) * t * pulse;
                Vector2 starOrigin = star.Size() * 0.5f;
                spriteBatch.Draw(star, baseScreen, null, starCol,
                    (float)Main.timeForVisualEffects * 0.05f, starOrigin, 0.35f * pulse, SpriteEffects.None, 0f);
            }
        }
    }
}
