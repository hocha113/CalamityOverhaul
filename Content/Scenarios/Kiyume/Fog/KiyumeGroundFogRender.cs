using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Fog
{
    /// <summary>
    /// 贴地残雾带与瀑布雾：逐列探地建一条地形跟随三角带（管线移植自
    /// <c>KikasaDreams/KikasaDreamFogRender.cs</c>），KiyumeGroundFog.fx 以连续噪声场着雾。<br/>
    /// 为鬼梦潮汐改造：逐列露出度门控（涨潮时雾海漫上来把这层吞没，退潮后它是退不掉的地气）、
    /// 逐列 <see cref="KiyumeFogTheme"/> 带色 + 采光烬染（继承雾海吃光语言）、
    /// 驱散走 <see cref="KiyumeFogSuppression.Evaluate"/>（一套驱散真相，推雾/回聚不对称免费继承）。<br/>
    /// 相邻列落差超阈值处挂缓速雾帘（瀑布雾），帘底散入贴地层。<br/>
    /// 由 <see cref="KiyumeFogSystem.PostDrawTiles"/> 在近带雾海之后调用；着色器缺失整层静默不画
    /// （增量层，雾海的 CPU 回退兜底世界不黑）。纯客户端表现，零同步包
    /// </summary>
    internal static class KiyumeGroundFogRender
    {
        /// <summary>探地列距（世界 px）</summary>
        private const float ColumnStep = 32f;
        /// <summary>列数上限（zoom1 下 4K 有余量；极限缩放超容量时跨度居中截断，见 Draw）</summary>
        private const int MaxColumns = 160;
        /// <summary>相邻列高度差钳制（px/列），雾爬坡不跳崖</summary>
        private const float MaxSlope = 30f;
        /// <summary>子世界探地：地板先验上方起探的行数（村屋房顶在地板线上方，要爬得到）</summary>
        private const int PlanProbeUpRows = 14;
        /// <summary>子世界先验探程（行）</summary>
        private const int PlanProbeRows = 20;
        /// <summary>盲探探程（行，主世界看样/先验落空回退，Kikasa 同值）</summary>
        private const int BlindProbeRows = 46;
        /// <summary>瀑带宽（px）</summary>
        private const float FallWidthPx = 64f;
        /// <summary>瀑带底部裙长（px），散入贴地层的画布</summary>
        private const float FallSkirtPx = 64f;
        /// <summary>每道瀑纵向细分段数</summary>
        private const int FallSegments = 8;
        /// <summary>瀑口检测去重最小列距</summary>
        private const int FallMinGapCols = 2;
        /// <summary>雾帘视觉流速（px/s）：雾不是水</summary>
        private const float FallVisualSpeed = 30f;

        //烬色与 KiyumeFogSim.EmberTint 同源（那边 private，改一处必改两处）
        private static readonly Vector3 EmberTint = new(0.95f, 0.34f, 0.14f);

        private struct FallInfo
        {
            internal float X;      //瀑口中心（世界px）
            internal float TopY;   //瀑口（上层地面）
            internal float Drop;   //落差 px
            internal Vector3 Tint; //落点列雾色
            internal float Alpha;  //断崖×露出×抑制复合
        }

        //探地/平滑/断崖/附加数据缓冲逐帧复用，零分配
        private static readonly float[] heights = new float[MaxColumns];
        private static readonly float[] smoothed = new float[MaxColumns];
        private static readonly float[] gaps = new float[MaxColumns];
        private static readonly float[] gapsBlur = new float[MaxColumns];
        private static readonly Vector3[] colTint = new Vector3[MaxColumns];
        private static readonly float[] colExpose = new float[MaxColumns];
        private static readonly float[] colSup = new float[MaxColumns];
        private static readonly VertexPositionColorTexture[] verts = new VertexPositionColorTexture[MaxColumns * 2];
        private static readonly VertexPositionColorTexture[] fallVerts = new VertexPositionColorTexture[(FallSegments + 1) * 2];
        private static readonly FallInfo[] falls = new FallInfo[8];
        private static int fallCount;

        internal static void Clear() => fallCount = 0;

        internal static void Draw(SpriteBatch spriteBatch, float presence) {
            if (presence < 0.01f) {
                return;
            }
            Player viewer = Main.LocalPlayer;
            if (viewer?.active != true) {
                return;
            }
            Effect fx = EffectLoader.KiyumeGroundFog?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null || noise.IsDisposed) {
                //增量层：着色器缺失即无，底下有雾海回退兜底，不做第二套回退
                return;
            }
            float alphaMul = KiyumeFogDebug.GroundFogAlpha * presence;
            if (alphaMul <= 0.003f) {
                return;
            }

            //带跨度：可见世界宽 ±80（考虑镜头缩放，缩得再远列数也被 MaxColumns 硬钳）
            float invZx = 1f / MathHelper.Max(Main.GameViewMatrix.Zoom.X, 0.05f);
            float viewW = Main.screenWidth * invZx;
            float centerX = Main.screenPosition.X + Main.screenWidth * 0.5f;
            float left = centerX - viewW * 0.5f - 80f;
            int cols = (int)((viewW + 160f) / ColumnStep) + 2;
            if (cols < 2) {
                return;
            }
            if (cols > MaxColumns) {
                //超容量（4K+极限缩放）：跨度以屏心居中截断，两缘对称让出
                //（同 KiyumeFogSim 超窗口钳制语义；原左锚截断会把右半屏整段裸出）
                cols = MaxColumns;
                left = centerX - cols * ColumnStep * 0.5f;
            }

            SampleGround(viewer, left, cols);
            BuildColumnData(left, cols);
            DetectFalls(left, cols);
            BuildBandVerts(left, cols);

            //PostDrawTiles 主批已收，这里只动设备态并画完还原（Kikasa 同法）；
            //顶点是世界坐标，交给 GetTransfromMatrix，严禁再减 screenPosition
            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            //风相向东：雾从湖里上岸往村里渗；MacroSeed 定相，同存档同相
            fx.Parameters["uWind"]?.SetValue(KiyumeFogDebug.GroundWindPxPerSec);
            fx.Parameters["uSeed"]?.SetValue((float)(KiyumeMetrics.MacroSeed & 1023));
            fx.Parameters["uAlpha"]?.SetValue(alphaMul);

            fx.CurrentTechnique = fx.Techniques["TechGroundBand"];
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, cols * 2 - 2);
            }

            if (fallCount > 0) {
                fx.CurrentTechnique = fx.Techniques["TechFogFall"];
                for (int f = 0; f < fallCount; f++) {
                    DrawOneFall(device, fx, in falls[f]);
                }
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
            //归还噪声槽：同帧邻居泄漏纪律
            device.Textures[1] = null;
        }

        //=== 探地 ===

        /// <summary>
        /// 逐列探地 + 3 点平均 + 双向斜率钳制 + 断崖羽化（链条照搬 Kikasa）。<br/>
        /// 子世界从 <see cref="KiyumePlans.FloorTopAt"/> 先验起探（联机客户端 FloorTop 数组无值时
        /// 回退基准曲线，建筑偏离曲线会探空）→ 落空走盲探兜底；主世界看样只有盲探
        /// </summary>
        private static void SampleGround(Player viewer, float left, int cols) {
            bool inKiyume = KiyumeWorld.Active;
            int blindFromRow = (int)((viewer.Center.Y - 60f) / 16f);
            float carry = viewer.Bottom.Y;
            for (int i = 0; i < cols; i++) {
                float x = left + i * ColumnStep;
                int tileX = (int)(x / 16f);
                bool found = false;
                float groundY = 0f;
                if (inKiyume) {
                    found = ProbeColumn(tileX, KiyumePlans.FloorTopAt(tileX) - PlanProbeUpRows,
                        PlanProbeRows, out groundY);
                }
                if (!found) {
                    found = ProbeColumn(tileX, blindFromRow, BlindProbeRows, out groundY);
                }
                if (found) {
                    heights[i] = groundY;
                    gaps[i] = 1f;
                    carry = groundY;
                }
                else {
                    //高度沿用最近有效列，可见性交给 gap 归零
                    heights[i] = carry;
                    gaps[i] = 0f;
                }
            }

            //3 点平均
            for (int i = 0; i < cols; i++) {
                float sum = heights[i];
                float weight = 1f;
                if (i > 0) {
                    sum += heights[i - 1];
                    weight += 1f;
                }
                if (i < cols - 1) {
                    sum += heights[i + 1];
                    weight += 1f;
                }
                smoothed[i] = sum / weight;
            }
            //双向斜率钳制：先左后右各一遍，坡度封在 MaxSlope/列 内（瀑口检测用的是钳前 heights）
            for (int i = 1; i < cols; i++) {
                smoothed[i] = MathHelper.Clamp(smoothed[i],
                    smoothed[i - 1] - MaxSlope, smoothed[i - 1] + MaxSlope);
            }
            for (int i = cols - 2; i >= 0; i--) {
                smoothed[i] = MathHelper.Clamp(smoothed[i],
                    smoothed[i + 1] - MaxSlope, smoothed[i + 1] + MaxSlope);
            }
            //断崖羽化两遍，雾在崖口渐没而非平切
            for (int pass = 0; pass < 2; pass++) {
                for (int i = 0; i < cols; i++) {
                    float sum = gaps[i] * 2f;
                    float weight = 2f;
                    if (i > 0) {
                        sum += gaps[i - 1];
                        weight += 1f;
                    }
                    if (i < cols - 1) {
                        sum += gaps[i + 1];
                        weight += 1f;
                    }
                    gapsBlur[i] = sum / weight;
                }
                Array.Copy(gapsBlur, gaps, cols);
            }
        }

        //探地语义镜像 KiyumeHoundShade.TryFindGround：实心且非平台顶，雾不站平台
        private static bool ProbeColumn(int tileX, int fromRow, int maxRows, out float groundY) {
            for (int i = 0; i < maxRows; i++) {
                int y = fromRow + i;
                if (!WorldGen.InWorld(tileX, y, 20)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    groundY = y * 16f;
                    return true;
                }
            }
            groundY = 0f;
            return false;
        }

        //=== 逐列附加数据：潮汐露出度 / 带色采光染 / 抑制因子 ===

        private static void BuildColumnData(float left, int cols) {
            float exposeSpan = MathHelper.Max(KiyumeFogDebug.GroundFogExposeSpanPx, 1f);
            float visFloor = MathHelper.Clamp(KiyumeFogDebug.LightVisFloor, 0f, 1f);
            float tintMax = MathHelper.Clamp(KiyumeFogDebug.LightTintStrength, 0f, 1f);
            bool anySuppress = KiyumeFogSuppression.AnyActive;
            for (int i = 0; i < cols; i++) {
                float x = left + i * ColumnStep;
                float ground = smoothed[i];
                //潮汐门控：地面露出雾线才显形，涨潮时被雾海接管吞没（y 向下为正）
                colExpose[i] = MathHelper.Clamp(
                    (KiyumeFogTide.SurfaceAt(x) - ground) / exposeSpan, 0f, 1f);
                //列底中心的抑制因子（32px 列距比 64px 雾元还细，推雾回聚不对称免费继承）
                colSup[i] = anySuppress
                    ? KiyumeFogSuppression.Evaluate(new Vector2(x, ground))
                    : 1f;
                //采光烬染：与 KiyumeFogSim.Upload 同式（贴地雾在窗火边同样被烘暖）
                int tileX = (int)(x / 16f);
                int tileY = Math.Clamp((int)(ground / 16f) - 2, 0, Main.maxTilesY - 1);
                float lit = MathHelper.Clamp(Lighting.Brightness(tileX, tileY) / 0.42f, 0f, 1f);
                lit *= lit;
                KiyumeFogTheme.Sample(x / 16f, out Vector3 themeCol, out _);
                Vector3 c = Vector3.Lerp(themeCol, EmberTint, lit * tintMax);
                colTint[i] = c * (visFloor + (1f - visFloor) * lit);
            }
        }

        //=== 瀑口检测：相邻列钳前落差 ≥ 阈值判瀑口，按落差降序取前 FogFallMax 道 ===

        private static void DetectFalls(float left, int cols) {
            fallCount = 0;
            int maxFalls = Math.Clamp(KiyumeFogDebug.FogFallMax, 0, falls.Length);
            float threshold = MathHelper.Max(KiyumeFogDebug.FogFallThresholdPx, 16f);
            if (maxFalls <= 0) {
                return;
            }
            //小候选集选择排序：每轮挑剩余最大落差，去重最小列距
            Span<int> taken = stackalloc int[falls.Length];
            for (int round = 0; round < maxFalls; round++) {
                int best = -1;
                float bestDrop = threshold;
                for (int i = 0; i < cols - 1; i++) {
                    if (gaps[i] < 0.5f || gaps[i + 1] < 0.5f) {
                        continue;
                    }
                    float drop = MathF.Abs(heights[i + 1] - heights[i]);
                    if (drop < bestDrop) {
                        continue;
                    }
                    bool near = false;
                    for (int t = 0; t < fallCount; t++) {
                        if (Math.Abs(taken[t] - i) <= FallMinGapCols) {
                            near = true;
                            break;
                        }
                    }
                    if (near) {
                        continue;
                    }
                    best = i;
                    bestDrop = drop;
                }
                if (best < 0) {
                    break;
                }
                taken[fallCount] = best;
                float lipX = left + best * ColumnStep + ColumnStep * 0.5f;
                float topY = MathF.Min(heights[best], heights[best + 1]);
                int lowIdx = heights[best + 1] > heights[best] ? best + 1 : best;
                //露出度取瀑口（雾线漫过崖顶就没有帘了），抑制/色取落点列
                float expose = MathHelper.Clamp(
                    (KiyumeFogTide.SurfaceAt(lipX) - topY) / MathHelper.Max(KiyumeFogDebug.GroundFogExposeSpanPx, 1f), 0f, 1f);
                falls[fallCount] = new FallInfo {
                    X = lipX,
                    TopY = topY,
                    Drop = bestDrop,
                    Tint = colTint[lowIdx],
                    Alpha = MathHelper.Min(gaps[best], gaps[best + 1]) * expose * colSup[lowIdx],
                };
                fallCount++;
            }
        }

        //=== 顶点构建 ===

        //顶点契约与 KiyumeGroundFog.fx 对齐：POSITION=世界坐标，
        //TEXCOORD0.y=带内高度01（顶=1 裙底=0），TEXCOORD0.x=抑制因子（孔缘微堆亮用），
        //COLOR0.rgb=染色后雾色，COLOR0.a=断崖×露出×抑制复合渐隐
        private static void BuildBandVerts(float left, int cols) {
            float bandH = KiyumeFogDebug.GroundFogHeightPx;
            float skirt = KiyumeFogDebug.GroundFogSkirtPx;
            for (int i = 0; i < cols; i++) {
                float x = left + i * ColumnStep;
                float ground = smoothed[i];
                float sup = colSup[i];
                float a = MathHelper.Clamp(gaps[i], 0f, 1f) * colExpose[i] * sup;
                var data = new Color(colTint[i].X, colTint[i].Y, colTint[i].Z, a);
                verts[i * 2] = new VertexPositionColorTexture(
                    new Vector3(x, ground - bandH, 0f), data, new Vector2(sup, 1f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(
                    new Vector3(x, ground + skirt, 0f), data, new Vector2(sup, 0f));
            }
        }

        //每道瀑单独上载长度类 uniform 再画：不同高度的帘共享一套 sqrt 域流速折算
        private static void DrawOneFall(GraphicsDevice device, Effect fx, in FallInfo fall) {
            float len = fall.Drop + FallSkirtPx;
            var data = new Color(fall.Tint.X, fall.Tint.Y, fall.Tint.Z, fall.Alpha);
            float xL = fall.X - FallWidthPx * 0.5f;
            float xR = fall.X + FallWidthPx * 0.5f;
            for (int r = 0; r <= FallSegments; r++) {
                float v = r / (float)FallSegments;
                float y = fall.TopY + v * len;
                fallVerts[r * 2] = new VertexPositionColorTexture(
                    new Vector3(xL, y, 0f), data, new Vector2(0f, v));
                fallVerts[r * 2 + 1] = new VertexPositionColorTexture(
                    new Vector3(xR, y, 0f), data, new Vector2(1f, v));
            }
            fx.Parameters["uFallLen"]?.SetValue(len);
            fx.Parameters["uFallDrop"]?.SetValue(fall.Drop);
            //sqrt 域滚速：让帘中段视觉流速 ≈ FallVisualSpeed（shader 纵坐标 = sqrt(px)*0.0635）
            float midSqrt = MathF.Sqrt(MathHelper.Max(fall.Drop * 0.5f, 16f));
            fx.Parameters["uFallFlow"]?.SetValue(0.0635f * FallVisualSpeed / (2f * midSqrt));
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, fallVerts, 0, FallSegments * 2);
            }
        }
    }
}
