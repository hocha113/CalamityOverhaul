using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 行进浪峰：跃空砸落从落点向两侧掀起的千像素巨浪。沿地面横移、贴地爬坡，
    /// 前倾浪体（TechGeyser 着色器复用，quad 前倾，主峰+尾随矮峰双层轮廓），
    /// 行进满程后撕裂消散。伤害窗=涌起后的行进段；判定芯窄于可见浪体，
    /// 躲法=预判行进带横向让位（浪速恒定可读）。
    /// ai[0]=行进方向 ±1，ai[1]=浪体高 px；Center=浪底地面点
    /// </summary>
    internal class SeaShrimpWaveCrest : SeaShrimpModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        /// <summary>名义可见宽 px（千像素级巨浪的厚度）</summary>
        private const float VisualWidth = 220f;
        /// <summary>判定芯半宽（判定藏在可见体内）</summary>
        private const float CoreHalfWidth = 70f;
        /// <summary>涌起帧数</summary>
        private const int SurgeFrames = 12;
        /// <summary>消散帧数</summary>
        private const int FadeFrames = 18;
        /// <summary>浪体前倾角 rad（朝行进向压顶的卷浪读感）</summary>
        private const float LeanAngle = 0.24f;

        private float Dir => Projectile.ai[0] >= 0f ? 1f : -1f;
        private float CrestHeight => Projectile.ai[1];
        /// <summary>行进帧数：满程 ÷ 浪速</summary>
        private static int TravelFrames => (int)(SeaShrimpDirector.WaveCrestRange / SeaShrimpDirector.WaveCrestSpeed);
        private static int TotalLife => SurgeFrames + TravelFrames + FadeFrames;

        /// <summary>本地帧龄：逐端计数</summary>
        private int Age => (int)Projectile.localAI[0];

        public override void SetStaticDefaults() {
            //quad 高 ~1300：近出屏不许整浪瞬灭
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 160;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>可见浪高比例：涌起急升 → 行进满高 → 消散缓落</summary>
        private float Height01() {
            int age = Age;
            if (age < SurgeFrames) {
                float t = age / (float)SurgeFrames;
                return 1f - (1f - t) * (1f - t) * (1f - t);
            }
            if (age < SurgeFrames + TravelFrames) {
                return 1f;
            }
            float r = (age - SurgeFrames - TravelFrames) / (float)FadeFrames;
            return 1f - r;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            int age = Age;
            if (age >= TotalLife) {
                Projectile.Kill();
                return;
            }

            //横移 + 贴地爬坡：从浪底上方小幅起点向下扫地，Y 平滑趋近（台阶地形浪爬上去；
            //起点不按浪高抬——千像素巨浪的扫描起点飘上空中平台会让浪跳台）
            Vector2 pos = Projectile.Center;
            pos.X += Dir * SeaShrimpDirector.WaveCrestSpeed;
            float ground = ShrimpTerrain.FindGroundBelow(
                new Vector2(pos.X, pos.Y - 200f), 620f);
            pos.Y = MathHelper.Lerp(pos.Y, ground, 0.3f);
            Projectile.Center = pos;

            float h01 = Height01();
            if (h01 > 0.1f) {
                Lighting.AddLight(Projectile.Center - new Vector2(0f, CrestHeight * h01 * 0.5f),
                    0.07f * h01, 0.16f * h01, 0.3f * h01);
            }

            if (Main.dedServ) {
                return;
            }
            if (age == 1) {
                SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.7f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
                EverdeepVFX.SplashBurst(Projectile.Center, new Vector2(Dir * 4f, -8f), 1.1f);
            }
            //顶冠泡沫甩滴：行进浪撕下的水沫，朝行进向抛
            if (h01 >= 0.6f && Main.GameUpdateCount % 2 == 0) {
                Vector2 crest = Projectile.Center
                    + new Vector2(Main.rand.NextFloat(-24f, 24f), -CrestHeight * h01);
                EverdeepVFX.ShedDroplet(crest,
                    new Vector2(Dir * Main.rand.NextFloat(1.5f, 3.5f), -Main.rand.NextFloat(1f, 3f)), 0.9f);
            }
        }

        /// <summary>伤害窗=涌起后的行进段（消散段无害）</summary>
        public override bool? CanDamage() {
            int age = Age;
            return age >= SurgeFrames / 2 && age < SurgeFrames + TravelFrames ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float h = CrestHeight * Height01() * 0.8f;
            if (h < 12f) {
                return false;
            }
            Rectangle column = new((int)(Projectile.Center.X - CoreHalfWidth),
                (int)(Projectile.Center.Y - h), (int)(CoreHalfWidth * 2f), (int)h);
            return column.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            int age = Age;
            float h01 = Height01();

            Effect fx = EffectLoader.SeaShrimpJet?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return false;
            }
            //画布契约：quad 高 = 浪高×1.30（顶冠飞屑余量）+ 底埋 8px；宽 = 名义宽×3
            float quadH = CrestHeight * 1.30f + 8f;
            float quadW = VisualWidth * 3.0f;
            float lean = Dir * LeanAngle;

            if (fx == null || noiseTex == null) {
                //回退：暗浪块 + 前倾
                Rectangle src = new(0, 0, 1, 1);
                Vector2 basePos = Projectile.Center - new Vector2(0f, CrestHeight * h01) - Main.screenPosition;
                Main.spriteBatch.Draw(pixel, basePos, src, SeaShrimpVFX.Deep * (0.7f * h01), lean,
                    new Vector2(0.5f, 0f), new Vector2(VisualWidth, CrestHeight * h01), SpriteEffects.None, 0f);
                return false;
            }

            float heightPx = CrestHeight * MathF.Max(h01, 0.02f);
            //消散段进 uLife 尾段撕裂区（0.74..1，镜像间歇泉收回）
            bool fading = age >= SurgeFrames + TravelFrames;
            float life = fading
                ? 0.74f + 0.26f * ((age - SurgeFrames - TravelFrames) / (float)FadeFrames)
                : 0.3f;

            fx.CurrentTechnique = fx.Techniques["TechGeyser"];
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["fadeAlpha"]?.SetValue(1f);
            fx.Parameters["uQuadWPx"]?.SetValue(quadW);
            fx.Parameters["uQuadHPx"]?.SetValue(quadH);
            fx.Parameters["uGeyserWPx"]?.SetValue(VisualWidth);
            fx.Parameters["uLife"]?.SetValue(life);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noiseTex.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            //底埋地下 8px + 绕浪底前倾：卷浪压向行进向；
            //主峰身后再画一根矮峰拖尾——浪的轮廓不是一根移动的柱
            Rectangle srcRect = new(0, 0, 1, 1);
            Vector2 origin = new(0.5f, 1f);

            fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.53f + 7.1f);
            fx.Parameters["uHeightPx"]?.SetValue(heightPx * 0.55f);
            fx.CurrentTechnique.Passes[0].Apply();
            Vector2 tailPivot = Projectile.Center + new Vector2(-Dir * 170f, 8f) - Main.screenPosition;
            sb.Draw(pixel, tailPivot, srcRect, Color.White, lean * 0.6f,
                origin, new Vector2(quadW, quadH), SpriteEffects.None, 0f);

            fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.53f);
            fx.Parameters["uHeightPx"]?.SetValue(heightPx);
            fx.CurrentTechnique.Passes[0].Apply();
            Vector2 pivot = Projectile.Center + new Vector2(0f, 8f) - Main.screenPosition;
            sb.Draw(pixel, pivot, srcRect, Color.White, lean,
                origin, new Vector2(quadW, quadH), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
