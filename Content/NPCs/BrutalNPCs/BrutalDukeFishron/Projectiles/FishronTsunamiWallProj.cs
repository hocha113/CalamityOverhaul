using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles
{
    /// <summary>
    /// 海啸浪墙：贴地横扫的水墙，浪冠可越，压过处留湿沫。
    /// ai[0]=方向符号 ai[1]=浪级(0常规/1高浪) localAI[0]=寿命计时
    /// </summary>
    internal class FishronTsunamiWallProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int WaveDamage = 48;
        private const int RiseTime = 26;
        private const int FadeTime = 30;
        private const int BaseLife = 360;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        private int Dir => Projectile.ai[0] >= 0 ? 1 : -1;
        private bool Tall => Projectile.ai[1] >= 1f;
        private ref float LifeTimer => ref Projectile.localAI[0];

        private float WallHeight => Tall ? 470f : 380f;
        private const float WallWidth = 130f;

        private float Envelope {
            get {
                float rise = MathHelper.Clamp(LifeTimer / RiseTime, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / (float)FadeTime, 0f, 1f);
                return Math.Min(rise * rise, fade);
            }
        }

        public override void SetDefaults() {
            Projectile.width = (int)WallWidth;
            Projectile.height = 380;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = BaseLife;
        }

        /// <summary>起浪/退浪期判定关闭</summary>
        private bool HitWindowOpen => LifeTimer >= RiseTime && Projectile.timeLeft >= FadeTime;

        public override bool CanHitPlayer(Player target) => HitWindowOpen;

        public override void AI() {
            LifeTimer++;

            //首帧按浪级定高
            if (LifeTimer == 1) {
                Vector2 bottom = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);
                Projectile.height = (int)WallHeight;
                Projectile.position = new Vector2(bottom.X - Projectile.width / 2f, bottom.Y - Projectile.height);
            }

            //起浪/退浪期无判定
            Projectile.damage = HitWindowOpen ? WaveDamage : 0;

            //贴地推进：velocity.X 为推进速度，Y 由地形吸附
            Vector2 ground = FishronMotionFX.FindSurfaceBelow(
                new Vector2(Projectile.Center.X, Projectile.position.Y + Projectile.height * 0.4f), out _);
            float targetBottom = ground.Y + 6f;
            float currentBottom = Projectile.position.Y + Projectile.height;
            Projectile.position.Y += MathHelper.Clamp(targetBottom - currentBottom, -11f, 11f);

            //浪墙表现
            if (!VaultUtils.isServer) {
                float env = Envelope;
                Vector2 crest = new(Projectile.Center.X + Dir * WallWidth * 0.2f, Projectile.position.Y + 16f);
                //浪冠喷雾
                if (Main.rand.NextBool(2)) {
                    FishronMotionFX.SpawnSprayCone(crest + Main.rand.NextVector2Circular(30f, 16f),
                        new Vector2(Dir * 0.8f, -1f).SafeNormalize(-Vector2.UnitY), 2, 2f, 7f, 0.55f, env);
                }
                //浪脚湿沫余痕：浪走过之后仍留在地上
                if (Main.rand.NextBool(3)) {
                    Vector2 foot = new(Projectile.Center.X - Dir * WallWidth * 0.5f,
                        Projectile.position.Y + Projectile.height - 12f);
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_FishronFoam>(foot,
                        new Vector2(-Dir * 0.4f, -0.3f), FishronMotionFX.FoamWhite * (0.4f * env),
                        Main.rand.NextFloat(0.8f, 1.3f))?.Configure(Main.rand.Next(40, 70), Main.rand.NextFloat(-0.02f, 0.02f));
                }
                if (LifeTimer % 30 == 0 && env > 0.4f) {
                    SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 3 }, Projectile.Center);
                }
                Lighting.AddLight(Projectile.Center, FishronMotionFX.SeaGreen.ToVector3() * 0.5f * env);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float env = Envelope;
            if (env <= 0.01f) {
                return false;
            }

            Effect effect = EffectLoader.FishronTsunami?.Value;
            Vector2 bottom = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);
            float drawW = WallWidth * 2.6f;
            float drawH = WallHeight * 1.18f;
            Vector2 drawCenter = bottom - new Vector2(0, drawH * 0.5f);

            if (effect == null || noiseTex == null) {
                DrawSpriteFallback(env, drawCenter, drawW, drawH);
                return false;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(env);
            effect.Parameters["uDir"]?.SetValue((float)Dir);
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.313f);
            effect.Parameters["uDeepColor"]?.SetValue(FishronMotionFX.DeepSea.ToVector3());
            effect.Parameters["uFoamColor"]?.SetValue(FishronMotionFX.FoamWhite.ToVector3());
            effect.Parameters["uSeaColor"]?.SetValue(FishronMotionFX.SeaGreen.ToVector3());
            effect.Parameters["uNoiseTex"]?.SetValue(noiseTex.Value);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new(drawW / pixel.Width, drawH / pixel.Height);
            //方向翻转由 uv 侧处理：uDir 传入着色器
            sb.Draw(pixel, drawCenter - Main.screenPosition, null, Color.White,
                0f, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>着色器缺失兜底：浪形贴图斜叠</summary>
        private void DrawSpriteFallback(float env, Vector2 center, float drawW, float drawH) {
            Texture2D wave = CWRUtils.GetT2DAsset(CWRConstant.Masking + "GlaciateWave")?.Value;
            if (wave == null) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                float t = i / 3f;
                Vector2 pos = center + new Vector2(-Dir * t * 26f, drawH * (0.5f - t * 0.8f) * 0.5f) - Main.screenPosition;
                Color c = Color.Lerp(FishronMotionFX.DeepSea, FishronMotionFX.SeaGreen, t);
                c = new Color(c.R, c.G, c.B, 0) * (env * 0.5f);
                float rot = Dir > 0 ? -0.4f : 0.4f;
                Main.EntitySpriteDraw(wave, pos, null, c, rot, wave.Size() / 2f,
                    new Vector2(drawW / wave.Width, drawH / wave.Height * (0.5f + t * 0.5f)),
                    Dir > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            }
        }
    }
}
