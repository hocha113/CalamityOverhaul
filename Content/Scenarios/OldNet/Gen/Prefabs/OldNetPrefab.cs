using System.Collections.Generic;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Prefabs
{
    //代码内字符画prefab机器：解析→几何盖章（OldNetTileBrush）→语义槽走委托
    //解析即校验：行长不齐/未知字符直接抛，fail loud
    //镜像 Dungeonworld Prefab 的精简版（无垂直镜像需求，暂不导出 socket）
    internal readonly struct OldNetPrefabSlot(int x, int y, OldNetPrefabSlotDef def)
    {
        internal readonly int X = x;
        internal readonly int Y = y;
        internal readonly OldNetPrefabSlotDef Def = def;
    }

    internal sealed class OldNetPrefab
    {
        internal readonly string Name;
        internal readonly int Width;
        internal readonly int Height;

        private readonly string[] _art;
        internal readonly List<OldNetPrefabSlot> Slots = [];

        private OldNetPrefab(string name, string[] art) {
            Name = name;
            _art = art;
            Height = art.Length;
            Width = art[0].Length;
        }

        internal static OldNetPrefab Parse(string name, string[] art, OldNetPrefabLegend legend) {
            if (art == null || art.Length == 0) {
                throw new System.InvalidOperationException($"[OldNet] prefab {name} 空字符画");
            }
            var prefab = new OldNetPrefab(name, art);
            for (int y = 0; y < prefab.Height; y++) {
                if (art[y].Length != prefab.Width) {
                    throw new System.InvalidOperationException(
                        $"[OldNet] prefab {name} 第{y}行长{art[y].Length}!=首行{prefab.Width}");
                }
                for (int x = 0; x < prefab.Width; x++) {
                    char c = art[y][x];
                    if (OldNetPrefabLegend.IsGeometryChar(c)) {
                        continue;
                    }
                    if (!legend.Slots.TryGetValue(c, out OldNetPrefabSlotDef def)) {
                        throw new System.InvalidOperationException(
                            $"[OldNet] prefab {name} 未知字符'{c}'@({x},{y})");
                    }
                    prefab.Slots.Add(new OldNetPrefabSlot(x, y, def));
                }
            }
            return prefab;
        }

        internal Rectangle Area(int left, int top) => new(left, top, Width, Height);

        //几何盖章：只动非' '格；槽字符格先落成空+墙，槽委托第二遍统一执行（两遍制）
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
                            OldNetTileBrush.SetSolid(wx, wy, brick);
                            break;
                        case '-':
                            OldNetTileBrush.ClearCell(wx, wy, wall);
                            OldNetTileBrush.SetPlatform(wx, wy, platformFrameY);
                            break;
                        case >= '1' and <= '4':
                            OldNetTileBrush.SetSloped(wx, wy, brick, (SlopeType)(c - '0'));
                            break;
                        default:
                            //'.'/'D'/语义槽字符全部先落成空+室内墙
                            OldNetTileBrush.ClearCell(wx, wy, wall);
                            break;
                    }
                }
            }
        }

        //语义槽第二遍：委托拒绝即跳过+记日志，绝不强写
        internal (int placed, int rejected) PlaceSlots(int left, int top) {
            int placed = 0, rejected = 0;
            foreach (OldNetPrefabSlot slot in Slots) {
                int wx = left + slot.X;
                int wy = top + slot.Y;
                if (slot.Def.Place != null && slot.Def.Place(wx, wy)) {
                    placed++;
                }
                else {
                    rejected++;
                    CWRMod.Instance.Logger.Warn(
                        $"[OldNet] prefab {Name} 槽{slot.Def.Name}@({wx},{wy})放置拒绝,跳过");
                }
            }
            return (placed, rejected);
        }
    }
}
