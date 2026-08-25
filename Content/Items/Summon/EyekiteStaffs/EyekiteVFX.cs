using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon.EyekiteStaffs
{
    /// <summary>缚瞳风筝色板与牵引/血带绘制</summary>
    internal static class EyekiteVFX
    {
        public static readonly Color BloodDeep = new(42, 7, 9);
        public static readonly Color Blood = new(168, 22, 32);
        public static readonly Color Arterial = new(210, 36, 45);
        public static readonly Color Sinew = new(232, 218, 204);

        public const int CordPoints = 18;
        public const int TrailPoints = 16;

        public static float Hash(int a, int b) {
            float v = MathF.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f;
            return v - MathF.Floor(v);
        }

        /// <summary>玩家肩侧到眼球尾的弹性悬链，含回弹挤波</summary>
        public static void BuildCord(Vector2[] points, Vector2 anchor, Vector2 eye
            , float tension, float twang, float twangPos, int seed, float time) {
            Vector2 delta = eye - anchor;
            float len = delta.Length();
            if (len < 4f) {
                for (int i = 0; i < points.Length; i++) {
                    points[i] = anchor;
                }
                return;
            }

            Vector2 dir = delta / len;
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float slack = MathHelper.Lerp(26f, 1.6f, tension);
            float wind = (1f - tension) * 5.5f;
            float twangAmp = twang * 14f;

            for (int i = 0; i < points.Length; i++) {
                float t = i / (float)(points.Length - 1);
                Vector2 p = Vector2.Lerp(anchor, eye, t);
                float belly = MathF.Sin(t * MathHelper.Pi);
                p.Y += belly * slack;
                float gust = MathF.Sin(time * 1.7f + t * 4.2f + seed * 0.31f);
                p += perp * (gust * wind * belly);
                float wave = MathF.Exp(-MathF.Pow((t - twangPos) * 9.5f, 2f));
                p += perp * (wave * twangAmp * MathF.Sin(time * 28f));
                points[i] = p;
            }
        }

        public static void DrawCord(ref Trail trail, Vector2[] points, TrailThicknessCalculator width
            , TrailColorEvaluator color, float tension, float twang, float twangPos, int seed, Color light) {
            Effect fx = EffectLoader.KiteSinew?.Value;
            if (fx == null || CWRAsset.PerlinNoise?.Value == null) {
                return;
            }

            trail ??= new Trail(points, width, color);
            trail.TrailPositions = points;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uTension"]?.SetValue(tension);
            fx.Parameters["uTwang"]?.SetValue(twang);
            fx.Parameters["uTwangPos"]?.SetValue(twangPos);
            fx.Parameters["uFade"]?.SetValue(1f);
            fx.Parameters["seed"]?.SetValue((seed % 97) * 0.017f);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            gd.BlendState = BlendState.AlphaBlend;
            trail.DrawTrail(fx);
        }

        public static float CordWidth(float t, float tension, float twang, float twangPos) {
            float mid = 1f - MathF.Abs(t - 0.5f) * 0.35f;
            float knot = MathHelper.Lerp(1.25f, 1f, MathHelper.Clamp(t / 0.12f, 0f, 1f));
            float attach = MathHelper.Lerp(1f, 1.15f, MathHelper.Clamp((t - 0.88f) / 0.12f, 0f, 1f));
            float taut = MathHelper.Lerp(7.2f, 4.1f, tension);
            float pinch = 1f - twang * 0.28f * MathF.Exp(-MathF.Pow((t - twangPos) * 8f, 2f));
            return taut * mid * knot * attach * pinch;
        }

        public static void DrawCordFallback(Vector2[] points, float tension, Color light) {
            Texture2D tex = TextureAssets.FishingLine.Value;
            Rectangle frame = tex.Frame();
            Vector2 origin = new Vector2(frame.Width / 2f, 2f);
            float thin = MathHelper.Lerp(1.15f, 0.7f, tension);
            for (int i = 0; i < points.Length - 1; i++) {
                Vector2 a = points[i];
                Vector2 b = points[i + 1];
                Vector2 diff = b - a;
                float rot = diff.ToRotation() - MathHelper.PiOver2;
                float stripe = (i + (int)(Main.GlobalTimeWrappedHourly * 3f)) % 3;
                Color c = stripe == 0 ? Sinew : Color.Lerp(Blood, Arterial, stripe * 0.5f);
                c = Color.Lerp(c, light, 0.25f);
                Vector2 scale = new Vector2(thin, (diff.Length() + 2f) / frame.Height);
                Main.EntitySpriteDraw(tex, a - Main.screenPosition, frame, c, rot, origin, scale, SpriteEffects.None, 0);
            }
        }

        public static void DrawChargeTrail(ref Trail trail, Vector2[] positions
            , TrailThicknessCalculator width, TrailColorEvaluator color, float intensity) {
            if (intensity <= 0.05f) {
                return;
            }
            Effect fx = EffectLoader.EocBloodTrail?.Value;
            bool bespoke = fx != null;
            fx ??= EffectLoader.GradientTrail?.Value;
            if (fx == null) {
                return;
            }

            trail ??= new Trail(positions, width, color);
            trail.TrailPositions = positions;

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            if (bespoke) {
                fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                fx.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.035f);
                fx.Parameters["uIntensity"]?.SetValue(intensity);
                gd.Textures[1] = CWRAsset.PerlinNoise.Value;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                gd.BlendState = BlendState.AlphaBlend;
                trail.DrawTrail(fx);
                return;
            }

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * -0.05f);
            fx.Parameters["uTimeG"]?.SetValue(Main.GlobalTimeWrappedHourly * -0.2f);
            fx.Parameters["udissolveS"]?.SetValue(1f);
            fx.Parameters["uBaseImage"]?.SetValue(VaultAsset.placeholder2.Value);
            fx.Parameters["uFlow"]?.SetValue(VaultAsset.placeholder2.Value);
            fx.Parameters["uGradient"]?.SetValue(CWRAsset.BloodRed_Bar.Value);
            fx.Parameters["uDissolve"]?.SetValue(VaultAsset.placeholder2.Value);
            gd.BlendState = BlendState.Additive;
            trail.DrawTrail(fx);
            gd.BlendState = BlendState.AlphaBlend;
        }

        public static void FillOldPosTrail(Projectile proj, Vector2[] dest) {
            int n = dest.Length;
            dest[n - 1] = proj.Center;
            int filled = 1;
            if (proj.oldPos != null) {
                for (int i = 0; i < proj.oldPos.Length && filled < n; i++) {
                    if (proj.oldPos[i] == Vector2.Zero) {
                        break;
                    }
                    Vector2 p = proj.oldPos[i] + proj.Size * 0.5f;
                    if (Vector2.DistanceSquared(p, dest[n - filled]) > 260f * 260f) {
                        break;
                    }
                    dest[n - 1 - filled] = p;
                    filled++;
                }
            }
            Vector2 oldest = dest[n - filled];
            for (int i = 0; i < n - filled; i++) {
                dest[i] = oldest;
            }
        }

        public static void IdleDrip(Vector2 pos) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, new Vector2(0f, 1.4f) + Main.rand.NextVector2Circular(0.4f, 0.3f)
                , Blood, Main.rand.NextFloat(0.45f, 0.7f)).Configure(28, 0.28f, 0.99f);
        }

        public static void ChargeSpray(Vector2 pos, Vector2 vel) {
            if (Main.dedServ) {
                return;
            }
            Vector2 back = vel.SafeNormalize(-Vector2.UnitX);
            for (int i = 0; i < 3; i++) {
                Vector2 v = -back.RotatedByRandom(0.5f) * Main.rand.NextFloat(2.2f, 5.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, v, i == 0 ? Arterial : Blood
                    , Main.rand.NextFloat(0.55f, 0.95f)).Configure(22 + i * 4, 0.22f, 0.986f);
            }
        }

        public static void YankBurst(Vector2 pos, Vector2 yankDir) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = yankDir.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 8; i++) {
                Vector2 v = dir.RotatedByRandom(0.9f) * Main.rand.NextFloat(3f, 9f)
                    + Main.rand.NextVector2Circular(1.5f, 1.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, v
                    , Main.rand.NextBool(3) ? BloodDeep : Arterial
                    , Main.rand.NextFloat(0.6f, 1.15f)).Configure(26 + i, 0.3f, 0.984f);
            }
        }

        public static void HitSplat(Vector2 pos, Vector2 dir) {
            if (Main.dedServ) {
                return;
            }
            Vector2 n = dir.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 6; i++) {
                Vector2 v = n.RotatedByRandom(0.8f) * Main.rand.NextFloat(2.5f, 7f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, v, Arterial
                    , Main.rand.NextFloat(0.5f, 0.9f)).Configure(20, 0.26f, 0.987f);
            }
        }
    }
}
