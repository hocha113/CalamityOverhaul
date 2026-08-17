using System.Collections.Generic;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Rooms
{
    //房间图数据层：图先于几何，本文件零tile写入
    //镜像 Dungeonworld RoomLayout 的精简版。DoorSocket 词汇已补（原 M3 遗留债）：
    //建造方每凿一个真实开口就登记一个 socket，图层因此知道房的连通词汇——
    //零 socket 的非平台厅房 = 密闭死房，P80 审计报警

    internal enum OldNetRoomRole { Normal, Landing, Vault, Machine, Archive }

    internal enum OldNetSocketSide { Left, Right, Top, Bottom }

    //房间开口记录：Side=开口在哪面壳上，Opening=开口矩形（世界格）
    internal readonly struct OldNetDoorSocket(OldNetSocketSide side, Rectangle opening)
    {
        internal readonly OldNetSocketSide Side = side;
        internal readonly Rectangle Opening = opening;
    }

    //轴对齐矩形房：Bounds 含 RoomShellThick 厚外壳
    internal sealed class OldNetRoomNode
    {
        internal Rectangle Bounds;
        internal OldNetRoomRole Role = OldNetRoomRole.Normal;

        //建造方登记的真实开口（走廊/门洞/天窗）
        internal readonly List<OldNetDoorSocket> Sockets = [];

        //内膛区间（半开），外壳之内
        internal int InteriorLeft => Bounds.Left + OldNetMetrics.RoomShellThick;
        internal int InteriorRight => Bounds.Right - OldNetMetrics.RoomShellThick;
        internal int InteriorTop => Bounds.Top + OldNetMetrics.RoomShellThick;
        //地板首行（实心），站立行=FloorTop-1
        internal int FloorTop => Bounds.Bottom - OldNetMetrics.RoomShellThick;
    }

    internal readonly struct OldNetRoomEdge(int a, int b)
    {
        internal readonly int A = a;
        internal readonly int B = b;
    }

    //每带一张房间图：链式挂房保连通，P80洪泛只是回归断言
    internal sealed class OldNetRoomGraph
    {
        internal readonly List<OldNetRoomNode> Rooms = [];
        internal readonly List<OldNetRoomEdge> Edges = [];

        internal void AddEdge(int a, int b) => Edges.Add(new OldNetRoomEdge(a, b));

        //数据层连通自检
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
                foreach (OldNetRoomEdge e in Edges) {
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
