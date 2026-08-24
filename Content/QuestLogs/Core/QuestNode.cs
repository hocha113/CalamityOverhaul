using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    public abstract class QuestNode : VaultType<QuestNode>, ILocalizedModType
    {
        #region Data
        private readonly static Dictionary<string, QuestNode> _quests = [];
        public static IReadOnlyCollection<QuestNode> AllQuests => _quests.Values;

        /// <summary>节点 ID</summary>
        public virtual string ID => Name;

        /// <summary>节点名称</summary>
        public LocalizedText DisplayName { get; protected set; }

        /// <summary>节点描述</summary>
        public LocalizedText Description { get; protected set; }

        /// <summary>详细任务描述</summary>
        public LocalizedText DetailedDescription { get; protected set; }

        /// <summary>图表位置，相对父节点</summary>
        public Vector2 Position;

        /// <summary>节点绝对位置</summary>
        public Vector2 CalculatedPosition {
            get {
                if (ParentIDs.Count > 0) {
                    var parent = GetQuest(ParentIDs[0]);
                    if (parent != null) {
                        return parent.CalculatedPosition + Position;
                    }
                }
                return Position;
            }
        }

        /// <summary>前置任务 ID 列表</summary>
        public List<string> ParentIDs = new();

        /// <summary>子任务 ID 列表</summary>
        public List<string> ChildIDs = new();

        /// <summary>图标类型</summary>
        public QuestIconType IconType = QuestIconType.Texture;

        /// <summary>图标纹理路径</summary>
        public string IconTexturePath;

        /// <summary>图标物品 ID，IconType 为 Item 时</summary>
        public int IconItemType;

        /// <summary>图标 NPC 类型，IconType 为 NPC 时</summary>
        public int IconNPCType;

        /// <summary>缓存图标纹理</summary>
        private Asset<Texture2D> _iconTextureCache;

        /// <summary>任务奖励列表</summary>
        public List<QuestReward> Rewards = new();

        /// <summary>任务目标列表</summary>
        public List<QuestObjective> Objectives = new();

        /// <summary>任务类型</summary>
        public QuestType QuestType;

        /// <summary>任务难度</summary>
        public QuestDifficulty Difficulty;

        /// <summary>章目枢纽：登记进左栏章目列表，供点击跳转</summary>
        public virtual bool IsChapterHub => false;

        /// <summary>
        /// 章目排序值，越小越靠前。无父根节点(起点)恒排最前，
        /// 教程按"章目第 0 条 = 起点任务"讲解，枢纽不要用 0 以下的值
        /// </summary>
        public virtual int ChapterOrder => 0;

        /// <summary>隐藏任务：解锁前不绘制、不可悬停，连线也不画</summary>
        public bool HiddenUntilUnlocked;

        /// <summary>此刻是否对玩家不可见</summary>
        public bool IsHiddenNow => HiddenUntilUnlocked && !IsUnlocked;

        /// <summary>
        /// 隐藏任务的触发条件，解锁除父任务外还需此条件成立。
        /// 条件按秒级节奏轮询，触发源要能持续成立（身处环境、持有物品、玩家持久标志）
        /// </summary>
        protected virtual bool HiddenTriggerMet() => true;

        /// <summary>是否已完成</summary>
        public bool IsCompleted {
            get => Main.LocalPlayer.GetModPlayer<QLPlayer>().GetQuestData(ID).IsCompleted;
            set {
                var data = Main.LocalPlayer.GetModPlayer<QLPlayer>().GetQuestData(ID);
                if (data.IsCompleted != value) {
                    data.IsCompleted = value;
                    if (value) {
                        OnCompletion();
                    }
                }
            }
        }

        /// <summary>已完成但奖励未领完</summary>
        public bool HasUnclaimedRewards => IsCompleted && Rewards != null
            && Rewards.Count > 0 && Rewards.Exists(r => !r.Claimed);

        /// <summary>已完成且奖励已领完</summary>
        public bool AllRewardsClaimed => IsCompleted && (Rewards == null
            || Rewards.Count == 0 || Rewards.TrueForAll(r => r.Claimed));

        /// <summary>是否已解锁</summary>
        public bool IsUnlocked {
            get => Main.LocalPlayer.GetModPlayer<QLPlayer>().GetQuestData(ID).IsUnlocked;
            set {
                var data = Main.LocalPlayer.GetModPlayer<QLPlayer>().GetQuestData(ID);
                if (data.IsUnlocked != value) {
                    data.IsUnlocked = value;
                    if (value) {
                        OnUnlock();
                    }
                }
            }
        }

        public string LocalizationCategory => "QuestLogs.QuestNode";
        #endregion

        public override bool IsLoadingEnabled(Mod mod) => CWRServerConfig.Instance.QuestLog;

        /// <summary>完成回调，勿在此改<see cref="IsCompleted"/></summary>
        protected virtual void OnCompletion() {
            if (Main.LocalPlayer.active) {
                QuestNotificationSystem.AddNotification(this);
            }

            //解锁子任务
            foreach (var quest in AllQuests) {
                //子任务/挂本节点为前置
                if (ChildIDs.Contains(quest.ID) || quest.ParentIDs.Contains(ID)) {
                    quest.CheckUnlock();
                }
            }
        }

        /// <summary>解锁回调，勿在此改<see cref="IsUnlocked"/></summary>
        protected virtual void OnUnlock() {

        }

        public void CheckUnlock() {
            if (IsUnlocked) return;

            bool allParentsCompleted = true;
            foreach (var parentID in ParentIDs) {
                var parent = GetQuest(parentID);
                if (parent == null || !parent.IsCompleted) {
                    allParentsCompleted = false;
                    break;
                }
            }

            if (allParentsCompleted && HiddenTriggerMet()) {
                IsUnlocked = true;
            }
        }

        public Texture2D GetIconTexture() {
            switch (IconType) {
                case QuestIconType.Item:
                    if (IconItemType > 0) {
                        Main.instance.LoadItem(IconItemType);
                        return TextureAssets.Item[IconItemType]?.Value;
                    }
                    break;

                case QuestIconType.NPC:
                    if (IconNPCType > 0) {
                        Main.instance.LoadNPC(IconNPCType);
                        return TextureAssets.Npc[IconNPCType]?.Value;
                    }
                    break;

                case QuestIconType.Texture:
                    if (!string.IsNullOrEmpty(IconTexturePath) && ModContent.HasAsset(IconTexturePath)) {
                        if (_iconTextureCache == null || !_iconTextureCache.IsLoaded) {
                            _iconTextureCache = CWRUtils.GetT2DAsset(IconTexturePath);
                        }
                        return _iconTextureCache?.Value;
                    }
                    break;
            }

            return VaultAsset.placeholder3.Value;
        }

        /// <summary>图标源矩形，动画帧</summary>
        public Rectangle? GetIconSourceRect(Texture2D texture) {
            if (texture == null) return null;

            switch (IconType) {
                case QuestIconType.Item:
                    if (IconItemType > 0 && Main.itemAnimations[IconItemType] != null) {
                        return Main.itemAnimations[IconItemType].GetFrame(texture);
                    }
                    return texture.Frame();

                case QuestIconType.NPC:
                    if (IconNPCType > 0) {
                        //NPC取首帧
                        return texture.Frame(1, Main.npcFrameCount[IconNPCType], 0, 0);
                    }
                    return texture.Frame();

                case QuestIconType.Texture:
                    return texture.Frame();
            }

            return texture.Frame();
        }

        public void SetItemIcon(int itemType) {
            IconType = QuestIconType.Item;
            IconItemType = itemType;
        }

        public void SetNPCIcon(int npcType) {
            IconType = QuestIconType.NPC;
            IconNPCType = npcType;
        }

        public void SetTextureIcon(string texturePath) {
            IconType = QuestIconType.Texture;
            IconTexturePath = texturePath;
        }

        protected void AddParent<T>() where T : QuestNode {
            ParentIDs.Add(typeof(T).Name);
        }

        protected void AddChild<T>() where T : QuestNode {
            ChildIDs.Add(typeof(T).Name);
        }

        public static QuestNode GetQuest(string id) => _quests.TryGetValue(id, out var quest) ? quest : null;
        public static QuestNode GetQuest<T>() where T : QuestNode => GetQuest(typeof(T).Name);

        public override void Unload() {
            _quests.Clear();
            _iconTextureCache = null;
        }

        protected sealed override void VaultRegister() {
            ModTypeLookup<QuestNode>.Register(this);
            Instances.Add(this);
            _quests.TryAdd(ID, this);
        }

        public override void VaultSetup() {
            try {
                SetStaticDefaults();
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[QuestNode:VaultSetup] an error has occurred:{ex.Message}");
            }

            DisplayName ??= this.GetLocalization(nameof(DisplayName), () => Name);
            Description ??= this.GetLocalization(nameof(Description), () => " ");
            DetailedDescription ??= this.GetLocalization(nameof(DetailedDescription), () => " ");
            InitializeRewards();
            for (int i = 0; i < Objectives.Count; i++) {
                if (Objectives[i].TargetItemID == 0 && IconType == QuestIconType.Item && IconItemType > 0) {
                    Objectives[i].TargetItemID = IconItemType;
                }

                if (Objectives[i].TargetNpcID == 0 && Objectives[i].DescriptionStyle == QuestObjectiveDescriptionStyle.DefeatNpc
                    && IconType == QuestIconType.NPC && IconNPCType > 0) {
                    Objectives[i].TargetNpcID = IconNPCType;
                }

                Objectives[i].Initialize(this, i);
            }
            PostSetup();
        }

        public void InitializeRewards() {
            for (int i = 0; i < Rewards.Count; i++) {
                Rewards[i].Initialize(this, i);
            }
        }

        public void AddReward(int itemType, int amount = 1, LocalizedText text = null) {
            if (itemType <= ItemID.None || amount <= 0) {
                return;
            }
            if (Rewards.Any(r => r.ItemType == itemType)) {
                return;
            }
            Rewards.Add(new QuestReward() {
                ItemType = itemType,
                Amount = amount,
                Description = text
            });
            InitializeRewards();
        }

        /// <summary>击败NPC目标，npcType=0时初始化从<see cref="IconNPCType"/>补全</summary>
        public void AddDefeatObjective(int npcType = 0) {
            Objectives.Add(new QuestObjective {
                DescriptionStyle = QuestObjectiveDescriptionStyle.DefeatNpc,
                TargetNpcID = npcType,
                RequiredProgress = 1
            });
        }

        /// <summary>获得物品目标，itemType=0时初始化从<see cref="IconItemType"/>补全</summary>
        public void AddObtainObjective(int itemType = 0) {
            Objectives.Add(new QuestObjective {
                DescriptionStyle = QuestObjectiveDescriptionStyle.ObtainItem,
                TargetItemID = itemType,
                RequiredProgress = 1
            });
        }

        public void AddCollectObjective(int amount, int itemType = 0) {
            if (amount <= 0) {
                return;
            }

            Objectives.Add(new QuestObjective {
                DescriptionStyle = QuestObjectiveDescriptionStyle.CollectItem,
                TargetItemID = itemType,
                RequiredProgress = amount
            });
        }

        /// <summary>SetStaticDefaults后，可依赖其它任务初始化</summary>
        public virtual void PostSetup() {

        }

        public virtual void UpdateByPlayer() { }

        public virtual void CraftedItem(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) { }

        public virtual void OnKillByNPC(NPC npc) { }

        public virtual void OnWorldEnter() { }

        public virtual bool PreDraw(SpriteBatch spriteBatch, Vector2 drawPos, float scale, bool isHovered, float alpha) { return true; }

        public virtual void PostDraw(SpriteBatch spriteBatch, Vector2 drawPos, float scale, bool isHovered, float alpha) { }
    }

    /// <summary>图标类型</summary>
    public enum QuestIconType
    {
        /// <summary>纹理文件</summary>
        Texture,
        /// <summary>物品图标</summary>
        Item,
        /// <summary>NPC 图标</summary>
        NPC
    }

    /// <summary>任务奖励</summary>
    public class QuestReward
    {
        /// <summary>奖励物品 ID</summary>
        public int ItemType;
        /// <summary>奖励数量</summary>
        public int Amount;
        /// <summary>自定义奖励描述，UI默认只显x数量</summary>
        public LocalizedText Description;

        private QuestNode _node;
        private int _index;

        public void Initialize(QuestNode node, int index) {
            _node = node;
            _index = index;
        }

        /// <summary>是否已领取</summary>
        public bool Claimed {
            get {
                if (_node == null) return false;
                var data = Main.LocalPlayer.GetModPlayer<QLPlayer>().GetQuestData(_node.ID);
                if (data.RewardsClaimed.Count <= _index) return false;
                return data.RewardsClaimed[_index];
            }
            set {
                if (_node == null) return;
                var data = Main.LocalPlayer.GetModPlayer<QLPlayer>().GetQuestData(_node.ID);
                while (data.RewardsClaimed.Count <= _index) data.RewardsClaimed.Add(false);
                data.RewardsClaimed[_index] = value;
            }
        }
    }

    /// <summary>任务目标</summary>
    public class QuestObjective
    {
        /// <summary>自定义描述，非Custom样式时通常不用</summary>
        public LocalizedText Description;
        /// <summary>自动描述模板样式</summary>
        public QuestObjectiveDescriptionStyle DescriptionStyle = QuestObjectiveDescriptionStyle.Custom;
        /// <summary>所需进度</summary>
        public int RequiredProgress;
        /// <summary>目标物品 ID</summary>
        public int TargetItemID;
        /// <summary>目标 NPC ID</summary>
        public int TargetNpcID;

        private QuestNode _node;
        private int _index;

        public void Initialize(QuestNode node, int index) {
            _node = node;
            _index = index;
        }

        public string GetDisplayText() => QuestObjectiveTemplates.Format(this);

        /// <summary>当前进度</summary>
        public int CurrentProgress {
            get {
                if (_node == null) return 0;
                var data = Main.LocalPlayer.GetModPlayer<QLPlayer>().GetQuestData(_node.ID);
                if (data.ObjectiveProgress.Count <= _index) return 0;
                return data.ObjectiveProgress[_index];
            }
            set {
                if (_node == null) return;
                var data = Main.LocalPlayer.GetModPlayer<QLPlayer>().GetQuestData(_node.ID);
                while (data.ObjectiveProgress.Count <= _index) data.ObjectiveProgress.Add(0);
                data.ObjectiveProgress[_index] = value;
            }
        }

        /// <summary>是否已完成</summary>
        public bool IsCompleted => CurrentProgress >= RequiredProgress;
    }

    /// <summary>任务类型</summary>
    public enum QuestType
    {
        Main,
        Side,
        Daily,
        Achievement
    }

    /// <summary>任务难度</summary>
    public enum QuestDifficulty
    {
        Easy,
        Normal,
        Hard,
        Expert,
        Master
    }
}