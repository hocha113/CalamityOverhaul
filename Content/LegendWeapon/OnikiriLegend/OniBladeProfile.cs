using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>
    /// 鬼切贴图剖面:首帧对 OnikiriItem.png 作一次垂线扫描,建立刃缘/栋缘/中线/绯纹表。<br/>
    /// 改铭台陈列(<see cref="UI.OniMeiBladeDraw"/>)与在世刀身铭刻层
    /// (<see cref="Inscriptions.OniMeiBladeEngrave"/>)共用同一份剖面,不各量一遍
    /// </summary>
    internal static class OniBladeProfile
    {
        [VaultLoaden("CalamityOverhaul/Content/LegendWeapon/OnikiriLegend/OnikiriItem")]
        private static Asset<Texture2D> bladeTex = null;

        //====贴图轴锚(像素,量自 82x230 原图):锋尖(上右)→柄尾(下左)====
        public static readonly Vector2 SpriteTip = new(72.5f, 1f);
        public static readonly Vector2 SpritePommel = new(10f, 227f);
        /// <summary>贴图几何中心,陈列态的默认变换原点</summary>
        public static readonly Vector2 SpriteCenter = new(41f, 115f);
        /// <summary>贴图像素尺寸</summary>
        public static readonly Vector2 SpriteSize = new(82f, 230f);

        /// <summary>刀轴向(锋→柄尾)单位向量,贴图与屏幕同向(零旋转)</summary>
        public static Vector2 AxisDir => Vector2.Normalize(SpritePommel - SpriteTip);
        /// <summary>刀轴法向(+s 侧=栋/柄侧,屏幕左),引线自这一侧搭上来,刃侧留给流光</summary>
        public static Vector2 NormalDir { get { Vector2 d = AxisDir; return new Vector2(-d.Y, d.X); } }
        /// <summary>刀轴角(弧度)</summary>
        public static float AxisAngle => (SpritePommel - SpriteTip).ToRotation();
        /// <summary>刀上字形随刀轴立正的旋转(茎铭沿刀读)</summary>
        public static float GlyphRot => AxisAngle - MathHelper.PiOver2;

        public static bool Ready => bladeTex?.Value != null;
        public static Texture2D Texture => bladeTex?.Value;

        //====轮廓表:沿轴 u∈[0,1] 采样,+s = 栋/柄侧====
        private const int Samples = 48;
        private static bool profileBuilt;
        private static readonly float[] edgeOff = new float[Samples];   //刃缘(负侧)
        private static readonly float[] backOff = new float[Samples];   //栋/柄缘(正侧)
        private static readonly float[] centerOff = new float[Samples]; //剪影中线
        private static readonly float[] redGlow = new float[Samples];   //绯纹权重

        private static void EnsureProfile() {
            if (profileBuilt || !Ready) {
                return;
            }
            profileBuilt = true;
            //兜底剖面:采样失败时给一根 ±7px 的直刃
            for (int i = 0; i < Samples; i++) {
                edgeOff[i] = -7f;
                backOff[i] = 7f;
                centerOff[i] = 0f;
                redGlow[i] = 0f;
            }
            try {
                Texture2D tex = bladeTex.Value;
                Color[] data = new Color[tex.Width * tex.Height];
                tex.GetData(data);
                Vector2 axis = SpritePommel - SpriteTip;
                Vector2 normal = NormalDir;
                for (int i = 0; i < Samples; i++) {
                    float u = i / (Samples - 1f);
                    Vector2 p = SpriteTip + axis * u;
                    float sMin = float.MaxValue, sMax = float.MinValue, red = 0f;
                    for (float s = -70f; s <= 70f; s += 1f) {
                        Vector2 q = p + normal * s;
                        int x = (int)MathF.Round(q.X);
                        int y = (int)MathF.Round(q.Y);
                        if (x < 0 || y < 0 || x >= tex.Width || y >= tex.Height) {
                            continue;
                        }
                        Color c = data[y * tex.Width + x];
                        if (c.A <= 24) {
                            continue;
                        }
                        sMin = MathF.Min(sMin, s);
                        sMax = MathF.Max(sMax, s);
                        red = MathF.Max(red, (c.R - MathF.Max(c.G, c.B)) / 255f);
                    }
                    if (sMax >= sMin) {
                        edgeOff[i] = sMin;
                        backOff[i] = sMax;
                        centerOff[i] = (sMin + sMax) * 0.5f;
                        redGlow[i] = MathHelper.Clamp(red * 1.6f, 0f, 1f);
                    }
                }
            } catch {
                //GetData 偶发失败(设备丢失等)走兜底直刃,下帧不重试
            }
        }

        private static float Sample(float[] arr, float u) {
            EnsureProfile();
            float f = MathHelper.Clamp(u, 0f, 1f) * (Samples - 1);
            int i = Math.Min((int)f, Samples - 2);
            return MathHelper.Lerp(arr[i], arr[i + 1], f - i);
        }

        /// <summary>u 处绯纹权重 0~1,呼吸妖光的分布</summary>
        public static float RedGlow(float u) => Sample(redGlow, u);
        /// <summary>u 处剪影厚度(贴图 px)</summary>
        public static float Thickness(float u) => Sample(backOff, u) - Sample(edgeOff, u);

        /// <summary>u 处剪影中线点(贴图 px);铭位锚/刻痕落点</summary>
        public static Vector2 SpinePx(float u)
            => SpriteTip + (SpritePommel - SpriteTip) * u + NormalDir * Sample(centerOff, u);

        /// <summary>u 处刃缘点(贴图 px),standoff 向刃外让出;流光走这侧</summary>
        public static Vector2 EdgePx(float u, float standoff = 0f)
            => SpriteTip + (SpritePommel - SpriteTip) * u + NormalDir * (Sample(edgeOff, u) - standoff);

        /// <summary>u 处栋/柄缘点(贴图 px),standoff 向外让出;锚钉/引线搭点走这侧</summary>
        public static Vector2 BackPx(float u, float standoff = 0f)
            => SpriteTip + (SpritePommel - SpriteTip) * u + NormalDir * (Sample(backOff, u) + standoff);

        /// <summary>u 处刃缘切向角(弧度,贴图与屏幕同向)</summary>
        public static float EdgeTangent(float u) {
            const float D = 0.03f;
            return (EdgePx(u + D) - EdgePx(u - D)).ToRotation();
        }

        /// <summary>
        /// 贴图 px → 世界坐标的仿射,复刻两处 DrawBladeSprite 的支点数学
        /// (护手 UV 定原点、刀尖 UV 定轴角、朝左垂直翻转并镜像支点)。<br/>
        /// 翻转只镜像贴图采样不动四边形几何,故贴图点 (x,y) 落在局部 (x, H-y)
        /// </summary>
        internal readonly struct BladeXform
        {
            public readonly bool Valid;
            private readonly Vector2 anchor;
            private readonly Vector2 origin;
            private readonly float rotation;
            private readonly float scale;
            private readonly bool flipped;
            private readonly float texHeight;

            internal BladeXform(Vector2 handWorld, float bladeRotation, int facing, float bladeScale,
                bool edgeFlip, Vector2 texSize, Vector2 hiltUV, Vector2 tipUV) {
                Vector2 o = new(texSize.X * hiltUV.X, texSize.Y * hiltUV.Y);
                Vector2 tip = new(texSize.X * tipUV.X, texSize.Y * tipUV.Y);
                flipped = facing < 0 != edgeFlip;
                if (flipped) {
                    o.Y = texSize.Y - o.Y;
                    tip.Y = texSize.Y - tip.Y;
                }
                anchor = handWorld;
                origin = o;
                rotation = bladeRotation - (tip - o).ToRotation();
                scale = bladeScale;
                texHeight = texSize.Y;
                Valid = true;
            }

            /// <summary>贴图 px → 世界点</summary>
            public Vector2 Map(Vector2 texPx) {
                Vector2 local = flipped ? new Vector2(texPx.X, texHeight - texPx.Y) : texPx;
                return anchor + ((local - origin) * scale).RotatedBy(rotation);
            }

            /// <summary>贴图内方向 → 世界方向(只过翻转与旋转,不过平移)</summary>
            public Vector2 MapDir(Vector2 texDir) {
                Vector2 d = flipped ? new Vector2(texDir.X, -texDir.Y) : texDir;
                return d.RotatedBy(rotation);
            }

            /// <summary>贴图 px 长度 → 世界长度</summary>
            public float MapLength(float texLength) => texLength * scale;

            /// <summary>贴图内角度 → 世界角度</summary>
            public float MapAngle(float texAngle) => MapDir(texAngle.ToRotationVector2()).ToRotation();
        }

        /// <summary>按世界刀身绘制参数建变换;贴图未就绪时 <see cref="BladeXform.Valid"/> 为假</summary>
        public static BladeXform BuildXform(Vector2 handWorld, float bladeRotation, int facing,
            float bladeScale, bool edgeFlip, Vector2 hiltUV, Vector2 tipUV) {
            Texture2D tex = Texture;
            return tex == null
                ? default
                : new BladeXform(handWorld, bladeRotation, facing, bladeScale, edgeFlip,
                    tex.Size(), hiltUV, tipUV);
        }
    }
}
