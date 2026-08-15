using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls
{
    /// <summary>
    /// 转化演出的共用拍：化水、聚拢、成形三幕共一套语汇——
    /// 雨拽（四面雨线扑向一点，让人看清动手的是"雨"）、水爆、浊雾、水环、尸斑青冷闪。
    /// 全是本地表现，dedServ 与可见性由调用方把关。
    /// </summary>
    internal static class KikasaThrallFX
    {
        /// <summary>雨拽：把四周的雨线拽向 focus，起点偏上取"雨自天来"的势</summary>
        internal static void RainYank(Vector2 focus, int count, float radius, float scale = 1f) {
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.25f, 0.25f);
                Vector2 dir = angle.ToRotationVector2();
                dir = new Vector2(dir.X, dir.Y * 0.7f - 0.5f).SafeNormalize(-Vector2.UnitY);
                PRTLoader.NewParticle<PRT_GhostRainYank>(
                    focus + dir * radius * Main.rand.NextFloat(0.72f, 1.15f),
                    -dir * Main.rand.NextFloat(1.6f, 3.4f),
                    KikasaThrall.PaleSheen * Main.rand.NextFloat(0.4f, 0.68f),
                    Main.rand.NextFloat(0.55f, 0.95f) * scale)
                    ?.Configure(focus, Main.rand.Next(26, 38));
            }
        }

        /// <summary>
        /// 水爆：重团配轻滴两个粒径一起炸，单一粒径会糊成一坨读不出量。
        /// upward=向上的扇形（砸地、顶出），否则整圈
        /// </summary>
        internal static void WaterBurst(Vector2 center, int count, float power, bool upward) {
            for (int i = 0; i < count; i++) {
                float angle = upward
                    ? -MathHelper.Pi * (0.08f + 0.84f * i / MathF.Max(count - 1, 1))
                    : MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.14f, 0.14f);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(2.2f, 5.4f) * power;
                PRTLoader.NewParticle<PRT_SewageGlob>(
                    center + Main.rand.NextVector2Circular(11f, 7f), vel,
                    Color.Lerp(KikasaThrall.SewageDeep, KikasaThrall.CorpseTeal,
                        Main.rand.NextFloat(0.45f)) * Main.rand.NextFloat(0.6f, 0.9f),
                    Main.rand.NextFloat(0.45f, 0.8f) * power)
                    ?.Configure(Main.rand.Next(18, 34));
                if (i % 2 == 0) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(center, vel * 1.3f,
                        KikasaThrall.PaleSheen * Main.rand.NextFloat(0.35f, 0.55f),
                        Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(Main.rand.Next(16, 28), vel.X * 0.25f);
                }
            }
        }

        /// <summary>浊雾：贴地摊开的一排潮气，给爆点垫体积</summary>
        internal static void MistRing(Vector2 center, int count, float spread, float scale = 1f) {
            for (int i = 0; i < count; i++) {
                float side = spread * (i / MathF.Max(count - 1, 1) * 2f - 1f);
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    center + new Vector2(side, Main.rand.NextFloat(-10f, 2f)),
                    new Vector2(side * 0.02f, -Main.rand.NextFloat(0.1f, 0.35f)),
                    KikasaThrall.SewageDark * Main.rand.NextFloat(0.55f, 0.85f),
                    Main.rand.NextFloat(0.7f, 1.1f) * scale)
                    ?.Configure(Main.rand.Next(60, 100));
            }
        }

        /// <summary>
        /// 水环：湿墨色板的冲击环，squish 小=贴地透视。
        /// 调用方须处于实体批，ShockRingDraw 内部切批后还原
        /// </summary>
        internal static void WaterRing(SpriteBatch sb, Vector2 world, float radius,
            float squish, float alpha, float seed = 0f) {
            if (alpha <= 0.01f) {
                return;
            }
            ShockRingDraw.Draw(sb, world, radius, MathHelper.Max(radius * 0.16f, 5f),
                KikasaThrall.PaleSheen, KikasaThrall.CorpseTeal, KikasaThrall.SewageDeep,
                alpha, tearPx: -1f, squish: squish, innerGlow: 0.12f, timeSeed: seed);
        }

        /// <summary>冷闪：黑底软辉，A=0 在普通批里当加色使</summary>
        internal static void Flash(SpriteBatch sb, Vector2 world, float radius,
            float flat, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || alpha <= 0.01f) {
                return;
            }
            sb.Draw(glow, world - Main.screenPosition, null,
                (KikasaThrall.CorpseTeal with { A = 0 }) * alpha, 0f, glow.Size() * 0.5f,
                new Vector2(radius * 2f / glow.Width, radius * 2f * flat / glow.Height),
                SpriteEffects.None, 0f);
        }

        /// <summary>演出包络常用的快起缓收</summary>
        internal static float EaseOut(float t) => 1f - MathF.Pow(1f - MathHelper.Clamp(t, 0f, 1f), 3f);
    }
}
