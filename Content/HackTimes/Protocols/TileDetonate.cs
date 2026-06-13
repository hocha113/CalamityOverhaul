using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>物块爆破协议</summary>
    internal class TileDetonate : QuickHackDef
    {
        //爆破半径（格子数）
        private const int BlastRadius = 3;
        //协议默认镐力，无镐也能破常见块
        private const int BasePickPower = 50;

        public override void SetDefaults() {
            UploadTime = 80;
            RamCost = 3;
            Category = QuickHackCategory.TileManip;
            SupportedTargets = HackTargetKind.Tile;
        }

        //累计单个容器中物品的最高镐力
        private static void AccumulatePickFromArray(Item[] items, ref int max) {
            if (items == null) return;
            for (int i = 0; i < items.Length; i++) {
                Item it = items[i];
                if (it != null && !it.IsAir && it.pick > max) {
                    max = it.pick;
                }
            }
        }

        //取背包最高镐力，与协议默认取大
        private static int GetEffectivePickPower(Player player) {
            int max = BasePickPower;
            if (player == null) return max;
            AccumulatePickFromArray(player.inventory, ref max);
            //含存钱罐等容器
            AccumulatePickFromArray(player.bank?.item, ref max);
            AccumulatePickFromArray(player.bank2?.item, ref max);
            AccumulatePickFromArray(player.bank3?.item, ref max);
            AccumulatePickFromArray(player.bank4?.item, ref max);
            return max;
        }

        //物块镐力门槛，模组读 MinPick，原版硬编码表
        private static int GetTileMinPick(int x, int y) {
            Tile tile = Main.tile[x, y];
            ushort type = tile.TileType;
            ModTile modTile = TileLoader.GetTile(type);
            if (modTile != null) {
                return modTile.MinPick;
            }
            return GetVanillaMinPick(type);
        }

        //原版镐力门槛，复刻 Player.PickTile
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

        //判断单格瓦砾是否能被当前镐力击碎
        private static bool CanBreakTileWithPickPower(int x, int y, int pickPower) {
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY) return false;
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile) return false;
            ushort type = tile.TileType;
            //跳过原版强制屏蔽的核心方块
            if (type == TileID.LihzahrdBrick || type == TileID.LihzahrdAltar) return false;
            if (type == TileID.DemonAltar || type == TileID.LunarMonolith) return false;
            //锤系方块（如平台、灯笼链等）不算镐子可破，跳过避免误炸不该炸的设施
            if (Main.tileHammer[type]) return false;
            //部分基础设施保护：宝箱、出生点等
            if (Main.tileContainer[type] || (Main.tileDungeon[type] && !NPC.downedBoss3)) return false;
            //核心判断：镐力是否达到该物块要求
            return pickPower >= GetTileMinPick(x, y) && WorldGen.CanKillTile(x, y);
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (target is not TileScannable s) return false;
            //不允许对神庙砖等极高硬度物块使用
            Tile tile = Main.tile[s.TileCoordX, s.TileCoordY];
            if (tile.TileType == TileID.LihzahrdBrick || tile.TileType == TileID.LihzahrdAltar) {
                return false;
            }
            //目标本体的镐力门槛也必须满足，否则没有意义
            int pickPower = GetEffectivePickPower(Main.LocalPlayer);
            return pickPower >= GetTileMinPick(s.TileCoordX, s.TileCoordY);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not TileScannable s) return false;
            int tileX = s.TileCoordX;
            int tileY = s.TileCoordY;
            Vector2 center = new(tileX * 16f + 8f, tileY * 16f + 8f);

            //本端破坏广播 SendTileSquare，远端仅视觉
            if (!HackTimeNetSync.IsRemoteApply) {
                int pickPower = GetEffectivePickPower(caster);

                //范围破坏
                for (int dx = -BlastRadius; dx <= BlastRadius; dx++) {
                    for (int dy = -BlastRadius; dy <= BlastRadius; dy++) {
                        if (dx * dx + dy * dy > BlastRadius * BlastRadius) continue;
                        int tx = tileX + dx;
                        int ty = tileY + dy;
                        if (!CanBreakTileWithPickPower(tx, ty, pickPower)) continue;
                        WorldGen.KillTile(tx, ty, false, false, false);
                    }
                }

                //同步网络
                if (Main.netMode != NetmodeID.SinglePlayer) {
                    NetMessage.SendTileSquare(-1, tileX - BlastRadius, tileY - BlastRadius,
                        BlastRadius * 2 + 1);
                }
            }

            //爆破粒子
            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                Color c = Color.Lerp(new Color(255, 150, 50), new Color(255, 80, 30), Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Spark>(center, vel, c, 1.5f).Configure(false, 30);
            }

            //碎片粒子向外飞散
            for (int i = 0; i < 24; i++) {
                Vector2 pos = center + Main.rand.NextVector2Circular(BlastRadius * 10f, BlastRadius * 10f);
                Vector2 vel = (pos - center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 5f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, new Color(80, 200, 255), 0.6f).Configure(false, 20);
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = -0.2f }, center);
            }

            return true;
        }
    }
}
