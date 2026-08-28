using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦贴地雾的离地距离场：按可见瓦片窗口逐列扫描，给每格算"到正下方最近地面的带符号
    /// 竖直距离"（空气为正=离地高，岩内为负=沉入地表深度），编码进世界锚定的窗口纹理，
    /// <c>KikasaDreamFog.fx</c> 逐像素采样定密度。<br/>
    /// 取代旧的逐列探地三角带：旧法探地起点钉在玩家视线高度、单值高度场表达不了多层洞穴、
    /// 斜率钳制切角陡坡，复杂地形下会把雾带横贯岩面暴露出来；距离场对任意地形逐像素成立。<br/>
    /// 窗口管理与上传纪律镜像 <see cref="Scenarios.Kiyume.Fog.KiyumeFogSim"/>；
    /// 场内容无时序状态（每 tick 由瓦片全量重建），窗口平移无需搬运。纯客户端表现
    /// </summary>
    internal static class KikasaDreamGroundField
    {
        /// <summary>1 场元边长（世界px）=1 tile</summary>
        internal const int CellPx = 16;
        /// <summary>窗口四周边距（tile），盖住同 tick 内的相机平移</summary>
        private const int MarginTiles = 4;
        /// <summary>窗口容量（4K@zoom1+边距富余；超界钳制居中并一次性日志）</summary>
        internal const int CapW = 256;
        internal const int CapH = 160;
        /// <summary>窗口底缘外扫行数：地面在屏下方仍要把雾顶送进视野（带高96px+余量）</summary>
        private const int SeedRowsBelow = 8;
        /// <summary>窗口顶缘外扫行数：岩内裙边深度先验（裙26px+余量）</summary>
        private const int SeedRowsAbove = 3;
        /// <summary>编码步长（px/单位）：R 通道 128=地表，量程 ±508px</summary>
        internal const float EncodeStepPx = 4f;

        //R=带符号离地距离编码，G/B/A 保留（Kiyume 移植位：采光/抑制通道）
        private static readonly Color[] upload = new Color[CapW * CapH];
        //逐列复用缓冲：实心缓存（含上下外扫行）与带符号距离，零逐帧分配
        private static readonly bool[] colSolid = new bool[CapH + SeedRowsAbove + SeedRowsBelow];
        private static readonly float[] colDist = new float[CapH];

        private static Texture2D texture;
        private static Point originTile = new(int.MinValue, int.MinValue);
        private static int winW;
        private static int winH;
        private static uint stamp;
        private static bool ready;
        private static bool clampLogged;

        /// <summary>距离场窗口纹理（容量尺寸，左上子矩形有效）</summary>
        internal static Texture2D Texture => texture;
        /// <summary>首次扫描+上传已完成，渲染方可消费</summary>
        internal static bool Ready => ready && texture != null && !texture.IsDisposed;
        internal static Point OriginTile => originTile;
        internal static int WindowW => winW;
        internal static int WindowH => winH;

        /// <summary>世界切换：窗口哨兵化，纹理保留复用（内容下次绘制全量重建）</summary>
        internal static void Reset() {
            originTile = new Point(int.MinValue, int.MinValue);
            winW = winH = 0;
            stamp = 0;
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

        /// <summary>
        /// 绘制方每帧调用，同 tick 幂等；窗口跟随可见区，扫描+上传全量重建。<br/>
        /// 内部会清 s1/s2 纹理槽（SetData 防绑定异常），须在消费端绑定采样器之前调
        /// </summary>
        internal static void Update() {
            if (ready && stamp == Main.GameUpdateCount) {
                return;
            }
            UpdateWindow();
            if (winW <= 0 || winH <= 0) {
                return;
            }
            Scan();
            UploadTexture();
            stamp = Main.GameUpdateCount;
        }

        //窗口跟随可见世界区（vanilla ScreenShaderData.Apply 同式，含镜头缩放）
        private static void UpdateWindow() {
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            float invZx = 1f / MathHelper.Max(zoom.X, 0.05f);
            float invZy = 1f / MathHelper.Max(zoom.Y, 0.05f);
            var visibleSize = new Vector2(Main.screenWidth * invZx, Main.screenHeight * invZy);
            Vector2 visibleTL = Main.screenPosition
                + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f * (Vector2.One - new Vector2(invZx, invZy));

            int newW = (int)MathF.Ceiling(visibleSize.X / CellPx) + MarginTiles * 2 + 1;
            int newH = (int)MathF.Ceiling(visibleSize.Y / CellPx) + MarginTiles * 2 + 1;
            bool clamped = newW > CapW || newH > CapH;
            newW = Math.Min(newW, CapW);
            newH = Math.Min(newH, CapH);
            if (clamped && !clampLogged) {
                clampLogged = true;
                CWRMod.Instance.Logger.Warn($"[KikasaDreamFog] 视野超距离场容量{CapW}x{CapH},窗口钳制到屏幕中心");
            }

            Point newOrigin;
            if (clamped) {
                //超容量：以可见区中心居中，两缘对称让出（KiyumeFogSim 同语义）
                Vector2 center = visibleTL + visibleSize * 0.5f;
                newOrigin = new Point(
                    (int)MathF.Floor(center.X / CellPx) - newW / 2,
                    (int)MathF.Floor(center.Y / CellPx) - newH / 2);
            }
            else {
                newOrigin = new Point(
                    (int)MathF.Floor(visibleTL.X / CellPx) - MarginTiles,
                    (int)MathF.Floor(visibleTL.Y / CellPx) - MarginTiles);
            }
            originTile = newOrigin;
            winW = newW;
            winH = newH;
        }

        //=== 扫描：逐列两遍线性，气高/岩深各一遍 ===

        private static void Scan() {
            int scanRows = winH + SeedRowsAbove + SeedRowsBelow;
            for (int cx = 0; cx < winW; cx++) {
                int tileX = originTile.X + cx;
                bool xIn = tileX >= 20 && tileX < Main.maxTilesX - 20;

                //列实心缓存（含上下外扫行；出世界视作空气）
                for (int r = 0; r < scanRows; r++) {
                    bool solid = false;
                    if (xIn) {
                        int tileY = originTile.Y - SeedRowsAbove + r;
                        if (tileY >= 20 && tileY < Main.maxTilesY - 20) {
                            Tile tile = Framing.GetTileSafely(tileX, tileY);
                            //与旧探地同判据：实心且非平台顶，雾不站平台
                            solid = tile.HasTile && Main.tileSolid[tile.TileType]
                                && !Main.tileSolidTop[tile.TileType];
                        }
                    }
                    colSolid[r] = solid;
                }

                //下→上：空气格离地高（到下方最近实心顶面）；扫程内未见地=正饱和（无雾）
                int airRows = -1;
                for (int r = scanRows - 1; r >= 0; r--) {
                    if (colSolid[r]) {
                        airRows = 0;
                    }
                    else if (airRows >= 0) {
                        airRows++;
                    }
                    int wr = r - SeedRowsAbove;
                    if (wr >= 0 && wr < winH && !colSolid[r]) {
                        colDist[wr] = airRows > 0 ? airRows * 16f - 8f : float.MaxValue;
                    }
                }

                //上→下：岩内格沉入地表深度（到上方最近空气底面）；未见空气=负饱和（无裙）。
                //洞顶实心的"表面"在其下方，这一遍天然给它大深度，雾不挂洞顶
                int rockRows = -1;
                for (int r = 0; r < scanRows; r++) {
                    if (!colSolid[r]) {
                        rockRows = 0;
                    }
                    else if (rockRows >= 0) {
                        rockRows++;
                    }
                    int wr = r - SeedRowsAbove;
                    if (wr >= 0 && wr < winH && colSolid[r]) {
                        colDist[wr] = rockRows > 0 ? -(rockRows * 16f - 8f) : float.MinValue;
                    }
                }

                //编码进行主序上载缓冲：128=地表，4px/单位（值恰为整数，无舍入损失）
                for (int wr = 0; wr < winH; wr++) {
                    float dist = colDist[wr];
                    byte enc;
                    if (dist == float.MaxValue) {
                        enc = 255;
                    }
                    else if (dist == float.MinValue) {
                        enc = 0;
                    }
                    else {
                        enc = (byte)MathHelper.Clamp(128f + dist / EncodeStepPx, 0f, 255f);
                    }
                    upload[wr * winW + cx] = new Color(enc, 0, 0, 255);
                }
            }
        }

        private static void UploadTexture() {
            GraphicsDevice gd = Main.instance?.GraphicsDevice;
            if (gd == null) {
                return;
            }
            if (texture == null || texture.IsDisposed) {
                texture = new Texture2D(gd, CapW, CapH, false, SurfaceFormat.Color);
            }
            //防 FNA 对已绑定纹理 SetData 抛异常（雾绘制自用 s1/s2，画完虽已归还，仍防御性清一遍）
            gd.Textures[1] = null;
            gd.Textures[2] = null;
            texture.SetData(0, new Rectangle(0, 0, winW, winH), upload, 0, winW * winH);
            ready = true;
        }
    }
}
