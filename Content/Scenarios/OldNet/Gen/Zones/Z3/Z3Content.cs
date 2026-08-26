namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z3
{
    //Z3 信号衰减带：疯域宿主。本轮扩容后不再只是空地
    //焦黑尖塔群/坍塌掩体给"信号尽头"实体证据，平台加密一档
    internal static class Z3Content
    {
        internal static void PlanAndBuild(OldNetBuildContext ctx) {
            //检疫关卡首位抢占边界带（点子6，固定锚跨带），东缘让位给带界立牌
            int checkpointEast = Z3Rooms.BuildCheckpoint();
            //坠亡巨物次位抢位（点子9 旗舰，大足印先落，尖塔/掩体自动绕行）
            Z3Giant.BuildFallenGiant(ctx);
            //地表目录（本轮扩容）：尖塔群 + 坍塌掩体
            int spires = Z3Rooms.BuildScorchedSpireGroups(ctx, OldNetMetrics.ScorchedSpireGroupCount);
            int bunkers = Z3Rooms.BuildCollapsedBunkers(ctx, OldNetMetrics.CollapsedBunkerCount);
            //静默哨雷（04 固定威胁，P55 执行）：衰减区配额 4，贴糖检查在 TryPlace 内自查
            ctx.Scatter.Add(new OldNetScatterEntry {
                Name = "mine-sentry",
                Target = OldNetMetrics.MineCountFade,
                DedupeDist = OldNetMetrics.MineDedupeDist,
                SurfaceAnchored = true,
                TryPlace = Tiles.OldNetSentryMineTile.TryPlaceNearLoot,
            });
            //衰减区平台仍比废墟带稀破，但比 M2a 加密一档
            OldNetZoneCommon.PlaceFloatingSlabs(ctx.Area.Left + 30, ctx.Area.Right - 20,
                60, 110, Z3Style.FloorBrick);
            int rooms = OldNetZoneCommon.HangRoomsForBand(ctx, 3,
                Z3Style.RoomBrick, Z3Style.RoomWall, roomsPerLanding: 3, nodeChance: 0.5f);
            //遗物陈设层（P55 执行）：衰减区全样式均布，TryWrite 按带自动上焦黑变体
            ctx.Scatter.Add(new OldNetScatterEntry {
                Name = "relic-z3",
                Target = OldNetMetrics.RelicScatterZ3,
                DedupeDist = 16,
                SurfaceAnchored = true,
                TryPlace = static (x, y) => Tiles.OldNetRelicTile.TryWrite(x, y, Tiles.OldNetRelicTile.RollStyle(3)),
            });
            //带界立牌：底噪警告（关卡建成时让到其东缘外，语义并入关卡群落）
            OldNetZoneCommon.PlaceBoundarySign(checkpointEast > 0
                ? checkpointEast + 6 : ctx.Area.Left + 4, OldNetTexts.OldNetSignFade.Value);
            CWRMod.Instance.Logger.Info($"[OldNet] Z3 rooms={rooms} spires={spires}"
                + $" bunkers={bunkers} graphConnected={ctx.Graph.IsConnected()}");
        }
    }
}
