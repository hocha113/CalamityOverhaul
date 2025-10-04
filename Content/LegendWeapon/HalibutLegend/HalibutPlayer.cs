﻿using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills;
using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutPlayer : PlayerOverride//这个类用于存储一些与玩家相关的额外数据
    {
        /// <summary>闪光技能：当前齐射是否激活</summary>
        public bool SparklingVolleyActive { get; set; }
        /// <summary>闪光技能：齐射冷却计时（帧）</summary>
        public int SparklingVolleyCooldown { get; set; }
        /// <summary>闪光技能：当前正在齐射的唯一ID</summary>
        public int SparklingVolleyId { get; set; } = -1;
        /// <summary>闪光技能：齐射内部计时</summary>
        public int SparklingVolleyTimer { get; set; }
        /// <summary>闪光技能：武器普通攻击使用计数</summary>
        public int SparklingUseCounter { get; set; }
        /// <summary>闪光技能：基础冷却（帧）</summary>
        public const int SparklingBaseCooldown = 120; // 2秒
        /// <summary>闪光技能：鱼数量</summary>
        public int SparklingFishCount { get; set; }
        /// <summary>闪光技能：下一条鱼开火索引</summary>
        public int SparklingNextFireIndex { get; set; }
        /// <summary>闪光技能：全部激光发射完成后的离场阶段</summary>
        public bool SparklingDeparturePhase { get; set; }
        /// <summary>闪光技能：离场阶段计时</summary>
        public int SparklingDepartureTimer { get; set; }

        /// <summary>
        /// 移形换影技能激活状态
        /// </summary>
        public bool FishSwarmActive { get; set; }

        /// <summary>
        /// 技能持续时间计数器
        /// </summary>
        public int FishSwarmTimer { get; set; }

        /// <summary>
        /// 技能最大持续时间（5秒 = 300帧）
        /// </summary>
        public const int FishSwarmDuration = 300;

        /// <summary>
        /// 技能冷却时间
        /// </summary>
        public int FishSwarmCooldown { get; set; }

        /// <summary>
        /// 技能冷却最大时间（10秒）
        /// </summary>
        public const int FishSwarmMaxCooldown = 600;

        /// <summary>
        /// 螺旋尖锥突袭状态
        /// </summary>
        public bool FishConeSurgeActive { get; set; }

        /// <summary>
        /// 突袭后攻击后摇计时器
        /// </summary>
        public int AttackRecoveryTimer { get; set; }

        /// <summary>
        /// 攻击后摇持续时间（60帧 = 1秒）
        /// </summary>
        public const int AttackRecoveryDuration = 60;

        public override void ResetEffects() {//用于每帧恢复数据

        }

        public override void PostUpdate() {//在每帧更新后进行一些操作
            if (Player.ownedProjectileCounts[ModContent.ProjectileType<SparklingFishHolder>()] == 0) {
                if (SparklingVolleyCooldown > 0) {
                    SparklingVolleyCooldown--;
                }
            }

            if (SparklingVolleyActive) {
                if (SparklingVolleyTimer > 0 && Player.ownedProjectileCounts[ModContent.ProjectileType<SparklingFishHolder>()] == 0) {
                    SparklingVolleyActive = false;
                }
                SparklingVolleyTimer++;
            }

            // 更新技能状态
            if (FishSwarmActive) {
                FishSwarmTimer++;

                if (FishSwarmTimer >= FishSwarmDuration) {
                    // 技能结束
                    FishSwarmActive = false;
                    FishSwarmTimer = 0;
                    FishSwarmCooldown = 60;
                }
            }

            // 更新冷却
            if (FishSwarmCooldown > 0) {
                FishSwarmCooldown--;
            }

            // 更新攻击后摇
            if (AttackRecoveryTimer > 0) {
                AttackRecoveryTimer--;
            }
        }

        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            //这里可以操纵players移除不需要绘制的玩家达到隐藏玩家的目的
            if (FishSwarmActive) {
                // 移除正在使用技能的玩家，使其隐藏
                List<Player> visiblePlayers = new List<Player>();
                foreach (Player player in players) {
                    if (player.whoAmI != Player.whoAmI) {
                        visiblePlayers.Add(player);
                    }
                }
                players = visiblePlayers;
            }
            return true;
        }
    }
}
