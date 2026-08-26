using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Frostveil.Projectiles;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Frostveil
{
    /// <summary>
    /// 风雪墙调度：残酷模式地表雪原周期性放出横扫的阵风雪浪。
    /// 决策与生成只在权威端跑（镜像 WastesBrutalNPC 的授权模型），
    /// 客户端一律通过同步弹幕实体看到墙。Boss 在场/城镇附近不放，
    /// 档位只调浪频率，浪的形状（速度/厚度/明窗）恒定
    /// </summary>
    internal class FrostveilGaleScheduler : ModSystem
    {
        /// <summary>两浪间隔基准帧（残酷/修罗/毁灭）</summary>
        private static readonly int[] WaveCooldownByTier = [3300, 2760, 2220];
        /// <summary>暴雪期间浪更密</summary>
        private const float BlizzardCooldownMul = 0.62f;
        /// <summary>全局并发上限</summary>
        private const int WaveCap = 2;
        /// <summary>条件不满足时的复查间隔</summary>
        private const int RetryFrames = 300;
        /// <summary>生成点到目标的横向距离：速度 7px/f 下逼近约 214 帧，远超 45 帧预告底线</summary>
        private const float SpawnDistance = 1500f;
        /// <summary>向下寻找地表的最大瓦格数</summary>
        private const int GroundSearchTiles = 16;

        /// <summary>世界级冷却（权威端私产，客户端不用它驱动任何画面）</summary>
        private int cooldown;

        public override void ClearWorld() => cooldown = 900;

        public override void PostUpdateEverything() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;//决策只在权威端
            }
            if (!GameModeSystem.BrutalActive || CWRWorld.HasBoss) {
                return;//Boss 在场机制暂停，冷却冻结
            }
            if (--cooldown > 0) {
                return;
            }

            if (CountActiveWaves() >= WaveCap) {
                cooldown = RetryFrames;
                return;
            }
            if (!TryPickTarget(out Player target)) {
                cooldown = RetryFrames;
                return;
            }

            SpawnWave(target);

            int tier = Math.Clamp(GameModeSystem.EffectiveTier, 1, 3);
            float mul = Main.raining ? BlizzardCooldownMul : 1f;
            cooldown = (int)(WaveCooldownByTier[tier - 1] * mul
                * Main.rand.NextFloat(0.85f, 1.15f));
        }

        /// <summary>统计活动中的风雪墙（只在冷却尽头调用，非每帧）</summary>
        private static int CountActiveWaves() {
            int waveType = ModContent.ProjectileType<FrostveilGaleWallProj>();
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == waveType && ++count >= WaveCap) {
                    break;
                }
            }
            return count;
        }

        /// <summary>蓄水池抽样一名合规玩家：在辖区、活着、不在城镇安宁圈内</summary>
        private static bool TryPickTarget(out Player picked) {
            picked = null;
            int seen = 0;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !FrostveilPlayer.InZone(player)) {
                    continue;
                }
                if (FrostveilAmbience.NearTown(player.Center)) {
                    continue;
                }
                seen++;
                if (Main.rand.NextBool(seen)) {
                    picked = player;
                }
            }
            return picked != null;
        }

        private static void SpawnWave(Player target) {
            //浪顺着风来；无风时随机取向
            float wind = Main.windSpeedCurrent;
            int dir = MathF.Abs(wind) > 0.1f ? MathF.Sign(wind) : (Main.rand.NextBool() ? 1 : -1);

            //明窗锚定在目标脚下地面上方 6~10 格：跳一下就能进的高度
            float seamY = target.Center.Y - 60f;
            if (TryFindGround(target, out Vector2 ground)) {
                seamY = ground.Y - 96f - Main.rand.NextFloat(0f, 64f);
            }

            Vector2 spawnPos = new(target.Center.X - dir * SpawnDistance, seamY);
            //档位只调浪频率（见 PostUpdateEverything 的冷却），墙体形状恒定，故不传 tier
            Projectile.NewProjectile(new EntitySource_WorldEvent(), spawnPos,
                new Vector2(dir * FrostveilGaleWallProj.SweepSpeed, 0f),
                ModContent.ProjectileType<FrostveilGaleWallProj>(), 0, 0f, Main.myPlayer,
                seamY, dir, 0f);
        }

        /// <summary>从目标脚下向下找可站立地表（镜像 Wastes 的锚点找法）</summary>
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
}
