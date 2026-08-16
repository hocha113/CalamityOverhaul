using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaWisps
{
    /// <summary>
    /// 湖面鬼火层渲染与演出派发（纯客户端，只对观看域生效）：
    /// 火体=KikasaWispFire.TechLakeFire 世界锚定 quad（画在 EndEntityDraw，
    /// 自动获得湖面镜面倒影）；粒子只作点缀——离体鬼火珠、贴根火舌、
    /// 压制拍的蒸汽与濒死出逃珠；水线金光照明
    /// </summary>
    internal static class KikasaWispFX
    {
        /// <summary>水线上火舌画布高（世界 px），内容经 guard 在画布顶前归零</summary>
        private const float FlameCanvasH = 170f;

        /// <summary>水线下浸线金晕画布高（深层照亮由 KikasaGrade.uWispGlow 接管）</summary>
        private const float LipCanvasH = 40f;

        private static int orbTimer;
        private static int tongueTimer;
        private static int steamTimer;
        private static int frontFxTimer;
        private static bool quenchHissDone;

        public static void Clear() {
            orbTimer = tongueTimer = steamTimer = frontFxTimer = 0;
            quenchHissDone = false;
        }

        /// <summary>有效燃沿半径：燃满后覆盖整湖——湖带跟着施术者走，火跟着湖走</summary>
        internal static float EffectiveReachPx(KikasaDomainPlayer kdp)
            => kdp.WispSpread >= 0.999f
                ? KikasaLakeSurface.HalfWidth * 8f
                : kdp.WispSpread * KikasaLakeSurface.HalfWidth;

        //行进前沿亮度：蔓延中亮、收火反啃暗一档、压制/静息无前沿
        private static float FrontGlow(KikasaDomainPlayer kdp) {
            if (kdp.WispQuench > 0.01f || kdp.WispSpread <= 0.001f || kdp.WispSpread >= 0.999f) {
                return 0f;
            }
            return kdp.WispFireActive ? 1f : 0.45f;
        }

        //已燃段 ∩ 屏幕 ∩ 湖带，粒子与照明只落在真燃着的地方
        private static bool ComputeBurnSpan(KikasaDomainPlayer kdp, out float xMin, out float xMax) {
            float reach = EffectiveReachPx(kdp);
            float casterX = kdp.Player.Center.X;
            xMin = MathF.Max(MathF.Max(Main.screenPosition.X - 60f,
                casterX - KikasaLakeSurface.HalfWidth), kdp.WispOriginX - reach);
            xMax = MathF.Min(MathF.Min(Main.screenPosition.X + Main.screenWidth + 60f,
                casterX + KikasaLakeSurface.HalfWidth), kdp.WispOriginX + reach);
            return xMax - xMin > 16f;
        }

        public static void Update() {
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;
            if (kdp == null || !kdp.AnyActive) {
                quenchHissDone = false;
                return;
            }

            //淬熄嘶声：压制拍起手一记——雨浇进火里
            if (kdp.WispQuench > 0.02f && !quenchHissDone) {
                quenchHissDone = true;
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.85f, Pitch = -0.25f, MaxInstances = 2 },
                    new Vector2(kdp.Player.Center.X, kdp.LakeWorldY));
            }
            if (kdp.WispQuench < 0.005f) {
                quenchHissDone = false;
            }

            float burn = kdp.WispT;
            if (burn <= 0.02f || kdp.DreamWorldVisual) {
                return;
            }
            if (!ComputeBurnSpan(kdp, out float xMin, out float xMax)) {
                return;
            }

            float lakeY = kdp.LakeWorldY;
            bool quenching = kdp.WispQuench > 0.02f;

            if (!quenching) {
                //离体鬼火珠：贴水缓浮的游魂灯
                if (--orbTimer <= 0) {
                    orbTimer = (int)MathHelper.Lerp(26f, 9f, burn);
                    float x = Main.rand.NextFloat(xMin, xMax);
                    PRTLoader.NewParticle<PRT_KikasaWispOrb>(
                        new Vector2(x, lakeY - Main.rand.NextFloat(2f, 14f)),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.5f, 1.1f)),
                        KikasaWisp.Tint(KikasaWisp.GoldBody) * burn,
                        Main.rand.NextFloat(0.7f, 1.15f))
                        ?.Configure(Main.rand.Next(80, 130), Main.rand.NextFloat(0.4f, 1f));
                }
                //贴根火舌偶尔腾起一簇
                if (--tongueTimer <= 0) {
                    tongueTimer = (int)MathHelper.Lerp(15f, 6f, burn);
                    float x = Main.rand.NextFloat(xMin, xMax);
                    PRTLoader.NewParticle<PRT_KikasaWispFlame>(
                        new Vector2(x, lakeY - 2f),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(1.4f, 2.6f)),
                        KikasaWisp.Tint(KikasaWisp.GoldBody) * (0.6f + 0.4f * burn),
                        Main.rand.NextFloat(0.6f, 1.1f))
                        ?.Configure(Main.rand.Next(24, 40));
                }
            }
            else if (kdp.WispQuench < 0.985f && ++steamTimer >= 3) {
                //压制拍：雨浇火的冷灰蒸汽 + 濒死火珠上逃即灭
                steamTimer = 0;
                float x = Main.rand.NextFloat(xMin, xMax);
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    new Vector2(x, lakeY - Main.rand.NextFloat(4f, 26f)),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.5f, 1.2f)),
                    new Color(150, 150, 142) * Main.rand.NextFloat(0.4f, 0.65f),
                    Main.rand.NextFloat(0.6f, 1.0f))
                    ?.Configure(Main.rand.Next(40, 80));
                if (Main.rand.NextBool(2)) {
                    float x2 = Main.rand.NextFloat(xMin, xMax);
                    PRTLoader.NewParticle<PRT_KikasaWispOrb>(
                        new Vector2(x2, lakeY - Main.rand.NextFloat(0f, 10f)),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1.6f, 3f)),
                        KikasaWisp.PaleDying * 0.9f, Main.rand.NextFloat(0.5f, 0.9f))
                        ?.Configure(Main.rand.Next(26, 44), Main.rand.NextFloat(0.8f, 1.6f), dyingMode: true);
                }
            }

            //燃沿行进：前沿点上的火花与被火搅动的水
            if (kdp.WispFireActive && !quenching
                && kdp.WispSpread > 0.01f && kdp.WispSpread < 0.999f && ++frontFxTimer >= 3) {
                frontFxTimer = 0;
                float reach = kdp.WispSpread * KikasaLakeSurface.HalfWidth;
                for (int side = -1; side <= 1; side += 2) {
                    float fx = kdp.WispOriginX + side * reach;
                    if (fx < Main.screenPosition.X - 40f
                        || fx > Main.screenPosition.X + Main.screenWidth + 40f
                        || MathF.Abs(fx - kdp.Player.Center.X) > KikasaLakeSurface.HalfWidth) {
                        continue;
                    }
                    Vector2 at = new(fx, lakeY);
                    PRTLoader.NewParticle<PRT_KikasaWispFlame>(
                        at + new Vector2(Main.rand.NextFloat(-8f, 8f), -2f),
                        new Vector2(side * Main.rand.NextFloat(0.5f, 1.6f), -Main.rand.NextFloat(2f, 3.4f)),
                        KikasaWisp.GoldCore * 0.9f, Main.rand.NextFloat(0.7f, 1.2f))
                        ?.Configure(Main.rand.Next(22, 34));
                    if (Main.rand.NextBool(4)) {
                        KikasaDomainDeco.RippleAt(at, Main.rand.NextFloat(0.32f, 0.5f));
                    }
                }
            }

            //水线金光照明：湖面被火照亮（领域是本地叠加层，照明只在观看端）
            float k = burn * (1f - kdp.WispQuench * 0.7f) * 0.62f;
            for (float x = xMin; x <= xMax; x += 170f) {
                Lighting.AddLight(new Vector2(x, lakeY - 10f), 0.86f * k, 0.60f * k, 0.22f * k);
            }
        }

        /// <summary>点燃确认拍：脚下腾起一蓬火舌与几盏游珠（命令端观看时由 ToggleWispFire 调）</summary>
        internal static void IgniteBurst(Vector2 lakeAt) {
            for (int i = 0; i < 12; i++) {
                float ang = -MathHelper.PiOver2 + Main.rand.NextFloat(-0.8f, 0.8f);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(1.6f, 4.4f);
                PRTLoader.NewParticle<PRT_KikasaWispFlame>(
                    lakeAt + new Vector2(Main.rand.NextFloat(-16f, 16f), -2f), vel,
                    KikasaWisp.Tint(KikasaWisp.GoldBody), Main.rand.NextFloat(0.7f, 1.3f))
                    ?.Configure(Main.rand.Next(26, 44));
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_KikasaWispOrb>(
                    lakeAt + new Vector2(Main.rand.NextFloat(-24f, 24f), -4f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.8f, 1.8f)),
                    KikasaWisp.Tint(KikasaWisp.GoldBody), Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(Main.rand.Next(60, 100), Main.rand.NextFloat(0.5f, 1.2f));
            }
        }

        public static void Draw(SpriteBatch spriteBatch) {
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;
            if (kdp == null || !kdp.AnyActive || kdp.DreamWorldVisual || kdp.WispT <= 0.012f) {
                return;
            }

            //quad 只裁屏幕与湖带，未燃段由着色器的 reach 熄掉——前沿辉光要越出已燃段一点
            float casterX = kdp.Player.Center.X;
            float left = MathF.Max(Main.screenPosition.X - 120f, casterX - KikasaLakeSurface.HalfWidth);
            float right = MathF.Min(Main.screenPosition.X + Main.screenWidth + 120f, casterX + KikasaLakeSurface.HalfWidth);
            if (right - left < 8f) {
                return;
            }

            Effect fx = EffectLoader.KikasaWispFire?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || white == null || noise == null) {
                DrawFallback(spriteBatch, kdp, left, right);
                return;
            }

            float quadW = right - left;
            float quadH = FlameCanvasH + LipCanvasH;
            Vector2 topLeft = new(left, kdp.LakeWorldY - FlameCanvasH);

            fx.Parameters["uTime"]?.SetValue(kdp.EffectTime);
            fx.Parameters["uRain"]?.SetValue(kdp.RainBlend);
            fx.Parameters["uQuadSize"]?.SetValue(new Vector2(quadW, quadH));
            fx.Parameters["uWaterV"]?.SetValue(FlameCanvasH / quadH);
            fx.Parameters["uWorldX0"]?.SetValue(left);
            fx.Parameters["uOriginX"]?.SetValue(kdp.WispOriginX);
            fx.Parameters["uSpreadPx"]?.SetValue(EffectiveReachPx(kdp));
            fx.Parameters["uFrontGlow"]?.SetValue(FrontGlow(kdp));
            fx.Parameters["uLakeMinX"]?.SetValue(casterX - KikasaLakeSurface.HalfWidth);
            fx.Parameters["uLakeMaxX"]?.SetValue(casterX + KikasaLakeSurface.HalfWidth);
            fx.Parameters["uBurn"]?.SetValue(kdp.WispT);
            fx.Parameters["uQuench"]?.SetValue(kdp.WispQuench);
            //水线波动幅度与湖面着色器同源换算（uv 幅 × 屏高 → 世界 px 要除 zoom）
            float wobblePx = (0.0025f + 0.011f * kdp.FoamBoost) * Main.screenHeight
                / MathF.Max(Main.GameViewMatrix.Zoom.Y, 0.01f);
            fx.Parameters["uWobblePx"]?.SetValue(wobblePx);
            KikasaDomainDeco.FillWaveUniformsWorld(fx);

            GraphicsDevice gd = Main.instance.GraphicsDevice;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique = fx.Techniques["TechLakeFire"];
            fx.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(white, topLeft - Main.screenPosition, null, Color.White, 0f,
                Vector2.Zero, new Vector2(quadW / white.Width, quadH / white.Height), SpriteEffects.None, 0f);
            spriteBatch.End();
        }

        //着色器缺失的克制回退：单层弱加色条 + 既有粒子，不拿灰度堆叠模拟火形（设计约束）
        private static void DrawFallback(SpriteBatch spriteBatch, KikasaDomainPlayer kdp, float left, float right) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            float burn = kdp.WispT * (1f - kdp.WispQuench * 0.8f);
            if (glow == null || burn <= 0.02f) {
                return;
            }
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            Color gold = KikasaWisp.Tint(KikasaWisp.GoldBody) * (0.22f * burn);
            float w = right - left;
            Vector2 pos = new Vector2(left + w * 0.5f, kdp.LakeWorldY - 14f) - Main.screenPosition;
            spriteBatch.Draw(glow, pos, null, gold, 0f, glow.Size() * 0.5f,
                new Vector2(w / glow.Width, 64f / glow.Height), SpriteEffects.None, 0f);
            spriteBatch.End();
        }
    }
}
