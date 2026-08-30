using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios
{
    /// <summary>
    /// 超高子世界的小地图渲染目标网格扩容器<br/>
    /// 原版 <c>Main.mapTarget</c> 网格 [5,2] 按最大世界 8400×2400 精确裁剪(单元 2000×1800,末行只建 600px),
    /// 纵向只覆盖 3600 行;更高的世界会让后台分段绘制(DrawToMap_Section→checkMap)、
    /// 全量重绘(DrawToMap)与地图 UI(DrawMap)三处索引 mapTarget[·,2] 越界,进世界数秒内必崩<br/>
    /// <br/>
    /// 机制:进世界时按 maxTilesY 扩数组行数(纯托管换数组,允许加载线程),
    /// 新增行的 RenderTarget 由世界内每帧 <see cref="Upkeep"/> 在主线程补建为全尺寸
    /// (绕开原版 checkMap 的"末行 600px"裁剪,600 高的末行盖不住 4200 行以下);
    /// 回到常规世界时 <see cref="Sync"/> 自动收缩回原版维度并释放新增行显存<br/>
    /// 接线:两个子世界 OnLoad 调 Sync、Update 调 Upkeep;OnWorldLoad 兜底自愈
    /// (SLib 子世界不保证走 ModSystem.OnWorldLoad,主世界一定走,收缩由它完成)
    /// </summary>
    internal class SubworldMapGrid : ModSystem
    {
        //原版行数基线(首次同步时记录,收缩时恢复)
        private static int vanillaRows = -1;
        //扩容态下需要补建目标的列数(按当前世界宽度算)
        private static int extraCols;
        //当前处于扩容态
        private static bool grown;
        //补建失败只告警一次,失败后让位给原版 checkMap 的容错(600 高目标,画面裁切但不崩)
        private static bool upkeepFailWarned;

        public override void OnWorldLoad() => Sync();

        /// <summary>
        /// 按当前世界高度同步网格容量,幂等;子世界 OnLoad 与主世界 OnWorldLoad 都会经过这里
        /// </summary>
        internal static void Sync() {
            if (Main.dedServ) {
                return;
            }
            if (vanillaRows < 0) {
                vanillaRows = Main.mapTargetY;
            }
            int needRows = (Main.maxTilesY + Main.textureMaxHeight - 1) / Main.textureMaxHeight;
            int targetRows = Math.Max(vanillaRows, needRows);
            if (targetRows != Main.mapTargetY) {
                Resize(targetRows);
            }
            grown = Main.mapTargetY > vanillaRows;
            //列数与 DrawToMap 的 checkMap 循环同式(floor+1):扩容行内被触碰的单元全部我方全尺寸预建
            extraCols = grown ? Main.maxTilesX / Main.textureMaxWidth + 1 : 0;
            upkeepFailWarned = false;
        }

        /// <summary>
        /// 世界内每帧保养(仅主线程调用):补建/修复扩容行的全尺寸渲染目标<br/>
        /// 覆盖两种缺口:首帧尚未建目标;设备重置销毁或原版 checkMap 按末行 600px 误建
        /// </summary>
        internal static void Upkeep() {
            if (!grown || Main.dedServ) {
                return;
            }
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            if (gd == null || gd.IsDisposed) {
                return;
            }
            bool rebuilt = false;
            int cols = Math.Min(extraCols, Main.mapTargetX);
            for (int i = 0; i < cols; i++) {
                for (int j = vanillaRows; j < Main.mapTargetY; j++) {
                    RenderTarget2D rt = Main.instance.mapTarget[i, j];
                    if (rt != null && !rt.IsDisposed && rt.Height >= Main.textureMaxHeight) {
                        continue;
                    }
                    try {
                        rt?.Dispose();
                        Main.instance.mapTarget[i, j] = new RenderTarget2D(gd,
                            Main.textureMaxWidth, Main.textureMaxHeight, mipMap: false,
                            gd.PresentationParameters.BackBufferFormat, DepthFormat.None, 0,
                            RenderTargetUsage.PreserveContents);
                        Main.initMap[i, j] = true;
                        rebuilt = true;
                    }
                    catch (Exception e) {
                        if (!upkeepFailWarned) {
                            upkeepFailWarned = true;
                            CWRMod.Instance.Logger.Warn($"[SubworldMapGrid] 扩容行目标补建失败({i},{j}): {e.Message}");
                        }
                        return;
                    }
                }
            }
            if (rebuilt) {
                //新建目标是空白的,触发整图重绘补内容(refreshMap 需搭配 updateMap 才被消费)
                Main.refreshMap = true;
                Main.updateMap = true;
            }
        }

        //换数组行数,拷贝保留行引用,释放被裁行的显存
        //写序契约:扩大=先换数组再抬 mapTargetY;收缩=先压 mapTargetY 再换数组
        //(任一时刻读到的 mapTargetY 都不超过新旧两数组的公共行数,杜绝撕裂越界)
        private static void Resize(int rows) {
            int cols = Main.mapTargetX;
            int oldRows = Main.mapTargetY;
            var newTargets = new RenderTarget2D[cols, rows];
            var newInit = new bool[cols, rows];
            var newLost = new bool[cols, rows];
            RenderTarget2D[,] oldTargets = Main.instance.mapTarget;
            int copyRows = Math.Min(rows, oldRows);
            for (int i = 0; i < cols; i++) {
                for (int j = 0; j < copyRows; j++) {
                    newTargets[i, j] = oldTargets[i, j];
                    newInit[i, j] = Main.initMap[i, j];
                    newLost[i, j] = Main.mapWasContentLost[i, j];
                }
            }
            if (rows > oldRows) {
                Main.instance.mapTarget = newTargets;
                Main.initMap = newInit;
                Main.mapWasContentLost = newLost;
                Main.mapTargetY = rows;
            }
            else {
                Main.mapTargetY = rows;
                Main.instance.mapTarget = newTargets;
                Main.initMap = newInit;
                Main.mapWasContentLost = newLost;
                for (int i = 0; i < cols; i++) {
                    for (int j = rows; j < oldRows; j++) {
                        oldTargets[i, j]?.Dispose();
                    }
                }
            }
            CWRMod.Instance.Logger.Info(
                $"[SubworldMapGrid] 地图网格 {cols}x{oldRows} → {cols}x{rows}(世界 {Main.maxTilesX}x{Main.maxTilesY})");
        }
    }
}
