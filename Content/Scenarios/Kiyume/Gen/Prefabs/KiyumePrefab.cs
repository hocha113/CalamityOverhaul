using System.Collections.Generic;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen.Prefabs
{
    //代码内字符画prefab机器：解析→几何盖章（KiyumeTileBrush）→语义槽走委托
    //解析即校验：行长不齐/未知字符直接抛，fail loud（编译期常量字符画等于单元测试）
    //镜像 OldNetPrefab，不引用；Kiyume 扩展 '~'水 '|'绳 'w'围栏墙 三个几何字符
    internal readonly struct KiyumePrefabSlot(int x, int y, KiyumePrefabSlotDef def)
    {
        internal readonly int X = x;
        internal readonly int Y = y;
        internal readonly KiyumePrefabSlotDef Def = def;
    }

    internal sealed class KiyumePrefab
    {
        internal readonly string Name;
        internal readonly int Width;
        internal readonly int Height;

        private readonly string[] _art;
        internal readonly List<KiyumePrefabSlot> Slots = [];

        private KiyumePrefab(string name, string[] art) {
            Name = name;
            _art = art;
            Height = art.Length;
            Width = art[0].Length;
        }

        internal static KiyumePrefab Parse(string name, string[] art, KiyumePrefabLegend legend) {
            if (art == null || art.Length == 0) {
                throw new System.InvalidOperationException($"[Kiyume] prefab {name} 空字符画");
            }
            var prefab = new KiyumePrefab(name, art);
            for (int y = 0; y < prefab.Height; y++) {
                if (art[y].Length != prefab.Width) {
                    throw new System.InvalidOperationException(
                        $"[Kiyume] prefab {name} 第{y}行长{art[y].Length}!=首行{prefab.Width}");
                }
                for (int x = 0; x < prefab.Width; x++) {
                    char c = art[y][x];
                    if (KiyumePrefabLegend.IsGeometryChar(c)) {
                        continue;
                    }
                    if (legend == null || !legend.Slots.TryGetValue(c, out KiyumePrefabSlotDef def)) {
                        throw new System.InvalidOperationException(
                            $"[Kiyume] prefab {name} 未知字符'{c}'@({x},{y})");
                    }
                    prefab.Slots.Add(new KiyumePrefabSlot(x, y, def));
                }
            }
            return prefab;
        }

        /// <summary>足印矩形（tile）：调用方拿去登记 ScatterExclusions/锚点表</summary>
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
                            KiyumeTileBrush.SetSolid(wx, wy, brick);
                            break;
                        case '-':
                            KiyumeTileBrush.ClearCell(wx, wy, wall);
                            KiyumeTileBrush.SetPlatform(wx, wy, platformFrameY);
                            break;
                        case >= '1' and <= '4':
                            KiyumeTileBrush.SetSloped(wx, wy, brick, (SlopeType)(c - '0'));
                            break;
                        case '~':
                            //水：构造性铺设（NormalUpdates=false 不流动），墙留在水后
                            KiyumeTileBrush.ClearCell(wx, wy, wall);
                            KiyumeTileBrush.SetWater(wx, wy);
                            break;
                        case '|':
                            KiyumeTileBrush.ClearCell(wx, wy, wall);
                            KiyumeTileBrush.SetRope(wx, wy);
                            break;
                        case 'w':
                            //围栏墙固定墙种：柜签名/院界都认 WoodenFence（KiyumeStructures 签名表同源）
                            KiyumeTileBrush.ClearCell(wx, wy, WallID.WoodenFence);
                            break;
                        default:
                            //'.'/'D'/语义槽字符全部先落成空+室内墙
                            KiyumeTileBrush.ClearCell(wx, wy, wall);
                            break;
                    }
                }
            }
        }

        //语义槽第二遍：委托拒绝即跳过+记日志，绝不强写
        internal (int placed, int rejected) PlaceSlots(int left, int top) {
            int placed = 0, rejected = 0;
            foreach (KiyumePrefabSlot slot in Slots) {
                int wx = left + slot.X;
                int wy = top + slot.Y;
                if (slot.Def.Place != null && slot.Def.Place(wx, wy)) {
                    placed++;
                }
                else {
                    rejected++;
                    CWRMod.Instance.Logger.Warn(
                        $"[Kiyume] prefab {Name} 槽{slot.Def.Name}@({wx},{wy})放置拒绝,跳过");
                }
            }
            return (placed, rejected);
        }
    }
}
