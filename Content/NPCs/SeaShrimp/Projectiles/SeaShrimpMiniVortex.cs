using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 行走水龙卷：甩尾甩出，定向行军（生成即定速不追踪=缺口保证）。
    /// 30f 生长期与 30f 消散期无伤（生长即预告）。
    /// 柱底在贴地容差内吸附地面爬坡；悬空生成（落差阀）时保持高度随空行军。
    /// ai[0]=可见高度，ai[1]=行军横速（带符号，定值）；Center=柱底
    /// </summary>
    internal class SeaShrimpMiniVortex : SeaShrimpModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        /// <summary>名义可见宽 px（四倍扩编 2026-08：千像素级龙卷配厚身）</summary>
        private const float VisualWidth = 190f;
        /// <summary>判定芯半宽（判定藏在可见体内）</summary>
        private const float CoreHalfWidth = 52f;
        private const int GrowFrames = 30;
        private const int FadeFrames = 30;
        private const int LifeFrames = 330;

        private float Height => Projectile.ai[0];
        private float WalkSpeed => Projectile.ai[1];

        private int Age => LifeFrames - Projectile.timeLeft;
        private float Grow01 => MathHelper.Clamp(Age / (float)GrowFrames, 0f, 1f);
        private float Fade01 => MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);

        public override void SetStaticDefaults() {
            //quad 高 ~1400：近出屏不许整柱瞬灭
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;
        }

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = LifeFrames;
        }

        public override void AI() {
            //定速行军：速度是生成时定死的声明量，不追踪
            Projectile.velocity = new Vector2(WalkSpeed * MathF.Min(Grow01 * 1.6f, 1f), 0f);

            //贴地：柱底吸附脚下地面（容差内缓贴，走上坡下坎不跳变）
            float groundY = ShrimpTerrain.FindGroundBelow(Projectile.Center - new Vector2(0f, 60f), 300f);
            if (MathF.Abs(groundY - Projectile.Center.Y) < 120f) {
                Projectile.Center = new Vector2(Projectile.Center.X,
                    MathHelper.Lerp(Projectile.Center.Y, groundY, 0.2f));
            }

            float env = Grow01 * Fade01;
            Lighting.AddLight(Projectile.Center - new Vector2(0f, Height * 0.4f),
                0.06f * env, 0.14f * env, 0.26f * env);

            if (!Main.dedServ && env > 0.3f && Main.GameUpdateCount % 6 == 0) {
                //柱脚踢水
                EverdeepVFX.ShedDroplet(Projectile.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), 0f),
                    new Vector2(WalkSpeed * 0.6f + Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(0.8f, 2.2f)), 0.75f);
            }
        }

        /// <summary>伤害窗=可见窗：长成 60% 才咬人，消散段无害</summary>
        public override bool? CanDamage() => Grow01 >= 0.6f && Fade01 > 0.6f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float h = Height * Grow01;
            Rectangle column = new((int)(Projectile.Center.X - CoreHalfWidth),
                (int)(Projectile.Center.Y - h), (int)(CoreHalfWidth * 2f), (int)h);
            return column.Intersects(targetHitbox);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //散场：柱身化滴坍落
            for (int i = 0; i < 5; i++) {
                EverdeepVFX.ShedDroplet(
                    Projectile.Center - new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(0f, Height * 0.7f)),
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 1.5f)), 0.85f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float env = Grow01 * Fade01;
            if (env <= 0.02f) {
                return false;
            }
            float riseT = 1f - (1f - Grow01) * (1f - Grow01);
            float drawW = VisualWidth * 3.0f;
            float drawH = Height * 1.30f * MathHelper.Lerp(0.30f, 1f, riseT) * MathHelper.Lerp(0.55f, 1f, Fade01);
            Vector2 drawCenter = Projectile.Center - new Vector2(0f, drawH * 0.5f);

            Effect effect = EffectLoader.FishronTornado?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return false;
            }
            if (effect == null || noiseTex == null) {
                //着色器缺失回退：暗柱+亮芯双竖条
                Rectangle src = new(0, 0, 1, 1);
                Vector2 basePos = Projectile.Center - new Vector2(0f, drawH) - Main.screenPosition;
                Main.spriteBatch.Draw(pixel, basePos, src, SeaShrimpVFX.Deep * (0.7f * env), 0f,
                    new Vector2(0.5f, 0f), new Vector2(VisualWidth * 0.8f, drawH), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(pixel, basePos, src, SeaShrimpVFX.Body * (0.8f * env), 0f,
                    new Vector2(0.5f, 0f), new Vector2(VisualWidth * 0.35f, drawH), SpriteEffects.None, 0f);
                return false;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(env * 1.15f);
            effect.Parameters["uGrade"]?.SetValue(1f);
            effect.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.83f);
            effect.Parameters["uDeepColor"]?.SetValue(SeaShrimpVFX.Deep.ToVector3());
            effect.Parameters["uSeaColor"]?.SetValue(SeaShrimpVFX.Body.ToVector3());
            effect.Parameters["uFoamColor"]?.SetValue(SeaShrimpVFX.Foam.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noiseTex.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();

            sb.Draw(pixel, drawCenter - Main.screenPosition, null, Color.White, 0f,
                pixel.Size() * 0.5f, new Vector2(drawW / pixel.Width, drawH / pixel.Height),
                SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
