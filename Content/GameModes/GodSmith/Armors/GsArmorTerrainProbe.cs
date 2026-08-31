using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors
{
    /// <summary>
    /// 盔甲天降/探地 proc 的族内共享几何工具（R2-A11 S1 几何返工包）。
    /// 三件套镜像轨内正范式：①天降出生点先向上探顶棚、被挡则收缩到顶棚下净空
    /// ②Stardust 式高度门（GsStardustFallProj：出生免地形碰撞，越过标的线才恢复）
    /// ③Fossil 式向下探实心地面。让天降与破土神赋在洞穴/低顶棚/隧道内照常落到目标
    /// </summary>
    internal static class GsArmorTerrainProbe
    {
        /// <summary>
        /// 取锚点上方的天降生成点：沿生成列向上探顶棚，想要的高度被顶棚挡住时
        /// 收缩到顶棚下方净空处（最低收到锚点高度）；侧向偏移列整个陷在洞壁里时，
        /// 收拢到锚点正上方重探保命
        /// </summary>
        internal static Vector2 SkySpawnAbove(Vector2 anchor, float xOffset, float desiredHeight, float ceilingGap = 24f) {
            Vector2 spawn = ProbeColumn(anchor, xOffset, desiredHeight, ceilingGap);
            if (xOffset != 0f && SolidAt(spawn)) {
                spawn = ProbeColumn(anchor, 0f, desiredHeight, ceilingGap);
            }
            return spawn;
        }

        /// <summary>单列向上探顶棚，返回该列夹紧后的生成点</summary>
        private static Vector2 ProbeColumn(Vector2 anchor, float xOffset, float desiredHeight, float ceilingGap) {
            float x = anchor.X + xOffset;
            int tileX = (int)(x / 16f);
            int startY = (int)(anchor.Y / 16f) - 1;
            int scanTiles = (int)(desiredHeight / 16f) + 1;
            float y = anchor.Y - desiredHeight;
            for (int dy = 0; dy <= scanTiles; dy++) {
                int tileY = startY - dy;
                if (!WorldGen.InWorld(tileX, tileY, 10)) {
                    break;
                }
                Tile t = Framing.GetTileSafely(tileX, tileY);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    //顶棚底边下方留净空；顶棚贴脸时最多收缩到锚点高度
                    y = Math.Min(Math.Max(y, (tileY + 1) * 16f + ceilingGap), anchor.Y);
                    break;
                }
            }
            return new Vector2(x, y);
        }

        /// <summary>该点所在砖是否实心（平台不算）</summary>
        private static bool SolidAt(Vector2 pos) {
            Point tile = pos.ToTileCoordinates();
            if (!WorldGen.InWorld(tile.X, tile.Y, 10)) {
                return false;
            }
            Tile t = Framing.GetTileSafely(tile.X, tile.Y);
            return t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType];
        }

        /// <summary>
        /// Stardust 式高度门：出生 tileCollide=false 的天降弹越过标的线
        /// （targetLineY - lead）后恢复地形碰撞；标的线为 0（未传参的裸生成）时立即恢复，
        /// 行为退回改造前
        /// </summary>
        internal static void UpdateFallGate(Projectile projectile, float targetLineY, float lead = 60f) {
            if (!projectile.tileCollide && projectile.Center.Y > targetLineY - lead) {
                projectile.tileCollide = true;
            }
        }

        /// <summary>
        /// 自起点向下最多 maxTiles 格找实心地面（Fossil 式，起扫上提两格容忍目标微陷地）；
        /// 找到返回 true 并给出地表世界 Y（砖上边缘），找不到 groundY 保持起点 Y（原空中落位）
        /// </summary>
        internal static bool TryFindGroundBelow(Vector2 from, int maxTiles, out float groundY) {
            Point tile = from.ToTileCoordinates();
            for (int dy = -2; dy < maxTiles; dy++) {
                int tileY = tile.Y + dy;
                if (!WorldGen.InWorld(tile.X, tileY, 10)) {
                    break;
                }
                Tile t = Framing.GetTileSafely(tile.X, tileY);
                if (t.HasTile && Main.tileSolid[t.TileType]) {
                    groundY = tileY * 16f;
                    return true;
                }
            }
            groundY = from.Y;
            return false;
        }
    }
}
