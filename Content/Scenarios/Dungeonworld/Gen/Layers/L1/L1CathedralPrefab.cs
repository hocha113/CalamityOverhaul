using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Prefabs;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L1
{
    //====================================================================
    //L1主教堂（ROOMS-L1 §1-#1/#2）：ChapelDraftPrefabs草案的放大演化。
    //保留草案骨架构图：阶梯收分穹顶+中央中空尖塔（吊柱拱廊，下方4高通行）+
    //塔心玫瑰窗+右侧祭坛台+彩窗留位+钟锚留位+左右门插槽。
    //字符画按STRUCTURES §2.3"分段拼接"切三段（西翼40+中殿62+东翼40=142宽），
    //按行拼接后走机器Prefab.Parse（行长不齐/未知字符即抛，fail loud）。
    //
    //构图要点（行号=art行，世界行=floorRow-55+art行）：
    //  西翼x0-39：唱诗席夹层（平台层+钢琴，ROOMS-L1唱诗席）+地面侧廊+
    //             正门Archway5(x0-1,行50-54)+夹层门(行38-40,通西上廊)
    //  中殿x40-101：吊柱拱廊4对（柱距10~12，柱底行50，通行行51-54）+
    //             中央尖塔（壁x62-63/78-79，井心x64-77，之字检修平台每4行）+
    //             阶梯收分穹顶（每2列收1行，§2.4-①）+高拱旗帜+长椅阵
    //  东翼x102-141：祭坛台(+3，1格台阶自动登F3)+祭坛桌/十字像/烛台+
    //             后殿（井站段语义：A路主竖井在x130-134开口，本图对应列
    //             全透明' '保留竖井与平台桥）+检修龛+东侧服务门洞
    //  出生点(SpawnX,204)落在尖塔正下方通行区（对齐断言见Build）
    //====================================================================
    internal static class L1CathedralPrefab
    {
        //===对齐常量（改动布局必须同步改字符画，Build里有断言兜底）===
        internal const int ArtWidth = 142;
        internal const int ArtHeight = 58;
        //art行55=地板首行（世界SpineFloorTop）
        internal const int FloorArtRow = 55;
        //A路主竖井在art内的列区间[130,135)，此5列地板行全透明
        internal const int ShaftArtLeft = 130;
        //尖塔井心art列区间[64,78)，玩家出生列必须落于其中
        internal const int SpireInnerLeft = 64;
        internal const int SpireInnerRight = 78;
        //尖塔壁列（钟楼井身向上延续用）
        internal const int SpireWallLeft = 62;
        //玫瑰窗心（art坐标）与半径
        internal const int RoseArtX = 71;
        internal const int RoseArtY = 26;
        internal const int RoseRadius = 6;
        //祭坛桌art列（圣书摆放用）
        internal const int AltarArtX = 109;
        //祭坛背景尖窗组（code刷彩玻璃墙）
        internal const int LancetArtX = 109;

        //===西翼段（40宽）===
        private static readonly string[] West = [
            "                                        ", //0
            "                                        ", //1
            "                                        ", //2
            "                                        ", //3
            "                                        ", //4
            "                                        ", //5
            "                                        ", //6
            "                                        ", //7
            "                                        ", //8
            "                                        ", //9
            "                                        ", //10
            "                                        ", //11
            "                                        ", //12
            "                                        ", //13
            "                                        ", //14
            "                                        ", //15
            "                                        ", //16
            "                                        ", //17
            "                                        ", //18
            "                                        ", //19
            "                                        ", //20
            "                                        ", //21
            "                                        ", //22
            "                                        ", //23
            "                                        ", //24
            "                                        ", //25
            "                                        ", //26
            "########################################", //27 唱诗席顶板
            "########################################", //28
            "##..................L...................", //29 唱诗席吊灯
            "##......................................", //30
            "##......................................", //31
            "##......................................", //32
            "##......................................", //33
            "##......................................", //34
            "##......................................", //35
            "##......................................", //36
            "##......................................", //37
            "DD......................................", //38 夹层门洞(通西上廊,开放)
            "DD......................................", //39
            "DD......P.....b...b.......c.............", //40 唱诗席钢琴/座椅/烛台
            "##--------------------------------------", //41 夹层平台
            "##..........##..........##..............", //42 平台支柱
            "##..........##..........##..............", //43
            "##..........##..........##..............", //44
            "##..........##..........##..............", //45
            "##..........##..........##........----..", //46 上夹层楼梯
            "##..........##..........##..............", //47
            "##..........##..........##..............", //48
            "##..........##..........##..............", //49
            "DD..........##..........##..............", //50 正门拱洞(50-54)
            "DD..........##..........##....----......", //51 下夹层楼梯
            "DD..........##..........##..............", //52
            "DD..........##..........##..............", //53
            "DD...b...b..##.c....b...##c..b..........", //54 侧廊长椅烛台
            "########################################", //55 地板
            "########################################", //56
            "########################################", //57
        ];

        //===中殿段（62宽，x40-101）===
        private static readonly string[] Nave = [
            "                      ##..............##                      ", //0
            "                      ##..............##                      ", //1
            "                      ##--------------##                      ", //2 塔井平台
            "                      ##..............##                      ", //3
            "                      ##..............##                      ", //4
            "                      ##..............##                      ", //5
            "                      ##--------------##                      ", //6
            "                      ##..............##                      ", //7
            "                      ##..............##                      ", //8
            "                    ####..............####                    ", //9 穹顶收分开始
            "                  ######--------------######                  ", //10
            "                ########..............########                ", //11
            "              ####....##..............##....####              ", //12
            "            ####......##..............##......####            ", //13
            "          ######......##--------------##......######          ", //14
            "        ####f.##......##..............##......##.F####        ", //15 高拱旗帜
            "      ####....##......##..............##......##....####      ", //16
            "    ####.F....##......##..............##......##....f.####    ", //17
            "  ####........##......##--------------##......##........####  ", //18
            "####.f........##......##..............##......##.........F####", //19
            "####..........##......##..............##......##..........####", //20
            ".F##..........##......##..............##......##..........##f.", //21
            "..##..........##......##--------------##......##..........##..", //22
            "..##..........##......##..............##......##..........##..", //23
            "..##..........##......##.......W......##......##..........##..", //24 玫瑰窗心留位
            "..##..........##......##..............##......##..........##..", //25
            "..##..........##......##--------------##......##..........##..", //26
            "..##..........##......##..............##......##..........##..", //27
            "..##..........##......##..............##......##..........##..", //28
            "..##..........##......##..............##......##..........##..", //29
            "..##..........##......##--------------##......##..........##..", //30
            "..##..........##......##..............##......##..........##..", //31
            "..##..........##......##..............##......##..........##..", //32
            "..##..........##......##..............##......##..........##..", //33
            "..##..........##......##--------------##......##..........##..", //34
            "..##..........##......##..............##......##..........##..", //35
            "..##..........##......##..............##......##..........##..", //36
            "..##..........##......##..............##......##..........##..", //37
            "..##..........##......##--------------##......##..........##..", //38
            "..##..........##......##..............##......##..........##..", //39
            "..##..........##......##..............##......##..........##..", //40
            "..##..........##......##..............##......##..........##..", //41
            "..##..........##......##--------------##......##..........##..", //42
            "..##..........##......##..............##......##..........##..", //43
            "..##..........##......##..............##......##..........##..", //44
            "..##..........##......##..............##......##..........##..", //45
            "..##..........##......##--------------##......##..........##..", //46
            "..##..........##......##..............##......##..........##..", //47
            "..##..........##......##..............##......##..........##..", //48
            "..##..........##......##..............##......##..........##..", //49
            ".3##4........3##4.....##--------------##....3##4........3##4..", //50 柱底收拱角
            "..............................................................", //51
            "..............................................................", //52
            ".............................................................#", //53 台阶h2
            ".....c...b........b......................c..b....b...b..n...##", //54 长椅/烛台/天使像/台阶
            "##############################################################", //55
            "##############################################################", //56
            "##############################################################", //57
        ];

        //===东翼段（40宽，x102-141）===
        private static readonly string[] East = [
            "                                        ", //0
            "                                        ", //1
            "                                        ", //2
            "                                        ", //3
            "                                        ", //4
            "                                        ", //5
            "                                        ", //6
            "                                        ", //7
            "                                        ", //8
            "                                        ", //9
            "                                        ", //10
            "                                        ", //11
            "                                        ", //12
            "                                        ", //13
            "                                        ", //14
            "                                        ", //15
            "                                        ", //16
            "                                        ", //17
            "                                        ", //18
            "                                        ", //19
            "##                                      ", //20 祭坛区穹顶延收
            "####                                    ", //21
            "..#############                         ", //22
            "....###########                         ", //23
            "........L........##                     ", //24 祭坛上方吊灯/后殿墙
            ".................##                     ", //25
            ".................##                     ", //26
            ".................##                     ", //27
            ".................##                     ", //28
            ".................##                     ", //29
            ".................##                     ", //30
            ".................##                     ", //31
            ".................##                     ", //32
            ".................##                     ", //33
            ".................##                     ", //34
            ".................##                     ", //35
            ".................##                     ", //36
            ".................##                     ", //37
            ".................##                     ", //38
            ".................##                     ", //39
            ".................#######################", //40 后殿顶板
            ".................#######################", //41
            ".................##..F..L........#######", //42 后殿旗帜吊灯
            ".................##..............#######", //43
            ".................##..............#######", //44
            ".................##..............#######", //45
            ".................##..............#######", //46
            ".................##..............#..####", //47 检修龛
            ".................................#..####", //48 后殿拱洞(48-51)
            ".................................#v.####", //49 龛内花瓶
            ".................................#######", //50
            "...c...A...c..X..................#######", //51 祭坛台家具
            "###################..............DDDDDDD", //52 台面/东门洞
            "####################.............DDD+DDD", //53
            "#####################.m..........DDDDDDD", //54 后殿落地灯
            "############################     #######", //55 竖井开口(透明)
            "############################     #######", //56
            "############################     #######", //57
        ];

        private static Prefab _cathedral;

        /// <summary>主教堂prefab（分段拼接后惰性解析，解析即校验）</summary>
        internal static Prefab Cathedral => _cathedral ??= BuildPrefab();

        private static Prefab BuildPrefab() {
            if (West.Length != ArtHeight || Nave.Length != ArtHeight || East.Length != ArtHeight) {
                throw new System.InvalidOperationException(
                    $"[L1] 教堂分段行数 西{West.Length}/中{Nave.Length}/东{East.Length} != {ArtHeight}");
            }
            var art = new string[ArtHeight];
            for (int y = 0; y < ArtHeight; y++) {
                art[y] = West[y] + Nave[y] + East[y];
            }
            Prefab prefab = Prefab.Parse("L1Cathedral", art, L1Style.Legend);
            SelfCheck(art);
            return prefab;
        }

        //关键格哨兵断言：竖井列透明/尖塔壁/地板行，抓分段拼接的错位
        private static void SelfCheck(string[] art) {
            for (int x = ShaftArtLeft; x < ShaftArtLeft + DungeonworldMetrics.ShaftWidth; x++) {
                for (int y = FloorArtRow; y < ArtHeight; y++) {
                    if (art[y][x] != ' ') {
                        throw new System.InvalidOperationException(
                            $"[L1] 教堂竖井列({x},{y})非透明'{art[y][x]}',会堵死A路主竖井");
                    }
                }
            }
            if (art[0][SpireWallLeft] != '#' || art[0][SpireInnerRight] != '#') {
                throw new System.InvalidOperationException("[L1] 教堂尖塔壁哨兵失败,分段错位");
            }
            if (art[FloorArtRow][0] != '#' || art[FloorArtRow][ArtWidth - 1] != '#') {
                throw new System.InvalidOperationException("[L1] 教堂地板行哨兵失败");
            }
        }

        //===钟室段prefab（ROOMS-L1 §1-#2：顶部钟室14~18w×10~12h）===
        //探出天空缓冲带部分只做剪影：无家具槽，只留钟锚B与侧窗心w（§1结构语法要点）
        private static readonly string[] BellChamberArt = [
            "##################",
            "##################",
            "##..............##",
            "##..w........w..##",
            "##..............##",
            "##......B.......##",
            "##..............##",
            "##..............##",
            "##..............##",
            "##..............##",
            "##..............##",
            "####----------####",
            "####          ####",
        ];

        private static Prefab _bellChamber;
        internal static Prefab BellChamber => _bellChamber ??= Prefab.Parse("L1BellChamber", BellChamberArt, L1Style.Legend);

        //==================== 构建 ====================

        /// <summary>本次生成的教堂左上角世界坐标（装修/预览定位用）</summary>
        internal static Point LastOrigin;

        /// <summary>
        /// 在floorRow（地板首行，gen期=Bands[0].SpineFloorTop）落主教堂。
        /// worldLeft由竖井对齐推导：shaftLeft-ShaftArtLeft；预览模式传入本地伪竖井列。
        /// fullTower=true时钟楼井身一路修到天空缓冲带并封顶钟室（行约20~34），
        /// 预览建议false（短塔，防打穿测试世界地表）。
        /// </summary>
        internal static void Build(int floorRow, int shaftLeft, bool fullTower) {
            int left = shaftLeft - ShaftArtLeft;
            int top = floorRow - FloorArtRow;
            LastOrigin = new Point(left, top);

            Prefab cathedral = Cathedral;
            //几何冻结后再装修（§3.1-3），与机器两遍制一致
            cathedral.StampGeometry(left, top, L1Style.Brick, L1Style.Wall, L1Style.PlatformFrameY);
            FurnishReport report = cathedral.PlaceFurniture(left, top);

            //玫瑰窗：塔心圆盘彩玻璃+Slab过梁圈（wall层，图案素圆【待签字】）
            L1Style.StainedGlassDisk(left + RoseArtX, top + RoseArtY, RoseRadius);
            //祭坛背景三联尖窗（中高侧低）
            L1Style.StainedGlassRect(left + LancetArtX - 1, top + 28, left + LancetArtX + 1, top + 40);
            L1Style.StainedGlassRect(left + LancetArtX - 5, top + 32, left + LancetArtX - 3, top + 39);
            L1Style.StainedGlassRect(left + LancetArtX + 3, top + 32, left + LancetArtX + 5, top + 39);

            //圣书单点：祭坛桌面上（INDEX §3书母题豁免；桌占2高，台面站立行-2）
            int altarX = left + AltarArtX;
            int daisStand = floorRow - 4;
            if (!L1Style.PlaceOnSurface(altarX, daisStand - 2, TileID.Books, WorldGen.genRand.Next(L1Style.BookStyleCount))) {
                CWRMod.Instance.Logger.Warn("[L1] 祭坛圣书放置失败(桌面不可用),跳过");
            }

            //钟楼井身+钟室
            BuildBellTower(left, top, fullTower);

            //做旧签名：蜡泪+烟熏顶（paint层，扫描光源家具，观感【待签字】）
            L1Style.AgeLightsInRect(new Rectangle(left, top, ArtWidth, ArtHeight));

            CWRMod.Instance.Logger.Info(
                $"[L1] 主教堂落成 origin=({left},{top}) 家具placed={report.Placed}"
                + $" rejected={report.Rejected} markers={report.Markers} sockets={cathedral.Sockets.Count}");
        }

        //钟楼井身：尖塔壁向上延续（art顶行往上），井心全宽检修平台每4行，
        //埋地段（世界行60以下段）每12行交替开2x3检修龛；天空段纯剪影（§1语法要点）。
        //顶端盖钟室prefab+阶梯收分尖帽（每行每侧收1列）
        private static void BuildBellTower(int left, int top, bool fullTower) {
            int wallL = left + SpireWallLeft;          //西壁外列
            int innerL = left + SpireInnerLeft;        //井心左列
            int innerR = left + SpireInnerRight;       //井心右列(不含)
            int wallR = innerR + 2;                    //东壁右界(不含)

            //钟室地板行：正式=世界行34（钟室体22-34，行约20达标）；短塔=艺顶上方24行
            int chamberFloor = fullTower ? 34 : top - 24;
            int shaftTop = chamberFloor + 1;           //井身从钟室地板下一行起
            if (shaftTop >= top) {
                CWRMod.Instance.Logger.Warn("[L1] 钟楼井身高度不足,跳过塔身");
                return;
            }

            for (int y = shaftTop; y < top; y++) {
                for (int x = wallL; x < wallL + 2; x++) {
                    TileBrush.SetSolid(x, y, L1Style.Brick);
                }
                for (int x = innerR; x < wallR; x++) {
                    TileBrush.SetSolid(x, y, L1Style.Brick);
                }
                for (int x = innerL; x < innerR; x++) {
                    TileBrush.ClearCell(x, y, L1Style.Wall);
                }
                //与art内塔井平台同相位（art行2≡世界行top+2，模4对齐）
                if (((y - top) % 4 + 4) % 4 == 2) {
                    TileBrush.PlatformRow(innerL, innerR, y, L1Style.PlatformFrameY);
                }
            }

            //埋地段检修龛：每12行交替左右，龛口穿壁+岩内2x3龛体，隔一龛放蜡烛
            int nicheIndex = 0;
            for (int y = System.Math.Max(shaftTop + 6, DungeonworldMetrics.SkyRows); y < top - 3; y += 12) {
                bool leftSide = (nicheIndex & 1) == 0;
                int holeL = leftSide ? wallL : innerR;
                int pocketL = leftSide ? wallL - 2 : wallR;
                for (int dy = 0; dy < 3; dy++) {
                    TileBrush.ClearCell(holeL, y + dy, L1Style.Wall);
                    TileBrush.ClearCell(holeL + 1, y + dy, L1Style.Wall);
                    TileBrush.ClearCell(pocketL, y + dy, L1Style.WallSlab);
                    TileBrush.ClearCell(pocketL + 1, y + dy, L1Style.WallSlab);
                }
                if ((nicheIndex & 1) == 0) {
                    L1Style.PlaceOnSurface(pocketL + (leftSide ? 0 : 1), y + 2, TileID.Candles, L1Style.StyleCandle);
                }
                nicheIndex++;
            }

            //钟室prefab（18宽与塔身同宽对齐）+侧窗彩玻璃+尖帽
            Prefab chamber = BellChamber;
            int chamberLeft = wallL;
            int chamberTop = chamberFloor - (chamber.Height - 2);
            chamber.StampGeometry(chamberLeft, chamberTop, L1Style.Brick, L1Style.WallSlab, L1Style.PlatformFrameY);
            FurnishReport chamberReport = chamber.PlaceFurniture(chamberLeft, chamberTop);
            L1Style.StainedGlassRect(chamberLeft + 3, chamberTop + 3, chamberLeft + 6, chamberTop + 10);
            L1Style.StainedGlassRect(chamberLeft + 12, chamberTop + 3, chamberLeft + 15, chamberTop + 10);

            //尖帽：阶梯收分到2列尖
            int capBase = chamberTop - 1;
            int shrink = 1;
            while (wallL + shrink < wallR - shrink && shrink <= 8) {
                for (int x = wallL + shrink; x < wallR - shrink; x++) {
                    TileBrush.SetSolid(x, capBase - shrink + 1, L1Style.Brick);
                }
                shrink++;
            }

            CWRMod.Instance.Logger.Info(
                $"[L1] 钟楼落成 井身[{shaftTop},{top}) 钟室top={chamberTop}"
                + $" 钟室markers={chamberReport.Markers}(钟锚/窗心留位,资产波接管)");
        }
    }
}
