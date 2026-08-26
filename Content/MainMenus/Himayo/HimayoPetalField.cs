using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.MainMenus.Himayo
{
    /// <summary>樱瓣场：花瓣存在于相机周围的世界空间，经 <see cref="HimayoMenuCamera.Project"/>
    /// 针孔透视上屏（与全景背景同几何，转头时流速严格一致）；深度连续驱动透视尺寸、
    /// 对焦近中景的景深柔边与远景紫雾；三成花瓣走贴近摄像机的近景大瓣带。
    /// 形体走 OniDomainDeco 的 TechMenuPetal；近距瓣可被光标扰动与接住</summary>
    internal static class HimayoPetalField
    {
        private class Petal
        {
            //相机原点世界坐标（y 向上，无量纲深度单位）；Prev 供部分帧插值
            public Vector3 Pos, PrevPos;
            //光标冲量（世界空间），指数衰减
            public Vector3 Imp;
            public float Rot, PrevRot, RotSpeed;
            public float SwayPhase;
            //世界空间基准尺寸；下落速度与横摆振幅逐瓣随机，纷飞不整齐
            public float Size;
            public float FallSpeed;
            public float SwayAmp;
            //水平距离（spawn 参数，供回收阈值自适应）
            public float Rho;
            public Color Tint;
            public bool Caught;
            public int CatchSlot = -1;
        }

        private const int PetalCount = 300;
        //世界分布：方位角 ±100°（可视 ±82° 加余量）；主场与近景带两段式
        private const float SpawnYawRange = 1.745f;
        private const float MainRhoMin = 0.9f, MainRhoMax = 7f;
        //近景大瓣带：贴近摄像机纷飞掠过，约占三成
        private const float NearBandChance = 0.30f;
        private const float NearRhoMin = 0.55f, NearRhoMax = 1.5f;
        private const float TopY = 1.9f, BottomY = -1.7f;
        //景深对焦近中景（近景带基本锐利，远景糊+雾）；前后景深度阈值：近段盖 UI
        private const float FocusDepth = 1.15f;
        private const float SplitDepth = 1.35f;
        //交互只作用近距瓣；判定在投影后的屏幕空间
        private const float InteractDepth = 1.75f;
        private const float SweepRadius = 92f;
        private const float CatchRadius = 50f;
        //接住的花瓣锁在光标射线上的固定视轴深度
        private const float CatchDepth = 0.95f;
        //可见锥半角近似（tan 垂直半 FOV + pitch 满偏），供按深度自适应的重生/回收高度
        private const float ViewConeSlope = 0.905f;

        private static readonly List<Petal> petals = new(PetalCount);
        //掌心槽位：按住左键最多托起 4 片；偏移在屏幕空间给出，反投影到掌心深度
        private static readonly Petal[] catchSlots = new Petal[4];
        private static readonly Vector2[] SlotOffsets = [new(-15f, 12f), new(11f, 16f), new(-2f, 24f), new(20f, 6f)];

        private static bool prewarmed;
        private static float windTime;

        //绘制工作表：逐帧复用，避免分配
        private struct DrawEntry
        {
            public Petal P;
            public Vector2 Screen;
            public float Depth;
        }
        private static readonly List<DrawEntry> drawList = new(PetalCount);
        private static readonly Comparison<DrawEntry> FarToNear = (a, b) => b.Depth.CompareTo(a.Depth);

        public static void Reset() {
            petals.Clear();
            Array.Clear(catchSlots);
            prewarmed = false;
            windTime = 0f;
        }

        /// <summary>释放全部被托花瓣，可继承释放瞬间的光标速度（UI 像素/tick，换算为世界冲量）</summary>
        public static void ReleaseCaught(Vector2 inheritVel = default) {
            for (int i = 0; i < catchSlots.Length; i++) {
                Petal p = catchSlots[i];
                if (p == null) {
                    continue;
                }
                p.Caught = false;
                p.CatchSlot = -1;
                float rho = new Vector2(p.Pos.X, p.Pos.Z).Length();
                p.Imp = ScreenVelToWorld(inheritVel, MathF.Max(rho, 0.3f)) * 0.55f;
                catchSlots[i] = null;
            }
        }

        //垂直焦距（像素）：屏幕尺寸 = 世界尺寸 / 视轴深度 * 焦距
        private static float FocalV => Main.screenHeight / (2f * HimayoMenuCamera.TanHalfFov);

        //屏幕速度（px/tick）→ 指定深度处的世界速度；方向落在当前视平面
        private static Vector3 ScreenVelToWorld(Vector2 screenVel, float depth) {
            HimayoMenuCamera.GetBasis(1f, out _, out Vector3 right, out Vector3 up);
            return (right * screenVel.X - up * screenVel.Y) * (depth / FocalV);
        }

        /// <summary>固定 60tick 推进；interactive=近距瓣允许交互，uiMouse/mouseVel 为 UI 空间，catching=左键按住托瓣</summary>
        public static void Tick(bool interactive, Vector2 uiMouse, Vector2 mouseVel, bool catching) {
            if (Main.screenWidth <= 0 || Main.screenHeight <= 0) {
                return;
            }
            windTime += 1f / 60f;
            if (!prewarmed) {
                Prewarm();
            }
            if (!catching) {
                ReleaseCaught(mouseVel);
            }

            //双频阵风（世界 +x）：慢波打底 + 中频阵涌，一阵一阵的纷飞感；量级为深度1处像素/tick
            float wind = (0.30f
                + MathF.Sin(windTime * 0.35f) * 0.26f
                + MathF.Sin(windTime * 1.13f + 2.1f) * 0.14f) / 864f;

            for (int i = 0; i < petals.Count; i++) {
                Petal p = petals[i];
                p.PrevPos = p.Pos;
                p.PrevRot = p.Rot;

                if (p.Caught) {
                    //弹簧吸附掌心：光标射线上的固定深度点 + 槽位屏幕偏移
                    Vector3 palm = HimayoMenuCamera.Unproject(uiMouse + SlotOffsets[p.CatchSlot], CatchDepth, 1f);
                    p.Pos = Vector3.Lerp(p.Pos, palm, 0.30f);
                    p.Rot = p.Rot.AngleLerp(MathF.Sin(windTime * 2.1f + p.SwayPhase) * 0.25f, 0.18f);
                    continue;
                }

                p.SwayPhase += 0.026f + p.Size * 0.35f;
                float sway = MathF.Sin(p.SwayPhase);
                //世界运动学：恒定下落 + 阵风 + 逐瓣振幅的水平摆（含纵深摆，瓣会缓慢穿越景深）
                Vector3 vel = new(
                    sway * p.SwayAmp + wind,
                    -p.FallSpeed,
                    MathF.Sin(p.SwayPhase * 0.63f + 1.7f) * 0.00042f);

                if (interactive
                    && HimayoMenuCamera.Project(p.Pos, 1f, out Vector2 screen, out float depth)
                    && depth < InteractDepth) {
                    //交互按玩家所见判定：投影后的屏幕距离；只有近距瓣响应
                    float dist = Vector2.Distance(screen, uiMouse);
                    if (dist < SweepRadius) {
                        float falloff = 1f - dist / SweepRadius;
                        //划过：按光标速度施加世界冲量，先在屏幕空间限幅防甩飞
                        Vector2 impulse = mouseVel * (0.16f * falloff);
                        if (impulse.Length() > 6f) {
                            impulse = impulse.SafeNormalize(Vector2.Zero) * 6f;
                        }
                        p.Imp += ScreenVelToWorld(impulse, depth);
                        p.RotSpeed += (mouseVel.X >= 0f ? 1f : -1f) * 0.004f * falloff;

                        if (catching && dist < CatchRadius) {
                            int slot = FindFreeSlot();
                            if (slot >= 0) {
                                p.Caught = true;
                                p.CatchSlot = slot;
                                catchSlots[slot] = p;
                                p.Imp = Vector3.Zero;
                                continue;
                            }
                        }
                    }
                }

                p.Imp *= 0.90f;
                p.Pos += vel + p.Imp;
                p.RotSpeed = MathHelper.Clamp(p.RotSpeed, -0.075f, 0.075f);
                p.Rot += p.RotSpeed + sway * 0.014f;
                p.RotSpeed *= 0.995f;

                //回收：落出自身深度的可见锥下界（近瓣快速循环）或横漂出方位余量，则回顶部重生
                float recycleY = MathF.Max(BottomY, -(p.Rho * ViewConeSlope + 0.25f));
                float yawAng = MathF.Atan2(p.Pos.X, p.Pos.Z);
                if (p.Pos.Y < recycleY || MathF.Abs(yawAng) > SpawnYawRange * 1.12f) {
                    Respawn(p, topOnly: true);
                }
            }
        }

        private static int FindFreeSlot() {
            for (int i = 0; i < catchSlots.Length; i++) {
                if (catchSlots[i] == null) {
                    return i;
                }
            }
            return -1;
        }

        //首帧铺满整个分布空间，避免开场空窗
        private static void Prewarm() {
            prewarmed = true;
            petals.Clear();
            for (int i = 0; i < PetalCount; i++) {
                Petal p = new();
                Respawn(p, topOnly: false);
                petals.Add(p);
            }
        }

        private static void Respawn(Petal p, bool topOnly) {
            float yaw = Main.rand.NextFloat(-SpawnYawRange, SpawnYawRange);
            //两段式分布：三成走近景大瓣带（贴脸纷飞），其余铺满主场
            bool nearBand = Main.rand.NextFloat() < NearBandChance;
            float rho = nearBand
                ? MathHelper.Lerp(NearRhoMin, NearRhoMax, Main.rand.NextFloat())
                : MathHelper.Lerp(MainRhoMin, MainRhoMax, Main.rand.NextFloat());
            //重生高度贴自身深度的可见锥上界：近瓣不必从 1.9 落半天才入画
            float spawnTop = MathF.Min(TopY, rho * ViewConeSlope + 0.2f);
            float y = topOnly
                ? spawnTop + Main.rand.NextFloat(0.30f)
                : Main.rand.NextFloat(MathF.Max(BottomY, -(rho * ViewConeSlope + 0.2f)), spawnTop);
            p.Pos = new Vector3(MathF.Sin(yaw) * rho, y, MathF.Cos(yaw) * rho);
            p.PrevPos = p.Pos;
            p.Rho = rho;
            p.Rot = Main.rand.NextFloat(MathHelper.TwoPi);
            p.PrevRot = p.Rot;
            p.RotSpeed = Main.rand.NextFloat(-0.045f, 0.045f);
            p.SwayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            p.Size = nearBand
                ? Main.rand.NextFloat(0.040f, 0.062f)
                : Main.rand.NextFloat(0.030f, 0.056f);
            p.FallSpeed = Main.rand.NextFloat(0.0008f, 0.0019f);
            p.SwayAmp = Main.rand.NextFloat(0.0007f, 0.0014f);
            p.Imp = Vector3.Zero;
            p.Caught = false;
            p.CatchSlot = -1;

            Color baseTint = Color.Lerp(HimayoMenuTheme.PetalPink, HimayoMenuTheme.PetalPinkDeep, Main.rand.NextFloat());
            //近距瓣偶发暗红，呼应背景灯笼
            if (rho < 1.8f && Main.rand.NextBool(14)) {
                baseTint = HimayoMenuTheme.PetalCrimson;
            }
            p.Tint = baseTint;
        }

        //景深柔边：对焦近中景，近景带基本锐利（还原贴近摄像机的清晰大瓣），远侧糊得缓且更糊
        private static float SoftnessAt(float depth) {
            float d = depth - FocusDepth;
            float soft = d >= 0f ? 0.055f + d * 0.042f : 0.055f - d * 0.060f;
            return MathHelper.Clamp(soft, 0.05f, 0.28f);
        }

        //远景紫雾混入与透明度衰减
        private static float HazeAt(float depth) => MathHelper.SmoothStep(0f, 0.52f,
            MathHelper.Clamp((depth - 1.8f) / 4.7f, 0f, 1f));

        private static float AlphaAt(float depth) => MathHelper.Lerp(1f, 0.52f,
            MathHelper.Clamp((depth - 1.2f) / 5.3f, 0f, 1f));

        /// <summary>远段（深度≥阈值），画在标题与按钮之下</summary>
        public static void DrawBack(SpriteBatch spriteBatch, float alpha, float fade) =>
            DrawSegment(spriteBatch, alpha, fade, near: false);

        /// <summary>近段盖顶，画在一切菜单 UI 之上</summary>
        public static void DrawFront(SpriteBatch spriteBatch, float alpha, float fade) =>
            DrawSegment(spriteBatch, alpha, fade, near: true);

        private static void DrawSegment(SpriteBatch spriteBatch, float alpha, float fade, bool near) {
            Effect deco = EffectLoader.OniDomainDeco?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            //花瓣是纯着色器形体，着色器缺席时整层跳过（背景与按钮不受影响）
            if (deco == null || white == null || petals.Count == 0) {
                return;
            }

            //世界坐标插值后投影（tick 间位移小，直接线性插值再投影足够准）；层归属按视轴深度
            drawList.Clear();
            foreach (Petal p in petals) {
                Vector3 pos = Vector3.Lerp(p.PrevPos, p.Pos, alpha);
                if (!HimayoMenuCamera.Project(pos, alpha, out Vector2 screen, out float depth)) {
                    continue;
                }
                if (depth < SplitDepth != near) {
                    continue;
                }
                drawList.Add(new DrawEntry { P = p, Screen = screen, Depth = depth });
            }
            if (drawList.Count == 0) {
                return;
            }
            //远→近绘制保证近瓣盖远瓣
            drawList.Sort(FarToNear);

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.UIScaleMatrix);

            Vector2 origin = white.Size() * 0.5f;
            deco.CurrentTechnique = deco.Techniques["TechMenuPetal"];
            float focal = FocalV;
            float appliedSoftness = -1f;
            foreach (DrawEntry e in drawList) {
                //柔边=景深，量化成档减少 Apply 次数（排序后深度单调，档位切换有限）
                float softness = MathF.Round(SoftnessAt(e.Depth) / 0.03f) * 0.03f;
                if (softness != appliedSoftness) {
                    appliedSoftness = softness;
                    deco.Parameters["uPetalSoftness"]?.SetValue(softness);
                    deco.CurrentTechnique.Passes[0].Apply();
                }
                float rot = MathHelper.Lerp(e.P.PrevRot, e.P.Rot, alpha);
                Color c = Color.Lerp(e.P.Tint, HimayoMenuTheme.HazePurple, HazeAt(e.Depth))
                    * (AlphaAt(e.Depth) * fade);
                //透视缩放：世界尺寸 / 视轴深度 * 焦距
                float px = e.P.Size / e.Depth * focal;
                Vector2 scale = new(px / white.Width, px * 1.15f / white.Height);
                spriteBatch.Draw(white, e.Screen, null, c, rot, origin, scale, SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }
    }
}
