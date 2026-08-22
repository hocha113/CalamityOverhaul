using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L5
{
    //====================================================================
    //L5有机坑道凿刻器，原版MakeDungeon游标随机游走(RESEARCH §1.2a DungeonHalls
    //WorldGen.cs:30454-30972拆解)的参数化重写,作为图边的第三种几何形态
    //(STRUCTURES §2.1裁决3/§2.5边形态"游走走廊")。层内自包含,只服务L5。
    //
    //凿刻语法(壳-膛盖章,F29):每步以游标为心盖一章，外壳矩形填砖但只动
    //"未挖区"(墙即已挖标记:凡带地牢墙的格视为已挖,自动免填→笔画重叠/
    //撞进房间socket时无缝融合);内膛矩形清空+刷墙。
    //
    //三个有机旋钮:宽度呼吸(半宽随计时器±1漂移)/方向漂移(转向概率+垂直
    //抖动)/之字横漂(竖向段横向分量周期反号,原版竖直走廊语法F29)。
    //
    //约束与决定论:
    //- 凿刻严格钳制在调用方给的包络矩形内(游标回弹+逐格越界跳过),包络
    //  已由调用方过ctx.Grid.CanReserve预检,宏观足印/跨层预留构造性避开;
    //- 随机全走WorldGen.genRand(F22),每步消耗顺序固定(呼吸→转向→之字),
    //  同种子逐格复现;
    //- 写入只走TileBrush;横档平台延迟记账(后续盖章会清掉先落的平台),
    //  由调用方在全部凿刻完成后FlushCrossbars统一铺设;
    //- 随机游走不承担连通性:步数预算耗尽由末段直线兜底强制抵达终点,
    //  "几何必达"是构造保证(§1.4),不靠运气。
    //====================================================================
    internal static class L5TunnelCarver
    {
        internal struct TunnelParams
        {
            internal int HalfWidthMin, HalfWidthMax;
            //每步方向抖动概率(0~1);越高越"醉"
            internal double TurnChance;
            //目标牵引基础增益,行程后45%自动x3保证收束
            internal double TargetBias;
            //净下降累计多少行记一道横档(≤4可回攀,F2满跳6.6),0=不记
            internal int CrossbarEvery;
            internal ushort CarveWall;
            //竖向主导段启用之字横漂
            internal bool ZigZag;
        }

        //横向坑道:骨窖标准连接形态(原版半宽4~6,本层收为4~5走廊感)
        internal static TunnelParams Lateral(ushort wall) => new() {
            HalfWidthMin = 4, HalfWidthMax = 5, TurnChance = 0.30, TargetBias = 0.22,
            CrossbarEvery = 4, CarveWall = wall, ZigZag = false,
        };

        //跨地层斜降坑道:之字横漂+密横档(下行可读、上行可攀)
        internal static TunnelParams Descent(ushort wall) => new() {
            HalfWidthMin = 4, HalfWidthMax = 5, TurnChance = 0.22, TargetBias = 0.30,
            CrossbarEvery = 4, CarveWall = wall, ZigZag = true,
        };

        //无光深巷:半宽3~4(ROOMS-L5 §1-7),更醉的漂移,巷内零灯由调用方保证
        internal static TunnelParams Alley(ushort wall) => new() {
            HalfWidthMin = 3, HalfWidthMax = 4, TurnChance = 0.42, TargetBias = 0.16,
            CrossbarEvery = 4, CarveWall = wall, ZigZag = false,
        };

        internal struct TunnelReport
        {
            internal int Steps;
            internal long Cells;
            internal bool ReachedByWalk;
            //中点游标(深巷藏龛用):首次剩余曼哈顿距≤半程时记录
            internal Point Mid;
            //横档记账(左缘x,行y),FlushCrossbars延迟铺设
            internal List<Point> Crossbars;
        }

        /// <summary>
        /// 在envelope内自start游走凿刻到end。start/end应位于包络内且各距边缘≥半宽+2
        /// (调用方以端点外扩构造包络即可)。返回凿刻报告。
        /// </summary>
        internal static TunnelReport Carve(Rectangle envelope, Point start, Point end, TunnelParams p) {
            UnifiedRandom rand = WorldGen.genRand;
            var report = new TunnelReport { Crossbars = [], Mid = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2) };

            double cx = start.X, cy = start.Y;
            int hw = rand.Next(p.HalfWidthMin, p.HalfWidthMax + 1);
            double dirX = end.X - cx, dirY = end.Y - cy;
            Normalize(ref dirX, ref dirY);

            int initialDist = Math.Abs(end.X - start.X) + Math.Abs(end.Y - start.Y);
            //步数预算:曼哈顿距x3+80,游走冗余充足;耗尽走直线兜底
            int maxSteps = initialDist * 3 + 80;
            int breath = rand.Next(6, 12);
            int zigTimer = rand.Next(8, 15);
            int zigSign = rand.NextBool(2) ? 1 : -1;
            int descent = 0, lastY = start.Y;
            bool midTaken = false;

            int step = 0;
            for (; step < maxSteps; step++) {
                int ix = (int)Math.Round(cx), iy = (int)Math.Round(cy);
                Stamp(ix, iy, hw, envelope, p.CarveWall, ref report.Cells);

                int remain = Math.Abs(end.X - ix) + Math.Abs(end.Y - iy);
                if (!midTaken && remain * 2 <= initialDist) {
                    report.Mid = new Point(ix, iy);
                    midTaken = true;
                }
                if (Math.Abs(end.X - ix) <= hw && Math.Abs(end.Y - iy) <= hw) {
                    report.ReachedByWalk = true;
                    break;
                }

                //1)宽度呼吸
                if (--breath <= 0) {
                    hw = Math.Clamp(hw + rand.Next(-1, 2), p.HalfWidthMin, p.HalfWidthMax);
                    breath = rand.Next(6, 12);
                }
                //2)方向漂移:垂直分量注入(近似±45°扇形转向)
                if (rand.NextDouble() < p.TurnChance) {
                    double k = (rand.NextDouble() - 0.5) * 1.4;
                    double px = -dirY, py = dirX;
                    dirX += px * k;
                    dirY += py * k;
                }
                //3)目标牵引(后段升压收束)
                double tx = end.X - cx, ty = end.Y - cy;
                Normalize(ref tx, ref ty);
                double bias = p.TargetBias * (step > maxSteps * 0.55 ? 3.0 : 1.0);
                dirX += tx * bias;
                dirY += ty * bias;
                //4)之字横漂(竖向段的斜之字坑道质感)
                if (p.ZigZag) {
                    if (--zigTimer <= 0) {
                        zigSign = -zigSign;
                        zigTimer = rand.Next(8, 15);
                    }
                    dirX += zigSign * 0.55;
                }
                Normalize(ref dirX, ref dirY);
                cx += dirX;
                cy += dirY;

                //包络钳制:游标限制在内缩半宽的活动区,越界贴边+方向回弹
                double xMin = envelope.Left + hw + 1, xMax = envelope.Right - hw - 2;
                double yMin = envelope.Top + hw + 1, yMax = envelope.Bottom - hw - 2;
                if (cx < xMin) { cx = xMin; dirX = Math.Abs(dirX); }
                else if (cx > xMax) { cx = xMax; dirX = -Math.Abs(dirX); }
                if (cy < yMin) { cy = yMin; dirY = Math.Abs(dirY); }
                else if (cy > yMax) { cy = yMax; dirY = -Math.Abs(dirY); }

                //横档记账:净下降达阈值(可回攀纪律)
                if (p.CrossbarEvery > 0) {
                    int nowY = (int)Math.Round(cy);
                    descent += nowY - lastY;
                    lastY = nowY;
                    if (descent >= p.CrossbarEvery) {
                        report.Crossbars.Add(new Point((int)Math.Round(cx) - 1, nowY + hw - 1));
                        descent = 0;
                    }
                }
            }

            //末段直线兜底:DDA逐格盖章到end,保证几何连通(不依赖游走命中)
            StraightConnect(ref cx, ref cy, end, hw, envelope, p, ref report);
            report.Steps = step;
            return report;
        }

        /// <summary>横档延迟铺设:3宽平台,只落在仍为空气的格(不碰家具/既有平台)</summary>
        internal static int FlushCrossbars(List<Point> bars, short frameY) {
            int laid = 0;
            foreach (Point bar in bars) {
                for (int dx = 0; dx < 3; dx++) {
                    int x = bar.X + dx;
                    if (WorldGen.InWorld(x, bar.Y, 5) && !Main.tile[x, bar.Y].HasTile) {
                        TileBrush.SetPlatform(x, bar.Y, frameY);
                        laid++;
                    }
                }
            }
            return laid;
        }

        //==================== 内部 ====================

        private static void StraightConnect(ref double cx, ref double cy, Point end, int hw,
            Rectangle envelope, TunnelParams p, ref TunnelReport report) {
            int guard = 0;
            int lastY = (int)Math.Round(cy);
            int descent = 0;
            while (guard++ < 4096) {
                int ix = (int)Math.Round(cx), iy = (int)Math.Round(cy);
                Stamp(ix, iy, hw, envelope, p.CarveWall, ref report.Cells);
                if (ix == end.X && iy == end.Y) {
                    break;
                }
                double dx = end.X - cx, dy = end.Y - cy;
                Normalize(ref dx, ref dy);
                cx += dx;
                cy += dy;
                //兜底段贴向终点,不再回弹钳制(end本就在包络内)
                if (p.CrossbarEvery > 0) {
                    int nowY = (int)Math.Round(cy);
                    descent += nowY - lastY;
                    lastY = nowY;
                    if (descent >= p.CrossbarEvery) {
                        report.Crossbars.Add(new Point((int)Math.Round(cx) - 1, nowY + hw - 1));
                        descent = 0;
                    }
                }
            }
        }

        //壳-膛盖章:内膛(±hw)清空刷墙;外环(壳厚3)只填"未挖区"(无地牢墙的格,F29)
        private static void Stamp(int cx, int cy, int hw, Rectangle envelope, ushort wall, ref long cells) {
            const int shellPad = 3;
            for (int x = cx - hw - shellPad; x <= cx + hw + shellPad; x++) {
                for (int y = cy - hw - shellPad; y <= cy + hw + shellPad; y++) {
                    if (x < envelope.Left || x >= envelope.Right || y < envelope.Top || y >= envelope.Bottom
                        || !WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    if (Math.Abs(x - cx) <= hw && Math.Abs(y - cy) <= hw) {
                        TileBrush.ClearCell(x, y, wall);
                        cells++;
                    }
                    else if (!Main.wallDungeon[Main.tile[x, y].WallType]) {
                        //墙即已挖标记:已凿区(房间内膛/先前坑道)免填,接缝自动融合
                        TileBrush.SetSolid(x, y, L5Palette.Brick);
                        cells++;
                    }
                }
            }
        }

        private static void Normalize(ref double x, ref double y) {
            double len = Math.Sqrt(x * x + y * y);
            if (len < 1e-6) {
                x = 1;
                y = 0;
                return;
            }
            x /= len;
            y /= len;
        }
    }
}
