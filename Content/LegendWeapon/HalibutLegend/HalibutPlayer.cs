using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using CalamityOverhaul.Content.Scenarios.Helen;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.GameSystem;
using InnoVault.VaultNetworks;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    public class HalibutPlayer : PlayerOverride//比目鱼玩家扩展数据
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
        /// <summary>鼠标世界坐标，仅适合粗方向计算</summary>
        public Vector2 MouseWorld {
            get {
                if (TryGetMouseWorld(out Vector2 mouseWorld)) {
                    return mouseWorld;
                }

                return Main.MouseWorld;
            }
        }

        /// <summary>
        /// 尝试读取由 InnoVault 玩家网络框架同步的鼠标世界坐标
        /// </summary>
        public bool TryGetMouseWorld(out Vector2 mouseWorld) {
            if (Player.whoAmI == Main.myPlayer) {
                mouseWorld = Main.MouseWorld;
                return true;
            }

            PlayerNetwork.KeepAlive(Player, PlayerNetworkDataFlags.BasicAim);
            return PlayerNetwork.TryGetApproxMouseWorld(Player, out mouseWorld);
        }

        internal int PlayerLifeMax;

        #region 深渊复苏系统
        /// <summary>复苏系统实例</summary>
        public ResurrectionSystem ResurrectionSystem { get; private set; } = new();

        //复苏增长相关常量
        private const float BaseResurrectionRatePerEye = 0.02f;//单层基础复苏速度
        private const float GeometricFactor = 1.2f;//几何倍率（每更高一层的额外提高倍率）
        private const float CrashedEyeSideEffectRate = 0.0001f;//死机眼睛的极小副作用
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
        /// <summary>当前齐射编号（同一帧的射击共享一个编号，供过去身轮流分配）</summary>
        private int cloneVolleySeq = -1;
        /// <summary>上一次记录射击事件所在帧，用于划分齐射边界</summary>
        private int cloneLastShootFrame = -1;
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

        #region 海洋领域技能数据
        /// <summary>
        /// 海洋领域是否激活
        /// </summary>
        public bool SeaDomainActive { get; set; }
        /// <summary>
        /// 海洋领域触发冷却
        /// </summary>
        public int SeaDomainToggleCD { get; set; }
        /// <summary>
        /// 海洋领域层数（1-10）
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
        /// <summary>死机等级</summary>
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

        /// <summary>时代唯一判定（成长等级 14）</summary>
        public static bool TheOnlyBornOfAnEra() {
            return HalibutData.GetLevel(Main.LocalPlayer.GetItem()) == 14;
        }

        /// <summary>激活领域层数（按眼睛激活序）</summary>
        public int CalculateActiveDomainLayers() {
            if (!Player.TryGetModPlayer<HalibutSave>(out var save)) {
                return 0;
            }

            int baseCount = 0;
            foreach (var eye in save.activationSequence) {
                if (eye.IsActive) {
                    baseCount++;
                }
            }
            if (ExtraEyeEffective(save)) {
                baseCount++;
            }

            return baseCount;
        }

        /// <summary>第十眼当前是否生效：玩家已开启且满足存在条件（外圈满九眼且时代唯一）；只读判定，切走武器只是暂时不计入，不清除持久开启状态</summary>
        internal bool ExtraEyeEffective(HalibutSave save) {
            return save != null && save.ExtraEyeActive
                && save.activationSequence.Count >= 9
                && HalibutData.GetLevel(Player.GetItem()) == 14;
        }

        /// <summary>复苏速度 tick（按眼睛层级几何叠加）</summary>
        public void UpdateResurrectionRate() {
            if (!Player.TryGetModPlayer<HalibutSave>(out var save)) {
                return;
            }

            float rate = 0f;
            int crashLevel = CrashesLevel();

            foreach (var eye in save.activationSequence) {
                if (!eye.IsActive) {
                    continue;
                }

                int layer = eye.LayerNumber ?? 1;
                bool isCrashed = layer <= crashLevel;

                if (isCrashed) {
                    rate += CrashedEyeSideEffectRate;
                }
                else {
                    float eyeRate = BaseResurrectionRatePerEye * MathF.Pow(GeometricFactor, layer - 1);
                    rate += eyeRate;
                }
            }

            //第十眼的复苏贡献
            if (ExtraEyeEffective(save)) {
                bool crashed = 10 <= crashLevel;
                if (crashed) {
                    rate += CrashedEyeSideEffectRate;
                }
                else {
                    rate += BaseResurrectionRatePerEye * MathF.Pow(GeometricFactor, 9);
                }
            }

            ResurrectionSystem.ResurrectionRate = rate;
        }

        /// <summary>领域层数与复苏速度 tick</summary>
        public void UpdateDomainSystemData() {
            SeaDomainLayers = CalculateActiveDomainLayers();
            UpdateResurrectionRate();
        }

        public void CloseEyes() {
            if (!Player.TryGetModPlayer<HalibutSave>(out var halibutSave)) {
                return;
            }

            //IsCrashed传player，dedServ可判
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

            //死后重InitEyes，UI顺序同步
            halibutSave.InitializeEyes(activeIndices);
            ResurrectionSystem.ResurrectionRate = 0f;
        }

        public override bool? On_PreKill(double damage, int hitDirection, bool pvp
            , ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            CloneFishActive = false;//强制关闭克隆技能
            RestartFishCooldown = 0;//强制清除重启技能冷却
            FishTeleportCooldown = 0;//强制清除瞬移技能冷却
            SuperpositionCooldown = 0;//强制清除叠加攻击技能冷却
            return null;
        }

        public override void PostUpdate() {//每帧收尾
            if (Player.TryGetModPlayer<HalibutSave>(out var halibutSave) && halibutSave.FishSkill != null) {
                SkillID = halibutSave.FishSkill.ID;
            }

            ResurrectionSystem.Player = Player;
            if (HeldHalibut && Player.Alives()) {
                if (CanCloseEye) {
                    CanCloseEye = false;
                    CloseEyes();
                }
                //领域层数与复苏 tick
                UpdateDomainSystemData();
                //复苏 tick
                ResurrectionSystem.Update();
                //同步最大生命值
                PlayerLifeMax = (int)MathHelper.Clamp(PlayerLifeMax, Player.statLifeMax2, int.MaxValue - 1);
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
                cloneVolleySeq = -1;
                cloneLastShootFrame = -1;
            }

            if (TimeGear.TimeScale > 0) {
                if (CloneFishToggleCD > 0) CloneFishToggleCD--;

                //海洋领域冷却
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
            }

            Item item = Player.GetItem();
            bool wasHeldHalibut = HeldHalibut;//记录上一帧状态
            HeldHalibut = item.Alives() && item.type == HalibutOverride.ID;
            HasHalubut = Player.inventory.Any(i => i.Alives() && i.type == HalibutOverride.ID);

            if (HasHalubut) {//只要拥有大比目鱼，就标记已经捕获过
                HalibutState.Write(Player, d => d.HasCaughtHalibut = true, d => d.HasCaughtHalibut = true);
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
                IsInteractionLockedTime--;//交互锁定的视觉表现由UI层直接读取本计时器
            }

            YourLevelIsTooLow.TryAutoActivate(Player);

            if (CWRKeySystem.Legend_UIControl.JustPressed && UI.Atlas.HalibutAtlas.Instance != null) {
                UI.Atlas.HalibutAtlas.Instance.Toggle();
            }

            //海洋领域激活检测，不要在服务器上访问按键
            //骇客时间激活期间禁止使用领域技能以及切换领域状态
            if (Content.HackTimes.HackTime.Active) {
                //后续领域相关的 JustPressed 全部跳过
            }
            else if (CWRKeySystem.Legend_Domain.JustPressed) {
                if (SeaDomainLayers > 0 || SeaDomainActive) {
                    SeaDomain.AltUse(Player);
                }
            }
            //克隆技能
            else if (CWRKeySystem.Halibut_Clone.JustPressed) {
                if (SeaDomainLayers > 0) {
                    CloneCount = SeaDomainLayers;
                    CloneFish.AltUse(Player);
                }
            }
            //重启技能
            else if (CWRKeySystem.Legend_Restart.JustPressed) {
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
            else if (CWRKeySystem.Legend_Teleport.JustPressed) {
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
            //同一帧内的多发归为同一齐射，跨帧则推进齐射编号，供过去身轮流射击
            if (CloneFrameCounter != cloneLastShootFrame) {
                cloneVolleySeq++;
                cloneLastShootFrame = CloneFrameCounter;
            }
            CloneShootEvents.Add(new CloneShootEvent {
                FrameIndex = CloneFrameCounter,
                VolleyId = cloneVolleySeq,
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
