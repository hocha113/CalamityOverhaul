using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 冷凝固化：把一片液体冻成可以踩的薄冰，到期化开。<br/>
    /// 只拆自己放下的那些格子——记账在 <see cref="placedIce"/>，
    /// 不按"范围内所有冰"清，那会连玩家自己铺的冰一起铲了
    /// </summary>
    internal class Cryostasis : QuickHackDef
    {
        //固化半径（格）
        private const int FreezeRadius = 5;

        private static readonly Color Frost = new(170, 230, 255);

        //目标格 → 本次放下的冰。协议实例是单例，per-effect 状态只能外挂
        private static readonly Dictionary<(int X, int Y), List<Point>> placedIce = [];

        public override void SetDefaults() {
            UploadTime = 80;
            RamCost = 3;
            Category = QuickHackCategory.Control;
            SupportedTargets = HackTargetKind.Water;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 8;

        public override void Unload() {
            base.Unload();
            placedIce.Clear();
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryLiquid(target, out int tileX, out int tileY)) return false;
            //同一片水正在固化时不重复下料，否则两笔账互相盖掉
            return !placedIce.ContainsKey((tileX, tileY));
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryLiquid(target, out int tileX, out int tileY)) return false;
            Vector2 center = HackTargets.TileWorldCenter(tileX, tileY);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                List<Point> placed = [];
                for (int dx = -FreezeRadius; dx <= FreezeRadius; dx++) {
                    for (int dy = -FreezeRadius; dy <= FreezeRadius; dy++) {
                        if (dx * dx + dy * dy > FreezeRadius * FreezeRadius) continue;
                        int tx = tileX + dx;
                        int ty = tileY + dy;
                        if (!HackTargets.InWorld(tx, ty)) continue;
                        Tile tile = Main.tile[tx, ty];
                        //只在有液体又没实体块的格子结冰
                        if (tile.HasTile || tile.LiquidAmount < 32) continue;
                        tile.LiquidAmount = 0;
                        tile.HasTile = true;
                        tile.TileType = TileID.BreakableIce;
                        tile.Slope = SlopeType.Solid;
                        tile.IsHalfBlock = false;
                        placed.Add(new Point(tx, ty));
                    }
                }
                if (placed.Count > 0) {
                    placedIce[(tileX, tileY)] = placed;
                    SyncArea(tileX, tileY);
                }
            }

            if (Main.netMode != NetmodeID.Server) EmitFreeze(center);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryLiquidCoords(target, out int tileX, out int tileY)) {
                EmitFreeze(HackTargets.TileWorldCenter(tileX, tileY));
            }
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryLiquidCoords(target, out int tileX, out int tileY)) {
                EmitDrift(HackTargets.TileWorldCenter(tileX, tileY), elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryLiquidCoords(target, out int tileX, out int tileY)) {
                EmitDrift(HackTargets.TileWorldCenter(tileX, tileY), elapsed);
            }
        }

        public override void OnRemove(IHackTarget target) {
            if (!HackTargets.TryLiquidCoords(target, out int tileX, out int tileY)) return;

            if (placedIce.Remove((tileX, tileY), out List<Point> placed)) {
                foreach (Point point in placed) {
                    if (!HackTargets.InWorld(point.X, point.Y)) continue;
                    Tile tile = Main.tile[point.X, point.Y];
                    //玩家可能已经把它敲了或在上面盖了东西，只收回还是原样的那些
                    if (!tile.HasTile || tile.TileType != TileID.BreakableIce) continue;
                    tile.HasTile = false;
                    tile.TileType = 0;
                }
                SyncArea(tileX, tileY);
            }

            if (Main.netMode != NetmodeID.Server) {
                EmitThaw(HackTargets.TileWorldCenter(tileX, tileY));
            }
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (HackTargets.TryLiquidCoords(target, out int tileX, out int tileY)) {
                EmitThaw(HackTargets.TileWorldCenter(tileX, tileY));
            }
        }

        private static void SyncArea(int tileX, int tileY) {
            int span = FreezeRadius * 2 + 1;
            WorldGen.RangeFrame(tileX - FreezeRadius, tileY - FreezeRadius,
                tileX + FreezeRadius, tileY + FreezeRadius);
            if (Main.netMode != NetmodeID.SinglePlayer) {
                NetMessage.SendTileSquare(-1, tileX - FreezeRadius,
                    tileY - FreezeRadius, span, span);
            }
        }

        private static void EmitFreeze(Vector2 center) {
            for (int i = 0; i < 18; i++) {
                Vector2 offset = Main.rand.NextVector2Circular(
                    FreezeRadius * 16f, FreezeRadius * 12f);
                PRTLoader.NewParticle<PRT_Spark>(center + offset,
                    new Vector2(0f, Main.rand.NextFloat(-1.2f, -0.2f)), Frost, 0.9f)
                    ?.Configure(false, 26);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.Hacker with { Pitch = 0.8f }, center);
            }
        }

        private static void EmitDrift(Vector2 center, int elapsed) {
            if (elapsed % 24 != 0) return;
            Vector2 offset = Main.rand.NextVector2Circular(FreezeRadius * 16f, 12f);
            PRTLoader.NewParticle<PRT_Spark>(center + offset, Vector2.Zero, Frost, 0.5f)
                ?.Configure(false, 20);
        }

        private static void EmitThaw(Vector2 center) {
            for (int i = 0; i < 10; i++) {
                Vector2 offset = Main.rand.NextVector2Circular(FreezeRadius * 14f, 10f);
                PRTLoader.NewParticle<PRT_Spark>(center + offset,
                    new Vector2(0f, Main.rand.NextFloat(0.3f, 1.4f)),
                    new Color(120, 180, 220), 0.7f)?.Configure(false, 18);
            }
        }
    }
}
