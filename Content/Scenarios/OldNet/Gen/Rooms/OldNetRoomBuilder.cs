using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Rooms
{
    //共用房间建造笔刷：壳房/走廊/挂房链。tile写入全走OldNetTileBrush
    internal static class OldNetRoomBuilder
    {
        /// <summary>壳房建造：Bounds填壳砖，内膛清空刷墙</summary>
        internal static void BuildShellRoom(OldNetRoomNode room, ushort brick, ushort wall) {
            Rectangle b = room.Bounds;
            OldNetTileBrush.FillRect(b.Left, b.Top, b.Right, b.Bottom, brick);
            OldNetTileBrush.CarveRect(room.InteriorLeft, room.InteriorTop,
                room.InteriorRight, room.FloorTop, wall);
        }

        /// <summary>水平走廊：rows [floorRow-3, floorRow) 清空刷墙（两端穿透既有壳体）</summary>
        internal static void CarveCorridor(int xFrom, int xTo, int floorRow, ushort wall) {
            if (xFrom > xTo) {
                (xFrom, xTo) = (xTo, xFrom);
            }
            OldNetTileBrush.CarveRect(xFrom, floorRow - 3, xTo, floorRow, wall);
        }

        /// <summary>
        /// 从平台厅两侧交替挂房 + 链式走廊。房地板行=平台厅地板行，
        /// 走廊刻画后即MarkUnchecked防后续预留切断。返回建成房列表。
        /// </summary>
        internal static List<OldNetRoomNode> HangRoomsOffLanding(OldNetBuildContext ctx,
            Rectangle landing, int count, ushort brick, ushort wall,
            Point interiorMin, Point interiorMax) {
            var rooms = new List<OldNetRoomNode>();
            int floorRow = landing.Bottom;
            //两侧推进边界：外缘含壳
            int frontRight = landing.Right + OldNetMetrics.RoomShellThick;
            int frontLeft = landing.Left - OldNetMetrics.RoomShellThick;
            //平台厅自身入图（连通断言的根）
            var landingNode = new OldNetRoomNode {
                Bounds = landing, Role = OldNetRoomRole.Landing,
            };
            ctx.Graph.Rooms.Add(landingNode);
            int landingIdx = ctx.Graph.Rooms.Count - 1;
            int prevRightIdx = landingIdx;
            int prevLeftIdx = landingIdx;

            for (int i = 0; i < count; i++) {
                bool right = i % 2 == 0;
                //间隙上限：走廊不许无限长
                OldNetRoomNode room = right
                    ? OldNetRoomPlacer.TryPlace(ctx.Grid, WorldGen.genRand,
                        frontRight + 1, System.Math.Min(frontRight + 46, ctx.Area.Right),
                        floorRow, interiorMin, interiorMax)
                    : OldNetRoomPlacer.TryPlace(ctx.Grid, WorldGen.genRand,
                        System.Math.Max(frontLeft - 46, ctx.Area.Left), frontLeft - 1,
                        floorRow, interiorMin, interiorMax);
                if (room == null) {
                    continue;
                }
                BuildShellRoom(room, brick, wall);

                //走廊：自上一结构内缘穿壳到新房内缘
                Rectangle corridor;
                if (right) {
                    corridor = new Rectangle(frontRight - 3, floorRow - 3,
                        room.InteriorLeft + 1 - (frontRight - 3), 3);
                    frontRight = room.Bounds.Right;
                }
                else {
                    corridor = new Rectangle(room.InteriorRight - 1, floorRow - 3,
                        frontLeft + 3 - (room.InteriorRight - 1), 3);
                    frontLeft = room.Bounds.Left;
                }
                CarveCorridor(corridor.Left, corridor.Right, floorRow, wall);
                ctx.Grid.MarkUnchecked(corridor);

                ctx.Graph.Rooms.Add(room);
                int idx = ctx.Graph.Rooms.Count - 1;
                ctx.Graph.AddEdge(right ? prevRightIdx : prevLeftIdx, idx);
                if (right) {
                    prevRightIdx = idx;
                }
                else {
                    prevLeftIdx = idx;
                }
                rooms.Add(room);
            }
            return rooms;
        }
    }
}
