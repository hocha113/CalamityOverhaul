using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Sunkendune.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Sunkendune
{
    /// <summary>
    /// 地下沙漠氛围的逐玩家状态与权威端事件调度（逐玩家状态放 ModPlayer，禁 static）。
    /// pitGrip/fallSoak 由场地弹幕逐帧点名本机玩家（镜像 WastesIceSlickZone 的本机判定），
    /// 移动学修改在 PostUpdateRunSpeeds 落地：只减速与缓拽，从不清速度，跳跃与钩爪照常可脱。
    /// 陷窝与沙瀑的生成决策只在权威端跑；档位只调频率（镜像 ByTier 公约），机制形状不随档位改变
    /// </summary>
    internal class SunkendunePlayer : ModPlayer
    {
        //==== 流沙陷窝调度 ====
        /// <summary>陷窝冷却，档位只调频率</summary>
        private static readonly int[] PitCooldownByTier = [900, 740, 580];
        /// <summary>陷窝全局并发上限</summary>
        private const int PitCap = 3;

        //==== 沙瀑调度 ====
        /// <summary>沙瀑冷却，档位只调频率</summary>
        private static readonly int[] FallCooldownByTier = [1150, 950, 750];
        /// <summary>沙瀑全局并发上限</summary>
        private const int FallCap = 3;

        /// <summary>触发条件不满足时的复查间隔</summary>
        private const int RetryFrames = 45;
        /// <summary>城镇安宁半径（约 60 格），圈内不起伤害/位移机制</summary>
        private const float TownPeaceRange = 960f;

        /// <summary>陷窝拽握标记（>0 生效；场地弹幕逐帧续 2，本类每帧衰减）</summary>
        internal int pitGrip;
        /// <summary>沙瀑淋压标记</summary>
        internal int fallSoak;
        /// <summary>权威端决策私产，客户端不得用它驱动画面</summary>
        private int pitCooldown;
        private int fallCooldown;

        public override void Initialize() {
            //出生错拍：避免多人同帧齐发
            pitCooldown = 700 + Main.rand.Next(400);
            fallCooldown = 1000 + Main.rand.Next(500);
        }

        public override void ResetEffects() {
            if (pitGrip > 0) {
                pitGrip--;
            }
            if (fallSoak > 0) {
                fallSoak--;
            }
        }

        public override void PostUpdateRunSpeeds() {
            if (Player.dead) {
                return;
            }
            if (pitGrip > 0) {
                //流沙裹足：横向显著减速（不禁锢）
                Player.maxRunSpeed *= 0.55f;
                Player.accRunSpeed *= 0.55f;
                Player.runAcceleration *= 0.62f;
                //向下缓拽：不清速度；跳跃初速（约 -5.1）高于阈值，钩爪期完全豁免
                if (Player.grapCount == 0 && Player.velocity.Y > -5f) {
                    Player.velocity.Y += 0.16f;
                }
            }
            if (fallSoak > 0 && Player.grapCount == 0 && Player.velocity.Y < 9f) {
                //沙瀑压顶：轻推向下
                Player.velocity.Y += 0.30f;
            }
        }

        public override void PostUpdate() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;//决策只在权威端
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            if (!Player.ZoneUndergroundDesert) {
                //离场：留出再入等待，进群系不会被立即偷袭
                if (pitCooldown < 240) {
                    pitCooldown = 240;
                }
                if (fallCooldown < 240) {
                    fallCooldown = 240;
                }
                return;
            }
            if (CWRWorld.HasBoss) {
                return;//Boss 在场暂停一切伤害/位移机制（冷却照走，战后自然恢复）
            }

            if (--pitCooldown <= 0) {
                pitCooldown = TrySpawnPit()
                    ? PitCooldownByTier[tier - 1] + Main.rand.Next(120)
                    : RetryFrames;
            }
            if (--fallCooldown <= 0) {
                fallCooldown = TrySpawnSandfall()
                    ? FallCooldownByTier[tier - 1] + Main.rand.Next(160)
                    : RetryFrames;
            }
        }

        //==================== 生成尝试（只在冷却尽头调用，非每帧）====================

        /// <summary>流沙陷窝：玩家站定时在脚下沙面锁点起窝（脚下沙类瓦片采样）</summary>
        private bool TrySpawnPit() {
            if (Player.velocity.Y != 0f) {
                return false;//站定才起窝，预告可读
            }
            if (CountActive(ModContent.ProjectileType<SunkenduneSinkPitProj>()) >= PitCap) {
                return false;
            }
            if (TownPeaceNearby(Player.Center)) {
                return false;
            }
            Point feet = Player.Bottom.ToTileCoordinates();
            int cx = feet.X + Main.rand.Next(-4, 5);
            if (!TryFindSandSurface(cx, feet.Y - 1, 12, out int surfaceY)) {
                return false;
            }
            //窝口跨度检查：两侧要有足够连续沙面（≥7/9 列），避免半悬空怪窝
            int good = 0;
            for (int dx = -4; dx <= 4; dx++) {
                if (TryFindSandSurface(cx + dx, surfaceY - 2, 5, out _)) {
                    good++;
                }
            }
            if (good < 7) {
                return false;
            }
            Vector2 basePos = new(cx * 16f + 8f, surfaceY * 16f);
            Projectile.NewProjectile(new EntitySource_Misc("CWR_SunkendunePit"), basePos, Vector2.Zero,
                ModContent.ProjectileType<SunkenduneSinkPitProj>(), 0, 0f, Main.myPlayer,
                SunkenduneSinkPitProj.DefaultHalfWidth);
            return true;
        }

        /// <summary>沙瀑：在玩家附近向上找沙类顶壁锁点（顶部沙瓦片采样），倾泻长度按净空实测</summary>
        private bool TrySpawnSandfall() {
            if (CountActive(ModContent.ProjectileType<SunkenduneSandfallProj>()) >= FallCap) {
                return false;
            }
            if (TownPeaceNearby(Player.Center)) {
                return false;
            }
            Point head = Player.Top.ToTileCoordinates();
            for (int attempt = 0; attempt < 6; attempt++) {
                int cx = head.X + Main.rand.Next(-14, 15);
                //向上找顶：最近的实心瓦必须是沙类
                int ceilY = -1;
                for (int dy = 2; dy <= 26; dy++) {
                    int y = head.Y - dy;
                    if (!WorldGen.InWorld(cx, y, 10)) {
                        break;
                    }
                    if (!WorldGen.SolidTile(cx, y)) {
                        continue;
                    }
                    if (IsSandFamily(Main.tile[cx, y].TileType)) {
                        ceilY = y;
                    }
                    break;
                }
                if (ceilY < 0) {
                    continue;
                }
                //顶下净空：至少 5 格才够倾泻成形
                int clear = 0;
                while (clear < 48 && WorldGen.InWorld(cx, ceilY + 1 + clear, 10)
                    && !WorldGen.SolidTile(cx, ceilY + 1 + clear)) {
                    clear++;
                }
                if (clear < 5) {
                    continue;
                }
                float lenPx = System.Math.Min(clear, 45) * 16f;
                Vector2 anchor = new(cx * 16f + 8f, (ceilY + 1) * 16f);
                Projectile.NewProjectile(new EntitySource_Misc("CWR_SunkenduneFall"), anchor, Vector2.Zero,
                    ModContent.ProjectileType<SunkenduneSandfallProj>(), SunkenduneSandfallProj.PourDamage, 1f,
                    Main.myPlayer, lenPx);
                return true;
            }
            return false;
        }

        //==================== 采样与公约辅助 ====================

        /// <summary>沙类瓦片全集（含邪化/神圣变体与硬沙岩族）</summary>
        internal static bool IsSandFamily(int type) =>
            type == TileID.Sand || type == TileID.Ebonsand || type == TileID.Crimsand || type == TileID.Pearlsand
            || type == TileID.HardenedSand || type == TileID.CorruptHardenedSand
            || type == TileID.CrimsonHardenedSand || type == TileID.HallowHardenedSand
            || type == TileID.Sandstone || type == TileID.CorruptSandstone
            || type == TileID.CrimsonSandstone || type == TileID.HallowSandstone;

        /// <summary>自 startY 向下找沙类站面：实心沙瓦且上方为空气</summary>
        private static bool TryFindSandSurface(int x, int startY, int depth, out int surfaceY) {
            surfaceY = 0;
            for (int dy = 0; dy < depth; dy++) {
                int y = startY + dy;
                if (!WorldGen.InWorld(x, y, 10)) {
                    return false;
                }
                if (!WorldGen.SolidTile(x, y)) {
                    continue;
                }
                if (!IsSandFamily(Main.tile[x, y].TileType)) {
                    return false;
                }
                surfaceY = y;
                return !WorldGen.SolidTile(x, y - 1);
            }
            return false;
        }

        /// <summary>统计某类弹幕的活动实例数（到 stopAt 提前退出；只在冷却尽头调用）</summary>
        private static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>城镇安宁：附近有存活城镇 NPC 时不触发伤害/位移机制（氛围照留）</summary>
        private static bool TownPeaceNearby(Vector2 pos) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(pos) < TownPeaceRange) {
                    return true;
                }
            }
            return false;
        }
    }
}
