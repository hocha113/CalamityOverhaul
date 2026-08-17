using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Prefabs;

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
    }
}
