using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Prefabs;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z2
{
    //Z2 预制体：字符画一次解析，跨次生成复用（槽委托读活的 OldNetPlans.Budget）
    internal static class Z2Prefabs
    {
        private static OldNetPrefab _rackRoom;

        //机柜房：吊装服务器阵列 + 双侧门洞 + 地面数据节点槽
        internal static OldNetPrefab RackRoom => _rackRoom ??= OldNetPrefab.Parse("机柜房", [
            "####################",
            "#.#..#..#..#..#..#.#",
            "#.#..#..#..#..#..#.#",
            "#.#..#..#..#..#..#.#",
            "#..................#",
            "D..................D",
            "D..................D",
            "D..n............n..D",
            "####################",
        ], Legend);

        private static OldNetPrefab _archive;

        //数据仓：架层书库式存储间（平台层架 + 层间节点），deep 层的第二种房间语汇
        internal static OldNetPrefab ArchiveRoom => _archive ??= OldNetPrefab.Parse("数据仓", [
            "##################",
            "#................#",
            "#---.---.---.---.#",
            "#.n...........n..#",
            "#---.---.---.---.#",
            "D................D",
            "D................D",
            "D..n.........n...D",
            "##################",
        ], Legend);

        private static OldNetPrefabLegend Legend => new OldNetPrefabLegend()
            .Add(new OldNetPrefabSlotDef {
                Ch = 'n',
                Name = "数据节点",
                Place = static (x, y) => OldNetPlans.Budget.TryPlaceUnderPlain(x, y),
            });

        private static OldNetPrefab _vaultRoom;

        //金库：被自己人搬空的核心资产冷库（点子7）。双厚壳读作厚重，
        //右侧爆破门框对接深井厅走廊（门板残段倒伏在门内），
        //四座基座三空一完好，顶部断裂吊轨。深层限定，director 深层判定命中即必装哨塔
        internal static OldNetPrefab VaultRoom => _vaultRoom ??= OldNetPrefab.Parse("金库", [
            "####################",
            "####################",
            "##--.---..--.---..##",
            "##................##",
            "##................##",
            "##................##",
            "##................1#",
            "##..........p.....DD",
            "##.--.--.--.--....DD",
            "##.##.##.##.##.##2DD",
            "####################",
            "####################",
        ], VaultLegend);

        private static OldNetPrefabLegend VaultLegend => new OldNetPrefabLegend()
            .Add(new OldNetPrefabSlotDef {
                Ch = 'p',
                Name = "金库母本",
                //02槽位:金库母本（完好基座顶的高值预留，02 接手前降级为普通节点）
                Place = static (x, y) => {
                    if (OldNetPlans.Budget.TryPlaceUnderPlain(x, y)) {
                        return true;
                    }
                    //UnderPlain 配额耗尽兜底：直写一枚普通节点并入账（保 AuditNodes 口径一致）。
                    //被搬空的金库至少留那一座完好基座的母本，不许饿死成空房
                    if (OldNetNodeBudget.WriteNodeTile(x, y,
                        ModContent.TileType<Tiles.OldNetDataNodeTile>())) {
                        OldNetPlans.Budget.UnderPlainPlaced++;
                        return true;
                    }
                    return false;
                },
            });
    }
}
