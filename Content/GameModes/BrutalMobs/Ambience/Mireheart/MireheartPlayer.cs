using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Mireheart.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Mireheart
{
    /// <summary>
    /// Mireheart 逐玩家状态与权威触发：
    /// 「沼气袋」按玩家计时，在其附近泥水边采样生成气泡包；
    /// 「蜂域警戒」按玩家累积蜂巢驻留，驻满激起蜂云。
    /// 逐玩家状态放 ModPlayer 实例字段（禁 static），
    /// 驻留在各端用同步的 Zone 旗标同规则推进（本地读数用于嗡鸣预告），
    /// 生成决策只在权威端落地。档位只调频率，不改机制形状
    /// </summary>
    internal class MireheartPlayer : ModPlayer
    {
        //==== 沼气袋（频率随档位）====
        /// <summary>沼气触发冷却，档位只调频率</summary>
        private static readonly int[] GasCooldownByTier = [660, 540, 430];
        /// <summary>条件不满足时的复查间隔</summary>
        private const int GasRetryFrames = 60;
        /// <summary>Boss 在场/城镇安宁时的长复查间隔</summary>
        private const int GasBlockedRetryFrames = 240;
        /// <summary>气泡包全局并发上限</summary>
        internal const int GasPocketCap = 3;
        /// <summary>采样窗口：距玩家的横向/纵向瓦格半径</summary>
        private const int GasSampleRangeX = 22;
        private const int GasSampleRangeY = 14;
        /// <summary>离玩家太近不生成（走位可避的最低余量，瓦格）</summary>
        private const int GasMinTilesFromPlayer = 5;
        /// <summary>与既有气泡包的最小间距（像素）</summary>
        private const float GasPocketSpacing = 90f;

        //==== 蜂域警戒（频率随档位）====
        /// <summary>激起蜂云所需驻留帧数，档位只调频率</summary>
        private static readonly int[] HiveDwellByTier = [840, 690, 560];
        /// <summary>蜂云全局并发上限</summary>
        internal const int BeeCloudCap = 2;
        /// <summary>离开蜂巢时驻留的衰减速度（帧/帧），立即平息</summary>
        private const int DwellDecayPerTick = 24;
        /// <summary>触发后驻留回落比例（持续压力，不清零）</summary>
        private const float DwellAfterTrigger = 0.35f;
        /// <summary>条件不满足时驻留小幅回退形成的复查间隔（帧）</summary>
        private const int DwellRetryBackoff = 45;

        //==== 通用 ====
        /// <summary>城镇安宁半径：约 60 格</summary>
        private const float TownCalmRadius = 960f;

        /// <summary>沼气触发计时（权威端决策私产）</summary>
        private int gasTimer = 300;
        /// <summary>蜂巢驻留帧数（各端同规则推进，本地玩家的读数喂嗡鸣预告）</summary>
        private int hiveDwell;

        /// <summary>蜂巢驻留进度 0~1（音画预告读这里）</summary>
        internal float HiveDwellFrac {
            get {
                int tier = Math.Clamp(GameModeSystem.EffectiveTier, 1, 3);
                return MathHelper.Clamp(hiveDwell / (float)HiveDwellByTier[tier - 1], 0f, 1f);
            }
        }

        /// <summary>玩家处于地下丛林主区（微区另判）</summary>
        internal static bool InUndergroundJungle(Player player)
            => player.ZoneJungle && (player.ZoneDirtLayerHeight || player.ZoneRockLayerHeight);

        /// <summary>约 60 格内有存活城镇 NPC，伤害性机制不触发</summary>
        internal static bool TownNpcNear(Vector2 pos) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(pos) < TownCalmRadius) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>统计某类弹幕的活动实例数（只在冷却尽头调用，非每帧）</summary>
        internal static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        public override void PostUpdate() {
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                hiveDwell = 0;
                return;
            }

            UpdateHiveDwell(tier);

            //生成决策只在权威端
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            UpdateGasPocket(tier);
        }

        /// <summary>死亡时驻留快速平息，沼气计时冻结（复活不被立刻埋伏）</summary>
        public override void UpdateDead() {
            hiveDwell = Math.Max(0, hiveDwell - DwellDecayPerTick);
        }

        //==== 蜂域警戒 ====

        /// <summary>驻留推进各端一致（输入是同步的 Zone 旗标）；触发只在权威端裁定</summary>
        private void UpdateHiveDwell(int tier) {
            if (!Player.ZoneHive) {
                if (hiveDwell > 0) {
                    hiveDwell = Math.Max(0, hiveDwell - DwellDecayPerTick);
                }
                return;
            }

            int threshold = HiveDwellByTier[tier - 1];
            if (hiveDwell < threshold) {
                hiveDwell++;
                return;
            }

            //驻满：权威端裁定是否放云；客户端把驻留钉在阈值（预告音保持满调）
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                hiveDwell = threshold;
                return;
            }
            if (CWRWorld.HasBoss || TownNpcNear(Player.Center)) {
                hiveDwell = threshold - DwellRetryBackoff;
                return;
            }
            int cloudType = ModContent.ProjectileType<MireheartBeeCloudProj>();
            if (CountActive(cloudType, BeeCloudCap) >= BeeCloudCap || HasOwnCloud(cloudType)) {
                hiveDwell = threshold - DwellRetryBackoff;
                return;
            }

            SpawnBeeCloud(cloudType);
            hiveDwell = (int)(threshold * DwellAfterTrigger);
        }

        /// <summary>一人同刻至多一朵蜂云</summary>
        private bool HasOwnCloud(int cloudType) {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == cloudType && (int)proj.ai[1] == Player.whoAmI) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 蜂云锁点：在玩家行进方向前方聚拢，缓速掠过玩家当前路径。
        /// 方向与速度在生成帧锁死（预告即承诺），随生成包同步，此后不再重瞄
        /// </summary>
        private void SpawnBeeCloud(int cloudType) {
            Vector2 dir = Player.velocity.LengthSquared() > 0.3f
                ? Vector2.Normalize(Player.velocity)
                : new Vector2(Player.direction, 0f);
            Vector2 spawn = Player.Center + dir * 340f
                + new Vector2(-dir.Y, dir.X) * Main.rand.NextFloat(-60f, 60f);
            Vector2 sweep = Player.Center + Player.velocity * 24f - spawn;
            if (sweep.LengthSquared() < 1f) {
                sweep = -dir;
            }
            Vector2 velocity = Vector2.Normalize(sweep) * 2.6f;

            Projectile.NewProjectile(Player.GetSource_Misc("CWR_MireheartHive"), spawn, velocity,
                cloudType, MireheartBeeCloudProj.CloudDamage(), 0f, Main.myPlayer,
                0f, Player.whoAmI);
        }

        //==== 沼气袋 ====

        /// <summary>沼气触发：附近泥水边采样一个气泡点，全程走位可避</summary>
        private void UpdateGasPocket(int tier) {
            if (Player.dead || !Player.active) {
                return;
            }
            if (!InUndergroundJungle(Player) || Player.ZoneHive || Player.ZoneLihzhardTemple) {
                //出区不倒计时；微区各有基调，沼气只属主区
                return;
            }
            if (--gasTimer > 0) {
                return;
            }
            if (CWRWorld.HasBoss || TownNpcNear(Player.Center)) {
                gasTimer = GasBlockedRetryFrames;
                return;
            }
            int pocketType = ModContent.ProjectileType<MireheartGasPocketProj>();
            if (CountActive(pocketType, GasPocketCap) >= GasPocketCap) {
                gasTimer = GasRetryFrames;
                return;
            }
            if (!TrySampleMudWaterSpot(out Vector2 spot, pocketType)) {
                gasTimer = GasRetryFrames;
                return;
            }

            float scale = Main.rand.NextFloat(0.9f, 1.25f);
            Projectile.NewProjectile(Player.GetSource_Misc("CWR_MireheartGas"), spot, Vector2.Zero,
                pocketType, 0, 0f, Main.myPlayer, scale);
            gasTimer = GasCooldownByTier[tier - 1] + Main.rand.Next(90);
        }

        /// <summary>
        /// 泥水边采样：水体格（水量足、非实心）正下方是泥土/丛林草的实心格，
        /// 上方留有气泡隆起的空间。取水面格中心为锚点
        /// </summary>
        private bool TrySampleMudWaterSpot(out Vector2 spot, int pocketType) {
            spot = default;
            Point center = Player.Center.ToTileCoordinates();
            for (int attempt = 0; attempt < 26; attempt++) {
                int dx = Main.rand.Next(-GasSampleRangeX, GasSampleRangeX + 1);
                int dy = Main.rand.Next(-GasSampleRangeY, GasSampleRangeY + 1);
                if (Math.Abs(dx) < GasMinTilesFromPlayer && Math.Abs(dy) < GasMinTilesFromPlayer) {
                    continue;
                }
                int x = center.X + dx;
                int y = center.Y + dy;
                if (!WorldGen.InWorld(x, y, 10)) {
                    continue;
                }
                Tile tile = Main.tile[x, y];
                if (tile.LiquidAmount < 120 || tile.LiquidType != LiquidID.Water || WorldGen.SolidTile(x, y)) {
                    continue;
                }
                if (!WorldGen.SolidTile(x, y + 1)) {
                    continue;
                }
                int belowType = Main.tile[x, y + 1].TileType;
                if (belowType != TileID.Mud && belowType != TileID.JungleGrass) {
                    continue;
                }
                if (WorldGen.SolidTile(x, y - 1)) {
                    continue;
                }
                Vector2 candidate = new(x * 16f + 8f, y * 16f + 8f);
                if (NearExistingPocket(candidate, pocketType)) {
                    continue;
                }
                spot = candidate;
                return true;
            }
            return false;
        }

        private static bool NearExistingPocket(Vector2 pos, int pocketType) {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == pocketType && proj.Distance(pos) < GasPocketSpacing) {
                    return true;
                }
            }
            return false;
        }
    }
}
