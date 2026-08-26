using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Ashreign
{
    /// <summary>
    /// 残酷模式地狱环境层的共用判定与参数中枢。
    /// 三个具名特色：烬雪（常态氛围：黑灰絮片缓落+火星上升+热浪扭曲+岩浆底噪）、
    /// 熔泡爆（岩浆池液面鼓泡预告→爆裂溅珠→烟柱余韵）、
    /// 烬暴（远处红霾墙逼近预告→过境烬幕+轻推+持续灼伤→散去），
    /// 另有恶魔号角（可选第四项，纯氛围）。
    /// 一切由岩浆与空气驱动，不挂任何 NPC；喷发源头必须是岩浆池液面
    /// （崖壁喷焰归 Cindercrag 槽，小鬼火弹归 JungleHell 包，勿越界）。
    /// 档位只调熔泡与烬暴频率，机制形状不变
    /// </summary>
    internal static class Ashreign
    {
        //==== 熔泡爆 ====
        /// <summary>逐玩家熔泡调度间隔（帧），档位只调频率</summary>
        internal static readonly int[] BubbleIntervalByTier = [540, 450, 370];
        /// <summary>熔泡全局并发上限</summary>
        internal const int BubbleCap = 6;
        /// <summary>岩浆珠伤害 = 地狱原版敌怪接触伤害 × 此值（公平条款 0.4~0.7 档）</summary>
        internal const float BeadDamageFrac = 0.45f;
        /// <summary>一次喷发的岩浆珠数（档位不变）</summary>
        internal const int BeadCount = 4;
        /// <summary>熔泡与目标玩家的最小距离（像素），泡不会顶着人脸起</summary>
        internal const float BubbleMinDist = 180f;
        /// <summary>熔泡采样的最大水平距离（瓦格）</summary>
        internal const int BubbleSampleTiles = 46;

        //==== 烬暴 ====
        /// <summary>逐玩家烬暴调度间隔（帧），档位只调频率</summary>
        internal static readonly int[] StormIntervalByTier = [6600, 5400, 4200];
        /// <summary>烬暴全局并发上限</summary>
        internal const int StormCap = 2;
        /// <summary>烬暴生成时距目标的水平提前量（像素），除以风速即为远霾逼近的预告时长</summary>
        internal const float StormSpawnLead = 1500f;
        /// <summary>烬暴推进速度（像素/帧）</summary>
        internal const float StormSpeed = 4.4f;
        /// <summary>同一名玩家周边此距离内已有烬暴时不再点名（像素）</summary>
        internal const float StormCrowdDist = 2600f;

        //==== 通用 ====
        /// <summary>城镇安宁半径（60 格）</summary>
        private const float TownCalmRadius = 960f;
        /// <summary>触发条件不满足时的复查间隔</summary>
        internal const int TriggerRetryFrames = 60;

        //==== 地狱色板 ====
        /// <summary>灰烬暗色（絮片/烬幕实体层，真 alpha 染色）</summary>
        internal static readonly Color AshDark = new(52, 38, 36);
        /// <summary>火星暖色（A=0 加色敷料）</summary>
        internal static readonly Color EmberWarm = new(255, 148, 58);
        /// <summary>红霾警示色（烬暴前缘）</summary>
        internal static readonly Color HazeRed = new(150, 44, 30);
        /// <summary>熔壳暗色（熔泡穹顶/熔渣珠实体层）</summary>
        internal static readonly Color CrustDark = new(74, 36, 28);
        /// <summary>熔芯亮色（A=0 加色敷料）</summary>
        internal static readonly Color MagmaBright = new(255, 132, 40);

        /// <summary>槽位检测契约：地狱高度带（ZoneUnderworldHeight）</summary>
        internal static bool InUnderworld(Player player) => player.ZoneUnderworldHeight;

        /// <summary>本槽位氛围在场：残酷模式开启且本玩家在地狱</summary>
        internal static bool AmbienceActive(Player player)
            => GameModeSystem.BrutalActive && InUnderworld(player);

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

        /// <summary>
        /// 岩浆珠伤害基准：取地狱原版敌怪（地狱蝙蝠）接触伤害乘 <see cref="BeadDamageFrac"/>。
        /// 原版敌对弹幕命中玩家自带 ×2/×4/×6 难度倍率，此处先除以基础 2，
        /// 难度递进交给原版结算，避免双重吃倍率（Brief 公平性条款）
        /// </summary>
        internal static int BeadDamage() {
            int baseContact = 34;
            if (ContentSamples.NpcsByNetId.TryGetValue(NPCID.Hellbat, out NPC sample)) {
                baseContact = Math.Max(sample.damage, 10);
            }
            return Math.Max(1, (int)(baseContact * BeadDamageFrac / 2f));
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

        /// <summary>该瓦格是否为岩浆液体（含任意量）</summary>
        internal static bool IsLavaTile(int tileX, int tileY) {
            if (!WorldGen.InWorld(tileX, tileY, 10)) {
                return false;
            }
            Tile tile = Framing.GetTileSafely(tileX, tileY);
            return tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Lava && !WorldGen.SolidTile(tileX, tileY);
        }

        /// <summary>该瓦格是否为岩浆液面（本格岩浆、上格无液且非实心，穹顶有露头空间）</summary>
        internal static bool IsLavaSurface(int tileX, int tileY) {
            if (!IsLavaTile(tileX, tileY)) {
                return false;
            }
            if (!WorldGen.InWorld(tileX, tileY - 1, 10)) {
                return false;
            }
            Tile above = Framing.GetTileSafely(tileX, tileY - 1);
            return above.LiquidAmount == 0 && !WorldGen.SolidTile(tileX, tileY - 1);
        }

        /// <summary>
        /// 在指定列向下找岩浆液面，命中返回液面锚点（像素，按液量对齐液面高度）。
        /// 要求池体规模：本列向下至少再 1 格岩浆，且左右至少一侧邻列同为岩浆（排除单格浅坑）
        /// </summary>
        internal static bool TryFindLavaSurfaceInColumn(int tileX, int startTileY, int scanDown, out Vector2 anchor) {
            anchor = default;
            for (int dy = 0; dy < scanDown; dy++) {
                int tileY = startTileY + dy;
                if (!WorldGen.InWorld(tileX, tileY, 10)) {
                    return false;
                }
                if (WorldGen.SolidTile(tileX, tileY)) {
                    return false;//先撞到地面则此列无露天岩浆
                }
                if (!IsLavaSurface(tileX, tileY)) {
                    continue;
                }
                //池体规模门：够深且够宽才算“岩浆池”
                if (!IsLavaTile(tileX, tileY + 1)) {
                    return false;
                }
                if (!IsLavaTile(tileX - 1, tileY) && !IsLavaTile(tileX + 1, tileY)) {
                    return false;
                }
                Tile tile = Framing.GetTileSafely(tileX, tileY);
                float surfaceY = tileY * 16f + (255 - tile.LiquidAmount) / 255f * 16f;
                anchor = new Vector2(tileX * 16f + 8f, surfaceY);
                return true;
            }
            return false;
        }
    }
}
