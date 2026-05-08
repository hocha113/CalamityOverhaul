using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>
    /// 级联节点：5次命中后在命中点悬停，周期性向最近敌人猎杀式射出光束
    /// <br/>悬停约2秒，共触发5次猎杀；消亡时爆散橙金粒子
    /// </summary>
    internal class CyberCascadeNodeProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        //总寿命帧数（约2.2秒）
        private const int Lifetime = 130;
        //猎杀射击间隔（帧）
        private const int FireInterval = 24;
        //最多射击次数
        private const int MaxFires = 5;
        //猎杀搜索半径（像素）
        private const float SearchRange = 420f;

        //记录已触发射击次数
        private int _fireCount;
        //射击倒计时
        private int _fireTimer;
        //收缩爆发动画计时器（0=无，>0逐帧衰减）
        private float _flashIntensity;
        //轨道旋转角
        private float _orbitAngle;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            //不移动，锚定在生成位置
            Projectile.velocity = Vector2.Zero;

            float lifeRatio = (float)Projectile.timeLeft / Lifetime;
            _orbitAngle += 0.055f;
            _flashIntensity *= 0.75f;

            //首帧：启动计时器并播放出现音效
            if (Projectile.localAI[0] == 0f) {
                _fireTimer = FireInterval / 2; //半个间隔后首次射击，快速建立存在感
                Projectile.localAI[0] = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    SpawnAppearBurst();
                    SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.5f, Pitch = 0.6f }, Projectile.Center);
                }
            }

            //周期射击
            if (_fireCount < MaxFires) {
                _fireTimer--;
                if (_fireTimer <= 0) {
                    _fireTimer = FireInterval;
                    if (Projectile.owner == Main.myPlayer) {
                        TryFireAtNearest();
                    }
                }
            }

            //持续轨道粒子（仅本地视觉）
            if (Main.netMode != NetmodeID.Server && Projectile.timeLeft > 8) {
                SpawnOrbitParticles(lifeRatio);
            }

            //寿命临近时加速粒子爆散，然后结束
            if (Projectile.timeLeft == 8 && Main.netMode != NetmodeID.Server) {
                SpawnDeathBurst();
            }

            //微弱光照
            float glow = (0.7f + 0.3f * MathF.Sin(_orbitAngle * 2f)) * MathF.Min(lifeRatio * 8f, 1f);
            Lighting.AddLight(Projectile.Center, new Vector3(1.0f, 0.75f, 0.15f) * glow * 0.6f);
        }

        private void TryFireAtNearest() {
            NPC target = Projectile.Center.FindClosestNPC(SearchRange, false, true);
            if (target == null) return;

            _fireCount++;
            _flashIntensity = 1f;

            int dmg = Math.Max(Projectile.damage, 1);
            Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            int idx = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center, dir * 14f,
                ModContent.ProjectileType<CyberTraceBeamProj>(),
                dmg, 0f, Projectile.owner,
                ai0: Main.rand.Next(3));
            if (idx >= 0 && idx < Main.maxProjectiles
                && Main.projectile[idx].ModProjectile is CyberTraceBeamProj beam) {
                beam.IsDerived = true;
                beam.LifeMul = 0.7f;
                //强追踪，确保猎杀感
                Main.projectile[idx].ai[1] = 2.2f;
            }

            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.4f, Pitch = 0.4f }, Projectile.Center);
                //射击方向闪光粒子
                for (int i = 0; i < 5; i++) {
                    Vector2 vel = dir.RotatedByRandom(0.5f) * Main.rand.NextFloat(4f, 9f);
                    PRTLoader.AddParticle(new PRT_CyberSquare(
                        Projectile.Center, vel,
                        new Color(255, 200, 60), new Color(255, 120, 10),
                        Main.rand.NextFloat(0.8f, 1.8f), Main.rand.Next(10, 20)));
                }
            }
        }

        private void SpawnOrbitParticles(float lifeRatio) {
            //3条轨道臂，每条间隔120°
            float fade = MathF.Min(lifeRatio * 6f, 1f) * MathF.Min((1f - lifeRatio) * 6f, 1f);
            if (fade < 0.05f) return;

            int armsCount = 3;
            for (int arm = 0; arm < armsCount; arm++) {
                float ang = _orbitAngle + arm * MathHelper.TwoPi / armsCount;
                float radius = 18f + _flashIntensity * 8f;
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * radius;
                Vector2 vel = ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.5f
                    + Main.rand.NextVector2Circular(0.5f, 0.5f);
                if (Main.rand.NextBool(3)) {
                    PRTLoader.AddParticle(new PRT_CyberSquare(
                        pos, vel,
                        Color.Lerp(new Color(255, 200, 60), new Color(255, 120, 10), Main.rand.NextFloat()),
                        new Color(255, 80, 0),
                        Main.rand.NextFloat(0.5f, 1.1f) * fade,
                        Main.rand.Next(6, 14)));
                }
            }
        }

        private void SpawnAppearBurst() {
            for (int i = 0; i < 14; i++) {
                float ang = MathHelper.TwoPi * i / 14f;
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(2f, 6f);
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    Projectile.Center, vel,
                    new Color(255, 210, 80), new Color(255, 140, 20),
                    Main.rand.NextFloat(0.8f, 2.0f), Main.rand.Next(12, 24)));
            }
        }

        private void SpawnDeathBurst() {
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f + Main.rand.NextFloat(4f), 3f + Main.rand.NextFloat(4f));
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    Projectile.Center, vel,
                    Color.Lerp(new Color(255, 200, 60), new Color(255, 80, 0), Main.rand.NextFloat()),
                    new Color(200, 60, 0),
                    Main.rand.NextFloat(0.6f, 1.6f), Main.rand.Next(15, 30)));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.netMode == NetmodeID.Server) return false;

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return false;

            float lifeRatio = (float)Projectile.timeLeft / Lifetime;
            float fadeIn = MathF.Min(lifeRatio * 8f, 1f);         //前12帧淡入
            float fadeOut = MathF.Min((1f - lifeRatio) * 8f, 1f); //后12帧淡出
            float alpha = fadeIn * fadeOut;

            //核心脉冲（略带跳动感）
            float pulse = 0.85f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.18f)
                        + _flashIntensity * 0.35f;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = glow.Size() * 0.5f;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);

            //外层柔和光晕（橙金大光晕）
            float outerScale = 1.6f * pulse;
            Color outerColor = new Color(255, 140, 20, 0) * alpha * 0.35f;
            Main.spriteBatch.Draw(glow, drawPos, null, outerColor, 0f, origin, outerScale, SpriteEffects.None, 0f);

            //中层（橙白核心）
            float midScale = 0.9f * pulse;
            Color midColor = new Color(255, 220, 100, 0) * alpha * 0.7f;
            Main.spriteBatch.Draw(glow, drawPos, null, midColor, 0f, origin, midScale, SpriteEffects.None, 0f);

            //内核（纯白热点）
            float coreScale = 0.3f * pulse;
            Color coreColor = new Color(255, 255, 240, 0) * alpha;
            Main.spriteBatch.Draw(glow, drawPos, null, coreColor, 0f, origin, coreScale, SpriteEffects.None, 0f);

            //射击闪光环（_flashIntensity > 0 时出现）
            if (_flashIntensity > 0.05f) {
                float flashScale = (0.5f + (1f - _flashIntensity) * 1.0f) * alpha;
                Color flashColor = new Color(255, 200, 60, 0) * _flashIntensity * alpha;
                Main.spriteBatch.Draw(glow, drawPos, null, flashColor, 0f, origin, flashScale, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        public override bool ShouldUpdatePosition() => false;
    }
}
