namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z3
{
    //Z3 信号衰减带：疯域宿主。本轮扩容后不再只是空地
    //焦黑尖塔群/坍塌掩体给"信号尽头"实体证据，平台加密一档
    internal static class Z3Content
    {
        internal static void PlanAndBuild(OldNetBuildContext ctx) {
            //地表目录（本轮扩容）：尖塔群 + 坍塌掩体
            int spires = Z3Rooms.BuildScorchedSpireGroups(ctx, OldNetMetrics.ScorchedSpireGroupCount);
            int bunkers = Z3Rooms.BuildCollapsedBunkers(ctx, OldNetMetrics.CollapsedBunkerCount);
            //衰减区平台仍比废墟带稀破，但比 M2a 加密一档
            OldNetZoneCommon.PlaceFloatingSlabs(ctx.Area.Left + 30, ctx.Area.Right - 20,
                60, 110, Z3Style.FloorBrick);
            int rooms = OldNetZoneCommon.HangRoomsForBand(ctx, 3,
                Z3Style.RoomBrick, Z3Style.RoomWall, roomsPerLanding: 3, nodeChance: 0.5f);
            //带界立牌：底噪警告
            OldNetZoneCommon.PlaceBoundarySign(ctx.Area.Left + 4, OldNetTexts.OldNetSignFade.Value);
            CWRMod.Instance.Logger.Info($"[OldNet] Z3 rooms={rooms} spires={spires}"
                + $" bunkers={bunkers} graphConnected={ctx.Graph.IsConnected()}");
        }
    }
}
