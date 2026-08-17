using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen
{
    /// <summary>
    /// 湖畔村轮廓：民居、望楼、枯树三种形，与天幕 <c>KiyumeSky.fx</c> 的 villageRow 抽签同构——
    /// 抬头看见的剪影，低头走进去要是同一个村子。<br/>
    /// 本轮只做外壳与门窗洞，屋内不摆家具（内部结构后续再说）。
    /// 房子必须平放，所以每栋先削/垫地基并回写 <see cref="KiyumePlans.FloorTop"/>，
    /// 撒布 pass 才不会把树种在屋顶上
    /// </summary>
    internal static class KiyumeVillage
    {
        //墙体与瓦：在血暮光照下压成暗红，别用亮木
        private const ushort WallTile = TileID.SpookyWood;
        private const ushort RoofTile = TileID.RedDynastyShingles;
        private const ushort FoundationTile = TileID.Ash;
        private const ushort InnerWall = WallID.SpookyWood;

        internal static int Huts;
        internal static int Towers;
        internal static int Torches;

        internal static void Reset() {
            Huts = Towers = Torches = 0;
        }

        internal static void Build() {
            Reset();
            //出生平台留白：别让玩家在墙里醒过来
            int spawnLeft = KiyumeMetrics.SpawnX - KiyumeMetrics.SpawnFlatCols;
            int spawnRight = KiyumeMetrics.SpawnX + KiyumeMetrics.SpawnFlatCols;

            int x = KiyumeMetrics.VillageLeft + 24;
            int right = KiyumeMetrics.GroveLeft - 30;
            while (x < right) {
                float roll = WorldGen.genRand.NextFloat();
                int width;
                if (x + 24 > spawnLeft && x - 24 < spawnRight) {
                    //跨出生带整段跳过
                    x = spawnRight + 8;
                    continue;
                }

                if (roll < 0.14f) {
                    //空地：村里的巷口与空场，剪影要有呼吸
                    width = WorldGen.genRand.Next(22, 42);
                }
                else if (roll < 0.26f) {
                    width = BuildTower(x) + WorldGen.genRand.Next(16, 30);
                }
                else {
                    width = BuildHut(x, ruined: roll > 0.88f) + WorldGen.genRand.Next(11, 26);
                }
                x += Math.Max(width, 8);
            }
        }

        //民居：身比檐窄，坡脊出檐，正面开门，山墙开窗；三成人家屋里点着灯
        private static int BuildHut(int left, bool ruined) {
            int w = WorldGen.genRand.Next(10, 18);
            int h = WorldGen.genRand.Next(6, 10);
            int eave = 2;
            int roofH = WorldGen.genRand.Next(4, 7);
            if (left + w + eave >= KiyumeMetrics.PlayRight) {
                return w;
            }

            int ground = HighestGround(left - eave, left + w + eave);
            Flatten(left - eave, left + w + eave, ground);

            int bodyTop = ground - h;
            //外壳一格厚
            KiyumeTileBrush.FillRect(left, bodyTop, left + w, ground, WallTile);
            KiyumeTileBrush.CarveRect(left + 1, bodyTop + 1, left + w - 1, ground, InnerWall);

            BuildRoof(left, left + w, bodyTop, roofH, eave);

            //门洞：正面偏一侧，2 宽 3 高
            int doorX = left + 2 + WorldGen.genRand.Next(Math.Max(w - 6, 1));
            KiyumeTileBrush.CarveRect(doorX, ground - 3, doorX + 2, ground, InnerWall);

            //山墙窗：一格洞，火光从这里漏出去给雾吃
            int winY = bodyTop + Math.Max(h / 3, 1);
            int winX = WorldGen.genRand.NextBool() ? left : left + w - 1;
            KiyumeTileBrush.CarveRect(winX, winY, winX + 1, winY + 2, InnerWall);

            if (ruined) {
                Ruin(left, bodyTop, w, h, roofH, eave);
            }
            else if (WorldGen.genRand.NextFloat() < 0.34f) {
                LightInside(left + 2, ground - 1, left + w - 2);
            }

            Huts++;
            return w + eave * 2;
        }

        //望楼：窄高一柱，脊更陡，顶窗常明——雾涨上来时它是最后沉没的东西
        private static int BuildTower(int left) {
            int w = WorldGen.genRand.Next(5, 8);
            int h = WorldGen.genRand.Next(14, 22);
            int eave = 2;
            int roofH = WorldGen.genRand.Next(5, 8);
            if (left + w + eave >= KiyumeMetrics.PlayRight) {
                return w;
            }

            int ground = HighestGround(left - eave, left + w + eave);
            Flatten(left - eave, left + w + eave, ground);

            int bodyTop = ground - h;
            KiyumeTileBrush.FillRect(left, bodyTop, left + w, ground, WallTile);
            KiyumeTileBrush.CarveRect(left + 1, bodyTop + 1, left + w - 1, ground, InnerWall);
            BuildRoof(left, left + w, bodyTop, roofH, eave);

            //底层门洞 + 每隔几格一道楼板缺口，读得出是能上人的塔
            KiyumeTileBrush.CarveRect(left + 1, ground - 3, left + 3, ground, InnerWall);
            for (int y = bodyTop + 4; y < ground - 3; y += 5) {
                KiyumeTileBrush.FillRect(left + 1, y, left + w - 1, y + 1, WallTile);
                int gap = left + 1 + WorldGen.genRand.Next(Math.Max(w - 4, 1));
                KiyumeTileBrush.CarveRect(gap, y, gap + 2, y + 1, InnerWall);
            }

            //顶窗
            KiyumeTileBrush.CarveRect(left + w / 2, bodyTop + 1, left + w / 2 + 1, bodyTop + 3, InnerWall);
            LightInside(left + 1, bodyTop + 3, left + w - 2);

            Towers++;
            return w + eave * 2;
        }

        //坡脊：檐口外挑，逐层收窄到脊头，屋顶下面那一层是檐板
        private static void BuildRoof(int left, int right, int bodyTop, int roofH, int eave) {
            int span = right - left + eave * 2;
            for (int i = 0; i < roofH; i++) {
                int inset = (int)MathF.Round(i * (span - 2) / (2f * roofH));
                int rl = left - eave + inset;
                int rr = right + eave - inset;
                if (rr - rl < 1) {
                    break;
                }
                KiyumeTileBrush.FillRect(rl, bodyTop - 1 - i, rr, bodyTop - i, RoofTile);
            }
        }

        //残屋：屋脊塌掉一段，墙上啃几个洞。村子不能整整齐齐，那不是记忆的样子
        private static void Ruin(int left, int bodyTop, int w, int h, int roofH, int eave) {
            int holeLeft = left + WorldGen.genRand.Next(Math.Max(w / 3, 1));
            int holeW = WorldGen.genRand.Next(3, Math.Max(w - 2, 4));
            KiyumeTileBrush.CarveRect(holeLeft, bodyTop - roofH - 1, holeLeft + holeW, bodyTop, WallID.None);
            for (int i = 0; i < 4; i++) {
                int hx = left + WorldGen.genRand.Next(w);
                int hy = bodyTop + WorldGen.genRand.Next(Math.Max(h - 1, 1));
                KiyumeTileBrush.CarveRect(hx, hy, hx + 1, hy + 1, InnerWall);
            }
        }

        //屋里点灯：火把要有实心落脚点，放不下就算了，不为一盏灯记日志
        private static void LightInside(int left, int floorRow, int right) {
            if (right <= left) {
                return;
            }
            int tx = left + WorldGen.genRand.Next(right - left + 1);
            if (WorldGen.PlaceTile(tx, floorRow, TileID.Torches, true, false, -1, 0)) {
                Torches++;
            }
        }

        //取区间内最高的地面行：房子平放在最高点上，低处靠地基垫起来
        private static int HighestGround(int left, int right) {
            int best = int.MaxValue;
            for (int x = left; x < right; x++) {
                best = Math.Min(best, KiyumePlans.FloorTopAt(x));
            }
            return best == int.MaxValue ? (int)KiyumeMetrics.BaseFloorAt(left) : best;
        }

        //削高垫低到同一行，并回写规划态
        private static void Flatten(int left, int right, int row) {
            int[] top = KiyumePlans.FloorTop;
            for (int x = left; x < right; x++) {
                if (x < 0 || x >= Main.maxTilesX) {
                    continue;
                }
                int cur = KiyumePlans.FloorTopAt(x);
                if (cur > row) {
                    KiyumeTileBrush.FillRect(x, row, x + 1, cur, FoundationTile);
                }
                else if (cur < row) {
                    KiyumeTileBrush.CarveRect(x, cur, x + 1, row);
                }
                if (top != null && x < top.Length) {
                    top[x] = row;
                }
            }
        }
    }
}
