using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>漫天星鱼域内资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishFallenStarAssets
    {
        /// <summary>坠星彗尾条带，金头→深蓝→靛蓝星尘撕尾</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishFallenStarComet { get; private set; }

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        internal static Asset<Texture2D> RayTex = null;//窄竖芒条，十字芒与条带分段共用
        [VaultLoaden(CWRConstant.Masking + "BlankStar")]
        internal static Asset<Texture2D> CrossTex = null;//锐利四芒星芯
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;//仅作靛蓝底晕与微热芯，禁当 body
    }

    /// <summary>漫天星鱼</summary>
    internal static class FishFallenStarVFX
    {
        /// <summary>夜空靛蓝（暗底/外圈）</summary>
        public static readonly Color NightIndigo = new(34, 46, 112);
        /// <summary>深蓝（条带中段、汇聚细芒）</summary>
        public static readonly Color DeepBlue = new(74, 112, 205);
        /// <summary>星金（主强调色）</summary>
        public static readonly Color StarGold = new(255, 198, 96);
        /// <summary>淡金热芯（小面积、短驻留）</summary>
        public static readonly Color PaleGold = new(255, 238, 190);

        /// <summary>定向震屏，尊重服务器配置；坠星落点专用，幅度克制</summary>
        public static void Punch(Vector2 pos, Vector2 dir, float strength, int frames, float falloff = 600f) {
            if (Main.dedServ || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                pos, dir.SafeNormalize(Vector2.UnitY), strength, 12f, frames, falloff, "FishFallenStar"));
        }

        /// <summary>星尘迸洒</summary>
        public static void StardustBurst(Vector2 pos, Vector2 bias, int count, float speed, float scaleMul = 1f) {
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.3f, 1f) * speed + bias;
                Color col = Main.rand.NextBool(3) ? DeepBlue : StarGold;
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos + Main.rand.NextVector2Circular(6f, 6f), vel
                    , col, Main.rand.NextFloat(0.35f, 0.7f) * scaleMul)?.Configure(true, Main.rand.Next(24, 40));
            }
        }

        /// <summary>十字闪芒爆点</summary>
        public static void CrossPop(Vector2 pos, float scale, int life = 14) {
            PRTLoader.NewParticle<PRT_Sparkle>(pos, Vector2.Zero, StarGold, scale)
                ?.Configure(NightIndigo, life, Main.rand.NextFloat(-0.06f, 0.06f), 0.85f);
        }

        /// <summary>星光汇聚</summary>
        public static void Converge(Vector2 center, float radius, int count, float inSpeed, float scaleMul = 1f) {
            for (int i = 0; i < count; i++) {
                float ang = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.25f, 0.25f);
                Vector2 pos = center + ang.ToRotationVector2() * radius * Main.rand.NextFloat(0.82f, 1.15f);
                Vector2 vel = (center - pos).SafeNormalize(Vector2.Zero) * inSpeed * Main.rand.NextFloat(0.8f, 1.2f);
                Color col = Main.rand.NextBool(3) ? DeepBlue : StarGold;
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, vel, col, Main.rand.NextFloat(0.4f, 0.75f) * scaleMul)
                    ?.Configure(false, Main.rand.Next(20, 30));
            }
        }

        /// <summary>落点小新星环爆</summary>
        public static void NovaRing(Vector2 pos, float scale) {
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, NightIndigo, 0.16f * scale)
                ?.Configure(Vector2.One, 0f, 0.58f * scale, 14);
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, StarGold, 0.10f * scale)
                ?.Configure(Vector2.One, 0f, 0.40f * scale, 11);
        }

        /// <summary>星体十字闪芒</summary>
        public static void DrawStarGlint(SpriteBatch sb, Vector2 drawPos, float alpha, float scale, float twinkle, float rot) {
            Texture2D ray = FishFallenStarAssets.RayTex?.Value;
            Texture2D cross = FishFallenStarAssets.CrossTex?.Value;
            Texture2D glow = FishFallenStarAssets.GlowTex?.Value;
            if (ray == null || cross == null) {
                return;
            }
            //靛蓝底晕
            if (glow != null) {
                sb.Draw(glow, drawPos, null, (NightIndigo with { A = 0 }) * (alpha * 0.55f), 0f
                    , glow.Size() * 0.5f, scale * 1.05f, SpriteEffects.None, 0f);
            }
            //双轴窄芒错相脉动
            float pulseV = 1f + 0.32f * MathF.Sin(twinkle);
            float pulseH = 1f + 0.32f * MathF.Sin(twinkle + 2.2f);
            Color gold = StarGold with { A = 0 };
            Vector2 rayOrigin = ray.Size() * 0.5f;
            sb.Draw(ray, drawPos, null, gold * (alpha * 0.85f), rot, rayOrigin
                , new Vector2(0.20f, 1.30f * pulseV) * scale, SpriteEffects.None, 0f);
            sb.Draw(ray, drawPos, null, gold * (alpha * 0.85f), rot + MathHelper.PiOver2, rayOrigin
                , new Vector2(0.20f, 1.30f * pulseH) * scale, SpriteEffects.None, 0f);
            //四芒星芯
            sb.Draw(cross, drawPos, null, gold * alpha, rot * 0.5f, cross.Size() * 0.5f
                , scale * 0.34f, SpriteEffects.None, 0f);
            //淡金微热芯，极小面积
            if (glow != null) {
                sb.Draw(glow, drawPos, null, (PaleGold with { A = 0 }) * (alpha * 0.9f), 0f
                    , glow.Size() * 0.5f, scale * 0.16f, SpriteEffects.None, 0f);
            }
        }

        /// <summary>星尾细条带</summary>
        public static void DrawRibbon(SpriteBatch sb, ReadOnlySpan<Vector2> pts, float headWidth, float alpha, float erode, float phase) {
            Texture2D tex = FishFallenStarAssets.RayTex?.Value;
            if (tex == null || pts.Length < 2 || alpha <= 0.01f) {
                return;
            }
            float k = 1f / tex.Width;
            Vector2 origin = tex.Size() * 0.5f;
            float cut = 1f - erode;
            Color indigo = NightIndigo with { A = 0 };
            Color pale = PaleGold with { A = 0 };
            for (int i = 1; i < pts.Length; i++) {
                float t = i / (float)(pts.Length - 1);
                //尾端先蚀，过蚀线的段淡出
                float fade = 1f;
                if (t > cut) {
                    fade = 1f - (t - cut) / 0.20f;
                    if (fade <= 0f) {
                        break;
                    }
                }
                Vector2 a = pts[i - 1];
                Vector2 b = pts[i];
                Vector2 seg = b - a;
                float len = seg.Length();
                if (len < 0.5f) {
                    continue;
                }
                float rot = seg.ToRotation() + MathHelper.PiOver2;
                Vector2 mid = (a + b) * 0.5f - Main.screenPosition;
                //星尾非稳定火焰
                float shimmer = 0.82f + 0.18f * MathF.Sin(i * 2.3f + phase);
                float headT = MathF.Pow(1f - t, 1.6f);
                float w = headWidth * MathF.Pow(1f - t, 0.75f);
                float segA = alpha * (0.22f + 0.78f * (1f - t)) * shimmer * fade;
                float lenScale = len * 1.3f / tex.Height;
                //靛蓝宽底
                sb.Draw(tex, mid, null, indigo * (segA * 0.9f), rot, origin
                    , new Vector2(w * 3.6f * k, lenScale), SpriteEffects.None, 0f);
                //深蓝→金渐变窄芯
                Color coreCol = Color.Lerp(DeepBlue, StarGold, headT) with { A = 0 };
                sb.Draw(tex, mid, null, coreCol * segA, rot, origin
                    , new Vector2(w * 1.6f * k, lenScale), SpriteEffects.None, 0f);
                //头段淡金热线，短驻留小面积
                if (t < 0.22f) {
                    sb.Draw(tex, mid, null, pale * (segA * 0.75f * (1f - t / 0.22f)), rot, origin
                        , new Vector2(w * 0.6f * k, lenScale), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>彗尾条带</summary>
        public static void DrawCometStrip(Projectile proj, float maxWidth, float fade) {
            if (Main.dedServ || fade <= 0.01f) {
                return;
            }
            Effect fx = FishFallenStarAssets.FishFallenStarComet;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }

            //采样点
            Vector2 half = proj.Size / 2f;
            Span<Vector2> pts = stackalloc Vector2[1 + proj.oldPos.Length];
            int count = 0;
            pts[count++] = proj.Center;
            for (int k = 0; k < proj.oldPos.Length; k++) {
                if (proj.oldPos[k] == Vector2.Zero) {
                    break;
                }
                Vector2 p = proj.oldPos[k] + half;
                if (Vector2.DistanceSquared(p, pts[count - 1]) < 4f) {
                    continue;
                }
                pts[count++] = p;
            }
            if (count < 3) {
                return;
            }

            //条带顶点
            var verts = new VertexPositionColorTexture[count * 2];
            for (int i = 0; i < count; i++) {
                float t = i / (float)(count - 1);
                Vector2 tangent = i < count - 1
                    ? (pts[i] - pts[i + 1]).SafeNormalize(Vector2.UnitX)
                    : (pts[i - 1] - pts[i]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float width = maxWidth * (0.5f + 0.5f * MathHelper.Clamp(t / 0.12f, 0f, 1f))
                    * MathF.Pow(1f - t, 0.8f);
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * width).ToVector3()
                    , Color.White, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * width).ToVector3()
                    , Color.White, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(proj.whoAmI * 0.61f % 1f);
            fx.Parameters["uFade"]?.SetValue(fade);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }

        /// <summary>快照弹体轨迹点生成独立残迹，星尾比弹体活得久</summary>
        public static void SpawnTrace(Projectile proj, float width, int life) {
            if (Main.dedServ) {
                return;
            }
            Span<Vector2> pts = stackalloc Vector2[1 + proj.oldPos.Length];
            int count = 0;
            pts[count++] = proj.Center;
            Vector2 half = proj.Size / 2f;
            for (int i = 0; i < proj.oldPos.Length; i++) {
                if (proj.oldPos[i] == Vector2.Zero) {
                    break;
                }
                Vector2 p = proj.oldPos[i] + half;
                if (Vector2.DistanceSquared(p, pts[count - 1]) < 4f) {
                    continue;
                }
                pts[count++] = p;
            }
            if (count < 3) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishFallenStarTrace>(pts[0], Vector2.Zero, Color.White, 1f)
                ?.Configure(pts[..count], width, life);
        }
    }

    /// <summary>星尾残迹</summary>
    internal class PRT_FishFallenStarTrace : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        private Vector2[] pts;
        private float headWidth;
        private float phase;

        public PRT_FishFallenStarTrace Configure(ReadOnlySpan<Vector2> points, float width, int lifetime) {
            pts = points.ToArray();
            headWidth = width;
            Lifetime = lifetime;
            phase = Main.rand.NextFloat(MathHelper.TwoPi);
            if (pts.Length > 0) {
                Position = pts[0];
            }
            return this;
        }

        public override void Reset() {
            base.Reset();
            pts = null;
            headWidth = 0f;
            phase = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Velocity = Vector2.Zero;
        }

        public override void AI() {
            Opacity = MathF.Pow(1f - LifetimeCompletion, 1.4f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (pts == null || pts.Length < 2) {
                return false;
            }
            FishFallenStarVFX.DrawRibbon(spriteBatch, pts, headWidth
                , Opacity * 0.85f, LifetimeCompletion * 1.08f, phase + Time * 0.35f);
            return false;
        }
    }
}
