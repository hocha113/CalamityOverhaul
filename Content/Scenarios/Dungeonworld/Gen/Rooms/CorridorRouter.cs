namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms
{
    //走廊路由器(§2.5):socket→脊/socket→socket的短程连接,≤2折
    //三形态:水平走廊/坡道(每列1格,自动登F3)/楼梯井(之字平台,竖距≤ShaftStepRows)
    //净空与厚度数值全取Metrics;写入只走TileBrush
    internal static class CorridorRouter
    {
        //形态计数,供生成报告回归比对(§3.1-4)
        internal static long RoutedHorizontal;
        internal static long RoutedRamp;
        internal static long RoutedStairWell;

        internal static void ResetCounters()
            => RoutedHorizontal = RoutedRamp = RoutedStairWell = 0;

        //===socket开口framing(§2.5-接缝)===

        //Door/Archway:在房壳上开槽;槽上下实心与两侧墙柱由壳厚≥2构造满足(F4)
        //过梁材质区分是视觉钩子,M0全蓝砖阶段无操作
        internal static void OpenWallSocket(RoomNode room, DoorSocket socket, ushort wall) {
            int height = socket.Kind == SocketKind.Door ? 3 : socket.Width;
            int top = room.Bounds.Top + socket.Offset;
            int left = socket.Side == SocketSide.Left ? room.Bounds.Left : room.InteriorRight;
            TileBrush.CarveRect(left, top, left + DungeonworldMetrics.RoomShellThick, top + height, wall);
        }

        //PlatformGap:地板开口盖平台防误落(§2.1),口沿走廊语法留墙柱
        internal static void OpenPlatformGap(RoomNode room, DoorSocket socket, short platformFrameY, ushort wall) {
            int left = room.Bounds.Left + socket.Offset;
            TileBrush.CarveRect(left, room.FloorTop, left + socket.Width, room.Bounds.Bottom, wall);
            TileBrush.PlatformRow(left, left + socket.Width, room.FloorTop, platformFrameY);
        }

        //===三种几何形态===

        //水平走廊:两端地板必须齐平(不齐平的边在数据层就非法,§2.5-1)
        internal static void CarveHorizontal(int xFrom, int xTo, int floorTop, ushort wall) {
            int left = System.Math.Min(xFrom, xTo);
            int right = System.Math.Max(xFrom, xTo);
            TileBrush.CarveRect(left, floorTop - DungeonworldMetrics.CorridorClearance,
                right, floorTop, wall);
            RoutedHorizontal++;
        }

        //坡道:每列降1格的连续台阶(自动登F3),多余列走平段;dirRight=向右延伸
        internal static void CarveRamp(int xStart, int floorStart, int floorEnd, bool dirRight, ushort wall) {
            int rise = floorEnd - floorStart;
            int run = System.Math.Abs(rise) + DungeonworldMetrics.CorridorClearance;
            int floor = floorStart;
            for (int i = 0; i < run; i++) {
                int x = dirRight ? xStart + i : xStart - i;
                TileBrush.CarveRect(x, floor - DungeonworldMetrics.CorridorClearance, x + 1, floor, wall);
                if (floor != floorEnd) {
                    floor += System.Math.Sign(rise);
                }
            }
            RoutedRamp++;
        }

        //楼梯井:净宽3竖井+全宽平台横档,竖距=ShaftStepRows(≤5可上行,F2)
        //上口若来自PlatformGap由调用方先开;下口直接落进目标走廊内膛
        internal static void CarveStairWell(int left, int floorTopUpper, int floorTopLower, short platformFrameY, ushort wall) {
            int right = left + DungeonworldMetrics.StairWellWidth;
            TileBrush.CarveRect(left, floorTopUpper, right, floorTopLower, wall);
            for (int y = floorTopLower - DungeonworldMetrics.ShaftStepRows;
                y > floorTopUpper; y -= DungeonworldMetrics.ShaftStepRows) {
                TileBrush.PlatformRow(left, right, y, platformFrameY);
            }
            RoutedStairWell++;
        }

        //===调度:socket→下方脊走廊(梳齿挂房的"齿",§2.5 LayerPlanner)===
        //形态按爬升量选择是设计规则(≤RampMaxRise坡道,更高楼梯井),不是静默修补
        internal static EdgeForm RouteToFloorBelow(RoomNode room, DoorSocket socket,
            int targetFloorTop, short platformFrameY, ushort wall) {
            int rise = targetFloorTop - room.FloorTop;
            if (socket.Kind == SocketKind.PlatformGap && socket.Side == SocketSide.Bottom) {
                OpenPlatformGap(room, socket, platformFrameY, wall);
                CarveStairWell(room.Bounds.Left + socket.Offset, room.Bounds.Bottom,
                    targetFloorTop, platformFrameY, wall);
                return EdgeForm.StairWell;
            }
            //侧壁Door:出壳后坡道下行;超限改楼梯井并记日志(规划期应已选对形态)
            OpenWallSocket(room, socket, wall);
            bool dirRight = socket.Side == SocketSide.Right;
            int exitX = dirRight ? room.Bounds.Right : room.Bounds.Left - 1;
            if (rise <= DungeonworldMetrics.RampMaxRise) {
                CarveRamp(exitX, room.FloorTop, targetFloorTop, dirRight, wall);
                return EdgeForm.Ramp;
            }
            CWRMod.Instance.Logger.Warn(
                $"[Dungeonworld] Router 爬升{rise}超坡道上限,回退楼梯井 room={room.Bounds}");
            int wellLeft = dirRight ? room.Bounds.Right : room.Bounds.Left - DungeonworldMetrics.StairWellWidth;
            //先水平出壳一段再垂直下井,保持≤2折
            TileBrush.CarveRect(wellLeft, room.FloorTop - DungeonworldMetrics.CorridorClearance,
                wellLeft + DungeonworldMetrics.StairWellWidth, room.FloorTop, wall);
            CarveStairWell(wellLeft, room.FloorTop, targetFloorTop, platformFrameY, wall);
            return EdgeForm.StairWell;
        }

        //===调度:socket↔socket水平边(链/环边的几何实现,M1只做直线形态)===
        internal static bool RouteDoorToDoor(RoomNode a, DoorSocket sa, RoomNode b, DoorSocket sb, ushort wall) {
            if (a.FloorTop != b.FloorTop) {
                //地板不齐平的边在Layout阶段就非法(§2.5-1),这里fail loud不修补
                CWRMod.Instance.Logger.Error(
                    $"[Dungeonworld] Router 门对门地板不齐平 a={a.FloorTop} b={b.FloorTop},责任=数据层配对");
                return false;
            }
            OpenWallSocket(a, sa, wall);
            OpenWallSocket(b, sb, wall);
            RoomNode leftRoom = a.Bounds.Left <= b.Bounds.Left ? a : b;
            RoomNode rightRoom = ReferenceEquals(leftRoom, a) ? b : a;
            CarveHorizontal(leftRoom.Bounds.Right, rightRoom.Bounds.Left, a.FloorTop, wall);
            return true;
        }
    }
}
