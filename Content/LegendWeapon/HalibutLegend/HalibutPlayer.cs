using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills;
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

        #region 克隆技能数据
        public bool CloneFishActive { get; set; }
        public int CloneFrameCounter { get; set; }
        public List<PlayerSnapshot> CloneSnapshots { get; set; } = new();
        public List<CloneShootEvent> CloneShootEvents { get; set; } = new();
        private const int MaxSnapshots = 60 * 10; // 最多记录10秒（支持更长延迟）
        /// <summary>克隆技能触发冷却，防止一帧多次切换</summary>
        public int CloneFishToggleCD { get; set; }

        /// <summary>克隆体数量（1-5）</summary>
        public int CloneCount { get; set; } = 1;//调试，先保持1个
        /// <summary>最小延迟帧数（最近的克隆体与玩家的时间差，30帧=0.5秒）</summary>
        public int CloneMinDelay { get; set; } = 60;
        /// <summary>克隆体间隔帧数（每个克隆体之间的时间差，20帧=0.33秒）</summary>
        public int CloneInterval { get; set; } = 30;
        #endregion

        #region 海域领域技能数据
        /// <summary>海域领域是否激活</summary>
        public bool SeaDomainActive { get; set; }
        /// <summary>海域领域触发冷却</summary>
        public int SeaDomainToggleCD { get; set; }
        /// <summary>海域领域冷却时间</summary>
        public int SeaDomainCooldown { get; set; }
        /// <summary>海域领域最大冷却（8秒）</summary>
        public const int SeaDomainMaxCooldown = 480;
        /// <summary>海域领域层数（1-10）</summary>
        public int SeaDomainLayers { get; set; } = 3;
        #endregion

        #region 重启技能数据
        /// <summary>重启技能触发冷却</summary>
        public int RestartFishToggleCD { get; set; }
        /// <summary>重启技能冷却时间</summary>
        public int RestartFishCooldown { get; set; }
        #endregion

        #region 叠加攻击技能数据
        /// <summary>叠加攻击触发冷却</summary>
        public int SuperpositionToggleCD { get; set; }
        /// <summary>叠加攻击冷却时间</summary>
        public int SuperpositionCooldown { get; set; }
        #endregion

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

            // 克隆技能记录
            if (CloneFishActive) {
                CloneFrameCounter++;
                // 记录快照
                CloneSnapshots.Add(new PlayerSnapshot(Player));
                if (CloneSnapshots.Count > MaxSnapshots) {
                    CloneSnapshots.RemoveAt(0);
                }
            }
            else {
                // 不活动时清理历史
                if (CloneSnapshots.Count > 0) CloneSnapshots.Clear();
                if (CloneShootEvents.Count > 0) CloneShootEvents.Clear();
                CloneFrameCounter = 0;
            }

            if (CloneFishToggleCD > 0) CloneFishToggleCD--;

            // 海域领域冷却
            if (SeaDomainToggleCD > 0) SeaDomainToggleCD--;
            if (SeaDomainCooldown > 0) SeaDomainCooldown--;

            // 重启技能冷却
            if (RestartFishToggleCD > 0) RestartFishToggleCD--;
            if (RestartFishCooldown > 0) RestartFishCooldown--;

            // 叠加攻击冷却
            if (SuperpositionToggleCD > 0) SuperpositionToggleCD--;
            if (SuperpositionCooldown > 0) SuperpositionCooldown--;
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

        #region 事件登记接口
        public void RegisterShoot(int projType, Vector2 velocity, int damage, float knockback, int itemType) {
            if (!CloneFishActive) return;
            CloneShootEvents.Add(new CloneShootEvent {
                FrameIndex = CloneFrameCounter,
                Velocity = velocity,
                Type = projType,
                Damage = damage,
                KnockBack = knockback,
                Owner = Player.whoAmI,
                Position = Player.Center,
                ItemType = itemType
            });
            if (CloneShootEvents.Count > 1000) { // 增加事件缓存
                CloneShootEvents.RemoveAt(0);
            }
        }
        #endregion
    }
}
