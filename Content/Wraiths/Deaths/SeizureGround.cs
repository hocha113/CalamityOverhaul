using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Deaths
{
    /// <summary>
    /// 夺身演出的地面锚，开演时探一次。<br/>
    /// 破土、沉地、落地一类的演出必须据此定位，不能再拿「玩家中心 + 固定偏移」当地面：
    /// 空中被夺身时那样会让手从空气里长出来。探不到地面时 <see cref="HasGround"/> 为假，
    /// 演出应走自己的空中答案而不是硬画。
    /// </summary>
    internal readonly struct SeizureGround
    {
        /// <summary>向下探测上限，约 16 格</summary>
        private const float ProbeDepth = 260f;

        /// <summary>地表世界 Y；无地面时为 <see cref="float.NaN"/></summary>
        internal float GroundY { get; }

        internal bool HasGround { get; }

        private SeizureGround(float groundY, bool hasGround) {
            GroundY = groundY;
            HasGround = hasGround;
        }

        internal static SeizureGround Sample(Vector2 from) {
            int tileX = (int)(from.X / 16f);
            int startY = (int)(from.Y / 16f);
            int endY = (int)((from.Y + ProbeDepth) / 16f);
            for (int tileY = startY; tileY <= endY; tileY++) {
                Tile tile = Framing.GetTileSafely(tileX, tileY);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]
                    && !Main.tileSolidTop[tile.TileType]) {
                    return new SeizureGround(tileY * 16f, true);
                }
            }
            return new SeizureGround(float.NaN, false);
        }

        /// <summary>脚下落点：有地贴地，无地退回身下一段固定距离（供空中变体自己判断要不要用）。</summary>
        internal Vector2 FootAnchor(Vector2 center, float airborneDrop = 44f)
            => new(center.X, HasGround ? GroundY : center.Y + airborneDrop);
    }
}
