using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>
    /// 血湖领域装饰：撕裂前沿飞散的湿纸屑、湖面涟漪、贴水血雾。
    /// 纸屑落进血湖会溅起小涟漪——湖是实体存在，不是贴图。
    /// </summary>
    internal static class KikasaDomainDeco
    {
        private class Scrap
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Rot;
            public float RotSpeed;
            public float SwayPhase;
            public float W;
            public float H;
            //湿度 0干~1透，越湿越暗越沉
            public float Wetness;
            public float Alpha = 1f;
            public int Life;
            public int MaxLife;
        }

        private class Ripple
        {
            public Vector2 Pos;
            public float Scale;
            public int Life;
            public int MaxLife;
        }

        private static readonly List<Scrap> scraps = new();
        private static readonly List<Ripple> ripples = new();

        private const int ScrapCap = 60;
        private const int RippleCap = 16;

        private static readonly Color PaperDry = new(176, 156, 130);
        private static readonly Color PaperWet = new(96, 80, 70);
        //血系配色随观看域的鬼雨异化冷化（血珠→尸雨灰白、血雾→潮雾沉青、血光→冷青微光）
        private static Color SplashPale => KikasaDomain.CoolTint(new(214, 118, 106), new(170, 185, 190));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));
        private static Color RippleGlow => KikasaDomain.CoolTint(new(198, 88, 82), new(126, 152, 158));

        private static int mistTimer;
        private static int rippleTimer;
        //满幕雨帘的补投累积
        private static float rainCarry;

        public static void Clear() {
            scraps.Clear();
            ripples.Clear();
            rainCarry = 0f;
        }

        /// <summary>撕裂前沿喷纸屑：沿覆盖圆的可见弧段撒点，向外飞散</summary>
        public static void BurstScraps(KikasaDomainPlayer kdp, int count) {
            Vector2 originScreen = Vector2.Transform(
                kdp.OriginWorldPos - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix);
            float diag = new Vector2(Main.screenWidth, Main.screenHeight).Length();
            float radius = kdp.SpreadProgress * 1.18f * diag;
            Matrix inv = Matrix.Invert(Main.GameViewMatrix.TransformationMatrix);

            for (int i = 0; i < count && scraps.Count < ScrapCap; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dir = ang.ToRotationVector2();
                Vector2 screenPx = originScreen + dir * (radius + Main.rand.NextFloat(-26f, 26f));
                //只在屏幕附近生，圆周大部分在屏外时省掉
                if (screenPx.X < -120f || screenPx.X > Main.screenWidth + 120f
                    || screenPx.Y < -120f || screenPx.Y > Main.screenHeight + 120f) {
                    continue;
                }
                Vector2 world = Vector2.Transform(screenPx, inv) + Main.screenPosition;
                SpawnScrap(world,
                    dir * Main.rand.NextFloat(2.2f, 5.5f) + new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.3f)),
                    Main.rand.NextFloat(0.55f, 1f));
            }
        }

        /// <summary>水面触脚溅花：血珠扇形溅起 + 潮雾</summary>
        public static void SplashAt(Vector2 world, int count) {
            for (int i = 0; i < count; i++) {
                float angle = -MathHelper.Pi * (0.15f + 0.7f * i / MathF.Max(count - 1, 1));
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(1.8f, 4.2f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    world + new Vector2(Main.rand.NextFloat(-14f, 14f), -2f),
                    vel, SplashPale * Main.rand.NextFloat(0.4f, 0.62f),
                    Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(Main.rand.Next(18, 30), vel.X);
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(
                world + new Vector2(0f, -6f),
                new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -0.1f),
                MistBlood * 0.8f, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(70, 110));
        }

        /// <summary>沸腾气泡：沿水线随机散点破水的碎泡，颜色随镜面预览向目标形态先行渐变</summary>
        public static void BoilBurst(KikasaDomainPlayer kdp, float strength, float coldMix) {
            Color bubble = Color.Lerp(new(214, 118, 106), new(170, 185, 190), coldMix);

            int count = 1 + (int)(strength * 3f);
            float left = Main.screenPosition.X;
            for (int i = 0; i < count; i++) {
                float x = left + Main.rand.NextFloat(0f, Main.screenWidth);
                Vector2 pos = new(x, kdp.LakeWorldY - Main.rand.NextFloat(0f, 4f));
                Vector2 vel = new(Main.rand.NextFloat(-0.8f, 0.8f),
                    -Main.rand.NextFloat(1.6f, 3.6f) * (0.6f + strength * 0.6f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos, vel,
                    bubble * Main.rand.NextFloat(0.4f, 0.62f),
                    Main.rand.NextFloat(0.45f, 0.8f))
                    ?.Configure(Main.rand.Next(16, 30), vel.X);
            }
            //滚水自己也荡圈
            if (Main.rand.NextBool(5)) {
                RippleAt(new Vector2(left + Main.rand.NextFloat(0f, Main.screenWidth), kdp.LakeWorldY),
                    Main.rand.NextFloat(0.4f, 0.9f) * (0.5f + strength * 0.5f));
            }
        }

        /// <summary>沸腾蒸汽：贴水上浮的翻滚潮气</summary>
        public static void BoilSteam(KikasaDomainPlayer kdp, float strength, float coldMix) {
            Color steam = Color.Lerp(new(58, 18, 20), new(52, 62, 66), coldMix);
            int count = 1 + (int)(strength * 2f);
            for (int i = 0; i < count; i++) {
                float x = Main.screenPosition.X + Main.rand.NextFloat(0f, Main.screenWidth);
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    new Vector2(x, kdp.LakeWorldY - Main.rand.NextFloat(2f, 24f)),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f),
                        -Main.rand.NextFloat(0.25f, 0.7f) * (0.5f + strength)),
                    steam * Main.rand.NextFloat(0.5f, 0.8f),
                    Main.rand.NextFloat(0.6f, 1.0f))
                    ?.Configure(Main.rand.Next(50, 90));
            }
        }

        /// <summary>湖面荡开一圈涟漪</summary>
        public static void RippleAt(Vector2 world, float scale) {
            if (ripples.Count >= RippleCap) {
                return;
            }
            ripples.Add(new Ripple {
                Pos = world,
                Scale = scale,
                MaxLife = Main.rand.Next(34, 50)
            });
        }

        private static void SpawnScrap(Vector2 pos, Vector2 vel, float wetness) {
            float w = Main.rand.NextFloat(6f, 15f);
            scraps.Add(new Scrap {
                Pos = pos,
                Vel = vel,
                Rot = Main.rand.NextFloat(MathHelper.TwoPi),
                RotSpeed = Main.rand.NextFloat(-0.07f, 0.07f),
                SwayPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                W = w,
                H = w * Main.rand.NextFloat(0.4f, 0.8f),
                Wetness = wetness,
                MaxLife = Main.rand.Next(110, 200)
            });
        }

        public static void Update() {
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;
            if (kdp == null) {
                if (scraps.Count > 0 || ripples.Count > 0) {
                    Clear();
                }
                return;
            }

            bool steady = kdp.Phase == KikasaDomainPhase.Open;
            bool lakeReady = kdp.RiseT > 0.95f;

            //稳态零星飘落的湿纸屑，领域残余的碎纸一直在往湖里掉

            if (steady && scraps.Count < 18 && Main.rand.NextBool(26)) {
                float x = Main.screenPosition.X + Main.rand.NextFloat(-100f, Main.screenWidth + 100f);
                float y = Main.screenPosition.Y - Main.rand.NextFloat(30f, 90f);
                SpawnScrap(new Vector2(x, y),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.4f, 0.9f)),
                    Main.rand.NextFloat(0.35f, 0.9f));
            }

            //死水偶发的自发涟漪与贴水血雾

            if (steady && lakeReady) {
                if (--rippleTimer <= 0) {
                    rippleTimer = Main.rand.Next(50, 130);
                    float x = Main.screenPosition.X + Main.rand.NextFloat(0f, Main.screenWidth);
                    RippleAt(new Vector2(x, kdp.LakeWorldY), Main.rand.NextFloat(0.5f, 1.1f));
                }
                if (--mistTimer <= 0) {
                    mistTimer = Main.rand.Next(40, 90);
                    float x = Main.screenPosition.X + Main.rand.NextFloat(0f, Main.screenWidth);
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        new Vector2(x, kdp.LakeWorldY - Main.rand.NextFloat(4f, 30f)),
                        new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -Main.rand.NextFloat(0.03f, 0.10f)),
                        MistBlood * Main.rand.NextFloat(0.45f, 0.7f),
                        Main.rand.NextFloat(0.55f, 0.95f))
                        ?.Configure(Main.rand.Next(90, 150));
                }
            }

            UpdateRainCurtain(kdp);
            UpdateScraps(kdp, lakeReady);
            UpdateRipples();
        }

        /// <summary>异化态满幕雨帘：密度吃领域的雨帘包络，做法镜像鬼雨世界常驻雨（湿墨色板）</summary>
        private static void UpdateRainCurtain(KikasaDomainPlayer kdp) {
            float density = kdp.RainCurtainDensity;
            if (density < 0.02f) {
                rainCarry = 0f;
                return;
            }

            float left = Main.screenPosition.X - 160f;
            float right = Main.screenPosition.X + Main.screenWidth + 160f;
            rainCarry += density * 0.02f * (right - left);
            int count = Math.Min((int)rainCarry, 72);
            rainCarry -= count;
            //进量超帧上限时截断积欠，防翻转叠加下无限攒债
            rainCarry = MathF.Min(rainCarry, 30f);
            if (count <= 0) {
                return;
            }

            Color pale = new(170, 185, 190);
            Color corpse = new(140, 170, 165);
            float wind = MathF.Sin(Main.worldID % 255 * 0.37f) * 2.2f * density;
            for (int i = 0; i < count; i++) {
                Vector2 pos = new(Main.rand.NextFloat(left, right),
                    Main.screenPosition.Y - Main.rand.NextFloat(10f, 220f));
                Vector2 vel = new(wind + Main.rand.NextFloat(-0.35f, 0.35f),
                    Main.rand.NextFloat(11f, 17f));
                Color color = (Main.rand.NextBool(7) ? corpse : pale)
                    * Main.rand.NextFloat(0.42f, 0.65f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos, vel, color,
                    Main.rand.NextFloat(0.8f, 1.25f))
                    ?.Configure(Main.rand.Next(70, 110), vel.X);
            }
        }

        private static void UpdateScraps(KikasaDomainPlayer kdp, bool lakeReady) {
            float screenBottom = Main.screenPosition.Y + Main.screenHeight;

            for (int i = scraps.Count - 1; i >= 0; i--) {
                Scrap s = scraps[i];
                s.Life++;
                s.SwayPhase += 0.035f;
                //湿纸重，横向飞散被空气很快吃掉，剩下沉沉往下坠
                s.Vel.X *= 0.955f;
                s.Vel.X += MathF.Sin(s.SwayPhase) * 0.02f * (1f - s.Wetness * 0.6f);
                s.Vel.Y = MathF.Min(s.Vel.Y + 0.035f, 1.0f + s.Wetness * 0.8f);
                s.Pos += s.Vel;
                s.Rot += s.RotSpeed + MathF.Sin(s.SwayPhase * 0.6f) * 0.010f;
                //飞着继续吸潮
                s.Wetness = MathF.Min(s.Wetness + 0.004f, 1f);

                //落湖：小涟漪 + 消失
                if (lakeReady && s.Pos.Y >= kdp.LakeWorldY - 2f) {
                    RippleAt(new Vector2(s.Pos.X, kdp.LakeWorldY), 0.32f + s.W * 0.014f);
                    scraps.RemoveAt(i);
                    continue;
                }

                if (s.Life >= s.MaxLife) {
                    s.Alpha -= 0.05f;
                    if (s.Alpha <= 0f) {
                        scraps.RemoveAt(i);
                        continue;
                    }
                }

                if (s.Pos.Y > screenBottom + 80f) {
                    scraps.RemoveAt(i);
                }
            }
        }

        private static void UpdateRipples() {
            for (int i = ripples.Count - 1; i >= 0; i--) {
                Ripple r = ripples[i];
                r.Life++;
                if (r.Life >= r.MaxLife) {
                    ripples.RemoveAt(i);
                }
            }
        }

        public static void Draw(SpriteBatch spriteBatch) {
            if (scraps.Count == 0 && ripples.Count == 0) {
                return;
            }

            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            if (white == null) {
                return;
            }

            //湿纸屑：素色小片，越湿越暗

            if (scraps.Count > 0) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);

                Vector2 origin = white.Size() * 0.5f;
                foreach (Scrap s in scraps) {
                    Color c = Color.Lerp(PaperDry, PaperWet, s.Wetness) * s.Alpha;
                    Vector2 scale = new(s.W / white.Width, s.H / white.Height);
                    spriteBatch.Draw(white, s.Pos - Main.screenPosition, null, c,
                        s.Rot, origin, scale, SpriteEffects.None, 0f);
                }

                spriteBatch.End();
            }

            //湖面涟漪：压扁的扩散环，加色微光

            if (ring != null && ripples.Count > 0) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);

                Vector2 rOrigin = ring.Size() * 0.5f;
                foreach (Ripple r in ripples) {
                    float lifeF = r.Life / (float)r.MaxLife;
                    float radius = MathHelper.Lerp(8f, 86f, 1f - (1f - lifeF) * (1f - lifeF)) * r.Scale;
                    float alpha = MathF.Sin(MathHelper.Clamp(lifeF, 0f, 1f) * MathHelper.Pi) * 0.34f;
                    //真加色批源因子是 SourceAlpha：A 置零=整圈不画，A 随强度走
                    Color c = RippleGlow * alpha;
                    Vector2 scale = new(radius * 2f / ring.Width, radius * 0.44f / ring.Height);
                    spriteBatch.Draw(ring, r.Pos - Main.screenPosition, null, c,
                        0f, rOrigin, scale, SpriteEffects.None, 0f);
                }

                spriteBatch.End();
            }
        }
    }
}
