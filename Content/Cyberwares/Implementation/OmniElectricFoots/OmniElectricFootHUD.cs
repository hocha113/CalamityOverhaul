using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.QuestLogs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.OmniElectricFoots
{
    /// <summary>
    /// 全向电动义足专属 HUD
    /// <br/>在玩家头顶绘制一段半弧形蓄力指示器，结合电流粒子+扫光高亮反馈蓄力强度
    /// <br/>显隐受 <see cref="OmniElectricFoot.GetEquipped"/> 与玩家自身存活状态共同约束，
    /// 全屏 UI 打开时主动隐藏避免遮挡
    /// </summary>
    internal class OmniElectricFootHUD : UIHandle
    {
        public static OmniElectricFootHUD Instance => UIHandleLoader.GetUIHandleOfType<OmniElectricFootHUD>();

        #region 显隐与配色

        public override bool Active {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead) {
                    return false;
                }
                if (OmniElectricFoot.GetEquipped(p) == null) {
                    return false;
                }
                if (QuestLog.Instance?.visible == true || QuestManagerUI.Instance?.IsOpen == true) {
                    return false;
                }
                //仅在蓄力进行中或者还有残余进度时显示，避免无功率时的视觉噪声
                OmniElectricFootPlayer fp = p.GetModPlayer<OmniElectricFootPlayer>();
                return fp.IsCharging || fp.ChargeRatio > 0.005f;
            }
        }

        //冷色调电流配色，与义足"高压电磁推进"的设定保持一致
        private static readonly Color BarColdLow = new(40, 80, 130);
        private static readonly Color BarColdHi = new(120, 220, 255);
        private static readonly Color BarHotHi = new(255, 230, 120);
        private static readonly Color BarFrame = new(8, 16, 24);

        #endregion

        #region 平滑/节奏状态

        //平滑跟随真实进度，避免数值抖动
        private float displayRatio;
        //蓄力时长的全局计时，用于扫光与电弧节奏，单位秒
        private float time;
        //蓄满时的脉冲强度
        private float fullPulse;

        #endregion

        public override void Update() {
            time += 1f / 60f;

            OmniElectricFootPlayer fp = Main.LocalPlayer.GetModPlayer<OmniElectricFootPlayer>();
            float target = MathHelper.Clamp(fp.ChargeRatio, 0f, 1f);

            displayRatio = MathHelper.Lerp(displayRatio, target, 0.25f);
            if (MathF.Abs(displayRatio - target) < 0.005f) {
                displayRatio = target;
            }

            //蓄满时的呼吸脉冲
            if (target >= 0.999f) {
                fullPulse = MathF.Min(1f, fullPulse + 0.08f);
            }
            else {
                fullPulse = MathF.Max(0f, fullPulse - 0.05f);
            }
        }

        public override void Draw(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            if (px == null) {
                return;
            }

            Player player = Main.LocalPlayer;
            //定位：从玩家头顶上方 70 像素处垂直向上展开弧条，便于玩家视线集中区
            Vector2 anchor = player.Top - Main.screenPosition + new Vector2(0f, -52f * player.gravDir);

            float ratio = MathHelper.Clamp(displayRatio, 0f, 1f);
            //配色与电压强度联动
            Color barCol = Color.Lerp(BarColdLow, BarColdHi, ratio);
            if (ratio > 0.85f) {
                float t = (ratio - 0.85f) / 0.15f;
                barCol = Color.Lerp(barCol, BarHotHi, t);
            }

            DrawArcBar(sb, px, anchor, ratio, barCol);
            DrawElectricBolts(sb, px, anchor, ratio, barCol);

            if (fullPulse > 0.02f) {
                DrawFullPulseRing(sb, px, anchor, fullPulse);
            }
        }

        /// <summary>
        /// 绘制弧形蓄力条本体，结构：底环（暗）+ 进度环（带浅色辉光）+ 顶端帽
        /// </summary>
        private void DrawArcBar(SpriteBatch sb, Texture2D px, Vector2 center, float ratio, Color barCol) {
            const float radius = 26f;
            const float thickness = 4.2f;
            //顶部 195° 的半弧（即 -7°/8 圆周左右的横向圆顶），开口朝下让玩家头顶不被遮挡
            const float arcStart = MathHelper.Pi + MathHelper.PiOver4 * 0.7f;
            const float arcEnd = MathHelper.TwoPi - MathHelper.PiOver4 * 0.7f;
            const int seg = 36;

            //底环（描边 + 半透明衬底）
            DrawArcStroke(sb, px, center, radius + thickness * 0.6f + 1f, arcStart, arcEnd, 1.4f, BarFrame * 0.85f, seg);
            DrawArcStroke(sb, px, center, radius - thickness * 0.6f - 1f, arcStart, arcEnd, 1.1f, BarFrame * 0.65f, seg);
            DrawArcSolid(sb, px, center, radius, arcStart, arcEnd, thickness, BarColdLow * 0.42f, seg);

            //进度环
            float fillEnd = MathHelper.Lerp(arcStart, arcEnd, ratio);
            if (fillEnd > arcStart + 0.001f) {
                int segFill = Math.Max(8, (int)(seg * ratio) + 4);
                //外层柔光
                DrawArcSolid(sb, px, center, radius, arcStart, fillEnd, thickness + 4f, barCol * 0.18f, segFill);
                //主体
                DrawArcSolid(sb, px, center, radius, arcStart, fillEnd, thickness, barCol * 0.95f, segFill);
                //内核高亮
                DrawArcSolid(sb, px, center, radius, arcStart, fillEnd, thickness * 0.45f, Color.White * (0.55f + ratio * 0.4f), segFill);

                //蓄满闪烁
                if (ratio > 0.985f) {
                    float flash = (MathF.Sin(time * 22f) + 1f) * 0.5f;
                    DrawArcSolid(sb, px, center, radius, arcStart, fillEnd, thickness + 1.5f,
                        BarHotHi * (0.45f + 0.4f * flash), segFill);
                }

                //顶端帽：在填充末端绘制小亮点表达"能量正在汇聚"
                Vector2 capDir = new(MathF.Cos(fillEnd), MathF.Sin(fillEnd));
                Vector2 capPos = center + capDir * radius;
                DrawDot(sb, px, capPos, 4f, Color.White * (0.85f + 0.15f * MathF.Sin(time * 12f)));
                DrawDot(sb, px, capPos, 7f, barCol * 0.4f);
            }

            //中央电池图标：简化为竖向小矩形 + 顶部端子，强化"电能"语义
            DrawBatteryGlyph(sb, px, center, ratio, barCol);
        }

        /// <summary>
        /// 蓄力期间从弧条向中心闪过的细小电弧，强度与蓄力进度同步
        /// </summary>
        private void DrawElectricBolts(SpriteBatch sb, Texture2D px, Vector2 center, float ratio, Color barCol) {
            if (ratio < 0.05f) {
                return;
            }
            //电弧数量与亮度按蓄力进度提升
            int boltCount = 1 + (int)MathF.Floor(ratio * 4f);
            for (int i = 0; i < boltCount; i++) {
                //每帧选取一段弧形位置上的随机点作为电弧起点
                float angle = MathHelper.Pi + MathHelper.PiOver4 * 0.7f
                    + (MathHelper.TwoPi - MathHelper.PiOver2 * 1.4f) * Main.rand.NextFloat();
                Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle));
                Vector2 from = center + dir * 26f;
                //中心稍偏移，避免所有电弧汇聚到同一点造成结块视觉
                Vector2 to = center + new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));

                Color boltCol = Color.Lerp(barCol, Color.White, 0.45f) * (0.55f + ratio * 0.4f);
                DrawJaggedLine(sb, px, from, to, 2, boltCol);
            }
        }

        /// <summary>
        /// 蓄满时从中心向外扩散的脉冲环，提供强烈的"已就绪"反馈
        /// </summary>
        private void DrawFullPulseRing(SpriteBatch sb, Texture2D px, Vector2 center, float pulse) {
            //循环展开：每 0.8 秒一次
            float phase = (time * 1.25f) % 1f;
            float r = MathHelper.Lerp(20f, 46f, phase);
            float alpha = (1f - phase) * pulse * 0.7f;
            DrawArcStroke(sb, px, center, r, 0f, MathHelper.TwoPi, 2f, BarHotHi * alpha, 36);
            //核心光斑
            DrawDot(sb, px, center, 9f, BarHotHi * (pulse * 0.3f));
        }

        /// <summary>
        /// 中心电池图标：底框 + 端子 + 内部填充随蓄力升降
        /// </summary>
        private static void DrawBatteryGlyph(SpriteBatch sb, Texture2D px, Vector2 center, float ratio, Color barCol) {
            const float bodyW = 9f;
            const float bodyH = 14f;
            //外框
            DrawRect(sb, px, center - new Vector2(bodyW * 0.5f, bodyH * 0.5f), bodyW, bodyH, BarFrame * 0.95f);
            //内填充：从底部向上随 ratio 增长
            float fillH = (bodyH - 2f) * ratio;
            if (fillH > 0.5f) {
                Vector2 fillTL = center + new Vector2(-bodyW * 0.5f + 1f, bodyH * 0.5f - 1f - fillH);
                DrawRect(sb, px, fillTL, bodyW - 2f, fillH, barCol);
            }
            //顶部端子
            DrawRect(sb, px, center - new Vector2(2f, bodyH * 0.5f + 2f), 4f, 2f, BarFrame * 0.95f);
            //蓄满时端子亮起
            if (ratio > 0.95f) {
                DrawDot(sb, px, center - new Vector2(0f, bodyH * 0.5f + 1f), 3f, BarHotHi * 0.85f);
            }
        }

        #region 几何工具：纯像素绘制，无外部贴图依赖

        private static void DrawArcSolid(SpriteBatch sb, Texture2D px, Vector2 center,
            float radius, float startAngle, float endAngle, float thickness, Color color, int segments) {
            if (endAngle <= startAngle || segments <= 0) {
                return;
            }
            float step = (endAngle - startAngle) / segments;
            Vector2 prev = center + new Vector2(MathF.Cos(startAngle), MathF.Sin(startAngle)) * radius;
            for (int i = 1; i <= segments; i++) {
                float a = startAngle + step * i;
                Vector2 cur = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
                DrawLine(sb, px, prev, cur, thickness, color);
                prev = cur;
            }
        }

        private static void DrawArcStroke(SpriteBatch sb, Texture2D px, Vector2 center,
            float radius, float startAngle, float endAngle, float thickness, Color color, int segments) {
            DrawArcSolid(sb, px, center, radius, startAngle, endAngle, thickness, color, segments);
        }

        private static void DrawJaggedLine(SpriteBatch sb, Texture2D px, Vector2 from, Vector2 to, int kinks, Color color) {
            //生成一段折线模拟雷电感，每段中点法向偏移随机量
            Vector2 prev = from;
            int total = kinks + 1;
            Vector2 dir = to - from;
            float len = dir.Length();
            if (len < 1f) {
                return;
            }
            Vector2 normal = new Vector2(-dir.Y, dir.X) / len;
            for (int i = 1; i <= total; i++) {
                float t = (float)i / total;
                Vector2 basePoint = Vector2.Lerp(from, to, t);
                float jitter = (i == total) ? 0f : Main.rand.NextFloat(-3f, 3f) * (1f - MathF.Abs(t - 0.5f) * 1.4f);
                Vector2 next = basePoint + normal * jitter;
                DrawLine(sb, px, prev, next, 1.4f, color);
                prev = next;
            }
        }

        private static void DrawLine(SpriteBatch sb, Texture2D px, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 0.5f) {
                return;
            }
            sb.Draw(px, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        private static void DrawDot(SpriteBatch sb, Texture2D px, Vector2 pos, float size, Color color) {
            //用 1 像素纹理拉伸出近似圆点：以正方形近似，足够小尺寸下肉眼无差别
            int sz = Math.Max(1, (int)MathF.Round(size));
            Rectangle dst = new((int)(pos.X - sz * 0.5f), (int)(pos.Y - sz * 0.5f), sz, sz);
            sb.Draw(px, dst, color);
        }

        private static void DrawRect(SpriteBatch sb, Texture2D px, Vector2 topLeft, float w, float h, Color color) {
            Rectangle dst = new((int)topLeft.X, (int)topLeft.Y, Math.Max(1, (int)MathF.Round(w)), Math.Max(1, (int)MathF.Round(h)));
            sb.Draw(px, dst, color);
        }

        #endregion
    }
}
