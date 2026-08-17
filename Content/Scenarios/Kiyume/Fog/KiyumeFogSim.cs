using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Fog
{
    /// <summary>
    /// 鬼梦湖雾密度场：世界锚定的粗网格滚动窗口（1雾元=4×4tile=64px），
    /// 每 2 tick 一步：按雾线求目标→驱散/回聚（时间不对称）→上传密度纹理（rgb=雾色, a=密度）。<br/>
    /// 工程骨架照搬深牢迷雾，唯一换掉的是那条目标公式：<br/>
    /// 深牢 <c>baseDensity(深度曲线) × (1 - 亮度×驱散)</c>，
    /// 这里 <c>tideFill(雾线Y - 世界Y) × 离湖衰减(x)</c>——雾是有水位的液体，不是随深度变浓的空气。<br/>
    /// 数组/纹理/上传缓冲全部持久复用，零逐帧分配；纯客户端，服务器不进
    /// </summary>
    internal static class KiyumeFogSim
    {
        /// <summary>1 雾元边长（世界px）=4 tile</summary>
        internal const int CellPx = 64;
        /// <summary>窗口四周边距雾元数（192px，入屏前预模拟）</summary>
        internal const int MarginCells = 3;
        /// <summary>模拟步进间隔（tick）</summary>
        internal const int SimIntervalTicks = 2;
        /// <summary>窗口容量（4K@zoom1+边距仍富余；超界钳制并一次性日志）</summary>
        internal const int CapW = 96;
        internal const int CapH = 64;

        //===雾线填充曲线（DESIGN §3.2）===
        /// <summary>雾面处的浓度</summary>
        internal const float SurfaceDensity = 0.42f;
        /// <summary>深处的浓度</summary>
        internal const float DeepDensity = 0.95f;
        /// <summary>沉多深到满浓度（px）</summary>
        internal const float SubmergeDepthPx = 640f;
        /// <summary>雾线以上多快衰减到零（px）</summary>
        internal const float AirFalloffPx = 176f;

        //烬色：亮处的雾被窗火烘暖，不是被照穿
        private static readonly Vector3 EmberTint = new(0.95f, 0.34f, 0.14f);

        //密度当前值（-1=哨兵：新入窗，当步以稳态落地）；delay=回聚延迟余量（tick）
        private static readonly float[] density = new float[CapW * CapH];
        private static readonly float[] densityScratch = new float[CapW * CapH];
        private static readonly ushort[] delay = new ushort[CapW * CapH];
        private static readonly ushort[] delayScratch = new ushort[CapW * CapH];
        //本步采到的亮度（扩散后上传时算染色用）
        private static readonly float[] lightBuf = new float[CapW * CapH];
        private static readonly float[] lightScratch = new float[CapW * CapH];
        private static readonly Color[] upload = new Color[CapW * CapH];
        //逐列缓存：雾面 Y / 浓度系数 / 雾色 / 蒸腾闸门，每步算一次给整列复用
        private static readonly float[] colSurface = new float[CapW];
        private static readonly float[] colFactor = new float[CapW];
        private static readonly Vector3[] colColor = new Vector3[CapW];
        private static readonly float[] colSteamGate = new float[CapW];

        private static Texture2D texture;
        private static Point originCell = new(int.MinValue, int.MinValue);
        private static int winW;
        private static int winH;
        private static int tickCounter;
        private static bool ready;
        private static bool clampLogged;

        /// <summary>密度窗口纹理（容量尺寸，左上子矩形有效）</summary>
        internal static Texture2D Texture => texture;
        /// <summary>首步模拟+上传已完成，渲染方可消费</summary>
        internal static bool Ready => ready && texture != null && !texture.IsDisposed;
        internal static Point OriginCell => originCell;
        internal static int WindowW => winW;
        internal static int WindowH => winH;

        /// <summary>
        /// 公开只读采样：世界px处当前雾密度 0~1（窗口外/未就绪回退解析式）。<br/>
        /// 玩法扩展位的接入口——真去消费它之前，潮汐时钟得先联机同步
        /// </summary>
        public static float DensityAt(Vector2 worldPx) {
            int cx = (int)MathF.Floor(worldPx.X / CellPx) - originCell.X;
            int cy = (int)MathF.Floor(worldPx.Y / CellPx) - originCell.Y;
            if (ready && cx >= 0 && cx < winW && cy >= 0 && cy < winH) {
                float d = density[cy * winW + cx];
                if (d >= 0f) {
                    return MathHelper.Clamp(d, 0f, 1f);
                }
            }
            return TargetAt(worldPx.X, worldPx.Y);
        }

        /// <summary>解析目标浓度：max(雾线填充 × 离湖衰减 × 带表倍率, 贴水蒸腾) × 全局倍率</summary>
        internal static float TargetAt(float worldX, float worldY) {
            KiyumeFogTheme.Sample(worldX / 16f, out _, out float mul);
            float raw = TideFill(KiyumeFogTide.SurfaceAt(worldX) - worldY)
                * LakeFalloff(worldX) * mul;
            if (KiyumeWorld.Active) {
                float steamGate = MathHelper.Clamp(
                    (KiyumeMetrics.WaterRightPx - worldX) / KiyumeMetrics.SteamFadeSpanPx, 0f, 1f);
                if (steamGate > 0f) {
                    raw = MathHelper.Max(raw, SteamFill(worldY) * steamGate);
                }
            }
            return MathHelper.Clamp(raw * MathHelper.Max(KiyumeFogDebug.DensityMul, 0f), 0f, 1f);
        }

        /// <summary>雾线填充：线下按沉深递增趋近满，线上快速衰减到零</summary>
        internal static float TideFill(float depthPx) {
            if (depthPx >= 0f) {
                float k = MathHelper.Clamp(depthPx / SubmergeDepthPx, 0f, 1f);
                //缓出：近表面梯度陡，雾面才有形；深处收敛
                k = k * (2f - k);
                return MathHelper.Lerp(SurfaceDensity, DeepDensity, k);
            }
            return SurfaceDensity * MathHelper.Clamp(1f + depthPx / AirFalloffPx, 0f, 1f);
        }

        /// <summary>贴水蒸腾：水下满值，水上二次衰减——雾底永远锚在湖面，退潮也不悬空</summary>
        internal static float SteamFill(float worldY) {
            float above = KiyumeMetrics.LakeWaterYPx - worldY;
            if (above <= 0f) {
                return KiyumeMetrics.SteamBaseDensity;
            }
            float k = 1f - MathHelper.Clamp(above / KiyumeMetrics.SteamHeightPx, 0f, 1f);
            return KiyumeMetrics.SteamBaseDensity * k * k;
        }

        /// <summary>离湖衰减：湖边满浓，远山那头只剩 FarFogMul</summary>
        internal static float LakeFalloff(float worldX) {
            float t = MathHelper.Clamp(
                (worldX - KiyumeMetrics.LakeRightPx) / KiyumeMetrics.FalloffSpanPx, 0f, 1f);
            return MathHelper.Lerp(1f, KiyumeMetrics.FarFogMul, t * t * (3f - 2f * t));
        }

        /// <summary>世界切换硬复位：窗口哨兵化，纹理保留复用</summary>
        internal static void Reset() {
            originCell = new Point(int.MinValue, int.MinValue);
            winW = winH = 0;
            ready = false;
        }

        /// <summary>模组卸载：释放纹理（主线程队列，防非主线程 Dispose）</summary>
        internal static void Unload() {
            Reset();
            Texture2D tex = texture;
            texture = null;
            if (tex != null && !tex.IsDisposed) {
                Main.QueueMainThreadAction(tex.Dispose);
            }
        }

        /// <summary>每 tick 调一次；内部按 SimIntervalTicks 分频</summary>
        internal static void Tick() {
            tickCounter++;
            if (tickCounter % SimIntervalTicks != 0) {
                return;
            }
            UpdateWindow();
            if (winW <= 0 || winH <= 0) {
                return;
            }
            CacheColumns();
            Step();
            SpreadLight();
            Upload();
        }

        //=== 窗口滚动 ===

        private static void UpdateWindow() {
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            float invZx = 1f / MathHelper.Max(zoom.X, 0.05f);
            float invZy = 1f / MathHelper.Max(zoom.Y, 0.05f);
            //可见世界区（vanilla ScreenShaderData.Apply 同式）
            var visibleSize = new Vector2(Main.screenWidth * invZx, Main.screenHeight * invZy);
            Vector2 visibleTL = Main.screenPosition
                + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f * (Vector2.One - new Vector2(invZx, invZy));

            int newW = (int)MathF.Ceiling(visibleSize.X / CellPx) + MarginCells * 2 + 1;
            int newH = (int)MathF.Ceiling(visibleSize.Y / CellPx) + MarginCells * 2 + 1;
            bool clamped = newW > CapW || newH > CapH;
            newW = Math.Min(newW, CapW);
            newH = Math.Min(newH, CapH);
            if (clamped && !clampLogged) {
                clampLogged = true;
                CWRMod.Instance.Logger.Warn($"[KiyumeFog] 视野超窗口容量{CapW}x{CapH},雾窗口钳制到屏幕中心");
            }

            Point newOrigin;
            if (clamped) {
                //超容量：以可见区中心居中
                Vector2 center = visibleTL + visibleSize * 0.5f;
                newOrigin = new Point(
                    (int)MathF.Floor(center.X / CellPx) - newW / 2,
                    (int)MathF.Floor(center.Y / CellPx) - newH / 2);
            }
            else {
                newOrigin = new Point(
                    (int)MathF.Floor(visibleTL.X / CellPx) - MarginCells,
                    (int)MathF.Floor(visibleTL.Y / CellPx) - MarginCells);
            }

            if (newOrigin == originCell && newW == winW && newH == winH) {
                return;
            }
            ShiftInto(newOrigin, newW, newH);
            originCell = newOrigin;
            winW = newW;
            winH = newH;
        }

        //窗口平移：重叠区搬运（密度有世界"住址"），新入窗置哨兵
        private static void ShiftInto(Point newOrigin, int newW, int newH) {
            bool fresh = winW <= 0 || winH <= 0 || originCell.X == int.MinValue;
            int dx = fresh ? 0 : newOrigin.X - originCell.X;
            int dy = fresh ? 0 : newOrigin.Y - originCell.Y;
            for (int y = 0; y < newH; y++) {
                int oy = y + dy;
                bool rowOk = !fresh && oy >= 0 && oy < winH;
                int newRow = y * newW;
                int oldRow = oy * winW;
                for (int x = 0; x < newW; x++) {
                    int ox = x + dx;
                    int ni = newRow + x;
                    if (rowOk && ox >= 0 && ox < winW) {
                        densityScratch[ni] = density[oldRow + ox];
                        delayScratch[ni] = delay[oldRow + ox];
                    }
                    else {
                        densityScratch[ni] = -1f;
                        delayScratch[ni] = 0;
                    }
                }
            }
            int count = newW * newH;
            Array.Copy(densityScratch, density, count);
            Array.Copy(delayScratch, delay, count);
        }

        //=== 逐列缓存 ===

        //浓度沿 x 只变一次（雾面高度、离湖衰减、带表色、蒸腾闸门），整列复用免得在内圈重算
        private static void CacheColumns() {
            float densityMul = MathHelper.Max(KiyumeFogDebug.DensityMul, 0f);
            //主世界看样没有血湖，蒸腾只在子世界里存在
            bool inKiyume = KiyumeWorld.Active;
            for (int x = 0; x < winW; x++) {
                float worldX = (originCell.X + x + 0.5f) * CellPx;
                colSurface[x] = KiyumeFogTide.SurfaceAt(worldX);
                KiyumeFogTheme.Sample(worldX / 16f, out Vector3 color, out float mul);
                colColor[x] = color;
                colFactor[x] = LakeFalloff(worldX) * mul * densityMul;
                colSteamGate[x] = inKiyume ? MathHelper.Clamp(
                    (KiyumeMetrics.WaterRightPx - worldX) / KiyumeMetrics.SteamFadeSpanPx, 0f, 1f) * densityMul : 0f;
            }
        }

        //=== 模拟步 ===

        private static void Step() {
            float dispelStep = StepFactor(KiyumeFogDebug.DispelHalfLifeTicks);
            float regatherStep = StepFactor(KiyumeFogDebug.RegatherHalfLifeTicks);
            ushort regatherDelay = (ushort)MathHelper.Clamp(KiyumeFogDebug.RegatherDelayTicks, 0f, 60000f);
            bool anySuppress = KiyumeFogSuppression.AnyActive;

            for (int y = 0; y < winH; y++) {
                int rowBase = y * winW;
                float worldY = (originCell.Y + y + 0.5f) * CellPx;
                int tileY = Math.Clamp((originCell.Y + y) * 4 + 2, 0, Main.maxTilesY - 1);

                for (int x = 0; x < winW; x++) {
                    int i = rowBase + x;
                    int tileX = Math.Clamp((originCell.X + x) * 4 + 2, 0, Main.maxTilesX - 1);
                    //亮度只用来染色，不参与驱散——梦里的光穿不过雾
                    lightBuf[i] = Lighting.Brightness(tileX, tileY);

                    float target = TideFill(colSurface[x] - worldY) * colFactor[x];
                    //湖区贴水蒸腾与雾线取大：退潮时湖上雾墙仍锚在水面
                    if (colSteamGate[x] > 0f) {
                        target = MathHelper.Max(target, SteamFill(worldY) * colSteamGate[x]);
                    }
                    target = MathHelper.Clamp(target, 0f, 1f);
                    if (anySuppress) {
                        target *= KiyumeFogSuppression.Evaluate(
                            new Vector2((originCell.X + x + 0.5f) * CellPx, worldY));
                    }

                    float cur = density[i];
                    if (cur < 0f) {
                        //新入窗雾元：以稳态落地（屏外的雾早已随潮位就位）
                        density[i] = target;
                        delay[i] = 0;
                        continue;
                    }
                    if (target < cur) {
                        //驱散：快速率，且每次驱散都重置回聚延迟
                        density[i] = cur + (target - cur) * dispelStep;
                        delay[i] = regatherDelay;
                    }
                    else if (target > cur) {
                        //回聚：先耗完延迟再慢速率合拢
                        if (delay[i] > SimIntervalTicks) {
                            delay[i] = (ushort)(delay[i] - SimIntervalTicks);
                        }
                        else {
                            delay[i] = 0;
                            density[i] = cur + (target - cur) * regatherStep;
                        }
                    }
                }
            }
        }

        //半衰期→单步插值系数
        private static float StepFactor(float halfLifeTicks) {
            halfLifeTicks = MathHelper.Max(halfLifeTicks, 0.5f);
            return 1f - MathF.Pow(2f, -SimIntervalTicks / halfLifeTicks);
        }

        //=== 光晕扩散 ===

        //两遍衰减膨胀（3×3 取邻域加权最大）：亮度向外漫开两雾元（~128px）而不稀释峰值，
        //窗火/火把在雾里成为一团体积暖光——这是"雾吃光"看得见的那一半
        private static void SpreadLight() {
            int count = winW * winH;
            for (int pass = 0; pass < 2; pass++) {
                for (int y = 0; y < winH; y++) {
                    int y0 = Math.Max(y - 1, 0);
                    int y1 = Math.Min(y + 1, winH - 1);
                    int rowBase = y * winW;
                    for (int x = 0; x < winW; x++) {
                        int x0 = Math.Max(x - 1, 0);
                        int x1 = Math.Min(x + 1, winW - 1);
                        float best = 0f;
                        for (int yy = y0; yy <= y1; yy++) {
                            int row = yy * winW;
                            float wy = yy == y ? 1f : 0.66f;
                            for (int xx = x0; xx <= x1; xx++) {
                                float v = lightBuf[row + xx] * wy * (xx == x ? 1f : 0.66f);
                                if (v > best) {
                                    best = v;
                                }
                            }
                        }
                        lightScratch[rowBase + x] = best;
                    }
                }
                Array.Copy(lightScratch, lightBuf, count);
            }
        }

        //=== 上传 ===

        private static void Upload() {
            GraphicsDevice gd = Main.instance?.GraphicsDevice;
            if (gd == null) {
                return;
            }
            if (texture == null || texture.IsDisposed) {
                texture = new Texture2D(gd, CapW, CapH, false, SurfaceFormat.Color);
            }

            //染色对比热调：地板越低暗雾越黑、烬色越强亮雾越暖——亮暗差必须肉眼可分
            float visFloor = MathHelper.Clamp(KiyumeFogDebug.LightVisFloor, 0f, 1f);
            float tintMax = MathHelper.Clamp(KiyumeFogDebug.LightTintStrength, 0f, 1f);
            for (int y = 0; y < winH; y++) {
                int rowBase = y * winW;
                for (int x = 0; x < winW; x++) {
                    int i = rowBase + x;
                    float d = MathHelper.Clamp(density[i], 0f, 1f);
                    //雾吃光：亮处不是被照穿而是被烘暖，且暗处雾仍看得见（可见度地板防纯黑）
                    float lit = MathHelper.Clamp(lightBuf[i] / 0.42f, 0f, 1f);
                    lit *= lit;
                    Vector3 c = Vector3.Lerp(colColor[x], EmberTint, lit * tintMax);
                    float vis = visFloor + (1f - visFloor) * lit;
                    upload[i] = new Color(c.X * vis, c.Y * vis, c.Z * vis, d);
                }
            }

            //防 FNA 对已绑定纹理 SetData 抛异常（无滤镜捕获的帧没人清纹理槽）
            gd.Textures[1] = null;
            gd.Textures[2] = null;
            texture.SetData(0, new Rectangle(0, 0, winW, winH), upload, 0, winW * winH);
            ready = true;
        }

        //=== CPU 回退绘制（着色器缺失时由 System 调用；只保背景层，宁可糙不许黑屏）===

        internal static void DrawFallback(SpriteBatch sb, float presence) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed || !ready || winW <= 0) {
                return;
            }
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            //双层合并系数：回退只画一层
            float alphaMul = MathHelper.Clamp(
                KiyumeFogDebug.BackLayerAlpha + KiyumeFogDebug.FrontLayerAlpha * 0.5f, 0f, 1f) * presence;
            for (int y = 0; y < winH; y++) {
                int rowBase = y * winW;
                int worldYpx = (originCell.Y + y) * CellPx;
                for (int x = 0; x < winW; x++) {
                    Color cell = upload[rowBase + x];
                    if (cell.A < 6) {
                        continue;
                    }
                    float a = cell.A / 255f * alphaMul;
                    //+1px 防雾元间缝
                    var dest = new Rectangle(
                        (int)((originCell.X + x) * CellPx - Main.screenPosition.X),
                        (int)(worldYpx - Main.screenPosition.Y),
                        CellPx + 1, CellPx + 1);
                    sb.Draw(px, dest, new Color(cell.R, cell.G, cell.B) * a);
                }
            }
            sb.End();
        }

        /// <summary>一行状态摘要（调试）</summary>
        internal static string StatusLine() {
            float centerDensity = Main.LocalPlayer != null ? DensityAt(Main.LocalPlayer.Center) : 0f;
            return $"[鬼梦雾] 窗口{winW}x{winH}@({originCell.X},{originCell.Y})"
                + $" 脚下浓度{centerDensity:F2} {KiyumeFogTide.StatusLine()}"
                + $" 抑制源{KiyumeFogSuppression.ActiveCount}"
                + $" presence{KiyumeFogSystem.Presence:F2} 就绪{Ready}";
        }
    }
}
