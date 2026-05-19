using CalamityOverhaul.Common;
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
    /// 赛博爆破特效弹幕
    /// <br/>由 CyberChargeOrbProj 命中时生成，使用 CyberDetonation.fx 着色器
    /// <br/>以全屏四边形渲染科技感爆破冲击环
    /// <br/>对范围内敌人造成 AOE 伤害
    /// </summary>
    internal class CyberDetonationProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int Lifetime = 40;
        /// <summary>基础爆炸半径（像素），受蓄力比例影响</summary>
        private const float BaseExplosionRadius = 200f;
        private const float MaxExplosionRadius = 350f;

        private float chargeRatio;
        private float explosionRadius;
        /// <summary>继承自充能球的超驱量 0-1</summary>
        private float overdriveAmount;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            // 初始帧：读取蓄力比例，计算爆炸范围
            if (Projectile.localAI[0] == 0f) {
                chargeRatio = MathHelper.Clamp(Projectile.ai[0], 0f, 1f);
                overdriveAmount = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
                explosionRadius = MathHelper.Lerp(BaseExplosionRadius, MaxExplosionRadius, chargeRatio);
                //超驱时实际爆炸半径同步扩大，与视觉范围保持一致
                if (overdriveAmount > 0f)
                    explosionRadius *= 1f + overdriveAmount * 0.5f;
                //改件半径倍率，由 SHPCOverride 通过 localAI[1] 注入；<=0 视为中性
                float radiusMul = Projectile.localAI[1];
                if (radiusMul > 0.01f) {
                    explosionRadius *= radiusMul;
                }
                //允许由生成方直接覆盖最终半径（localAI[2] > 0 时使用绝对像素值）
                float radiusOverride = Projectile.localAI[2];
                if (radiusOverride > 1f) {
                    explosionRadius = radiusOverride;
                }
                Projectile.localAI[0] = 1f;

                // 设置碰撞范围用于伤害检测
                int size = (int)(explosionRadius * 2f);
                Projectile.Resize(size, size);

                SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);

                // 初始粒子爆发
                if (Main.netMode != NetmodeID.Server) {
                    SpawnExplosionParticles();
                }
            }

            // 第一帧施加AOE伤害（通过碰撞检测）
            // Projectile自带碰撞检测会自动处理

            // 阻止弹幕移动
            Projectile.velocity = Vector2.Zero;

            // 光照（超驱时红炽光照）
            float t = 1f - (float)Projectile.timeLeft / Lifetime;
            float lightIntensity = MathF.Pow(1f - t, 2f);
            float od = overdriveAmount;
            Color lightCol = Color.Lerp(
                Color.Lerp(new Color(255, 220, 80), new Color(80, 230, 220), chargeRatio),
                new Color(255, 80, 20), od);
            Lighting.AddLight(Projectile.Center, lightCol.ToVector3() * lightIntensity * (1.2f + od * 0.8f));
        }

        private void SpawnExplosionParticles() {
            float od = overdriveAmount;
            Color mainCol = Color.Lerp(
                Color.Lerp(new Color(255, 220, 80), new Color(220, 255, 255), chargeRatio),
                new Color(255, 200, 50), od);
            Color edgeCol = Color.Lerp(
                Color.Lerp(new Color(230, 170, 30), new Color(80, 230, 220), chargeRatio),
                new Color(255, 30, 5), od);

            // 径向爆发粒子（超驱时更多更大）
            int count = 20 + (int)(chargeRatio * 15f) + (int)(od * 25f);
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.1f, 0.1f);
                float speed = Main.rand.NextFloat(4f, 10f + od * 6f) * (0.6f + chargeRatio * 0.4f);
                Vector2 vel = angle.ToRotationVector2() * speed;
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center + vel * 2f, vel, mainCol, Main.rand.NextFloat(1.0f, 2.5f + od * 1.5f)).Configure(edgeCol, Main.rand.Next(25, 55));
            }

            // 内环密集小粒子（超驱时更多）
            int innerCount = 12 + (int)(od * 12f);
            for (int i = 0; i < innerCount; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f + od * 3f, 3f + od * 3f);
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, vel, Color.White, Main.rand.NextFloat(0.4f, 1.0f + od * 0.6f)).Configure(mainCol, Main.rand.Next(15, 35));
            }

            // 超驱时额外红炽碎片爆发
            if (od > 0.3f) {
                for (int i = 0; i < 20; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f) * Main.rand.NextFloat(0.5f, 1.2f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, vel, new Color(255, 30, 5), Main.rand.NextFloat(1.2f, 2.8f)).Configure(new Color(255, 200, 50), Main.rand.Next(20, 45));
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 圆形碰撞检测
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            return dist < explosionRadius;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            // 距离衰减：中心满伤，边缘50%
            float dist = Vector2.Distance(Projectile.Center, target.Center);
            float falloff = 1f - (dist / explosionRadius) * 0.5f;
            modifiers.FinalDamage *= MathHelper.Clamp(falloff, 0.5f, 1f);
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.8f;
            }
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.CyberDetonation?.Value;
            if (shader == null) return false;
            if (CWRAsset.Placeholder_White == null) return false;
            if (CWRAsset.Extra_193?.Value == null) return false;

            Texture2D canvas = CWRAsset.Placeholder_White.Value;
            Texture2D noise = CWRAsset.Extra_193.Value;

            float t = 1f - (float)Projectile.timeLeft / Lifetime;
            // 快速起步缓出
            float ringProgress = 1f - MathF.Pow(1f - t, 2.5f);

            // 淡入淡出
            float fadeAlpha;
            if (t < 0.15f)
                fadeAlpha = MathHelper.SmoothStep(0f, 1f, t / 0.15f);
            else if (t > 0.5f)
                fadeAlpha = MathHelper.SmoothStep(1f, 0f, (t - 0.5f) / 0.5f);
            else
                fadeAlpha = 1f;
            fadeAlpha = MathHelper.Clamp(fadeAlpha, 0f, 1f);

            // 颜色根据蓄力比例过渡，超驱时偏向红炽
            float od = overdriveAmount;
            Vector3 baseCoreCol = Vector3.Lerp(new Vector3(1f, 0.86f, 0.31f), new Vector3(0.86f, 1f, 1f), chargeRatio);
            Vector3 baseRingCol = Vector3.Lerp(new Vector3(0.9f, 0.67f, 0.12f), new Vector3(0.31f, 0.9f, 0.86f), chargeRatio);
            Vector3 baseFragCol = Vector3.Lerp(new Vector3(0.59f, 0.39f, 0.06f), new Vector3(0.08f, 0.55f, 0.51f), chargeRatio);

            Vector3 coreCol = Vector3.Lerp(baseCoreCol, new Vector3(1f, 0.97f, 0.82f), od);
            Vector3 ringCol = Vector3.Lerp(baseRingCol, new Vector3(1f, 0.12f, 0.03f), od);
            Vector3 fragCol = Vector3.Lerp(baseFragCol, new Vector3(0.75f, 0.04f, 0f), od);

            // 设置着色器参数（取主人玩家的领域时间，远端客户端不能读 Local）
            CyberspacePlayer ownerCp = Cyberspace.For(Projectile.owner);
            float effectTime = ownerCp != null && ownerCp.Active
                ? ownerCp.EffectTime
                : (float)Main.timeForVisualEffects * 0.04f;
            shader.Parameters["uTime"]?.SetValue(effectTime);
            shader.Parameters["ringProgress"]?.SetValue(ringProgress);
            shader.Parameters["fadeAlpha"]?.SetValue(fadeAlpha);
            shader.Parameters["coreColor"]?.SetValue(coreCol);
            shader.Parameters["ringColor"]?.SetValue(ringCol);
            shader.Parameters["fragColor"]?.SetValue(fragCol);
            // 超驱故障参数
            shader.Parameters["overdriveAmount"]?.SetValue(od);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float drawDiameter = explosionRadius * 2.2f;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(drawDiameter, drawDiameter),
                SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
