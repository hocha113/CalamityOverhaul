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
    /// 海啸浪墙：贴地横扫的水墙，出生后一路增速，浪冠可越，压过处留湿沫。
    /// 流体感核心在着色器的世界锚定噪声域：浪形在跑，水体留在世界里。
    /// ai[0]=方向符号 ai[1]=浪级(0常规/1高浪) localAI[0]=寿命计时
    /// </summary>
    internal class FishronTsunamiWallProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int WaveDamage = 48;
        private const int RiseTime = 26;
        private const int FadeTime = 30;
        private const int BaseLife = 360;
        /// <summary>速度基准：uSpeedRatio=1 的刻度</summary>
        internal const float BaseSpeed = 18f;
        /// <summary>推进速度上限：迅猛但可被越顶/拉开</summary>
        private const float MaxSpeed = 26f;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        private int Dir => Projectile.ai[0] >= 0 ? 1 : -1;
        private bool Tall => Projectile.ai[1] >= 1f;
        private ref float LifeTimer => ref Projectile.localAI[0];

        private float WallHeight => Tall ? 470f : 380f;
        private const float WallWidth = 130f;

        private float SpeedRatio => Math.Abs(Projectile.velocity.X) / BaseSpeed;

        private float Envelope {
            get {
                float rise = MathHelper.Clamp(LifeTimer / RiseTime, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / (float)FadeTime, 0f, 1f);
                return Math.Min(rise * rise, fade);
            }
        }

        public override void SetStaticDefaults() {
            //抛沫画布高出命中盒五成：出屏余量不足会整墙瞬灭
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 560;
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

            //一路增速：起浪站稳后复合加速到上限，海啸是追人的
            if (LifeTimer > RiseTime && Projectile.timeLeft > FadeTime
                && Math.Abs(Projectile.velocity.X) < MaxSpeed) {
                Projectile.velocity.X *= 1.011f;
            }

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
                //浪冠喷雾：前甩为主，带重力弧线的断裂抛沫；越快甩得越猛
                if (Main.rand.NextBool(2)) {
                    int sprayCount = 2 + (int)SpeedRatio;
                    FishronMotionFX.SpawnSprayCone(crest + Main.rand.NextVector2Circular(30f, 16f),
                        new Vector2(Dir * 1.4f, -0.9f).SafeNormalize(-Vector2.UnitY),
                        sprayCount, 3f, 8f + SpeedRatio * 3f, 0.5f, env);
                }
                //溃散期：冠线崩解成一阵密集碎沫，浪"塌"下去而不是淡出去
                if (Projectile.timeLeft <= FadeTime && Main.rand.NextBool(2)) {
                    float collapseT = 1f - Projectile.timeLeft / (float)FadeTime;
                    Vector2 fallCrest = new(Projectile.Center.X + Main.rand.NextFloat(-0.5f, 0.5f) * WallWidth,
                        Projectile.position.Y + Projectile.height * (0.1f + collapseT * 0.7f));
                    FishronMotionFX.SpawnSprayCone(fallCrest, -Vector2.UnitY, 2, 1.5f, 5f, 0.9f, 0.8f);
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_FishronFoam>(fallCrest,
                        new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 2f)),
                        FishronMotionFX.FoamWhite * 0.45f, Main.rand.NextFloat(0.8f, 1.4f))
                        ?.Configure(Main.rand.Next(20, 36), Main.rand.NextFloat(-0.04f, 0.04f));
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
            //顶部预留三成画布给冠口抛沫，浪冠永不顶着画布边
            float drawW = WallWidth * 2.6f;
            float drawH = WallHeight * 1.5f;
            Vector2 drawCenter = bottom - new Vector2(0, drawH * 0.5f);

            if (effect == null || noiseTex == null) {
                DrawSpriteFallback(env, drawCenter, drawW, drawH);
                return false;
            }

            //起浪/溃散走几何：浪从地里立起、自冠而下蚀掉，alpha 只承担残余
            float growth = MathHelper.Clamp(LifeTimer / (float)RiseTime, 0f, 1f);
            growth = growth * growth * (3f - 2f * growth);
            float collapse = 1f - MathHelper.Clamp(Projectile.timeLeft / (float)FadeTime, 0f, 1f);

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(0.35f + 0.65f * env);
            effect.Parameters["uGrowth"]?.SetValue(growth);
            effect.Parameters["uCollapse"]?.SetValue(collapse);
            effect.Parameters["uDir"]?.SetValue((float)Dir);
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.313f);
            //流体域世界锚定：浪形平移时水体纹理钉在世界里，水从浪身里流过
            effect.Parameters["uWorldX"]?.SetValue(Projectile.Center.X);
            effect.Parameters["uCanvasPx"]?.SetValue(drawW);
            effect.Parameters["uSpeedRatio"]?.SetValue(SpeedRatio);
            effect.Parameters["uDeepColor"]?.SetValue(FishronMotionFX.DeepSea.ToVector3());
            effect.Parameters["uFoamColor"]?.SetValue(FishronMotionFX.FoamWhite.ToVector3());
            effect.Parameters["uSeaColor"]?.SetValue(FishronMotionFX.SeaGreen.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图，
            //参数式贴图绑定实机失效，浪顶"灰度图"即画布渐变漏色（合同同 ShockRingDraw.Draw）
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noiseTex.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
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

        /// <summary>着色器缺失兜底：真 alpha 雾团堆出暗水墙体，只求带伤害的浪不隐形</summary>
        private void DrawSpriteFallback(float env, Vector2 center, float drawW, float drawH) {
            Texture2D puff = CWRAsset.Fog?.Value;
            if (puff == null) {
                return;
            }
            Vector2 origin = puff.Size() / 2f;
            for (int i = 0; i < 3; i++) {
                float t = i / 2f;
                //自浪脚向冠线逐层收窄：底厚顶薄，前脸压向行进方向
                Vector2 pos = center + new Vector2(Dir * t * 20f, drawH * (0.30f - t * 0.30f)) - Main.screenPosition;
                Color deep = Color.Lerp(FishronMotionFX.DeepSea, FishronMotionFX.SeaGreen, t);
                Main.EntitySpriteDraw(puff, pos, null, deep * (0.75f * env), 0f, origin,
                    new Vector2(drawW * (0.62f - 0.12f * t) / puff.Width, drawH * 0.34f / puff.Height),
                    SpriteEffects.None, 0);
            }
            //冠线白沫
            Main.EntitySpriteDraw(puff, center + new Vector2(Dir * 24f, -drawH * 0.16f) - Main.screenPosition, null,
                FishronMotionFX.FoamWhite * (0.36f * env), 0f, origin,
                new Vector2(drawW * 0.42f / puff.Width, drawH * 0.10f / puff.Height), SpriteEffects.None, 0);
        }
    }
}
