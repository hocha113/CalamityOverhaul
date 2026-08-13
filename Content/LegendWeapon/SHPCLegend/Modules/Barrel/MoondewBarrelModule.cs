using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>月露枪管，凝露珠棱镜，后续束折射短程派生</summary>
    internal sealed class MoondewBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(185, 220, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.15f;
            ctx.DamageMul += -0.08f;
            ctx.BeamLifeMul += 0.08f;
            ctx.CritAdd += 4;
            ctx.ManaCostMul += 0.36f;
        }

        //同主棱镜上限
        private const int MaxConcurrentPrisms = 4;
        //同点80px内已有则跳过
        private const float MinSpacing = 80f;

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            //夜+上半月加快节奏
            int interval = !Main.dayTime && Main.moonPhase <= 2 ? 36 : 60;
            if ((Main.GameUpdateCount + (uint)beam.Projectile.whoAmI) % (uint)interval != 0) return;
            int prismType = ModContent.ProjectileType<SHPCMoondewPrismProj>();
            if (SHPCNaturalFx.CountOwned(beam.Projectile.owner, prismType) >= MaxConcurrentPrisms) return;
            if (SHPCNaturalFx.HasOwnedNear(beam.Projectile.owner, prismType, beam.Projectile.Center, MinSpacing)) return;
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center, Vector2.Zero,
                prismType, Math.Max(beam.Projectile.damage / 2, 1), 0f, beam.Projectile.owner);
        }
    }

    /// <summary>月露棱镜</summary>
    internal sealed class SHPCMoondewPrismProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 210;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        //折射扫描，每4帧
        private const int RefractScanInterval = 4;
        //凝珠包络帧数
        private const int CondenseFrames = 10;

        private bool inited;

        //localAI[1] 折射挤压包络 8→0，闪光弹幕首帧回写，远端同演

        public override void AI() {
            if (!inited) {
                inited = true;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
                }
            }
            if (Projectile.localAI[1] > 0f) {
                Projectile.localAI[1] -= 1f;
            }
            int frame = (int)Main.GameUpdateCount + Projectile.whoAmI;
            if (frame % RefractScanInterval == 0
                && Projectile.owner == Main.myPlayer
                && Projectile.localAI[0] < MaxRefractions()) {
                TryRefractBeam();
            }
            //月华火星，12帧节流
            if (Main.netMode == NetmodeID.Server || Main.GameUpdateCount % 12 != 0) return;
            PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f), Main.rand.NextVector2Circular(0.5f, 0.5f), new Color(220, 240, 255), Main.rand.NextFloat(0.3f, 0.65f)).Configure(new Color(120, 170, 230), Main.rand.Next(16, 28), Main.rand.NextFloat(-0.15f, 0.15f), 0.7f);
        }

        public override void OnKill(int timeLeft) {
            //破珠余韵，露滴带重力散落
            if (Main.dedServ) return;
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 3; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-1.7f, 1.7f), Main.rand.NextFloat(-0.6f, 1.3f));
                PRTLoader.NewParticle<PRT_SHPCMoondewDrop>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), vel,
                    new Color(205, 235, 255), Main.rand.NextFloat(0.7f, 1.1f)).Configure(Main.rand.Next(24, 36));
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, new Color(210, 235, 255), 0.04f).Configure(0.04f, 0.22f, 12);
        }

        private int MaxRefractions() {
            bool moonFavored = !Main.dayTime && Main.moonPhase <= 2;
            return moonFavored ? 3 : 1;
        }

        private void TryRefractBeam() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.owner != Projectile.owner) continue;
                if (other.type != ModContent.ProjectileType<CyberTraceBeamProj>()) continue;
                if (Vector2.DistanceSquared(other.Center, Projectile.Center) > 42f * 42f) continue;
                Vector2 dir = other.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f));
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 13f,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    Math.Max(Projectile.damage, 1), 0f, Projectile.owner, ai0: Main.rand.Next(3), ai1: 1.8f);
                if (idx >= 0 && idx < Main.maxProjectiles
                    && Main.projectile[idx].ModProjectile is CyberTraceBeamProj beam) {
                    beam.IsDerived = true;
                    beam.LifeMul = 0.32f;
                }
                //短 Trail 闪光，纯视觉；折射拍的音效/闪环/挤压走它首帧，各端同演
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir,
                    ModContent.ProjectileType<SHPCMoondewRefractFlashProj>(), 0, 0f, Projectile.owner);
                Projectile.localAI[0]++;
                Projectile.timeLeft -= 30;
                return;
            }
        }

        //张力摆动+折射挤压，露珠不自旋
        private Vector2 BodySquash(out float bodyScale) {
            float wob = MathF.Sin((float)Main.timeForVisualEffects * 0.115f + Projectile.whoAmI * 1.7f) * 0.06f;
            float squeeze = Projectile.localAI[1] / 8f;
            float sq = squeeze * squeeze * 0.22f;
            float spawnT = MathHelper.Clamp((210 - Projectile.timeLeft) / (float)CondenseFrames, 0f, 1f);
            //凝珠自小胀足带一次过冲
            bodyScale = MathHelper.SmoothStep(0.3f, 1f, spawnT) * (1f + MathF.Sin(spawnT * MathHelper.Pi) * 0.14f);
            return new Vector2(1f + wob + sq, 1f - wob - sq);
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float drift = (float)Main.timeForVisualEffects * 0.04f;
            float pulse = 0.85f + 0.15f * MathF.Sin(drift * 4f);
            Vector2 squash = BodySquash(out float bodyScale);

            //珠底水膜，AlphaBlend 预乘批 A=0 即加色
            Texture2D disc = CWRAsset.DiffusionCircle?.Value;
            if (disc != null) {
                Color filmCol = new Color(190, 225, 255, 0) * (0.5f * pulse * bodyScale);
                Main.spriteBatch.Draw(disc, baseScreen, null, filmCol,
                    0f, disc.Size() * 0.5f, 0.155f * bodyScale * squash, SpriteEffects.None, 0f);
                //下缘内反射月牙，取环下半，重力把亮弧压在珠底
                Rectangle lowerHalf = new(0, disc.Height / 2, disc.Width, disc.Height / 2);
                Vector2 arcOrigin = new(disc.Width * 0.5f, 0f);
                Color arcCol = new Color(238, 248, 255, 0) * (0.85f * bodyScale);
                Main.spriteBatch.Draw(disc, baseScreen + new Vector2(0f, 1.5f), lowerHalf, arcCol,
                    0f, arcOrigin, 0.12f * bodyScale * squash, SpriteEffects.None, 0f);
            }
            //镜面高光点，环境光方向恒定不随珠转
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null) {
                Vector2 starOrigin = star.Size() * 0.5f;
                float wobble = MathF.Sin((float)Main.timeForVisualEffects * 0.115f + Projectile.whoAmI * 1.7f);
                Main.spriteBatch.Draw(star, baseScreen + new Vector2(-3.5f, -4.5f) * bodyScale, null,
                    new Color(235, 245, 255, 0) * (0.9f * pulse * bodyScale),
                    -MathHelper.PiOver4 + wobble * 0.12f, starOrigin, 0.09f * bodyScale, SpriteEffects.None, 0f);
            }
            //满折射夜，月相光环
            if (MaxRefractions() == 3) {
                Texture2D cyclone = CWRAsset.Cyclone?.Value;
                if (cyclone != null) {
                    Vector2 cycOrigin = cyclone.Size() * 0.5f;
                    Color halo = new Color(255, 245, 200, 0) * (0.3f * pulse * bodyScale);
                    Main.spriteBatch.Draw(cyclone, baseScreen, null, halo, drift * 0.6f, cycOrigin, 0.55f, SpriteEffects.None, 0f);
                }
            }
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            //真加色批源因子是 SourceAlpha，染色必须带 A
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            Vector2 squash = BodySquash(out float bodyScale);
            bool moonFavored = MaxRefractions() == 3;
            float disp = moonFavored ? 4.5f : 3.5f;
            float glowScale = 0.55f * bodyScale;
            //折光三芒，R/G/B 沿光路错位
            Color rCol = new Color(255, 90, 90) * 0.28f;
            Color gCol = new Color(90, 255, 150) * 0.28f;
            Color bCol = new Color(140, 185, 255) * 0.3f;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen + new Vector2(-disp, 0f) * squash, rCol, rCol * 0.4f, glowScale, 0f, 2);
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen + new Vector2(disp, 0f) * squash, gCol, gCol * 0.4f, glowScale, 0f, 2);
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen + new Vector2(0f, disp) * squash, bCol, bCol * 0.4f, glowScale, 0f, 2);
            //月白核
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen,
                new Color(220, 240, 255) * (0.42f * bodyScale),
                new Color(120, 160, 220) * (0.2f * bodyScale), 0.42f * bodyScale, 0f, 2);
        }
    }

    /// <summary>月露折射闪光，折线 Trail，纯视觉</summary>
    internal sealed class SHPCMoondewRefractFlashProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int MaxLife = 5;
        private static readonly Vector3 CoreVec = new Color(220, 240, 255).ToVector3();
        private static readonly Vector3 GlowVec = new Color(120, 200, 255).ToVector3();

        private Vector2[] points;
        private Trail trail;

        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (points != null) return;
            //折线沿速度延60px+垂噪
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            points = new Vector2[5];
            for (int i = 0; i < points.Length; i++) {
                float t = i / (float)(points.Length - 1);
                float taper = MathF.Sin(t * MathHelper.Pi);
                points[i] = Projectile.Center + dir * t * 60f + perp * Main.rand.NextFloat(-6f, 6f) * taper;
            }
            //折射拍各端自演：棱音+闪环+回写棱镜挤压包络
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item101 with { Volume = 0.55f, Pitch = 0.4f }, Projectile.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, new Color(220, 240, 255), 0.05f).Configure(0.05f, 0.3f, 14);
                int prismType = ModContent.ProjectileType<SHPCMoondewPrismProj>();
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (!p.active || p.owner != Projectile.owner || p.type != prismType) continue;
                    if (Vector2.DistanceSquared(p.Center, Projectile.Center) > 20f * 20f) continue;
                    p.localAI[1] = 8f;
                    break;
                }
            }
        }

        private float WidthFunction(float progress) {
            float taper = MathF.Sin(MathHelper.Clamp(progress * MathHelper.Pi, 0f, MathHelper.Pi));
            float life = Projectile.timeLeft / (float)MaxLife;
            return taper * 6f * life;
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (points == null) return;
            Effect shader = EffectLoader.CyberDataArc?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.ThunderTrail?.Value ?? CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            trail ??= new Trail(points, WidthFunction, ColorFunction);
            trail.TrailPositions = points;

            float life = Projectile.timeLeft / (float)MaxLife;
            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.06f);
            shader.Parameters["fadeAlpha"]?.SetValue(life);
            shader.Parameters["coreColor"]?.SetValue(CoreVec);
            shader.Parameters["glowColor"]?.SetValue(GlowVec);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
