using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rimehollow.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rimehollow
{
    /// <summary>
    /// 冰雪洞穴权威端导演：低频扫描在场玩家周边，
    /// 为「冰锥垂生」找洞顶冰面锚点、为「寒雾洼」找低洼滞留位。
    /// 决策与生成只在权威端跑，客户端通过同步弹幕实体看到状态
    /// </summary>
    internal class RimehollowDirector : ModSystem
    {
        //==== 冰锥垂生 ====
        private const int IcicleScanFrames = 50;
        /// <summary>冰锥全局并发上限</summary>
        private const int IcicleCap = 10;
        /// <summary>候选锚点搜索半宽（瓦格）</summary>
        private const int IcicleSearchHalfW = 44;
        private const int IcicleSearchHalfH = 26;
        /// <summary>同类冰锥最小间距（像素）</summary>
        private const float IcicleSpacing = 96f;
        /// <summary>
        /// 坠落伤害：镜像 DamageFrac 惯例，取冰雪层小怪接触伤害（冰蝙蝠 30/冰壳武士 70）
        /// 的约 0.5 倍，"微量伤害"
        /// </summary>
        private const int IcicleDamageEarly = 16;
        private const int IcicleDamageHard = 34;

        //==== 寒雾洼 ====
        private const int MistScanFrames = 90;
        /// <summary>寒雾洼全局并发上限</summary>
        private const int MistCap = 3;
        private const int MistSearchHalfW = 34;
        /// <summary>两片寒雾的最小间距（像素）</summary>
        private const float MistSpacing = 620f;

        private int icicleScanIn = IcicleScanFrames;
        private int mistScanIn = MistScanFrames;

        public override void PostUpdateEverything() {
            //决策只在权威端（单人/服务器）
            if (VaultUtils.isClient || !GameModeSystem.BrutalActive) {
                return;
            }
            if (--icicleScanIn <= 0) {
                icicleScanIn = IcicleScanFrames;
                //Boss 在场暂停新威胁的孵化，已有冰锥保留为纯视觉
                if (!CWRWorld.HasBoss) {
                    TrySpawnIcicles();
                }
            }
            if (--mistScanIn <= 0) {
                mistScanIn = MistScanFrames;
                TrySpawnMist();
            }
        }

        public override void ClearWorld() {
            icicleScanIn = IcicleScanFrames;
            mistScanIn = MistScanFrames;
        }

        /// <summary>统计某类弹幕的活动实例数（到 stopAt 提前退出；只在扫描节拍调用）</summary>
        private static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>指定点附近是否已有同类弹幕</summary>
        private static bool AnyProjNear(int projType, Vector2 pos, float range) {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && proj.Distance(pos) < range) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>「冰锥垂生」孵化：洞顶冰系瓦片下、有下坠空间处，低频挂一枚生长中的冰锥</summary>
        private void TrySpawnIcicles() {
            int icicleType = ModContent.ProjectileType<RimehollowIcicleProj>();
            if (CountActive(icicleType) >= IcicleCap) {
                return;
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            int damage = Main.hardMode ? IcicleDamageHard : IcicleDamageEarly;

            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !RimehollowAmbience.In(player)) {
                    continue;
                }
                //每次扫描每位玩家至多孵化一枚，且四成概率直接歇拍，生成保持低频
                if (Main.rand.NextBool(2, 5)) {
                    continue;
                }
                Point center = player.Center.ToTileCoordinates();
                for (int attempt = 0; attempt < 6; attempt++) {
                    int tx = center.X + Main.rand.Next(-IcicleSearchHalfW, IcicleSearchHalfW + 1);
                    int ty = center.Y + Main.rand.Next(-IcicleSearchHalfH, IcicleSearchHalfH + 1);
                    if (!TryFindIcicleAnchor(tx, ty, out Vector2 anchor)) {
                        continue;
                    }
                    if (AnyProjNear(icicleType, anchor, IcicleSpacing)) {
                        continue;
                    }
                    if (RimehollowAmbience.TownCalmNear(anchor)) {
                        continue;
                    }
                    //ai[0]=相位 ai[1]=相位计时 ai[2]=档位*10+体型
                    int variant = Main.rand.Next(3);
                    Projectile.NewProjectile(new EntitySource_Misc("RimehollowIcicle"),
                        anchor, Vector2.Zero, icicleType, damage, 2f, Main.myPlayer,
                        0f, 0f, tier * 10 + variant);
                    break;
                }
            }
        }

        /// <summary>
        /// 校验冰锥锚点：(tx,ty) 是空气、正上方是冰系实心瓦片、
        /// 下方 ≥5 格净空且无液体（坠落要有意义）
        /// </summary>
        private static bool TryFindIcicleAnchor(int tx, int ty, out Vector2 anchor) {
            anchor = default;
            if (!WorldGen.InWorld(tx, ty, 24)) {
                return false;
            }
            Tile air = Framing.GetTileSafely(tx, ty);
            if (air.HasTile || air.LiquidAmount > 0) {
                return false;
            }
            Tile ceiling = Framing.GetTileSafely(tx, ty - 1);
            if (!ceiling.HasTile || !WorldGen.SolidTile(tx, ty - 1)
                || !RimehollowAmbience.IsIcicleAnchor(ceiling.TileType)) {
                return false;
            }
            for (int dy = 1; dy <= 5; dy++) {
                if (WorldGen.SolidTile(tx, ty + dy) || Framing.GetTileSafely(tx, ty + dy).LiquidAmount > 0) {
                    return false;
                }
            }
            //锚点：洞顶瓦片下缘中点
            anchor = new Vector2(tx * 16f + 8f, ty * 16f + 2f);
            return true;
        }

        /// <summary>「寒雾洼」孵化：两侧地势更高的低洼地面上滞留一片可见寒雾带</summary>
        private void TrySpawnMist() {
            int mistType = ModContent.ProjectileType<RimehollowMistPoolProj>();
            if (CountActive(mistType) >= MistCap) {
                return;
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }

            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !RimehollowAmbience.In(player)) {
                    continue;
                }
                Point center = player.Center.ToTileCoordinates();
                for (int attempt = 0; attempt < 4; attempt++) {
                    int tx = center.X + Main.rand.Next(-MistSearchHalfW, MistSearchHalfW + 1);
                    if (!TryFindMistBasin(tx, center.Y, out Vector2 pos, out float halfWidth)) {
                        continue;
                    }
                    if (AnyProjNear(mistType, pos, MistSpacing)) {
                        continue;
                    }
                    //ai[0]=半宽 ai[1]=存续帧 ai[2]=档位
                    Projectile.NewProjectile(new EntitySource_Misc("RimehollowMist"),
                        pos, Vector2.Zero, mistType, 0, 0f, Main.myPlayer,
                        halfWidth, 1500 + Main.rand.Next(600), tier);
                    return;//全局每拍至多一片，寒雾保持稀疏
                }
            }
        }

        /// <summary>
        /// 低洼检测：从候选列向下找地面，再确认左右 2~8 格内两侧
        /// 都存在高出 ≥2 格的地势（洼地才滞雾）
        /// </summary>
        private static bool TryFindMistBasin(int tx, int startTy, out Vector2 pos, out float halfWidth) {
            pos = default;
            halfWidth = 0f;
            if (!WorldGen.InWorld(tx, startTy, 24)) {
                return false;
            }
            int gy = FindFloor(tx, startTy - 2, 24);
            if (gy < 0) {
                return false;
            }
            //洼底得是敞开的空气（头顶两格净空、无液体）
            Tile above = Framing.GetTileSafely(tx, gy - 1);
            Tile above2 = Framing.GetTileSafely(tx, gy - 2);
            if (above.HasTile || above.LiquidAmount > 0 || above2.HasTile) {
                return false;
            }

            int wallLeft = FindBasinWall(tx, gy, -1);
            int wallRight = FindBasinWall(tx, gy, 1);
            if (wallLeft < 0 || wallRight < 0) {
                return false;
            }
            int halfTiles = System.Math.Min(wallLeft, wallRight);
            halfWidth = MathHelper.Clamp(halfTiles * 16f, 84f, 190f);
            //雾带中心悬在洼底上方一段
            pos = new Vector2(tx * 16f + 8f, gy * 16f - 24f);
            return true;
        }

        /// <summary>从 startTy 向下找第一块实心地面，返回瓦格 Y（找不到 -1）</summary>
        private static int FindFloor(int tx, int startTy, int maxDepth) {
            for (int dy = 0; dy < maxDepth; dy++) {
                int ty = startTy + dy;
                if (!WorldGen.InWorld(tx, ty, 24)) {
                    return -1;
                }
                if (WorldGen.SolidTile(tx, ty)) {
                    return ty;
                }
            }
            return -1;
        }

        /// <summary>沿 dir 侧找"高出洼底 ≥2 格"的地势，返回距离（瓦格，找不到 -1）</summary>
        private static int FindBasinWall(int tx, int basinY, int dir) {
            for (int k = 2; k <= 8; k++) {
                int cx = tx + dir * k;
                int floor = FindFloor(cx, basinY - 8, 14);
                if (floor < 0) {
                    continue;
                }
                if (floor <= basinY - 2) {
                    return k;
                }
            }
            return -1;
        }
    }
}
