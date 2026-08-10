using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.QuestLogs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.OmniElectricFoots
{
    /// <summary>
    /// 全向电动义足 HUD，头顶半弧蓄力条
    /// <br/>蓄力中或残余进度时显示，全屏 UI 打开隐藏
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
                //蓄力中、残余进度或断电红闪
                OmniElectricFootPlayer fp = p.GetModPlayer<OmniElectricFootPlayer>();
                return fp.IsCharging || fp.ChargeRatio > 0.005f || fp.BrokenFlash > 0;
            }
        }

        //冷色电流
        private static readonly Color BarColdLow = new(40, 80, 130);
        private static readonly Color BarColdHi = new(120, 220, 255);
        private static readonly Color BarHotHi = new(255, 230, 120);
        private static readonly Color BarFrame = new(8, 16, 24);
        //断电
        private static readonly Color BarFault = new(255, 90, 70);
        //头顶限高
        private static readonly Color BarCeiling = new(255, 176, 64);

        #endregion

        #region 平滑/节奏状态

        //平滑进度
        private float displayRatio;
        //扫光/电弧节奏，秒
        private float time;
        //蓄满脉冲
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

            //蓄满呼吸
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
            //头顶上方
            Vector2 anchor = player.Top - Main.screenPosition + new Vector2(0f, -52f * player.gravDir);

            float ratio = MathHelper.Clamp(displayRatio, 0f, 1f);
            Color barCol = Color.Lerp(BarColdLow, BarColdHi, ratio);
            if (ratio > 0.85f) {
                float t = (ratio - 0.85f) / 0.15f;
                barCol = Color.Lerp(barCol, BarHotHi, t);
            }

            OmniElectricFootPlayer fp = player.GetModPlayer<OmniElectricFootPlayer>();
            DrawArcBar(sb, px, anchor, ratio, barCol);
            //头顶不够高时，标出这次蹬升实际用得上的上限
            if (fp.IsCharging && fp.MaxUsableRatio < 0.98f) {
                DrawCeilingLimit(sb, px, anchor, fp.MaxUsableRatio, ratio);
            }
            DrawElectricBolts(sb, px, anchor, ratio, barCol);

            if (fullPulse > 0.02f) {
                DrawFullPulseRing(sb, px, anchor, fullPulse);
            }

            //断电：整环红闪一下，明确告诉玩家这次蓄力废了
            float broken = fp.BrokenFlash / 20f;
            if (broken > 0f) {
                DrawFaultArc(sb, px, anchor, broken);
            }
        }

        /// <summary>
        /// 限高刻度：净空吃不下的那段染琥珀，刻度线画在可用上限处
        /// <br/>超过刻度还在蓄就等于白蓄，撞顶不罚冷却但也不会更高
        /// </summary>
        private void DrawCeilingLimit(SpriteBatch sb, Texture2D px, Vector2 center
            , float limit, float ratio) {
            const float radius = 26f;
            const float arcStart = MathHelper.Pi + MathHelper.PiOver4 * 0.7f;
            const float arcEnd = MathHelper.TwoPi - MathHelper.PiOver4 * 0.7f;

            float limitAngle = MathHelper.Lerp(arcStart, arcEnd, MathHelper.Clamp(limit, 0f, 1f));
            //刻度之后的整段底环染琥珀，一眼看出"再蓄没用"
            DrawArcSolid(sb, px, center, radius, limitAngle, arcEnd, 5f
                , BarCeiling * 0.22f, 20);
            //已经蓄过头的部分加重
            if (ratio > limit) {
                float overAngle = MathHelper.Lerp(arcStart, arcEnd, MathHelper.Clamp(ratio, 0f, 1f));
                float pulse = 0.5f + 0.5f * MathF.Sin(time * 16f);
                DrawArcSolid(sb, px, center, radius, limitAngle, overAngle, 4.4f
                    , BarCeiling * (0.35f + 0.35f * pulse), 20);
            }
            //径向刻度线
            Vector2 dir = new(MathF.Cos(limitAngle), MathF.Sin(limitAngle));
            DrawLine(sb, px, center + dir * (radius - 7f), center + dir * (radius + 7f)
                , 1.8f, BarCeiling * 0.9f);
        }

        /// <summary>断电红弧，随剩余帧衰减并高频闪</summary>
        private void DrawFaultArc(SpriteBatch sb, Texture2D px, Vector2 center, float strength) {
            const float arcStart = MathHelper.Pi + MathHelper.PiOver4 * 0.7f;
            const float arcEnd = MathHelper.TwoPi - MathHelper.PiOver4 * 0.7f;
            float flash = 0.55f + 0.45f * MathF.Sin(time * 40f);
            Color col = BarFault * (strength * flash);
            DrawArcSolid(sb, px, center, 26f, arcStart, arcEnd, 5.6f, col * 0.35f, 36);
            DrawArcSolid(sb, px, center, 26f, arcStart, arcEnd, 2.6f, col, 36);
            DrawDot(sb, px, center, 8f, col * 0.5f);
        }

        /// <summary>弧形蓄力条，底环+进度环+顶端帽</summary>
        private void DrawArcBar(SpriteBatch sb, Texture2D px, Vector2 center, float ratio, Color barCol) {
            const float radius = 26f;
            const float thickness = 4.2f;
            //顶部 195° 半弧，开口朝下
            const float arcStart = MathHelper.Pi + MathHelper.PiOver4 * 0.7f;
            const float arcEnd = MathHelper.TwoPi - MathHelper.PiOver4 * 0.7f;
            const int seg = 36;

            //底环
            DrawArcStroke(sb, px, center, radius + thickness * 0.6f + 1f, arcStart, arcEnd, 1.4f, BarFrame * 0.85f, seg);
            DrawArcStroke(sb, px, center, radius - thickness * 0.6f - 1f, arcStart, arcEnd, 1.1f, BarFrame * 0.65f, seg);
            DrawArcSolid(sb, px, center, radius, arcStart, arcEnd, thickness, BarColdLow * 0.42f, seg);

            //进度环
            float fillEnd = MathHelper.Lerp(arcStart, arcEnd, ratio);
            if (fillEnd > arcStart + 0.001f) {
                int segFill = Math.Max(8, (int)(seg * ratio) + 4);
                DrawArcSolid(sb, px, center, radius, arcStart, fillEnd, thickness + 4f, barCol * 0.18f, segFill);
                DrawArcSolid(sb, px, center, radius, arcStart, fillEnd, thickness, barCol * 0.95f, segFill);
                DrawArcSolid(sb, px, center, radius, arcStart, fillEnd, thickness * 0.45f, Color.White * (0.55f + ratio * 0.4f), segFill);

                //蓄满闪烁
                if (ratio > 0.985f) {
                    float flash = (MathF.Sin(time * 22f) + 1f) * 0.5f;
                    DrawArcSolid(sb, px, center, radius, arcStart, fillEnd, thickness + 1.5f,
                        BarHotHi * (0.45f + 0.4f * flash), segFill);
                }

                //顶端帽
                Vector2 capDir = new(MathF.Cos(fillEnd), MathF.Sin(fillEnd));
                Vector2 capPos = center + capDir * radius;
                DrawDot(sb, px, capPos, 4f, Color.White * (0.85f + 0.15f * MathF.Sin(time * 12f)));
                DrawDot(sb, px, capPos, 7f, barCol * 0.4f);
            }

            DrawBatteryGlyph(sb, px, center, ratio, barCol);
        }

        /// <summary>蓄力电弧，强度随 ratio</summary>
        private void DrawElectricBolts(SpriteBatch sb, Texture2D px, Vector2 center, float ratio, Color barCol) {
            if (ratio < 0.05f) {
                return;
            }
            int boltCount = 1 + (int)MathF.Floor(ratio * 4f);
            for (int i = 0; i < boltCount; i++) {
                float angle = MathHelper.Pi + MathHelper.PiOver4 * 0.7f
                    + (MathHelper.TwoPi - MathHelper.PiOver2 * 1.4f) * Main.rand.NextFloat();
                Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle));
                Vector2 from = center + dir * 26f;
                //中心微偏，防结块
                Vector2 to = center + new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));

                Color boltCol = Color.Lerp(barCol, Color.White, 0.45f) * (0.55f + ratio * 0.4f);
                DrawJaggedLine(sb, px, from, to, 2, boltCol);
            }
        }

        /// <summary>蓄满脉冲环，约 0.8 秒一轮</summary>
        private void DrawFullPulseRing(SpriteBatch sb, Texture2D px, Vector2 center, float pulse) {
            //0.8 秒一轮
            float phase = (time * 1.25f) % 1f;
            float r = MathHelper.Lerp(20f, 46f, phase);
            float alpha = (1f - phase) * pulse * 0.7f;
            DrawArcStroke(sb, px, center, r, 0f, MathHelper.TwoPi, 2f, BarHotHi * alpha, 36);
            DrawDot(sb, px, center, 9f, BarHotHi * (pulse * 0.3f));
        }

        /// <summary>中心电池图标，填充随 ratio</summary>
        private static void DrawBatteryGlyph(SpriteBatch sb, Texture2D px, Vector2 center, float ratio, Color barCol) {
            const float bodyW = 9f;
            const float bodyH = 14f;
            DrawRect(sb, px, center - new Vector2(bodyW * 0.5f, bodyH * 0.5f), bodyW, bodyH, BarFrame * 0.95f);
            //内填充自下而上
            float fillH = (bodyH - 2f) * ratio;
            if (fillH > 0.5f) {
                Vector2 fillTL = center + new Vector2(-bodyW * 0.5f + 1f, bodyH * 0.5f - 1f - fillH);
                DrawRect(sb, px, fillTL, bodyW - 2f, fillH, barCol);
            }
            DrawRect(sb, px, center - new Vector2(2f, bodyH * 0.5f + 2f), 4f, 2f, BarFrame * 0.95f);
            if (ratio > 0.95f) {
                DrawDot(sb, px, center - new Vector2(0f, bodyH * 0.5f + 1f), 3f, BarHotHi * 0.85f);
            }
        }

        #region 几何工具

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
            //折线雷电，法向抖动
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
            //1px 纹理当圆点
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
