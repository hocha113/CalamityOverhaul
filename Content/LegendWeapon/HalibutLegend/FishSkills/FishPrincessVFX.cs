using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>公主鱼域内 shader 资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishPrincessAssets
    {
        /// <summary>绘本符号弹体，心/星 SDF 平涂+描边+高光点</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishPrincessSymbol { get; private set; }

        /// <summary>缎带条带，弹尾拖带用，带尾蚀参数</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishPrincessRibbon { get; private set; }
    }

    /// <summary>公主鱼 VFX，粉彩三色 Blush/Lavender/Cream，描边 InkRose，禁荧光粉/彩虹/常驻纯白</summary>
    internal static class FishPrincessVFX
    {
        //==== 色彩脚本 ====
        /// <summary>粉（主色）</summary>
        public static readonly Color Blush = new(238, 158, 190);
        /// <summary>薰衣草（辅色）</summary>
        public static readonly Color Lavender = new(196, 173, 229);
        /// <summary>奶油金（点缀，代替白色高光）</summary>
        public static readonly Color Cream = new(246, 224, 172);
        /// <summary>深玫瑰描边墨色</summary>
        public static readonly Color InkRose = new(150, 72, 110);
        /// <summary>深紫灰暗托（暗外圈/尾段压制过曝）</summary>
        public static readonly Color DeepLilac = new(94, 74, 118);

        /// <summary>三色粉彩循环取色</summary>
        public static Color Pastel(int i) => (((i % 3) + 3) % 3) switch {
            0 => Blush,
            1 => Lavender,
            _ => Cream,
        };

        //==== 粒子族（星尘 / 圆点 / 闪光）====

        /// <summary>星尘 motes，小而慢的漂浮尘埃，微浮力+摇曳</summary>
        public static void Stardust(Vector2 pos, Vector2 baseVel, int count, float spread = 1.2f) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = baseVel + Main.rand.NextVector2Circular(spread, spread);
                PRTLoader.NewParticle<PRT_FishPrincessMote>(pos + Main.rand.NextVector2Circular(10f, 10f)
                    , vel, Pastel(Main.rand.Next(3)), Main.rand.NextFloat(0.7f, 1.15f))
                    ?.Configure(Main.rand.Next(42, 70));
            }
        }

        /// <summary>绘本圆点爆发，哑光粉彩圆点弹出后轻飘坠落</summary>
        public static void DotBurst(Vector2 pos, int count, float speed, int colorSeed = -1) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.25f, 0.25f);
                Vector2 vel = angle.ToRotationVector2() * speed * Main.rand.NextFloat(0.6f, 1f);
                int ci = colorSeed >= 0 ? colorSeed + i : Main.rand.Next(3);
                PRTLoader.NewParticle<PRT_FishPrincessDot>(pos, vel, Pastel(ci), Main.rand.NextFloat(0.8f, 1.3f))
                    ?.Configure(Main.rand.Next(18, 28));
            }
        }

        /// <summary>星形闪光，绘本"叮"的一下，小十字星尖闪</summary>
        public static void Glint(Vector2 pos, Vector2 vel, Color col, float scale) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_Sparkle>(pos, vel, col, scale * 0.6f)
                ?.Configure(col, Main.rand.Next(14, 22), Main.rand.NextFloat(0.05f, 0.12f), 0.35f);
        }

        //==== 缎带段绘制（贴图段式，SpriteBatch 内可用，供三明治分层）====

        /// <summary>
        /// 沿点链绘制哑光缎带，段式 Extra_98 软块拼接，沿长渐变 mid→edge
        /// 缎面流光段用 Cream 提色；根部收窄锚定宿主，尾端收梢
        /// </summary>
        public static void DrawRibbonSegments(SpriteBatch sb, ReadOnlySpan<Vector2> pts, int count
            , float baseWidth, Color mid, Color edge, float alpha, float sheenPhase) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || count < 3 || alpha <= 0.01f) {
                return;
            }
            float sheenPos = sheenPhase - MathF.Floor(sheenPhase);
            for (int i = 1; i < count; i++) {
                Vector2 a = pts[i - 1];
                Vector2 b = pts[i];
                Vector2 seg = b - a;
                float len = seg.Length();
                if (len < 0.5f) {
                    continue;
                }
                float u = i / (float)(count - 1);
                //根部收窄锚定 + 尾端收梢
                float width = baseWidth * MathF.Sin(MathF.Min(u * 2.2f, 1f) * MathHelper.PiOver2) * (1f - u * 0.55f);
                if (width < 0.4f) {
                    continue;
                }
                Color col = Color.Lerp(mid, edge, u);
                //缎面流光，沿带滑动的高光段
                float sheen = MathF.Exp(-MathF.Pow((u - sheenPos) * 5f, 2f));
                col = Color.Lerp(col, Cream, sheen * 0.45f);
                float segAlpha = alpha * (1f - u * 0.45f);
                Vector2 drawPos = (a + b) * 0.5f - Main.screenPosition;
                sb.Draw(tex, drawPos, null, col * segAlpha, seg.ToRotation()
                    , tex.Size() * 0.5f, new Vector2(len / tex.Width * 1.5f, width / tex.Height), SpriteEffects.None, 0);
            }
        }

        //==== 图元绘制（DrawPrimitives 内用，实体层顶点管线）====

        /// <summary>绘本符号四边形，世界坐标，shape 0 心 1 星，sigil>0 转描边符印</summary>
        public static void DrawSymbolQuad(Vector2 center, float rotation, float halfSize
            , int shape, Color fill, float pulse, float fade, float sigil = 0f) {
            Effect fx = FishPrincessAssets.FishPrincessSymbol;
            if (fx == null || fade <= 0.01f || halfSize < 1f) {
                return;
            }

            Vector2 axisX = rotation.ToRotationVector2() * halfSize;
            Vector2 axisY = new Vector2(-axisX.Y, axisX.X);
            var verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((center - axisX - axisY).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((center + axisX - axisY).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((center - axisX + axisY).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((center + axisX + axisY).ToVector3(), Color.White, new Vector2(1f, 1f));

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uShape"]?.SetValue((float)shape);
            fx.Parameters["uSigil"]?.SetValue(sigil);
            fx.Parameters["uFade"]?.SetValue(fade);
            fx.Parameters["uPulse"]?.SetValue(pulse);
            fx.Parameters["uColFill"]?.SetValue(fill.ToVector3());
            fx.Parameters["uColDeep"]?.SetValue(Color.Lerp(fill, DeepLilac, 0.38f).ToVector3());
            fx.Parameters["uColInk"]?.SetValue(InkRose.ToVector3());
            fx.Parameters["uColGloss"]?.SetValue(Cream.ToVector3());

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }

        /// <summary>缎带图元条带，世界坐标点链，头宽向尾收梢，erode 尾部先蚀</summary>
        public static void DrawRibbonStrip(ReadOnlySpan<Vector2> pts, int count
            , float headWidth, float seed, float fade, float erode = 0f) {
            Effect fx = FishPrincessAssets.FishPrincessRibbon;
            if (fx == null || count < 3 || fade <= 0.01f) {
                return;
            }

            var verts = new VertexPositionColorTexture[count * 2];
            for (int i = 0; i < count; i++) {
                float t = i / (float)(count - 1);
                Vector2 tangent = i < count - 1
                    ? (pts[i] - pts[i + 1]).SafeNormalize(Vector2.UnitX)
                    : (pts[i - 1] - pts[i]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                //头段快速铺满再向尾收成尖
                float width = headWidth * (0.5f + 0.5f * MathHelper.Clamp(t / 0.12f, 0f, 1f)) * MathF.Pow(1f - t, 0.8f);
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * width).ToVector3()
                    , Color.White, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * width).ToVector3()
                    , Color.White, new Vector2(t, 1f));
            }

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uFade"]?.SetValue(fade);
            fx.Parameters["uErode"]?.SetValue(erode);
            fx.Parameters["uColMid"]?.SetValue(Blush.ToVector3());
            fx.Parameters["uColEdge"]?.SetValue(Lavender.ToVector3());
            fx.Parameters["uColSheen"]?.SetValue(Cream.ToVector3());
            fx.Parameters["uColDark"]?.SetValue(DeepLilac.ToVector3());

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }
            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }

        //==== 数学 ====

        /// <summary>带过冲缓出（符印/入场的"弹入"曲线）</summary>
        public static float EaseOutBack(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }
    }
}
