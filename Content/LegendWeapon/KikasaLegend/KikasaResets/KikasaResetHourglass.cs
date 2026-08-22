using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets
{
    /// <summary>
    /// 大范围重启的背景沙漏演出：冲刷段散乱雨丝弧线汇聚、勾出一座古老沙漏，
    /// 倒带段沙（雨珠）自下腔逆流升入上腔，流转比例即倒带进度、
    /// 细流粗细吃回卷脉冲；落定白闪下溃散归雨。
    /// 纯本机表现零网络包；着色器画本体、CPU 粒子画汇聚雨丝与升沙。
    /// 画布坐标：y 向下为正、颈口为原点，与 KikasaHourglass.fx 的常量同源。
    /// </summary>
    internal static class KikasaResetHourglass
    {
        private enum DropKind : byte
        {
            /// <summary>散乱雨丝沿贝塞尔弧被收向轮廓</summary>
            Converge,
            /// <summary>到位后沿玻璃剖面滑动的轮廓水线</summary>
            GlassFlow,
            /// <summary>挂在框架上的静滴</summary>
            Cling,
            /// <summary>倒带期自下堆剥离、升入上腔的沙珠</summary>
            Grain,
            /// <summary>落定/中断后化回落雨淡出</summary>
            FallOut,
        }

        private sealed class Drop
        {
            public DropKind Kind;
            public Vector2 Pos;
            public Vector2 Vel;
            public Vector2 Start;
            public Vector2 Ctrl;
            public Vector2 Target;
            /// <summary>汇聚曲线进度 0~1</summary>
            public float T;
            public float TStep;
            /// <summary>轮廓侧 ±1</summary>
            public float Side;
            /// <summary>轮廓流粒子在剖面上的 y</summary>
            public float YOn;
            public float BaseAlpha;
            public float Scale;
            public float Seed;
            public int Fade;
            /// <summary>目标在玻璃剖面上（否则挂框）</summary>
            public bool Glass;
            /// <summary>亮色沙珠（湿反光色）</summary>
            public bool Bright;
        }

        private static readonly List<Drop> drops = [];

        //==================== 画布几何（与 KikasaHourglass.fx 常量同源） ====================

        private const float CanvasAspect = 0.80f;
        private const float GlassH = 0.36f;
        private const float NeckW = 0.022f;
        private const float BulbW = 0.205f;
        private const float ConeH = 0.09f;
        private const float Crater = 0.08f;

        /// <summary>汇聚雨丝上限</summary>
        private const int ConvergeCap = 190;
        /// <summary>升沙珠上限（叠加在汇聚粒之上）</summary>
        private const int GrainCap = 130;
        /// <summary>沙珠倒放坠落的重力（画布单位/帧²）：升得快、到顶慢，正是倒放的自由落体</summary>
        private const float GrainGravity = 0.00073f;

        private static int lastResetId;
        private static float flowPhase;
        private static float grainCarry;
        private static Vector2 anchorWorld;

        //==================== 每帧推进（由 KikasaResetSystem 客户端驱动） ====================

        internal static void Update() {
            KikasaReset.ResetShow show = KikasaReset.Active;
            if (show == null) {
                //中断或落定后：残余粒子全部化回落雨淡出
                if (drops.Count > 0) {
                    ConvertAllToFallOut();
                    StepParticles(false, 0f);
                }
                return;
            }
            if (!KikasaReset.LocallyViewed) {
                //旁观距离外不演；钉住本场 id 防中途走近半场重建
                lastResetId = show.ResetId;
                drops.Clear();
                return;
            }
            if (show.ResetId != lastResetId) {
                lastResetId = show.ResetId;
                drops.Clear();
                flowPhase = 0f;
                grainCarry = 0f;
                Player owner = Main.player[show.OwnerWho];
                anchorWorld = owner?.active == true
                    ? owner.Center
                    : Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            }

            int timer = show.Timer;
            bool rewind = KikasaReset.RainRewindActive;
            float pulse = KikasaReset.RewindPulseRate;

            //玻璃雨丝的流动相位：成形段顺淌而下，倒带段随脉冲向上抽回
            flowPhase += rewind ? 0.012f + 0.030f * pulse : -0.007f;

            if (timer >= KikasaReset.SnapshotEnd && timer <= KikasaReset.WashEnd - 6
                && drops.Count < ConvergeCap) {
                SpawnConverge();
            }
            if (rewind) {
                SpawnGrains(pulse, FillProgress(timer));
            }
            if (timer >= KikasaReset.RewindEnd) {
                ConvertAllToFallOut();
            }
            StepParticles(rewind, pulse);
        }

        internal static void Clear() {
            drops.Clear();
            grainCarry = 0f;
            flowPhase = 0f;
            lastResetId = 0;
        }

        /// <summary>沙移入上腔的比例=倒带进度，三段脉冲的顿挫由 AgeAt 自带</summary>
        private static float FillProgress(int timer)
            => MathHelper.Clamp(
                KikasaReset.AgeAt(timer) / (float)KikasaReset.RewindWindowFrames, 0f, 1f);

        //==================== 生成 ====================

        /// <summary>散乱雨丝：散点出生（部分自上缘带雨相），弧线被收向沙漏轮廓</summary>
        private static void SpawnConverge() {
            int budget = Math.Min(5, ConvergeCap - drops.Count);
            for (int i = 0; i < budget; i++) {
                SampleOutline(out Vector2 target, out float side, out bool onGlass);
                Vector2 start = new(
                    Main.rand.NextFloat(-0.85f, 0.85f),
                    Main.rand.NextBool(3)
                        ? Main.rand.NextFloat(-0.85f, -0.55f)
                        : Main.rand.NextFloat(-0.62f, 0.62f));
                //控制点：中点顺雨向下坠 + 随机侧偏，弧线读作被无形的线收回
                Vector2 mid = (start + target) * 0.5f;
                Vector2 perp = (target - start).SafeNormalize(Vector2.UnitY)
                    .RotatedBy(MathHelper.PiOver2);
                Vector2 ctrl = mid + new Vector2(0f, Main.rand.NextFloat(0.05f, 0.18f))
                    + perp * Main.rand.NextFloat(-0.16f, 0.16f);
                drops.Add(new Drop {
                    Kind = DropKind.Converge,
                    Pos = start,
                    Start = start,
                    Ctrl = ctrl,
                    Target = target,
                    TStep = 1f / Main.rand.Next(26, 42),
                    Side = side,
                    YOn = target.Y,
                    BaseAlpha = Main.rand.NextFloat(0.42f, 0.62f),
                    Scale = Main.rand.NextFloat(0.45f, 0.8f),
                    Seed = Main.rand.NextFloat(10f),
                    Glass = onGlass,
                });
            }
        }

        /// <summary>轮廓取点：玻璃剖面为主，上下座与侧柱作配重</summary>
        private static void SampleOutline(out Vector2 target, out float side, out bool onGlass) {
            float roll = Main.rand.NextFloat();
            side = Main.rand.NextBool() ? 1f : -1f;
            if (roll < 0.62f) {
                float y = Main.rand.NextFloat(-GlassH, GlassH);
                target = new Vector2(side * WOf(y), y);
                onGlass = true;
                return;
            }
            onGlass = false;
            if (roll < 0.82f) {
                float ySign = Main.rand.NextBool() ? 1f : -1f;
                target = new Vector2(Main.rand.NextFloat(-0.26f, 0.26f),
                    ySign * (0.395f + Main.rand.NextFloat(-0.028f, 0.028f)));
                return;
            }
            target = new Vector2(side * 0.250f + Main.rand.NextFloat(-0.012f, 0.012f),
                Main.rand.NextFloat(-0.40f, 0.40f));
        }

        /// <summary>沙珠：自下堆锥顶剥离，倒放自由落体升入上腔坑心；量随回卷脉冲</summary>
        private static void SpawnGrains(float pulse, float fill) {
            if (fill >= 0.97f) {
                return;
            }
            grainCarry += 0.6f + 5.5f * pulse;
            int count = Math.Min((int)grainCarry, 6);
            grainCarry -= count;
            grainCarry = MathF.Min(grainCarry, 8f);
            for (int i = 0; i < count; i++) {
                if (drops.Count >= ConvergeCap + GrainCap) {
                    return;
                }
                float yBEdge = MathHelper.Lerp(0.115f, GlassH + ConeH + 0.03f, fill);
                float startY = MathHelper.Clamp(
                    yBEdge - ConeH + Main.rand.NextFloat(0f, 0.03f), 0.02f, GlassH - 0.01f);
                //目标=上腔漏斗坑心，随填充逐帧抬高
                float targetY = MathF.Min(-fill * 0.27f + Crater - 0.012f, -0.015f);
                float rise = MathF.Max(startY - targetY, 0.05f);
                //倒放的自由落体：出发最快、到顶恰好减速归零
                float v0 = -MathF.Sqrt(2f * GrainGravity * rise);
                drops.Add(new Drop {
                    Kind = DropKind.Grain,
                    Pos = new Vector2(Main.rand.NextFloat(-0.022f, 0.022f), startY),
                    Vel = new Vector2(Main.rand.NextFloat(-0.0012f, 0.0012f), v0),
                    Target = new Vector2(0f, targetY),
                    BaseAlpha = Main.rand.NextFloat(0.55f, 0.8f),
                    Scale = Main.rand.NextFloat(0.30f, 0.5f),
                    Seed = Main.rand.NextFloat(10f),
                    Bright = true,
                });
            }
        }

        //==================== 推进 ====================

        private static void StepParticles(bool rewind, float pulse) {
            for (int i = drops.Count - 1; i >= 0; i--) {
                Drop d = drops[i];
                switch (d.Kind) {
                    case DropKind.Converge: {
                        d.T += d.TStep;
                        float e = Smooth01(d.T);
                        Vector2 next = Bezier(d.Start, d.Ctrl, d.Target, e);
                        d.Vel = next - d.Pos;
                        d.Pos = next;
                        if (d.T >= 1f) {
                            d.Kind = d.Glass ? DropKind.GlassFlow : DropKind.Cling;
                            d.Pos = d.Target;
                        }
                        break;
                    }
                    case DropKind.GlassFlow: {
                        float speed = rewind ? -(0.006f + 0.014f * pulse) : 0.0045f;
                        float prevY = d.YOn;
                        d.YOn += speed;
                        //端点回绕藏在上下座后面，跳变不可见
                        if (d.YOn > GlassH) {
                            d.YOn -= GlassH * 2f;
                        }
                        else if (d.YOn < -GlassH) {
                            d.YOn += GlassH * 2f;
                        }
                        Vector2 next = new(
                            d.Side * WOf(d.YOn)
                                + MathF.Sin(d.Seed * 9f + Main.GlobalTimeWrappedHourly * 3f) * 0.003f,
                            d.YOn);
                        bool wrapped = Math.Abs(d.YOn - prevY) > 0.1f;
                        d.Vel = wrapped ? new Vector2(0f, speed) : next - d.Pos;
                        d.Pos = next;
                        break;
                    }
                    case DropKind.Cling: {
                        d.Vel *= 0.8f;
                        break;
                    }
                    case DropKind.Grain: {
                        d.Vel.Y += GrainGravity;
                        //颈口区向中线收束，穿颈后随坑面散开
                        float pullX = Math.Abs(d.Pos.Y) < 0.09f ? 0.22f : 0.05f;
                        d.Vel.X = MathHelper.Lerp(d.Vel.X, (d.Target.X - d.Pos.X) * 0.02f, pullX);
                        d.Pos += d.Vel;
                        //夹在玻璃腔内：穿颈自然被挤成一线
                        float w = WOf(d.Pos.Y) - 0.006f;
                        d.Pos.X = MathHelper.Clamp(d.Pos.X, -w, w);
                        //升到坑面或上冲耗尽：并入上堆
                        if (d.Pos.Y <= d.Target.Y || d.Vel.Y >= 0f) {
                            drops.RemoveAt(i);
                            continue;
                        }
                        break;
                    }
                    case DropKind.FallOut: {
                        d.Vel.X *= 0.985f;
                        d.Vel.Y = MathF.Min(d.Vel.Y + 0.0011f, 0.028f);
                        d.Pos += d.Vel;
                        if (--d.Fade <= 0) {
                            drops.RemoveAt(i);
                            continue;
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>落定/中断：全体化回落雨，保留当前动量、重力接管</summary>
        private static void ConvertAllToFallOut() {
            foreach (Drop d in drops) {
                if (d.Kind == DropKind.FallOut) {
                    continue;
                }
                d.Kind = DropKind.FallOut;
                d.Fade = Main.rand.Next(12, 18);
                d.Vel += new Vector2(Main.rand.NextFloat(-0.002f, 0.002f),
                    Main.rand.NextFloat(0f, 0.004f));
            }
        }

        //==================== 绘制（由 KikasaResetHourglassRender 在 NPC 层之下调用） ====================

        internal static void Draw(SpriteBatch spriteBatch) {
            KikasaReset.ResetShow show = KikasaReset.Active;
            if (Main.gameMenu || (show == null && drops.Count == 0)) {
                return;
            }
            if (show != null && !KikasaReset.LocallyViewed) {
                return;
            }

            float quadH = Main.screenHeight * 0.62f;
            Vector2 center = CanvasCenter();

            if (show != null && show.Timer > KikasaReset.SnapshotEnd) {
                DrawShaderBody(spriteBatch, show, center, quadH);
            }
            DrawDrops(spriteBatch, center, quadH);
        }

        /// <summary>屏幕锚定 + 触发点微视差：倒带时相机大幅回移，世界锚定会把沙漏甩出屏幕</summary>
        private static Vector2 CanvasCenter() {
            Vector2 camCenter = Main.screenPosition
                + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            Vector2 off = (anchorWorld - camCenter) * 0.05f;
            off.X = MathHelper.Clamp(off.X, -40f, 40f);
            off.Y = MathHelper.Clamp(off.Y, -40f, 40f);
            return new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.44f) + off;
        }

        private static void DrawShaderBody(SpriteBatch spriteBatch,
            KikasaReset.ResetShow show, Vector2 center, float quadH) {
            Effect fx = EffectLoader.KikasaHourglass?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || canvas == null || noise == null) {
                //无着色器只留 CPU 粒子：轮廓与升沙仍可读，不落黑块
                return;
            }

            int timer = show.Timer;
            float form = Smooth01((timer - KikasaReset.SnapshotEnd)
                / (float)(KikasaReset.WashEnd - KikasaReset.SnapshotEnd));
            float disperse = timer > KikasaReset.RewindEnd
                ? MathHelper.Clamp((timer - KikasaReset.RewindEnd)
                    / (float)(KikasaReset.TotalFrames - KikasaReset.RewindEnd), 0f, 1f)
                : 0f;
            float alphaIn = MathHelper.Clamp((timer - KikasaReset.SnapshotEnd) / 10f, 0f, 1f);

            GraphicsDevice gd = Main.instance.GraphicsDevice;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            //共享 uniform 是设备全局状态：每次调用全参数重设，漏一个就串残值
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(show.Seed);
            fx.Parameters["uForm"]?.SetValue(form);
            fx.Parameters["uFill"]?.SetValue(FillProgress(timer));
            fx.Parameters["uPulse"]?.SetValue(KikasaReset.RewindPulseRate);
            fx.Parameters["uDisperse"]?.SetValue(disperse);
            fx.Parameters["uAlpha"]?.SetValue(0.92f * alphaIn);
            fx.Parameters["uFlow"]?.SetValue(flowPhase);
            fx.Parameters["uAspect"]?.SetValue(CanvasAspect);
            fx.Parameters["uColBody"]?.SetValue(KikasaInk.InkBody.ToVector3());
            fx.Parameters["uColDeep"]?.SetValue(KikasaInk.InkDeep.ToVector3());
            fx.Parameters["uColCore"]?.SetValue(KikasaInk.BloodCore.ToVector3());
            fx.Parameters["uColSheen"]?.SetValue(KikasaInk.WetSheen.ToVector3());
            fx.CurrentTechnique.Passes[0].Apply();

            float quadW = quadH * CanvasAspect;
            Rectangle dest = new((int)(center.X - quadW * 0.5f),
                (int)(center.Y - quadH * 0.5f), (int)quadW, (int)quadH);
            spriteBatch.Draw(canvas, dest, Color.White);
            spriteBatch.End();
        }

        /// <summary>雨丝/沙珠：Extra_98 真 alpha 双层速度拉伸，复刻 PRT_GhostRainDrop 的成熟画法</summary>
        private static void DrawDrops(SpriteBatch spriteBatch, Vector2 center, float quadH) {
            if (drops.Count == 0) {
                return;
            }
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }

            Color pale = KikasaDomain.CoolTint(new(214, 118, 106), new(170, 185, 190));
            Color sheen = KikasaDomain.CoolTint(new(238, 122, 106), new(178, 202, 208));

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            Vector2 origin = tex.Size() * 0.5f;
            foreach (Drop d in drops) {
                Vector2 screen = center + d.Pos * quadH;
                Vector2 velPx = d.Vel * quadH;
                float speed = velPx.Length();
                float rot = speed > 0.35f ? velPx.ToRotation() + MathHelper.PiOver2 : 0f;
                float stretch = MathHelper.Clamp(speed * 0.055f, 0f, 1f);
                Vector2 body = new Vector2(0.13f * (1f - stretch * 0.35f),
                    0.42f * (1f + stretch * 2.4f)) * d.Scale;
                float alpha = d.BaseAlpha;
                if (d.Kind == DropKind.FallOut) {
                    alpha *= MathHelper.Clamp(d.Fade / 16f, 0f, 1f);
                }
                Color c = (d.Bright ? sheen : pale) * alpha;
                spriteBatch.Draw(tex, screen, null, c, rot, origin, body,
                    SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, screen, null, c * 0.6f, rot, origin,
                    body * new Vector2(0.45f, 1.06f), SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }

        //==================== 小工具 ====================

        /// <summary>玻璃剖面半宽：颈口窄、腔肩宽、近座微收（与 shader 同式）</summary>
        private static float WOf(float y) {
            float q = MathHelper.Clamp(Math.Abs(y) / GlassH, 0f, 1f);
            float shoulder = SmoothStep(0f, 0.60f, q) * (1f - 0.25f * SmoothStep(0.60f, 1f, q));
            return MathHelper.Lerp(NeckW, BulbW, shoulder);
        }

        private static float Smooth01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private static float SmoothStep(float e0, float e1, float x)
            => Smooth01((x - e0) / (e1 - e0));

        private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t) {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }
    }

    /// <summary>
    /// 沙漏的绘制层：物块之后、NPC/玩家之前，被倒放的演员从沙漏前掠过，
    /// 读作背景结构；Weight 压在血湖领域调色(1.24)之前，
    /// 领域调色与湖面镜面会把它一并接管（沙漏在血湖里有倒影）
    /// </summary>
    internal sealed class KikasaResetHourglassRender : RenderHandle
    {
        public override float Weight => 1.23f;

        public override void UpdateBySystem(int index) {
            //主菜单兜底清场（PostUpdateEverything 不再运行）
            if (Main.gameMenu) {
                KikasaResetHourglass.Clear();
            }
        }

        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }
            KikasaResetHourglass.Draw(spriteBatch);
        }
    }
}
