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
    /// <summary>
    /// 月露枪管：光束凝结露珠棱镜，后续光束触碰后被折射成短程派生束。
    /// </summary>
    internal sealed class MoondewBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(185, 220, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.12f;
            ctx.DamageMul += -0.06f;
            ctx.BeamLifeMul += 0.10f;
            ctx.CritAdd += 5;
            ctx.ManaCostMul += 0.3f;
        }

        //同主同时存在的月露棱镜上限
        private const int MaxConcurrentPrisms = 4;
        //同点 130px 内已有棱镜则跳过本次生成
        private const float MinSpacing = 130f;

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            //夜晚 + 上半月相加快节奏，普通时段更稀疏
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

    /// <summary>
    /// 月露棱镜
    /// </summary>
    internal sealed class SHPCMoondewPrismProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

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

        //折射扫描节流：每 4 帧才扫一次全弹幕表
        private const int RefractScanInterval = 4;

        public override void AI() {
            Projectile.rotation += 0.03f;
            int frame = (int)Main.GameUpdateCount + Projectile.whoAmI;
            if (frame % RefractScanInterval == 0
                && Projectile.owner == Main.myPlayer
                && Projectile.localAI[0] < MaxRefractions()) {
                TryRefractBeam();
            }
            //偶发月华火星（节流到 12 帧）
            if (Main.netMode == NetmodeID.Server || Main.GameUpdateCount % 12 != 0) return;
            PRTLoader.AddParticle(new PRT_Sparkle(
                Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                Main.rand.NextVector2Circular(0.5f, 0.5f),
                new Color(220, 240, 255), new Color(120, 170, 230),
                Main.rand.NextFloat(0.3f, 0.65f), Main.rand.Next(16, 28),
                Main.rand.NextFloat(-0.15f, 0.15f), 0.7f));
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
                //追加一发短 Trail 闪光，纯视觉，3 帧寿命
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir,
                    ModContent.ProjectileType<SHPCMoondewRefractFlashProj>(), 0, 0f, Projectile.owner);
                Projectile.localAI[0]++;
                Projectile.timeLeft -= 30;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item101 with { Volume = 0.55f, Pitch = 0.4f }, Projectile.Center);
                    PRTLoader.AddParticle(new PRT_StarPulseRing(
                        Projectile.Center, Vector2.Zero,
                        new Color(220, 240, 255, 0), 0.05f, 0.3f, 14));
                }
                return;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float drift = (float)Main.timeForVisualEffects * 0.04f;
            float pulse = 0.85f + 0.15f * MathF.Sin(drift * 4f);

            //主体星纹
            if (star != null) {
                Vector2 starOrigin = star.Size() * 0.5f;
                Main.spriteBatch.Draw(star, baseScreen, null,
                    new Color(220, 240, 255, 0) * pulse, Projectile.rotation, starOrigin, 0.18f, SpriteEffects.None, 0f);
            }
            //满折射夜晚：淡黄月相光环
            if (MaxRefractions() == 3) {
                Texture2D cyclone = CWRAsset.Cyclone?.Value;
                if (cyclone != null) {
                    Vector2 cycOrigin = cyclone.Size() * 0.5f;
                    Color halo = new Color(255, 245, 200, 0) * 0.3f * pulse;
                    Main.spriteBatch.Draw(cyclone, baseScreen, null, halo, drift * 0.6f, cycOrigin, 0.55f, SpriteEffects.None, 0f);
                }
            }
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            //RGB 三色微偏（仿色散）
            Color rCol = new Color(255, 80, 80, 0) * 0.5f;
            Color gCol = new Color(80, 255, 140, 0) * 0.5f;
            Color bCol = new Color(140, 180, 255, 0) * 0.5f;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen + new Vector2(-2f, 0f), rCol, rCol * 0.4f, 0.6f, 0f, 2);
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen + new Vector2(2f, 0f), gCol, gCol * 0.4f, 0.6f, 0f, 2);
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen + new Vector2(0f, 2f), bCol, bCol * 0.4f, 0.6f, 0f, 2);
            //叠 1 层白色核心
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen,
                new Color(220, 240, 255, 0) * 0.7f,
                new Color(120, 160, 220, 0) * 0.4f, 0.5f, 0f, 2);
        }
    }

    /// <summary>
    /// 月露折射闪光：3 段折线 Trail（CyberDataArc shader），3 帧寿命，纯视觉装饰
    /// </summary>
    internal sealed class SHPCMoondewRefractFlashProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

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
            //3 段折线，沿 velocity 方向延伸 60px，附加垂直噪声
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            points = new Vector2[5];
            for (int i = 0; i < points.Length; i++) {
                float t = i / (float)(points.Length - 1);
                float taper = MathF.Sin(t * MathHelper.Pi);
                points[i] = Projectile.Center + dir * t * 60f + perp * Main.rand.NextFloat(-6f, 6f) * taper;
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
