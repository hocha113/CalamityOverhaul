using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Starfall.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Starfall
{
    /// <summary>
    /// 「余爆」的权威端调度：残酷模式下身处陨石坑的玩家周期性触发一次小型爆燃，
    /// 爆点从玩家附近的陨石瓦片采样（预告即承诺，走位即避）。
    /// 冷却为逐玩家状态（禁 static），档位只调频率不改机制形状；
    /// Boss 在场、城镇安宁圈内、并发到达上限时一律不触发
    /// </summary>
    internal class StarfallPlayer : ModPlayer
    {
        /// <summary>余爆冷却，档位只调频率（残酷 15s / 修罗 11.7s / 毁灭 8.7s 基准，另乘随机抖动）</summary>
        private static readonly int[] BurstCooldownByTier = [900, 700, 520];
        /// <summary>触发条件不满足时的复查间隔</summary>
        private const int RetryFrames = 45;
        /// <summary>入场宽限：刚进群系（或离场回场）至少 4 秒不炸</summary>
        private const int EntryGraceFrames = 240;
        /// <summary>余爆伤害 = 陨石头基准接触伤害 × 难度倍率 × 此值（环境机制无宿主怪，锚定本群系原版敌怪）</summary>
        private const float BurstDamageFrac = 0.4f;
        /// <summary>陨石头经典模式接触伤害基准</summary>
        private const int MeteorHeadContactBase = 25;
        /// <summary>余爆全局并发上限，超限跳过本次触发</summary>
        private const int BurstCap = 3;
        /// <summary>城镇安宁半径（瓦格）：圈内存活城镇 NPC 则不触发</summary>
        private const int TownRangeTiles = 60;
        /// <summary>采样窗：水平 4~34 瓦格（太近不公平、太远看不见），垂直 ±18</summary>
        private const int SampleMinX = 4;
        private const int SampleMaxX = 34;
        private const int SampleRangeY = 18;
        /// <summary>单次调度的采样尝试数</summary>
        private const int SampleTries = 20;

        /// <summary>余爆调度冷却（权威端决策私产，客户端不得用它驱动画面）</summary>
        private int burstTimer = EntryGraceFrames;

        public override void PostUpdate() {
            if (!VaultUtils.isServer && !VaultUtils.isSinglePlayer) {
                return;//决策只在权威端
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            if (!Player.ZoneMeteor) {
                //离场时把冷却抬到入场宽限值，回场不至于立刻挨炸
                if (burstTimer < EntryGraceFrames) {
                    burstTimer = EntryGraceFrames;
                }
                return;
            }
            if (burstTimer > 0) {
                burstTimer--;
                return;
            }
            burstTimer = RetryFrames;

            if (CWRWorld.HasBoss || Player.dead || Player.ghost) {
                return;//Boss 战与死亡状态下伤害性机制暂停
            }
            if (TownNpcNear(Player)) {
                return;//城镇安宁
            }
            int burstType = ModContent.ProjectileType<StarfallAfterburstProj>();
            if (CountActive(burstType) >= BurstCap) {
                return;
            }
            if (!TrySampleBurstPoint(Player, out Vector2 basePos)) {
                return;
            }

            float difficultyMul = Main.masterMode ? 3f : Main.expertMode ? 2f : 1f;
            int damage = (int)(MeteorHeadContactBase * difficultyMul * BurstDamageFrac);
            float scale = Main.rand.NextFloat(0.85f, 1.2f);
            Projectile.NewProjectile(Player.GetSource_Misc("CWR_StarfallAfterburst"),
                basePos, Vector2.Zero, burstType, damage, 1f, Main.myPlayer, scale);
            burstTimer = (int)(BurstCooldownByTier[tier - 1] * Main.rand.NextFloat(0.8f, 1.25f));
        }

        /// <summary>玩家约 60 格内是否有存活城镇 NPC（只在冷却尽头调用，非每帧）</summary>
        private static bool TownNpcNear(Player player) {
            float range = TownRangeTiles * 16f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(player.Center) < range) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>统计某类弹幕的活动实例数（到上限提前退出）</summary>
        private static int CountActive(int projType) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= BurstCap) {
                    break;
                }
            }
            return count;
        }

        /// <summary>
        /// 在玩家附近采一个爆点：必须是顶面暴露的陨石实心瓦片，且上方留有火柱空间。
        /// 水平至少隔 4 瓦格，绝不贴脸生成；采不到（悬空/野外边缘）本轮放弃
        /// </summary>
        private static bool TrySampleBurstPoint(Player player, out Vector2 basePos) {
            basePos = default;
            Point center = player.Center.ToTileCoordinates();
            for (int attempt = 0; attempt < SampleTries; attempt++) {
                int dx = Main.rand.Next(SampleMinX, SampleMaxX + 1)
                    * (Main.rand.NextBool() ? 1 : -1);
                int x = center.X + dx;
                int y = center.Y + Main.rand.Next(-SampleRangeY, SampleRangeY + 1);
                if (!WorldGen.InWorld(x, y, 10)) {
                    continue;
                }
                Tile tile = Framing.GetTileSafely(x, y);
                if (!tile.HasTile || tile.TileType != TileID.Meteorite || !WorldGen.SolidTile(x, y)) {
                    continue;
                }
                //顶面暴露且上方 3 格无实心：火柱要有地方窜
                if (WorldGen.SolidTile(x, y - 1) || WorldGen.SolidTile(x, y - 2)
                    || WorldGen.SolidTile(x, y - 3)) {
                    continue;
                }
                basePos = new Vector2(x * 16f + 8f, y * 16f);
                return true;
            }
            return false;
        }
    }
}
