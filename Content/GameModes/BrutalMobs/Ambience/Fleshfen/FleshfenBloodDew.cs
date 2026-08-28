using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Fleshfen.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Fleshfen
{
    /// <summary>
    /// 「血露」权威调度器：残酷模式下猩红之地的环境驱动雨帘（与 EvilBiome 包的 NPC 弹幕溅射/汲取分界：
    /// 此处不看任何 NPC，血自猩红大地渗出凝落）。决策与弹幕生成只在权威端；档位只调频率，机制形状不变。
    /// 凝核锚定猩红世界物：立足面上方凝云、洞穴猩红顶壁贴顶凝露；
    /// 玩家距离只是采样窗安全网，不是生成轴（世界锚契约）。
    /// 公平闸：入界错拍宽限、Boss 在场不触发、城镇安宁（60 格内有城镇 NPC 不触发）、
    /// 全局并发上限、同点重复压制、无合格猩红锚或净空不足不投放
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
        /// <summary>锚面净空扫描上限（瓦格）与最低净空需求（不足视为憋屈矮洞，弃投）</summary>
        private const int HeadroomScanTiles = 17;
        private const int HeadroomMinTiles = 7;
        /// <summary>凝核在立足面锚上方的抬升上限（瓦格）</summary>
        private const int CondenseRiseCapTiles = 13;
        /// <summary>锚点采样窗半径（瓦格，以玩家为中心）：把投放限定在玩家活动范围内的筛选安全网</summary>
        private const int AnchorSampleRangeX = 26;
        private const int AnchorSampleRangeY = 18;
        /// <summary>每轮随机列采样次数，全部落空则本轮不投放（定时重试，禁退化回玩家轴）</summary>
        private const int AnchorSampleAttempts = 24;

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
                if (!TryFindCondensePoint(player, out Vector2 core, out Vector2 anchorFace)) {
                    //无合格猩红锚：本轮不投放，定时重试（禁退化回玩家轴）
                    state.DewCooldown = 300;
                    continue;
                }

                //凝核位置出手即锁定（预告即承诺，此后不追人）；伤害在弹体内按端本机结算，弹体恒 damage=0；
                //ai[1]=锚面 Y（与凝核同列），随生成包原生同步，供各端画"雨从锚物来"的凝聚演出
                Projectile.NewProjectile(player.GetSource_Misc("CWRFleshfenDew"), core, Vector2.Zero,
                    ModContent.ProjectileType<FleshfenDewRainProj>(), 0, 0f, Main.myPlayer,
                    CurtainHalfWidthPx, anchorFace.Y);
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

        /// <summary>合格猩红锚物瓦：原版猩红家族（草地/Crimstone/冰/沙系）+ 血肉块；
        /// 猩红植被非实心，由其立足瓦代表</summary>
        private static bool IsCrimsonAnchorTile(int type)
            => TileID.Sets.Crimson[type] || type == TileID.FleshBlock;

        /// <summary>
        /// 找凝核点：生成轴是猩红世界物，不是玩家坐标。在玩家活动范围（采样窗安全网）内随机取列，
        /// 列内向上找猩红顶壁（洞穴变体优先，贴顶凝露）、向下找猩红立足面（锚物上方凝云）；
        /// 锚面 Y 一并交出，供弹体画"血珠自锚物汇聚攀升"的凝聚演出。
        /// 净空不足（憋屈矮洞/檐下）或全部列落空 → 本轮不投放
        /// </summary>
        private static bool TryFindCondensePoint(Player player, out Vector2 core, out Vector2 anchorFace) {
            core = default;
            anchorFace = default;
            int playerTileX = (int)(player.Center.X / 16f);
            int playerTileY = (int)(player.Center.Y / 16f);
            for (int attempt = 0; attempt < AnchorSampleAttempts; attempt++) {
                int tileX = playerTileX + Main.rand.Next(-AnchorSampleRangeX, AnchorSampleRangeX + 1);
                if (!WorldGen.InWorld(tileX, playerTileY, 10) || WorldGen.SolidTile(tileX, playerTileY)) {
                    continue;//该列在玩家高度被实心占据，不属于玩家活动空域
                }

                //洞穴变体：顶壁是猩红瓦且下方有落帘净空 → 凝核贴顶壁下挂
                int ceilingY = ScanSolid(tileX, playerTileY, -1);
                if (ceilingY >= 0 && IsCrimsonAnchorTile(Main.tile[tileX, ceilingY].TileType)
                    && CountClearance(tileX, ceilingY, 1) >= HeadroomMinTiles) {
                    anchorFace = new Vector2(tileX * 16f + 8f, (ceilingY + 1) * 16f);
                    core = new Vector2(anchorFace.X, anchorFace.Y + 28f);
                    return true;
                }

                //立足面：猩红锚物上方留足净空处凝云（净空越大凝得越高）
                int floorY = ScanSolid(tileX, playerTileY, 1);
                if (floorY >= 0 && IsCrimsonAnchorTile(Main.tile[tileX, floorY].TileType)) {
                    int free = CountClearance(tileX, floorY, -1);
                    if (free >= HeadroomMinTiles) {
                        int rise = Math.Min(free - 2, CondenseRiseCapTiles);
                        anchorFace = new Vector2(tileX * 16f + 8f, floorY * 16f);
                        core = new Vector2(anchorFace.X, (floorY - rise) * 16f + 8f);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>自玩家高度沿列向上（step=-1）/向下（step=1）找第一块实心瓦（限采样窗内），无则 -1</summary>
        private static int ScanSolid(int tileX, int fromTileY, int step) {
            for (int i = 1; i <= AnchorSampleRangeY; i++) {
                int tileY = fromTileY + step * i;
                if (!WorldGen.InWorld(tileX, tileY, 10)) {
                    return -1;
                }
                if (WorldGen.SolidTile(tileX, tileY)) {
                    return tileY;
                }
            }
            return -1;
        }

        /// <summary>自某实心瓦沿列数连续非实心净空（上限 <see cref="HeadroomScanTiles"/>）</summary>
        private static int CountClearance(int tileX, int fromTileY, int step) {
            int free = 0;
            for (int i = 1; i <= HeadroomScanTiles; i++) {
                int tileY = fromTileY + step * i;
                if (!WorldGen.InWorld(tileX, tileY, 10) || WorldGen.SolidTile(tileX, tileY)) {
                    break;
                }
                free++;
            }
            return free;
        }
    }
}
