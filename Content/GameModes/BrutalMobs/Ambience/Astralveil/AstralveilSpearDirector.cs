using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Astralveil.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Astralveil
{
    /// <summary>
    /// 「星辉矛」夜间点名调度器（决策只在权威端）。
    /// 低频点名一名星辉瘟疫内的玩家：脚下锁点星纹圈（预告即承诺，圈固定不追踪）→
    /// 星矛从天纵落命中圈心 → 落点交给「感染绽放」余威。
    /// 有其他候选时永不连续点名同一玩家；档位只调点名频率与绽放持续，机制形状不变
    /// </summary>
    internal class AstralveilSpearDirector : ModSystem
    {
        /// <summary>点名间隔（帧），档位只调频率</summary>
        private static readonly int[] SpearIntervalByTier = [1400, 1150, 900];
        /// <summary>绽放存续（帧），档位只调持续；随星矛 ai[0] 传给绽放实体</summary>
        private static readonly int[] BloomDurationByTier = [300, 390, 480];
        /// <summary>
        /// 同一玩家两次被点名的最短间隔（随档位缩短，≈全局间隔的 1.35~1.43 倍）。
        /// 单人时唯一候选就是上次被点名者，节奏由本数组主导（全局间隔被它盖过），
        /// 档位递进合同由此保住；多人时全局间隔主导
        /// </summary>
        private static readonly int[] NamedCooldownByTier = [2000, 1550, 1200];
        /// <summary>条件不满足时的复查间隔</summary>
        private const int RetryFrames = 45;
        /// <summary>星纹圈全局并发上限</summary>
        private const int MarkCap = 2;
        /// <summary>感染绽放全局并发上限</summary>
        private const int BloomCap = 3;
        /// <summary>城镇安宁半径（60 格内有存活城镇 NPC 则不点名）</summary>
        private const float TownPeaceRange = 960f;
        /// <summary>向下寻找地表的最大瓦格数（目标悬空超此值则放弃本次点名）</summary>
        private const int GroundSearchTiles = 14;

        private static int globalTimer;
        /// <summary>上一次被点名者（whoAmI+名字双校验，防断线重连后槽位复用误判）</summary>
        private static int lastNamedWho = -1;
        private static string lastNamedName = "";
        /// <summary>候选缓存（复用避免逐次分配）</summary>
        private static readonly int[] candidateBuf = new int[Main.maxPlayers];

        public override void ClearWorld() {
            globalTimer = 0;
            lastNamedWho = -1;
            lastNamedName = "";
        }

        public override void PostUpdateEverything() {
            if (VaultUtils.isClient) {
                return;
            }
            if (!GameModeSystem.BrutalActive || !CWRRef.Has) {
                return;
            }

            TickNamedCooldowns();

            //星矛夜间限定：白天只留氛围层；黎明把计时抬回小值，入夜不至开幕即落矛
            if (Main.dayTime) {
                if (globalTimer < RetryFrames * 2) {
                    globalTimer = RetryFrames * 2;
                }
                return;
            }
            if (CWRWorld.HasBoss) {
                return;//Boss 在场暂停伤害机制
            }
            if (--globalTimer > 0) {
                return;
            }
            globalTimer = RetryFrames;

            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            //并发上限（镜像 Cap 家族风格）：星纹圈与在场绽放都要有余量
            if (CountActive(ModContent.ProjectileType<AstralveilSpearMarkProj>()) >= MarkCap) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<AstralveilBloomProj>()) >= BloomCap) {
                return;
            }

            int picked = PickTarget();
            if (picked < 0) {
                return;
            }
            Player target = Main.player[picked];
            if (!TryFindGround(target, out Vector2 basePos)) {
                return;
            }

            //ai[0]=绽放存续（随包同步，各端一致）；ai[1]=被点名者+1（个人预警提亮用）
            Projectile.NewProjectile(new EntitySource_Misc("CWRAstralveilSpear"), basePos, Vector2.Zero,
                ModContent.ProjectileType<AstralveilSpearMarkProj>(), AstralveilSpearMarkProj.ImpactDamage,
                0f, Main.myPlayer, BloomDurationByTier[tier - 1], picked + 1);

            target.GetModPlayer<AstralveilPlayer>().NamedCooldown = NamedCooldownByTier[tier - 1];
            lastNamedWho = picked;
            lastNamedName = target.name;
            //±14% 抖动避免机械节拍
            int interval = SpearIntervalByTier[tier - 1];
            globalTimer = interval + Main.rand.Next(-interval / 7, interval / 7 + 1);
        }

        /// <summary>收集候选并点名：优先未被上次点名者；全场只剩上次点名者（单人）才允许复点</summary>
        private static int PickTarget() {
            int count = 0;
            int preferred = 0;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (!player.active || player.dead || player.ghost) {
                    continue;
                }
                if (!player.GetPlayerZoneAstral()) {
                    continue;
                }
                if (player.GetModPlayer<AstralveilPlayer>().NamedCooldown > 0) {
                    continue;
                }
                if (TownNpcNearby(player)) {
                    continue;
                }
                if (!TryFindGround(player, out _)) {
                    continue;
                }
                candidateBuf[count++] = i;
                if (!IsLastNamed(player)) {
                    preferred++;
                }
            }
            if (count == 0) {
                return -1;
            }
            if (preferred <= 0) {
                return candidateBuf[Main.rand.Next(count)];
            }
            int roll = Main.rand.Next(preferred);
            for (int i = 0; i < count; i++) {
                if (IsLastNamed(Main.player[candidateBuf[i]])) {
                    continue;
                }
                if (roll == 0) {
                    return candidateBuf[i];
                }
                roll--;
            }
            return -1;
        }

        private static bool IsLastNamed(Player player)
            => player.whoAmI == lastNamedWho && player.name == lastNamedName;

        /// <summary>逐玩家点名冷却递减（权威端集中推进，绕开死亡玩家不跑 PostUpdate 的坑）</summary>
        private static void TickNamedCooldowns() {
            foreach (Player player in Main.ActivePlayers) {
                AstralveilPlayer modPlayer = player.GetModPlayer<AstralveilPlayer>();
                if (modPlayer.NamedCooldown > 0) {
                    modPlayer.NamedCooldown--;
                }
            }
        }

        /// <summary>城镇安宁：候选玩家 60 格内有存活城镇 NPC 则不触发伤害机制</summary>
        private static bool TownNpcNearby(Player player) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(player.Center) < TownPeaceRange) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>统计某类弹幕的活动实例数（只在冷却尽头调用，非每帧）</summary>
        private static int CountActive(int projType, int stopAt = 16) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>从目标脚下向下找可站立地表，返回圈心锚点（找不到视为悬空，放弃）</summary>
        private static bool TryFindGround(Player target, out Vector2 basePos) {
            basePos = default;
            Point feet = target.Bottom.ToTileCoordinates();
            for (int dy = 0; dy < GroundSearchTiles; dy++) {
                int tileY = feet.Y + dy;
                if (!WorldGen.InWorld(feet.X, tileY, 10)) {
                    return false;
                }
                if (WorldGen.SolidTile(feet.X, tileY)) {
                    basePos = new Vector2(feet.X * 16f + 8f, tileY * 16f);
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>逐玩家点名冷却（权威端调度私产：不入存档、不走同步、不用 static 存逐玩家数据）</summary>
    internal class AstralveilPlayer : ModPlayer
    {
        /// <summary>再次可被星矛点名前的剩余帧数</summary>
        internal int NamedCooldown;
    }
}
