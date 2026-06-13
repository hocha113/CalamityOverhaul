using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>高压核心：命中充压至 100kV，满压下次命中放电直线电弧</summary>
    internal sealed class HighVoltageCoreModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //高压电蓝
        public override Color TintColor => new(80, 180, 255);

        private const float VoltageCap = 100f;
        /// <summary>当前电压 0~100</summary>
        private float voltage;
        /// <summary>满压提示音是否已播放</summary>
        private bool fullPinged;

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += 0.08f;
            ctx.ManaCostMul += 0.35f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived) return;
            //满压时本次命中即为放电触发点
            if (voltage >= VoltageCap) {
                Discharge(beam.Projectile, target);
                return;
            }
            voltage = Math.Min(voltage + 12f, VoltageCap);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (voltage >= VoltageCap) {
                Discharge(laser.Projectile, target);
                return;
            }
            voltage = Math.Min(voltage + 5f, VoltageCap);
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            voltage = Math.Min(voltage + 50f, VoltageCap);
        }

        /// <summary>
        /// 高压放电：从玩家枪口穿过触发目标延伸 1300px 的电弧，命中线上所有敌人
        /// </summary>
        private void Discharge(Projectile source, NPC throughTarget) {
            voltage = 0f;
            fullPinged = false;
            if (source.owner != Main.myPlayer) return;
            Player owner = Main.player[source.owner];
            if (owner == null || !owner.active) return;

            Vector2 dir = (throughTarget.Center - owner.Center).SafeNormalize(Vector2.UnitX);
            int dmg = Math.Max(source.damage * 4, 1);
            Projectile.NewProjectile(source.GetSource_FromThis(),
                owner.Center + dir * 40f, dir,
                ModContent.ProjectileType<SHPCVoltArcProj>(),
                dmg, 6f, source.owner);
        }

        public override void OnPlayerUpdate(Player player) {
            if (voltage < VoltageCap) {
                fullPinged = false;
                return;
            }
            //满压状态：电火花在玩家周身爆跳 + 一次性提示音
            if (!fullPinged) {
                fullPinged = true;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.45f, Pitch = 0.6f }, player.Center);
                }
            }
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                Vector2 pos = player.Center + Main.rand.NextVector2Circular(26f, 30f);
                PRTLoader.NewParticle<PRT_Spark>(pos, Main.rand.NextVector2CircularEdge(3f, 3f),
                    new Color(140, 215, 255), Main.rand.NextFloat(0.5f, 1.0f)).Configure(true, Main.rand.Next(8, 16));
            }
        }
    }

    /// <summary>高压电弧折跳，前 10 帧伤害；SHPCVoltArc.fx</summary>
    internal sealed class SHPCVoltArcProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int Lifetime = 26;
        private const int DamageWindow = 10;
        private const float ArcLength = 1300f;
        private const float ArcHitWidth = 34f;
        private const int PointCount = 18;

        private static readonly Color ArcCore = new(225, 245, 255);
        private static readonly Color ArcGlow = new(70, 170, 255);
        private static readonly Color ArcAura = new(25, 45, 140);

        private Vector2[] arcPoints;
        private Trail trail;
        private Vector2 arcDir;
        private float arcSeed;
        private float fadeAlpha;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //每个敌人只被同一道电弧击中一次
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧：固定方向与随机种子，velocity 仅作为方向载体
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                arcDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                arcSeed = Main.rand.NextFloat(100f);
                Projectile.velocity = Vector2.Zero;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.6f, Pitch = 0.45f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
                    SpawnIonBurst();
                }
                SHPCNaturalFx.Shake(6f);
                RebuildArc();
            }

            int age = Lifetime - Projectile.timeLeft;
            //折点重掷：放电期间高频抖动，残辉期减慢
            if (age % 4 == 0) {
                RebuildArc();
            }

            fadeAlpha = age <= DamageWindow
                ? 1f
                : 1f - (age - DamageWindow) / (float)(Lifetime - DamageWindow);

            //沿弧线整路照明
            for (int i = 0; i < 5; i++) {
                Vector2 lightPos = Projectile.Center + arcDir * (ArcLength * i / 4f);
                Lighting.AddLight(lightPos, ArcGlow.ToVector3() * 0.8f * fadeAlpha);
            }

            //放电期沿线持续蹦出电火花
            if (age <= DamageWindow && Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 3; i++) {
                    float t = Main.rand.NextFloat();
                    Vector2 pos = Projectile.Center + arcDir * ArcLength * t
                        + arcDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-22f, 22f);
                    PRTLoader.NewParticle<PRT_Spark>(pos, Main.rand.NextVector2CircularEdge(4f, 4f),
                        new Color(150, 220, 255), Main.rand.NextFloat(0.5f, 1.1f)).Configure(true, Main.rand.Next(8, 18));
                }
            }
        }

        /// <summary>重建折跳路径，中段法线随机偏移</summary>
        private void RebuildArc() {
            arcPoints ??= new Vector2[PointCount];
            Vector2 normal = arcDir.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < PointCount; i++) {
                float t = i / (float)(PointCount - 1);
                float swing = MathF.Sin(t * MathHelper.Pi);
                float offset = Main.rand.NextFloat(-1f, 1f) * 30f * swing;
                arcPoints[i] = Projectile.Center + arcDir * ArcLength * t + normal * offset;
            }
        }

        private void SpawnIonBurst() {
            for (int i = 0; i < 14; i++) {
                Vector2 vel = arcDir.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(4f, 12f);
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, vel,
                    ArcCore, Main.rand.NextFloat(0.7f, 1.6f)).Configure(ArcGlow, Main.rand.Next(14, 26));
            }
        }

        public override bool? CanDamage() => Lifetime - Projectile.timeLeft <= DamageWindow;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(
                new Vector2(targetHitbox.X, targetHitbox.Y),
                new Vector2(targetHitbox.Width, targetHitbox.Height),
                Projectile.Center, Projectile.Center + arcDir * ArcLength, ArcHitWidth, ref _);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.4f, Pitch = 0.5f }, target.Center);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, new Color(170, 230, 255), Main.rand.NextFloat(0.7f, 1.4f)).Configure(true, Main.rand.Next(12, 22));
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, new Color(90, 180, 255, 0), 0.05f).Configure(0.05f, 0.42f, 18);
        }

        private float WidthFunction(float progress) {
            float endTaper = MathF.Sin(MathHelper.Clamp(progress * MathHelper.Pi, 0f, MathHelper.Pi));
            return (16f + endTaper * 22f) * MathHelper.Clamp(fadeAlpha + 0.2f, 0f, 1f);
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (arcPoints == null || fadeAlpha < 0.02f) return;
            Effect shader = EffectLoader.SHPCVoltArc?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            trail ??= new Trail(arcPoints, WidthFunction, ColorFunction);
            trail.TrailPositions = arcPoints;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["fadeAlpha"]?.SetValue(fadeAlpha);
            shader.Parameters["arcSeed"]?.SetValue(arcSeed);
            shader.Parameters["coreColor"]?.SetValue(ArcCore.ToVector3());
            shader.Parameters["glowColor"]?.SetValue(ArcGlow.ToVector3());
            shader.Parameters["auraColor"]?.SetValue(ArcAura.ToVector3());
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.02f) return;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            //两端电极光球
            Vector2 startScreen = Projectile.Center - Main.screenPosition;
            Vector2 endScreen = Projectile.Center + arcDir * ArcLength - Main.screenPosition;
            Vector2 origin = glow.Size() * 0.5f;
            spriteBatch.Draw(glow, startScreen, null, ArcGlow * fadeAlpha * 0.8f, 0f, origin, 1.5f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, startScreen, null, ArcCore * fadeAlpha * 0.9f, 0f, origin, 0.7f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, endScreen, null, ArcGlow * fadeAlpha * 0.5f, 0f, origin, 1.0f, SpriteEffects.None, 0f);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
