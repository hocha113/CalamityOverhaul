using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Verdant.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Verdant
{
    /// <summary>
    /// 沼雾伏影调度器：环境驱动（非 NPC 驱动），周期性在丛林地表玩家附近的
    /// 低洼处/水边凝起雾团。决策与生成只在权威端，客户端通过弹幕实体原生同步看到一切。
    /// 档位只调雾团出现频率（与合拢圈收拢速度），机制形状不变
    /// </summary>
    internal class VerdantMireScheduler : ModSystem
    {
        /// <summary>雾团冷却（帧），档位只调频率</summary>
        private static readonly int[] FogCooldownByTier = [2900, 2300, 1750];
        /// <summary>雾团全局并发上限</summary>
        private const int FogCap = 3;
        /// <summary>条件不满足时的复查间隔</summary>
        private const int RetryFrames = 90;
        /// <summary>进世界宽限：先让玩家站稳再起雾</summary>
        private const int InitialGrace = 900;

        private static int cooldown = InitialGrace;

        public override void ClearWorld() => cooldown = InitialGrace;

        public override void PostUpdateEverything() {
            if (Main.gameMenu) {
                return;
            }
            if (!VaultUtils.isServer && !VaultUtils.isSinglePlayer) {
                return;//决策只在权威端
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            if (CWRWorld.HasBoss) {
                //Boss 在场不起新雾（存量雾团自会走完消散）
                if (cooldown < RetryFrames) {
                    cooldown = RetryFrames;
                }
                return;
            }
            if (--cooldown > 0) {
                return;
            }
            cooldown = RetryFrames;

            if (VerdantAmbience.CountActive(ModContent.ProjectileType<VerdantMireFogProj>()) >= FogCap) {
                return;
            }

            //从随机起点轮询，多人时不总偏向低位玩家
            int start = Main.rand.Next(Main.maxPlayers);
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[(start + i) % Main.maxPlayers];
                if (!player.active || player.dead || player.ghost
                    || !VerdantAmbience.InVerdant(player)
                    || VerdantAmbience.TownSanctuary(player.Center)) {
                    continue;
                }
                if (!TryFindMireAnchor(player, out Vector2 anchor)) {
                    continue;
                }
                Projectile.NewProjectile(new EntitySource_Misc("CWR_VerdantMire"), anchor, Vector2.Zero,
                    ModContent.ProjectileType<VerdantMireFogProj>(), 0, 0f, Main.myPlayer, tier);
                cooldown = (int)(FogCooldownByTier[tier - 1] * Main.rand.NextFloat(0.85f, 1.15f));
                return;
            }
        }

        /// <summary>
        /// 在玩家附近找沼雾锚点：优先水面，其次低洼地形（两侧地面高出 ≥2 格），雨天放宽到任意地面。
        /// 找不到（悬空/纯平地且无雨无水）则本轮放弃
        /// </summary>
        private static bool TryFindMireAnchor(Player target, out Vector2 anchor) {
            anchor = default;
            for (int attempt = 0; attempt < 7; attempt++) {
                float dx = attempt == 0 ? Main.rand.NextFloat(-90f, 90f) : Main.rand.NextFloat(-360f, 360f);
                int tileX = (int)((target.Bottom.X + dx) / 16f);
                int startY = (int)(target.Bottom.Y / 16f) - 5;
                if (!WorldGen.InWorld(tileX, startY, 24)) {
                    continue;
                }

                //向下探：先遇水即"水边"，先遇实体块则记地面
                int waterY = -1;
                int groundY = -1;
                for (int dy = 0; dy < 20; dy++) {
                    int ty = startY + dy;
                    if (!WorldGen.InWorld(tileX, ty, 24)) {
                        break;
                    }
                    Tile tile = Main.tile[tileX, ty];
                    if (tile.LiquidAmount > 64 && tile.LiquidType == LiquidID.Water) {
                        waterY = ty;
                        break;
                    }
                    if (WorldGen.SolidTile(tileX, ty)) {
                        groundY = ty;
                        break;
                    }
                }
                if (waterY > 0) {
                    anchor = new Vector2(tileX * 16f + 8f, waterY * 16f - 26f);
                    return true;
                }
                if (groundY < 0) {
                    continue;//悬空列
                }

                //低洼：两侧 8 格的地面都比这里高（瓦格 Y 更小）至少 2 格
                int left = VerdantAmbience.FindGroundTileY(tileX - 8, startY, 20);
                int right = VerdantAmbience.FindGroundTileY(tileX + 8, startY, 20);
                bool hollow = left > 0 && right > 0 && groundY - left >= 2 && groundY - right >= 2;

                //水边：地表附近 ±6 格内有水体
                bool nearWater = false;
                for (int ox = -6; ox <= 6 && !nearWater; ox++) {
                    int wx = tileX + ox;
                    if (!WorldGen.InWorld(wx, groundY, 24)) {
                        continue;
                    }
                    for (int oy = -1; oy <= 1; oy++) {
                        Tile tile = Main.tile[wx, groundY + oy];
                        if (tile.LiquidAmount > 64 && tile.LiquidType == LiquidID.Water) {
                            nearWater = true;
                            break;
                        }
                    }
                }

                if (hollow || nearWater || Main.raining) {
                    anchor = new Vector2(tileX * 16f + 8f, groundY * 16f - 34f);
                    return true;
                }
            }
            return false;
        }
    }
}
