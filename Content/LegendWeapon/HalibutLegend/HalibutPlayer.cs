using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills;
using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutPlayer : PlayerOverride//这个类用于存储一些与玩家相关的额外数据
    {
        /// <summary>
        /// 技能ID
        /// </summary>
        public int SkillID;
        /// <summary>
        /// 是否拥有大比目鱼
        /// </summary>
        public bool HasHalibut;

        #region 闪光皇后
        /// <summary>
        /// 当前齐射是否激活
        /// </summary>
        public bool SparklingVolleyActive { get; set; }
        /// <summary>
        /// 当前正在齐射的唯一ID
        /// </summary>
        public int SparklingVolleyId { get; set; } = -1;
        /// <summary>
        /// 齐射内部计时
        /// </summary>
        public int SparklingVolleyTimer { get; set; }
        /// <summary>
        /// 武器普通攻击使用计数
        /// </summary>
        public int SparklingUseCounter { get; set; }
        /// <summary>
        /// 鱼数量
        /// </summary>
        public int SparklingFishCount { get; set; }
        /// <summary>
        /// 下一条鱼开火索引
        /// </summary>
        public int SparklingNextFireIndex { get; set; }
        /// <summary>
        /// 全部激光发射完成后的离场阶段
        /// </summary>
        public bool SparklingDeparturePhase { get; set; }
        /// <summary>
        /// 离场阶段计时
        /// </summary>
        public int SparklingDepartureTimer { get; set; }
        #endregion

        #region 鱼形换影
        /// <summary>
        /// 移形换影技能激活状态
        /// </summary>
        public bool FishSwarmActive { get; set; }
        /// <summary>
        /// 技能持续时间计数器
        /// </summary>
        public int FishSwarmTimer { get; set; }
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
        #endregion

        #region 克隆技能数据
        public bool CloneFishActive { get; set; }
        public int CloneFrameCounter { get; set; }
        public List<PlayerSnapshot> CloneSnapshots { get; set; } = new();
        public List<CloneShootEvent> CloneShootEvents { get; set; } = new();
        private const int MaxSnapshots = 60 * 10; //最多记录10秒（支持更长延迟）
        /// <summary>克隆技能触发冷却，防止一帧多次切换</summary>
        public int CloneFishToggleCD { get; set; }

        /// <summary>克隆体数量（1-5）</summary>
        public int CloneCount { get; set; } = 1;//先保持1个
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
        public int SeaDomainLayers { get; set; } = 1;
        #endregion

        #region 重启技能数据
        /// <summary>重启技能触发冷却</summary>
        public int RestartFishToggleCD { get; set; }
        /// <summary>重启技能冷却时间</summary>
        public int RestartFishCooldown { get; set; }
        #endregion

        #region 瞬移技能数据
        /// <summary>瞬移技能触发冷却</summary>
        public int FishTeleportToggleCD { get; set; }
        /// <summary>瞬移技能冷却时间</summary>
        public int FishTeleportCooldown { get; set; }
        #endregion

        #region 叠加攻击技能数据
        /// <summary>叠加攻击触发冷却</summary>
        public int SuperpositionToggleCD { get; set; }
        /// <summary>叠加攻击冷却时间</summary>
        public int SuperpositionCooldown { get; set; }
        #endregion

        #region 终极技能数据
        /// <summary>终极技能触发冷却</summary>
        public int YourLevelIsTooLowToggleCD { get; set; }
        /// <summary>终极技能冷却时间</summary>
        public int YourLevelIsTooLowCooldown { get; set; }
        #endregion

        public override void ResetEffects() {//用于每帧恢复数据

        }

        public override void PostUpdate() {//在每帧更新后进行一些操作
            //克隆技能记录
            if (CloneFishActive) {
                CloneFrameCounter++;
                //记录快照
                CloneSnapshots.Add(new PlayerSnapshot(Player));
                if (CloneSnapshots.Count > MaxSnapshots) {
                    CloneSnapshots.RemoveAt(0);
                }
            }
            else {
                //不活动时清理历史
                if (CloneSnapshots.Count > 0) CloneSnapshots.Clear();
                if (CloneShootEvents.Count > 0) CloneShootEvents.Clear();
                CloneFrameCounter = 0;
            }

            if (CloneFishToggleCD > 0) CloneFishToggleCD--;

            //海域领域冷却
            if (SeaDomainToggleCD > 0) SeaDomainToggleCD--;
            if (SeaDomainCooldown > 0) SeaDomainCooldown--;

            //重启技能冷却
            if (RestartFishToggleCD > 0) RestartFishToggleCD--;
            if (RestartFishCooldown > 0) RestartFishCooldown--;

            //瞬移技能冷却
            if (FishTeleportToggleCD > 0) FishTeleportToggleCD--;
            if (FishTeleportCooldown > 0) FishTeleportCooldown--;

            //叠加攻击冷却
            if (SuperpositionToggleCD > 0) SuperpositionToggleCD--;
            if (SuperpositionCooldown > 0) SuperpositionCooldown--;

            //终极技能冷却
            if (YourLevelIsTooLowToggleCD > 0) YourLevelIsTooLowToggleCD--;
            if (YourLevelIsTooLowCooldown > 0) YourLevelIsTooLowCooldown--;

            foreach(var skill in FishSkill.Instances) {
                if (skill.UpdateCooldown(this, Player) && skill.Cooldown > 0) {
                    skill.Cooldown--;
                }
            }

            Item item = Player.GetItem();
            HasHalibut = item.Alives() && item.type == HalibutOverride.ID;

            if (VaultUtils.isServer || !HasHalibut) {
                return;
            }

            //海域领域激活检测，不要在服务器上访问按键
            if (CWRKeySystem.Halibut_Domain.JustPressed) {
                SeaDomain.AltUse(item, Player);
            }
            //封锁过去，埋葬现在，截断未来...难道我今天真的要被孟小董杀死？不，现在还不能放弃...
            else if (CWRKeySystem.Halibut_Clone.JustPressed) {
                CloneCount = SeaDomainLayers;
                CloneFish.AltUse(item, Player);
            }
            //既然总部认为我已经死了，那么就让你们看看，我死后，到底会发生什么事情
            else if (CWRKeySystem.Halibut_Restart.JustPressed) {
                if (SeaDomainLayers >= 5) {//大于等于五层领域后才能使用
                    RestartFish.AltUse(item, Player);
                }
            }
            //叠加袭击
            else if (CWRKeySystem.Halibut_Superposition.JustPressed) {
                if (SeaDomainLayers >= 7) {//大于等于七层领域后才能使用
                    Superposition.AltUse(item, Player);
                }
            }
        }

        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            //这里可以操纵players移除不需要绘制的玩家达到隐藏玩家的目的
            if (FishSwarmActive) {
                //移除正在使用技能的玩家，使其隐藏
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
            if (CloneShootEvents.Count > 1000) { //增加事件缓存
                CloneShootEvents.RemoveAt(0);
            }
        }
        #endregion
    }
}
