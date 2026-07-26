using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using OFR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs
{
    /// <summary>过刃线登记簿(纯客户端视觉)。每道直痕把"切出去比你看到的更远"的贯穿屏幕细线常驻在场，
    /// 深度视差 + 景深虚化 + 错帧闪现撑出切割线之间的纵深；死寂期全场随终斩同相呼吸（深处滞后半拍），
    /// 纳刀引爆一声令下全部兑现为真实裂缝并快速退场</summary>
    internal static class OniFinaleLattice
    {
        /// <summary>细线半长，覆盖最小缩放下的屏幕对角线、两端始终出屏</summary>
        private const float HairHalfX = 4600f;
        /// <summary>深度→视差系数，深处线随镜头移动更慢（贴向背景）</summary>
        private const float ParallaxPerDepth = 0.60f;
        /// <summary>无主兜底寿命（主控正常流程会在此之前 CashIn）</summary>
        private const int FailsafeAge = 200;
        private const int MaxLines = 48;

        private struct LatticeLine
        {
            public Vector2 WorldCenter;
            public float Angle;
            public float Depth;      //0=玩法平面 →1 深背景

            public float Flip;
            public float Seed;
            public float SizeMul;
            public int Delay;        //出生延迟（深度回声错帧、刀意穿进纵深的时差）

            public int Age;
        }

        private static readonly List<LatticeLine> lines = new(MaxLines);
        private static float breathAmp;
        private static int breathTimer;
        private static bool breathPushed;
        private static int cashAge = -1;
        private static uint lastUpdateCount;

        public static bool HasAny => lines.Count > 0;

        /// <summary>登记一条过刃线。depth=0 为玩法平面上的真切线，越大越沉向背景</summary>
        public static void AddLine(Vector2 worldCenter, float angle, float depth, float sizeMul, int delay = 0) {
            if (VaultUtils.isServer || lines.Count >= MaxLines) {
                return;
            }
            lines.Add(new LatticeLine {
                WorldCenter = worldCenter,
                Angle = MathHelper.WrapAngle(angle),
                Depth = MathHelper.Clamp(depth, 0f, 1f),
                Flip = Main.rand.NextBool() ? 1f : -1f,
                Seed = Main.rand.NextFloat(),
                SizeMul = sizeMul,
                Delay = delay,
                Age = -1,
            });
        }

        /// <summary>死寂期主控每帧推送呼吸、amp 随逼近纳刀升压，深处线用同一时钟相位滞后</summary>
        public static void PushBreath(int masterTimer, float amp) {
            breathTimer = masterTimer;
            breathAmp = MathHelper.Clamp(MathF.Max(breathAmp, amp), 0f, 1f);
            breathPushed = true;
        }

        /// <summary>纳刀引爆、全场细线兑现成真实裂缝——闪一下随即快速退场（深处稍慢，纵深最后熄灭）</summary>
        public static void CashIn() {
            if (cashAge < 0 && lines.Count > 0) {
                cashAge = 0;
            }
        }

        public static void Clear() {
            lines.Clear();
            breathAmp = 0f;
            breathPushed = false;
            cashAge = -1;
        }

        /// <summary>主控 AI 驱动，帧防重入（多场演出并存时也只推进一次）</summary>
        public static void Update() {
            if (lastUpdateCount == Main.GameUpdateCount) {
                return;
            }
            lastUpdateCount = Main.GameUpdateCount;

            if (!breathPushed) {
                breathAmp *= 0.86f;
                if (breathAmp < 0.012f) {
                    breathAmp = 0f;
                }
            }
            breathPushed = false;

            if (cashAge >= 0) {
                cashAge++;
            }

            for (int i = lines.Count - 1; i >= 0; i--) {
                LatticeLine line = lines[i];
                if (line.Delay > 0) {
                    line.Delay--;
                }
                else {
                    line.Age++;
                }
                lines[i] = line;

                if (line.Age > FailsafeAge + 30 || (cashAge >= 0 && ComposeFade(in line) <= 0f)) {
                    lines.RemoveAt(i);
                }
            }
            if (lines.Count == 0) {
                cashAge = -1;
            }
        }

        /// <summary>兑现/兜底淡出包络，深处线拖长一点点尾巴、纵深最后熄灭</summary>
        private static float ComposeFade(in LatticeLine line) {
            float fade = 1f;
            if (cashAge >= 0) {
                fade = 1f - cashAge / (7f + line.Depth * 12f);
            }
            if (line.Age > FailsafeAge) {
                fade = MathF.Min(fade, 1f - (line.Age - FailsafeAge) / 26f);
            }
            return fade;
        }

        /// <summary>深度视差、深处的线贴向镜头中心（背景化），镜头移动时滑得更慢</summary>
        private static Vector2 ParallaxCenter(in LatticeLine line) {
            Vector2 viewCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            return Vector2.Lerp(line.WorldCenter, viewCenter, line.Depth * ParallaxPerDepth);
        }

        /// <summary>细线主体，须在 OFR.BeginDraw 作用域内调用；远→近排序，纵深有绘制先后。
        /// 防重绘由主控侧"指定绘制者"判定承担</summary>
        public static void DrawLines(GraphicsDevice device, Effect fx) {
            if (lines.Count == 0) {
                return;
            }

            //far first：把索引按深度降序过一遍（数量小，插入排序开销可忽略）

            Span<int> order = stackalloc int[lines.Count];
            for (int i = 0; i < lines.Count; i++) {
                order[i] = i;
            }
            for (int i = 1; i < order.Length; i++) {
                int key = order[i];
                float d = lines[key].Depth;
                int j = i - 1;
                while (j >= 0 && lines[order[j]].Depth < d) {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = key;
            }

            foreach (int idx in order) {
                LatticeLine line = lines[idx];
                if (line.Age < 0) {
                    continue;
                }

                float fade = MathHelper.Clamp(ComposeFade(in line), 0f, 1f);
                if (fade <= 0.012f) {
                    continue;
                }

                float flash = line.Age <= 1 ? 0.9f : MathF.Pow(0.55f, line.Age - 1);
                float pop = cashAge is >= 0 and <= 3 ? 0.7f * MathF.Pow(0.6f, cashAge) : 0f;
                float hot = MathF.Max(flash, pop);

                //呼吸、深处滞后半拍——同一口气从近处传向纵深

                float breath = breathAmp * (0.5f + 0.5f * MathF.Sin(
                    (breathTimer - line.Depth * 8f) * 0.55f - MathHelper.PiOver2));

                float nearFactor = 1f - line.Depth * 0.62f;
                float opacity = 0.34f * nearFactor * (0.70f + 0.55f * breath)
                    + hot * 0.55f * nearFactor;
                opacity *= fade;
                if (opacity <= 0.012f) {
                    continue;
                }

                float settle = MathHelper.Clamp((line.Age - 3) / 9f, 0f, 1f);

                OFR.BladeDef def = new() {
                    SweepFrames = 2, Life = 600,
                    Mode = 1f, Rot = line.Angle, Span = 0f,
                    Thick = 0.34f,
                    HalfX = HairHalfX * line.SizeMul,
                    //景深虚化：越深的线越宽越淡（失焦），近处的线锋利如发丝

                    HalfY = (7.5f + 17f * line.Depth) * line.SizeMul,
                    Flip = line.Flip, Opacity = 1f,
                    FrontGlow = 0f, Seed = line.Seed,
                    Palette = OFR.BladePalette.Escalate(hot * 0.9f),
                };
                OFR.BladeState state = new() {
                    Sweep = OFR.EaseOutCubic((line.Age + 1) / 2f),
                    Flash = hot > 0.02f ? hot : 0f,
                    Opacity = opacity,
                    //深度雾化走既有亮→暗酒红压暗通道，闪现帧保持白热

                    ColorShift = MathHelper.Clamp((line.Depth * 0.5f + settle * 0.4f) * (1f - hot), 0f, 1f),
                    FrontGlow = line.Age <= 2 ? 1.6f : 0f,
                    FlowPhase = 0.45f * OFR.EaseOutCubic(line.Age / 16f),
                    ScaleMul = 1f,
                    ThickMul = (0.9f + 0.3f * breath) * (line.Age <= 1 ? 1.35f : 1f),
                    Erode = cashAge >= 0 ? (1f - fade) * 0.85f : 0f,
                };
                OFR.DrawBlade(device, fx, in def, in state, ParallaxCenter(in line), 0f
                    , opacityMul: 1f, thickMul: 1f, frontMul: 1f, forceHot: false);
            }
        }

        /// <summary>出生掠光、小辉点 1~2 帧内掠过全线，深处的更小更暗（纵深里传来的一闪）</summary>
        public static void DrawGlints(SpriteBatch sb) {
            if (lines.Count == 0 || CWRAsset.StarFlare02?.Value is not Texture2D flare) {
                return;
            }

            foreach (LatticeLine line in lines) {
                if (line.Age < 0 || line.Age > 3) {
                    continue;
                }
                float travel = MathHelper.Clamp((line.Age + 0.5f) / 2.2f, 0f, 1f);
                float exitFade = line.Age <= 2 ? 1f : 0.5f;
                float depthMul = 1f - line.Depth * 0.55f;
                Vector2 dir = line.Angle.ToRotationVector2();
                Vector2 center = ParallaxCenter(in line);
                float halfX = HairHalfX * line.SizeMul;

                for (int g = 0; g < 2; g++) {
                    float tg = travel - g * 0.10f;
                    if (tg <= 0f) {
                        continue;
                    }
                    Vector2 pos = center + dir * (tg * 2f - 1f) * halfX * 0.92f - Main.screenPosition;
                    float a = (1f - g * 0.4f) * exitFade * depthMul;
                    sb.Draw(flare, pos, null, new Color(255, 242, 226) * (0.8f * a)
                        , g * 1.9f + line.Seed * 6f, flare.Size() * 0.5f
                        , (0.30f - g * 0.08f) * depthMul * line.SizeMul, SpriteEffects.None, 0);
                }
            }
        }
    }
}
