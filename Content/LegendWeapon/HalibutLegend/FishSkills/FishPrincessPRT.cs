using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 公主鱼星尘 mote，小而慢的粉彩尘埃，微浮力上飘 + 横向摇曳 + 十字星闪烁
    /// 绘本里撒的亮粉质感，加色绘制但个体极小、低亮，靠数量克制防糊
    /// </summary>
    internal class PRT_FishPrincessMote : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        [VaultLoaden(CWRConstant.Masking + "StarGlow01")]
        internal static Asset<Texture2D> StarTex = null;

        private float swaySeed;
        private float twinkleSeed;

        public PRT_FishPrincessMote Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            swaySeed = 0f;
            twinkleSeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            swaySeed = Main.rand.NextFloat(MathHelper.TwoPi);
            twinkleSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(42, 70);
            }
        }

        public override void AI() {
            //缓停后微浮力上飘，横向缓摆
            Velocity *= 0.955f;
            Velocity.Y -= 0.009f;
            Velocity.X += MathF.Sin(Time * 0.055f + swaySeed) * 0.014f;

            float lc = LifetimeCompletion;
            //缓入缓出
            Opacity = MathF.Min(lc * 6f, 1f) * (1f - MathF.Pow(lc, 2.2f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D glow = TexValue;
            Texture2D star = StarTex?.Value;
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };

            //底层软点，极小低亮
            spriteBatch.Draw(glow, pos, null, col * (0.42f * Opacity), 0f
                , glow.Size() * 0.5f, 0.13f * Scale, SpriteEffects.None, 0f);

            //十字星闪烁
            if (star != null) {
                float tw = 0.5f + 0.5f * MathF.Sin(Time * 0.23f + twinkleSeed);
                spriteBatch.Draw(star, pos, null, col * (0.75f * Opacity * tw), Rotation
                    , star.Size() * 0.5f, 0.09f * Scale * (0.7f + tw * 0.5f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 公主鱼绘本圆点，哑光粉彩圆点，弹出带过冲、随后轻飘坠落缩小
    /// AlphaBlend 非加色，读作绘本纸屑而非光点
    /// </summary>
    internal class PRT_FishPrincessDot : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Color initialColor;

        public PRT_FishPrincessDot Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
        }

        public override void AI() {
            Velocity *= 0.90f;
            //中段后轻飘下坠
            if (LifetimeCompletion > 0.4f) {
                Velocity.Y += 0.045f;
            }

            float lc = LifetimeCompletion;
            //前 25% 过冲弹入，之后缓缩
            float pop = lc < 0.25f ? FishPrincessVFX.EaseOutBack(lc * 4f) : 1f - (lc - 0.25f) * 0.55f;
            Opacity = (1f - MathF.Pow(lc, 3f)) * MathHelper.Clamp(pop, 0f, 1.2f);
            Color = Color.Lerp(initialColor, FishPrincessVFX.DeepLilac, lc * 0.35f);
            Rotation += Velocity.X * 0.02f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            float px = 9f * Scale * MathHelper.Clamp(Opacity + 0.25f, 0f, 1.1f);
            //哑光单层，真 alpha 贴图直绘
            spriteBatch.Draw(tex, pos, null, Color * (0.92f * Opacity), Rotation
                , tex.Size() * 0.5f, px / tex.Width, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 公主鱼缎带残迹，弹体死亡后接管其轨迹点链，整体轻微上飘 + 摇曳
    /// 尾部先蚀（从尾梢向死亡点收缩），缎带活得比弹体久
    /// </summary>
    internal class PRT_FishPrincessRibbonFade : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        public const int MaxPts = 22;
        private readonly Vector2[] pts = new Vector2[MaxPts];
        private int ptCount;
        private Color mid;
        private Color edge;
        private float sheenSeed;

        public PRT_FishPrincessRibbonFade Configure(ReadOnlySpan<Vector2> source, int count
            , Color midCol, Color edgeCol, int lifetime) {
            ptCount = Math.Min(count, MaxPts);
            for (int i = 0; i < ptCount; i++) {
                pts[i] = source[i];
            }
            mid = midCol;
            edge = edgeCol;
            Lifetime = lifetime;
            sheenSeed = Main.rand.NextFloat();
            return this;
        }

        public override void Reset() {
            base.Reset();
            ptCount = 0;
            mid = default;
            edge = default;
            sheenSeed = 0f;
        }

        public override void AI() {
            //整带上飘 + 逐点摇曳，读作缎带失去动力后飘散
            for (int i = 0; i < ptCount; i++) {
                pts[i].Y -= 0.30f;
                pts[i].X += MathF.Sin(Time * 0.13f + i * 0.52f) * 0.22f;
            }
            Opacity = 1f - MathF.Pow(LifetimeCompletion, 1.6f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (ptCount < 3) {
                return false;
            }
            //尾部先蚀，可见段从尾梢向头收缩
            int visible = (int)(ptCount * (1f - LifetimeCompletion * 0.85f));
            if (visible < 3) {
                return false;
            }
            FishPrincessVFX.DrawRibbonSegments(spriteBatch, pts.AsSpan(0, ptCount), visible
                , 5.5f * Scale, mid, edge, 0.55f * Opacity, sheenSeed + Time * 0.02f);
            return false;
        }
    }
}
