using CalamityOverhaul.Content.Scenarios.OldNet.Tiles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Passes
{
    //P55 撒布：地表节点分带配额（普通全域/加密废墟+衰减/事件废墟带约束）
    //+ 各带 ctx.Scatter 条目。配额全部过 OldNetPlans.Budget，P80 审计
    internal class OldNetScatterPass : GenPass
    {
        public OldNetScatterPass() : base("OldNet Scatter", 0.4f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "撒布数据节点...";
            int plainType = ModContent.TileType<OldNetDataNodeTile>();
            int encryptType = ModContent.TileType<OldNetEncryptedNodeTile>();
            int ruinLeft = OldNetMetrics.WallCols + OldNetMetrics.FootCols;

            //普通节点全域（含新开的衰减区）
            int plain = PlaceBatch(OldNetMetrics.NodePlainCount,
                OldNetMetrics.WallCols + OldNetMetrics.SpawnFlatCols,
                OldNetMetrics.PlayRight - 20, plainType);
            OldNetPlans.Budget.PlainPlaced = plain;
            progress.Set(0.4);

            //加密节点：废墟带主产 + 衰减区高险高值
            int encrypt = PlaceBatch(OldNetMetrics.NodeEncryptCount,
                ruinLeft, OldNetMetrics.FadeLeft - 20, encryptType);
            encrypt += PlaceBatch(OldNetMetrics.NodeFadeEncryptCount,
                OldNetMetrics.FadeLeft + 30, OldNetMetrics.PlayRight - 20, encryptType);
            OldNetPlans.Budget.EncryptPlaced = encrypt;
            progress.Set(0.7);

            //事件节点：只落废墟带，离封锁区足够远
            ScatterEventNodes(ruinLeft);
            progress.Set(0.8);

            //M3：回声节点（废墟+衰减区，时停考古）+ 深潜缓存（衰减区限定）
            int echo = PlaceBatch(OldNetMetrics.EchoNodeCount,
                ruinLeft, OldNetMetrics.PlayRight - 20,
                ModContent.TileType<OldNetEchoNodeTile>());
            int caches = PlaceBatch(OldNetMetrics.CacheCount,
                OldNetMetrics.FadeLeft + 40, OldNetMetrics.PlayRight - 20,
                ModContent.TileType<OldNetCacheTile>());
            CWRMod.Instance.Logger.Info($"[OldNet] scatter echo={echo} caches={caches}");
            progress.Set(0.9);

            //带声明的撒布条目（M3 装饰扩容的入口）
            RunZoneEntries();
            progress.Set(1.0);

            CWRMod.Instance.Logger.Info(
                $"[OldNet] scatter plain={OldNetPlans.Budget.PlainPlaced}"
                + $" underPlain={OldNetPlans.Budget.UnderPlainPlaced}"
                + $" encrypt={OldNetPlans.Budget.EncryptPlaced}"
                + $" event={OldNetPlans.Budget.EventPlaced}");
        }

        /// <summary>从天空向下找该列首块实心（地板或平台），返回其上方空位行；无效给 -1</summary>
        internal static int FindNodeSlotY(int x) {
            int floorLimit = OldNetPlans.FloorTop != null
                ? OldNetPlans.FloorTop[x] + 2 : OldNetMetrics.FloorRow + OldNetMetrics.FadeWobble + 2;
            int surfaceY = -1;
            for (int y = OldNetMetrics.BorderThick + 4; y < floorLimit; y++) {
                Tile probe = Main.tile[x, y];
                if (probe.HasTile && Main.tileSolid[probe.TileType]) {
                    surfaceY = y;
                    break;
                }
            }
            if (surfaceY < 0 || Main.tile[x, surfaceY - 1].HasTile) {
                return -1;
            }
            return surfaceY - 1;
        }

        //同位置去重：左右 range 格内已有任何节点/回声/缓存则拒绝
        private static bool IsCrowded(int x, int y, int range) {
            int plainType = ModContent.TileType<OldNetDataNodeTile>();
            int encryptType = ModContent.TileType<OldNetEncryptedNodeTile>();
            int eventType = ModContent.TileType<OldNetEventNodeTile>();
            int echoType = ModContent.TileType<OldNetEchoNodeTile>();
            int cacheType = ModContent.TileType<OldNetCacheTile>();
            for (int dx = -range; dx <= range; dx++) {
                Tile near = Main.tile[x + dx, y];
                if (near.HasTile && (near.TileType == plainType
                    || near.TileType == encryptType || near.TileType == eventType
                    || near.TileType == echoType || near.TileType == cacheType)) {
                    return true;
                }
            }
            return false;
        }

        private static int PlaceBatch(int count, int minX, int maxX, int type) {
            int placed = 0;
            int attempts = 0;
            while (placed < count && attempts++ < count * 40) {
                int x = WorldGen.genRand.Next(minX, maxX);
                int slotY = FindNodeSlotY(x);
                if (slotY < 0 || OldNetPlans.InScatterExclusion(x, slotY) || IsCrowded(x, slotY, 6)) {
                    continue;
                }
                if (OldNetNodeBudget.WriteNodeTile(x, slotY, type)) {
                    placed++;
                }
            }
            return placed;
        }

        //事件节点：离封锁区 ≥ EventToSealMinCols（拉闸的人要跑一段才能吃到糖），彼此 ≥40 列
        private static void ScatterEventNodes(int ruinLeft) {
            int eventType = ModContent.TileType<OldNetEventNodeTile>();
            List<int> placedX = [];
            int attempts = 0;
            while (placedX.Count < OldNetMetrics.NodeEventCount
                && attempts++ < OldNetMetrics.NodeEventCount * 60) {
                int x = WorldGen.genRand.Next(ruinLeft, OldNetMetrics.FadeLeft - 20);
                bool tooClose = false;
                foreach (Rectangle box in OldNetPlans.SealBoxes) {
                    if (Math.Abs(x - (box.X + box.Width / 2)) < OldNetMetrics.EventToSealMinCols) {
                        tooClose = true;
                        break;
                    }
                }
                foreach (int prevX in placedX) {
                    if (Math.Abs(x - prevX) < 40) {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) {
                    continue;
                }
                int slotY = FindNodeSlotY(x);
                if (slotY < 0 || OldNetPlans.InScatterExclusion(x, slotY) || IsCrowded(x, slotY, 6)) {
                    continue;
                }
                if (OldNetNodeBudget.WriteNodeTile(x, slotY, eventType)) {
                    placedX.Add(x);
                    OldNetPlans.Budget.EventPlaced++;
                }
            }
        }

        //带声明条目：随机撒点→局部验证→失败计数保底退出（原版三段模式）
        private static void RunZoneEntries() {
            foreach (OldNetBuildContext ctx in new[] {
                OldNetPlans.Z1, OldNetPlans.Z2, OldNetPlans.Z3, OldNetPlans.Z4 }) {
                if (ctx == null) {
                    continue;
                }
                foreach (OldNetScatterEntry entry in ctx.Scatter) {
                    RunEntry(ctx, entry);
                }
            }
        }

        private static void RunEntry(OldNetBuildContext ctx, OldNetScatterEntry entry) {
            var placedPts = new List<Point>();
            int placed = 0, attempts = 0;
            int maxAttempts = entry.Target * 12;
            while (placed < entry.Target && attempts++ < maxAttempts) {
                int x = WorldGen.genRand.Next(ctx.Area.Left + 2, ctx.Area.Right - 2);
                int y;
                if (entry.SurfaceAnchored) {
                    y = FindNodeSlotY(x);
                    if (y < 0) {
                        continue;
                    }
                }
                else {
                    y = WorldGen.genRand.Next(ctx.Area.Top + 2, ctx.Area.Bottom - 2);
                }
                if (OldNetPlans.InScatterExclusion(x, y) || TooClose(placedPts, x, y, entry.DedupeDist)) {
                    continue;
                }
                if (entry.TryPlace(x, y)) {
                    placedPts.Add(new Point(x, y));
                    placed++;
                }
            }
            CWRMod.Instance.Logger.Info(
                $"[OldNet] scatter entry {ctx.Name}/{entry.Name} placed={placed}/{entry.Target}");
        }

        private static bool TooClose(List<Point> pts, int x, int y, int dist) {
            foreach (Point p in pts) {
                if (Math.Abs(p.X - x) < dist && Math.Abs(p.Y - y) < dist) {
                    return true;
                }
            }
            return false;
        }
    }
}
