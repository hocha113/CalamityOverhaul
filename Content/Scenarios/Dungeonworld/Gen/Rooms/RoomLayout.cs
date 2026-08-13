using System.Collections.Generic;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms
{
    //房间图数据层(§1.4三级连通/§2.1核心表示):图先于几何,
    //本文件零tile写入,一切相交/连通推理在数据层完成后才交给路由器刻画

    internal enum SocketSide { Left, Right, Top, Bottom }

    //Door:1x3标准门;Archway:3~6宽拱洞;ShaftMouth:竖井接口;PlatformGap:地/顶面平台通行口(§2.1)
    internal enum SocketKind { Door, Archway, ShaftMouth, PlatformGap }

    internal enum RoomRole { Normal, Entry, Exit, Treasure, Safe, Puzzle, Boss }

    //边的几何实现形态(§2.5:直线/L形/游走;游走形态M3再入列)
    internal enum EdgeForm { Horizontal, Ramp, StairWell }

    //门插槽:房间对外的唯一开口(§2.1),走廊不许随便捅进侧壁
    internal readonly struct DoorSocket(SocketSide side, int offset, SocketKind kind, int width)
    {
        internal readonly SocketSide Side = side;
        //Left/Right边=开口顶距Bounds.Top的行数;Top/Bottom边=开口左缘距Bounds.Left的列数
        internal readonly int Offset = offset;
        internal readonly SocketKind Kind = kind;
        //Door固定3(高),Archway/PlatformGap为口宽
        internal readonly int Width = width;
    }

    //轴对齐矩形房:非矩形轮廓由archetype/prefab在包络内部雕刻(§2.1防破碎结构)
    internal sealed class RoomNode
    {
        //含RoomShellThick厚外壳
        internal Rectangle Bounds;
        internal RoomRole Role = RoomRole.Normal;
        internal readonly List<DoorSocket> Sockets = [];

        //内膛区间(半开),外壳之内
        internal int InteriorLeft => Bounds.Left + DungeonworldMetrics.RoomShellThick;
        internal int InteriorRight => Bounds.Right - DungeonworldMetrics.RoomShellThick;
        internal int InteriorTop => Bounds.Top + DungeonworldMetrics.RoomShellThick;
        //地板首行(实心),站立行=FloorTop-1
        internal int FloorTop => Bounds.Bottom - DungeonworldMetrics.RoomShellThick;
    }

    internal readonly struct RoomEdge(int a, int b, SocketKind kind, EdgeForm form)
    {
        internal readonly int A = a;
        internal readonly int B = b;
        internal readonly SocketKind Kind = kind;
        internal readonly EdgeForm Form = form;
    }

    //每层一张房间图:先生成树保连通,再加环边防一本道(§1.4-2)
    internal sealed class RoomGraph
    {
        internal readonly List<RoomNode> Rooms = [];
        internal readonly List<RoomEdge> Edges = [];

        //脊柱制下的生成树即按x序成链(§2.5 LayerPlanner:梳齿挂房);
        //自由树形留给各层planner,M1切片用链已覆盖"树先行"不变量
        internal void ConnectAsChain(SocketKind kind, EdgeForm form) {
            Rooms.Sort((l, r) => l.Bounds.Left.CompareTo(r.Bounds.Left));
            for (int i = 0; i + 1 < Rooms.Count; i++) {
                Edges.Add(new RoomEdge(i, i + 1, kind, form));
            }
        }

        //环边:随机取非相邻房对,1~3条(§1.4-2),重复对自动跳过
        internal int AddLoopEdges(UnifiedRandom rand, int count) {
            int added = 0;
            if (Rooms.Count < 3) {
                return added;
            }
            for (int attempt = 0; attempt < count * 4 && added < count; attempt++) {
                int a = rand.Next(Rooms.Count);
                int b = rand.Next(Rooms.Count);
                if (System.Math.Abs(a - b) < 2 || HasEdge(a, b)) {
                    continue;
                }
                Edges.Add(new RoomEdge(System.Math.Min(a, b), System.Math.Max(a, b),
                    SocketKind.Archway, EdgeForm.Horizontal));
                added++;
            }
            return added;
        }

        internal bool HasEdge(int a, int b) {
            foreach (RoomEdge e in Edges) {
                if ((e.A == a && e.B == b) || (e.A == b && e.B == a)) {
                    return true;
                }
            }
            return false;
        }

        //数据层连通自检:洪泛校验(P80)只是回归断言,不变量在这里保证(§1.4)
        internal bool IsConnected() {
            if (Rooms.Count == 0) {
                return true;
            }
            var seen = new bool[Rooms.Count];
            var stack = new Stack<int>();
            stack.Push(0);
            seen[0] = true;
            int visited = 1;
            while (stack.Count > 0) {
                int cur = stack.Pop();
                foreach (RoomEdge e in Edges) {
                    int other = e.A == cur ? e.B : e.B == cur ? e.A : -1;
                    if (other >= 0 && !seen[other]) {
                        seen[other] = true;
                        visited++;
                        stack.Push(other);
                    }
                }
            }
            return visited == Rooms.Count;
        }
    }
}
