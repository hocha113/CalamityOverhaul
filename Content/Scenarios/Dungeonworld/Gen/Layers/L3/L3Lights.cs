using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L3
{
    //====================================================================
    //L3灭灯+开关电线玩法(本层独占,INDEX §3裁决;语法逐行对源=F33):
    //  ·吊灯:WorldGen.PlaceChand(中列,首空行,34,style)——自带3x3净空+中列顶锚校验
    //    (WorldGen.cs:46176-46243);熄灭=3x3全格frameX+=54,与Wiring.ToggleChandelier
    //    (Wiring.cs:2836-2874,点亮判据frameX%108==0)完全互逆——原版gen里对吊灯只写
    //    两格frameX=18(:28688-28689)是无效操作,本层修正为可开关的真熄灭
    //  ·灯笼:WorldGen.Place1x2Top(x,首空行,42,style)——自带顶锚实心+下格空校验
    //    (:38648-38671);熄灭=两格frameX=18(:28754-28757原版逐字),ToggleHangingLantern互逆
    //  ·开关:tile136裸PlaceTile+落点条件镜像原版(:28718-28727):
    //    候选=灯±12列/下方3~20行,侧邻实心非门或脚下实心,以active()为准
    //  ·电线:开关→灯的阶梯路径逐格RedWire(:28664-28685原版walk逐字,TML现代API=Tile.RedWire)
    //随机全走WorldGen.genRand(F22)
    //====================================================================
    internal static class L3Lights
    {
        //计数器:入口报告用(每次生成由L3Content重置)
        internal static int LampsLit;
        internal static int LampsOff;
        internal static int SwitchesPlaced;

        internal static void ResetCounters() => LampsLit = LampsOff = SwitchesPlaced = 0;

        //==================== 放灯(亮态) ====================

        /// <summary>吊灯:centerX=中列,airY=天花板下首空行;成功返回true</summary>
        internal static bool PlaceChandelier(int centerX, int airY) {
            WorldGen.PlaceChand(centerX, airY, TileID.Chandeliers, L3Palette.StyleChandelier);
            return Main.tile[centerX, airY].HasTile
                && Main.tile[centerX, airY].TileType == TileID.Chandeliers;
        }

        /// <summary>灯笼:x列airY=天花板下首空行;caged=笼灯样式,否则链灯笼</summary>
        internal static bool PlaceLantern(int x, int airY, bool caged) {
            WorldGen.Place1x2Top(x, airY, TileID.HangingLanterns,
                caged ? L3Palette.StyleLanternCaged : L3Palette.StyleLanternChain);
            return Main.tile[x, airY].HasTile
                && Main.tile[x, airY].TileType == TileID.HangingLanterns;
        }

        //==================== 熄灭(帧改写,与Wiring开关互逆) ====================

        /// <summary>吊灯熄灭:3x3全格frameX+=54(centerX/airY=放置时参数)</summary>
        internal static void ExtinguishChandelier(int centerX, int airY) {
            for (int x = centerX - 1; x <= centerX + 1; x++) {
                for (int y = airY; y < airY + 3; y++) {
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType == TileID.Chandeliers) {
                        tile.TileFrameX += 54;
                    }
                }
            }
        }

        /// <summary>灯笼熄灭:两格frameX=18(原版:28754-28757逐字)</summary>
        internal static void ExtinguishLantern(int x, int airY) {
            for (int dy = 0; dy < 2; dy++) {
                Tile tile = Main.tile[x, airY + dy];
                if (tile.HasTile && tile.TileType == TileID.HangingLanterns) {
                    tile.TileFrameX = 18;
                }
            }
        }

        //==================== 开关与电线 ====================

        /// <summary>
        /// 指定点放开关:落点空+侧邻实心非门或脚下实心(镜像:28724-28727),
        /// 以active为准;成功计数并返回true。
        /// </summary>
        internal static bool TryPlaceSwitch(int x, int y) {
            Tile cell = Main.tile[x, y];
            if (cell.HasTile) {
                return false;
            }
            bool sideMount = (SolidNotDoor(x - 1, y) || SolidNotDoor(x + 1, y))
                || WorldGen.SolidTile(x, y + 1);
            if (!sideMount) {
                return false;
            }
            WorldGen.PlaceTile(x, y, TileID.Switches, mute: true);
            if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Switches) {
                SwitchesPlaced++;
                return true;
            }
            return false;
        }

        private static bool SolidNotDoor(int x, int y)
            => WorldGen.SolidTile(x, y) && Main.tile[x, y].TileType != TileID.ClosedDoor;

        /// <summary>
        /// 电线阶梯路径:自(fromX,fromY)走到(toX,toY),逐格RedWire
        /// (原版walk逐字:先横步后纵步交替,两端点均被布线,:28664-28685)。
        /// </summary>
        internal static void WireStaircase(int fromX, int fromY, int toX, int toY) {
            int x = fromX, y = fromY;
            int guard = 0;
            while ((x != toX || y != toY) && guard++ < 600) {
                Main.tile[x, y].RedWire = true;
                if (x > toX) {
                    x--;
                }
                if (x < toX) {
                    x++;
                }
                Main.tile[x, y].RedWire = true;
                if (y > toY) {
                    y--;
                }
                if (y < toY) {
                    y++;
                }
                Main.tile[x, y].RedWire = true;
            }
        }

        //==================== 组合语法:带开关的灯(撒布与灯房共用) ====================

        /// <summary>
        /// 原版F33完整序列:在(x,airY)放灯→附近寻位放开关→布线→按offChance熄灭。
        /// 开关候选=±12列/下方3~20行(原版参数逐字);寻位失败=灯保持点亮(fail-open,
        /// 绝不留"无开关的灭灯");chandelier=吊灯形态,否则灯笼。
        /// 返回true=灯已放置(无论亮灭)。
        /// </summary>
        internal static bool PlaceWiredLamp(int x, int airY, bool chandelier, bool caged,
            int offNumerator, int offDenominator, UnifiedRandom rand) {
            bool placed = chandelier ? PlaceChandelier(x, airY) : PlaceLantern(x, airY, caged);
            if (!placed) {
                return false;
            }

            //开关寻位:镜像原版1000次掷点的缩减版(60次足够,失败=保持点亮)
            bool wired = false;
            for (int attempt = 0; attempt < 60 && !wired; attempt++) {
                int sx = x + rand.Next(-12, 13);
                int sy = airY + rand.Next(3, 21);
                if (!WorldGen.InWorld(sx, sy, 5) || !Main.wallDungeon[Main.tile[sx, sy].WallType]) {
                    continue;
                }
                if (!TryPlaceSwitch(sx, sy)) {
                    continue;
                }
                WireStaircase(sx, sy, x, airY);
                wired = true;
            }

            if (wired && rand.Next(offDenominator) < offNumerator) {
                if (chandelier) {
                    ExtinguishChandelier(x, airY);
                }
                else {
                    ExtinguishLantern(x, airY);
                }
                LampsOff++;
            }
            else {
                LampsLit++;
            }
            return true;
        }

        /// <summary>
        /// 灯房专用:显式开关位+一关多灯(开关→灯1→灯2链式布线,信号沿线全触发)。
        /// lampXs为各灯x列(同一天花行airY),switchX/switchY为开关位;全部灯放置后熄灭。
        /// 返回实际放置的灯数。
        /// </summary>
        internal static int PlaceLampChain(int[] lampXs, int airY, int switchX, int switchY,
            bool caged, UnifiedRandom rand) {
            int placed = 0;
            var lit = new System.Collections.Generic.List<int>();
            foreach (int lx in lampXs) {
                if (PlaceLantern(lx, airY, caged)) {
                    lit.Add(lx);
                    placed++;
                }
            }
            if (lit.Count == 0) {
                return 0;
            }
            if (TryPlaceSwitch(switchX, switchY)) {
                //开关→首灯→次灯…链式布线
                WireStaircase(switchX, switchY, lit[0], airY);
                for (int i = 0; i + 1 < lit.Count; i++) {
                    WireStaircase(lit[i], airY, lit[i + 1], airY);
                }
                foreach (int lx in lit) {
                    ExtinguishLantern(lx, airY);
                    LampsOff++;
                }
            }
            else {
                //开关落位失败:灯保持点亮(fail-open),交日志复核
                LampsLit += lit.Count;
                CWRMod.Instance.Logger.Warn(
                    $"[L3Lights] 灯房开关({switchX},{switchY})落位失败,{lit.Count}盏灯保持点亮");
            }
            return placed;
        }
    }
}
