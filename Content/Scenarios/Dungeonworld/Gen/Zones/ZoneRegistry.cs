using System.Collections.Generic;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Zones
{
    //子地带种别(Wave-2 B路环境三地带;Wave-3 地带专属怪按此查询投放)
    internal enum ZoneKind : byte
    {
        /// <summary>L4 沉没暗渠带:全淹检修暗渠+气钟龛+苔光</summary>
        DrownedCulvert,
        /// <summary>L5 落灰场:骨灰沉积+Tiled墙换派+灰口竖窖</summary>
        AshfallStratum,
        /// <summary>L6 渣汽疏泄带:静液岩浆渣池+狱石壳+间歇泉喷口</summary>
        SlagVentBelt,
    }

    //====================================================================
    //地带注册表:ZonePass 生成期登记矩形,运行时按 tile 查询(WAVE2-ENVIRONMENTS §3)。
    //
    //权威端=服务端:生成只在服务端跑,联机客户端本表恒空。消费者全部在服务端:
    //DungeonworldZoneNPC 刷怪权重、DungeonworldZoneVents 喷口驱动、Wave-3 地带专属怪。
    //客户端侧(氛围/提示)不读本表,改用 tile 采样签名(计划 §7,世界数据已同步)。
    //
    //回放制:ShouldSave=false,每次生成由 ZonePass 开头 Reset 重建;
    //无同步字段、无持久化。一个地带可登记多个矩形(渣汽疏泄带按窖登记)。
    //====================================================================
    internal static class ZoneRegistry
    {
        private static readonly List<(ZoneKind Kind, Rectangle Area)> _zones = [];

        /// <summary>全部登记项(只读视图,自报日志与 QA 用)</summary>
        internal static IReadOnlyList<(ZoneKind Kind, Rectangle Area)> All => _zones;

        internal static void Reset() => _zones.Clear();

        internal static void Register(ZoneKind kind, Rectangle area) => _zones.Add((kind, area));

        /// <summary>tile 坐标是否落在指定地带内(服务端查询)</summary>
        internal static bool Inside(ZoneKind kind, int tileX, int tileY) {
            foreach ((ZoneKind k, Rectangle area) in _zones) {
                if (k == kind && area.Contains(tileX, tileY)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>查 tile 所在地带;地带间构造上不重叠,先登记者胜</summary>
        internal static bool TryGetAt(int tileX, int tileY, out ZoneKind kind) {
            foreach ((ZoneKind k, Rectangle area) in _zones) {
                if (area.Contains(tileX, tileY)) {
                    kind = k;
                    return true;
                }
            }
            kind = default;
            return false;
        }
    }
}
