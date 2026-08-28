using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics
{
    /// <summary>
    /// 地形装备：射线探面 + 圆周法线采样 + 垂扫找地。
    /// 全部只读物块数据，各端对同一坐标求得同一结果（确定性，联机安全）
    /// </summary>
    internal static class ShrimpTerrain
    {
        /// <summary>世界点是否落在实心固体内（斜坡/半砖按实心算，平台不算）</summary>
        public static bool SolidAt(Vector2 world) {
            int tx = (int)(world.X / 16f);
            int ty = (int)(world.Y / 16f);
            if (tx < 5 || tx > Main.maxTilesX - 5 || ty < 5 || ty > Main.maxTilesY - 5) {
                return false;
            }
            Tile tile = Framing.GetTileSafely(tx, ty);
            return tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        /// <summary>
        /// 沿方向射线找固体表面：8px 步进，命中后回退到边界。
        /// 返回是否命中；point 为表面点（最后一个空位与固体位的中点）
        /// </summary>
        public static bool RaycastSurface(Vector2 from, Vector2 dir, float maxDist, out Vector2 point) {
            const float Step = 8f;
            Vector2 prev = from;
            for (float d = Step; d <= maxDist; d += Step) {
                Vector2 p = from + dir * d;
                if (SolidAt(p)) {
                    //细化：二分一次贴近边界
                    Vector2 mid = (prev + p) * 0.5f;
                    point = SolidAt(mid) ? (prev + mid) * 0.5f : mid;
                    return true;
                }
                prev = p;
            }
            point = from + dir * maxDist;
            return false;
        }

        /// <summary>
        /// 圆周法线采样（Everglow Caterpillar 形状）：16 向探固体，
        /// 空侧加权、实侧减权，得到表面外法线。采不出返回 -dirHint
        /// </summary>
        public static Vector2 SampleNormal(Vector2 surfacePoint, Vector2 dirHint, float radius = 22f) {
            Vector2 normal = Vector2.Zero;
            for (int i = 0; i < 16; i++) {
                Vector2 offset = (MathHelper.TwoPi * i / 16f).ToRotationVector2() * radius;
                if (SolidAt(surfacePoint + offset)) {
                    normal -= offset;
                }
                else {
                    normal += offset;
                }
            }
            if (normal.LengthSquared() < 0.01f) {
                return -dirHint;
            }
            return Vector2.Normalize(normal);
        }

        /// <summary>向下逐格找地（EowMotionFX 形状）：返回首格实心顶面世界 Y，扫不到给兜底</summary>
        public static float FindGroundBelow(Vector2 from, float maxDepth = 1280f) {
            int tx = (int)(from.X / 16f);
            int startTy = Math.Max((int)(from.Y / 16f), 10);
            int maxTy = Math.Min((int)((from.Y + maxDepth) / 16f), Main.maxTilesY - 10);
            for (int y = startTy; y <= maxTy; y++) {
                Tile tile = Framing.GetTileSafely(tx, y);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    return y * 16f;
                }
            }
            return from.Y + maxDepth;
        }

        /// <summary>世界点是否浸水</summary>
        public static bool WetAt(Vector2 world)
            => Collision.WetCollision(world - new Vector2(8f, 8f), 16, 16);
    }
}
