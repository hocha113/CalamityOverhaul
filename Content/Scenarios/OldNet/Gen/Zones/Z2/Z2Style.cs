using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z2
{
    //Z2 废墟带 材质表：数据中心废墟，火星导管镀层的机房语言
    //主题锚：遗址主产区，浅层/深层内部空间在这里首次成规模
    internal static class Z2Style
    {
        internal const ushort FloorBrick = TileID.StoneSlab;
        //机房壳体=火星导管镀层（TileID.cs L1397=350）
        internal const ushort RoomBrick = TileID.MartianConduitPlating;
        //室内墙=火星导管墙（WallID.cs L414=176）
        internal const ushort RoomWall = WallID.MartianConduit;
        internal const short PlatformFrameY = 9 * 18;
    }
}
