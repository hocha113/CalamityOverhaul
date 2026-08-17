namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z3
{
    //Z3 信号衰减带：M2a 只给稀疏浮空板与最小浅层结构——地形先行，
    //疯域规则/深潜限定池 M3 接管
    internal static class Z3Content
    {
        internal static void PlanAndBuild(OldNetBuildContext ctx) {
            //衰减区平台更稀更破：读作信号尽头的残骸
            OldNetZoneCommon.PlaceFloatingSlabs(ctx.Area.Left + 30, ctx.Area.Right - 20,
                80, 130, Z3Style.FloorBrick);
            int rooms = OldNetZoneCommon.HangRoomsForBand(ctx, 3,
                Z3Style.RoomBrick, Z3Style.RoomWall, roomsPerLanding: 2, nodeChance: 0.5f);
            //带界立牌：底噪警告
            OldNetZoneCommon.PlaceBoundarySign(ctx.Area.Left + 4, OldNetTexts.OldNetSignFade.Value);
            CWRMod.Instance.Logger.Info($"[OldNet] Z3 rooms={rooms} graphConnected={ctx.Graph.IsConnected()}");
        }
    }
}
