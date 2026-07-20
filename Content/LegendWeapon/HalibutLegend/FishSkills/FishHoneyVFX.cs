using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>蜜诏群蜂域内 shader 资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishHoneyAssets
    {
        /// <summary>蜜团核心：SDF 粘稠液团 + 薄边透光 + 琥珀高光缓扫 + 噪声现形/溶解</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishHoneyCore { get; private set; }
    }

    /// <summary>
    /// 蜜诏群蜂共享演出。<br/>
    /// 色彩脚本：暖金/蜜橙/深琥珀，蜜体 AlphaBlend 半透明（液体非光源），
    /// 高光只用小面积暖白 A=0 点，禁大面积加色光斑。<br/>
    /// 签名行为：慢滴垂坠成斑、拉丝断丝回缩、蜂群高频微抖动
    /// </summary>
    internal static class FishHoneyVFX
    {
        /// <summary>深琥珀（厚蜜、暗外圈）</summary>
        public static readonly Color HoneyDeep = new(126, 64, 14);
        /// <summary>蜜橙（主体）</summary>
        public static readonly Color HoneyAmber = new(216, 128, 30);
        /// <summary>暖金（薄蜜、饱和中层）</summary>
        public static readonly Color HoneyGold = new(255, 188, 72);
        /// <summary>暖白高光（仅小点，禁常驻大面积）</summary>
        public static readonly Color HoneyGlint = new(255, 236, 182);

        /// <summary>粘液闷响：蜜的落定/破裂声底</summary>
        public static void GlugSound(Vector2 pos, float pitch = -0.5f, float volume = 0.5f) {
            SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = pitch, Volume = volume, MaxInstances = 3 }, pos);
        }

        /// <summary>慢速蜜滴迸发：dir 为主迸方向（Zero 则全向），带上抛弧感</summary>
        public static void DropletBurst(Vector2 pos, Vector2 dir, int count, float speed, float scaleMul = 1f, bool leaveBlot = true) {
            if (Main.dedServ) {
                return;
            }
            bool omni = dir == Vector2.Zero;
            for (int i = 0; i < count; i++) {
                Vector2 vel = omni
                    ? Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.35f, 1f) * speed
                    : dir.RotatedByRandom(0.65f) * Main.rand.NextFloat(0.5f, 1f) * speed;
                vel.Y -= Main.rand.NextFloat(0.4f, 1.4f);
                Color col = Main.rand.NextBool(3) ? HoneyDeep : HoneyAmber;
                PRTLoader.NewParticle<PRT_FishHoneyDrop>(pos + Main.rand.NextVector2Circular(5f, 5f), vel
                    , col, Main.rand.NextFloat(0.55f, 1f) * scaleMul)
                    ?.Configure(Main.rand.Next(70, 110), 0.16f, leaveBlot);
            }
        }

        /// <summary>蜇刺小蜜溅：2-3 颗慢滴 + 深琥珀微环，无大闪</summary>
        public static void StingSplash(Vector2 pos, Vector2 outward) {
            if (Main.dedServ) {
                return;
            }
            outward = outward.SafeNormalize(-Vector2.UnitY);
            int n = Main.rand.Next(2, 4);
            for (int i = 0; i < n; i++) {
                Vector2 vel = outward.RotatedByRandom(0.8f) * Main.rand.NextFloat(1.2f, 2.6f);
                vel.Y -= Main.rand.NextFloat(0.3f, 1f);
                PRTLoader.NewParticle<PRT_FishHoneyDrop>(pos, vel, Main.rand.NextBool() ? HoneyAmber : HoneyGold
                    , Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(24, 38), 0.14f, true);
            }
            PRTLoader.NewParticle<Content.PRTTypes.PRT_DWave>(pos, Vector2.Zero, HoneyDeep, 0.05f)
                ?.Configure(Vector2.One, outward.ToRotation(), 0.15f, 8);
        }

        /// <summary>
        /// 粘稠蜜丝：from→to 垂弧贝塞尔条带，stretch 0..1 越大越绷直、中段颈缩越细，
        /// 高拉伸时中段透出暖金薄蜜、颈点亮一粒将断高光
        /// </summary>
        public static void DrawStrand(SpriteBatch sb, Vector2 from, Vector2 to, float stretch, float alpha) {
            Texture2D tex = CWRAsset.Line?.Value;
            if (tex == null || alpha <= 0.02f) {
                return;
            }
            float dist = Vector2.Distance(from, to);
            if (dist < 3f) {
                return;
            }
            stretch = MathHelper.Clamp(stretch, 0f, 1f);
            int segs = Math.Clamp((int)(dist / 9f), 4, 20);
            //垂弧：越松垂得越深
            Vector2 mid = (from + to) * 0.5f + new Vector2(0f, dist * 0.26f * (1f - stretch));
            Vector2 prev = from;
            for (int i = 1; i <= segs; i++) {
                float t = i / (float)segs;
                Vector2 a = Vector2.Lerp(from, mid, t);
                Vector2 b = Vector2.Lerp(mid, to, t);
                Vector2 p = Vector2.Lerp(a, b, t);
                Vector2 seg = p - prev;
                //颈缩：绷紧时中段变细
                float neck = 1f - MathF.Sin(t * MathHelper.Pi) * (0.15f + 0.55f * stretch);
                float widthPx = MathHelper.Lerp(3f, 1.2f, stretch) * neck;
                Color col = Color.Lerp(HoneyDeep, HoneyGold, stretch * MathF.Sin(t * MathHelper.Pi) * 0.8f) * (alpha * 0.9f);
                Vector2 drawPos = (prev + p) * 0.5f - Main.screenPosition;
                sb.Draw(tex, drawPos, null, col, seg.ToRotation() + MathHelper.PiOver2, tex.Size() * 0.5f
                    , new Vector2(widthPx / tex.Width, seg.Length() / tex.Height * 1.12f), SpriteEffects.None, 0f);
                prev = p;
            }
            //将断瞬间：颈点一粒暖白高光
            if (stretch > 0.8f) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    Vector2 neckPos = (from + to) * 0.5f + new Vector2(0f, dist * 0.13f * (1f - stretch));
                    sb.Draw(glow, neckPos - Main.screenPosition, null, HoneyGlint with { A = 0 } * ((stretch - 0.8f) * 5f * 0.6f * alpha)
                        , 0f, glow.Size() * 0.5f, 0.07f, SpriteEffects.None, 0f);
                }
            }
        }
    }

    /// <summary>
    /// 琥珀蜜滴：低重力高阻力的粘稠液滴，初段悬滞欲坠再垂落，随速度拉伸；
    /// 落地转蜜斑短命 decal（压扁微摊开 + 一次琥珀 glint 缓扫后消退）。
    /// AlphaBlend 液体感，高光用 A=0 小点
    /// </summary>
    internal class PRT_FishHoneyDrop : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Color initialColor;
        private float gravity;
        private bool leaveBlot;
        private bool landed;
        private int landTime;
        private int blotLife;
        private float seed;

        public PRT_FishHoneyDrop Configure(int lifetime, float gravityPerFrame = 0.16f, bool blot = true) {
            Lifetime = lifetime;
            initialColor = Color;
            gravity = gravityPerFrame;
            leaveBlot = blot;
            return this;
        }

        public override void SetProperty() {
            seed = Main.rand.NextFloat(1000f);
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = 0f;
            leaveBlot = false;
            landed = false;
            landTime = 0;
            blotLife = 0;
            seed = 0f;
        }

        public override void AI() {
            if (landed) {
                Velocity = Vector2.Zero;
                return;
            }

            //粘稠：初段悬滞欲坠
            if (Time < 7) {
                Velocity *= 0.82f;
            }
            Velocity.X *= 0.955f;
            Velocity.Y += gravity;
            if (Velocity.Y > 7.5f) {
                Velocity.Y = 7.5f;
            }
            float t = LifetimeCompletion;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(t, 2.6f));
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            //落地成斑：延长寿命进入 decal 相
            if (leaveBlot && Time > 4 && Velocity.Y > 0.5f && Collision.SolidCollision(Position, 1, 1)) {
                landed = true;
                landTime = Time;
                blotLife = Main.rand.Next(60, 100);
                Lifetime = Time + blotLife;
                Position -= Velocity;
                Color = initialColor;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow?.Value;

            if (landed) {
                DrawBlot(spriteBatch, tex, glow, origin, pos);
                return false;
            }

            //随速度纵向拉伸：快成丝、慢成珠
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.055f, 0f, 0.9f);
            Vector2 scale = new Vector2(0.26f * (1f - stretch * 0.4f), 0.46f * (1f + stretch * 1.9f)) * Scale;
            //双层窄叠：中心更实，读作液滴而非光斑
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale * new Vector2(0.45f, 1f), SpriteEffects.None, 0f);
            //珠面高光：小、暖、A=0（AlphaBlend 批内即加色）
            if (glow != null && stretch < 0.5f) {
                float glintA = (1f - LifetimeCompletion) * (1f - stretch * 2f) * 0.5f;
                spriteBatch.Draw(glow, pos + new Vector2(-1.5f, -2.5f) * Scale, null
                    , FishHoneyVFX.HoneyGlint with { A = 0 } * glintA, 0f, glow.Size() * 0.5f, 0.05f * Scale, SpriteEffects.None, 0f);
            }
            return false;
        }

        private void DrawBlot(SpriteBatch spriteBatch, Texture2D tex, Texture2D glow, Vector2 origin, Vector2 pos) {
            float bt = blotLife > 0 ? (Time - landTime) / (float)blotLife : 1f;
            float inT = MathHelper.Clamp((Time - landTime) / 6f, 0f, 1f);
            float fade = 1f - MathF.Pow(bt, 3f);
            //落定后微摊开
            float spread = 1f + 0.4f * (1f - MathF.Exp(-(Time - landTime) * 0.1f));
            float alpha = inT * fade;
            if (alpha <= 0.02f) {
                return;
            }

            //软性暗底（径向图仅作底层）
            if (glow != null) {
                spriteBatch.Draw(glow, pos, null, FishHoneyVFX.HoneyDeep * (alpha * 0.42f), 0f
                    , glow.Size() * 0.5f, new Vector2(0.42f * spread, 0.13f) * Scale, SpriteEffects.None, 0f);
            }
            //蜜斑主体：竖条横放成扁液斑，两层异宽
            spriteBatch.Draw(tex, pos, null, initialColor * (alpha * 0.8f), MathHelper.PiOver2, origin
                , new Vector2(0.13f, 0.5f * spread) * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, FishHoneyVFX.HoneyGold * (alpha * 0.5f), MathHelper.PiOver2, origin
                , new Vector2(0.08f, 0.3f * spread) * Scale, SpriteEffects.None, 0f);
            //溅点卫星滴
            if (glow != null) {
                for (int i = 0; i < 2; i++) {
                    float sx = MathF.Sin(seed * 3.7f + i * 2.4f);
                    Vector2 off = new Vector2(sx * 15f * (i == 0 ? 1f : -0.7f), -1f) * Scale;
                    spriteBatch.Draw(glow, pos + off, null, FishHoneyVFX.HoneyAmber * (alpha * 0.55f), 0f
                        , glow.Size() * 0.5f, 0.045f * Scale, SpriteEffects.None, 0f);
                }
                //一次性琥珀 glint 缓扫
                float gT = MathHelper.Clamp((bt - 0.22f) / 0.3f, 0f, 1f);
                if (gT > 0f && gT < 1f) {
                    float gx = MathHelper.Lerp(-12f, 12f, gT) * Scale * spread;
                    spriteBatch.Draw(glow, pos + new Vector2(gx, -1f), null
                        , FishHoneyVFX.HoneyGlint with { A = 0 } * (MathF.Sin(gT * MathHelper.Pi) * 0.45f * alpha), 0f
                        , glow.Size() * 0.5f, 0.05f, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
