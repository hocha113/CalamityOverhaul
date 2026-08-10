using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>液体抽排：把一片液体直接抽干，岩浆同样吃</summary>
    internal class LiquidPurge : QuickHackDef
    {
        //抽排半径（格）
        private const int PurgeRadius = 6;

        public override void SetDefaults() {
            UploadTime = 70;
            RamCost = 3;
            Category = QuickHackCategory.TileManip;
            SupportedTargets = HackTargetKind.Water;
            UnlockedByDefault = false;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryLiquid(target, out int tileX, out int tileY)) return false;
            Vector2 center = HackTargets.TileWorldCenter(tileX, tileY);
            int liquidType = Main.tile[tileX, tileY].LiquidType;

            if (Main.netMode != NetmodeID.Server) EmitPurge(center, liquidType);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                for (int dx = -PurgeRadius; dx <= PurgeRadius; dx++) {
                    for (int dy = -PurgeRadius; dy <= PurgeRadius; dy++) {
                        if (dx * dx + dy * dy > PurgeRadius * PurgeRadius) continue;
                        int tx = tileX + dx;
                        int ty = tileY + dy;
                        if (!HackTargets.InWorld(tx, ty)) continue;
                        Tile tile = Main.tile[tx, ty];
                        if (tile.LiquidAmount == 0) continue;
                        tile.LiquidAmount = 0;
                        tile.LiquidType = 0;
                    }
                }
                int span = PurgeRadius * 2 + 1;
                WorldGen.RangeFrame(tileX - PurgeRadius, tileY - PurgeRadius,
                    tileX + PurgeRadius, tileY + PurgeRadius);
                if (Main.netMode != NetmodeID.SinglePlayer) {
                    NetMessage.SendTileSquare(-1, tileX - PurgeRadius,
                        tileY - PurgeRadius, span, span);
                }
            }
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            //抽干后液体量已归零，这里只认座标，不要求还有液体
            if (HackTargets.TryLiquidCoords(target, out int tileX, out int tileY)) {
                EmitPurge(HackTargets.TileWorldCenter(tileX, tileY), LiquidID.Water);
            }
        }

        private static void EmitPurge(Vector2 center, int liquidType) {
            Color tint = liquidType switch {
                LiquidID.Lava => new Color(255, 130, 40),
                LiquidID.Honey => new Color(255, 200, 60),
                LiquidID.Shimmer => new Color(220, 160, 255),
                _ => new Color(90, 170, 255),
            };
            //液体向下抽走，粒子统一朝中心下沉
            for (int i = 0; i < 20; i++) {
                Vector2 offset = Main.rand.NextVector2Circular(
                    PurgeRadius * 16f, PurgeRadius * 10f);
                PRTLoader.NewParticle<PRT_Spark>(center + offset,
                    new Vector2(offset.X * -0.02f, Main.rand.NextFloat(1f, 3f)),
                    tint, 0.9f)?.Configure(false, 20);
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.6f }, center);
        }
    }
}
