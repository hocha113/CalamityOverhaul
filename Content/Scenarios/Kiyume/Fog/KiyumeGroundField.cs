using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Fog
{
    /// <summary>
    /// 鬼梦贴地残雾的离地距离场（<see cref="LegendWeapon.KikasaLegend.KikasaDreams.KikasaDreamGroundField"/>
    /// 同法，为潮汐/驱散/采光染扩通道）：按可见瓦片窗口逐列扫描，
    /// R=到正下方最近地面的带符号竖直距离（空气为正、岩内为负），G=驱散抑制因子，
    /// B=采光曲线值（与 <see cref="KiyumeFogSim"/> 染色同式）；主题带色沿 x 缓变，
    /// 另烘 CapW×1 条带纹理（<see cref="ThemeTexture"/>）。KiyumeGroundFog.fx 逐像素采样定密度。<br/>
    /// 取代旧的逐列探地三角带（盲探回退会把雾带钉在玩家视线高度、单值高度场表达不了多层地形）；
    /// 瀑布雾的瀑口检测仍走渲染器保留的探柱。窗口/上传纪律镜像 <see cref="KiyumeFogSim"/>；
    /// 场内容无时序状态（按步由瓦片全量重建），窗口平移无需搬运。纯客户端表现
    /// </summary>
    internal static class KiyumeGroundField
    {
        /// <summary>1 场元边长（世界px）=1 tile</summary>
        internal const int CellPx = 16;
        /// <summary>窗口四周边距（tile），盖住步进间隔内的相机平移</summary>
        private const int MarginTiles = 4;
        /// <summary>重建步进间隔（tick），与 KiyumeFogSim 同频</summary>
        private const int StepIntervalTicks = 2;
        /// <summary>窗口容量（4K@zoom1+边距富余；超界钳制居中并一次性日志）</summary>
        internal const int CapW = 256;
        internal const int CapH = 160;
        /// <summary>窗口底缘外扫行数：地面在屏下方仍要把雾顶送进视野（带高110px+余量）</summary>
        private const int SeedRowsBelow = 9;
        /// <summary>窗口顶缘外扫行数：岩内裙边深度先验（裙30px+余量）</summary>
        private const int SeedRowsAbove = 3;
        /// <summary>编码步长（px/单位）：R 通道 128=地表，±508px 量程</summary>
        internal const float EncodeStepPx = 4f;

        private static readonly Color[] upload = new Color[CapW * CapH];
        private static readonly Color[] themeUpload = new Color[CapW];
        //逐列复用缓冲：实心缓存（含上下外扫行）与带符号距离，零逐帧分配
        private static readonly bool[] colSolid = new bool[CapH + SeedRowsAbove + SeedRowsBelow];
        private static readonly float[] colDist = new float[CapH];

        private static Texture2D texture;
        private static Texture2D themeTexture;
        private static Point originTile = new(int.MinValue, int.MinValue);
        private static int winW;
        private static int winH;
        private static uint stamp;
        private static bool ready;
        private static bool clampLogged;

        /// <summary>距离场窗口纹理（容量尺寸，左上子矩形有效）</summary>
        internal static Texture2D Texture => texture;
        /// <summary>主题带色条带（CapW×1，与场窗口同列对齐）</summary>
        internal static Texture2D ThemeTexture => themeTexture;
        /// <summary>首次扫描+上传已完成，渲染方可消费</summary>
        internal static bool Ready => ready
            && texture != null && !texture.IsDisposed
            && themeTexture != null && !themeTexture.IsDisposed;
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
            Texture2D theme = themeTexture;
            texture = null;
            themeTexture = null;
            Main.QueueMainThreadAction(() => {
                if (tex != null && !tex.IsDisposed) {
                    tex.Dispose();
                }
                if (theme != null && !theme.IsDisposed) {
                    theme.Dispose();
                }
            });
        }

        /// <summary>
        /// 绘制方每帧调用；内部按 <see cref="StepIntervalTicks"/> 分频全量重建。<br/>
        /// 内部会清 s1~s3 纹理槽（SetData 防绑定异常），须在消费端绑定采样器之前调
        /// </summary>
        internal static void Update() {
            if (ready && Main.GameUpdateCount - stamp < StepIntervalTicks) {
                return;
            }
            UpdateWindow();
            if (winW <= 0 || winH <= 0) {
                return;
            }
            Scan();
            UploadTextures();
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
                CWRMod.Instance.Logger.Warn($"[KiyumeGroundFog] 视野超距离场容量{CapW}x{CapH},窗口钳制到屏幕中心");
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

        //=== 扫描：距离两遍线性 + 逐格抑制/采光 + 逐列主题色 ===

        private static void Scan() {
            int scanRows = winH + SeedRowsAbove + SeedRowsBelow;
            bool anySuppress = KiyumeFogSuppression.AnyActive;
            for (int cx = 0; cx < winW; cx++) {
                int tileX = originTile.X + cx;
                bool xIn = tileX >= 20 && tileX < Main.maxTilesX - 20;

                //主题带色（沿 x 缓变，逐列一采）
                KiyumeFogTheme.Sample(tileX + 0.5f, out Vector3 themeCol, out _);
                themeUpload[cx] = new Color(themeCol.X, themeCol.Y, themeCol.Z);

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

                //下→上：空气格离地高；扫程内未见地=正饱和（无雾）
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

                //上→下：岩内格沉入地表深度；未见空气=负饱和（无裙）。洞顶实心天然得大深度，雾不挂洞顶
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

                //编码：R=距离（128=地表，4px/单位）、G=抑制、B=采光曲线值
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

                    int tileY = originTile.Y + wr;
                    //抑制因子（16px 场元比 64px 雾元还细，推雾回聚不对称免费继承）
                    float sup = anySuppress
                        ? KiyumeFogSuppression.Evaluate(new Vector2(tileX * 16f + 8f, tileY * 16f + 8f))
                        : 1f;
                    //采光烬染曲线与 KiyumeFogSim.Upload 同式；岩内格上移 2 tile 采地表光，
                    //裙边继承地上亮度，接触处不生暗缝
                    int litY = Math.Clamp(colSolid[wr + SeedRowsAbove] ? tileY - 2 : tileY, 0, Main.maxTilesY - 1);
                    float lit = xIn
                        ? MathHelper.Clamp(Lighting.Brightness(tileX, litY) / 0.42f, 0f, 1f)
                        : 0f;
                    lit *= lit;

                    upload[wr * winW + cx] = new Color(enc,
                        (byte)(MathHelper.Clamp(sup, 0f, 1f) * 255f),
                        (byte)(lit * 255f), (byte)255);
                }
            }
        }

        private static void UploadTextures() {
            GraphicsDevice gd = Main.instance?.GraphicsDevice;
            if (gd == null) {
                return;
            }
            if (texture == null || texture.IsDisposed) {
                texture = new Texture2D(gd, CapW, CapH, false, SurfaceFormat.Color);
            }
            if (themeTexture == null || themeTexture.IsDisposed) {
                themeTexture = new Texture2D(gd, CapW, 1, false, SurfaceFormat.Color);
            }
            //防 FNA 对已绑定纹理 SetData 抛异常（雾绘制自用 s1~s3，画完虽已归还，仍防御性清一遍）
            gd.Textures[1] = null;
            gd.Textures[2] = null;
            gd.Textures[3] = null;
            texture.SetData(0, new Rectangle(0, 0, winW, winH), upload, 0, winW * winH);
            themeTexture.SetData(0, new Rectangle(0, 0, winW, 1), themeUpload, 0, winW);
            ready = true;
        }
    }
}
