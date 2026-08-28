using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Gen
{
    /// <summary>
    /// 废村营造：塌顶残屋、沉水户、断壁、村井。没有一盏灯，这村子空了很久。<br/>
    /// 组团制：2-4 栋共享窄巷成组，组团间留大间距保剪影呼吸；
    /// 全部落在两个村带里，避让洼地与出生台地，足印登记进禁区表
    /// </summary>
    internal static class KiameRuins
    {
        internal static int Huts;
        internal static int Sunken;
        internal static int Wells;

        internal static void Reset() {
            Huts = Sunken = Wells = 0;
        }

        /// <summary>两个村带各走一遍组团铺设</summary>
        internal static void Build() {
            Reset();
            BuildBand(1, KiameMetrics.SunkenHutChanceWest);
            BuildBand(3, KiameMetrics.SunkenHutChanceEast);
        }

        private static void BuildBand(int bandIdx, float sunkenChance) {
            KiameBand band = KiameMetrics.Bands[bandIdx];
            int cursor = band.Left + 16;
            int limit = band.Right - 20;

            while (cursor < limit) {
                int hutCount = WorldGen.genRand.Next(KiameMetrics.ClusterHutMin, KiameMetrics.ClusterHutMax + 1);
                int built = 0;
                for (int i = 0; i < hutCount && cursor < limit; i++) {
                    int width = WorldGen.genRand.Next(KiameMetrics.HutWidthMin, KiameMetrics.HutWidthMax + 1);
                    if (TryBuildHut(cursor, width, sunkenChance)) {
                        built++;
                        cursor += width + WorldGen.genRand.Next(KiameMetrics.AlleyMin, KiameMetrics.AlleyMax + 1);
                    }
                    else {
                        //落不下就往前挪一段再试，别原地死磕
                        cursor += 6;
                    }
                }
                //组团收尾：抽一口村井
                if (built > 0 && WorldGen.genRand.NextFloat() < KiameMetrics.WellChance) {
                    TryBuildWell(cursor + 2);
                }
                cursor += WorldGen.genRand.Next(KiameMetrics.ClusterGapMin, KiameMetrics.ClusterGapMax + 1);
            }
        }

        /// <summary>
        /// 一栋残屋：灰砖基座 + 木骨山墙 + 木坡顶。残破三签：门洞侧、断墙侧、塌顶。
        /// 沉水户内膛下挖两行灌水，门槛进来就是没踝的黑水
        /// </summary>
        private static bool TryBuildHut(int left, int width, float sunkenChance) {
            int right = left + width;             //半开
            if (right >= KiameMetrics.PlayRight - 4) {
                return false;
            }
            //避让：洼地（外扩 2）、出生台地、既有结构
            if (KiamePlans.OverlapsPool(left, right - 1, margin: 2)) {
                return false;
            }
            if (right >= KiameMetrics.SpawnReserveLeft && left < KiameMetrics.SpawnReserveRight) {
                return false;
            }
            if (KiamePlans.OverlapsExclusion(left - 1, right + 1)) {
                return false;
            }

            //地基：以中列地板为准，落差超 4 行的坡不建
            int center = left + width / 2;
            int baseRow = KiamePlans.FloorTopAt(center);
            int minTop = baseRow;
            int maxTop = baseRow;
            for (int x = left; x < right; x++) {
                int top = KiamePlans.FloorTopAt(x);
                minTop = Math.Min(minTop, top);
                maxTop = Math.Max(maxTop, top);
            }
            if (maxTop - minTop > 4) {
                return false;
            }

            //平整：高处削掉、低处用土补（补的格子背景墙同步补上，防漏光），
            //然后整段压一行灰砖基座
            for (int x = left; x < right; x++) {
                int top = KiamePlans.FloorTop[x];
                if (top < baseRow) {
                    for (int y = top; y < baseRow; y++) {
                        KiameTileBrush.ClearCell(x, y);
                    }
                }
                else if (top > baseRow) {
                    for (int y = baseRow; y < top; y++) {
                        KiameTileBrush.SetSolid(x, y, TileID.Mud);
                        KiameTileBrush.SetWall(x, y, WallID.MudUnsafe);
                    }
                }
                KiamePlans.FloorTop[x] = baseRow;
                KiameTileBrush.SetSolid(x, baseRow, TileID.GrayBrick);
            }

            int wallH = WorldGen.genRand.Next(KiameMetrics.HutWallHMin, KiameMetrics.HutWallHMax + 1);
            int wallTop = baseRow - wallH;

            //内膛：清空 + 木墙背景（先满刷，再打洞）
            KiameTileBrush.CarveRect(left + 1, wallTop, right - 1, baseRow, WallID.Wood);
            PunchWallHoles(left + 1, wallTop, right - 1, baseRow);

            //山墙两面：一面开门洞，另一面完好/断墙对半抽
            bool doorOnLeft = WorldGen.genRand.NextBool();
            BuildGableWall(left, baseRow, wallH, doorOnLeft ? GableKind.Doorway
                : WorldGen.genRand.NextBool() ? GableKind.Intact : GableKind.Broken);
            BuildGableWall(right - 1, baseRow, wallH, !doorOnLeft ? GableKind.Doorway
                : WorldGen.genRand.NextBool() ? GableKind.Intact : GableKind.Broken);

            //坡顶
            bool collapsed = WorldGen.genRand.NextFloat() < KiameMetrics.RoofCollapseChance;
            int collapseDir = WorldGen.genRand.NextBool() ? 1 : -1;
            BuildRoof(left, right, wallTop, collapsed, collapseDir, baseRow);

            //沉水户：内膛下挖两行灌黑水，底换淤泥
            if (WorldGen.genRand.NextFloat() < sunkenChance) {
                for (int x = left + 1; x < right - 1; x++) {
                    KiameTileBrush.ClearCell(x, baseRow, WallID.Wood);
                    KiameTileBrush.ClearCell(x, baseRow + 1, WallID.Wood);
                    KiameTileBrush.SetWater(x, baseRow);
                    KiameTileBrush.SetWater(x, baseRow + 1);
                    KiameTileBrush.SetSolid(x, baseRow + 2, TileID.Mud);
                }
                Sunken++;
            }
            else {
                //干屋内饰：残桌残椅低概率，摆不下就算了；蛛网挂角
                ScatterInterior(left, right, baseRow, wallTop);
            }

            KiamePlans.RegisterExclusion(left - 1, right + 1);
            Huts++;
            return true;
        }

        private enum GableKind : byte { Intact, Doorway, Broken }

        private static void BuildGableWall(int x, int baseRow, int wallH, GableKind kind) {
            switch (kind) {
                case GableKind.Intact:
                    for (int y = baseRow - wallH; y < baseRow; y++) {
                        KiameTileBrush.SetSolid(x, y, TileID.WoodBlock);
                    }
                    break;
                case GableKind.Doorway:
                    //底三行留门洞，其上照砌
                    for (int y = baseRow - wallH; y < baseRow - 3; y++) {
                        KiameTileBrush.SetSolid(x, y, TileID.WoodBlock);
                    }
                    break;
                case GableKind.Broken:
                    //只剩底部 1-3 行的断墙茬
                    int stub = WorldGen.genRand.Next(1, 4);
                    for (int y = baseRow - stub; y < baseRow; y++) {
                        KiameTileBrush.SetSolid(x, y, TileID.WoodBlock);
                    }
                    break;
            }
        }

        //坡顶：檐口外挑一列，逐行向脊心收窄；塌顶侧整面缺失，木屑掉进屋里
        private static void BuildRoof(int left, int right, int wallTop, bool collapsed, int collapseDir, int baseRow) {
            int width = right - left;
            int cx2 = left + right - 1;           //双倍中心，免半格偏移
            int ridgeH = 2 + width / 5;
            for (int i = 0; i < ridgeH; i++) {
                float k = i / (float)ridgeH;
                int rowY = wallTop - 1 - i;
                int halfSpan2 = (int)MathF.Round(MathHelper.Lerp(width + 2f, 2f, MathF.Pow(k, 0.8f)));
                for (int x = left - 1; x <= right; x++) {
                    int off2 = x * 2 - cx2;
                    if (Math.Abs(off2) > halfSpan2) {
                        continue;
                    }
                    //塌顶侧：离脊 1 列之外整面缺失
                    if (collapsed && Math.Sign(off2) == collapseDir && Math.Abs(off2) > 3) {
                        continue;
                    }
                    //完好侧也蛀掉些许
                    if (WorldGen.genRand.NextFloat() < 0.12f) {
                        continue;
                    }
                    KiameTileBrush.SetSolid(x, rowY, TileID.WoodBlock);
                }
            }
            //塌下来的木屑：散在屋内地板上
            if (collapsed) {
                int debris = WorldGen.genRand.Next(2, 5);
                for (int i = 0; i < debris; i++) {
                    int x = WorldGen.genRand.Next(left + 1, right - 1);
                    KiameTileBrush.SetSolid(x, baseRow - 1, TileID.WoodBlock);
                    if (WorldGen.genRand.NextBool(3)) {
                        KiameTileBrush.SetSolid(x, baseRow - 2, TileID.WoodBlock);
                    }
                }
            }
        }

        //木墙背景打洞：低频散点 + 一枚椭圆大破洞
        private static void PunchWallHoles(int left, int top, int right, int bottom) {
            for (int x = left; x < right; x++) {
                for (int y = top; y < bottom; y++) {
                    if (WorldGen.genRand.NextFloat() < 0.10f) {
                        KiameTileBrush.SetWall(x, y, WallID.None);
                    }
                }
            }
            if (WorldGen.genRand.NextFloat() < 0.45f && right - left > 4) {
                int hx = WorldGen.genRand.Next(left + 1, right - 1);
                int hy = WorldGen.genRand.Next(top + 1, bottom - 1);
                int r = WorldGen.genRand.Next(1, 3);
                for (int x = hx - r; x <= hx + r; x++) {
                    for (int y = hy - r; y <= hy + r; y++) {
                        if (x >= left && x < right && y >= top && y < bottom
                            && (x - hx) * (x - hx) + (y - hy) * (y - hy) <= r * r + 1) {
                            KiameTileBrush.SetWall(x, y, WallID.None);
                        }
                    }
                }
            }
        }

        //干屋内饰：蛛网挂两角，残家具低概率（放不下就算了，废屋本来就该空）
        private static void ScatterInterior(int left, int right, int baseRow, int wallTop) {
            if (WorldGen.genRand.NextBool()) {
                KiameTileBrush.SetSolid(left + 1, wallTop, TileID.Cobweb);
            }
            if (WorldGen.genRand.NextBool()) {
                KiameTileBrush.SetSolid(right - 2, wallTop, TileID.Cobweb);
            }
            //碎砖堆
            int rubble = WorldGen.genRand.Next(0, 3);
            for (int i = 0; i < rubble; i++) {
                int x = WorldGen.genRand.Next(left + 1, right - 1);
                KiameTileBrush.SetSolid(x, baseRow - 1, TileID.GrayBrick);
                if (WorldGen.genRand.NextBool(3)) {
                    KiameTileBrush.SetSolid(x, baseRow - 2, TileID.GrayBrick);
                }
            }
            //残桌残椅：原版放置自带锚定校验，失败即空屋
            if (WorldGen.genRand.NextBool(3)) {
                KiameTileBrush.TryPlaceObject(WorldGen.genRand.Next(left + 2, right - 3), baseRow - 1, TileID.Tables, 0);
            }
            if (WorldGen.genRand.NextBool(3)) {
                KiameTileBrush.TryPlaceTile(WorldGen.genRand.Next(left + 2, right - 2), baseRow - 1, TileID.Chairs);
            }
        }

        /// <summary>村井：灰砖井沿 + 三宽竖井 + 底两行黑水。往下看不见底，最好也别看</summary>
        private static void TryBuildWell(int left) {
            int right = left + 3;                 //井筒三宽，半开 [left,right)
            if (KiamePlans.OverlapsPool(left - 1, right, margin: 2)
                || KiamePlans.OverlapsExclusion(left - 2, right + 2)
                || right >= KiameMetrics.PlayRight - 4) {
                return;
            }
            if (right >= KiameMetrics.SpawnReserveLeft && left < KiameMetrics.SpawnReserveRight) {
                return;
            }

            int ground = KiamePlans.FloorTopAt(left + 1);
            int depth = WorldGen.genRand.Next(KiameMetrics.WellDepthMin, KiameMetrics.WellDepthMax + 1);

            //井沿：两侧各一格灰砖立起
            KiameTileBrush.SetSolid(left - 1, ground - 1, TileID.GrayBrick);
            KiameTileBrush.SetSolid(right, ground - 1, TileID.GrayBrick);
            //井筒：清空灌底水，筒壁刷灰砖背景
            for (int x = left; x < right; x++) {
                for (int y = ground; y < ground + depth; y++) {
                    if (y >= ground + depth - 2) {
                        KiameTileBrush.SetWater(x, y);
                        KiameTileBrush.SetWall(x, y, WallID.GrayBrick);
                    }
                    else {
                        KiameTileBrush.ClearCell(x, y, WallID.GrayBrick);
                    }
                }
                KiameTileBrush.SetSolid(x, ground + depth, TileID.GrayBrick);
            }

            KiamePlans.RegisterExclusion(left - 2, right + 2);
            Wells++;
        }
    }
}
