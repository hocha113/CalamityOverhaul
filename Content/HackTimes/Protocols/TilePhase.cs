using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>物块虚化协议</summary>
    internal class TilePhase : QuickHackDef
    {
        //虚化持续时间（帧，8秒）
        private const int PhaseDuration = 60 * 8;

        public override void SetDefaults() {
            UploadTime = 100;
            RamCost = 4;
            Category = QuickHackCategory.TileManip;
            SupportedTargets = HackTargetKind.Tile;
        }

        public override int GetDuration() => PhaseDuration;

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (target is not TileScannable s) return false;
            Tile tile = Main.tile[s.TileCoordX, s.TileCoordY];
            //已经被致动（actuated）的不再重复施加
            if (tile.IsActuated) return false;
            //不允许对神庙砖使用
            return tile.TileType != TileID.LihzahrdBrick;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not TileScannable s) return false;
            int tileX = s.TileCoordX;
            int tileY = s.TileCoordY;
            //计算多物块对象的尺寸，对整个物块进行虚化
            TileObjectData data = TileObjectData.GetTileData(Main.tile[tileX, tileY].TileType, 0);
            int w = data?.Width ?? 1;
            int h = data?.Height ?? 1;

            //找到左上角
            int originX = tileX;
            int originY = tileY;
            if (data != null) {
                Tile t = Main.tile[tileX, tileY];
                int frameWidth = data.CoordinateWidth + data.CoordinatePadding;
                int frameHeight = data.CoordinateHeights[0] + data.CoordinatePadding;
                int offX = t.TileFrameX % (data.Width * frameWidth) / frameWidth;
                int offY = t.TileFrameY % (data.Height * frameHeight) / frameHeight;
                originX = tileX - offX;
                originY = tileY - offY;
            }

            //本端致动广播，远端 TileSquare 同步
            if (!HackTimeNetSync.IsRemoteApply) {
                //致动整个物块对象
                for (int dx = 0; dx < w; dx++) {
                    for (int dy = 0; dy < h; dy++) {
                        int tx = originX + dx;
                        int ty = originY + dy;
                        if (tx < 0 || tx >= Main.maxTilesX || ty < 0 || ty >= Main.maxTilesY) continue;
                        Tile tile = Main.tile[tx, ty];
                        if (tile.HasTile && !tile.IsActuated) {
                            tile.IsActuated = true;
                        }
                    }
                }

                //网络同步
                if (Main.netMode != NetmodeID.SinglePlayer) {
                    NetMessage.SendTileSquare(-1, originX, originY, w, h);
                }
            }

            //虚化粒子效果
            Vector2 center = new(tileX * 16f + 8f, tileY * 16f + 8f);
            for (int i = 0; i < 12; i++) {
                Vector2 pos = center + Main.rand.NextVector2Circular(w * 8f, h * 8f);
                Vector2 vel = new(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2f, 0f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, new Color(100, 180, 255, 120), 0.8f).Configure(false, 30);
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.4f, Pitch = 0.6f }, center);
            }

            return true;
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (target is not TileScannable s) return true;
            int tileX = s.TileCoordX;
            int tileY = s.TileCoordY;
            //周期性虚化粒子提示效果仍在
            if (elapsed % 30 == 0) {
                Vector2 center = new(tileX * 16f + 8f, tileY * 16f + 8f);
                Vector2 vel = new(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1f, 0f));
                PRTLoader.NewParticle<PRT_Spark>(center, vel, new Color(100, 180, 255, 80), 0.5f).Configure(false, 20);
            }
            return true;
        }

        public override void OnRemove(IHackTarget target) {
            if (target is not TileScannable s) return;
            int tileX = s.TileCoordX;
            int tileY = s.TileCoordY;
            //恢复实体状态
            TileObjectData data = TileObjectData.GetTileData(Main.tile[tileX, tileY].TileType, 0);
            int w = data?.Width ?? 1;
            int h = data?.Height ?? 1;

            int originX = tileX;
            int originY = tileY;
            if (data != null) {
                Tile t = Main.tile[tileX, tileY];
                int frameWidth = data.CoordinateWidth + data.CoordinatePadding;
                int frameHeight = data.CoordinateHeights[0] + data.CoordinatePadding;
                int offX = t.TileFrameX % (data.Width * frameWidth) / frameWidth;
                int offY = t.TileFrameY % (data.Height * frameHeight) / frameHeight;
                originX = tileX - offX;
                originY = tileY - offY;
            }

            for (int dx = 0; dx < w; dx++) {
                for (int dy = 0; dy < h; dy++) {
                    int tx = originX + dx;
                    int ty = originY + dy;
                    if (tx < 0 || tx >= Main.maxTilesX || ty < 0 || ty >= Main.maxTilesY) continue;
                    Tile tile = Main.tile[tx, ty];
                    if (tile.HasTile && tile.IsActuated) {
                        tile.IsActuated = false;
                    }
                }
            }

            //恢复粒子效果
            Vector2 center = new(tileX * 16f + 8f, tileY * 16f + 8f);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                PRTLoader.NewParticle<PRT_Spark>(center, vel, new Color(80, 200, 255), 1.0f).Configure(false, 25);
            }

            //网络同步
            if (Main.netMode != NetmodeID.SinglePlayer) {
                NetMessage.SendTileSquare(-1, originX, originY, w, h);
            }
        }
    }
}
