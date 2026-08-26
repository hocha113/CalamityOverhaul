using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Lumindepth.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Lumindepth
{
    /// <summary>
    /// 沉沦之海环境包的逐玩家状态：静谧涡流的调度时钟。
    /// 冷却是权威端决策私产（客户端副本不参与，随机数无同步语义），不入存档；
    /// 沉沦之海是和平群系，唯一机制就是这枚无伤涡流，档位只调频率与拉力
    /// </summary>
    internal class LumindepthPlayer : ModPlayer
    {
        /// <summary>涡流冷却（帧），-1 表示尚未在本群系起表</summary>
        private int vortexCooldown = -1;
        /// <summary>群系旗标低频采样计时（反射读灾厄旗标，不逐帧问）</summary>
        private int zoneRecheck;
        private bool zoneCached;

        /// <summary>涡流冷却档位表：1 残酷 / 2 修罗 / 3 毁灭，只调频率不换机制形状</summary>
        private static readonly int[] VortexCooldownByTier = [2400, 1950, 1500];
        /// <summary>涡流全局并发上限</summary>
        private const int VortexCap = 2;
        /// <summary>触发条件不满足时的复查间隔</summary>
        private const int RetryFrames = 90;
        /// <summary>城镇安宁半径（约 60 格）</summary>
        private const float TownPeaceRange = 960f;
        /// <summary>首次入海的起表延迟：先让玩家看够氛围再上机制</summary>
        private const int FirstEntryDelay = 900;

        /// <summary>权威端时钟推进（由 <see cref="LumindepthAmbience"/> 在权威端逐玩家调用）</summary>
        internal void TickVortexClock() {
            if (--zoneRecheck <= 0) {
                zoneRecheck = 15;
                zoneCached = Player.GetPlayerZoneSunkenSea();
            }
            if (!zoneCached) {
                return;//离海冻结时钟，回来接着走
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            if (vortexCooldown < 0) {
                vortexCooldown = FirstEntryDelay + Main.rand.Next(600);
                return;
            }
            if (--vortexCooldown > 0) {
                return;
            }
            vortexCooldown = TrySpawnVortex(tier)
                ? VortexCooldownByTier[tier - 1] + Main.rand.Next(600)
                : RetryFrames;
        }

        /// <summary>尝试在目标附近的成片水体中锚出一个涡心，成功返回 true</summary>
        private bool TrySpawnVortex(int tier) {
            if (!Player.wet) {
                return false;//目标不在水里就不起水涡
            }
            if (CountVortex() >= VortexCap) {
                return false;
            }
            //城镇安宁：附近有存活城镇 NPC 时不上挑战机制（氛围照常）
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(Player.Center) < TownPeaceRange) {
                    return false;
                }
            }
            for (int attempt = 0; attempt < 8; attempt++) {
                Vector2 center = Player.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(190f, 430f);
                if (!IsOpenWater(center)) {
                    continue;
                }
                //四向探针都要泡在水里，保证涡流长在成片水体中央而不是贴壁水洼
                bool open = true;
                for (int k = 0; k < 4; k++) {
                    if (!IsOpenWater(center + (MathHelper.PiOver2 * k).ToRotationVector2() * 70f)) {
                        open = false;
                        break;
                    }
                }
                if (!open) {
                    continue;
                }
                Projectile.NewProjectile(Player.GetSource_Misc("CWR_LumindepthVortex"), center, Vector2.Zero,
                    ModContent.ProjectileType<LumindepthVortexProj>(), 0, 0f, Main.myPlayer, tier);
                return true;
            }
            return false;
        }

        /// <summary>统计活动涡流数（到上限提前退出；只在冷却尽头调用，非每帧）</summary>
        private static int CountVortex() {
            int type = ModContent.ProjectileType<LumindepthVortexProj>();
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && ++count >= VortexCap) {
                    break;
                }
            }
            return count;
        }

        /// <summary>该点是否是开阔水体（非实心且水量充足的水）</summary>
        internal static bool IsOpenWater(Vector2 worldPos) {
            Point pt = worldPos.ToTileCoordinates();
            if (!WorldGen.InWorld(pt.X, pt.Y, 10)) {
                return false;
            }
            if (WorldGen.SolidTile(pt.X, pt.Y)) {
                return false;
            }
            Tile tile = Framing.GetTileSafely(pt.X, pt.Y);
            return tile.LiquidAmount > 160 && tile.LiquidType == LiquidID.Water;
        }
    }
}
