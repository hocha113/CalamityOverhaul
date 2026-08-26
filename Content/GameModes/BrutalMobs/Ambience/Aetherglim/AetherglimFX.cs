using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Aetherglim
{
    /// <summary>
    /// 微光地带环境特色的共用风味表：珠光虹彩色板、加色批染色工具、
    /// 微光湖面探测与公平性门（Boss 在场/城镇安宁）统一从这里取，
    /// 保证珠光、引力泡、深引、相位闪四个特色的视觉语言一致。
    /// 表现语言是"重力/相位"而非"水流"：这里的一切扰动都写成重力方向的异常
    /// </summary>
    internal static class AetherglimFX
    {
        /// <summary>城镇安宁半径（60 格）</summary>
        private const float TownCalmRangePx = 960f;

        /// <summary>
        /// 珠光虹彩：薄膜干涉式的柔和相位色，t 任意相位输入。
        /// 色相锁在青-紫-粉的微光带上，避免撞上纯彩虹（那是光女的语言）
        /// </summary>
        public static Color Iridescent(float t) {
            float hue = 0.78f + 0.24f * MathF.Sin(t);
            hue %= 1f;
            if (hue < 0f) {
                hue += 1f;
            }
            return Main.hslToRgb(hue, 0.72f, 0.70f);
        }

        /// <summary>虹彩暗底（真 alpha 层用，比亮色沉两档）</summary>
        public static Color IridescentDeep(float t) {
            float hue = 0.78f + 0.24f * MathF.Sin(t);
            hue %= 1f;
            if (hue < 0f) {
                hue += 1f;
            }
            return Main.hslToRgb(hue, 0.55f, 0.34f);
        }

        /// <summary>
        /// 加色批染色：InnoVault AdditiveBlend 批源因子是 SourceAlpha，
        /// A=0 会让整张消失；包络必须写进 A，rgb 保持本色（镜像 EmpressPRTDraw 教训）
        /// </summary>
        public static Color Tint(Color rgb, float envelope)
            => rgb with { A = (byte)(255f * MathHelper.Clamp(envelope, 0f, 1f)) };

        /// <summary>玩家附近有存活城镇 NPC（城镇安宁：扰动机制不触发，氛围可留）</summary>
        public static bool NearTownNPC(Vector2 pos) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(pos) < TownCalmRangePx) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>扰动机制总门：残酷模式开启、无 Boss、城镇安宁之外</summary>
        public static bool MechanicsAllowed(Vector2 pos)
            => GameModeSystem.EffectiveTier > 0 && !CWRWorld.HasBoss && !NearTownNPC(pos);

        /// <summary>统计某类弹幕的活动实例数（镜像 Wastes 的 Cap 检查，只在冷却尽头调用）</summary>
        public static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>
        /// 在指定瓦格列向下找微光液面（微光液体且上方无液体无实体块），
        /// 返回液面世界坐标（像素，取格顶中心）。找不到返回 false
        /// </summary>
        public static bool TryFindShimmerSurface(int tileX, int topY, int bottomY, out Vector2 surface) {
            surface = default;
            if (!WorldGen.InWorld(tileX, topY, 10) || !WorldGen.InWorld(tileX, bottomY, 10)) {
                return false;
            }
            for (int y = topY; y <= bottomY; y++) {
                Tile tile = Main.tile[tileX, y];
                if (tile.LiquidAmount < 120 || tile.LiquidType != LiquidID.Shimmer) {
                    continue;
                }
                Tile above = Main.tile[tileX, y - 1];
                bool aboveOpen = above.LiquidAmount == 0
                    && !(above.HasTile && Main.tileSolid[above.TileType] && !Main.tileSolidTop[above.TileType]);
                if (aboveOpen) {
                    surface = new Vector2(tileX * 16f + 8f, y * 16f + (255 - tile.LiquidAmount) / 255f * 16f);
                    return true;
                }
            }
            return false;
        }
    }
}
