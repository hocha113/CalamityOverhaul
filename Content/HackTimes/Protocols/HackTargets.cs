using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.Industrials;
using InnoVault.TileProcessors;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 协议里反复出现的目标解包。<br/>
    /// 每个协议自己抄一遍这七行在只有三五个协议时还行，扩到二十来个就纯是噪音了
    /// </summary>
    internal static class HackTargets
    {
        public static bool TryNpc(IHackTarget target, out NPC npc) {
            npc = null;
            if (target is not NpcScannable s || s.NpcIndex < 0
                || s.NpcIndex >= Main.maxNPCs) {
                return false;
            }
            npc = Main.npc[s.NpcIndex];
            return npc.active;
        }

        public static bool TryProjectile(IHackTarget target, out Projectile projectile) {
            projectile = null;
            if (target is not ProjectileScannable s || s.ProjectileIndex < 0
                || s.ProjectileIndex >= Main.maxProjectiles) {
                return false;
            }
            projectile = Main.projectile[s.ProjectileIndex];
            return projectile.active;
        }

        public static bool TryItem(IHackTarget target, out Item item) {
            item = null;
            if (target is not ItemScannable s || s.ItemIndex < 0
                || s.ItemIndex >= Main.maxItems) {
                return false;
            }
            item = Main.item[s.ItemIndex];
            return item.active && !item.IsAir;
        }

        /// <summary>液体格；返回的是格座标，液体量已确认大于零</summary>
        public static bool TryLiquid(IHackTarget target, out int tileX, out int tileY) {
            tileX = -1;
            tileY = -1;
            if (target is not WaterScannable s) {
                return false;
            }
            tileX = s.TileCoordX;
            tileY = s.TileCoordY;
            return InWorld(tileX, tileY) && Main.tile[tileX, tileY].LiquidAmount > 0;
        }

        /// <summary>液体格座标，不校验是否还有液体（抽干后仍要能取到落点）</summary>
        public static bool TryLiquidCoords(IHackTarget target, out int tileX, out int tileY) {
            tileX = -1;
            tileY = -1;
            if (target is not WaterScannable s) {
                return false;
            }
            tileX = s.TileCoordX;
            tileY = s.TileCoordY;
            return InWorld(tileX, tileY);
        }

        /// <summary>物块目标下面挂着的机械 TP</summary>
        public static bool TryMachine(IHackTarget target, out MachineTP machine) {
            machine = null;
            if (target is not TileScannable s) {
                return false;
            }
            if (!VaultUtils.SafeGetTopLeft(s.TileCoordX, s.TileCoordY, out var topLeft)) {
                return false;
            }
            if (!TileProcessorLoader.TP_Point_To_Instance.TryGetValue(topLeft, out var tp)) {
                return false;
            }
            if (tp is not MachineTP m || !tp.Active) {
                return false;
            }
            machine = m;
            return true;
        }

        public static bool InWorld(int tileX, int tileY)
            => tileX >= 0 && tileX < Main.maxTilesX
                && tileY >= 0 && tileY < Main.maxTilesY;

        /// <summary>格座标转世界中心点</summary>
        public static Vector2 TileWorldCenter(int tileX, int tileY)
            => new(tileX * 16f + 8f, tileY * 16f + 8f);
    }
}
