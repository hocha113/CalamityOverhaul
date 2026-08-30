using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 落地水龙卷：跃空砸落在落点原地掀起的巨型水柱（约 2000px），短命爆发版封场柱。
    /// 快速起身（起身即预告，60% 前无伤）→ 驻留 → 撕裂消散。
    /// 绘制复用 FishronTornado.fx 换深渊色板（消费合同同 SeaShrimpVortexWall）。
    /// ai[0]=可见高度；Center=柱底地面点
    /// </summary>
    internal class SeaShrimpLeapVortex : SeaShrimpModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        /// <summary>名义可见宽 px（2000 高的巨柱要够厚）</summary>
        private const float VisualWidth = 260f;
        /// <summary>判定芯半宽（判定藏在可见体内）</summary>
        private const float CoreHalfWidth = 64f;
        private const int GrowFrames = 22;
        private const int HoldFrames = 108;
        private const int FadeFrames = 26;
        private const int TotalLife = GrowFrames + HoldFrames + FadeFrames;

        private float Height => Projectile.ai[0];

        /// <summary>本地帧龄：逐端计数，迟入端不重播起身</summary>
        private int Age => (int)Projectile.localAI[0];

        private float Grow01 => MathHelper.Clamp(Age / (float)GrowFrames, 0f, 1f);
        private float Fade01 => Age < GrowFrames + HoldFrames
            ? 1f
            : 1f - MathHelper.Clamp((Age - GrowFrames - HoldFrames) / (float)FadeFrames, 0f, 1f);

        public override void SetStaticDefaults() {
            //quad 高 ~2600：近出屏不许整柱瞬灭
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;
        }

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = TotalLife + 10;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.localAI[0]++;
            int age = Age;
            if (age >= TotalLife) {
                Projectile.Kill();
                return;
            }

            float env = Grow01 * Fade01;
            Lighting.AddLight(Projectile.Center - new Vector2(0f, Height * 0.35f * env),
                0.12f * env, 0.26f * env, 0.48f * env);

            if (Main.dedServ || env < 0.15f) {
                return;
            }
            if (age == 2) {
                SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.9f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.4f, MaxInstances = 2 }, Projectile.Center);
            }
            //起身期猛踢水，驻留期低频飞沫（短命演出，预算比封场柱高一档）
            int footRate = age < GrowFrames ? 1 : 4;
            if (Main.GameUpdateCount % footRate == 0) {
                Vector2 foot = Projectile.Center + new Vector2(Main.rand.NextFloat(-VisualWidth * 0.45f, VisualWidth * 0.45f), 0f);
                EverdeepVFX.ShedDroplet(foot,
                    new Vector2(Main.rand.NextFloat(-2.6f, 2.6f), -Main.rand.NextFloat(2f, 5.5f)), 1f);
            }
            if (Main.GameUpdateCount % 6 == 0) {
                Vector2 rim = Projectile.Center - new Vector2(
                    Main.rand.NextFloat(-VisualWidth * 0.5f, VisualWidth * 0.5f),
                    Main.rand.NextFloat(0.15f, 0.95f) * Height * env);
                EverdeepVFX.ShedDroplet(rim,
                    new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), -Main.rand.NextFloat(0.6f, 2f)), 0.8f);
            }
        }

        /// <summary>伤害窗=可见窗：长成 60% 才咬人，消散过半即无害</summary>
        public override bool? CanDamage() => Grow01 >= 0.6f && Fade01 > 0.5f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float h = Height * Grow01;
            Rectangle column = new(
                (int)(Projectile.Center.X - CoreHalfWidth),
                (int)(Projectile.Center.Y - h),
                (int)(CoreHalfWidth * 2f), (int)h);
            return column.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            float env = Grow01 * Fade01;
            if (env <= 0.02f) {
                return false;
            }
            //柱高吃起身缓动，底锚地面——柱从地里长出来，不是凭空淡入
            float riseT = 1f - (1f - Grow01) * (1f - Grow01);
            float drawW = VisualWidth * 3.0f;
            float drawH = Height * 1.30f * MathHelper.Lerp(0.25f, 1f, riseT) * MathHelper.Lerp(0.55f, 1f, Fade01);
            Vector2 bottom = Projectile.Center;
            Vector2 drawCenter = bottom - new Vector2(0f, drawH * 0.5f);

            Effect effect = EffectLoader.FishronTornado?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (effect == null || noiseTex == null || pixel == null) {
                DrawFallback(bottom, drawH, env);
                return false;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(env * 1.5f);
            effect.Parameters["uGrade"]?.SetValue(1f);
            effect.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.61f);
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

        /// <summary>着色器缺失回退：暗柱+亮芯双竖条</summary>
        private void DrawFallback(Vector2 bottom, float drawH, float env) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            Rectangle src = new(0, 0, 1, 1);
            Vector2 basePos = bottom - new Vector2(0f, drawH) - Main.screenPosition;
            Main.spriteBatch.Draw(pixel, basePos, src, SeaShrimpVFX.Deep * (0.7f * env), 0f,
                new Vector2(0.5f, 0f), new Vector2(VisualWidth * 0.9f, drawH), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, basePos, src, SeaShrimpVFX.Body * (0.8f * env), 0f,
                new Vector2(0.5f, 0f), new Vector2(VisualWidth * 0.4f, drawH), SpriteEffects.None, 0f);
        }
    }
}
