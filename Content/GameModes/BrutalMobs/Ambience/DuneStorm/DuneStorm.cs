using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.DuneStorm
{
    /// <summary>
    /// 残酷模式地表沙漠环境层的共用判定与参数中枢。
    /// 三个具名特色：扬沙（常态氛围）、风堑（阵风推挤）、沙暴强化（含沙鞭），另有正午灼热（可选第四项）。
    /// 一切由天气与地形驱动，不挂任何 NPC；档位只调频率/密度/推力，不换机制形状
    /// </summary>
    internal static class DuneStorm
    {
        //==== 风堑（阵风推挤） ====
        /// <summary>风堑调度间隔（帧），档位只调频率</summary>
        internal static readonly int[] GustIntervalByTier = [1500, 1260, 1020];
        /// <summary>阵风推力（每帧加速度），档位只调推力，空中减半</summary>
        internal static readonly float[] GustAccelByTier = [0.20f, 0.24f, 0.28f];
        /// <summary>阵风携带速度上限（超过则不再加力，温和不致命）</summary>
        internal static readonly float[] GustCarryCapByTier = [4.6f, 5.2f, 5.8f];
        /// <summary>风堑波全局并发上限</summary>
        internal const int GustCap = 3;

        //==== 沙暴强化（沙鞭） ====
        /// <summary>沙鞭调度间隔（帧），仅沙暴事件期间生效</summary>
        internal static readonly int[] LashIntervalByTier = [900, 760, 620];
        /// <summary>沙鞭预告体全局并发上限</summary>
        internal const int LashOmenCap = 2;
        /// <summary>沙鞭弹幕全局并发上限</summary>
        internal const int LashCap = 3;
        /// <summary>沙鞭伤害 = 沙漠原版敌怪接触伤害 × 此值（公平条款 0.4~0.5 档）</summary>
        internal const float LashDamageFrac = 0.45f;

        //==== 正午灼热 ====
        /// <summary>热浪累积速率（每帧），档位只调速率；满值 100</summary>
        internal static readonly float[] HeatRateByTier = [0.24f, 0.29f, 0.34f];

        //==== 通用 ====
        /// <summary>城镇安宁半径（60 格）</summary>
        private const float TownCalmRadius = 960f;
        /// <summary>向下寻地的最大瓦格数</summary>
        internal const int GroundSearchTiles = 40;
        /// <summary>头顶通天检查的最大瓦格数</summary>
        private const int SkySearchTiles = 60;

        //==== 沙漠色板（与 Wastes 沙喷家族同源） ====
        /// <summary>暗沙底色（实体层）</summary>
        internal static readonly Color SandDeep = new(140, 108, 62);
        /// <summary>亮沙色（A=0 加色敷料）</summary>
        internal static readonly Color SandBright = new(232, 202, 126);
        /// <summary>警示暖光</summary>
        internal static readonly Color WarnGlow = new(255, 200, 110);

        /// <summary>
        /// 槽位检测契约：ZoneDesert 且地表高度且非 ZoneUndergroundDesert。
        /// 地下沙漠归 Sunkendune 槽，高度判定必须干净
        /// </summary>
        internal static bool InSurfaceDesert(Player player)
            => player.ZoneDesert && player.ZoneOverworldHeight && !player.ZoneUndergroundDesert;

        /// <summary>挑战机制总闸：残酷模式开启且 Boss 不在场（Boss 战期间伤害/减益/位移一律暂停）</summary>
        internal static bool MechanicsAllowed => GameModeSystem.BrutalActive && !CWRWorld.HasBoss;

        /// <summary>城镇安宁：位置约 60 格内有存活城镇 NPC 时伤害性机制不触发（氛围保留）</summary>
        internal static bool TownCalm(Vector2 pos) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(pos) < TownCalmRadius) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>沙系地块（含硬化沙与沙岩，吃转换表以覆盖邪化/神圣变体）</summary>
        internal static bool IsSandFamily(int tileType)
            => TileID.Sets.Conversion.Sand[tileType]
            || TileID.Sets.Conversion.HardenedSand[tileType]
            || TileID.Sets.Conversion.Sandstone[tileType];

        /// <summary>
        /// 沙鞭伤害基准：取沙漠原版敌怪接触伤害（硬模式食尸鬼/前期蚁狮兵模板伤）
        /// 乘以当前难度的敌怪伤害系数，再乘 <see cref="LashDamageFrac"/>。
        /// 刻意不吃档位与残酷增幅，锚定原版接触伤害（Brief 公平性条款）
        /// </summary>
        internal static int LashDamage() {
            int repType = Main.hardMode ? NPCID.DesertGhoul : NPCID.WalkingAntlion;
            int baseContact = 40;
            if (ContentSamples.NpcsByNetId.TryGetValue(repType, out NPC sample)) {
                baseContact = Math.Max(sample.damage, 10);
            }
            float diffMul = Main.GameModeInfo.EnemyDamageMultiplier;
            return Math.Max(1, (int)(baseContact * diffMul * LashDamageFrac));
        }

        /// <summary>统计某类弹幕的活动实例数（到 stopAt 提前退出；只在冷却尽头调用，非每帧）</summary>
        internal static int CountActive(int projType, int stopAt = 16) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>
        /// 从指定瓦坐标向下找可站立地表，返回地表锚点（像素）。
        /// 用于风堑沙线与沙鞭预告的贴地锚定，找不到视为悬空放弃
        /// </summary>
        internal static bool TryFindGround(int tileX, int startTileY, out Vector2 basePos) {
            basePos = default;
            for (int dy = 0; dy < GroundSearchTiles; dy++) {
                int tileY = startTileY + dy;
                if (!WorldGen.InWorld(tileX, tileY, 10)) {
                    return false;
                }
                if (WorldGen.SolidTile(tileX, tileY)) {
                    basePos = new Vector2(tileX * 16f + 8f, tileY * 16f);
                    return true;
                }
            }
            return false;
        }

        /// <summary>玩家是否暴露在天空下（任一身位列头顶通天即算暴露；有顶棚遮蔽则免疫风推）</summary>
        internal static bool ExposedToSky(Player player) {
            int left = (int)(player.position.X / 16f);
            int right = (int)((player.position.X + player.width) / 16f);
            int top = (int)(player.position.Y / 16f);
            for (int x = left; x <= right; x++) {
                bool blocked = false;
                int floor = Math.Max(top - SkySearchTiles, 10);
                for (int y = top - 1; y >= floor; y--) {
                    if (WorldGen.SolidTile(x, y)) {
                        blocked = true;
                        break;
                    }
                }
                if (!blocked) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>归一化风强 0~1（原版 windSpeedCurrent 量程约 ±0.8）</summary>
        internal static float WindStrength01()
            => MathHelper.Clamp(Math.Abs(Main.windSpeedCurrent) / 0.8f, 0f, 1f);

        /// <summary>风向符号（近无风时返回 0，由调用方决定兜底）</summary>
        internal static float WindDir() {
            if (Math.Abs(Main.windSpeedCurrent) < 0.05f) {
                return 0f;
            }
            return Math.Sign(Main.windSpeedCurrent);
        }
    }
}
