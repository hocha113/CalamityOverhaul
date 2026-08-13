using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L1;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Prefabs;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L7
{
    //====================================================================
    //L7倒吊中殿（ROOMS-L7 §1-#3/#4/#5）：L1CathedralPrefab（只读镜像素材源）
    //经机器FlipY（§2.3镜像五层法1+2+3层自动）后的倒相定制集：
    //  ·承重层：原天花成地板后加铺2格实心拉平（§2.4-⑦，避开朝下穹顶slope怪坑）
    //  ·舞台再开凿：倒吊通行生成器职责（§2.3-4镜像不管通行）——切开吊柱下段
    //    打通东西向Boss舞台，柱残端补斜切收尾（吊柱拱廊倒相）
    //  ·封门：镜像侧门全封，"顶部唯一入口"=原地板竖井槽翻上来的天窗（花名册#3）
    //  ·垂钟井：镜像尖塔向下延伸60行+倒像钟室（垂钟龛）收底，钟体剪影砖砌
    //  ·倒置玫瑰窗（紫彩玻璃，同窗异相）+镜像尖窗组+倒挂祭坛区（镜像悬空祭坛台
    //    =天花板上的倒吊台，其正下方舞台地板留Boss触发位铭文圆斑）
    //镜像行号映射：新行=57-旧行。关键对位（世界行=top+新行）：
    //  原地板55-57→顶板0-2（竖井槽x130-134成顶部天窗）
    //  原通行区51-54→顶廊3-6 | 原吊柱21-50→垂柱7-36 | 原穹顶9-20→朝下穹壳37-48
    //  原唱诗席顶板27-28→西厢库房地板29-30（圣物终库，花名册#6）
    //  原后殿顶板40-41→东厢舱地板16-17（倒吊侧廊东段）
    //  原祭坛台51-54→倒吊祭坛台3-5（x102-120悬于舞台上方）
    //====================================================================
    internal static class L7InvertedCathedral
    {
        //===镜像对位常量（源自L1CathedralPrefab公开常量+其字符画结构，
        //   改L1布局会被下方SelfCheckWorld哨兵抓住）===
        internal const int ArtW = L1CathedralPrefab.ArtWidth;    //142
        internal const int ArtH = L1CathedralPrefab.ArtHeight;   //58
        //顶部天窗（原竖井槽翻上）：x[130,135)，穿透顶板0-2三行
        internal const int HatchLeft = L1CathedralPrefab.ShaftArtLeft;
        internal const int HatchWidth = DungeonworldMetrics.ShaftWidth;
        //中央塔管（垂钟井井身）壁列与井心
        internal const int TubeWallL = L1CathedralPrefab.SpireWallLeft; //62
        internal const int TubeInnerL = L1CathedralPrefab.SpireInnerLeft; //64
        internal const int TubeInnerR = L1CathedralPrefab.SpireInnerRight; //78
        //倒置玫瑰窗心（镜像坐标）
        internal const int RoseX = L1CathedralPrefab.RoseArtX; //71
        internal const int RoseY = ArtH - 1 - L1CathedralPrefab.RoseArtY; //31

        //舞台再开凿：净空行[StageClearTop,BearingTop)，残端斜切行=StageClearTop
        internal const int StageClearTop = 30;
        internal const int BearingTop = 37;   //承重层2行：37-38
        //舞台x区间[44,102)：西界保留x42-43整柱为舞台西墙（西侧留空洞归终库深渊口）
        internal const int StageLeft = 44;
        internal const int StageRight = 102;
        //需切开的垂柱对（x左列；x42柱不切）
        private static readonly int[] StumpCols = [54, 62, 78, 86, 98];

        //东厢舱（镜像后殿）：地板行16、内膛x[121,134)行[6,16)、西壁x119-120
        internal const int PodFloorRow = 16;
        internal const int PodDoorTop = 12;   //西壁门洞行[12,15)
        //西厢库房（镜像唱诗席=圣物终库）：地板行29-30、内膛x[2,38)行[3,29)
        internal const int VaultFloorRow = 29;
        //库房天窗：顶板x[16,21)行[0,3)
        internal const int VaultHatchLeft = 16;

        //垂钟井延伸段行数（prefab底行58起向下）与倒像钟室
        internal const int WellRows = 60;
        internal const int NicheTop = ArtH + WellRows;          //118（art相对行）
        internal const int NicheHeight = 13;                    //BellChamber高
        internal const int NicheBottom = NicheTop + NicheHeight; //131
        /// <summary>结构总深（含垂钟龛），Content做红线与空腔预算用</summary>
        internal const int TotalDepth = NicheBottom;

        private static Prefab _inverted;
        private static Prefab _invertedNiche;

        /// <summary>倒吊教堂prefab（FlipY惰性求值；文本级变换后重解析，解析即校验）</summary>
        internal static Prefab Inverted => _inverted ??= L1CathedralPrefab.Cathedral.FlipY();
        /// <summary>垂钟龛=钟室prefab的倒像（入口翻到顶，地板翻到底）</summary>
        internal static Prefab InvertedNiche => _invertedNiche ??= L1CathedralPrefab.BellChamber.FlipY();

        /// <summary>本次构建左上角（预览/装修定位用）</summary>
        internal static Point LastOrigin;

        /// <summary>
        /// 在(left,top)落倒吊教堂全套：镜像盖章→倒相定制几何→家具（含对偶槽）→
        /// 链束→彩窗/做旧。周边空腔与吊挂链束的锚定环境由调用方（L7Content/预览）备好。
        /// 几何冻结先于装修（§3.1-3），一切放置拒绝即跳过+记日志（F9）。
        /// </summary>
        internal static void Build(int left, int top) {
            LastOrigin = new Point(left, top);
            Prefab inverted = Inverted;

            //===1) 镜像盖章（布尔几何/slope对偶/槽对偶已由FlipY机器完成）===
            inverted.StampGeometry(left, top, L7Style.Brick, L7Style.Wall, L7Style.PlatformFrameY);
            //镜像通行不参与（§2.3-4）：唱诗席平台翻进终库内膛，清掉以免半层挡路
            ClearPlatforms(left + 2, top + 3, left + 38, top + VaultFloorRow);

            //===2) 倒相定制：封门（花名册#3"顶部唯一Door，无其他开口"）===
            //镜像正门(行3-7)/镜像夹层门(行17-19)/镜像东门(行3-5)全封实
            SealRect(left + 0, top + 3, left + 2, top + 8);
            SealRect(left + 0, top + 17, left + 2, top + 20);
            SealRect(left + 135, top + 3, left + 142, top + 6);

            //===3) 承重层：行37-38实心拉平（§2.4-⑦），塔管井心留井口平台===
            for (int y = top + BearingTop; y < top + BearingTop + 2; y++) {
                for (int x = left + 40; x < left + TubeWallL; x++) {
                    TileBrush.SetSolid(x, y, L7Style.Brick);
                }
                for (int x = left + TubeInnerR + 2; x < left + StageRight; x++) {
                    TileBrush.SetSolid(x, y, L7Style.Brick);
                }
            }
            //井口：PlatformGap语义，盖平台防误落（§2.1），▼下行入垂钟井
            TileBrush.PlatformRow(left + TubeInnerL, left + TubeInnerR, top + BearingTop, L7Style.PlatformFrameY);

            //===4) 舞台再开凿（倒吊通行生成器，§2.3-4）：切开垂柱下段贯通东西===
            TileBrush.CarveRect(left + StageLeft, top + StageClearTop,
                left + StageRight, top + BearingTop, L7Style.Wall);
            //垂柱残端斜切收尾（与镜像柱冠'1##2'同语法的下垂尖端）
            foreach (int px in StumpCols) {
                TileBrush.SetSloped(left + px, top + StageClearTop, L7Style.Brick, SlopeType.SlopeDownLeft);
                TileBrush.SetSloped(left + px + 1, top + StageClearTop, L7Style.Brick, SlopeType.SlopeDownRight);
            }

            //===5) 东厢舱通行：西壁门洞（行12-14）+舱内检修檐（回程跳点）===
            TileBrush.CarveRect(left + 119, top + PodDoorTop, left + 121, top + PodDoorTop + 3, L7Style.Wall);
            for (int x = left + 115; x < left + 119; x++) {
                TileBrush.SetSolid(x, top + 15, L7Style.Brick);
            }
            TileBrush.SetSloped(left + 114, top + 15, L7Style.Brick, SlopeType.SlopeDownLeft);

            //===6) 圣物终库天窗：顶板开5宽井口（东留2列自由落体道，西3列挂链）===
            TileBrush.CarveRect(left + VaultHatchLeft, top, left + VaultHatchLeft + 5, top + 3, L7Style.Wall);

            //===7) 垂钟井延伸：井身向下60行（镜像BuildBellTower的向下版）===
            int wellTop = top + ArtH;
            int nicheTop = top + NicheTop;
            for (int y = wellTop; y < nicheTop; y++) {
                TileBrush.SetSolid(left + TubeWallL, y, L7Style.Brick);
                TileBrush.SetSolid(left + TubeWallL + 1, y, L7Style.Brick);
                TileBrush.SetSolid(left + TubeInnerR, y, L7Style.Brick);
                TileBrush.SetSolid(left + TubeInnerR + 1, y, L7Style.Brick);
                for (int x = left + TubeInnerL; x < left + TubeInnerR; x++) {
                    TileBrush.ClearCell(x, y, L7Style.Wall);
                }
                //之字检修平台每4行（与prefab内塔井平台同相位：art行≡3 mod 4）
                //井心x69-71留空走中央钟链，平台分列两侧仍可踏
                if (((y - top) % 4 + 4) % 4 == 3) {
                    TileBrush.PlatformRow(left + TubeInnerL, left + 69, y, L7Style.PlatformFrameY);
                    TileBrush.PlatformRow(left + 72, left + TubeInnerR, y, L7Style.PlatformFrameY);
                }
            }

            //===8) 垂钟龛：倒像钟室收底（入口在顶，地板在底=全层最低可通行点）===
            Prefab niche = InvertedNiche;
            niche.StampGeometry(left + TubeWallL, nicheTop, L7Style.Brick, L7Style.Wall, L7Style.PlatformFrameY);

            //===9) 终钟剪影（终钟TP归资产波，本波砖砌剪影+链悬吊，INDEX §8）===
            BuildBellSilhouette(left + TubeWallL, nicheTop);

            //===哨兵自检：几何冻结前抓镜像/定制错位（fail loud，§3.1-2）===
            SelfCheckWorld(left, top);

            //===10) 家具：镜像槽对偶落位（吊灯↔烛台已由对偶表换槽）+定制件===
            FurnishReport report = inverted.PlaceFurniture(left, top);
            FurnishFixed(left, top);

            //===11) 链束（全部≥3宽，INDEX §3）===
            int chainCells = PlaceChains(left, top);

            //===12) 彩窗+做旧（wall/paint层收尾）===
            DressWallsAndPaint(left, top);

            CWRMod.Instance.Logger.Info(
                $"[L7] 倒吊教堂落成 origin=({left},{top})"
                + $" 家具placed={report.Placed} rejected={report.Rejected} markers={report.Markers}"
                + $" 镜像删槽={Inverted.MirrorDroppedSlots}+龛{InvertedNiche.MirrorDroppedSlots}"
                + $" 封门=3 垂柱残端={StumpCols.Length} 链束格={chainCells}"
                + $" sockets(倒)={inverted.Sockets.Count}(全封,顶部天窗为唯一入口)");
        }

        //清掉镜像残留平台（只动平台格，墙与砖不动）
        private static void ClearPlatforms(int leftX, int topY, int rightX, int bottomY) {
            for (int x = leftX; x < rightX; x++) {
                for (int y = topY; y < bottomY; y++) {
                    if (!WorldGen.InWorld(x, y)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType == TileID.Platforms) {
                        TileBrush.ClearCell(x, y, L7Style.Wall);
                    }
                }
            }
        }

        //封实：镜像门洞回填砖+墙（"无其他开口"事实条款）
        private static void SealRect(int leftX, int topY, int rightX, int bottomY) {
            for (int x = leftX; x < rightX; x++) {
                for (int y = topY; y < bottomY; y++) {
                    TileBrush.SetSolid(x, y, L7Style.Brick);
                }
            }
        }

        //终钟剪影：3宽吊链+砖砌钟体（4宽身+6宽裙口斜切+钟舌），悬于龛心
        //nicheLeft=龛prefab左缘（=塔管西壁列）
        private static void BuildBellSilhouette(int nicheLeft, int nicheTop) {
            int cx = nicheLeft + 9;               //龛心（18宽龛的中缝右列）
            int chainTop = nicheTop + 2;          //入口平台下方起链
            L7Style.ChainBundle(cx - 1, 3, chainTop, 2);
            int bellTop = nicheTop + 4;
            //钟身2行×4宽
            for (int y = bellTop; y < bellTop + 2; y++) {
                for (int x = cx - 2; x < cx + 2; x++) {
                    TileBrush.SetSolid(x, y, L7Style.Brick);
                }
            }
            //裙口6宽：两端斜切外张
            TileBrush.SetSloped(cx - 3, bellTop + 2, L7Style.Brick, SlopeType.SlopeDownLeft);
            for (int x = cx - 2; x < cx + 2; x++) {
                TileBrush.SetSolid(x, bellTop + 2, L7Style.Brick);
            }
            TileBrush.SetSloped(cx + 2, bellTop + 2, L7Style.Brick, SlopeType.SlopeDownRight);
            //钟舌
            TileBrush.SetSolid(cx - 1, bellTop + 3, L7Style.Brick);
        }

        //定制家具：终库箱+仪式光引线+高拱幡（帧不镜像的旗帜由本层重摆，压暗）
        private static void FurnishFixed(int left, int top) {
            //圣物终库：锁金箱+定点挂画（Boss后门禁归运行时【待定】，几何上天窗可达）
            L7Style.PlaceVaultChest(left + 24, top + VaultFloorRow - 1);
            if (!L7Style.PlacePainting(left + 30, top + VaultFloorRow - 5)) {
                CWRMod.Instance.Logger.Warn("[L7] 终库挂画放置失败,跳过");
            }

            //仪式光引线（ROOMS-L7 §2.2：入口→祭坛下方，每处1~2点，非撒布）
            L7Style.PlaceTorch(left + 128, top + PodFloorRow - 2, L7Style.TorchDemon);  //东厢舱
            L7Style.PlaceTorch(left + 105, top + 32, L7Style.TorchDemon);               //倒吊祭坛台下
            L7Style.PlaceTorch(left + 48, top + 34, L7Style.TorchDemon);                //舞台西端
            L7Style.PlaceTorch(left + 6, top + VaultFloorRow - 2, L7Style.TorchDemon);  //终库
            L7Style.PlaceTorch(left + TubeWallL + 4, top + NicheTop + 9, L7Style.TorchBone); //垂钟龛（骨火把）

            //高拱幡：吊在顶板下缘俯瞰舞台（原高拱旗帜镜像被判删，本层重摆+深紫压暗）
            int[] bannerCols = [48, 58, 84, 94];
            for (int i = 0; i < bannerCols.Length; i++) {
                int x = left + bannerCols[i];
                int style = (i & 1) == 0 ? L7Style.StyleBannerA : L7Style.StyleBannerB;
                if (L7Style.TryPlaceObject(x, top + 3, TileID.Banners, style)) {
                    L7Style.PaintTileArea(x, top + 3, x, top + 5, TileID.Banners, L7Style.PaintPurple);
                }
                else {
                    CWRMod.Instance.Logger.Warn($"[L7] 高拱幡@({x},{top + 3})放置失败,跳过");
                }
            }
        }

        //prefab辖域内链束：全部≥3宽结构链（通行路径的2x3包络已避让——
        //P80洪泛把链视为不可通行，链束一律贴在包络路线侧旁）
        private static int PlaceChains(int left, int top) {
            int cells = 0;
            //天窗降链：贴天窗东三列（x132-134），西两列留包络下落道；锚=顶板
            cells += L7Style.ChainBundle(left + 132, 3, top + 3, PodFloorRow - 3);
            //祭坛台降链：锚=倒吊台底（行5实心），垂到东露台上方；东侧留门洞包络路
            cells += L7Style.ChainBundle(left + 110, 3, top + 6, 28);
            //终库回程链：贴天窗西侧板下（x13-15），落体道x19-20不占
            cells += L7Style.ChainBundle(left + 13, 3, top + 3, VaultFloorRow - 3);
            //垂钟井中央钟链：井心x69-71，延伸段平台已让缝，直达垂钟龛入口
            cells += L7Style.ChainBundle(left + 69, 3, top + ArtH + 1, WellRows - 1);
            //建筑下腹断链垂端（"垂落的巨链末端"）：逐列贴最低实心向下悬
            cells += L7Style.ChainBundleBelowSolid(left + 34, 3, top, top + ArtH, 22);
            cells += L7Style.ChainBundleBelowSolid(left + 88, 3, top, top + ArtH, 30);
            cells += L7Style.ChainBundleBelowSolid(left + 106, 3, top, top + ArtH, 18);
            return cells;
        }

        //彩窗与做旧：倒置玫瑰窗/镜像尖窗组/龛窗（紫）+倒挂蜡泪+冥紫染
        private static void DressWallsAndPaint(int left, int top) {
            //倒置玫瑰窗：同一扇窗的紫相（LOADING-SCREEN §5-VII，图案素圆【待签字】）
            L7Style.RoseWindowDisk(left + RoseX, top + RoseY, L1CathedralPrefab.RoseRadius);

            //镜像祭坛尖窗组（原L1代码刷窗行28-40/32-39的行倒序）——中高侧低翻成中深侧浅
            int lx = left + L1CathedralPrefab.LancetArtX;
            L7Style.GlassRect(lx - 1, top + 17, lx + 2, top + 29);
            L7Style.GlassRect(lx - 5, top + 18, lx - 2, top + 25);
            L7Style.GlassRect(lx + 3, top + 18, lx + 6, top + 25);

            //垂钟龛侧窗（镜像钟室侧窗位，'w'槽已被对偶表判删，本层紫玻璃重制）
            int nl = left + TubeWallL;
            int nt = top + NicheTop;
            L7Style.GlassRect(nl + 3, nt + 4, nl + 6, nt + 9);
            L7Style.GlassRect(nl + 12, nt + 4, nl + 15, nt + 9);

            //冥紫变调主漆：全构体蓝砖刷深紫（含承重层/井身/钟体/残端）
            long painted = L7Style.PurpleSweep(new Rectangle(
                left - 2, top - 2, ArtW + 4, TotalDepth + 4));
            //对偶换位家具/门板压暗（帧不翻，身份靠漆【待签字】）
            var dress = new Rectangle(left, top, ArtW, ArtH);
            L7Style.PaintTileArea(dress.Left, dress.Top, dress.Right - 1, dress.Bottom - 1,
                TileID.Chandeliers, L7Style.PaintPurple);
            L7Style.PaintTileArea(dress.Left, dress.Top, dress.Right - 1, dress.Bottom - 1,
                TileID.Candelabras, L7Style.PaintPurple);
            L7Style.PaintTileArea(dress.Left, dress.Top, dress.Right - 1, dress.Bottom - 1,
                TileID.ClosedDoor, L7Style.PaintPurple);

            //倒挂蜡泪+冥紫染：垂柱残端逐个（蜡凝在原地板家具位=今日天花，垂向深渊）
            foreach (int px in StumpCols) {
                L7Style.WaxDrip(left + px, top + StageClearTop + 1, 3);
                L7Style.WaxDrip(left + px + 1, top + StageClearTop + 1, 2);
                L7Style.PurpleWallDisk(left + px, top + StageClearTop, 2);
            }
            //Boss触发位环形铭文（倒吊祭坛正下方地板，paint层【建议】，实体归C/运行时路）
            L7Style.PurpleWallDisk(left + 109, top + 34, 3);
            //吊灯族蜡泪（对偶表换来的顶锚光源）
            L7Style.AgeInvertedInRect(new Rectangle(left, top, ArtW, ArtH));

            CWRMod.Instance.Logger.Info($"[L7] 变调收尾 紫漆砖={painted} 玫瑰窗=({left + RoseX},{top + RoseY})");
        }

        //关键格哨兵：镜像对位/定制几何错位在装修前抛出（坐标+责任方）
        private static void SelfCheckWorld(int left, int top) {
            void Expect(bool cond, string what) {
                if (!cond) {
                    throw new System.InvalidOperationException(
                        $"[L7] 倒吊教堂哨兵失败:{what}，L1素材源或镜像定制常量已错位");
                }
            }
            bool Solid(int x, int y) => Main.tile[x, y].HasTile
                && Main.tileSolid[Main.tile[x, y].TileType];
            bool Air(int x, int y) => !Main.tile[x, y].HasTile;

            Expect(Solid(left + 10, top + 1) && Solid(left + 120, top + 1), "顶板(原地板)非实心");
            Expect(Air(left + 132, top + 1), "顶部天窗(原竖井槽)被堵");
            Expect(Air(left + 50, top + 33) && Air(left + 90, top + 33), "舞台净空行33非空");
            Expect(Solid(left + 50, top + BearingTop) && Solid(left + 90, top + BearingTop), "承重层缺失");
            Expect(Solid(left + 126, top + PodFloorRow), "东厢舱地板(原后殿顶板)缺失");
            Expect(Air(left + 126, top + 10), "东厢舱内膛被堵");
            Expect(Solid(left + 20, top + VaultFloorRow), "终库地板(原唱诗席顶板)缺失");
            Expect(Air(left + 20, top + 20), "终库内膛被堵");
            Expect(Solid(left + TubeWallL, top + 45) && Solid(left + TubeInnerR, top + 45), "塔管壁行45缺失");
            Expect(Air(left + 70, top + 45), "塔管井心被堵");
            Expect(Air(left + 70, top + NicheTop + 9), "垂钟龛内膛被堵");
        }
    }
}
