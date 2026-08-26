using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Core
{
    //折线节点:世界格坐标+半径
    internal struct HadalPathNode
    {
        internal float X, Y, R;
        internal HadalPathNode(float x, float y, float r) { X = x; Y = y; R = r; }
    }

    //支沟(侧廊):从主沟分叉的折线,可回接成环/接溶洞/死端鼓包
    internal sealed class HadalGallery
    {
        internal List<HadalPathNode> Nodes = [];
        internal bool LoopBack;      //尾端回接主沟
        internal bool HasCaveField;  //尾端挂溶洞群
    }

    //溶洞群:椭圆域内的腔室链(蓝图§3.4:构造性连通,Worley只做壁纹理)
    internal sealed class HadalCaveField
    {
        internal float CX, CY;
        internal List<(float x, float y, float rx, float ry)> Chambers = [];
        internal List<(int a, int b, float r)> Links = [];
        internal (float x, float y) EntryFrom; //支沟尾端进点
        internal int EntryChamber;
    }

    //竖井:垂直折线
    internal sealed class HadalShaft
    {
        internal List<HadalPathNode> Nodes = [];
        internal bool DeadEnd; //景观井:不通底
    }

    //深渊平原:透镜状巨腔
    internal sealed class HadalPlainSpec
    {
        internal int Top, Bottom;
        internal float CenterX, HalfSpan;
        //残柱(x,半宽,形态0全柱/1垂乳残柱/2断柱墩)
        internal List<(float x, float halfW, int mode)> Pillars = [];
        //热液丘群中心x
        internal List<float> VentClusters = [];
    }

    //深渊下厅
    internal struct HadalHall
    {
        internal float CX, CY, RX, RY;
    }

    //封闭盆地(登记死水,洪泛白名单)
    internal struct HadalBasin
    {
        internal float CX, CY, RX, RY;
    }

    //出生气穴房(蓝图§2.6):月池竖井接主沟
    internal sealed class HadalSpawnRoomSpec
    {
        internal int Left, Right;      //内膛x区间[Left,Right]
        internal int TopY, BotY;       //内膛y区间[TopY,BotY](空气区)
        internal int PoolLeft;         //月池3宽左缘
        internal int PoolBottomY;      //月池竖井底行
        internal int CorridorTargetX;  //水平廊道打穿沟壁的目标x(含富余)
        internal int Dir;              //+1房在东壁,-1西壁
        internal int SpawnX, SpawnY;   //spawnTileY=地板实心行
    }

    //规划产物:纯数据,雕刻器只做投影(镜像Barotrauma"骨架先行"哲学H3)
    internal sealed class HadalTerrainPlan
    {
        internal float[] CenterX;   //每行主沟中心线
        internal float[] HalfL;     //每行左半宽(含崖阶量化)
        internal float[] HalfR;
        internal int[] SeabedY;     //每列海床行
        internal int MouthX;        //豁口中心
        internal List<(int x, int topW, int depth)> FalseCracks = [];
        internal List<(int x, int h, int w)> Reefs = [];
        internal List<HadalGallery> Galleries = [];
        internal List<HadalCaveField> CaveFields = [];
        internal HadalPlainSpec Plain;
        internal List<HadalShaft> Shafts = [];
        internal List<HadalHall> Halls = [];
        internal List<HadalPathNode> HallCorridor = []; //厅间走廊折线(逐段首尾相接)
        internal List<HadalBasin> Basins = [];
        internal HadalSpawnRoomSpec SpawnRoom;
        internal List<(int y, string name)> Chokes = [];    //登记窄喉(演出锚)
        internal List<(float x, float y)> BioSpots = [];    //微光斑位(雕刻后落材质)
        internal (float x, float y, float rx, float ry) VEndBulb; //沟底终腔(鲸落场)
        internal int TrenchCarveBottom;  //主沟列式雕刻下界(平原顶+搭接)
        internal int VTopY;              //V形段上界
        internal int PlainLinkY;         //门槛喉→平原顶的搭接点
    }

    //规划器:一切随机在此消耗,雕刻器零随机(决定论账目集中)
    internal static class HadalTerrainPlanner
    {
        internal static HadalTerrainPlan Build(HadalGenParams p, HadalRng rng) {
            var plan = new HadalTerrainPlan();
            HadalRng rTrench = rng.Fork(0x01);
            HadalRng rSeabed = rng.Fork(0x02);
            HadalRng rGallery = rng.Fork(0x03);
            HadalRng rCave = rng.Fork(0x04);
            HadalRng rDeep = rng.Fork(0x05);
            HadalRng rRoom = rng.Fork(0x06);
            HadalRng rBio = rng.Fork(0x07);

            BuildCenterline(p, plan, rTrench);
            BuildSeabed(p, plan, rSeabed);
            BuildWidths(p, plan, rTrench);
            BuildGalleriesAndCaves(p, plan, rGallery, rCave);
            BuildDeepStructures(p, plan, rDeep);
            BuildSpawnRoom(p, plan, rRoom);
            BuildBioSpots(p, plan, rBio);
            return plan;
        }

        //——主沟中心线:折线节点+Catmull-Rom平滑到逐行(蓝图§1.2)——
        private static void BuildCenterline(HadalGenParams p, HadalTerrainPlan plan, HadalRng rng) {
            plan.MouthX = p.Width / 2 + rng.Next(-180, 181);
            var nodes = new List<(float y, float x)> { (80f, plan.MouthX) };
            float x = plan.MouthX;
            float y = 120f;
            while (y < 4820f) {
                float step = rng.NextFloat(90f, 150f);
                y += step;
                float sway = rng.NextFloat(40f, 110f) * (rng.Chance(0.5f) ? 1f : -1f);
                //深处摆幅收敛:V形段走线趋刚(蓝图§2.5)
                if (y > 3800f) {
                    sway *= 0.45f;
                }
                x = Math.Clamp(x + sway, 500f, 1700f);
                nodes.Add((y, x));
            }

            plan.CenterX = new float[p.Height];
            ulong nJitter = rng.NextULong();
            int seg = 0;
            for (int row = 0; row < p.Height; row++) {
                while (seg < nodes.Count - 2 && row > nodes[seg + 1].y) {
                    seg++;
                }
                //逐行小抖动:折线平滑后再加一层曲流,破长直段
                float jitter = (HadalNoise.Fbm1(row * 0.008f, nJitter, 3) - 0.5f) * 22f;
                plan.CenterX[row] = CatmullRow(nodes, seg, row) + jitter;
            }
        }

        private static float CatmullRow(List<(float y, float x)> n, int i, float row) {
            var p0 = n[Math.Max(0, i - 1)];
            var p1 = n[i];
            var p2 = n[Math.Min(n.Count - 1, i + 1)];
            var p3 = n[Math.Min(n.Count - 1, i + 2)];
            float span = p2.y - p1.y;
            float t = span <= 0f ? 0f : Math.Clamp((row - p1.y) / span, 0f, 1f);
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1.x) + (-p0.x + p2.x) * t
                + (2f * p0.x - 5f * p1.x + 4f * p2.x - p3.x) * t2
                + (-p0.x + 3f * p1.x - 3f * p2.x + p3.x) * t3);
        }

        //——海床线+礁丘+假裂缝(蓝图§2.1)——
        private static void BuildSeabed(HadalGenParams p, HadalTerrainPlan plan, HadalRng rng) {
            ulong ns = rng.NextULong();
            plan.SeabedY = new int[p.Width];
            for (int x = 0; x < p.Width; x++) {
                float bed = 190f + (HadalNoise.Fbm1(x * 0.008f, ns, 4) - 0.5f) * 70f;
                plan.SeabedY[x] = (int)bed;
            }
            //礁丘3-5处:远离豁口,彼此隔开
            int reefCount = rng.Next(3, 6);
            var used = new List<int>();
            for (int i = 0; i < reefCount * 6 && plan.Reefs.Count < reefCount; i++) {
                int rx = rng.Next(p.PlayLeft + 120, p.PlayRight - 120);
                if (Math.Abs(rx - plan.MouthX) < 260 || used.Exists(u => Math.Abs(u - rx) < 170)) {
                    continue;
                }
                used.Add(rx);
                int h = rng.Next(10, 23);
                int w = rng.Next(40, 91);
                plan.Reefs.Add((rx, h, w));
                //礁丘隆起直接叠进海床线
                for (int dx = -w; dx <= w; dx++) {
                    int cx = rx + dx;
                    if (cx < 0 || cx >= p.Width) {
                        continue;
                    }
                    float t = dx / (float)w;
                    plan.SeabedY[cx] -= (int)(h * MathF.Exp(-4f * t * t));
                }
            }
            //假裂缝2-3条:死端叙事沟,要压得出存在感
            int crackCount = rng.Next(2, 4);
            for (int i = 0; i < crackCount * 6 && plan.FalseCracks.Count < crackCount; i++) {
                int cx = rng.Next(p.PlayLeft + 150, p.PlayRight - 150);
                if (Math.Abs(cx - plan.MouthX) < 350 || used.Exists(u => Math.Abs(u - cx) < 140)) {
                    continue;
                }
                used.Add(cx);
                plan.FalseCracks.Add((cx, rng.Next(16, 31), rng.Next(60, 141)));
            }
        }

        //——宽度呼吸场+崖阶量化(蓝图§1.2/§2.2)——
        private static void BuildWidths(HadalGenParams p, HadalTerrainPlan plan, HadalRng rng) {
            ulong nBreath = rng.NextULong();
            ulong nSideL = rng.NextULong();
            ulong nSideR = rng.NextULong();
            //三处保底窄喉(登记演出锚)+平原门槛(内建于zoneBase)
            int choke1 = rng.Next(860, 1000);
            int choke2 = rng.Next(1900, 2160);
            int choke3 = rng.Next(2380, 2560);
            plan.Chokes.Add((choke1, "第一鬼门"));
            plan.Chokes.Add((choke2, "长喉"));
            plan.Chokes.Add((choke3, "暗喉"));

            //平原/V的纵向布局先定,zoneBase要用
            int plainTop = 2780 + rng.Next(-30, 31);
            int plainBottom = plainTop + rng.Next(330, 400);
            plan.PlainLinkY = plainTop + 30;
            plan.TrenchCarveBottom = plainTop + 34;
            plan.VTopY = p.AbyssalBottom; //4100
            plan.Chokes.Add((2740, "门槛喉"));
            plan.Plain = new HadalPlainSpec { Top = plainTop, Bottom = plainBottom };

            plan.HalfL = new float[p.Height];
            plan.HalfR = new float[p.Height];

            //先算原始半宽(呼吸含,窄喉不含):崖阶持平要整值采样
            var rawL = new float[p.Height];
            var rawR = new float[p.Height];
            for (int y = 0; y < p.Height; y++) {
                float baseW = ZoneBaseWidth(p, plan, y);
                if (baseW <= 0f) {
                    continue;
                }
                //呼吸:低频fBm整形,高原展宽+低谷收缩;V底保底不收(蓝图§2.5)
                float b = HadalNoise.Fbm1(y * 0.0045f, nBreath, 3);
                float s = b * b * (3f - 2f * b);
                float breath = 0.34f + MathF.Pow(s, 0.85f) * 1.5f;
                if (y > 4600) {
                    breath = MathF.Max(breath, 0.95f);
                }
                float w = baseW * breath;
                rawL[y] = w * 0.5f * (0.75f + 0.5f * HadalNoise.Fbm1(y * 0.011f, nSideL, 3));
                rawR[y] = w * 0.5f * (0.75f + 0.5f * HadalNoise.Fbm1(y * 0.011f, nSideR, 3));
            }

            //崖阶节点:段内整半宽持平,阶间跳变即台阶(暮光密,日光唇缘疏而大)
            //节点缩放抖动放大级差,阶面才够宽可站(第三轮预览:纯持平级差太小)
            var knotsL = BuildTerraceKnots(rng, 200, p.TwilightBottom);
            var knotsR = BuildTerraceKnots(rng, 200, p.TwilightBottom);
            var scaleL = new List<float>();
            var scaleR = new List<float>();
            for (int i = 0; i < knotsL.Count; i++) {
                scaleL.Add(rng.NextFloat(0.82f, 1.28f));
            }
            for (int i = 0; i < knotsR.Count; i++) {
                scaleR.Add(rng.NextFloat(0.82f, 1.28f));
            }

            for (int y = 0; y < p.Height; y++) {
                if (rawL[y] <= 0f && rawR[y] <= 0f) {
                    plan.HalfL[y] = 0f;
                    plan.HalfR[y] = 0f;
                    continue;
                }
                float hl = rawL[y];
                float hr = rawR[y];
                //日光唇缘塌落台阶+暮光崖阶:同机制不同节距(蓝图§2.1/§2.2)
                if (y >= 200 && y < p.TwilightBottom) {
                    int ki = KnotIndex(knotsL, y);
                    hl = rawL[Math.Min(p.Height - 1, knotsL[ki])] * scaleL[ki];
                    ki = KnotIndex(knotsR, y);
                    hr = rawR[Math.Min(p.Height - 1, knotsR[ki])] * scaleR[ki];
                }
                //窄喉包络后乘:崖阶壁上切出必经咽喉,漏斗穿层理(演出锚)
                float choke = ChokeEnvelope(y, choke1, 0.72f)
                    * ChokeEnvelope(y, choke2, 0.76f)
                    * ChokeEnvelope(y, choke3, 0.66f);
                hl *= choke;
                hr *= choke;
                //钳制:总净宽≥8(H1包络),单侧≥4
                plan.HalfL[y] = MathF.Max(4f, hl);
                plan.HalfR[y] = MathF.Max(4f, hr);
            }
        }

        //崖阶节点表:日光段节距80-140,暮光段60-110;返回"本行取值行"
        private static List<int> BuildTerraceKnots(HadalRng rng, int top, int bottom) {
            var knots = new List<int>();
            int y = top;
            while (y < bottom) {
                knots.Add(y);
                y += y < 500 ? rng.Next(80, 141) : rng.Next(60, 111);
            }
            return knots;
        }

        private static int KnotIndex(List<int> knots, int y) {
            for (int i = knots.Count - 1; i >= 0; i--) {
                if (y >= knots[i]) {
                    return i;
                }
            }
            return 0;
        }

        private static float ChokeEnvelope(int y, int chokeY, float depth) {
            float t = (y - chokeY) / 46f;
            return 1f - depth * MathF.Exp(-t * t);
        }

        //分带基准宽:豁口喇叭→暮光→午夜→门槛喉;平原带与下厅带无主沟;V形段收窄
        private static float ZoneBaseWidth(HadalGenParams p, HadalTerrainPlan plan, int y) {
            if (y < 110) {
                return 0f;
            }
            if (y < p.SunlitBottom) {
                //豁口:唇宽130缓收到60(easeOut)
                float t = Math.Clamp((y - 170f) / (p.SunlitBottom - 170f), 0f, 1f);
                return Lerp(130f, 62f, 1f - (1f - t) * (1f - t));
            }
            if (y < p.TwilightBottom) {
                return 58f;
            }
            if (y < 2690) {
                return 70f;
            }
            if (y <= plan.TrenchCarveBottom) {
                //门槛喉:猛收到14,潜入平原顶
                float t = Math.Clamp((y - 2690f) / (plan.TrenchCarveBottom - 2690f), 0f, 1f);
                return Lerp(46f, 14f, t);
            }
            if (y < plan.VTopY) {
                return 0f; //平原/下厅带:主沟停,由冲压结构接管
            }
            if (y < 4650) {
                //V形收窄:60→14
                float t = (y - plan.VTopY) / (4650f - plan.VTopY);
                return Lerp(60f, 14f, t);
            }
            if (y <= 4770) {
                return 14f; //V底走廊基准(保底呼吸后约12-18)
            }
            return 0f;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        //——支沟+溶洞群(蓝图§2.3)——
        private static void BuildGalleriesAndCaves(HadalGenParams p, HadalTerrainPlan plan, HadalRng rGal, HadalRng rCave) {
            //暮光教学支沟:分2槽,取1-2条
            int twiCount = rGal.Next(1, 3);
            for (int i = 0; i < twiCount; i++) {
                int slotTop = 620 + i * 300;
                TryAddGallery(p, plan, rGal, rCave,
                    slotTop, Math.Min(1200, slotTop + 280), i, teaching: true);
            }
            //午夜主力:纵向均分6-8槽,每槽一条,侧向交替防偏科
            int midCount = rGal.Next(6, 9);
            int span = (2600 - 1360) / midCount;
            for (int i = 0; i < midCount; i++) {
                int slotTop = 1360 + i * span;
                TryAddGallery(p, plan, rGal, rCave,
                    slotTop, slotTop + span - 20, twiCount + i, teaching: false);
            }
        }

        private static void TryAddGallery(HadalGenParams p, HadalTerrainPlan plan, HadalRng rng, HadalRng rCave,
            int yMin, int yMax, int index, bool teaching) {
            if (yMax - yMin < 40) {
                return;
            }
            int y0 = rng.Next(yMin, yMax);
            //侧向交替+25%翻面:两壁都吃到支沟
            int dir = (index & 1) == 0 ? 1 : -1;
            if (rng.Chance(0.25f)) {
                dir = -dir;
            }
            var g = new HadalGallery();
            float cx = plan.CenterX[y0];
            //起点埋进主沟内膛,连接构造性成立
            float x = cx + dir * (plan.HalfL[y0] + plan.HalfR[y0]) * 0.25f;
            float y = y0;
            float r0 = teaching ? rng.NextFloat(5f, 8f) : rng.NextFloat(5f, 10f);
            g.Nodes.Add(new HadalPathNode(x, y, r0));

            int segs = teaching ? rng.Next(2, 4) : rng.Next(3, 7);
            for (int i = 0; i < segs; i++) {
                float dx = dir * rng.NextFloat(50f, 130f);
                //偶发折返制造曲折
                if (i > 0 && rng.Chance(0.22f)) {
                    dx = -dx * 0.6f;
                }
                float dy = rng.NextFloat(12f, teaching ? 44f : 85f);
                float px = x, py = y;
                x = Math.Clamp(x + dx, p.PlayLeft + 26, p.PlayRight - 26);
                y = Math.Clamp(y + dy, yMin - 40, yMax + 160);
                //中点位移一轮:长段折出肘弯,破"电路板直线"感
                float mx = (px + x) * 0.5f + rng.NextFloat(-1f, 1f) * MathF.Abs(x - px) * 0.25f;
                float my = (py + y) * 0.5f + rng.NextFloat(-1f, 1f) * 24f;
                g.Nodes.Add(new HadalPathNode(
                    Math.Clamp(mx, p.PlayLeft + 26, p.PlayRight - 26),
                    Math.Clamp(my, yMin - 40, yMax + 160), rng.NextFloat(4.5f, 9f)));
                g.Nodes.Add(new HadalPathNode(x, y, rng.NextFloat(4.5f, 9.5f)));
            }

            if (!teaching && rng.Chance(0.4f)) {
                //回接成环:尾节点放回更深处的主沟内膛
                g.LoopBack = true;
                int yEnd = (int)Math.Clamp(y0 + rng.Next(140, 321), yMin, 2650);
                float exC = plan.CenterX[yEnd];
                g.Nodes.Add(new HadalPathNode(
                    exC + (rng.Chance(0.5f) ? 1 : -1) * (plan.HalfL[yEnd] + plan.HalfR[yEnd]) * 0.2f,
                    yEnd, rng.NextFloat(5f, 8f)));
            }
            else if (!teaching && rng.Chance(0.75f)) {
                g.HasCaveField = true;
            }
            plan.Galleries.Add(g);

            if (g.HasCaveField) {
                BuildCaveField(p, plan, rCave, g);
            }
        }

        //腔室链溶洞(蓝图§3.4):泊松腔心+最近邻链+1-2附加边
        private static void BuildCaveField(HadalGenParams p, HadalTerrainPlan plan, HadalRng rng, HadalGallery g) {
            var end = g.Nodes[^1];
            float dir = end.X >= plan.CenterX[(int)end.Y] ? 1f : -1f;
            var f = new HadalCaveField {
                CX = Math.Clamp(end.X + dir * rng.NextFloat(50f, 110f), p.PlayLeft + 110, p.PlayRight - 110),
                CY = Math.Clamp(end.Y + rng.NextFloat(-20f, 70f), 1400f, 2600f),
                EntryFrom = (end.X, end.Y),
            };
            float rxField = rng.NextFloat(80f, 160f);
            float ryField = rng.NextFloat(50f, 100f);

            //腔心允许部分交叠(minDist<r和):并出胸腔式肿胀空腔
            int target = rng.Next(8, 19);
            for (int i = 0; i < target * 8 && f.Chambers.Count < target; i++) {
                float ang = rng.NextFloat(0f, MathF.PI * 2f);
                float rad = MathF.Sqrt(rng.NextFloat());
                float px = f.CX + MathF.Cos(ang) * rad * rxField;
                float py = f.CY + MathF.Sin(ang) * rad * ryField;
                bool tooClose = false;
                foreach (var c in f.Chambers) {
                    float ddx = c.x - px, ddy = c.y - py;
                    if (ddx * ddx + ddy * ddy < 22f * 22f) {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) {
                    continue;
                }
                float r = rng.NextFloat(10f, 28f);
                f.Chambers.Add((px, py, r, r * rng.NextFloat(0.6f, 0.9f)));
            }
            if (f.Chambers.Count < 2) {
                return;
            }
            //入口腔=离支沟尾端最近
            int entry = 0;
            float best = float.MaxValue;
            for (int i = 0; i < f.Chambers.Count; i++) {
                float ddx = f.Chambers[i].x - end.X, ddy = f.Chambers[i].y - end.Y;
                float d = ddx * ddx + ddy * ddy;
                if (d < best) {
                    best = d;
                    entry = i;
                }
            }
            f.EntryChamber = entry;
            //最近邻链:连通构造性成立
            var linked = new List<int> { entry };
            var remain = new List<int>();
            for (int i = 0; i < f.Chambers.Count; i++) {
                if (i != entry) {
                    remain.Add(i);
                }
            }
            while (remain.Count > 0) {
                float bd = float.MaxValue;
                int ba = -1, bb = -1;
                foreach (int a in linked) {
                    foreach (int b in remain) {
                        float ddx = f.Chambers[a].x - f.Chambers[b].x;
                        float ddy = f.Chambers[a].y - f.Chambers[b].y;
                        float d = ddx * ddx + ddy * ddy;
                        if (d < bd) {
                            bd = d;
                            ba = a;
                            bb = b;
                        }
                    }
                }
                f.Links.Add((ba, bb, rng.NextFloat(3f, 6f)));
                linked.Add(bb);
                remain.Remove(bb);
            }
            //附加环边1-2条
            int extra = rng.Next(1, 3);
            for (int i = 0; i < extra && f.Chambers.Count > 3; i++) {
                int a = rng.Next(0, f.Chambers.Count);
                int b = rng.Next(0, f.Chambers.Count);
                if (a != b) {
                    f.Links.Add((a, b, rng.NextFloat(3f, 5f)));
                }
            }
            plan.CaveFields.Add(f);
        }

        //——深渊带结构:平原残柱/热液/竖井/下厅/V/盆地(蓝图§2.4/§2.5)——
        private static void BuildDeepStructures(HadalGenParams p, HadalTerrainPlan plan, HadalRng rng) {
            HadalPlainSpec plain = plan.Plain;
            int midY = (plain.Top + plain.Bottom) / 2;
            plain.CenterX = Math.Clamp(plan.CenterX[midY] + rng.NextFloat(-100f, 100f), 800f, 1400f);
            plain.HalfSpan = rng.NextFloat(380f, 520f);
            float px0 = Math.Max(p.PlayLeft + 60, plain.CenterX - plain.HalfSpan);
            float px1 = Math.Min(p.PlayRight - 60, plain.CenterX + plain.HalfSpan);
            plain.CenterX = (px0 + px1) * 0.5f;
            plain.HalfSpan = (px1 - px0) * 0.5f;

            //残柱5-9根:只落|t|<0.7的开阔段,间距110,防止封舱(首轮预览暴露的死腔根因)
            int pillarCount = rng.Next(5, 10);
            var usedX = new List<float>();
            for (int i = 0; i < pillarCount * 6 && plain.Pillars.Count < pillarCount; i++) {
                float x = plain.CenterX + rng.NextFloat(-0.7f, 0.7f) * plain.HalfSpan;
                //平原中心走廊留空:门槛喉落点±90内不立柱
                if (MathF.Abs(x - plan.CenterX[plain.Top]) < 90f) {
                    continue;
                }
                if (usedX.Exists(u => MathF.Abs(u - x) < 110f)) {
                    continue;
                }
                usedX.Add(x);
                int mode = rng.Chance(0.55f) ? 0 : (rng.Chance(0.5f) ? 1 : 2);
                plain.Pillars.Add((x, rng.NextFloat(8f, 13f), mode));
            }
            //热液丘群3-6簇
            int ventCount = rng.Next(3, 7);
            var ventX = new List<float>();
            for (int i = 0; i < ventCount * 6 && ventX.Count < ventCount; i++) {
                float x = plain.CenterX + rng.NextFloat(-0.75f, 0.75f) * plain.HalfSpan;
                if (ventX.Exists(u => MathF.Abs(u - x) < 85f) || usedX.Exists(u => MathF.Abs(u - x) < 40f)) {
                    continue;
                }
                ventX.Add(x);
            }
            plain.VentClusters.AddRange(ventX);

            //下厅2-3个:纵向3450-3980,横向绕中心线摆
            int hallCount = rng.Next(2, 4);
            float hy = 3480f + rng.NextFloat(-30f, 30f);
            float hx = plan.CenterX[(int)hy] + rng.NextFloat(-120f, 120f);
            for (int i = 0; i < hallCount; i++) {
                var hall = new HadalHall {
                    CX = Math.Clamp(hx, p.PlayLeft + 240, p.PlayRight - 240),
                    CY = hy,
                    RX = rng.NextFloat(120f, 210f),
                    RY = rng.NextFloat(45f, 75f),
                };
                plan.Halls.Add(hall);
                hy += rng.NextFloat(180f, 260f);
                hx += rng.NextFloat(150f, 300f) * (rng.Chance(0.5f) ? 1f : -1f);
                if (hy > 3960f) {
                    break;
                }
            }

            //主竖井:平原底→下厅1顶,微折
            var main = new HadalShaft();
            HadalHall h0 = plan.Halls[0];
            float sx = Math.Clamp(plain.CenterX + rng.NextFloat(-200f, 200f), px0 + 80f, px1 - 80f);
            float sy = plain.Bottom - 30f;
            main.Nodes.Add(new HadalPathNode(sx, sy, rng.NextFloat(14f, 20f)));
            while (sy < h0.CY - h0.RY) {
                sy += rng.NextFloat(80f, 140f);
                sx = Math.Clamp(sx + rng.NextFloat(-30f, 30f), p.PlayLeft + 60, p.PlayRight - 60);
                main.Nodes.Add(new HadalPathNode(sx, MathF.Min(sy, h0.CY), rng.NextFloat(14f, 22f)));
            }
            //末节点吸附厅心,保证贯通
            main.Nodes.Add(new HadalPathNode(h0.CX, h0.CY, 16f));
            plan.Shafts.Add(main);

            //景观死端井1-2根:井口避开主竖井与残柱(柱基堵井口=死腔)
            int deadCount = rng.Next(1, 3);
            for (int i = 0; i < deadCount; i++) {
                var dead = new HadalShaft { DeadEnd = true };
                float dx0 = Math.Clamp(plain.CenterX + rng.NextFloat(-0.6f, 0.6f) * plain.HalfSpan, px0 + 60f, px1 - 60f);
                if (MathF.Abs(dx0 - main.Nodes[0].X) < 120f
                    || plain.Pillars.Exists(pl => MathF.Abs(pl.x - dx0) < 60f)) {
                    continue;
                }
                float dy = plain.Bottom - 20f;
                dead.Nodes.Add(new HadalPathNode(dx0, dy, rng.NextFloat(8f, 13f)));
                float bottom = dy + rng.NextFloat(220f, 480f);
                while (dy < bottom) {
                    dy += rng.NextFloat(70f, 120f);
                    dx0 = Math.Clamp(dx0 + rng.NextFloat(-24f, 24f), p.PlayLeft + 50, p.PlayRight - 50);
                    dead.Nodes.Add(new HadalPathNode(dx0, MathF.Min(dy, 3980f), rng.NextFloat(7f, 12f)));
                }
                plan.Shafts.Add(dead);
            }

            //厅间走廊:厅i边缘→厅i+1边缘,一处中折
            for (int i = 0; i + 1 < plan.Halls.Count; i++) {
                HadalHall a = plan.Halls[i];
                HadalHall b = plan.Halls[i + 1];
                float r = rng.NextFloat(6f, 9f);
                float mx = (a.CX + b.CX) * 0.5f + rng.NextFloat(-70f, 70f);
                float my = (a.CY + b.CY) * 0.5f + rng.NextFloat(-30f, 30f);
                plan.HallCorridor.Add(new HadalPathNode(a.CX, a.CY, r));
                plan.HallCorridor.Add(new HadalPathNode(mx, my, r * rng.NextFloat(0.8f, 1.2f)));
                plan.HallCorridor.Add(new HadalPathNode(b.CX, b.CY, r));
            }
            //末厅→V口漏斗
            HadalHall last = plan.Halls[^1];
            float vx = plan.CenterX[plan.VTopY + 14];
            plan.HallCorridor.Add(new HadalPathNode(last.CX, last.CY, 11f));
            plan.HallCorridor.Add(new HadalPathNode(
                (last.CX + vx) * 0.5f + rng.NextFloat(-50f, 50f),
                (last.CY + plan.VTopY) * 0.5f, 10f));
            plan.HallCorridor.Add(new HadalPathNode(vx, plan.VTopY + 16, 10f));

            //沟底终腔:V底走廊尽头的鲸落场(蓝图§2.5)
            int bulbY = 4728;
            plan.VEndBulb = (plan.CenterX[bulbY] + rng.NextFloat(-8f, 8f), bulbY,
                rng.NextFloat(17f, 24f), rng.NextFloat(6f, 9f));

            //封闭盆地2-3个:V两侧岩体,密封间距构造性保证(蓝图§6-4)
            int basinCount = rng.Next(2, 4);
            var usedBy = new List<int>();
            for (int i = 0; i < basinCount * 6 && plan.Basins.Count < basinCount; i++) {
                int by = rng.Next(4250, 4601);
                if (usedBy.Exists(u => Math.Abs(u - by) < 90)) {
                    continue;
                }
                usedBy.Add(by);
                float rx = rng.NextFloat(25f, 45f);
                float ry = rng.NextFloat(12f, 22f);
                float half = MathF.Max(plan.HalfL[by], plan.HalfR[by]);
                float off = half + rx + rng.NextFloat(30f, 80f);
                float bx = plan.CenterX[by] + (rng.Chance(0.5f) ? 1f : -1f) * off;
                bx = Math.Clamp(bx, p.PlayLeft + rx + 14f, p.PlayRight - rx - 14f);
                if (by + ry > 4760f) {
                    continue;
                }
                plan.Basins.Add(new HadalBasin { CX = bx, CY = by, RX = rx, RY = ry });
            }
        }

        //——出生气穴房:豁口下方沟壁,月池竖井接主沟(蓝图§2.6)——
        private static void BuildSpawnRoom(HadalGenParams p, HadalTerrainPlan plan, HadalRng rng) {
            int dir = rng.Chance(0.5f) ? 1 : -1;
            int yFloor = rng.Next(258, 296);
            float edge = dir > 0
                ? plan.CenterX[yFloor] + plan.HalfR[yFloor]
                : plan.CenterX[yFloor] - plan.HalfL[yFloor];
            const int shell = 4;
            const int innerW = 16;
            const int innerH = 9;
            int left = dir > 0
                ? (int)(edge + shell + 6)
                : (int)(edge - shell - 6 - innerW);
            left = Math.Clamp(left, p.PlayLeft + 8, p.PlayRight - 8 - innerW);
            var room = new HadalSpawnRoomSpec {
                Dir = dir,
                Left = left,
                Right = left + innerW - 1,
                BotY = yFloor,
                TopY = yFloor - innerH + 1,
            };
            //月池贴沟侧
            room.PoolLeft = dir > 0 ? room.Left + 2 : room.Right - 4;
            room.PoolBottomY = yFloor + rng.Next(12, 19);
            //水平廊道打穿沟壁:目标x=沟内膛再进6格(壁面噪声±6全覆盖)
            int corrY = room.PoolBottomY - 1;
            float edgeAtCorr = dir > 0
                ? plan.CenterX[corrY] + plan.HalfR[corrY]
                : plan.CenterX[corrY] - plan.HalfL[corrY];
            room.CorridorTargetX = (int)(edgeAtCorr - dir * 6);
            room.SpawnX = (room.Left + room.Right) / 2;
            room.SpawnY = yFloor + 1;
            plan.SpawnRoom = room;
        }

        //——微光斑位(装饰性发光物块归B路)——
        private static void BuildBioSpots(HadalGenParams p, HadalTerrainPlan plan, HadalRng rng) {
            //溶洞腔底30-50%
            foreach (HadalCaveField f in plan.CaveFields) {
                foreach (var c in f.Chambers) {
                    if (rng.Chance(0.4f)) {
                        plan.BioSpots.Add((c.x, c.y + c.ry * 0.5f));
                    }
                }
            }
            //下厅各一
            foreach (HadalHall h in plan.Halls) {
                plan.BioSpots.Add((h.CX + rng.NextFloat(-0.5f, 0.5f) * h.RX, h.CY + h.RY * 0.6f));
            }
            //V底走廊2-4处
            int vCount = rng.Next(2, 5);
            for (int i = 0; i < vCount; i++) {
                int vy = rng.Next(4640, 4730);
                plan.BioSpots.Add((plan.CenterX[vy] + rng.NextFloat(-6f, 6f), vy));
            }
            //平原角落2-3处
            int pCount = rng.Next(2, 4);
            for (int i = 0; i < pCount; i++) {
                plan.BioSpots.Add((
                    plan.Plain.CenterX + rng.NextFloat(-0.85f, 0.85f) * plan.Plain.HalfSpan,
                    plan.Plain.Bottom - 30));
            }
        }
    }
}
