using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.MainMenus.Himayo
{
    /// <summary>三景深樱瓣场：形体走 OniDomainDeco 的 TechMenuPetal（柔边=景深），运动学全在此处；近层可被光标扰动与接住</summary>
    internal static class HimayoPetalField
    {
        private class Petal
        {
            public Vector2 Pos, PrevPos;
            //光标冲量，指数衰减
            public Vector2 Imp;
            public float Rot, PrevRot, RotSpeed;
            public float SwayPhase;
            //像素尺寸
            public float Scale;
            public Color Tint;
            public bool Caught;
            public int CatchSlot = -1;
        }

        private const int LayerCount = 3;
        //层参数：0远 1中 2近
        private static readonly int[] Caps = [36, 24, 12];
        private static readonly float[] SizeMin = [8f, 14f, 26f];
        private static readonly float[] SizeMax = [14f, 22f, 40f];
        private static readonly float[] Softness = [0.24f, 0.12f, 0.03f];
        private static readonly float[] LayerAlpha = [0.55f, 0.80f, 1f];
        private static readonly float[] Parallax = [0.22f, 0.50f, 1f];
        private static readonly float[] SpeedMul = [0.45f, 0.75f, 1.15f];
        private static readonly float[] HazeMix = [0.45f, 0.20f, 0f];

        //近层交互半径
        private const float SweepRadius = 92f;
        private const float CatchRadius = 50f;

        private static readonly List<Petal>[] layers = [new(), new(), new()];
        //掌心槽位：按住左键最多托起 4 片
        private static readonly Petal[] catchSlots = new Petal[4];
        private static readonly Vector2[] SlotOffsets = [new(-15f, 12f), new(11f, 16f), new(-2f, 24f), new(20f, 6f)];

        private static bool prewarmed;
        private static float windTime;

        public static void Reset() {
            foreach (List<Petal> list in layers) {
                list.Clear();
            }
            Array.Clear(catchSlots);
            prewarmed = false;
            windTime = 0f;
        }

        /// <summary>释放全部被托花瓣，可继承释放瞬间的光标速度</summary>
        public static void ReleaseCaught(Vector2 inheritVel = default) {
            for (int i = 0; i < catchSlots.Length; i++) {
                Petal p = catchSlots[i];
                if (p == null) {
                    continue;
                }
                p.Caught = false;
                p.CatchSlot = -1;
                p.Imp = inheritVel * 0.55f;
                catchSlots[i] = null;
            }
        }

        /// <summary>固定 60tick 推进；interactive=近层允许交互，uiMouse/mouseVel 为 UI 空间，catching=左键按住托瓣</summary>
        public static void Tick(bool interactive, Vector2 uiMouse, Vector2 mouseVel, bool catching) {
            float w = Main.screenWidth, h = Main.screenHeight;
            if (w <= 0 || h <= 0) {
                return;
            }
            windTime += 1f / 60f;
            if (!prewarmed) {
                Prewarm(w, h);
            }
            if (!catching) {
                ReleaseCaught(mouseVel);
            }

            //视差在绘制侧叠加，交互按玩家所见位置判定：把光标换算进近层空间
            Vector2 mouseInNear = uiMouse - HimayoMenuCamera.ParallaxOffset(1f, Parallax[2]);

            for (int l = 0; l < LayerCount; l++) {
                List<Petal> list = layers[l];
                if (list.Count < Caps[l] && Main.rand.NextBool(3)) {
                    list.Add(SpawnPetal(l, w, h, topOnly: true));
                }
                for (int i = list.Count - 1; i >= 0; i--) {
                    Petal p = list[i];
                    p.PrevPos = p.Pos;
                    p.PrevRot = p.Rot;

                    if (p.Caught) {
                        //弹簧吸附掌心，姿态缓摆若托于掌上
                        Vector2 palm = mouseInNear + SlotOffsets[p.CatchSlot];
                        p.Pos = Vector2.Lerp(p.Pos, palm, 0.30f);
                        p.Rot = p.Rot.AngleLerp(MathF.Sin(windTime * 2.1f + p.SwayPhase) * 0.25f, 0.18f);
                        continue;
                    }

                    p.SwayPhase += 0.026f + p.Scale * 0.0006f;
                    float sway = MathF.Sin(p.SwayPhase);
                    //全局缓变横风 + 层内正弦摆，远层整体更慢
                    float wind = 0.22f + MathF.Sin(windTime * 0.35f + l * 1.7f) * 0.18f;
                    Vector2 vel = new(
                        sway * (0.55f + 0.30f * l) + wind,
                        0.66f + p.Scale * 0.016f);
                    vel *= SpeedMul[l];

                    if (l == 2 && interactive) {
                        float dist = Vector2.Distance(p.Pos, mouseInNear);
                        if (dist < SweepRadius) {
                            float falloff = 1f - dist / SweepRadius;
                            //划过：按光标速度施加冲量，限幅防甩飞
                            Vector2 impulse = mouseVel * (0.16f * falloff);
                            if (impulse.Length() > 6f) {
                                impulse = impulse.SafeNormalize(Vector2.Zero) * 6f;
                            }
                            p.Imp += impulse;
                            p.RotSpeed += (mouseVel.X >= 0f ? 1f : -1f) * 0.004f * falloff;

                            if (catching && dist < CatchRadius) {
                                int slot = FindFreeSlot();
                                if (slot >= 0) {
                                    p.Caught = true;
                                    p.CatchSlot = slot;
                                    catchSlots[slot] = p;
                                    p.Imp = Vector2.Zero;
                                    continue;
                                }
                            }
                        }
                    }

                    p.Imp *= 0.90f;
                    p.Pos += vel + p.Imp;
                    p.RotSpeed = MathHelper.Clamp(p.RotSpeed, -0.06f, 0.06f);
                    p.Rot += p.RotSpeed + sway * 0.010f;
                    p.RotSpeed *= 0.995f;

                    //出界回收：底部或两侧过远则回顶部重生
                    if (p.Pos.Y > h + 70f || p.Pos.X < -220f || p.Pos.X > w + 220f) {
                        list[i] = SpawnPetal(l, w, h, topOnly: true);
                    }
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

        //首帧铺满全屏，避免开场空窗
        private static void Prewarm(float w, float h) {
            prewarmed = true;
            for (int l = 0; l < LayerCount; l++) {
                layers[l].Clear();
                for (int i = 0; i < Caps[l]; i++) {
                    layers[l].Add(SpawnPetal(l, w, h, topOnly: false));
                }
            }
        }

        private static Petal SpawnPetal(int layer, float w, float h, bool topOnly) {
            float x = Main.rand.NextFloat(-160f, w + 160f);
            float y = topOnly ? -Main.rand.NextFloat(30f, 120f) : Main.rand.NextFloat(-40f, h);
            Color baseTint = Color.Lerp(HimayoMenuTheme.PetalPink, HimayoMenuTheme.PetalPinkDeep, Main.rand.NextFloat());
            //近层偶发暗红瓣，呼应背景灯笼
            if (layer == 2 && Main.rand.NextBool(34)) {
                baseTint = HimayoMenuTheme.PetalCrimson;
            }
            Petal p = new() {
                Pos = new Vector2(x, y),
                Rot = Main.rand.NextFloat(MathHelper.TwoPi),
                RotSpeed = Main.rand.NextFloat(-0.03f, 0.03f),
                SwayPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                Scale = Main.rand.NextFloat(SizeMin[layer], SizeMax[layer]),
                Tint = Color.Lerp(baseTint, HimayoMenuTheme.HazePurple, HazeMix[layer])
            };
            p.PrevPos = p.Pos;
            p.PrevRot = p.Rot;
            return p;
        }

        /// <summary>远+中两层，画在标题与按钮之下</summary>
        public static void DrawBack(SpriteBatch spriteBatch, float alpha, float fade) => DrawLayers(spriteBatch, alpha, fade, 0, 2);

        /// <summary>近层盖顶，画在一切菜单 UI 之上</summary>
        public static void DrawFront(SpriteBatch spriteBatch, float alpha, float fade) => DrawLayers(spriteBatch, alpha, fade, 2, 3);

        private static void DrawLayers(SpriteBatch spriteBatch, float alpha, float fade, int from, int to) {
            Effect deco = EffectLoader.OniDomainDeco?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            //花瓣是纯着色器形体，着色器缺席时整层跳过（背景与按钮不受影响）
            if (deco == null || white == null) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.UIScaleMatrix);

            Vector2 origin = white.Size() * 0.5f;
            deco.CurrentTechnique = deco.Techniques["TechMenuPetal"];
            for (int l = from; l < to; l++) {
                if (layers[l].Count == 0) {
                    continue;
                }
                //逐层柔边=景深，重新 Apply 生效
                deco.Parameters["uPetalSoftness"]?.SetValue(Softness[l]);
                deco.CurrentTechnique.Passes[0].Apply();

                Vector2 par = HimayoMenuCamera.ParallaxOffset(alpha, Parallax[l]);
                foreach (Petal p in layers[l]) {
                    Vector2 pos = Vector2.Lerp(p.PrevPos, p.Pos, alpha) + par;
                    float rot = MathHelper.Lerp(p.PrevRot, p.Rot, alpha);
                    Color c = p.Tint * (LayerAlpha[l] * fade);
                    Vector2 scale = new(p.Scale / white.Width, p.Scale * 1.15f / white.Height);
                    spriteBatch.Draw(white, pos, null, c, rot, origin, scale, SpriteEffects.None, 0f);
                }
            }
            spriteBatch.End();
        }
    }
}
