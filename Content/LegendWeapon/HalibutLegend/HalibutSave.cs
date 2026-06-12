using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    /// <summary>
    /// 比目鱼每玩家数据中心：技能装备栏、技能图鉴解锁集、领域之眼状态、研究祭坛进度
    /// UI层只读写本类，不持有任何独立数据源
    /// </summary>
    internal class HalibutSave : ModPlayer
    {
        /// <summary>
        /// 装备栏容量上限（轮盘扇区数上限）
        /// </summary>
        public const int LoadoutCap = 10;
        /// <summary>
        /// 外圈领域之眼数量
        /// </summary>
        public const int MaxEyes = 9;

        /// <summary>
        /// 装备栏：战斗中可通过轮盘/快捷键选择的技能，有序，容量 <see cref="LoadoutCap"/>
        /// </summary>
        public readonly List<FishSkill> loadout = [];
        /// <summary>
        /// 全部已解锁（已研究）的技能
        /// </summary>
        public readonly List<FishSkill> unlocked = [];
        /// <summary>
        /// 当前选中的技能
        /// </summary>
        public FishSkill FishSkill;

        /// <summary>
        /// 全部九只外圈领域之眼（固定顺序，按 <see cref="SeaEyeState.Index"/>）
        /// </summary>
        internal readonly List<SeaEyeState> eyes = [];
        /// <summary>
        /// 按激活顺序排列的眼睛列表，决定各眼层数
        /// </summary>
        public readonly List<SeaEyeState> activationSequence = [];
        /// <summary>
        /// 第十只中心额外之眼是否激活（需九眼全开且达到时代唯一条件）
        /// </summary>
        public bool ExtraEyeActive;

        #region 研究祭坛运行时状态（不持久化，与旧版行为一致）
        /// <summary>
        /// 祭坛中的鱼
        /// </summary>
        public Item StudyItem = new();
        /// <summary>
        /// 当前研究计时（帧）
        /// </summary>
        public int StudyTimer;
        /// <summary>
        /// 是否正在研究
        /// </summary>
        public bool IsStudying;
        /// <summary>
        /// 研究完成时的全局通知（参数为新解锁的技能），UI订阅以播放演出；卸载时清空
        /// </summary>
        public static Action<FishSkill> StudyCompleted;
        /// <summary>
        /// 每研究一条新鱼提升的复苏上限
        /// </summary>
        public const float ResurrectionMaxIncreasePerFish = 15f;
        #endregion

        public override void Unload() {
            StudyCompleted = null;
        }

        public override void Initialize() {
            loadout.Clear();
            unlocked.Clear();
            FishSkill = null;
            ExtraEyeActive = false;
            eyes.Clear();
            for (int i = 0; i < MaxEyes; i++) {
                eyes.Add(new SeaEyeState(i));
            }
            activationSequence.Clear();
            StudyItem = new Item();
            StudyTimer = 0;
            IsStudying = false;
        }

        #region 技能装备API
        /// <summary>
        /// 该技能是否已解锁
        /// </summary>
        public bool IsUnlocked(FishSkill skill) => skill != null && unlocked.Contains(skill);

        /// <summary>
        /// 解锁一个技能；默认在装备栏有空位时顺手装备
        /// </summary>
        /// <returns>是否产生了新的解锁</returns>
        public bool UnlockSkill(FishSkill skill, bool intoLoadout = true) {
            if (skill == null || unlocked.Contains(skill)) {
                return false;
            }
            unlocked.Add(skill);
            if (intoLoadout && loadout.Count < LoadoutCap && !loadout.Contains(skill)) {
                loadout.Add(skill);
            }
            return true;
        }

        /// <summary>
        /// 将已解锁的技能装入装备栏
        /// </summary>
        /// <param name="skill">目标技能</param>
        /// <param name="slot">期望槽位，-1表示追加到末尾；指定槽位已被占用时插入到该位置</param>
        public bool EquipSkill(FishSkill skill, int slot = -1) {
            if (skill == null || !unlocked.Contains(skill)) {
                return false;
            }
            if (loadout.Contains(skill)) {
                //已在装备栏中则视为移动
                if (slot >= 0) {
                    MoveLoadout(loadout.IndexOf(skill), slot);
                    return true;
                }
                return false;
            }
            if (loadout.Count >= LoadoutCap) {
                return false;
            }
            if (slot < 0 || slot >= loadout.Count) {
                loadout.Add(skill);
            }
            else {
                loadout.Insert(slot, skill);
            }
            return true;
        }

        /// <summary>
        /// 将技能从装备栏卸下（仍保留在解锁集中）
        /// </summary>
        public void UnequipSkill(FishSkill skill) {
            if (skill == null) {
                return;
            }
            loadout.Remove(skill);
            if (FishSkill == skill) {
                FishSkill = null;
            }
        }

        /// <summary>
        /// 调整装备栏顺序
        /// </summary>
        public void MoveLoadout(int from, int to) {
            if (from < 0 || from >= loadout.Count) {
                return;
            }
            to = Math.Clamp(to, 0, loadout.Count - 1);
            if (from == to) {
                return;
            }
            FishSkill skill = loadout[from];
            loadout.RemoveAt(from);
            loadout.Insert(to, skill);
        }
        #endregion

        #region 领域之眼API
        /// <summary>
        /// 当前激活的眼睛总数（含第十眼），即领域层数
        /// </summary>
        public int ActiveEyeCount {
            get {
                int count = 0;
                foreach (var eye in activationSequence) {
                    if (eye.IsActive) {
                        count++;
                    }
                }
                if (ExtraEyeActive) {
                    count++;
                }
                return count;
            }
        }

        /// <summary>
        /// 切换一只外圈眼睛的激活状态，维护激活顺序与层数，并同步领域/克隆的重启
        /// </summary>
        public void ToggleEye(SeaEyeState eye) {
            if (eye == null) {
                return;
            }
            if (eye.IsActive) {
                eye.IsActive = false;
                eye.LayerNumber = null;
                activationSequence.Remove(eye);
                RecalculateLayerNumbers();
            }
            else {
                eye.IsActive = true;
                if (!activationSequence.Contains(eye)) {
                    activationSequence.Add(eye);
                }
                eye.LayerNumber = activationSequence.Count;
            }
            NotifyDomainConfigChanged();
        }

        /// <summary>
        /// 切换第十眼的激活状态
        /// </summary>
        public void ToggleExtraEye() {
            ExtraEyeActive = !ExtraEyeActive;
            NotifyDomainConfigChanged();
        }

        private void RecalculateLayerNumbers() {
            for (int i = 0; i < activationSequence.Count; i++) {
                activationSequence[i].LayerNumber = i + 1;
            }
        }

        /// <summary>
        /// 眼睛配置变化后，若领域或克隆正在运行则标记重启以同步新层数
        /// </summary>
        public void NotifyDomainConfigChanged() {
            if (!Player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }
            if (halibutPlayer.SeaDomainActive) {
                halibutPlayer.OnStartSeaDomain = true;
                halibutPlayer.SeaDomainLayers = ActiveEyeCount;
                SeaDomain.Deactivate(Player);
            }
            if (halibutPlayer.CloneFishActive) {
                halibutPlayer.OnStartClone = true;
                halibutPlayer.CloneCount = halibutPlayer.SeaDomainLayers;
                CloneFish.Deactivate(Player);
            }
        }

        /// <summary>
        /// 根据激活索引序列重建眼睛状态（读档与死亡重置共用）
        /// </summary>
        public void InitializeEyes(List<int> activeIndices) {
            activationSequence.Clear();
            if (eyes.Count == 0) {
                for (int i = 0; i < MaxEyes; i++) {
                    eyes.Add(new SeaEyeState(i));
                }
            }
            foreach (var eye in eyes) {
                eye.IsActive = false;
                eye.LayerNumber = null;
            }
            foreach (int index in activeIndices) {
                if (index >= 0 && index < eyes.Count) {
                    var eye = eyes[index];
                    eye.IsActive = true;
                    activationSequence.Add(eye);
                    eye.LayerNumber = activationSequence.Count;
                }
            }
        }
        #endregion

        #region 研究祭坛
        /// <summary>
        /// 该物品是否是一条可研究且尚未研究过的鱼
        /// </summary>
        public bool CanStudy(Item item) {
            if (!item.Alives() || item.type <= ItemID.None) {
                return false;
            }
            if (!FishSkill.UnlockFishs.TryGetValue(item.type, out FishSkill skill)) {
                return false;
            }
            return !unlocked.Contains(skill);
        }

        /// <summary>
        /// 当前研究目标的总时长（帧）
        /// </summary>
        public int StudyDuration {
            get {
                if (StudyItem.Alives() && FishSkill.UnlockFishs.TryGetValue(StudyItem.type, out FishSkill skill)) {
                    return skill.ResearchDuration;
                }
                return 1200;
            }
        }

        public override void PostUpdate() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            UpdateStudy();
        }

        private void UpdateStudy() {
            if (!IsStudying) {
                return;
            }
            if (!StudyItem.Alives() || StudyItem.type <= ItemID.None) {
                IsStudying = false;
                StudyTimer = 0;
                return;
            }
            StudyTimer++;
            if (StudyTimer < StudyDuration) {
                return;
            }
            //研究完成
            SoundEngine.PlaySound(SoundID.ResearchComplete);
            IsStudying = false;
            StudyTimer = 0;
            if (FishSkill.UnlockFishs.TryGetValue(StudyItem.type, out FishSkill fishSkill) && UnlockSkill(fishSkill)) {
                if (Player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    var res = halibutPlayer.ResurrectionSystem;
                    res.MaxValue += ResurrectionMaxIncreasePerFish;
                    res.Reset();
                }
                StudyCompleted?.Invoke(fishSkill);
            }
            StudyItem.TurnToAir();
        }
        #endregion

        #region 存档
        //玩家是单实例的，UI也是单实例的，所以在保存加载中不要使用静态数据，每个玩家之间的数据必须独立
        public override void SaveData(TagCompound tag) {
            try {
                //全部已解锁技能（含各技能的自定义数据）
                IList<TagCompound> unlockedList = [];
                foreach (var skill in unlocked) {
                    if (skill == null) {
                        continue;
                    }
                    TagCompound skillTag = [];
                    skillTag["Name"] = skill.FullName;
                    skill.SaveData(skillTag);
                    unlockedList.Add(skillTag);
                }
                tag["UnlockedSkills"] = unlockedList;

                //装备栏顺序
                List<string> loadoutNames = [];
                foreach (var skill in loadout) {
                    if (skill != null) {
                        loadoutNames.Add(skill.FullName);
                    }
                }
                tag["Loadout"] = loadoutNames;

                if (FishSkill != null) {
                    tag["HalibutTargetSkillName"] = FishSkill.FullName;
                }

                //眼睛激活顺序与第十眼
                List<int> activeEyeIndices = [];
                foreach (var eye in activationSequence) {
                    if (eye.IsActive) {
                        activeEyeIndices.Add(eye.Index);
                    }
                }
                tag["ActiveEyeIndices"] = activeEyeIndices;
                tag["ExtraEyeActive"] = ExtraEyeActive;

                if (Player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    tag["ResurrectionSystem"] = halibutPlayer.ResurrectionSystem.SaveData();
                    tag["IsInteractionLockedTime"] = halibutPlayer.IsInteractionLockedTime;
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error("HalibutSave.SaveData Error", ex);
            }
        }

        public override void LoadData(TagCompound tag) {
            try {
                unlocked.Clear();
                loadout.Clear();
                FishSkill = null;

                if (tag.TryGet<IList<TagCompound>>("UnlockedSkills", out var unlockedList)) {
                    //新版格式
                    foreach (var skillTag in unlockedList) {
                        if (!skillTag.TryGet<string>("Name", out var name) ||
                            !FishSkill.NameToInstance.TryGetValue(name, out var fishSkill)) {
                            continue;
                        }
                        fishSkill.LoadData(skillTag);
                        if (!unlocked.Contains(fishSkill)) {
                            unlocked.Add(fishSkill);
                        }
                    }
                    if (tag.TryGet<List<string>>("Loadout", out var loadoutNames)) {
                        foreach (var name in loadoutNames) {
                            if (FishSkill.NameToInstance.TryGetValue(name, out var fishSkill)
                                && unlocked.Contains(fishSkill) && !loadout.Contains(fishSkill)
                                && loadout.Count < LoadoutCap) {
                                loadout.Add(fishSkill);
                            }
                        }
                    }
                }
                else {
                    //旧版迁移：FishSkills=主列表（带数据），SkillLibrary=技能库（仅名字）
                    if (tag.TryGet<IList<TagCompound>>("FishSkills", out var legacyMain)) {
                        foreach (var skillTag in legacyMain) {
                            if (!skillTag.TryGet<string>("Name", out var name) ||
                                !FishSkill.NameToInstance.TryGetValue(name, out var fishSkill)) {
                                continue;
                            }
                            fishSkill.LoadData(skillTag);
                            if (!unlocked.Contains(fishSkill)) {
                                unlocked.Add(fishSkill);
                            }
                            if (loadout.Count < LoadoutCap && !loadout.Contains(fishSkill)) {
                                loadout.Add(fishSkill);
                            }
                        }
                    }
                    if (tag.TryGet<IList<TagCompound>>("SkillLibrary", out var legacyLibrary)) {
                        foreach (var skillTag in legacyLibrary) {
                            if (!skillTag.TryGet<string>("Name", out var name) ||
                                !FishSkill.NameToInstance.TryGetValue(name, out var fishSkill)) {
                                continue;
                            }
                            if (!unlocked.Contains(fishSkill)) {
                                unlocked.Add(fishSkill);
                            }
                        }
                    }
                }

                if (tag.TryGet<string>("HalibutTargetSkillName", out var skillName)) {
                    FishSkill = FishSkill.NameToInstance.GetValueOrDefault(skillName);
                }

                if (tag.TryGet<List<int>>("ActiveEyeIndices", out var activeIndices)) {
                    InitializeEyes(activeIndices);
                }
                if (tag.TryGet("ExtraEyeActive", out bool extraEye)) {
                    ExtraEyeActive = extraEye;
                }

                if (Player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    if (tag.TryGet<TagCompound>("ResurrectionSystem", out var resurrectionTag)) {
                        halibutPlayer.ResurrectionSystem.LoadData(resurrectionTag);
                    }
                    //向后兼容：如果旧版ADCSave数据存在于HalibutSave中，委托给ADVLegacyMigration迁移
                    if (tag.ContainsKey("ADCSave") && Player.TryGetModPlayer<ADVSavePlayer>(out var advSavePlayer)) {
                        advSavePlayer.MigrateFromLegacy(tag);
                    }
                    if (tag.TryGet("IsInteractionLockedTime", out int isInteractionLockedTime)) {
                        halibutPlayer.IsInteractionLockedTime = isInteractionLockedTime;
                    }
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error("HalibutSave.LoadData Error", ex);
                unlocked.Clear();
                loadout.Clear();
                activationSequence.Clear();
                FishSkill = null;
            }
        }
        #endregion
    }
}
