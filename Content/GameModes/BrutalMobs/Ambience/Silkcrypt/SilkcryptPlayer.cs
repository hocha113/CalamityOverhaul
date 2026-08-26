using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Silkcrypt.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Silkcrypt
{
    /// <summary>
    /// 蛛巢逐玩家状态：低频墙体采样（蛛巢无 Zone 旗标，扫周边 SpiderUnsafe 墙占比，
    /// 滞回防边界抖动）+ 权威端的黏络网斑/垂袭蛛影调度。
    /// 采样服务端对全员跑（生成决策要用），联机客户端只跑本机玩家（省预算）；
    /// 所有逐玩家状态都在实例字段上，禁 static
    /// </summary>
    internal class SilkcryptPlayer : ModPlayer
    {
        //==== 检测参数（低频 + 滞回）====
        /// <summary>采样间隔（帧），入场错拍防全员同帧扫描</summary>
        private const int SampleGap = 30;
        /// <summary>采样网格步长（瓦格），稀疏采样压成本</summary>
        private const int SampleStep = 3;
        private const int SampleHalfX = 24;
        private const int SampleHalfY = 16;
        /// <summary>进入阈值：采样墙体中蛛巢墙占比</summary>
        private const float EnterFrac = 0.12f;
        /// <summary>退出阈值（滞回下沿）</summary>
        private const float ExitFrac = 0.05f;

        //==== 黏络网斑（档位只调频率，形状不变）====
        private static readonly int[] PatchCooldownByTier = [1080, 900, 720];
        /// <summary>网斑全局并发上限</summary>
        private const int PatchCap = 4;
        /// <summary>网斑间最小间距（像素）</summary>
        private const float PatchSpacingPx = 180f;
        /// <summary>网斑生成距玩家的最近/最远距离（瓦格）</summary>
        private const int PatchMinDistTiles = 12;
        private const int PatchMaxDistTiles = 38;
        /// <summary>踩中后同一玩家的再触发保护（帧）</summary>
        internal const int WebGraceFrames = 300;

        //==== 垂袭蛛影（档位只调频率）====
        private static readonly int[] ShadowCooldownByTier = [1680, 1380, 1080];
        /// <summary>垂影全局并发上限</summary>
        private const int ShadowCap = 2;
        /// <summary>垂影伤害 = 蛛巢原版敌怪接触伤害 × 此值（擦伤级）</summary>
        private const float ShadowDamageFrac = 0.5f;
        /// <summary>锚点上探/下探的最大瓦格数</summary>
        private const int CeilSearchTiles = 36;
        private const int DropMaxTiles = 24;
        private const int DropMinTiles = 7;

        /// <summary>触发条件不满足时的复查间隔基准</summary>
        private const int RetryFrames = 100;

        /// <summary>本玩家当前在蛛巢内（滞回后的稳定判定）</summary>
        internal bool InNest { get; private set; }
        /// <summary>最近一次采样的蛛巢墙占比 0~1（氛围密度参考）</summary>
        internal float WallFrac { get; private set; }
        /// <summary>被网斑黏住后的再触发保护计时</summary>
        internal int WebGraceTicks;

        //初值给一段入场缓冲：服务端不跑 OnEnterWorld，靠内联初值 + 首轮随机冷却错拍
        private int sampleTimer = 1;
        private int patchTimer = 420;
        private int shadowTimer = 780;

        /// <summary>总开关 + 地下高度粗筛（蛛巢只生成于地下，先挡掉地表零成本）</summary>
        internal static bool ZoneEligible(Player player)
            => GameModeSystem.BrutalActive
            && player.Center.Y > (float)(Main.worldSurface * 16.0);

        /// <summary>蛛巢原版敌怪接触伤害基数：爬墙蛛 30 / 黑隐士 90（经典模式）</summary>
        internal static int ContactBase() => Main.hardMode ? 90 : 30;

        public override void PostUpdateMiscEffects() {
            if (WebGraceTicks > 0) {
                WebGraceTicks--;
            }

            //联机客户端只维护本机玩家的检测（远端玩家的氛围与决策都不在本端）
            if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI != Main.myPlayer) {
                return;
            }

            if (--sampleTimer <= 0) {
                sampleTimer = SampleGap;
                SampleNest();
            }

            //生成决策只在权威端
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (!InNest || Player.dead) {
                return;
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }

            if (--patchTimer <= 0) {
                patchTimer = TryPlacePatch()
                    ? PatchCooldownByTier[tier - 1] + Main.rand.Next(-180, 181)
                    : RetryFrames + Main.rand.Next(60);
            }
            if (--shadowTimer <= 0) {
                shadowTimer = TryDropShadow()
                    ? ShadowCooldownByTier[tier - 1] + Main.rand.Next(-240, 241)
                    : RetryFrames + Main.rand.Next(60);
            }
        }

        public override void UpdateDead() {
            if (WebGraceTicks > 0) {
                WebGraceTicks--;
            }
        }

        public override void OnEnterWorld() {
            //错拍：避免多人同帧齐扫/齐触发
            sampleTimer = 1 + Player.whoAmI * 7 % SampleGap;
            patchTimer = 300 + Player.whoAmI * 31 % 240;
            shadowTimer = 600 + Player.whoAmI * 47 % 300;
        }

        //==================== 检测：稀疏墙体采样 + 滞回 ====================

        private void SampleNest() {
            if (!ZoneEligible(Player)) {
                InNest = false;
                WallFrac = 0f;
                return;
            }
            Point center = Player.Center.ToTileCoordinates();
            int total = 0;
            int hits = 0;
            for (int dx = -SampleHalfX; dx <= SampleHalfX; dx += SampleStep) {
                for (int dy = -SampleHalfY; dy <= SampleHalfY; dy += SampleStep) {
                    int x = center.X + dx;
                    int y = center.Y + dy;
                    if (!WorldGen.InWorld(x, y, 10)) {
                        continue;
                    }
                    total++;
                    if (Main.tile[x, y].WallType == WallID.SpiderUnsafe) {
                        hits++;
                    }
                }
            }
            WallFrac = total > 0 ? hits / (float)total : 0f;
            //滞回：进 0.12 / 出 0.05，边界走动不抖
            if (InNest) {
                if (WallFrac < ExitFrac) {
                    InNest = false;
                }
            }
            else if (WallFrac >= EnterFrac) {
                InNest = true;
            }
        }

        //==================== 公平性共用门 ====================

        /// <summary>伤害/减益机制的统一放行门：Boss 在场暂停、城镇安宁</summary>
        private bool HarmAllowed()
            => !CWRWorld.HasBoss && !SilkcryptAmbience.NearTown(Player.Center);

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

        //==================== 黏络：新鲜网斑铺设 ====================

        /// <summary>
        /// 在通道口/角落低频铺一块新鲜网斑：空气格 + 蛛巢墙 + 至少一侧贴实体瓦，
        /// 距玩家 12 瓦以上（必然可绕开），间距与并发都有上限
        /// </summary>
        private bool TryPlacePatch() {
            if (!HarmAllowed()) {
                return false;
            }
            int patchType = ModContent.ProjectileType<SilkcryptWebPatchProj>();
            if (CountActive(patchType) >= PatchCap) {
                return false;
            }

            for (int attempt = 0; attempt < 14; attempt++) {
                double angle = Main.rand.NextDouble() * MathHelper.TwoPi;
                float dist = Main.rand.Next(PatchMinDistTiles, PatchMaxDistTiles + 1);
                Point center = Player.Center.ToTileCoordinates();
                int x = center.X + (int)(Math.Cos(angle) * dist);
                int y = center.Y + (int)(Math.Sin(angle) * dist * 0.7f);
                if (!WorldGen.InWorld(x, y, 10) || WorldGen.SolidTile(x, y)) {
                    continue;
                }
                if (Main.tile[x, y].WallType != WallID.SpiderUnsafe) {
                    continue;//只长在蛛巢墙前，离开群系自然绝迹
                }

                //找贴附面：下/上/左/右第一处实体瓦决定网斑姿态
                int side = -1;
                if (WorldGen.SolidTile(x, y + 1)) {
                    side = 0;
                }
                else if (WorldGen.SolidTile(x, y - 1)) {
                    side = 1;
                }
                else if (WorldGen.SolidTile(x - 1, y)) {
                    side = 2;
                }
                else if (WorldGen.SolidTile(x + 1, y)) {
                    side = 3;
                }
                if (side < 0) {
                    continue;//悬空气格不是角落
                }

                Vector2 pos = new(x * 16f + 8f, y * 16f + 8f);
                if (TooCloseToPatch(pos, patchType)) {
                    continue;
                }

                Projectile.NewProjectile(Player.GetSource_Misc("CWR_SilkcryptWeb"), pos, Vector2.Zero,
                    patchType, 0, 0f, Main.myPlayer, side, Main.rand.Next(1000));
                return true;
            }
            return false;
        }

        private static bool TooCloseToPatch(Vector2 pos, int patchType) {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == patchType
                    && Vector2.DistanceSquared(proj.Center, pos) < PatchSpacingPx * PatchSpacingPx) {
                    return true;
                }
            }
            return false;
        }

        //==================== 垂袭影：头顶垂降黑影 ====================

        /// <summary>
        /// 从玩家头顶附近的天花板垂一道蛛影：先找顶（≤36 瓦），要求锚点贴蛛巢墙、
        /// 下方净空 ≥7 瓦；预告-速降-收回全由弹幕实体自走
        /// </summary>
        private bool TryDropShadow() {
            if (!HarmAllowed()) {
                return false;
            }
            int shadowType = ModContent.ProjectileType<SilkcryptDropShadowProj>();
            if (CountActive(shadowType) >= ShadowCap) {
                return false;
            }

            Point head = Player.Top.ToTileCoordinates();
            for (int attempt = 0; attempt < 6; attempt++) {
                int x = head.X + Main.rand.Next(-6, 7);
                //向上找第一块天花板
                int anchorY = -1;
                for (int dy = 2; dy <= CeilSearchTiles; dy++) {
                    int y = head.Y - dy;
                    if (!WorldGen.InWorld(x, y, 10)) {
                        break;
                    }
                    if (WorldGen.SolidTile(x, y)) {
                        anchorY = y;
                        break;
                    }
                }
                if (anchorY < 0) {
                    continue;
                }
                //锚口贴蛛巢墙（垂影不出蛛巢办事）
                if (Main.tile[x, anchorY + 1].WallType != WallID.SpiderUnsafe) {
                    continue;
                }
                //向下量净空，决定垂降行程
                int dropTiles = 0;
                for (int dy = 1; dy <= DropMaxTiles; dy++) {
                    int y = anchorY + dy;
                    if (!WorldGen.InWorld(x, y, 10) || WorldGen.SolidTile(x, y)) {
                        break;
                    }
                    dropTiles++;
                }
                if (dropTiles < DropMinTiles) {
                    continue;
                }

                //敌对弹幕命中玩家时原版自带 ×2（难度再放大），预除一半让
                //实际擦伤 ≈ 接触伤害 × ShadowDamageFrac，随难度自动跟走
                int damage = (int)(ContactBase() * ShadowDamageFrac * 0.5f);
                Vector2 anchor = new(x * 16f + 8f, (anchorY + 1) * 16f);
                Projectile.NewProjectile(Player.GetSource_Misc("CWR_SilkcryptShade"), anchor, Vector2.Zero,
                    shadowType, damage, 1.2f, Main.myPlayer, dropTiles * 16f - 8f, Main.rand.Next(1000));
                return true;
            }
            return false;
        }
    }
}
