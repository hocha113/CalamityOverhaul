using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.ADV.Scenarios;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    public class HalibutPlayer : PlayerOverride//这个类用于存储一些与玩家相关的额外数据
    {
        #region Data
        /// <summary>
        /// 技能ID
        /// </summary>
        public int SkillID;
        /// <summary>
        /// 是否手持大比目鱼
        /// </summary>
        public bool HeldHalibut;
        /// <summary>
        /// 是否拥有大比目鱼
        /// </summary>
        public bool HasHalubut;
        /// <summary>
        /// 是否尝试关闭眼睛
        /// </summary>
        internal bool CanCloseEye;
        /// <summary>
        /// 隐藏玩家计时器
        /// </summary>
        public int HidePlayerTime;
        /// <summary>
        /// 锁定控制面板的时间
        /// </summary>
        public int IsInteractionLockedTime;
        /// <summary>
        /// 鼠标世界坐标，注意该属性只应该用于不精密的方向计算
        /// </summary>
        public Vector2 MouseWorld {
            get {
                if (Player.whoAmI == Main.myPlayer) {
                    UpdateMouseWorld();
                    return Player.Center + Player.To(Main.MouseWorld);
                }
                //返回玩家中心 + 方向向量 * 固定距离，用于动画计算
                return Player.Center + _mouseDirection * 500f;
            }
            set {
                //接收网络同步时，计算并存储方向
                Vector2 toMouse = value - Player.Center;
                if (toMouse.LengthSquared() > 1f) {
                    _mouseDirection = Vector2.Normalize(toMouse);
                }
            }
        }
        /// <summary>
        /// 鼠标相对于玩家的方向
        /// </summary>
        private Vector2 _mouseDirection;
        /// <summary>
        /// 上一次同步的鼠标方向（相对于玩家）
        /// </summary>
        private Vector2 _lastSyncedMouseDirection;
        /// <summary>
        /// 方向变化的最小角度阈值（弧度）
        /// </summary>
        private const float MIN_DIRECTION_CHANGE_THRESHOLD = 0.16f;

        #region 深渊复苏系统
        /// <summary>
        /// 深渊复苏系统实例
        /// </summary>
        public ResurrectionSystem ResurrectionSystem { get; private set; } = new();
        #endregion

        #region ADV场景数据
        public ADVSave ADVSave { get; private set; } = new();
        #endregion

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
        /// 螺旋尖锥突袭状态
        /// </summary>
        public bool FishConeSurgeActive { get; set; }
        /// <summary>
        /// 突袭后攻击后摇计时器
        /// </summary>
        public int AttackRecoveryTimer { get; set; }
        #endregion

        #region 蝙蝠
        /// <summary>
        /// 技能激活状态
        /// </summary>
        public bool BatSwarmActive { get; set; }
        /// <summary>
        /// 技能持续时间计数器
        /// </summary>
        public int BatSwarmTimer { get; set; }
        #endregion

        #region 克隆技能数据
        public bool CloneFishActive { get; set; }
        public int CloneFrameCounter { get; set; }
        public List<PlayerSnapshot> CloneSnapshots { get; set; } = new();
        public List<CloneShootEvent> CloneShootEvents { get; set; } = new();
        private const int MaxSnapshots = 60 * 10; //最多记录10秒（支持更长延迟）
        /// <summary>
        /// 克隆技能触发冷却，防止一帧多次切换
        /// </summary>
        public int CloneFishToggleCD { get; set; }
        /// <summary>
        /// 克隆体数量（1-10）
        /// </summary>
        public int CloneCount { get; set; } = 1;//先保持1个
        /// <summary>
        /// 最小延迟帧数（最近的克隆体与玩家的时间差，30帧=0.5秒）
        /// </summary>
        public int CloneMinDelay { get; set; } = 60;
        /// <summary>
        /// 克隆体间隔帧数（每个克隆体之间的时间差，20帧=0.33秒）
        /// </summary>
        public int CloneInterval { get; set; } = 30;
        /// <summary>
        /// 将要启动克隆
        /// </summary>
        public bool OnStartClone;
        #endregion

        #region 海域领域技能数据
        /// <summary>
        /// 海域领域是否激活
        /// </summary>
        public bool SeaDomainActive { get; set; }
        /// <summary>
        /// 海域领域触发冷却
        /// </summary>
        public int SeaDomainToggleCD { get; set; }
        /// <summary>
        /// 海域领域层数（1-10）
        /// </summary>
        public int SeaDomainLayers { get; set; } = 1;
        /// <summary>
        /// 将要启动领域
        /// </summary>
        public bool OnStartSeaDomain;
        #endregion

        #region 重启技能数据
        /// <summary>
        /// 重启技能触发冷却
        /// </summary>
        public int RestartFishToggleCD { get; set; }
        /// <summary>
        /// 重启技能冷却时间
        /// </summary>
        public int RestartFishCooldown { get; set; }
        #endregion

        #region 瞬移技能数据
        /// <summary>
        /// 瞬移技能触发冷却
        /// </summary>
        public int FishTeleportToggleCD { get; set; }
        /// <summary>
        /// 瞬移技能冷却时间
        /// </summary>
        public int FishTeleportCooldown { get; set; }
        #endregion

        #region 叠加攻击技能数据
        /// <summary>
        /// 叠加攻击触发冷却
        /// </summary>
        public int SuperpositionToggleCD { get; set; }
        /// <summary>
        /// 叠加攻击冷却时间
        /// </summary>
        public int SuperpositionCooldown { get; set; }
        #endregion

        /// <summary>
        /// 每个时期阶段对应的死机等级
        /// </summary>
        private static Dictionary<int, int> CrashesLevelDictionary => new Dictionary<int, int>(){
            {0, 0},
            {1, 0},
            {2, 1},
            {3, 2},
            {4, 3},
            {5, 3},
            {6, 4},
            {7, 5},
            {8, 5},
            {9, 6},
            {10, 7},
            {11, 8},
            {12, 8},
            {13, 9},
            {14, 10}
        };
        #endregion
        /// <summary>
        /// 获取死机等级
        /// </summary>
        public static int GetCrashesLevel(Item item) {
            if (Main.LocalPlayer.name == "杨戬") {
                return 14;
            }
            int level = HalibutData.GetLevel(item);
            return CrashesLevelDictionary.TryGetValue(level, out int value) ? value : 0;
        }
        /// <summary>
        /// 低于或者等于这个等级的眼睛会进入死机状态
        /// </summary>
        public int CrashesLevel() {
            int level = GetCrashesLevel(Player.GetItem());
            if (Player.HasBuff<FishoilBuff>()) {
                level++;//鱼油加持下可以临时的多死机一只眼
            }
            if (!Main.gameMenu &&
                Player.TryGetModPlayer<SirenMusicalBoxPlayer>(out var sirenMusicalBoxPlayer)
                && sirenMusicalBoxPlayer.IsCursed) {
                if (level < 5) {
                    level = 5;//被诅咒时最低死机等级为5
                }
            }
            return (int)MathHelper.Clamp(level, 0, 10);
        }

        /// <summary>
        /// 我是一个时代孕育出来的唯一，既然敢舍弃玩家的身份，自然是无所不能
        /// </summary>
        /// <returns></returns>
        public static bool TheOnlyBornOfAnEra() {
            return HalibutData.GetLevel(Main.LocalPlayer.GetItem()) == 14;
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type != CWRMessageType.HalibutMouseWorld) {
                return;
            }

            try {
                int playerIndex = reader.ReadByte();

                if (!playerIndex.TryGetPlayer(out var player) || player == null) {
                    return;
                }
                if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer) || halibutPlayer == null) {
                    return;
                }

                //读取同步的方向向量
                Vector2 mouseDirection = reader.ReadVector2();

                //验证方向向量的合法性（应该是单位向量）
                float lengthSq = mouseDirection.LengthSquared();
                if (lengthSq < 0.9f || lengthSq > 1.1f) {
                    return;//不是有效的单位向量，忽略
                }

                //更新方向
                halibutPlayer._mouseDirection = mouseDirection;

                halibutPlayer.MouseWorld = player.Center + mouseDirection * 500f;

                if (!VaultUtils.isServer) {
                    return;
                }

                //服务器转发给其他客户端
                ModPacket modPacket = CWRMod.Instance.GetPacket();
                modPacket.Write((byte)CWRMessageType.HalibutMouseWorld);
                modPacket.Write((byte)playerIndex);
                modPacket.WriteVector2(mouseDirection);
                modPacket.Send(-1, whoAmI);
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error("Error in HandleHalibutMouseWorld: " + ex.Message);
            }
        }

        private void UpdateMouseWorld() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            //计算鼠标相对于玩家的方向
            Vector2 mouseWorld = Main.MouseWorld;
            Vector2 mouseDirection = mouseWorld - Player.Center;

            if (mouseDirection.LengthSquared() < 1f) {
                //鼠标太接近玩家中心，不更新方向
                return;
            }
            mouseDirection.Normalize();

            //计算方向变化的角度差
            float directionDot = Vector2.Dot(mouseDirection, _lastSyncedMouseDirection);

            //只有当角度差超过阈值时才同步
            if (directionDot >= 1f || (_lastSyncedMouseDirection != Vector2.Zero &&
                Math.Acos(MathHelper.Clamp(directionDot, -1f, 1f)) < MIN_DIRECTION_CHANGE_THRESHOLD)) {
                return;
            }

            //更新本地方向
            _mouseDirection = mouseDirection;

            //方向发生了显著变化，执行网络同步
            //只同步方向向量，而不是完整的世界坐标
            if (VaultUtils.isClient) {
                ModPacket modPacket = CWRMod.Instance.GetPacket();
                modPacket.Write((byte)CWRMessageType.HalibutMouseWorld);
                modPacket.Write((byte)Player.whoAmI);
                //同步归一化的方向向量
                modPacket.WriteVector2(_mouseDirection);
                modPacket.Send();
            }
            _lastSyncedMouseDirection = mouseDirection;
        }

        public void CloseEyes() {
            if (!Player.TryGetModPlayer<HalibutSave>(out var halibutSave)) {
                return;
            }

            //使用新的IsCrashed方法，传入player实例，确保在服务器上也能正确判断
            foreach (var save in halibutSave.activationSequence) {
                if (save.IsCrashedState(Player)) {
                    continue;//死机状态的眼睛不受影响
                }
                save.IsActive = false;//关掉所有眼球，避免死后继续因为眼球的复苏再次进入临界值
            }

            List<int> activeIndices = [];

            foreach (var index in halibutSave.activationSequence) {
                if (index.IsActive) {
                    activeIndices.Add(index.Index);
                }
            }

            //初始化一下，确保UI同步，因为死后不这么干的话顺序会乱掉
            halibutSave.InitializeEyes(activeIndices);
            ResurrectionSystem.ResurrectionRate = 0f;
        }

        public override bool? On_PreKill(double damage, int hitDirection, bool pvp
            , ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            CloneFishActive = false;//强制关闭克隆技能
            RestartFishCooldown = 0;//强制清除重启技能冷却
            FishTeleportCooldown = 0;//强制清除瞬移技能冷却
            SuperpositionCooldown = 0;//强制清除叠加攻击技能冷却
            if (Player.CountProjectilesOfID<YourLevelIsTooLowProj>() > 0) {
                return false;//无限重启，不死
            }
            return null;
        }

        public override void PostUpdate() {//在每帧更新后进行一些操作
            ResurrectionSystem.Player = Player;
            if (HeldHalibut && Player.Alives()) {
                if (CanCloseEye) {
                    CanCloseEye = false;
                    CloseEyes();
                }
                //更新深渊复苏系统
                ResurrectionSystem.Update();
            }

            if (Player.whoAmI == Main.myPlayer) {//关于ADV场景的更新只在本地玩家上进行
                foreach (var scenario in ADVScenarioBase.Instances) {
                    scenario.Update(ADVSave, this);
                }
            }

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

            //重启技能冷却
            if (RestartFishToggleCD > 0) RestartFishToggleCD--;
            if (RestartFishCooldown > 0) RestartFishCooldown--;

            //瞬移技能冷却
            if (FishTeleportToggleCD > 0) FishTeleportToggleCD--;
            if (FishTeleportCooldown > 0) FishTeleportCooldown--;

            //叠加攻击冷却
            if (SuperpositionToggleCD > 0) SuperpositionToggleCD--;
            if (SuperpositionCooldown > 0) SuperpositionCooldown--;

            if (HidePlayerTime > 0) HidePlayerTime--;

            foreach (var skill in FishSkill.Instances) {
                if (skill.UpdateCooldown(this, Player) && skill.Cooldown > 0) {
                    skill.Cooldown--;
                }
            }

            Item item = Player.GetItem();
            bool wasHeldHalibut = HeldHalibut;//记录上一帧状态
            HeldHalibut = item.Alives() && item.type == HalibutOverride.ID;
            HasHalubut = Player.inventory.Any(i => i.Alives() && i.type == HalibutOverride.ID);

            if (HasHalubut) {//只要拥有大比目鱼，就标记已经捕获过
                ADVSave.HasCaughtHalibut = true;
            }

            if (!HeldHalibut && Main.myPlayer == Player.whoAmI) {
                //当切换走武器时，如果领域或过去身处于激活状态，标记需要在重新拿起时恢复
                if (SeaDomainActive) {
                    OnStartSeaDomain = true;//重新拿起后触发底部的启动检测恢复
                    SeaDomain.Deactivate(Player);
                }
                if (CloneFishActive) {
                    OnStartClone = true;//重新拿起后触发底部的启动检测恢复
                    CloneFish.Deactivate(Player);
                }
            }

            if (VaultUtils.isServer || Main.myPlayer != Player.whoAmI || !HeldHalibut) {
                return;
            }

            if (IsInteractionLockedTime > 0) {
                IsInteractionLockedTime--;
                if (!DomainUI.Instance.IsInteractionLocked) {
                    DomainUI.Instance.IsInteractionLocked = true;
                }
            }
            else {
                if (DomainUI.Instance.IsInteractionLocked) {
                    DomainUI.Instance.IsInteractionLocked = false;
                }
            }

            YourLevelIsTooLow.TryAutoActivate(Player);

            if (CWRKeySystem.Halibut_UIControl.JustPressed && HalibutUIHead.Instance != null && HalibutUIHead.Instance.Active) {
                SoundEngine.PlaySound(CWRSound.ButtonZero);
                HalibutUIHead.Instance.Open = !HalibutUIHead.Instance.Open;
            }

            //海域领域激活检测，不要在服务器上访问按键
            if (CWRKeySystem.Halibut_Domain.JustPressed) {
                if (SeaDomainLayers > 0 || SeaDomainActive) {
                    SeaDomain.AltUse(Player);
                }
            }
            //封锁过去，埋葬现在，截断未来...难道我今天真的要被孟小董杀死？不，现在还不能放弃...
            else if (CWRKeySystem.Halibut_Clone.JustPressed) {
                if (SeaDomainLayers > 0) {
                    CloneCount = SeaDomainLayers;
                    CloneFish.AltUse(Player);
                }
            }
            //既然总部认为我已经死了，那么就让你们看看，我死后，到底会发生什么事情
            else if (CWRKeySystem.Halibut_Restart.JustPressed) {
                if (SeaDomainLayers >= 5) {//大于等于五层领域后才能使用
                    RestartFish.AltUse(Player);
                }
            }
            //叠加袭击
            else if (CWRKeySystem.Halibut_Superposition.JustPressed) {
                if (SeaDomainLayers >= 7) {//大于等于七层领域后才能使用
                    Superposition.AltUse(Player);
                }
            }
            //领域传送
            else if (CWRKeySystem.Halibut_Teleport.JustPressed) {
                if (SeaDomainActive) {
                    FishTeleport.AltUse(Player);
                }
            }

            if (!SeaDomainActive && OnStartSeaDomain && Player.CountProjectilesOfID<SeaDomainProj>() == 0) {
                if (SeaDomainLayers > 0) {
                    SeaDomain.AltUse(Player);
                }
                OnStartSeaDomain = false;
            }
            if (!CloneFishActive && OnStartClone && Player.CountProjectilesOfID<ClonePlayer>() == 0) {
                if (SeaDomainLayers > 0) {
                    CloneCount = SeaDomainLayers;
                    CloneFish.AltUse(Player);
                }
                OnStartClone = false;
            }
        }

        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            //这里可以操纵players移除不需要绘制的玩家达到隐藏玩家的目的
            if (HidePlayerTime > 0) {
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

        public void RegisterShoot(int projType, Vector2 velocity, int damage, float knockback, int itemType) {
            if (!CloneFishActive) {
                return;
            }
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
    }
}
