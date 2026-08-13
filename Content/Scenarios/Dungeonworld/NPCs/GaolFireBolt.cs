using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 深牢怨灵吐出的追踪狱火：冷粉鬼焰，出膛先抛一口弧线再咬住猎物，
    /// 航向叠加正弦扭摆走蛇形尾迹；追猎后半程放弃锁定改滑翔，给闪避留出口。
    /// ai[0]=追踪目标玩家位（服务器定，spawn 包自带），ai[1]=蛇摆相位符号。
    /// 命中/超时冷粉迸溅；纯发光体自绘，无贴图依赖
    /// </summary>
    internal class GaolFireBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>出膛后多少帧开始咬住目标：先把上抛弧线画完</summary>
        private const int HomingDelay = 12;
        /// <summary>此帧后放弃锁定滑翔（公平阀：不许无限追）</summary>
        private const int HomingEnd = 96;

        private ref float Life => ref Projectile.localAI[0];
        private int TargetIndex => (int)Projectile.ai[0];
        private float SwaySign => Projectile.ai[1] >= 0f ? 1f : -1f;

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 170;
            //鬼火穿行地形，贴合怨灵读感；谢幕统一走 OnKill 迸溅
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Life++;

            if (Life > HomingDelay && Life <= HomingEnd) {
                Player target = TargetIndex >= 0 && TargetIndex < Main.maxPlayers ? Main.player[TargetIndex] : null;
                if (target != null && target.active && !target.dead) {
                    //咬合：转率随追猎时间爬升，速度复利到上限
                    float turn = MathHelper.Lerp(0.028f, 0.07f, MathHelper.Clamp((Life - HomingDelay) / 40f, 0f, 1f));
                    float wantAngle = (target.Center - Projectile.Center).ToRotation();
                    float angle = Projectile.velocity.ToRotation().AngleTowards(wantAngle, turn);
                    float speed = MathF.Min(Projectile.velocity.Length() * 1.01f, 12.5f);
                    Projectile.velocity = angle.ToRotationVector2() * speed;
                }
                else {
                    Projectile.velocity *= 0.995f;
                }
            }
            else if (Life <= HomingDelay) {
                //上抛段微重力，弧线读得出"吐"的抛物感
                Projectile.velocity.Y += 0.1f;
            }
            else {
                //追猎结束：滑翔泄劲，寿终由 timeLeft 收
                Projectile.velocity *= 0.99f;
            }

            //蛇形摆尾：航向逐帧叠加正弦扭摆，路径本身就是尾迹
            Projectile.velocity = Projectile.velocity.RotatedBy(
                MathF.Sin(Life * 0.34f + Seed * 2f) * 0.045f * SwaySign);
            Projectile.rotation = Projectile.velocity.ToRotation();

            //沿途余烬
            if (!Main.dedServ && Life % 3 == 0) {
                Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
                PRTLoader.NewParticle<PRT_GaolFireWisp>(
                    Projectile.Center + back * Main.rand.NextFloat(4f, 10f),
                    Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    Main.rand.NextBool(3) ? DeepGaolWraith.GaolPinkDeep : DeepGaolWraith.GaolPink,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 24));
            }

            float glow = 0.5f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.36f * glow, 0.14f * glow, 0.24f * glow);
        }

        //==================== 命中与谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //冷粉迸溅：半球余烬 + 扩散环 + 湿噗
            Vector2 normal = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = normal.RotatedByRandom(1.2f) * Main.rand.NextFloat(1.6f, 5.2f);
                PRTLoader.NewParticle<PRT_GaolFireWisp>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    vel, Main.rand.NextBool(3) ? DeepGaolWraith.GaolPinkDeep : DeepGaolWraith.GaolPink,
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(16, 28));
            }
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, DeepGaolWraith.GaolPink, 0.06f)
                ?.Configure(new Vector2(0.8f, 1f), normal.ToRotation(), 0.2f, 8);
            SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaiveImpactGhost with { Volume = 0.4f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
        }

        //==================== 绘制：纯发光鬼焰（速度拉伸 + 残影串尾）====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D blob = CWRAsset.Extra_98?.Value;
            if (glow == null || blob == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 gOrigin = glow.Size() * 0.5f;
            Vector2 bOrigin = blob.Size() * 0.5f;
            float fade = VisualFade;
            float flick = 0.82f + 0.18f * MathF.Sin(Life * 0.55f + Seed * 4f);

            //残影串尾：越旧越小越淡
            for (int k = Projectile.oldPos.Length - 1; k >= 1; k -= 2) {
                Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                if (oldCenter == Projectile.Size * 0.5f) {
                    continue;
                }
                float fall = 1f - k / (float)Projectile.oldPos.Length;
                sb.Draw(glow, oldCenter - Main.screenPosition, null,
                    (DeepGaolWraith.GaolPink with { A = 0 }) * (0.2f * fall * fade), 0f,
                    gOrigin, new Vector2(16f * (0.8f - k * 0.05f) * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //焰体三层：暗焰缘（真 alpha 血珠布）→ 粉焰身 → 白热芯，沿速度拉伸
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.06f, 0f, 1f);
            Vector2 shape = new(1f + stretch * 1.6f, 1f - stretch * 0.3f);
            float rot = Projectile.rotation;

            sb.Draw(blob, pos, null, DeepGaolWraith.GaolPinkDeep * (0.85f * fade), rot,
                bOrigin, new Vector2(0.52f, 0.4f) * shape, SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, (DeepGaolWraith.GaolPink with { A = 0 }) * (0.9f * fade * flick), rot,
                gOrigin, new Vector2(30f * shape.X * 2f / glow.Width, 24f * shape.Y * 2f / glow.Height), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, (DeepGaolWraith.GaolWhiteHot with { A = 0 }) * (0.65f * fade * flick), rot,
                gOrigin, new Vector2(14f * shape.X * 2f / glow.Width, 11f * shape.Y * 2f / glow.Height), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>狱火余烬：不坠反升的冷粉焰屑，速度拉伸、呼吸闪烁、尾段收芯转暗</summary>
    internal class PRT_GaolFireWisp : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 400;

        private Color initialColor;

        public PRT_GaolFireWisp Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 20;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //焰性上浮 + 横向阻尼
            Velocity.X *= 0.96f;
            Velocity.Y = MathF.Max(Velocity.Y - 0.05f, -2.4f);

            float t = LifetimeCompletion;
            Scale *= 0.975f;
            //先亮后暗，尾段陡熄
            Color = Color.Lerp(initialColor, DeepGaolWraith.GaolPinkDeep, MathF.Pow(t, 1.4f) * 0.8f);
            Opacity = 1f - MathF.Pow(t, 2.6f);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.08f, 0f, 0.8f);
            Vector2 scale = new Vector2(0.3f * (1f - stretch * 0.3f), 0.42f * (1f + stretch * 1.6f)) * Scale;

            Color body = Color * Opacity;
            //暗焰缘略宽一圈，给焰屑体积
            spriteBatch.Draw(tex, pos, null,
                Color.Lerp(Color, DeepGaolWraith.GaolPinkDeep, 0.6f) * Opacity, Rotation, origin,
                scale * new Vector2(1.3f, 1.05f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, body, Rotation, origin, scale, SpriteEffects.None, 0f);
            //新鲜期白热芯（A=0 加色）
            float fresh = 1f - MathHelper.Clamp(LifetimeCompletion * 2f, 0f, 1f);
            if (fresh > 0.05f) {
                spriteBatch.Draw(tex, pos, null,
                    (DeepGaolWraith.GaolWhiteHot with { A = 0 }) * (0.45f * fresh * Opacity), Rotation, origin,
                    scale * new Vector2(0.4f, 0.7f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
