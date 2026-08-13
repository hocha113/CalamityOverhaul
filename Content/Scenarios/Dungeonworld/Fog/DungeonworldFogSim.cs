using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Fog
{
    /// <summary>
    /// 深牢迷雾密度场：世界锚定的粗网格滚动窗口（1雾元=4×4tile=64px），
    /// 每 2 tick 一步：采光→驱散/回聚（时间不对称）→上传密度纹理（rgb=受光雾色, a=密度）。<br/>
    /// 数组/纹理/上传缓冲全部持久复用，零逐帧分配；纯客户端，服务器不进（FOG.md §2）
    /// </summary>
    internal static class DungeonworldFogSim
    {
        /// <summary>1 雾元边长（世界px）=4 tile</summary>
        internal const int CellPx = 64;
        /// <summary>窗口四周边距雾元数（192px，恰在原版屏外照明覆盖内，入屏前预模拟）</summary>
        internal const int MarginCells = 3;
        /// <summary>模拟步进间隔（tick）</summary>
        internal const int SimIntervalTicks = 2;
        /// <summary>窗口容量（4K@zoom1+边距仍富余；超界钳制并一次性日志）</summary>
        internal const int CapW = 96;
        internal const int CapH = 64;

        //密度当前值（-1=哨兵：新入窗，当步以稳态落地）；delay=回聚延迟余量（tick）
        private static readonly float[] density = new float[CapW * CapH];
        private static readonly float[] densityScratch = new float[CapW * CapH];
        private static readonly ushort[] delay = new ushort[CapW * CapH];
        private static readonly ushort[] delayScratch = new ushort[CapW * CapH];
        //本步采到的亮度（上传时算受光可见度用）
        private static readonly float[] lightBuf = new float[CapW * CapH];
        private static readonly Color[] upload = new Color[CapW * CapH];

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
        /// 公开只读采样：世界px处当前雾密度 0~1（窗口外/未就绪回退基础曲线值）。<br/>
        /// 瘴气 debuff / 雾中敌人扩展位的接入口（FOG.md §9，待用户点火）
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
            DungeonworldFogTheme.Sample(worldPx.Y / 16f, out float baseDensity, out _);
            return MathHelper.Clamp(baseDensity, 0f, 1f);
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
            Step();
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
                CWRMod.Instance.Logger.Warn($"[DungeonworldFog] 视野超窗口容量{CapW}x{CapH},雾窗口钳制到屏幕中心");
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

        //=== 模拟步 ===

        private static void Step() {
            float dispelStep = StepFactor(DungeonworldFogDebug.DispelHalfLifeTicks);
            float regatherStep = StepFactor(DungeonworldFogDebug.RegatherHalfLifeTicks);
            float lightDispel = MathHelper.Max(DungeonworldFogDebug.LightDispel, 0f);
            float densityMul = MathHelper.Max(DungeonworldFogDebug.DensityMul, 0f);
            float fakeRow = DungeonworldFogDebug.FakeWorldRow;
            ushort regatherDelay = (ushort)MathHelper.Clamp(DungeonworldFogDebug.RegatherDelayTicks, 0f, 60000f);
            bool anySuppress = FogSuppression.AnyActive;

            for (int y = 0; y < winH; y++) {
                int rowBase = y * winW;
                float worldY = (originCell.Y + y + 0.5f) * CellPx;
                float sampleRow = fakeRow >= 0f ? fakeRow : worldY / 16f;
                DungeonworldFogTheme.Sample(sampleRow, out float baseDensity, out _);
                int tileY = Math.Clamp((originCell.Y + y) * 4 + 2, 0, Main.maxTilesY - 1);

                for (int x = 0; x < winW; x++) {
                    int i = rowBase + x;
                    int tileX = Math.Clamp((originCell.X + x) * 4 + 2, 0, Main.maxTilesX - 1);
                    //Lighting.Brightness=GlobalBrightness*(r+g+b)/3（TML Lighting.cs L119）
                    float bright = Lighting.Brightness(tileX, tileY);
                    lightBuf[i] = bright;

                    float target = baseDensity * MathHelper.Clamp(1f - bright * lightDispel, 0f, 1f) * densityMul;
                    if (anySuppress) {
                        target *= FogSuppression.Evaluate(new Vector2((originCell.X + x + 0.5f) * CellPx, worldY));
                    }

                    float cur = density[i];
                    if (cur < 0f) {
                        //新入窗雾元：以稳态落地（屏外的雾早已回聚/被光让开）
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

        //=== 上传 ===

        private static void Upload() {
            GraphicsDevice gd = Main.instance?.GraphicsDevice;
            if (gd == null) {
                return;
            }
            if (texture == null || texture.IsDisposed) {
                texture = new Texture2D(gd, CapW, CapH, false, SurfaceFormat.Color);
            }

            float fakeRow = DungeonworldFogDebug.FakeWorldRow;
            for (int y = 0; y < winH; y++) {
                int rowBase = y * winW;
                float sampleRow = fakeRow >= 0f ? fakeRow : (originCell.Y + y + 0.5f) * CellPx / 16f;
                DungeonworldFogTheme.Sample(sampleRow, out _, out Vector3 fogColor);
                for (int x = 0; x < winW; x++) {
                    int i = rowBase + x;
                    float d = MathHelper.Clamp(density[i], 0f, 1f);
                    //受光可见度：纯黑处雾不可见（没有光就没有可看见的雾），微光处雾最有形
                    float t = MathHelper.Clamp((lightBuf[i] - 0.04f) / 0.26f, 0f, 1f);
                    float vis = t * t * (3f - 2f * t);
                    upload[i] = new Color(fogColor.X * vis, fogColor.Y * vis, fogColor.Z * vis, d);
                }
            }

            //防 FNA 对已绑定纹理 SetData 抛异常（无滤镜捕获的帧没人清纹理槽）
            gd.Textures[1] = null;
            gd.Textures[2] = null;
            texture.SetData(0, new Rectangle(0, 0, winW, winH), upload, 0, winW * winH);
            ready = true;
        }

        //=== CPU 回退绘制（着色器缺失时由 System 调用；只保背景层，宁可糙不许黑屏/裸奔）===

        internal static void DrawFallback(SpriteBatch sb, float presence) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed || !ready || winW <= 0) {
                return;
            }
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            //双层合并系数：回退只画一层
            float alphaMul = MathHelper.Clamp(
                DungeonworldFogDebug.BackLayerAlpha + DungeonworldFogDebug.FrontLayerAlpha * 0.5f, 0f, 1f) * presence;
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
            return $"[深牢迷雾] 窗口{winW}x{winH}@({originCell.X},{originCell.Y})"
                + $" 脚下浓度{centerDensity:F2} 抑制源{FogSuppression.ActiveCount}"
                + $" presence{DungeonworldFogSystem.Presence:F2} 就绪{Ready}";
        }
    }
}
