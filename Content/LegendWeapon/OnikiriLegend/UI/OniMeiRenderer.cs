using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>改铭台静态绘制:烛光/白布/刀身简笔/铭位框环/鏨盘扇/烙印木牌/大字/静物/工具</summary>
    internal static class OniMeiRenderer
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        private static float Hash01(int n) {
            unchecked {
                n = n * 374761393 + 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0x7FFFFFFF) / (float)int.MaxValue;
            }
        }

        //====================== 烛光 ======================

        /// <summary>底缘烛光:光自屏下缘涌上,焰心呼吸+低频摇曳(对位点鬼簿的绯月在上)</summary>
        public static void DrawCandleGlow(SpriteBatch sb, Vector2 bladeCenter, float alpha, float time, Vector2 parallax) {
            float flick = 0.86f + 0.10f * (float)Math.Sin(time * 2.1f) + 0.04f * (float)Math.Sin(time * 7.3f + 1.7f);
            Vector2 glowBase = new Vector2(bladeCenter.X, OnikiriUITheme.UIScreenH + 60f) + parallax;
            //三层暖光,越往上越淡
            OniBrush.DrawBacklight(sb, glowBase, 460f * flick, OnikiriUITheme.CandleWarm, alpha * 0.5f);
            OniBrush.DrawBacklight(sb, glowBase + new Vector2(-140f, 20f), 300f, OnikiriUITheme.BurnDim, alpha * 0.22f * flick);
            OniBrush.DrawBacklight(sb, glowBase + new Vector2(150f, 30f), 260f, OnikiriUITheme.Deep, alpha * 0.25f);
        }

        //====================== 白布 ======================

        /// <summary>解剑白布:shader 织纹布面优先(OniMeiStand.TechCloth),缺席退回 CPU 三段简笔</summary>
        public static void DrawCloth(SpriteBatch sb, Rectangle rect, float alpha, float reveal, float time) {
            float unroll = reveal * (2f - reveal);
            Rectangle shown = new(rect.X, (int)(rect.Center.Y - rect.Height * 0.5f * unroll),
                rect.Width, (int)(rect.Height * unroll));
            if (shown.Height < 6) {
                return;
            }

            //布影:羽化的落影,不再一整块硬边矩形
            OniBrush.DrawFeathered(sb, shown.Center.ToVector2() + new Vector2(5f, 9f), 0f,
                new Vector2(shown.Width * 0.98f, shown.Height * 0.96f), new Color(8, 2, 5), alpha * alpha * 0.75f);

            if (OniMeiStandDraw.Available) {
                OniMeiStandDraw.DrawCloth(sb, shown, alpha, time);
            }
            else {
                DrawClothFallback(sb, shown, alpha, time);
            }

            //边缘微垂:两端下摆一点弧影(两条路径共用)
            sb.Draw(Pixel, new Vector2(shown.X + 3f, shown.Bottom + 3f), PixelSrc, new Color(8, 2, 5) * (alpha * 0.4f),
                0.16f, new Vector2(0f, 0.5f), new Vector2(26f, 4f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, new Vector2(shown.Right - 3f, shown.Bottom + 3f), PixelSrc, new Color(8, 2, 5) * (alpha * 0.4f),
                MathHelper.Pi - 0.16f, new Vector2(0f, 0.5f), new Vector2(26f, 4f), SpriteEffects.None, 0f);
        }

        /// <summary>CPU 简笔布面(shader 降级):三段明暗+褶皱暗线+深红压边</summary>
        private static void DrawClothFallback(SpriteBatch sb, Rectangle shown, float alpha, float time) {
            //布体:暗调素布,烛光里下缘更暖更亮
            Color clothTop = new Color(46, 40, 36) * (alpha * 0.96f);
            Color clothMid = new Color(58, 50, 44) * (alpha * 0.96f);
            Color clothLow = new Color(70, 58, 48) * (alpha * 0.96f);
            int h3 = shown.Height / 3;
            sb.Draw(Pixel, new Rectangle(shown.X, shown.Y, shown.Width, h3), PixelSrc, clothTop);
            sb.Draw(Pixel, new Rectangle(shown.X, shown.Y + h3, shown.Width, h3), PixelSrc, clothMid);
            sb.Draw(Pixel, new Rectangle(shown.X, shown.Y + h3 * 2, shown.Width, shown.Height - h3 * 2), PixelSrc, clothLow);

            //褶皱:数道纵向暗线,位置恒定,烛光里微息
            for (int i = 0; i < 7; i++) {
                float u = 0.06f + Hash01(i * 37 + 5) * 0.88f;
                float x = shown.X + shown.Width * u;
                float breath = 0.8f + 0.2f * (float)Math.Sin(time * 0.9f + i * 2.3f);
                float w = 1.4f + Hash01(i * 91) * 1.6f;
                sb.Draw(Pixel, new Vector2(x, shown.Center.Y), PixelSrc, new Color(24, 18, 16) * (alpha * 0.5f * breath),
                    (Hash01(i * 53) - 0.5f) * 0.06f, new Vector2(0.5f), new Vector2(w, shown.Height * 0.94f), SpriteEffects.None, 0f);
                //褶脊高光在暗线旁
                sb.Draw(Pixel, new Vector2(x + 2.4f, shown.Center.Y), PixelSrc, new Color(96, 82, 68) * (alpha * 0.3f * breath),
                    (Hash01(i * 53) - 0.5f) * 0.06f, new Vector2(0.5f), new Vector2(1f, shown.Height * 0.88f), SpriteEffects.None, 0f);
            }

            //深红压边:上下缘各一线(裱布的绫边)
            sb.Draw(Pixel, new Rectangle(shown.X, shown.Y, shown.Width, 3), PixelSrc, OnikiriUITheme.Deep * (alpha * 0.6f));
            sb.Draw(Pixel, new Rectangle(shown.X, shown.Bottom - 3, shown.Width, 3), PixelSrc, OnikiriUITheme.Deep * (alpha * 0.75f));
        }

        //====================== 刀身 ======================

        /// <summary>刀身入口:shader 在则 shader,否则 CPU 简笔;刀鸣/鬼影掠面恒走 CPU 叠加</summary>
        public static void DrawBlade(SpriteBatch sb, Vector2 center, Vector2 dir, Vector2 perp,
            float bladeW, float quadH, float alpha, float time, float slide, float songRun, float wispRun) {
            //布上刀影
            Vector2 tip = center - dir * (bladeW * 0.5f);
            sb.Draw(Pixel, center + new Vector2(2f, 7f), PixelSrc, new Color(8, 2, 5) * (alpha * 0.5f),
                OnikiriUITheme.MeiBladeCant, new Vector2(0.5f), new Vector2(bladeW * 0.98f, quadH * 0.30f), SpriteEffects.None, 0f);

            if (OniMeiBladeDraw.Available) {
                OniMeiBladeDraw.Draw(sb, center, OnikiriUITheme.MeiBladeCant, new Vector2(bladeW, quadH), alpha, time);
            }
            else {
                DrawBladeFallback(sb, tip, dir, perp, bladeW, quadH, alpha, time);
            }

            //拔刀扫入的白闪:两端没入的软流光,不再一根硬边长条
            if (slide > 2f) {
                float flash = MathHelper.Clamp(slide / 60f, 0f, 1f);
                OniBrush.DrawSoftStreak(sb, center - perp * (quadH * 0.135f), OnikiriUITheme.MeiBladeCant,
                    bladeW, 2.0f, OnikiriUITheme.HotWhite, alpha * 0.55f * flash, glowMul: 0.6f);
            }

            //刀鸣:一线白光沿刃口颤过(软芯+辉光,读作流光不是方块)
            if (songRun >= 0f) {
                float t = songRun / 90f;
                float u = MathHelper.Lerp(0.04f, 0.96f, t);
                float pulse = (float)Math.Sin(t * MathHelper.Pi);
                Vector2 pos = tip + dir * (bladeW * u) - perp * (quadH * 0.135f + (float)Math.Sin(songRun * 1.7f) * 1.2f);
                OniBrush.DrawSoftStreak(sb, pos, OnikiriUITheme.MeiBladeCant, 54f, 1.5f,
                    OnikiriUITheme.HotWhite, alpha * 0.8f * pulse, glowMul: 1.1f);
                OniBrush.DrawSoftDot(sb, pos, 11f, OnikiriUITheme.Bright, alpha * 0.30f * pulse);
            }

            //鬼影掠面:刀面倒影里一道暗痕走过(羽化退晕,影子没有直角)
            if (wispRun >= 0f) {
                float t = wispRun / 70f;
                float u = MathHelper.Lerp(0.9f, 0.08f, t);
                float pulse = (float)Math.Sin(t * MathHelper.Pi);
                Vector2 pos = tip + dir * (bladeW * u) + perp * ((float)Math.Sin(t * 9f) * 2f);
                OniBrush.DrawFeathered(sb, pos, OnikiriUITheme.MeiBladeCant + 0.05f,
                    new Vector2(52f, quadH * 0.12f), OnikiriUITheme.Ink, alpha * 0.8f * pulse);
                OniBrush.DrawSoftStreak(sb, pos + dir * 40f, OnikiriUITheme.MeiBladeCant, 26f, 1.6f,
                    OnikiriUITheme.Paper, alpha * 0.12f * pulse, glowMul: 0.3f);
            }
        }

        /// <summary>
        /// CPU 简笔刀身(shader 降级同款):素钢三段明暗+刃口白线+切先收窄,
        /// 茎段锈色+鑢目斜纹+目钉孔+区分界+铜金 habaki
        /// </summary>
        public static void DrawBladeFallback(SpriteBatch sb, Vector2 tip, Vector2 dir, Vector2 perp,
            float bladeW, float quadH, float alpha, float time) {
            const int Segs = 30;
            float cant = OnikiriUITheme.MeiBladeCant;
            float tangStart = 1f - OnikiriUITheme.MeiTangFraction;
            float bladeHalf = quadH * 0.135f;
            float segLen = bladeW / Segs;
            float sheenU = time * 0.055f - (float)Math.Floor(time * 0.055f);

            for (int i = 0; i < Segs; i++) {
                float u0 = i / (float)Segs;
                float um = (i + 0.5f) / Segs;
                bool isTang = um >= tangStart;

                //切先收窄:前 8% 由零涨满;茎段略瘦,茎尾平切
                float half = bladeHalf;
                if (um < 0.085f) {
                    float k = um / 0.085f;
                    half *= k * (2f - k);
                }
                if (isTang) {
                    half *= 0.82f;
                }
                Vector2 segC = tip + dir * (bladeW * (u0 + 0.5f / Segs));

                if (!isTang) {
                    //素钢三段:刃侧亮(上),镐面中,栋侧沉(下)
                    float sheen = (float)Math.Exp(-Math.Pow((um - sheenU) * 7f, 2)) * 0.10f;
                    float lum = 1f + sheen;
                    sb.Draw(Pixel, segC - perp * (half * 0.62f), PixelSrc, OnikiriUITheme.Paper * (alpha * 0.60f * lum),
                        cant, new Vector2(0.5f), new Vector2(segLen + 0.7f, half * 0.76f), SpriteEffects.None, 0f);
                    sb.Draw(Pixel, segC, PixelSrc, OnikiriUITheme.Paper * (alpha * 0.47f * lum),
                        cant, new Vector2(0.5f), new Vector2(segLen + 0.7f, half * 0.72f), SpriteEffects.None, 0f);
                    sb.Draw(Pixel, segC + perp * (half * 0.60f), PixelSrc, OnikiriUITheme.Paper * (alpha * 0.34f),
                        cant, new Vector2(0.5f), new Vector2(segLen + 0.7f, half * 0.80f), SpriteEffects.None, 0f);
                    //刃口白线(上缘) + 烛光暖染(下缘)
                    sb.Draw(Pixel, segC - perp * half, PixelSrc, OnikiriUITheme.HotWhite * (alpha * 0.55f * lum),
                        cant, new Vector2(0.5f), new Vector2(segLen + 0.7f, 1.3f), SpriteEffects.None, 0f);
                    sb.Draw(Pixel, segC + perp * half, PixelSrc, OnikiriUITheme.CandleWarm * (alpha * 0.16f),
                        cant, new Vector2(0.5f), new Vector2(segLen + 0.7f, 1.4f), SpriteEffects.None, 0f);
                    //镐线:一线极淡的分面
                    sb.Draw(Pixel, segC - perp * (half * 0.28f), PixelSrc, OnikiriUITheme.TextDim * (alpha * 0.22f),
                        cant, new Vector2(0.5f), new Vector2(segLen + 0.7f, 1f), SpriteEffects.None, 0f);
                }
                else {
                    //茎:黑锈铁,下缘略暖
                    float rustN = Hash01(i * 131 + 7);
                    Color rust = Color.Lerp(new Color(52, 34, 26), new Color(74, 48, 32), rustN);
                    sb.Draw(Pixel, segC, PixelSrc, rust * (alpha * 0.95f),
                        cant, new Vector2(0.5f), new Vector2(segLen + 0.7f, half * 2f), SpriteEffects.None, 0f);
                    sb.Draw(Pixel, segC - perp * half, PixelSrc, new Color(96, 66, 44) * (alpha * 0.5f),
                        cant, new Vector2(0.5f), new Vector2(segLen + 0.7f, 1.2f), SpriteEffects.None, 0f);
                }
            }

            //====鑢目:茎上斜向锉痕====
            float tangLen = bladeW * OnikiriUITheme.MeiTangFraction;
            Vector2 tangA = tip + dir * (bladeW * tangStart);
            int marks = (int)(tangLen / 9f);
            for (int i = 1; i < marks; i++) {
                Vector2 pos = tangA + dir * (i * 9f);
                sb.Draw(Pixel, pos, PixelSrc, new Color(30, 20, 16) * (alpha * 0.55f),
                    cant + 0.62f, new Vector2(0.5f), new Vector2(1f, bladeHalf * 1.3f), SpriteEffects.None, 0f);
            }

            //====区(machi)分界 + habaki 铜金口====
            sb.Draw(Pixel, tangA, PixelSrc, OnikiriUITheme.Deep * (alpha * 0.55f),
                cant + MathHelper.PiOver2, new Vector2(0.5f), new Vector2(bladeHalf * 2.1f, 1.4f), SpriteEffects.None, 0f);
            Vector2 habakiC = tangA - dir * (bladeW * 0.017f);
            sb.Draw(Pixel, habakiC, PixelSrc, OnikiriUITheme.GoldDeep * (alpha * 0.95f),
                cant, new Vector2(0.5f), new Vector2(bladeW * 0.030f, bladeHalf * 2.25f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, habakiC - perp * (bladeHalf * 1.02f), PixelSrc, OnikiriUITheme.GoldInlay * (alpha * 0.8f),
                cant, new Vector2(0.5f), new Vector2(bladeW * 0.030f, 1.4f), SpriteEffects.None, 0f);

            //====目钉孔====
            Vector2 mekugi = tip + dir * (bladeW * 0.815f);
            sb.Draw(Pixel, mekugi, PixelSrc, OnikiriUITheme.Ink * (alpha * 0.95f), 0f,
                new Vector2(0.5f), new Vector2(7.5f, 6.5f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, mekugi, PixelSrc, new Color(8, 2, 5) * alpha, 0f,
                new Vector2(0.5f), new Vector2(4.6f, 4f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, mekugi + new Vector2(1.4f, 1.6f), PixelSrc, new Color(96, 66, 44) * (alpha * 0.5f), 0f,
                new Vector2(0.5f), new Vector2(1.6f), SpriteEffects.None, 0f);
        }

        //====================== 开屏编舞小件 ======================

        /// <summary>目钉飞脱:小木销自孔中弹出翻滚坠落</summary>
        public static void DrawMekugiPop(SpriteBatch sb, Vector2 hole, Vector2 dir, Vector2 perp, float anim, float alpha) {
            float t = anim;
            //抛物:先上后落
            Vector2 pos = hole - perp * (34f * t - 46f * t * t) + dir * (18f * t);
            float fade = 1f - MathHelper.Clamp((t - 0.7f) / 0.3f, 0f, 1f);
            float rot = t * 9f;
            sb.Draw(Pixel, pos, PixelSrc, new Color(96, 66, 44) * (alpha * 0.9f * fade), rot,
                new Vector2(0.5f), new Vector2(7f, 3f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, pos, PixelSrc, new Color(140, 100, 62) * (alpha * 0.6f * fade), rot,
                new Vector2(0.5f), new Vector2(4f, 1.4f), SpriteEffects.None, 0f);
            //出孔那一下的轻闪
            if (t < 0.25f) {
                OniBrush.DrawBacklight(sb, hole, 16f, OnikiriUITheme.CandleWarm, alpha * (1f - t / 0.25f) * 0.6f);
            }
        }

        /// <summary>柄影褪去:漆柄剪影自茎上滑脱,菱巻纹随行</summary>
        public static void DrawTsukaSlideOff(SpriteBatch sb, Vector2 tangEnd, Vector2 dir, Vector2 perp, float anim, float alpha) {
            float ease = anim * (2f - anim);
            Vector2 grip = tangEnd - dir * 70f + dir * (ease * 250f);
            float fade = (1f - anim) * 0.92f;
            float cant = OnikiriUITheme.MeiBladeCant;
            //柄身
            sb.Draw(Pixel, grip, PixelSrc, OnikiriUITheme.Dark * (alpha * fade),
                cant, new Vector2(0f, 0.5f), new Vector2(150f, 15f), SpriteEffects.None, 0f);
            //菱巻:交错朱菱
            for (int i = 0; i < 8; i++) {
                Vector2 p = grip + dir * (12f + i * 17f) + perp * ((i % 2 == 0 ? 1f : -1f) * 2.4f);
                sb.Draw(Pixel, p, PixelSrc, OnikiriUITheme.Deep * (alpha * fade * 0.9f),
                    cant + MathHelper.PiOver4, new Vector2(0.5f), new Vector2(6.5f), SpriteEffects.None, 0f);
            }
            //柄头
            sb.Draw(Pixel, grip + dir * 150f, PixelSrc, OnikiriUITheme.Deep * (alpha * fade),
                cant, new Vector2(0.5f), new Vector2(5f, 17f), SpriteEffects.None, 0f);
        }

        //====================== 铭位 ======================

        /// <summary>空铭位:凿框虚线菱,悬停白亮微转</summary>
        public static void DrawSlotEmpty(SpriteBatch sb, Vector2 pos, float size, float hover, float select,
            float alpha, float time, float rotation) {
            float breath = 0.55f + 0.25f * (float)Math.Sin(time * 1.8f + pos.X * 0.01f);
            float a = alpha * (breath * 0.5f + hover * 0.5f + select * 0.3f);
            float r = size * 0.5f;
            float spin = rotation + hover * (float)Math.Sin(time * 2.2f) * 0.03f;

            //菱形四边各两段虚线
            Vector2[] corners = new Vector2[4];
            for (int i = 0; i < 4; i++) {
                corners[i] = pos + (spin + MathHelper.PiOver2 * i + MathHelper.PiOver4).ToRotationVector2() * r;
            }
            Color line = Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.Paper, hover) * a;
            for (int e = 0; e < 4; e++) {
                Vector2 c0 = corners[e];
                Vector2 c1 = corners[(e + 1) % 4];
                DrawDash(sb, Vector2.Lerp(c0, c1, 0.08f), Vector2.Lerp(c0, c1, 0.36f), line, 1.2f);
                DrawDash(sb, Vector2.Lerp(c0, c1, 0.64f), Vector2.Lerp(c0, c1, 0.92f), line, 1.2f);
            }
            //心点:一粒极小的凿位标记
            sb.Draw(Pixel, pos, PixelSrc, line * 0.8f, spin + MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(2.6f + hover * 1.2f), SpriteEffects.None, 0f);
        }

        private static void DrawDash(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float thick) {
            Vector2 edge = b - a;
            float len = edge.Length();
            if (len < 0.5f) {
                return;
            }
            sb.Draw(Pixel, a, PixelSrc, color, edge.ToRotation(), new Vector2(0f, 0.5f),
                new Vector2(len, thick), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 铭位常驻标记:呼吸暖芒垫底+环上小刻标(朱菱+垂针)+周期巡环亮弧+开屏涟漪,
        /// 让三处铭位不靠悬停也读得出"此处可铭"
        /// </summary>
        public static void DrawSlotMarker(SpriteBatch sb, Vector2 pos, float radius, bool engraved,
            float hover, float alpha, float time, float ripple, int index) {
            float breath = OnikiriUITheme.Breath(time, index * 0.77f, 1.5f);
            //呼吸暖芒:空位醒目(等着落鏨),已铭收敛一档;悬停时让位给环
            float baseA = engraved ? 0.10f : 0.20f;
            OniBrush.DrawSoftDot(sb, pos, radius * (1.45f + breath * 0.35f), OnikiriUITheme.CandleWarm,
                alpha * (baseA + breath * 0.09f) * (1f - hover * 0.55f));

            //环上小刻标:一枚朱菱悬在环顶,垂一根短针指向铭位
            float tickLift = breath * 2.2f;
            Vector2 tickTop = pos - Vector2.UnitY * (radius + 14f + tickLift);
            Color tick = Color.Lerp(OnikiriUITheme.Seal, OnikiriUITheme.Bright, hover)
                * (alpha * (0.50f + breath * 0.28f + hover * 0.22f));
            sb.Draw(Pixel, tickTop, PixelSrc, tick, MathHelper.PiOver4, new Vector2(0.5f),
                new Vector2(4.4f + hover * 1.2f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, tickTop + Vector2.UnitY * 6.5f, PixelSrc, tick * 0.85f, 0f,
                new Vector2(0.5f, 0f), new Vector2(1.1f, 6f), SpriteEffects.None, 0f);

            //周期巡环:一小段亮弧每约六秒绕行一周(三位错相),睡着的标记轮流醒一下
            float sweepT = (time * 0.155f + index * 0.36f) % 1f;
            if (sweepT < 0.30f) {
                float k = sweepT / 0.30f;
                float fade = (float)Math.Sin(k * MathHelper.Pi);
                float baseAng = k * MathHelper.TwoPi - MathHelper.PiOver2;
                Color arc = OnikiriUITheme.GoldInlay * (alpha * 0.55f * fade * (1f - hover));
                for (int i = 0; i < 4; i++) {
                    float ang = baseAng + i * 0.16f;
                    Vector2 a = pos + ang.ToRotationVector2() * radius;
                    sb.Draw(Pixel, a, PixelSrc, arc * (1f - i * 0.2f), ang + MathHelper.PiOver2,
                        new Vector2(0f, 0.5f), new Vector2(5f, 1.1f), SpriteEffects.None, 0f);
                }
            }

            //开屏涟漪:一圈刻度环自铭位扩散,伴一记软亮,开台即点名三处位置
            if (ripple > 0.01f && ripple < 0.995f) {
                float rr = radius * (0.55f + ripple * 2.1f);
                float ra = (1f - ripple) * (1f - ripple);
                Color ring = Color.Lerp(OnikiriUITheme.HotWhite, OnikiriUITheme.Seal, ripple) * (alpha * 0.8f * ra);
                for (int i = 0; i < 12; i++) {
                    float ang = MathHelper.TwoPi * i / 12f + ripple * 0.9f;
                    Vector2 a = pos + ang.ToRotationVector2() * rr;
                    sb.Draw(Pixel, a, PixelSrc, ring, ang, new Vector2(0f, 0.5f),
                        new Vector2(4f + ripple * 3f, 1.1f), SpriteEffects.None, 0f);
                }
                OniBrush.DrawSoftDot(sb, pos, rr * 0.8f, OnikiriUITheme.CandleWarm, alpha * 0.35f * ra);
            }
        }

        /// <summary>铭位环:悬停点亮一圈短刻度,选中加朱色常亮</summary>
        public static void DrawSlotRing(SpriteBatch sb, Vector2 pos, float radius, float hover, float select,
            float alpha, float time) {
            float show = Math.Max(hover, select * 0.9f);
            if (show < 0.03f) {
                return;
            }
            Color col = Color.Lerp(OnikiriUITheme.Paper, OnikiriUITheme.Seal, select) * (alpha * 0.6f * show);
            float spin = time * (0.2f + hover * 0.25f);
            for (int i = 0; i < 10; i++) {
                float ang = spin + MathHelper.TwoPi * i / 10f;
                Vector2 dir = ang.ToRotationVector2();
                Vector2 a = pos + dir * (radius - 2f);
                sb.Draw(Pixel, a, PixelSrc, col, ang, new Vector2(0f, 0.5f),
                    new Vector2(4.5f + select * 1.5f, 1.1f), SpriteEffects.None, 0f);
            }
        }

        //====================== 鏨盘扇 ======================

        /// <summary>扇骨+菱纹章:骨自枢张出,章内阴刻字形,悬停点亮;isCurrent 顶角朱点</summary>
        public static void DrawFanRib(SpriteBatch sb, Vector2 pivot, Vector2 pos, string glyphKey, bool gold,
            bool isCurrent, float vis, float hover, float alpha, float time) {
            float ease = vis * (2f - vis);
            Vector2 drawPos = Vector2.Lerp(pivot, pos, ease);
            float a = alpha * vis;

            //骨
            OniBrush.DrawGradientLine(sb, pivot, drawPos, OnikiriUITheme.Dark * (a * 0.8f),
                OnikiriUITheme.Deep * (a * 0.9f), 2f);

            //菱章:影/缘/体
            float g = OnikiriUITheme.MeiFanGlyphSize;
            float lift = 1f + hover * 0.1f;
            Vector2 half = new(0.5f);
            Color rim = gold
                ? Color.Lerp(OnikiriUITheme.GoldDeep, OnikiriUITheme.GoldInlay, 0.4f + hover * 0.5f)
                : Color.Lerp(OnikiriUITheme.Deep, OnikiriUITheme.Bright, hover * 0.6f);
            sb.Draw(Pixel, drawPos + new Vector2(1.6f, 2.2f), PixelSrc, new Color(8, 2, 5) * (a * 0.55f),
                MathHelper.PiOver4, half, new Vector2(g * 1.06f * lift), SpriteEffects.None, 0f);
            sb.Draw(Pixel, drawPos, PixelSrc, rim * (a * 0.9f),
                MathHelper.PiOver4, half, new Vector2(g * 1.06f * lift), SpriteEffects.None, 0f);
            sb.Draw(Pixel, drawPos, PixelSrc, OnikiriUITheme.Ink * (a * 0.97f),
                MathHelper.PiOver4, half, new Vector2(g * 0.96f * lift), SpriteEffects.None, 0f);

            //章内字形:钢底一小片衬字
            sb.Draw(Pixel, drawPos, PixelSrc, OnikiriUITheme.Paper * (a * 0.14f),
                MathHelper.PiOver4, half, new Vector2(g * 0.82f * lift), SpriteEffects.None, 0f);
            OniMeiGlyphStyle style = OniMeiGlyphStyle.Engraved(a);
            style.Time = time;
            style.Inlay = gold ? 1f : 0f;
            style.Accent = gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright;
            style.Lit = hover * 0.7f;
            OniMeiGlyph.Draw(sb, glyphKey, drawPos, g * 0.72f * lift, style);

            //现铭标记:顶角一粒朱印软点
            if (isCurrent) {
                Vector2 mark = drawPos + new Vector2(0f, -g * 0.72f);
                OniBrush.DrawSoftDot(sb, mark, 3.6f, OnikiriUITheme.Seal, a * 0.95f);
            }
        }

        /// <summary>除铭骨:暗章锉叉,悬停转绯红</summary>
        public static void DrawFanRibErase(SpriteBatch sb, Vector2 pivot, Vector2 pos, float vis, float hover,
            float alpha, float time) {
            float ease = vis * (2f - vis);
            Vector2 drawPos = Vector2.Lerp(pivot, pos, ease);
            float a = alpha * vis;

            OniBrush.DrawGradientLine(sb, pivot, drawPos, OnikiriUITheme.Dark * (a * 0.7f),
                OnikiriUITheme.Dark * (a * 0.9f), 2f);

            float g = OnikiriUITheme.MeiFanGlyphSize;
            float lift = 1f + hover * 0.1f;
            Vector2 half = new(0.5f);
            Color rim = Color.Lerp(OnikiriUITheme.Disabled, OnikiriUITheme.Bright, hover) * (a * 0.85f);
            sb.Draw(Pixel, drawPos + new Vector2(1.6f, 2.2f), PixelSrc, new Color(8, 2, 5) * (a * 0.5f),
                MathHelper.PiOver4, half, new Vector2(g * 1.02f * lift), SpriteEffects.None, 0f);
            sb.Draw(Pixel, drawPos, PixelSrc, rim,
                MathHelper.PiOver4, half, new Vector2(g * 1.02f * lift), SpriteEffects.None, 0f);
            sb.Draw(Pixel, drawPos, PixelSrc, OnikiriUITheme.Ink * (a * 0.97f),
                MathHelper.PiOver4, half, new Vector2(g * 0.92f * lift), SpriteEffects.None, 0f);

            //锉叉:两笔交错刀痕
            float r = g * 0.3f * lift;
            Color cross = Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.Bright, hover) * a;
            DrawDash(sb, drawPos + new Vector2(-r, -r), drawPos + new Vector2(r, r), cross, 2f);
            DrawDash(sb, drawPos + new Vector2(r, -r), drawPos + new Vector2(-r, r), cross, 2f);
        }

        //====================== 烙印木牌 ======================

        /// <summary>
        /// 细节木牌:手裁板体(shader 木纹焦边优先)+系绳挂钉+烙印文字打字机,
        /// 金阶盖金签,除铭题绯红;它是挂在台边的一块荷札,不是浮空面板
        /// </summary>
        public static void DrawWoodTag(SpriteBatch sb, DynamicSpriteFont font, Rectangle rect,
            string title, string kindLabel, string origin, string power, string burden, bool gold, bool erase,
            int visibleChars, float burnFresh, float alpha, float time) {
            //板影:羽化落影
            OniBrush.DrawFeathered(sb, rect.Center.ToVector2() + new Vector2(5f, 7f), 0.008f,
                new Vector2(rect.Width, rect.Height), new Color(8, 2, 5), alpha * 0.72f);

            //系绳:从穿绳孔上挑到台缘一枚钉,让牌"挂"在世界里
            Vector2 hole = new(rect.X + 14f, rect.Y + 12f);
            Vector2 nail = hole + new Vector2(-22f, -40f);
            float sway = (float)Math.Sin(time * 1.1f) * 1.6f;
            Vector2 mid = (hole + nail) * 0.5f + new Vector2(5f + sway, 6f);
            OniBrush.DrawGradientLine(sb, nail, mid, OnikiriUITheme.Deep * (alpha * 0.85f),
                OnikiriUITheme.Deep * (alpha * 0.7f), 1.5f);
            OniBrush.DrawGradientLine(sb, mid, hole, OnikiriUITheme.Deep * (alpha * 0.7f),
                OnikiriUITheme.Dark * (alpha * 0.85f), 1.5f);
            sb.Draw(Pixel, nail, PixelSrc, OnikiriUITheme.GoldDeep * (alpha * 0.95f), MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(4.2f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, nail + new Vector2(-0.8f, -0.8f), PixelSrc, OnikiriUITheme.GoldInlay * (alpha * 0.6f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(1.8f), SpriteEffects.None, 0f);

            //板体:shader 手裁木板(木纹/焦边/缺角/绳孔),缺席退回简笔
            if (OniMeiStandDraw.Available) {
                Rectangle plank = rect;
                plank.Inflate(6, 6);
                OniMeiStandDraw.DrawWoodPlank(sb, plank, alpha, time);
            }
            else {
                //CPU 简笔:包边+板体+纵纹+绳孔
                sb.Draw(Pixel, new Rectangle(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6), PixelSrc,
                    OnikiriUITheme.Deep * (alpha * 0.5f));
                sb.Draw(Pixel, rect, PixelSrc, new Color(52, 18, 16) * (alpha * 0.97f));
                for (int i = 0; i < 5; i++) {
                    float u = 0.1f + Hash01(i * 61 + 3) * 0.8f;
                    sb.Draw(Pixel, new Vector2(rect.X + rect.Width * u, rect.Center.Y), PixelSrc,
                        OnikiriUITheme.Ink * (alpha * 0.28f), 0f, new Vector2(0.5f),
                        new Vector2(1f, rect.Height * 0.85f), SpriteEffects.None, 0f);
                }
                sb.Draw(Pixel, hole, PixelSrc, OnikiriUITheme.Ink * (alpha * 0.9f),
                    MathHelper.PiOver4, new Vector2(0.5f), new Vector2(4.4f), SpriteEffects.None, 0f);
            }

            float textLeft = rect.X + 28f;
            float headerRight = rect.Right - 16f;

            //题名(烙黑边白热字) + 类目签 + 金签
            Color titleCol = erase ? OnikiriUITheme.Bright : OnikiriUITheme.HotWhite;
            Utils.DrawBorderString(sb, title, new Vector2(textLeft, rect.Y + 9f), titleCol * alpha, 0.95f);
            Vector2 kSize = font.MeasureString(kindLabel) * 0.62f;
            Utils.DrawBorderString(sb, kindLabel, new Vector2(headerRight - kSize.X, rect.Y + 13f),
                OnikiriUITheme.TextDim * alpha, 0.62f);
            if (gold) {
                string goldMark = OniMeiUI.GoldMark.Value;
                Vector2 gSize = font.MeasureString(goldMark) * 0.6f;
                Utils.DrawBorderString(sb, goldMark, new Vector2(headerRight - gSize.X, rect.Y + 32f),
                    OnikiriUITheme.GoldInlay * (alpha * 0.95f), 0.6f);
            }
            //题下一笔烙痕
            OniBrush.DrawTaperedSlash(sb, new Vector2(rect.X + 12f, rect.Y + 38f),
                new Vector2(rect.Right - 12f, rect.Y + 36f), 1.8f, 1.2f, alpha * 0.75f);

            //出处 + 赋效 + 代价,烙印打字机(最新字覆灼橙);凿前必见真实数值
            float y = rect.Y + 48f;
            Utils.DrawBorderString(sb, OniMeiUI.OriginLabel.Value, new Vector2(textLeft, y),
                OnikiriUITheme.Deep * (alpha * 1.2f), 0.6f);
            y += 15f;
            y = OniRegisterRenderer.DrawTypedWrapped(sb, font, origin, new Vector2(textLeft, y),
                headerRight - textLeft, OnikiriUITheme.TextDim, 0.7f, alpha, visibleChars, burnFresh,
                OnikiriUITheme.BurnHot);
            if (power.Length > 0 && visibleChars > origin.Length) {
                y += 6f;
                Utils.DrawBorderString(sb, OniMeiUI.PowerLabel.Value, new Vector2(textLeft, y),
                    OnikiriUITheme.Deep * (alpha * 1.2f), 0.6f);
                y += 15f;
                Color powerCol = gold
                    ? Color.Lerp(OnikiriUITheme.Paper, OnikiriUITheme.GoldInlay, 0.4f)
                    : Color.Lerp(OnikiriUITheme.Paper, OnikiriUITheme.Bright, 0.28f);
                y = OniRegisterRenderer.DrawTypedWrapped(sb, font, power, new Vector2(textLeft, y),
                    headerRight - textLeft, powerCol, 0.7f, alpha, visibleChars - origin.Length, burnFresh,
                    OnikiriUITheme.BurnHot);
            }
            if (burden.Length > 0 && visibleChars > origin.Length + power.Length) {
                y += 6f;
                Utils.DrawBorderString(sb, OniMeiUI.BurdenLabel.Value, new Vector2(textLeft, y),
                    OnikiriUITheme.Seal * (alpha * 1.2f), 0.6f);
                y += 15f;
                //代价用压暗绯红,与赋效的亮色分列可辨
                Color burdenCol = Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.Bright, 0.45f);
                OniRegisterRenderer.DrawTypedWrapped(sb, font, burden, new Vector2(textLeft, y),
                    headerRight - textLeft, burdenCol, 0.7f, alpha,
                    visibleChars - origin.Length - power.Length, burnFresh, OnikiriUITheme.BurnHot);
            }
        }

        //====================== 右缘刀铭大字 ======================

        /// <summary>竖排大字刀铭:charVis 0~1 按笔顺写入,fresh 新字带灼热;字脚金压线,底一枚朱印</summary>
        public static void DrawNameColumn(SpriteBatch sb, DynamicSpriteFont font, string name, Vector2 top,
            float alpha, float charVis, bool fresh, float time) {
            if (string.IsNullOrEmpty(name) || alpha <= 0.01f) {
                return;
            }
            float scale = OnikiriUITheme.MeiNameScale;

            //背衬:一道极淡的纵向朱丝栏
            float colH = OnikiriUITheme.UIScreenH * 0.52f;
            sb.Draw(Pixel, top + new Vector2(0f, colH * 0.5f - 20f), PixelSrc, OnikiriUITheme.Deep * (alpha * 0.22f),
                0f, new Vector2(0.5f), new Vector2(1.2f, colH), SpriteEffects.None, 0f);

            if (!OniBrush.ContainsCJK(name)) {
                //拉丁名:整串旋 90°
                Vector2 size = font.MeasureString(name) * scale;
                int visChars = Math.Max(1, (int)Math.Ceiling(name.Length * MathHelper.Clamp(charVis, 0f, 1f)));
                string shown = name[..Math.Min(visChars, name.Length)];
                Vector2 pos = new(top.X + size.Y * 0.5f, top.Y);
                sb.DrawString(font, shown, pos + new Vector2(1.5f, 1.5f), OnikiriUITheme.Ink * (alpha * 0.85f),
                    MathHelper.PiOver2, Vector2.Zero, scale, SpriteEffects.None, 0f);
                sb.DrawString(font, shown, pos, OnikiriUITheme.Paper * alpha,
                    MathHelper.PiOver2, Vector2.Zero, scale, SpriteEffects.None, 0f);
                return;
            }

            float charH = font.MeasureString("字").Y * scale + 8f;
            int total = name.Length;
            float visF = MathHelper.Clamp(charVis, 0f, 1f) * total;
            float y = top.Y;
            for (int i = 0; i < total; i++) {
                float charA = MathHelper.Clamp(visF - i, 0f, 1f);
                if (charA <= 0.01f) {
                    break;
                }
                string s = name[i].ToString();
                Vector2 size = font.MeasureString(s) * scale;
                Vector2 pos = new(top.X - size.X * 0.5f, y);
                bool newest = fresh && visF - i < 1.6f;
                Color col = newest
                    ? Color.Lerp(OnikiriUITheme.BurnHot, OnikiriUITheme.HotWhite, MathHelper.Clamp(visF - i - 0.6f, 0f, 1f))
                    : OnikiriUITheme.Paper;
                Utils.DrawBorderString(sb, s, pos, col * (alpha * charA), scale);
                //字脚金压线
                sb.Draw(Pixel, new Vector2(top.X, y + size.Y - 2f), PixelSrc,
                    OnikiriUITheme.GoldDeep * (alpha * charA * 0.55f), 0f, new Vector2(0.5f),
                    new Vector2(size.X * 0.72f, 1.2f), SpriteEffects.None, 0f);
                y += charH;
            }
            //名讳底一枚小朱印
            if (visF >= total) {
                OniBrush.DrawSealGlyph(sb, new Vector2(top.X, y + 10f), 10f, alpha * 0.9f, 0.05f);
            }
        }

        //====================== 静物 / 题字 / 页签 ======================

        /// <summary>台上小静物:鏨、砥石、丁子油瓶,伏在布右下</summary>
        public static void DrawStillLife(SpriteBatch sb, Rectangle clothRect, float alpha, float time) {
            Vector2 baseP = new(clothRect.Right - 130f, clothRect.Bottom - 52f);

            //砥石:圆角矮块,上面浅色磨面
            sb.Draw(Pixel, baseP + new Vector2(2f, 3f), PixelSrc, new Color(8, 2, 5) * (alpha * 0.5f), 0.02f,
                new Vector2(0.5f), new Vector2(38f, 12f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, baseP, PixelSrc, new Color(64, 52, 44) * (alpha * 0.95f), 0.02f,
                new Vector2(0.5f), new Vector2(38f, 12f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, baseP - new Vector2(0f, 4f), PixelSrc, new Color(96, 82, 68) * (alpha * 0.8f), 0.02f,
                new Vector2(0.5f), new Vector2(36f, 3f), SpriteEffects.None, 0f);

            //鏨:斜倚在砥石旁,钢杆+暗柄+锋尖一点光
            Vector2 chiselC = baseP + new Vector2(44f, 1f);
            float cRot = -0.34f;
            sb.Draw(Pixel, chiselC, PixelSrc, OnikiriUITheme.TextDim * (alpha * 0.9f), cRot,
                new Vector2(0f, 0.5f), new Vector2(26f, 3f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, chiselC + cRot.ToRotationVector2() * 18f, PixelSrc, OnikiriUITheme.Dark * (alpha * 0.95f), cRot,
                new Vector2(0f, 0.5f), new Vector2(9f, 4.4f), SpriteEffects.None, 0f);
            OniBrush.DrawSoftDot(sb, chiselC, 2.2f, OnikiriUITheme.HotWhite, alpha * 0.5f);

            //丁子油瓶:深琉璃小瓶+木塞+烛光高光
            Vector2 bottleC = baseP + new Vector2(-42f, -6f);
            sb.Draw(Pixel, bottleC + new Vector2(1.5f, 2f), PixelSrc, new Color(8, 2, 5) * (alpha * 0.5f), 0f,
                new Vector2(0.5f), new Vector2(11f, 15f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, bottleC, PixelSrc, new Color(38, 14, 12) * (alpha * 0.96f), 0f,
                new Vector2(0.5f), new Vector2(11f, 15f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, bottleC - new Vector2(0f, 10f), PixelSrc, new Color(38, 14, 12) * (alpha * 0.96f), 0f,
                new Vector2(0.5f), new Vector2(4.6f, 6f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, bottleC - new Vector2(0f, 14f), PixelSrc, OnikiriUITheme.GoldDeep * (alpha * 0.9f), 0f,
                new Vector2(0.5f), new Vector2(4f, 3f), SpriteEffects.None, 0f);
            float glint = 0.5f + 0.3f * (float)Math.Sin(time * 1.3f);
            OniBrush.DrawSoftStreak(sb, bottleC + new Vector2(-2.6f, -3f), MathHelper.PiOver2, 7f, 1.4f,
                OnikiriUITheme.CandleWarm, alpha * 0.5f * glint, 0.6f);
        }

        /// <summary>台题:布上居中横书+朱印+短烙痕(左上让位给吊挂卷轴)</summary>
        public static void DrawTitle(SpriteBatch sb, DynamicSpriteFont font, Rectangle clothRect, string title, float alpha) {
            Vector2 tSize = font.MeasureString(title) * 1.02f;
            Vector2 tPos = new(clothRect.Center.X - tSize.X * 0.5f, clothRect.Y - 42f);
            OniBrush.DrawSealGlyph(sb, tPos + new Vector2(-24f, tSize.Y * 0.5f), 12f, alpha * 0.95f);
            Utils.DrawBorderString(sb, title, tPos, OnikiriUITheme.HotWhite * alpha, 1.02f);
            OniBrush.DrawTaperedSlash(sb, tPos + new Vector2(-4f, tSize.Y + 5f),
                tPos + new Vector2(tSize.X + 4f, tSize.Y + 3f), 2f, 1.4f, alpha * 0.85f);
        }

        //====================== 吊挂卷轴(回点鬼簿的门) ======================

        /// <summary>
        /// 悬挂的收卷点鬼簿微缩:对面屏(纸面)的器物本体挂在梁下作切换门。
        /// 纸垂随风;Echo 鬼火漏缝(本屏唯一许可的青——簿那头在闹);Ceremony 地杆弹开一截瞥见名录
        /// </summary>
        public static void DrawHangingScroll(SpriteBatch sb, OniHangingSwitch sw, float alpha, float time, bool danger) {
            if (alpha <= 0.01f) {
                return;
            }
            sw.DrawRope(sb, alpha);

            float s = OnikiriUITheme.HangSwitchScale;
            float rot = sw.Rot;
            Vector2 top = sw.End;
            Vector2 down = (MathHelper.PiOver2 + rot).ToRotationVector2();
            Vector2 side = rot.ToRotationVector2();
            float a = alpha * (0.92f + sw.HoverEase * 0.08f);
            float lift = 1f + sw.HoverEase * 0.08f;
            Vector2 half = new(0.5f);
            Vector2 P(float y, float x = 0f) => top + down * (y * s) + side * (x * s);
            Vector2 Sz(float w, float h) => new Vector2(w, h) * s;

            //挂绪结
            sb.Draw(Pixel, top, PixelSrc, OnikiriUITheme.Seal * a, MathHelper.PiOver4 + rot * 0.4f,
                half, Sz(4.2f, 4.2f), SpriteEffects.None, 0f);

            //整卷淡影
            sb.Draw(Pixel, P(40f) + new Vector2(1.5f, 2.2f) * s, PixelSrc, new Color(8, 2, 5) * (a * 0.45f),
                rot, half, Sz(16f, 72f), SpriteEffects.None, 0f);

            //====天杆+朱漆端帽====
            Vector2 rodTopC = P(7f);
            sb.Draw(Pixel, rodTopC, PixelSrc, OnikiriUITheme.Dark * (a * 0.96f), rot, half,
                Sz(30f, 3.6f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, rodTopC - down * (0.8f * s), PixelSrc, new Color(120, 52, 40) * (a * 0.6f), rot, half,
                Sz(28f, 1f), SpriteEffects.None, 0f);
            foreach (float x in new[] { -16f, 16f }) {
                sb.Draw(Pixel, P(7f, x), PixelSrc, OnikiriUITheme.Deep * (a * 0.95f), rot, half,
                    Sz(5f, 6f), SpriteEffects.None, 0f);
                sb.Draw(Pixel, P(5.8f, x), PixelSrc, OnikiriUITheme.Bright * (a * 0.5f), rot, half,
                    Sz(1.6f, 1.6f), SpriteEffects.None, 0f);
            }

            //====纸垂两条:挂在天杆上,簿上有鬼躁动时抖得更急====
            Rectangle shideRect = new((int)(rodTopC.X - 13f * s), (int)(rodTopC.Y + 1f * s), (int)(26f * s), (int)(6f * s));
            float shideTime = time * (danger ? 1.7f : 1f);
            OniBrush.DrawSingleShide(sb, shideRect, 0.10f, 12f * s, a * 0.95f, shideTime, 0.4f);
            OniBrush.DrawSingleShide(sb, shideRect, 0.90f, 13f * s, a * 0.9f, shideTime, 2.3f);

            //====卷体:纸筒三带卖圆,卷层暗线,束带一匝====
            float c = sw.Ceremony01;
            float cEase = c * (2f - c);
            Vector2 rollC = P(38f);
            sb.Draw(Pixel, rollC, PixelSrc, OnikiriUITheme.Paper * (a * 0.62f), rot, half,
                Sz(14f, 52f) * lift, SpriteEffects.None, 0f);
            sb.Draw(Pixel, rollC - side * (3.5f * s), PixelSrc, OnikiriUITheme.Paper * (a * 0.78f), rot, half,
                Sz(5.5f, 52f) * lift, SpriteEffects.None, 0f);
            sb.Draw(Pixel, rollC + side * (5f * s), PixelSrc, OnikiriUITheme.Paper * (a * 0.42f), rot, half,
                Sz(3.5f, 52f) * lift, SpriteEffects.None, 0f);
            foreach (float y in new[] { 22f, 36f, 50f }) {
                sb.Draw(Pixel, P(y), PixelSrc, OnikiriUITheme.TextDim * (a * 0.30f), rot, half,
                    Sz(13f, 1f), SpriteEffects.None, 0f);
            }
            sb.Draw(Pixel, P(38f), PixelSrc, OnikiriUITheme.Deep * (a * 0.9f), rot, half,
                Sz(15.5f, 2.6f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, P(38f, 8f), PixelSrc, OnikiriUITheme.Deep * (a * 0.85f), rot + MathHelper.PiOver4,
                half, Sz(3f, 3f), SpriteEffects.None, 0f);

            //====回声:鬼火自卷缝漏一丝(软焰,非硬条)====
            float echo = sw.Echo01;
            if (echo > 0.01f) {
                float pulse = MathF.Sin(echo * MathHelper.Pi);
                Vector2 seam = P(30f + echo * 14f, -6f);
                OniBrush.DrawSoftStreak(sb, seam - down * (2.5f * s * pulse), rot + MathHelper.PiOver2,
                    7f * s * pulse, 1.6f * s, OnikiriUITheme.GhostDim, a * 0.5f * pulse, 0.7f);
                OniBrush.DrawSoftDot(sb, seam, 3.2f * s * pulse, OnikiriUITheme.GhostFire, a * 0.7f * pulse);
            }

            //====地杆:预演时向下弹开,缝里瞥见名录====
            float dropY = 66f + cEase * 16f;
            if (cEase > 0.03f) {
                float gap = (dropY - 64f) * s;
                Vector2 gapC = P(64f + (dropY - 64f) * 0.5f);
                float flash = MathF.Sin(c * MathHelper.Pi);
                OniBrush.DrawBacklight(sb, gapC, 18f * s, OnikiriUITheme.GhostDim, a * 0.4f * flash);
                sb.Draw(Pixel, gapC, PixelSrc, OnikiriUITheme.Paper * (a * 0.85f), rot, half,
                    new Vector2(11f * s, gap), SpriteEffects.None, 0f);
                sb.Draw(Pixel, P(64f + (dropY - 64f) * 0.45f, -2.5f), PixelSrc, OnikiriUITheme.Ink * (a * 0.7f), rot, half,
                    new Vector2(1.2f * s, gap * 0.55f), SpriteEffects.None, 0f);
                sb.Draw(Pixel, P(64f + (dropY - 64f) * 0.55f, 2.5f), PixelSrc, OnikiriUITheme.Ink * (a * 0.6f), rot, half,
                    new Vector2(1.2f * s, gap * 0.4f), SpriteEffects.None, 0f);
            }
            Vector2 rodBotC = P(dropY);
            sb.Draw(Pixel, rodBotC, PixelSrc, OnikiriUITheme.Dark * (a * 0.96f), rot, half,
                Sz(26f, 3.2f), SpriteEffects.None, 0f);
            foreach (float x in new[] { -14f, 14f }) {
                sb.Draw(Pixel, P(dropY, x), PixelSrc, OnikiriUITheme.Deep * (a * 0.95f), rot, half,
                    Sz(4.4f, 5.4f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>切换门悬浮说明:小裱墨牌(跟随光标),题名+移步提示;两屏共用</summary>
        public static void DrawSwitchHoverTag(SpriteBatch sb, DynamicSpriteFont font, Vector2 mouse,
            string title, string hint, float alpha) {
            if (alpha <= 0.02f) {
                return;
            }
            float w = Math.Max(font.MeasureString(title).X * 0.78f, font.MeasureString(hint).X * 0.7f);
            Rectangle panel = new((int)mouse.X + 16, (int)mouse.Y - 6, (int)w + 20, 42);
            //不出屏
            if (panel.Right > OnikiriUITheme.UIScreenW - 8f) {
                panel.X = (int)(mouse.X - panel.Width - 12f);
            }
            sb.Draw(Pixel, new Rectangle(panel.X + 2, panel.Y + 3, panel.Width, panel.Height), PixelSrc,
                new Color(8, 2, 5) * (alpha * 0.5f));
            sb.Draw(Pixel, panel, PixelSrc, OnikiriUITheme.Ink * (alpha * 0.95f));
            OniBrush.DrawTaperedSlash(sb, new Vector2(panel.X + 4f, panel.Y + 20f),
                new Vector2(panel.Right - 4f, panel.Y + 19f), 1.3f, 0.7f, alpha * 0.7f);
            Utils.DrawBorderString(sb, title, new Vector2(panel.X + 9f, panel.Y + 3f),
                OnikiriUITheme.HotWhite * alpha, 0.78f);
            Utils.DrawBorderString(sb, hint, new Vector2(panel.X + 9f, panel.Y + 23f),
                OnikiriUITheme.TextDim * alpha, 0.7f);
        }

        //====================== 仪式工具 ======================

        /// <summary>鏨具:钢杆斜压在笔锋上,随击震颤;pose 0~1 入位</summary>
        public static void DrawChiselTool(SpriteBatch sb, Vector2 tip, float pose, Vector2 shake, float time) {
            float a = pose;
            //入位:自上方落到位
            Vector2 tipDraw = tip + new Vector2(0f, -26f * (1f - pose * (2f - pose)));
            float rot = -1.02f;
            Vector2 shaft = rot.ToRotationVector2();

            //杆影/杆体/杆脊光
            sb.Draw(Pixel, tipDraw + new Vector2(1.5f, 2f), PixelSrc, new Color(8, 2, 5) * (a * 0.5f), rot,
                new Vector2(0f, 0.5f), new Vector2(38f, 4.6f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, tipDraw, PixelSrc, OnikiriUITheme.TextDim * (a * 0.95f), rot,
                new Vector2(0f, 0.5f), new Vector2(38f, 4.2f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, tipDraw + new Vector2(-0.8f, -1.2f), PixelSrc, OnikiriUITheme.Paper * (a * 0.45f), rot,
                new Vector2(0f, 0.5f), new Vector2(34f, 1.2f), SpriteEffects.None, 0f);
            //杆尾铜箍(受锤的一端)
            sb.Draw(Pixel, tipDraw + shaft * 36f, PixelSrc, OnikiriUITheme.GoldDeep * (a * 0.95f), rot,
                new Vector2(0.5f), new Vector2(6f, 7f), SpriteEffects.None, 0f);
            //锋尖一点白(软辉)
            OniBrush.DrawSoftDot(sb, tipDraw, 2.4f + shake.Length() * 0.7f, OnikiriUITheme.HotWhite, a * 0.8f);
        }

        /// <summary>锉刀:横杆在字形上往复,t 0~1 锉程</summary>
        public static void DrawFileTool(SpriteBatch sb, Vector2 center, float size, float t, float alpha, float time) {
            float a = alpha * MathHelper.Clamp(t / 0.15f, 0f, 1f) * MathHelper.Clamp((1f - t) / 0.1f + 0.4f, 0f, 1f);
            float sweep = (float)Math.Sin(time * 13f) * size * 0.4f;
            Vector2 pos = center + new Vector2(sweep, -size * 0.18f);
            float rot = 0.05f;
            sb.Draw(Pixel, pos + new Vector2(1.5f, 2f), PixelSrc, new Color(8, 2, 5) * (a * 0.5f), rot,
                new Vector2(0.5f), new Vector2(size * 1.15f, 6f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, pos, PixelSrc, OnikiriUITheme.Dark * (a * 0.96f), rot,
                new Vector2(0.5f), new Vector2(size * 1.15f, 5.4f), SpriteEffects.None, 0f);
            OniBrush.DrawSoftStreak(sb, pos - new Vector2(0f, 2.4f), rot, size * 1.1f, 1.2f,
                OnikiriUITheme.TextDim, a * 0.55f, 0.25f);
            //柄头
            sb.Draw(Pixel, pos + rot.ToRotationVector2() * (size * 0.62f), PixelSrc, OnikiriUITheme.Deep * (a * 0.9f), rot,
                new Vector2(0.5f), new Vector2(9f, 6.5f), SpriteEffects.None, 0f);
        }
    }
}
