using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using static CalamityOverhaul.Content.Rarities.RarityNameEffects;

namespace CalamityOverhaul.Content.Rarities
{
    /// <summary>
    /// 四传奇专属稀有度的共同骨架：主题双色横向渐变 + 主题暗色描边 + 字缘加色呼吸底光，
    /// 各传奇再叠一条自己的签名行为。每把一个独立稀有度而非一档查表，
    /// 是为了拾取飘字、[i:] 标签等只读 RarityColor 的位置也能拿到各自主题色
    /// </summary>
    internal abstract class LegendRarity : CWRRarity
    {
        protected abstract Color Primary { get; }
        protected abstract Color Secondary { get; }
        protected abstract Color Edge { get; }
        /// <summary>渐变相位速度</summary>
        protected virtual float GradientSpeed => 1.2f;

        public override Color BaseColor => Primary;

        public override void DrawName(SpriteBatch sb, Item item, string text, Vector2 pos, Color color, Vector2 scale, float time) {
            float fade = FadeOf(color);
            Color primary = Fade(Primary, fade);
            Color secondary = Fade(Secondary, fade);

            //字缘加色底光，只占字外一圈
            float breath = Breath(time, 2.8f, 0.10f, 0.18f);
            DrawOutline(sb, text, pos, secondary with { A = 0 } * breath, scale, 2.5f);
            DrawOutline(sb, text, pos, Fade(Edge, fade), scale, 1.2f);

            GlyphLayout layout = Layout(text, pos, scale);
            for (int i = 0; i < layout.Count; i++) {
                float t = 0.5f + 0.5f * MathF.Sin(layout.CenterX(i) * 0.02f + time * GradientSpeed);
                DrawGlyph(sb, layout, i, GlyphOffset(i, time, scale), Color.Lerp(primary, secondary, t), scale);
            }
            DrawAccent(sb, layout, fade, time, scale);
        }

        /// <summary>逐字位移（如鬼伞的轻晃）</summary>
        protected virtual Vector2 GlyphOffset(int i, float time, Vector2 scale) => Vector2.Zero;

        /// <summary>签名行为，画在正文之上</summary>
        protected virtual void DrawAccent(SpriteBatch sb, GlyphLayout layout, float fade, float time, Vector2 scale) { }
    }

    /// <summary>鬼切：绯红到纸白，每隔数秒一道刃光自左向右掠过</summary>
    internal sealed class OnikiriLegendRarity : LegendRarity
    {
        private static readonly Color Crimson = Color.Lerp(OnikiriUITheme.Bright, OnikiriUITheme.Paper, 0.22f);
        private const float SlashPeriod = 5.2f;
        private const float SlashDuration = 0.3f;

        public override int Tier => 101;
        protected override Color Primary => Crimson;
        protected override Color Secondary => OnikiriUITheme.Paper;
        protected override Color Edge => OnikiriUITheme.Ink;

        protected override void DrawAccent(SpriteBatch sb, GlyphLayout layout, float fade, float time, Vector2 scale) {
            float cycle = time / SlashPeriod % 1f;
            float window = SlashDuration / SlashPeriod;
            if (cycle > window) {
                return;
            }
            float sweepX = layout.Origin.X - 20f + cycle / window * (layout.Width + 40f);
            Color hot = Fade(OnikiriUITheme.HotWhite, fade);
            for (int i = 0; i < layout.Count; i++) {
                float d = (layout.CenterX(i) - sweepX) / 14f;
                float w = MathF.Exp(-d * d);
                if (w < 0.02f) {
                    continue;
                }
                DrawGlyph(sb, layout, i, Vector2.Zero, hot * w, scale);
            }
        }
    }

    /// <summary>鬼伞：墨青到月白，逐字随雨幕轻晃</summary>
    internal sealed class KikasaLegendRarity : LegendRarity
    {
        private static readonly Color MoonWhite = new(222, 236, 240);
        private static readonly Color DeepInk = new(12, 26, 32);

        public override int Tier => 102;
        protected override Color Primary => KikasaStoryTheme.WetInk;
        protected override Color Secondary => MoonWhite;
        protected override Color Edge => DeepInk;
        protected override float GradientSpeed => 0.8f;

        protected override Vector2 GlyphOffset(int i, float time, Vector2 scale)
            => new(0f, MathF.Sin(time * 1.6f + i * 0.7f) * 0.6f * scale.Y);
    }

    /// <summary>比目鱼：深海青到日光金，偶发一点焦散星光</summary>
    internal sealed class HalibutLegendRarity : LegendRarity
    {
        private const float GlintPeriod = 3.1f;
        private const float GlintLife = 0.35f;

        public override int Tier => 103;
        protected override Color Primary => HalibutTheme.Glow;
        protected override Color Secondary => HalibutTheme.Accent;
        protected override Color Edge => HalibutTheme.Deep;

        protected override void DrawAccent(SpriteBatch sb, GlyphLayout layout, float fade, float time, Vector2 scale) {
            float cycle = time / GlintPeriod;
            int index = (int)MathF.Floor(cycle);
            float t = cycle - index;
            if (t > GlintLife) {
                return;
            }
            float intensity = MathF.Sin(MathHelper.Pi * t / GlintLife);
            Vector2 p = new(
                layout.Origin.X + Hash01(index, 1) * layout.Width,
                layout.Origin.Y + layout.Height * (0.2f + 0.4f * Hash01(index, 2)));
            DrawStar(sb, p, 8f + 3f * intensity, Fade(HalibutTheme.Caustic, fade) * (0.8f * intensity), intensity * 0.5f);
        }
    }

    /// <summary>SHPC：赛博青单色系快速换相，扫描线下扫，逐字偶发数字闪断</summary>
    internal sealed class SHPCLegendRarity : LegendRarity
    {
        private const float ScanPeriod = 1.8f;

        public override int Tier => 104;
        protected override Color Primary => SHPCTheme.Cyan;
        protected override Color Secondary => SHPCTheme.CyanHi;
        protected override Color Edge => SHPCTheme.ShadowDark;
        protected override float GradientSpeed => 2.2f;

        protected override void DrawAccent(SpriteBatch sb, GlyphLayout layout, float fade, float time, Vector2 scale) {
            //扫描线自上而下穿过字身
            float scanY = layout.Origin.Y + layout.Height * (0.15f + 0.7f * (time / ScanPeriod % 1f));
            DrawHLine(sb, new Vector2(layout.Origin.X - 2f, scanY), layout.Width + 4f, 1f, Fade(SHPCTheme.CyanHi, fade) with { A = 0 } * 0.35f);

            //每秒 12 拍，每拍每字 5% 概率压暗一拍
            int beat = (int)(time * 12f);
            Color dim = Fade(SHPCTheme.ShadowDark, fade) * 0.55f;
            for (int i = 0; i < layout.Count; i++) {
                if (Hash01(i, beat) < 0.05f) {
                    DrawGlyph(sb, layout, i, Vector2.Zero, dim, scale);
                }
            }
        }
    }
}
