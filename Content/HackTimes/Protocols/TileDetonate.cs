using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>物块爆破</summary>
    internal class TileDetonate : QuickHackDef
    {
        //爆破半径（格）
        private const int BlastRadius = 3;
        //默认镐力，无镐也能破常见块
        private const int BasePickPower = 50;

        public override void SetDefaults() {
            UploadTime = 80;
            RamCost = 3;
            Category = QuickHackCategory.TileManip;
            SupportedTargets = HackTargetKind.Tile;
        }

        private static void AccumulatePickFromArray(Item[] items, ref int max) {
            if (items == null) return;
            for (int i = 0; i < items.Length; i++) {
                Item it = items[i];
                if (it != null && !it.IsAir && it.pick > max) {
                    max = it.pick;
                }
            }
        }

        //背包最高镐力，与默认取大，含存钱罐
        private static int GetEffectivePickPower(Player player) {
            int max = BasePickPower;
            if (player == null) return max;
            AccumulatePickFromArray(player.inventory, ref max);
            AccumulatePickFromArray(player.bank?.item, ref max);
            AccumulatePickFromArray(player.bank2?.item, ref max);
            AccumulatePickFromArray(player.bank3?.item, ref max);
            AccumulatePickFromArray(player.bank4?.item, ref max);
            return max;
        }

        //模组读 MinPick，原版硬编码表
        private static int GetTileMinPick(int x, int y) {
            Tile tile = Main.tile[x, y];
            ushort type = tile.TileType;
            ModTile modTile = TileLoader.GetTile(type);
            if (modTile != null) {
                return modTile.MinPick;
            }
            return GetVanillaMinPick(type);
        }

        //复刻 Player.PickTile
        private static int GetVanillaMinPick(int type) {
            if (type == TileID.Meteorite) return 50;
            if (type == TileID.Demonite || type == TileID.Crimtane) return 55;
            if (type == TileID.Ebonstone || type == TileID.Crimstone
                || type == TileID.Pearlstone || type == TileID.Hellstone) return 65;
            if (type == TileID.Cobalt || type == TileID.Palladium) return 100;
            if (type == TileID.Mythril || type == TileID.Orichalcum) return 110;
            if (type == TileID.Adamantite || type == TileID.Titanium) return 150;
            if (type == TileID.Chlorophyte) return 200;
            if (type == TileID.LihzahrdBrick) return 210;
            return 0;
        }

        private static bool CanBreakTileWithPickPower(int x, int y, int pickPower) {
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY) return false;
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile) return false;
            ushort type = tile.TileType;
            if (type == TileID.LihzahrdBrick || type == TileID.LihzahrdAltar) return false;
            if (type == TileID.DemonAltar || type == TileID.LunarMonolith) return false;
            //锤系块不炸
            if (Main.tileHammer[type]) return false;
            //宝箱、未打骷髅王的地牢砖
            if (Main.tileContainer[type] || (Main.tileDungeon[type] && !NPC.downedBoss3)) return false;
            return pickPower >= GetTileMinPick(x, y) && WorldGen.CanKillTile(x, y);
        }

        public override bool CanApplyTo(IHackTarget target) {
            return CanApplyToPlayer(target, Main.LocalPlayer);
        }

        public override bool CanApplyTo(IHackTarget target, Player caster) {
            return CanApplyToPlayer(target, caster);
        }

        private bool CanApplyToPlayer(IHackTarget target, Player caster) {
            if (!base.CanApplyTo(target)) return false;
            if (target is not TileScannable s) return false;
            Tile tile = Main.tile[s.TileCoordX, s.TileCoordY];
            if (tile.TileType == TileID.LihzahrdBrick || tile.TileType == TileID.LihzahrdAltar) {
                return false;
            }
            int pickPower = GetEffectivePickPower(caster);
            return pickPower >= GetTileMinPick(s.TileCoordX, s.TileCoordY);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not TileScannable s) return false;
            int tileX = s.TileCoordX;
            int tileY = s.TileCoordY;
            Vector2 center = new(tileX * 16f + 8f, tileY * 16f + 8f);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int pickPower = GetEffectivePickPower(caster);

                for (int dx = -BlastRadius; dx <= BlastRadius; dx++) {
                    for (int dy = -BlastRadius; dy <= BlastRadius; dy++) {
                        if (dx * dx + dy * dy > BlastRadius * BlastRadius) continue;
                        int tx = tileX + dx;
                        int ty = tileY + dy;
                        if (!CanBreakTileWithPickPower(tx, ty, pickPower)) continue;
                        WorldGen.KillTile(tx, ty, false, false, false);
                    }
                }

                if (Main.netMode != NetmodeID.SinglePlayer) {
                    NetMessage.SendTileSquare(-1, tileX - BlastRadius, tileY - BlastRadius,
                        BlastRadius * 2 + 1);
                }
            }

            if (Main.netMode != NetmodeID.Server) EmitVisual(center);

            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (target is not TileScannable s) return;
            int tileX = s.TileCoordX;
            int tileY = s.TileCoordY;
            if (tileX < 0 || tileX >= Main.maxTilesX
                || tileY < 0 || tileY >= Main.maxTilesY) return;
            EmitVisual(new Vector2(tileX * 16f + 8f, tileY * 16f + 8f));
        }

        private static void EmitVisual(Vector2 center) {
            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                Color c = Color.Lerp(new Color(255, 150, 50), new Color(255, 80, 30), Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Spark>(center, vel, c, 1.5f).Configure(false, 30);
            }

            for (int i = 0; i < 24; i++) {
                Vector2 pos = center + Main.rand.NextVector2Circular(BlastRadius * 10f, BlastRadius * 10f);
                Vector2 vel = (pos - center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 5f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, new Color(80, 200, 255), 0.6f).Configure(false, 20);
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = -0.2f }, center);
            }

        }
    }
}
