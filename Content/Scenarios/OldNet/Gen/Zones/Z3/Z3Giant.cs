using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z3
{
    //坠亡巨物·沉默残骸（点子9 旗舰）：天幕远景巨物（OldNetSkyEvents.Giant）的一具
    //坠死在信号尽头，全图最大单体地标。弦月巨壳半埋隆起，脊背断续栈线可走，
    //肋腔与头颅腔是衰减区唯一的大型室内空间（疯域噪音不衰减 × 死胡同腔体=进腔即入瓮）。
    //
    //刻意不入 ctx.Graph：图的契约是井网链式挂房连通，巨物腔室只连开阔地表，
    //入图必致 Z3 日志 graphConnected 误报。腔室坐标由 FallenGiant* 常量 + 槽位注释暴露，
    //04/06 需要布防或事件舞台时按坐标自取（头颅腔单出入口近似必死房，04 建议豁免或低概率）
    internal static class Z3Giant
    {
        //18 段阶梯弧高度表（Hmax 比例）：西端没入地面（坠向）→ 峰腰 → 东端翘起断口
        private static readonly float[] Profile = [
            0.04f, 0.10f, 0.18f, 0.30f, 0.44f, 0.60f, 0.76f, 0.88f, 0.97f,
            1.00f, 0.98f, 0.92f, 0.83f, 0.72f, 0.62f, 0.55f, 0.58f, 0.52f,
        ];

        /// <summary>
        /// 宽度参数化 88→72→60 同构缩比落位（16 次试位/档），返回建成数。
        /// 峰高/腔体/头颅随宽度等比换算，最坏缩到 60 宽在 Z3 带必然放得下
        /// </summary>
        internal static int BuildFallenGiant(OldNetBuildContext ctx) {
            int[] floorTop = OldNetPlans.FloorTop;
            int built = 0;
            for (int n = 0; n < OldNetMetrics.FallenGiantCount; n++) {
                if (!TryBuildOne(ctx, floorTop)) {
                    CWRMod.Instance.Logger.Warn("[OldNet] 坠亡巨物落位失败（三档缩比全败）");
                    break;
                }
                built++;
            }
            return built;
        }

        private static bool TryBuildOne(OldNetBuildContext ctx, int[] floorTop) {
            foreach (int w in OldNetMetrics.FallenGiantWidths) {
                int hmax = w * 30 / 88;
                for (int attempt = 0; attempt < 16; attempt++) {
                    int left = WorldGen.genRand.Next(
                        OldNetMetrics.FallenGiantColMin, OldNetMetrics.FallenGiantColMax - w);
                    int surface = floorTop[left + w / 2];
                    //足印含西侧坠击沟（-14）与东侧散落残骸带（+12），高度含脊背栈线与半埋段
                    var foot = new Rectangle(left - 14, surface - hmax - 6, w + 26, hmax + 12);
                    if (!ctx.Grid.TryReserve(foot, OldNetMetrics.RoomPadding)) {
                        continue;
                    }
                    BuildGiantAt(left, w, hmax, surface, floorTop);
                    OldNetPlans.ScatterExclusions.Add(foot);
                    CWRMod.Instance.Logger.Info($"[OldNet] 坠亡巨物@列{left} 宽{w} 峰高{hmax}");
                    return true;
                }
            }
            return false;
        }

        private static void BuildGiantAt(int left, int w, int hmax, int surface, int[] floorTop) {
            //① 主壳：逐列查表拼竖条，壳体焦黑黑曜石（Z3 正典），基部逐列扎进本地地面
            int[] hullTop = new int[w];
            for (int i = 0; i < w; i++) {
                float t = i / (float)(w - 1);
                int seg = System.Math.Min((int)(t * Profile.Length), Profile.Length - 1);
                int h = System.Math.Max(1, (int)(Profile[seg] * hmax));
                int x = left + i;
                hullTop[i] = surface - h;
                //西端 18% 半埋更深：坠向朝西的物理证词
                int burrow = t < 0.18f ? 3 : 1;
                int bottom = System.Math.Max(surface, floorTop[x]) + burrow;
                if (hullTop[i] < floorTop[x]) {
                    OldNetTileBrush.FillRect(x, hullTop[i], x + 1, bottom, TileID.ObsidianBrick);
                }
            }
            //顶缘阶梯弧收角：邻列 1~3 行落差补斜切帽（更大落差=刻意的断面）
            for (int i = 0; i < w - 1; i++) {
                int step = hullTop[i] - hullTop[i + 1];
                if (step >= 1 && step <= 3) {
                    OldNetTileBrush.SetSloped(left + i, hullTop[i] - 1,
                        TileID.ObsidianBrick, SlopeType.SlopeDownLeft);
                }
                else if (step <= -1 && step >= -3) {
                    OldNetTileBrush.SetSloped(left + i + 1, hullTop[i + 1] - 1,
                        TileID.ObsidianBrick, SlopeType.SlopeDownRight);
                }
            }

            //② 肋腔（主腔）：壳下空腔，内衬完好导管材质（"外焦里完好"的自体剖面）。
            //腔体西缘从 0.18W 起找第一根壳厚足够的列（腔顶之上至少 2 行壳，防穿顶）
            int cavH = System.Math.Max(8, 12 * w / 88);
            int cavW = System.Math.Max(14, 26 * w / 88);
            int cavTop = surface - cavH;
            int cavL = left + w * 18 / 100;
            while (cavL - left < w / 2 && hullTop[cavL - left] > cavTop - 2) {
                cavL++;
            }
            int cavR = System.Math.Min(cavL + cavW, left + w / 2 + 4);
            OldNetTileBrush.CarveRect(cavL, cavTop, cavR, surface, WallID.MartianConduit);

            //③ 顶部破口：腔体东端的天窗（脊背栈线在此留缺，从上方坠入腔内）。
            //逐列钳制刷墙上界：壳顶以上的露天格只清不刷墙（防地标最高轮廓处的悬浮墙贴片），
            //顶行取 hullTop-1 只为掀掉 ① 补的斜切帽
            int breachX = cavR - 3;
            for (int bx = breachX; bx < breachX + 2; bx++) {
                int colTop = hullTop[bx - left];
                for (int y = colTop - 1; y < cavTop + 1; y++) {
                    OldNetTileBrush.ClearCell(bx, y, y < colTop ? WallID.None : WallID.MartianConduit);
                }
            }

            //④ 肋骨：每 4 列一根 2 宽竖柱，顶接壳底、底插地面；破口正下跳过
            //04槽位:肋腔吊点（腔顶中线 (cavL+cavW/2, cavTop+1)，布防按坐标自取）
            for (int rx = cavL + 3; rx + 2 <= cavR - 2; rx += 4) {
                if (rx + 2 > breachX && rx < breachX + 2) {
                    continue;
                }
                OldNetTileBrush.FillRect(rx, cavTop, rx + 2, surface + 1, TileID.MartianConduitPlating);
            }

            //⑤ 西侧肋间隙入口：地面 3 高横道，壳薄处自然张成洞口。
            //西趾低段逐列钳制：壳顶以上的露天格只清不刷墙（同 ③ 防悬浮墙）
            for (int ex = cavL - 7; ex < cavL + 1; ex++) {
                int colTop = hullTop[ex - left];
                for (int y = surface - 3; y < surface; y++) {
                    OldNetTileBrush.ClearCell(ex, y, y < colTop ? WallID.None : WallID.MartianConduit);
                }
            }

            //⑥ 头颅腔：东端翘起段的独立小腔（唯一出入口=颈部撕裂口，装进瓮里的那间）
            int chamW = System.Math.Max(8, 12 * w / 88);
            int chamH = System.Math.Max(5, 8 * w / 88);
            int chamL = left + w * 72 / 100;
            int chamR = chamL + chamW;
            int chamFloor = surface - 5;
            OldNetTileBrush.CarveRect(chamL, chamFloor - chamH, chamR, chamFloor, WallID.MartianConduit);
            //颈部撕裂口：3 高通道自腔体东壁通到断口崖面（腔底比通道低 1 行，翻唇缘而入）
            OldNetTileBrush.CarveRect(chamR, surface - 9, left + w, surface - 6, WallID.MartianConduit);
            //核心台：3×2 基座 + 顶面平台
            //02槽位:巨物核心（高值预留，02 接手前放普通节点）
            int coreX = chamL + 2;
            OldNetTileBrush.FillRect(coreX, chamFloor - 2, coreX + 3, chamFloor, TileID.MartianConduitPlating);
            OldNetTileBrush.PlatformRow(coreX, coreX + 3, chamFloor - 3, Z3Style.PlatformFrameY);

            //⑦ 脊背栈线：断续平台读作脊椎板（每 8~12 格断 1~2 格），破口处留缺
            int runLeft = WorldGen.genRand.Next(8, 13);
            int gapLeft = 0;
            for (int i = 4; i < w - 4; i++) {
                int x = left + i;
                if (x >= breachX - 1 && x < breachX + 3) {
                    continue;
                }
                if (gapLeft > 0) {
                    gapLeft--;
                    continue;
                }
                OldNetTileBrush.SetPlatform(x, hullTop[i] - 2, Z3Style.PlatformFrameY);
                if (--runLeft <= 0) {
                    gapLeft = WorldGen.genRand.Next(1, 3);
                    runLeft = WorldGen.genRand.Next(8, 13);
                }
            }

            //⑧ 坠击沟：西侧犁沟阶梯下凹（1→2→3 行，1 行台阶可走免斜切），回写 FloorTop
            for (int x = left - 12; x < left; x++) {
                int depth = x < left - 9 ? 1 : x < left - 5 ? 2 : 3;
                for (int y = floorTop[x]; y < floorTop[x] + depth; y++) {
                    OldNetTileBrush.ClearCell(x, y);
                }
                floorTop[x] += depth;
            }

            //⑨ 腔内普通节点（既有 Budget 配额）：肋腔 1~2 + 头颅核心 1
            OldNetPlans.Budget.TryPlaceUnderPlain(cavL + 9, surface - 1);
            if (WorldGen.genRand.NextBool()) {
                OldNetPlans.Budget.TryPlaceUnderPlain(cavR - 4, surface - 1);
            }
            OldNetPlans.Budget.TryPlaceUnderPlain(coreX + 1, chamFloor - 4);

            //⑩ 散落残骸：壳周碎块堆（斜切毛边），东侧兼作攀入撕裂口的踏脚
            SpawnDebris(left - 10, floorTop, 2, 2);
            SpawnDebris(left - 4, floorTop, 3, 2);
            SpawnDebris(left + w + 2, floorTop, 3, 3);
            SpawnDebris(left + w + 6, floorTop, 4, 2);
            if (WorldGen.genRand.NextBool()) {
                SpawnDebris(left + w + 10, floorTop, 2, 2);
            }
            //遗物直放（TryWrite 在衰减区自动上焦黑变体）：通勤者的东西按放下的姿势停在原地
            Tiles.OldNetRelicTile.TryWrite(left - 8, floorTop[left - 8] - 1,
                Tiles.OldNetRelicTile.RollStyle(3));
            Tiles.OldNetRelicTile.TryWrite(left + w + 8, floorTop[left + w + 8] - 1,
                Tiles.OldNetRelicTile.RollStyle(3));
            Tiles.OldNetRelicTile.TryWrite(cavL + 5, surface - 1, Tiles.OldNetRelicTile.RollStyle(3));
        }

        //碎块堆：实心小堆 + 一角斜切毛边
        private static void SpawnDebris(int x, int[] floorTop, int dw, int dh) {
            int baseRow = floorTop[x];
            OldNetTileBrush.FillRect(x, baseRow - dh, x + dw, baseRow, TileID.ObsidianBrick);
            OldNetTileBrush.SetSloped(x + (WorldGen.genRand.NextBool() ? 0 : dw - 1), baseRow - dh - 1,
                TileID.ObsidianBrick, WorldGen.genRand.NextBool()
                    ? SlopeType.SlopeDownLeft : SlopeType.SlopeDownRight);
        }
    }
}
