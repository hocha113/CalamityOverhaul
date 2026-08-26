using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Nyxdepth.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Nyxdepth
{
    /// <summary>
    /// 「下沉流」调度器：逐玩家冷却（实例字段，禁 static 存逐玩家数据），
    /// 决策与生成只在权威端跑，客户端通过同步弹幕实体看到状态。<br/>
    /// 档位唯一旋钮之二：只调下沉流出现频率，柱体形状与拽力不随档位变。<br/>
    /// 公平闸门：Boss 在场不触发、约 60 格内有存活城镇 NPC 不触发、全局并发上限 2
    /// </summary>
    internal class NyxdepthPlayer : ModPlayer
    {
        /// <summary>触发冷却（帧），档位只调频率不换机制</summary>
        private static readonly int[] SinkCooldownByTier = [1500, 1180, 900];
        /// <summary>下沉流全局并发上限</summary>
        private const int SinkCap = 2;
        /// <summary>柱体半宽（像素），形状恒定</summary>
        private const float ColumnHalfWidth = 130f;
        /// <summary>拖拽存续帧，形状恒定</summary>
        private const int PullFrames = 220;
        /// <summary>城镇安宁半径（格）</summary>
        private const int TownPeaceTiles = 60;
        /// <summary>触发条件不满足时的复查间隔</summary>
        private const int RetryFrames = 90;

        /// <summary>本玩家的下沉流冷却，权威端的实例是唯一裁决者</summary>
        private int sinkCooldown;

        public override void Initialize() => sinkCooldown = 700;

        public override void PostUpdate() {
            if (!VaultUtils.isServer && !VaultUtils.isSinglePlayer) {
                return;//决策只在权威端
            }
            if (!CWRRef.Has || !GameModeSystem.BrutalActive) {
                return;
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            if (sinkCooldown > 0) {
                sinkCooldown--;
                return;
            }
            //入水的深渊玩家才有资格；Boss 在场位移机制整体停摆
            if (CWRWorld.HasBoss || Player.dead || !Player.wet || !Player.GetPlayerZoneAbyss()) {
                sinkCooldown = RetryFrames;
                return;
            }
            if (TownNearby()) {
                sinkCooldown = 240;
                return;
            }
            if (CountActive(ModContent.ProjectileType<NyxdepthSinkColumnProj>()) >= SinkCap) {
                sinkCooldown = 120;
                return;
            }
            if (!TryFindColumnSpot(out Vector2 center)) {
                sinkCooldown = RetryFrames;
                return;
            }

            Projectile.NewProjectile(Player.GetSource_Misc("NyxdepthSink"), center, Vector2.Zero,
                ModContent.ProjectileType<NyxdepthSinkColumnProj>(), 0, 0f, Main.myPlayer,
                ColumnHalfWidth, PullFrames);
            sinkCooldown = SinkCooldownByTier[tier - 1] + Main.rand.Next(240);
        }

        /// <summary>城镇安宁：约 60 格内有存活城镇 NPC 则不触发</summary>
        private bool TownNearby() {
            float radius = TownPeaceTiles * 16f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(Player.Center) < radius) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>统计某类弹幕的活动实例数（到 stopAt 提前退出；只在冷却尽头调用，非每帧）</summary>
        private static int CountActive(int projType, int stopAt = 8) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>
        /// 在玩家附近找一根竖直开阔水柱：柱心横向 ±300 像素内（挑战与玩家相关），
        /// 沿柱高取 5 个采样点，要求全程无实体块且至少 4 点有水
        /// </summary>
        private bool TryFindColumnSpot(out Vector2 center) {
            center = default;
            for (int attempt = 0; attempt < 8; attempt++) {
                Vector2 candidate = new(
                    Player.Center.X + Main.rand.NextFloat(-300f, 300f),
                    Player.Center.Y + Main.rand.NextFloat(-40f, 150f));
                bool blocked = false;
                int waterCount = 0;
                for (int s = -2; s <= 2; s++) {
                    Point tilePos = (candidate + new Vector2(0f, s * 180f)).ToTileCoordinates();
                    if (!WorldGen.InWorld(tilePos.X, tilePos.Y, 40)) {
                        blocked = true;
                        break;
                    }
                    if (WorldGen.SolidTile(tilePos.X, tilePos.Y)) {
                        blocked = true;
                        break;
                    }
                    Tile tile = Framing.GetTileSafely(tilePos.X, tilePos.Y);
                    if (tile.LiquidAmount > 100 && tile.LiquidType == Terraria.ID.LiquidID.Water) {
                        waterCount++;
                    }
                }
                if (blocked || waterCount < 4) {
                    continue;
                }
                center = candidate;
                return true;
            }
            return false;
        }
    }
}
