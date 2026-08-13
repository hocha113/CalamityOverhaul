using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Prefabs
{
    //代码内字符画prefab机器(§2.3):解析→几何盖章(TileBrush)→语义槽落家具(PlaceObject)
    //垂直镜像=文本级变换后重解析(§2.3镜像五层法的1+2+3层;通行结构与倒吊overlay不参与,
    //由倒吊通行生成器/overlay prefab另行负责,§2.3第4/5条)

    //从字符画贴边D连块导出的门插槽,坐标为prefab局部系
    internal readonly struct PrefabSocketInfo(Rooms.SocketSide side, int x, int y, int w, int h)
    {
        internal readonly Rooms.SocketSide Side = side;
        internal readonly int X = x;
        internal readonly int Y = y;
        internal readonly int W = w;
        internal readonly int H = h;
    }

    internal readonly struct PrefabSlot(int x, int y, PrefabSlotDef def)
    {
        internal readonly int X = x;
        internal readonly int Y = y;
        internal readonly PrefabSlotDef Def = def;
    }

    internal struct FurnishReport
    {
        internal int Placed;
        internal int Rejected;
        internal int Markers;
    }

    internal sealed class Prefab
    {
        internal readonly string Name;
        internal readonly int Width;
        internal readonly int Height;
        //镜像时被对偶表判删除的槽数,报告用
        internal int MirrorDroppedSlots;

        private readonly string[] _art;
        private readonly PrefabLegend _legend;
        internal readonly List<PrefabSlot> Slots = [];
        internal readonly List<PrefabSocketInfo> Sockets = [];

        private Prefab(string name, string[] art, PrefabLegend legend) {
            Name = name;
            _art = art;
            _legend = legend;
            Height = art.Length;
            Width = art[0].Length;
        }

        //解析即校验:行长不齐/未知字符直接抛,fail loud(§3.1-2)
        internal static Prefab Parse(string name, string[] art, PrefabLegend legend) {
            if (art == null || art.Length == 0) {
                throw new System.InvalidOperationException($"[Dungeonworld] prefab {name} 空字符画");
            }
            var prefab = new Prefab(name, art, legend);
            for (int y = 0; y < prefab.Height; y++) {
                if (art[y].Length != prefab.Width) {
                    throw new System.InvalidOperationException(
                        $"[Dungeonworld] prefab {name} 第{y}行长{art[y].Length}!=首行{prefab.Width}");
                }
                for (int x = 0; x < prefab.Width; x++) {
                    char c = art[y][x];
                    if (PrefabLegend.IsGeometryChar(c)) {
                        continue;
                    }
                    if (!legend.Slots.TryGetValue(c, out PrefabSlotDef def)) {
                        throw new System.InvalidOperationException(
                            $"[Dungeonworld] prefab {name} 未知字符'{c}'@({x},{y})");
                    }
                    prefab.Slots.Add(new PrefabSlot(x, y, def));
                }
            }
            prefab.CollectSockets();
            return prefab;
        }

        //D连块→门插槽:4邻域连通分量,竖高横宽定Side(镜像后由重解析自动跟随)
        private void CollectSockets() {
            var seen = new bool[Width, Height];
            for (int y = 0; y < Height; y++) {
                for (int x = 0; x < Width; x++) {
                    if (_art[y][x] != 'D' || seen[x, y]) {
                        continue;
                    }
                    int minX = x, maxX = x, minY = y, maxY = y;
                    var stack = new Stack<(int, int)>();
                    stack.Push((x, y));
                    seen[x, y] = true;
                    while (stack.Count > 0) {
                        (int cx, int cy) = stack.Pop();
                        if (cx < minX) minX = cx;
                        if (cx > maxX) maxX = cx;
                        if (cy < minY) minY = cy;
                        if (cy > maxY) maxY = cy;
                        Visit(cx + 1, cy);
                        Visit(cx - 1, cy);
                        Visit(cx, cy + 1);
                        Visit(cx, cy - 1);
                    }
                    int w = maxX - minX + 1;
                    int h = maxY - minY + 1;
                    Rooms.SocketSide side = h >= w
                        ? (minX < Width / 2 ? Rooms.SocketSide.Left : Rooms.SocketSide.Right)
                        : (minY < Height / 2 ? Rooms.SocketSide.Top : Rooms.SocketSide.Bottom);
                    Sockets.Add(new PrefabSocketInfo(side, minX, minY, w, h));

                    void Visit(int nx, int ny) {
                        if (nx < 0 || ny < 0 || nx >= Width || ny >= Height
                            || seen[nx, ny] || _art[ny][nx] != 'D') {
                            return;
                        }
                        seen[nx, ny] = true;
                        stack.Push((nx, ny));
                    }
                }
            }
        }

        //垂直镜像:行倒序+slope对偶(1↔3,2↔4)+半砖按声明规则+槽走对偶表(§2.3)
        //文本级变换后重解析,socket/槽坐标自动跟随几何翻转
        internal Prefab FlipY() {
            int dropped = 0;
            var flipped = new string[Height];
            for (int y = 0; y < Height; y++) {
                char[] row = _art[Height - 1 - y].ToCharArray();
                for (int x = 0; x < row.Length; x++) {
                    row[x] = MapChar(row[x], ref dropped);
                }
                flipped[y] = new string(row);
            }
            Prefab result = Parse(Name + "_倒吊", flipped, _legend);
            result.MirrorDroppedSlots = dropped;
            return result;
        }

        private char MapChar(char c, ref int dropped) {
            switch (c) {
                case '1': return '3';
                case '3': return '1';
                case '2': return '4';
                case '4': return '2';
                case '_':
                    return _legend.HalfBrick == HalfBrickMirrorRule.ToPlatform ? '-' : '.';
            }
            if (_legend.Slots.TryGetValue(c, out PrefabSlotDef def)) {
                if (def.MirrorCh == '\0') {
                    dropped++;
                    return '.';
                }
                return def.MirrorCh;
            }
            return c;
        }

        internal Rectangle Area(int left, int top) => new(left, top, Width, Height);

        //几何盖章:只动非' '格;槽字符格按空+墙落地,家具第二遍统一放(§2.3两遍制)
        internal void StampGeometry(int left, int top, ushort brick, ushort wall, short platformFrameY) {
            for (int y = 0; y < Height; y++) {
                for (int x = 0; x < Width; x++) {
                    char c = _art[y][x];
                    int wx = left + x;
                    int wy = top + y;
                    switch (c) {
                        case ' ':
                            break;
                        case '#':
                            TileBrush.SetSolid(wx, wy, brick);
                            break;
                        case '_':
                            TileBrush.SetHalfBrick(wx, wy, brick);
                            break;
                        case '-':
                            TileBrush.ClearCell(wx, wy, wall);
                            TileBrush.SetPlatform(wx, wy, platformFrameY);
                            break;
                        case >= '1' and <= '4':
                            TileBrush.SetSloped(wx, wy, brick, (SlopeType)(c - '0'));
                            break;
                        default:
                            //'.'/'D'/语义槽字符全部先落成空+室内墙
                            TileBrush.ClearCell(wx, wy, wall);
                            break;
                    }
                }
            }
        }

        //语义槽落家具:锚定吸附(确定性,底锚向下/顶锚向上找最近支承,≤3格)后
        //交给PlaceObject做原版锚定校验,拒绝即跳过+记日志,绝不强写帧(§2.3/F9)
        internal FurnishReport PlaceFurniture(int left, int top) {
            FurnishReport report = default;
            foreach (PrefabSlot slot in Slots) {
                int wx = left + slot.X;
                int wy = top + slot.Y;
                PrefabSlotDef def = slot.Def;
                if (def.MarkerOnly) {
                    report.Markers++;
                    CWRMod.Instance.Logger.Info(
                        $"[Dungeonworld] prefab {Name} 留位槽{def.Name}@({wx},{wy})登记");
                    continue;
                }
                int anchorY = SnapAnchor(wx, wy, def.TopAnchor);
                if (def.TopAnchor && !HasClearanceBelow(wx, anchorY, def.ClearanceBelow)) {
                    report.Rejected++;
                    CWRMod.Instance.Logger.Warn(
                        $"[Dungeonworld] prefab {Name} 槽{def.Name}@({wx},{anchorY})下方净空<{def.ClearanceBelow},跳过");
                    continue;
                }
                if (WorldGen.PlaceObject(wx, anchorY, def.TileType, mute: true, def.Style)) {
                    report.Placed++;
                }
                else {
                    report.Rejected++;
                    CWRMod.Instance.Logger.Warn(
                        $"[Dungeonworld] prefab {Name} 槽{def.Name}@({wx},{anchorY})PlaceObject拒绝,跳过");
                }
            }
            return report;
        }

        //底锚槽向下贴地/顶锚槽向上贴顶,最多3格,找不到就地尝试(拒绝由PlaceObject裁决)
        private static int SnapAnchor(int x, int y, bool topAnchor) {
            int dir = topAnchor ? -1 : 1;
            for (int i = 0; i < 3; i++) {
                int probe = y + (i + 1) * dir;
                if (!WorldGen.InWorld(x, probe)) {
                    break;
                }
                if (Main.tile[x, probe].HasTile && Main.tile[x, probe].TileType != TileID.Platforms) {
                    return probe - dir;
                }
                if (Main.tile[x, y + i * dir].HasTile) {
                    break;
                }
            }
            return y;
        }

        private static bool HasClearanceBelow(int x, int y, int rows) {
            for (int i = 1; i <= rows; i++) {
                if (!WorldGen.InWorld(x, y + i) || Main.tile[x, y + i].HasTile) {
                    return false;
                }
            }
            return true;
        }
    }
}
