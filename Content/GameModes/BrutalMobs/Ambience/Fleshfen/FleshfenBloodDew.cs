using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Fleshfen.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Fleshfen
{
    /// <summary>
    /// 「血露」权威调度器：残酷模式下猩红之地的环境驱动雨帘（与 EvilBiome 包的 NPC 弹幕溅射/汲取分界：
    /// 此处不看任何 NPC，天上自己下）。决策与弹幕生成只在权威端；档位只调频率，机制形状不变。
    /// 公平闸：入界错拍宽限、Boss 在场不触发、城镇安宁（60 格内有城镇 NPC 不触发）、
    /// 全局并发上限、同点重复压制、头顶净空不足不触发
    /// </summary>
    internal class FleshfenBloodDew : ModSystem
    {
        /// <summary>血露冷却（档位 1/2/3 只调频率），到期后附加 0~<see cref="CooldownJitter"/> 抖动</summary>
        private static readonly int[] CooldownByTier = [3300, 2700, 2100];
        private const int CooldownJitter = 600;
        /// <summary>入界错拍宽限：刚踏进猩红之地至少这么久才可能首触</summary>
        private const int EntryGraceFrames = 1080;
        private const int EntryGraceJitter = 720;
        /// <summary>雨帘全局并发上限</summary>
        private const int CurtainCap = 2;
        /// <summary>同一玩家附近已有雨帘时不再叠放的判定半径</summary>
        private const float CurtainCrowdDist = 900f;
        /// <summary>城镇安宁半径（约 60 格）</summary>
        private const float TownPeaceDist = 960f;
        /// <summary>帘半宽（像素；固定形状，档位不改）</summary>
        private const float CurtainHalfWidthPx = 88f;
        /// <summary>头顶净空扫描上限（瓦格）与最低净空需求</summary>
        private const int HeadroomScanTiles = 17;
        private const int HeadroomMinTiles = 7;
        /// <summary>凝核抬升上限（瓦格）</summary>
        private const int CondenseRiseCapTiles = 13;

        public override void PostUpdateEverything() {
            //总开关 + 权威端专属（客户端一切可见结果来自弹幕实体同步）
            if (!GameModeSystem.BrutalActive || VaultUtils.isClient) {
                return;
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }

            foreach (Player player in Main.ActivePlayers) {
                FleshfenPlayer state = player.GetModPlayer<FleshfenPlayer>();
                if (!player.ZoneCrimson || player.dead || player.ghost) {
                    state.InZoneStreak = 0;
                    continue;
                }
                if (state.InZoneStreak == 0) {
                    //刚入界：错拍宽限，防踏入即被点名
                    state.DewCooldown = Math.Max(state.DewCooldown,
                        EntryGraceFrames + Main.rand.Next(EntryGraceJitter));
                }
                if (state.InZoneStreak < int.MaxValue) {
                    state.InZoneStreak++;
                }
                if (state.DewCooldown > 0) {
                    state.DewCooldown--;
                    continue;
                }

                //资格闸：不满足时短冷却复查，不烧掉整轮冷却
                if (CWRWorld.HasBoss) {
                    state.DewCooldown = 240;
                    continue;
                }
                if (TownNpcNear(player)) {
                    state.DewCooldown = 420;
                    continue;
                }
                if (CountCurtains() >= CurtainCap || CurtainNear(player)) {
                    state.DewCooldown = 180;
                    continue;
                }
                if (!TryFindCondensePoint(player, out Vector2 point)) {
                    state.DewCooldown = 300;
                    continue;
                }

                //凝核位置出手即锁定（预告即承诺，此后不追人）；伤害在弹体内按端本机结算，弹体恒 damage=0
                Projectile.NewProjectile(player.GetSource_Misc("CWRFleshfenDew"), point, Vector2.Zero,
                    ModContent.ProjectileType<FleshfenDewRainProj>(), 0, 0f, Main.myPlayer, CurtainHalfWidthPx);
                state.DewCooldown = CooldownByTier[tier - 1] + Main.rand.Next(CooldownJitter);
            }
        }

        /// <summary>城镇安宁：玩家 60 格内有存活城镇 NPC 则伤害性机制不触发</summary>
        private static bool TownNpcNear(Player player) {
            float distSq = TownPeaceDist * TownPeaceDist;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && Vector2.DistanceSquared(npc.Center, player.Center) < distSq) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>雨帘现存数（仅冷却到期时扫描，非每帧）</summary>
        private static int CountCurtains() {
            int type = ModContent.ProjectileType<FleshfenDewRainProj>();
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>目标玩家附近已有雨帘（避免同人被双帘夹击）</summary>
        private static bool CurtainNear(Player player) {
            int type = ModContent.ProjectileType<FleshfenDewRainProj>();
            float distSq = CurtainCrowdDist * CurtainCrowdDist;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && Vector2.DistanceSquared(proj.Center, player.Center) < distSq) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 找凝核点：从头顶向上数净空，地表即高空凝云、洞穴即贴顶凝露；
        /// 净空不足（憋屈矮洞/檐下）视为无处凝聚，本轮放弃
        /// </summary>
        private static bool TryFindCondensePoint(Player player, out Vector2 point) {
            point = default;
            int tileX = (int)(player.Center.X / 16f);
            int startY = (int)(player.Center.Y / 16f) - 1;
            int free = 0;
            for (int dy = 1; dy <= HeadroomScanTiles; dy++) {
                int tileY = startY - dy;
                if (!WorldGen.InWorld(tileX, tileY, 10) || WorldGen.SolidTile(tileX, tileY)) {
                    break;
                }
                free++;
            }
            if (free < HeadroomMinTiles) {
                return false;
            }
            int rise = Math.Min(free - 2, CondenseRiseCapTiles);
            point = new Vector2(player.Center.X, (startY - rise) * 16f + 8f);
            return true;
        }
    }
}
