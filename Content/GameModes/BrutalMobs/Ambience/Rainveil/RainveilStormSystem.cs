using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rainveil.Projectiles;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rainveil
{
    /// <summary>
    /// 「雨帷落雷」调度器（权威端）。仅雷暴期（<see cref="Main.IsItStorming"/>）
    /// 对暴露在天空下的野外玩家附近随机地表点落雷：警示柱 ≥40 帧 → 雷击一拍。
    /// 普通降雨（非风暴）无任何危害，纯氛围归 <see cref="RainveilAmbience"/>。
    /// 档位只调频率，机制形状不变；Boss 在场与城镇安宁期一律停手。
    /// 决策与生成只在权威端，客户端经同步弹幕实体看到状态（镜像 RotmireVentSystem）
    /// </summary>
    internal class RainveilStormSystem : ModSystem
    {
        /// <summary>落雷调度间隔（帧），档位只调频率</summary>
        private static readonly int[] BoltIntervalByTier = [560, 470, 390];
        /// <summary>玩家人均落雷冷却（帧），公平下限 900，档位只调频率</summary>
        private static readonly int[] PlayerCooldownByTier = [1500, 1160, 900];
        /// <summary>雷击伤害 = 雨衣僵尸同档（原版僵尸）接触伤害 × 此系数</summary>
        private const float BoltDamageFrac = 0.9f;
        /// <summary>雷柱全局并发上限</summary>
        private const int BoltCap = 2;
        /// <summary>落点距任何玩家的最小距离（像素）</summary>
        private const float MinPlayerGapPx = 60f;
        /// <summary>落点采样的横向瓦格范围（距目标玩家）</summary>
        private const int SampleTilesMin = 5, SampleTilesMax = 25;
        /// <summary>条件不满足时的复查间隔</summary>
        private const int RetryFrames = 45;
        /// <summary>城镇安宁半径（60 格）：附近有存活城镇 NPC 时不落雷</summary>
        private const float TownPeaceRange = 960f;
        /// <summary>单次脉冲的地形采样尝试次数</summary>
        private const int SampleAttempts = 8;
        /// <summary>落点上方通天检查的瓦格数（雷要够得着地面）</summary>
        private const int OpenSkyTiles = 44;

        private static int boltTimer;
        /// <summary>轮询游标：多人时轮流选目标玩家（权威端私有调度状态）</summary>
        private static int robin;
        /// <summary>逐玩家冷却表（权威端私有调度状态，槽位复用时至多带来一次良性延迟）</summary>
        private static readonly int[] playerCooldown = new int[Main.maxPlayers];

        public override void ClearWorld() {
            boltTimer = 150;
            robin = 0;
            Array.Clear(playerCooldown, 0, playerCooldown.Length);
        }

        public override void PostUpdateEverything() {
            if (VaultUtils.isClient) {
                return;//决策与敌对弹幕生成只在权威端
            }
            if (!GameModeSystem.BrutalActive) {
                return;
            }
            if (CWRWorld.HasBoss) {
                return;//Boss 在场暂停伤害性环境机制（计时冻结，不积压）
            }
            if (!Main.raining || !Main.IsItStorming) {
                return;//普通雨无危害；计时同样冻结
            }

            for (int i = 0; i < playerCooldown.Length; i++) {
                if (playerCooldown[i] > 0) {
                    playerCooldown[i]--;
                }
            }
            if (boltTimer > 0) {
                boltTimer--;
                return;
            }

            int tier = Math.Clamp(GameModeSystem.EffectiveTier, 1, 3);
            if (TryBoltPulse(tier)) {
                boltTimer = BoltIntervalByTier[tier - 1] + Main.rand.Next(90);
            }
            else {
                boltTimer = RetryFrames;
            }
        }

        //==================== 目标选取 ====================

        /// <summary>轮流挑一位符合条件的玩家（野外地表、通天、冷却已过、城镇安宁不成立）</summary>
        private static Player PickPlayer() {
            int eligible = 0;
            foreach (Player player in Main.ActivePlayers) {
                if (Eligible(player)) {
                    eligible++;
                }
            }
            if (eligible == 0) {
                return null;
            }
            int pick = robin++ % eligible;
            foreach (Player player in Main.ActivePlayers) {
                if (!Eligible(player)) {
                    continue;
                }
                if (pick-- == 0) {
                    return player;
                }
            }
            return null;
        }

        private static bool Eligible(Player player) {
            if (player.dead || !player.ZoneOverworldHeight) {
                return false;
            }
            if (playerCooldown[player.whoAmI] > 0) {
                return false;
            }
            if (!ExposedToSky(player)) {
                return false;//有顶棚遮蔽=躲雨成立，不做落雷目标
            }
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(player.Center) < TownPeaceRange) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>玩家是否暴露在天空下（任一身位列头顶通天即算，镜像 DuneStorm 的判法）</summary>
        private static bool ExposedToSky(Player player) {
            int left = (int)(player.position.X / 16f);
            int right = (int)((player.position.X + player.width) / 16f);
            int top = (int)(player.position.Y / 16f);
            for (int x = left; x <= right; x++) {
                bool blocked = false;
                int ceiling = Math.Max(top - OpenSkyTiles, 10);
                for (int y = top - 1; y >= ceiling; y--) {
                    if (WorldGen.SolidTile(x, y)) {
                        blocked = true;
                        break;
                    }
                }
                if (!blocked) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>统计某类弹幕的活动实例数（只在脉冲时刻调用，非每帧）</summary>
        private static int CountActive(int projType) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType) {
                    count++;
                }
            }
            return count;
        }

        private static bool AnyProjNear(int projType, Vector2 pos, float range) {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && proj.Distance(pos) < range) {
                    return true;
                }
            }
            return false;
        }

        //==================== 落雷脉冲 ====================

        private static bool TryBoltPulse(int tier) {
            Player target = PickPlayer();
            if (target == null) {
                return false;
            }
            int boltType = ModContent.ProjectileType<RainveilThunderboltProj>();
            if (CountActive(boltType) >= BoltCap) {
                return false;
            }

            for (int attempt = 0; attempt < SampleAttempts; attempt++) {
                if (!TrySampleStrikeSite(target, out Vector2 basePos)) {
                    continue;
                }
                if (AnyProjNear(boltType, basePos, 120f)) {
                    continue;
                }
                Projectile.NewProjectile(new EntitySource_WorldEvent(), basePos, Vector2.Zero,
                    boltType, AnchorDamage(), 2f, Main.myPlayer,
                    Main.rand.NextFloat(0.92f, 1.1f));
                playerCooldown[target.whoAmI] = PlayerCooldownByTier[tier - 1];
                return true;
            }
            return false;
        }

        /// <summary>
        /// 落点采样：目标两侧 5~25 格内找露天地表（无墙、上方通天、非深液面），
        /// 且距任何玩家 ≥<see cref="MinPlayerGapPx"/> 像素
        /// </summary>
        private static bool TrySampleStrikeSite(Player target, out Vector2 basePos) {
            basePos = default;
            int px = (int)(target.Center.X / 16f);
            int py = (int)(target.Center.Y / 16f);
            int x = px + Main.rand.Next(SampleTilesMin, SampleTilesMax + 1)
                * (Main.rand.NextBool() ? 1 : -1);
            if (!FindFloor(x, py - 16, 44, out int floorY)) {
                return false;
            }
            Tile above = Framing.GetTileSafely(x, floorY - 1);
            if (above.WallType != WallID.None || above.LiquidAmount > 64) {
                return false;//有墙=室内，深液面不作落点
            }
            for (int dy = 1; dy <= OpenSkyTiles; dy++) {
                int y = floorY - dy;
                if (y < 10) {
                    break;
                }
                if (WorldGen.SolidTile(x, y)) {
                    return false;//头顶有遮挡，雷够不到这块地面
                }
            }
            Vector2 candidate = new(x * 16f + 8f, floorY * 16f);
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && player.Distance(candidate) < MinPlayerGapPx) {
                    return false;
                }
            }
            basePos = candidate;
            return true;
        }

        /// <summary>自上而下先越过实心找到空腔，再落到腔底实心面（镜像 RotmireVentSystem）</summary>
        private static bool FindFloor(int x, int fromY, int span, out int floorY) {
            floorY = 0;
            bool inAir = false;
            for (int y = fromY; y < fromY + span; y++) {
                if (!WorldGen.InWorld(x, y, 10)) {
                    return false;
                }
                bool solid = WorldGen.SolidTile(x, y);
                if (!inAir) {
                    if (!solid) {
                        inAir = true;
                    }
                    continue;
                }
                if (solid) {
                    floorY = y;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 雷击伤害锚：雨衣僵尸同档的原版僵尸（<see cref="NPCID.Zombie"/>）接触伤害 × 0.9。
        /// 敌对弹幕命中玩家时原版自带 ×2（难度再放大），此处预除 ×0.5，
        /// 经典档实收 ≈ 接触伤 × 0.9，随难度自动跟走，禁止再叠任何手动难度乘数。
        /// ContentSamples 在载入期（普通难度）构建，是稳定的原版基准；读取异常时用具名常量兜底
        /// </summary>
        private static int AnchorDamage() {
            int baseDamage = 14;
            if (ContentSamples.NpcsByNetId.TryGetValue(NPCID.Zombie, out NPC sample) && sample.damage > 0) {
                baseDamage = sample.damage;
            }
            return Math.Max(1, (int)(baseDamage * BoltDamageFrac * 0.5f));
        }
    }
}
