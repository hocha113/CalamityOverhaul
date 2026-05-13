using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>
    /// 棱镜激光枪管弹幕，持续跟随光标的静态光柱
    /// 从枪口延伸至光标位置，复用 CyberTraceBeam.fx 与 Trail 系统渲染
    /// 通过线段碰撞对路径上的敌人持续造成伤害
    /// </summary>
    internal class CyberPrismLaserProj : BaseHeldProj, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int PointCount = 24;
        private const float MaxRange = 1600f;
        private const float BeamHitWidth = 18f;

        //激光专属配色：赛博紫罗兰
        private static readonly Vector3 LaserCoreVec = new Color(220, 160, 255).ToVector3();
        private static readonly Vector3 LaserGlowVec = new Color(140, 60, 220).ToVector3();
        private static readonly Vector3 LaserAuraVec = new Color(60, 20, 120).ToVector3();

        //超驱配色（与 CyberTraceBeamProj 保持一致，高温红炽）
        private static readonly Vector3 OdCoreVec = new Color(255, 255, 220).ToVector3();
        private static readonly Vector3 OdGlowVec = new Color(255, 40, 15).ToVector3();
        private static readonly Vector3 OdAuraVec = new Color(200, 10, 0).ToVector3();

        private static readonly Color LaserParticleMain = new Color(200, 140, 255);
        private static readonly Color LaserParticleEdge = new Color(120, 40, 220);
        private static readonly Color OdParticleMain = new Color(255, 200, 50);
        private static readonly Color OdParticleEdge = new Color(255, 30, 5);

        private Trail trail;
        private Vector2[] laserPoints;
        private Vector2 beamEnd;
        private float fadeAlpha;
        private float age;
        private int particleTimer;
        private float overdriveAmount;

        //═════════════ 改件行为注入字段 ═════════════
        //由 SHPCOverride.On_Shoot 在 NewProjectile 之后直接写入

        /// <summary>脑冲爆炸帧间隔（0=关闭）</summary>
        public int PulseInterval;
        /// <summary>脑冲爆炸半径（像素）</summary>
        public float PulseRadius = 80f;
        /// <summary>命中时是否施加炉灼类 debuff</summary>
        public bool ScorchOnHit;
        /// <summary>炉灼持续帧数</summary>
        public int ScorchDuration;

        private int pulseTimer;

        //每帧由改件 OnLaserAI 写入，绘制时消费；每帧 AI 开始时重置为默认紫罗兰配色
        public Color ThemeCore = new(220, 160, 255);
        public Color ThemeGlow = new(140, 60, 220);
        public Color ThemeAura = new(60, 20, 120);
        public Color ThemeParticleMain = new(200, 140, 255);
        public Color ThemeParticleEdge = new(120, 40, 220);

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (fadeAlpha < 0.15f || beamEnd == Vector2.Zero) return false;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(
                new Vector2(targetHitbox.X, targetHitbox.Y),
                new Vector2(targetHitbox.Width, targetHitbox.Height),
                Projectile.Center, beamEnd, BeamHitWidth, ref _);
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || !Owner.channel || Owner.GetItem().type != SHPCOverride.ID || Owner.statMana <= 0) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 10;

            //跟随枪口，沿瞄准方向偏移到枪口前端
            Vector2 aimDir = UnitToMouseV.SafeNormalize(Vector2.UnitX);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + aimDir * 80f + aimDir.GetNormalVector() * 12 * Math.Sign(aimDir.X);
            Projectile.rotation = aimDir.ToRotation();
            Projectile.velocity = Vector2.Zero;

            //光束终点：鼠标位置，最大射程限制
            beamEnd = Projectile.Center + aimDir * MaxRange;

            //均匀填充顶点数组（Trail 沿直线渲染光柱）
            laserPoints ??= new Vector2[PointCount];
            for (int i = 0; i < PointCount; i++) {
                float t = (float)i / (PointCount - 1);
                laserPoints[i] = Vector2.Lerp(Projectile.Center, beamEnd, t);
            }

            //渐入
            age++;
            fadeAlpha = MathHelper.Clamp(age / 8f, 0f, 1f);

            //超驱检测：仅看"主人玩家自己的领域"，避免你的激光在别人领域里也触发超驱
            bool inDomain = Cyberspace.IsInsideDomainOf(Projectile.owner, Projectile.Center);
            overdriveAmount = MathHelper.Lerp(overdriveAmount, inDomain ? 1f : 0f, 0.06f);
            if (overdriveAmount < 0.005f) overdriveAmount = 0f;

            //每帧重置颜色主题，允许 OnLaserAI 钩子按需覆写
            ThemeCore = new Color(220, 160, 255);
            ThemeGlow = new Color(140, 60, 220);
            ThemeAura = new Color(60, 20, 120);
            ThemeParticleMain = new Color(200, 140, 255);
            ThemeParticleEdge = new Color(120, 40, 220);

            //动态光照
            float intensity = fadeAlpha * (0.7f + overdriveAmount * 0.6f);
            Color lightCol = Color.Lerp(new Color(180, 100, 255), new Color(255, 180, 100), overdriveAmount);
            Lighting.AddLight(Projectile.Center, lightCol.ToVector3() * intensity);
            Lighting.AddLight(beamEnd, lightCol.ToVector3() * intensity * 0.75f);
            Lighting.AddLight((Projectile.Center + beamEnd) * 0.5f, lightCol.ToVector3() * intensity * 0.45f);

            if (Main.netMode != NetmodeID.Server) {
                particleTimer++;
                if (particleTimer >= 2) {
                    particleTimer = 0;
                    SpawnLaserParticles(aimDir);
                }
            }

            //脑冲定时器：每隔 PulseInterval 帧在终点引爆一次小爆炸
            if (PulseInterval > 0 && beamEnd != Vector2.Zero) {
                pulseTimer++;
                if (pulseTimer >= PulseInterval) {
                    pulseTimer = 0;
                    if (Projectile.owner == Main.myPlayer) {
                        SpawnPulseExplosion();
                    }
                }
            }

            SHPCModificationSystem.ForEachModule(Owner, mod => mod.OnLaserAI(this));
        }

        private void SpawnPulseExplosion() {
            int dmg = Math.Max(Projectile.damage, 1);
            int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                beamEnd, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, Projectile.owner,
                ai0: 0.5f, ai1: overdriveAmount);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[2] = PulseRadius;
            }
        }

        private void SpawnLaserParticles(Vector2 aimDir) {
            Vector2 perp = aimDir.RotatedBy(MathHelper.PiOver2);
            float od = overdriveAmount;
            Color mainCol = Color.Lerp(ThemeParticleMain, OdParticleMain, od);
            Color edgeCol = Color.Lerp(ThemeParticleEdge, OdParticleEdge, od);

            //沿光束随机位置散出少量粒子
            for (int i = 0; i < 2; i++) {
                float t = Main.rand.NextFloat();
                Vector2 pos = Vector2.Lerp(Projectile.Center, beamEnd, t);
                pos += perp * Main.rand.NextFloat(-10f, 10f);
                Vector2 vel = perp * Main.rand.NextFloat(-2.5f, 2.5f)
                    + aimDir * Main.rand.NextFloat(-0.5f, 0.5f);
                PRTLoader.AddParticle(new PRT_CyberSquare(pos, vel, mainCol, edgeCol,
                    Main.rand.NextFloat(0.4f, 1.1f), Main.rand.Next(8, 22)));
            }

            //终点冲击光晕粒子
            if (Main.rand.NextBool(3)) {
                Vector2 endVel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    beamEnd + Main.rand.NextVector2Circular(6f, 6f),
                    endVel, mainCol, edgeCol,
                    Main.rand.NextFloat(0.7f, 1.6f), Main.rand.Next(6, 16)));
            }
        }

        private float WidthFunction(float progress) {
            //两端微收（progress=0 为枪口，progress=1 为光束终端），中段保持均匀宽度形成光柱感
            float taper = MathF.Sin(MathHelper.Clamp(progress * MathHelper.Pi, 0f, MathHelper.Pi));
            taper = 0.6f + 0.4f * taper;
            float pulse = 1f + 0.07f * MathF.Sin((float)Main.timeForVisualEffects * 0.22f + progress * 5f);
            return taper * pulse * (26f + overdriveAmount * 18f);
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (laserPoints == null || fadeAlpha < 0.01f) return;

            Effect shader = EffectLoader.CyberTraceBeam?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            trail ??= new Trail(laserPoints, WidthFunction, ColorFunction);
            trail.TrailPositions = laserPoints;

            float od = overdriveAmount;
            CyberspacePlayer ownerCp = Cyberspace.For(Projectile.owner);
            float timeVal = ownerCp != null && ownerCp.Active
                ? ownerCp.EffectTime
                : (float)Main.timeForVisualEffects * 0.04f;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue(timeVal);
            shader.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(fadeAlpha, 0f, 1f));
            shader.Parameters["coreColor"]?.SetValue(Vector3.Lerp(ThemeCore.ToVector3(), OdCoreVec, od));
            shader.Parameters["glowColor"]?.SetValue(Vector3.Lerp(ThemeGlow.ToVector3(), OdGlowVec, od));
            shader.Parameters["auraColor"]?.SetValue(Vector3.Lerp(ThemeAura.ToVector3(), OdAuraVec, od));
            shader.Parameters["uNoiseTex"]?.SetValue(noise);
            shader.Parameters["overdriveAmount"]?.SetValue(od);
            shader.Parameters["glitchBurst"]?.SetValue(0f);
            shader.Parameters["odCoreColor"]?.SetValue(OdCoreVec);
            shader.Parameters["odGlowColor"]?.SetValue(OdGlowVec);
            shader.Parameters["odAuraColor"]?.SetValue(OdAuraVec);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.01f || beamEnd == Vector2.Zero) return;

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;

            float od = overdriveAmount;
            float pulse = 1f + 0.12f * MathF.Sin((float)Main.timeForVisualEffects * 0.18f);
            float alpha = fadeAlpha * pulse;
            Vector2 glowOrigin = glow.Size() * 0.5f;

            Color auraCol = Color.Lerp(ThemeAura, new Color(200, 10, 0), od);
            Color coreCol = Color.Lerp(ThemeCore, new Color(255, 255, 200), od);

            //终端聚焦光晕（两层：外层柔和光晕+内层核心亮点）
            Vector2 endScreen = beamEnd - Main.screenPosition;
            spriteBatch.Draw(glow, endScreen, null, auraCol * alpha * 0.4f, 0f,
                glowOrigin, 3.2f + od * 2.5f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, endScreen, null, coreCol * alpha * 0.75f, 0f,
                glowOrigin, 1.4f + od * 0.8f, SpriteEffects.None, 0f);

            //枪口起点光晕
            Vector2 startScreen = Projectile.Center - Main.screenPosition;
            spriteBatch.Draw(glow, startScreen, null, auraCol * alpha * 0.28f, 0f,
                glowOrigin, 1.6f, SpriteEffects.None, 0f);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            float od = overdriveAmount;
            Color mainCol = Color.Lerp(ThemeParticleMain, OdParticleMain, od);
            Color edgeCol = Color.Lerp(ThemeParticleEdge, OdParticleEdge, od);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.3f, Pitch = 0.5f }, target.Center);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f + od * 4f, 4f + od * 4f);
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    target.Center + vel, vel, mainCol, edgeCol,
                    Main.rand.NextFloat(0.6f, 1.6f), Main.rand.Next(12, 28)));
            }
            if (ScorchOnHit && ScorchDuration > 0 && Projectile.owner == Main.myPlayer) {
                target.AddBuff(ModContent.BuffType<HellburnBuff>(), ScorchDuration);
            }
            SHPCModificationSystem.ForEachModule(Owner, mod => mod.OnLaserHitNPC(this, target, hit, damageDone));
        }

        public override void OnKill(int timeLeft) {
            SHPCModificationSystem.ForEachModule(Owner, mod => mod.OnLaserKill(this));
            if (Main.netMode == NetmodeID.Server || beamEnd == Vector2.Zero) return;
            float od = overdriveAmount;
            Color mainCol = Color.Lerp(ThemeParticleMain, OdParticleMain, od);
            Color edgeCol = Color.Lerp(ThemeParticleEdge, OdParticleEdge, od);
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f + od * 4f, 4f + od * 4f);
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    beamEnd + Main.rand.NextVector2Circular(16f, 16f), vel,
                    mainCol, edgeCol,
                    Main.rand.NextFloat(0.5f, 1.5f), Main.rand.Next(12, 30)));
            }
        }

        public override bool ShouldUpdatePosition() => false;
    }
}
