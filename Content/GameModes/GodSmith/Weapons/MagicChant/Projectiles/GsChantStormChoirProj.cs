using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant.Projectiles
{
    /// <summary>
    /// 气象痛「风暴合唱」：满层强化召来的驻场大风暴柱。<br/>
    /// 判定 = 本体矩形（宽 110 高 140，与可见柱身同源），idStatic 免疫 24t 兑现 0.4s 一跳；
    /// 伤害在生成时按 0.5 倍烘焙。绘制复用 FishronTornado.fx 换灰绿风暴色板，
    /// 着色器缺失走旋筒贴图兜底。ai[0] = 水平漂移方向（±1，生成时烘焙随包同步）
    /// </summary>
    internal class GsChantStormChoirProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicChant";

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        /// <summary>总寿命 1.8s</summary>
        private const int LifeTicks = 108;
        /// <summary>起身帧数</summary>
        private const int RiseTicks = 14;
        /// <summary>消散帧数</summary>
        private const int FadeTicks = 16;

        //风暴合唱的灰绿色板（气象痛的沼风材质，非 Duke 海色）
        private static readonly Color StormDeep = new(52, 74, 66);
        private static readonly Color StormMist = new(178, 208, 188);
        private static readonly Color StormBody = new(96, 138, 118);

        private float seed;

        private float Envelope {
            get {
                float rise = MathHelper.Clamp((LifeTicks - Projectile.timeLeft) / (float)RiseTicks, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / (float)FadeTicks, 0f, 1f);
                return Math.Min(rise * rise, fade);
            }
        }

        public override void SetDefaults() {
            Projectile.width = 110;
            Projectile.height = 140;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTicks;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 24;
        }

        public override void SetStaticDefaults() {
            //quad 宽出命中盒许多：柱身近出屏时不许整柱瞬灭
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Envelope > 0.5f ? null : false;

        public override void AI() {
            seed = Projectile.whoAmI * 0.617f;
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffTwisterLoop with {
                        Volume = 0.8f, Pitch = -0.2f, MaxInstances = 2
                    }, Projectile.Center);
                }
            }
            float env = Envelope;
            //缓慢横漂，读作风暴在行进而非钉死的立柱
            Projectile.position.X += Projectile.ai[0] * 0.45f * env;

            Lighting.AddLight(Projectile.Center, StormMist.ToVector3() * (0.35f * env));

            if (VaultUtils.isServer) {
                return;
            }
            //风暴身份：云絮绕柱盘升 + 柱底尘沫横甩，持续期 ≤3/帧
            Vector2 bottom = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);
            if (Main.rand.NextBool(3)) {
                float h = Main.rand.NextFloat(0.1f, 0.9f);
                PRTLoader.NewParticle<PRT_SvcCloud>(
                    bottom - new Vector2(-Main.rand.NextFloat(-0.5f, 0.5f) * Projectile.width, Projectile.height * h),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(0.6f, 1.6f)),
                    StormBody * (0.5f * env), Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(20, 32));
            }
            if (Main.rand.NextBool(4)) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                PRTLoader.NewParticle<PRT_Spark>(
                    bottom + new Vector2(side * Main.rand.NextFloat(0.3f, 0.6f) * Projectile.width, -6f),
                    new Vector2(side * Main.rand.NextFloat(2f, 4f), -Main.rand.NextFloat(0.5f, 1.2f)),
                    StormMist, Main.rand.NextFloat(0.2f, 0.32f))?.Configure(false, Main.rand.Next(10, 16));
            }
        }

        public override void OnKill(int timeLeft) {
            //余痕相：风暴散场留一圈缓旋云絮，活得比柱身久
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                float ang = MathHelper.TwoPi * i / 5f;
                PRTLoader.NewParticle<PRT_SvcCloud>(
                    Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(20f, 46f),
                    ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(0.8f, 1.6f),
                    StormBody * 0.45f, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(26, 40));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float env = Envelope;
            if (env <= 0.01f) {
                return false;
            }

            Effect effect = EffectLoader.FishronTornado?.Value;
            Vector2 bottom = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);
            //quad 大幅宽于名义柱径：撕裂轮廓与离体飞沫留在画布内侧，护栏不承担切边
            float drawW = Projectile.width * 3.0f;
            float drawH = Projectile.height * 1.30f;
            Vector2 drawCenter = bottom - new Vector2(0, drawH * 0.5f);

            if (effect == null || noiseTex == null) {
                DrawSpriteFallback(env, bottom, drawW, drawH);
                return false;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(env);
            effect.Parameters["uGrade"]?.SetValue(0f);
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uDeepColor"]?.SetValue(StormDeep.ToVector3());
            effect.Parameters["uFoamColor"]?.SetValue(StormMist.ToVector3());
            effect.Parameters["uSeaColor"]?.SetValue(StormBody.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            //噪声显式绑 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图，参数式贴图绑定实机失效
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noiseTex.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new(drawW / pixel.Width, drawH / pixel.Height);
            sb.Draw(pixel, drawCenter - Main.screenPosition, null, Color.White,
                0f, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>着色器缺失兜底：旋筒贴图堆叠（identity 定相，不掷随机）</summary>
        private void DrawSpriteFallback(float env, Vector2 bottom, float drawW, float drawH) {
            Texture2D cyclone = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Cyclone")?.Value;
            if (cyclone == null) {
                return;
            }
            int layers = 5;
            for (int i = 0; i < layers; i++) {
                float t = i / (float)(layers - 1);
                Vector2 pos = bottom - new Vector2(0, drawH * t) - Main.screenPosition;
                float w = MathHelper.Lerp(drawW * 0.5f, drawW, t) / cyclone.Width;
                float rot = Main.GlobalTimeWrappedHourly * (4f - t * 1.5f) * (i % 2 == 0 ? 1f : -1f) + seed;
                Color c = Color.Lerp(StormDeep, StormBody, t);
                c = new Color(c.R, c.G, c.B, 0) * (env * 0.55f);
                Main.EntitySpriteDraw(cyclone, pos, null, c, rot, cyclone.Size() / 2f,
                    new Vector2(w, w * 0.6f), SpriteEffects.None, 0);
            }
        }
    }
}
