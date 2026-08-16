using System.Collections.Generic;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Rooms
{
    //房间图数据层：图先于几何，本文件零tile写入
    //镜像 Dungeonworld RoomLayout 的精简版（旧网房间挂在竖井平台厅上，
    //开口由建造方直接凿，暂不需要 DoorSocket 词汇——M3 目录扩容时再补）

    internal enum OldNetRoomRole { Normal, Landing, Vault, Machine, Archive }

    //轴对齐矩形房：Bounds 含 RoomShellThick 厚外壳
    internal sealed class OldNetRoomNode
    {
        internal Rectangle Bounds;
        internal OldNetRoomRole Role = OldNetRoomRole.Normal;

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
