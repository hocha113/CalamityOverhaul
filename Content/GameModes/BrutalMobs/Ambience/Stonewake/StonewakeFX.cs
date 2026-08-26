using CalamityOverhaul.Content.Items.Stones;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Stonewake
{
    /// <summary>
    /// 石醒双厅（花岗岩洞+大理石洞）共用风味表：
    /// 色板复用石头族 <see cref="GraniteMarbleVFX"/>（花岗蓝白/大理石金白），
    /// 伤害锚点、城镇安宁门、并发计数与锚点搜寻统一从这里取
    /// </summary>
    internal static class StonewakeFX
    {
        //==== 花岗岩厅色板（幽蓝科技感） ====
        public static Color GraniteCore => GraniteMarbleVFX.GraniteCore;
        public static Color GraniteDeep => GraniteMarbleVFX.GraniteDeep;
        public static Color GraniteSpark => GraniteMarbleVFX.GraniteSpark;

        //==== 大理石厅色板（庄严神话感） ====
        public static Color MarbleCore => GraniteMarbleVFX.MarbleCore;
        public static Color MarbleGold => GraniteMarbleVFX.MarbleGold;
        public static Color MarbleDust => GraniteMarbleVFX.MarbleDust;

        /// <summary>大理石原生怪接触伤害锚点（正常模式，Hoplite 60 / Medusa 45 取中）</summary>
        public const int MarbleContactAnchor = 55;

        /// <summary>城镇安宁半径（约 60 格）</summary>
        private const float TownCalmRadius = 960f;

        /// <summary>按世界难度缩放接触伤害锚点（镜像原版 npc.damage 的难度倍率）</summary>
        public static int ScaledContact(int baseContact) {
            if (Main.masterMode) {
                return baseContact * 3;
            }
            return Main.expertMode ? baseContact * 2 : baseContact;
        }

        /// <summary>某点约 60 格内有存活城镇 NPC 则为安宁区，伤害/减益机制不触发</summary>
        public static bool TownNpcNear(Vector2 pos) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(pos) < TownCalmRadius) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>统计某类弹幕的活动实例数（到 stopAt 提前退出；只在冷却尽头调用）</summary>
        public static int CountActive(int projType, int stopAt = 16) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>
        /// 共振脉冲锚点：在目标附近随机取样，向下寻花岗岩地表，返回晶簇生长点（顶面中心）。
        /// 找不到视为地形不合适，本轮放弃
        /// </summary>
        public static bool TryFindGraniteAnchor(Player target, out Vector2 anchor) {
            anchor = default;
            for (int attempt = 0; attempt < 12; attempt++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(180f, 560f);
                Vector2 sample = target.Center + ang.ToRotationVector2() * dist;
                Point tile = sample.ToTileCoordinates();
                if (!WorldGen.InWorld(tile.X, tile.Y, 10)) {
                    continue;
                }
                //取样点若在实体里则不合适（要的是洞腔内的地面）
                if (WorldGen.SolidTile(tile.X, tile.Y)) {
                    continue;
                }
                for (int dy = 1; dy <= 8; dy++) {
                    int tileY = tile.Y + dy;
                    if (!WorldGen.InWorld(tile.X, tileY, 10)) {
                        break;
                    }
                    if (!WorldGen.SolidTile(tile.X, tileY)) {
                        continue;
                    }
                    if (Main.tile[tile.X, tileY].TileType == TileID.Granite) {
                        anchor = new Vector2(tile.X * 16f + 8f, tileY * 16f);
                        return true;
                    }
                    break;//首个固体不是花岗岩，换点
                }
            }
            return false;
        }

        /// <summary>
        /// 凝视之柱锚点：从目标脚下向下寻大理石地表（平台会被穿过，落在真地面上）。
        /// 脚下不是大理石则放弃本轮
        /// </summary>
        public static bool TryFindMarbleFloor(Player target, out Vector2 basePos) {
            basePos = default;
            Point feet = target.Bottom.ToTileCoordinates();
            for (int dy = 0; dy < 12; dy++) {
                int tileY = feet.Y + dy;
                if (!WorldGen.InWorld(feet.X, tileY, 10)) {
                    return false;
                }
                if (!WorldGen.SolidTile(feet.X, tileY)) {
                    continue;
                }
                if (Main.tile[feet.X, tileY].TileType != TileID.Marble) {
                    return false;
                }
                basePos = new Vector2(feet.X * 16f + 8f, tileY * 16f);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 在屏幕内随机找一处裸露的指定石材面（固体且四邻有空气），供氛围粒子锚定。
        /// 纯客户端取样，找不到返回 false
        /// </summary>
        public static bool TryFindExposedTile(int tileType, out Vector2 facePos) {
            facePos = default;
            for (int attempt = 0; attempt < 6; attempt++) {
                Vector2 sample = Main.screenPosition + new Vector2(
                    Main.rand.NextFloat(Main.screenWidth), Main.rand.NextFloat(Main.screenHeight));
                Point tile = sample.ToTileCoordinates();
                if (!WorldGen.InWorld(tile.X, tile.Y, 10) || !WorldGen.SolidTile(tile.X, tile.Y)) {
                    continue;
                }
                if (Main.tile[tile.X, tile.Y].TileType != tileType) {
                    continue;
                }
                //找一张裸露面，粒子贴面而不是闷在岩体里
                if (!WorldGen.SolidTile(tile.X, tile.Y - 1)) {
                    facePos = new Vector2(tile.X * 16f + 8f, tile.Y * 16f - 2f);
                    return true;
                }
                if (!WorldGen.SolidTile(tile.X - 1, tile.Y)) {
                    facePos = new Vector2(tile.X * 16f - 2f, tile.Y * 16f + 8f);
                    return true;
                }
                if (!WorldGen.SolidTile(tile.X + 1, tile.Y)) {
                    facePos = new Vector2(tile.X * 16f + 18f, tile.Y * 16f + 8f);
                    return true;
                }
                if (!WorldGen.SolidTile(tile.X, tile.Y + 1)) {
                    facePos = new Vector2(tile.X * 16f + 8f, tile.Y * 16f + 18f);
                    return true;
                }
            }
            return false;
        }
    }
}
