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

        /// <summary>任务完成回调，避免在此改<see cref="IsCompleted"/></summary>
        protected virtual void OnCompletion() {
            //播放完成通知
            if (Main.LocalPlayer.active) {
                QuestNotificationSystem.AddNotification(this);
            }

            //尝试解锁子任务
            foreach (var quest in AllQuests) {
                //子任务或以本任务为前置的任务
                if (ChildIDs.Contains(quest.ID) || quest.ParentIDs.Contains(ID)) {
                    quest.CheckUnlock();
                }
            }
        }

        /// <summary>任务解锁回调，避免在此改<see cref="IsUnlocked"/></summary>
        protected virtual void OnUnlock() {

        }

        /// <summary>检查解锁条件</summary>
        public void CheckUnlock() {
            if (IsUnlocked) return;

            //检查前置任务是否全部完成
            bool allParentsCompleted = true;
            foreach (var parentID in ParentIDs) {
                var parent = GetQuest(parentID);
                if (parent == null || !parent.IsCompleted) {
                    allParentsCompleted = false;
                    break;
                }
            }

            if (allParentsCompleted) {
                IsUnlocked = true;
            }
        }

        /// <summary>获取任务图标纹理</summary>
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

        /// <summary>图标源矩形，动画帧用</summary>
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
                        //NPC使用第一帧
                        return texture.Frame(1, Main.npcFrameCount[IconNPCType], 0, 0);
                    }
                    return texture.Frame();

                case QuestIconType.Texture:
                    return texture.Frame();
            }

            return texture.Frame();
        }

        /// <summary>设置物品图标</summary>
        public void SetItemIcon(int itemType) {
            IconType = QuestIconType.Item;
            IconItemType = itemType;
        }

        /// <summary>设置 NPC 图标</summary>
        public void SetNPCIcon(int npcType) {
            IconType = QuestIconType.NPC;
            IconNPCType = npcType;
        }

        /// <summary>设置纹理图标</summary>
        public void SetTextureIcon(string texturePath) {
            IconType = QuestIconType.Texture;
            IconTexturePath = texturePath;
        }

        /// <summary>添加前置任务</summary>
        protected void AddParent<T>() where T : QuestNode {
            ParentIDs.Add(typeof(T).Name);
        }

        /// <summary>添加子任务</summary>
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
                //无目标物品且图标为物品时，默认用图标物品
                if (Objectives[i].TargetItemID == 0 && IconType == QuestIconType.Item && IconItemType > 0) {
                    Objectives[i].TargetItemID = IconItemType;
                }
                Objectives[i].Initialize(this, i);
            }
            PostSetup();
        }

        /// <summary>初始化奖励数据</summary>
        public void InitializeRewards() {
            for (int i = 0; i < Rewards.Count; i++) {
                Rewards[i].Initialize(this, i);
            }
        }

        /// <summary>添加奖励</summary>
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
                Description = text ?? VaultUtils.GetLocalizedItemName(itemType)
            });
            InitializeRewards();
        }

        /// <summary>SetStaticDefaults 后调用，依赖其它任务的初始化</summary>
        public virtual void PostSetup() {

        }

        /// <summary>每帧更新，检查完成条件</summary>
        public virtual void UpdateByPlayer() { }

        /// <summary>玩家合成物品时调用</summary>
        public virtual void CraftedItem(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) { }

        /// <summary>NPC 死亡时调用</summary>
        public virtual void OnKillByNPC(NPC npc) { }

        /// <summary>玩家进入世界时调用</summary>
        public virtual void OnWorldEnter() { }

        /// <summary>节点图标前绘制</summary>
        public virtual bool PreDraw(SpriteBatch spriteBatch, Vector2 drawPos, float scale, bool isHovered, float alpha) { return true; }

        /// <summary>节点图标后绘制</summary>
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
        /// <summary>奖励描述</summary>
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
        /// <summary>目标描述</summary>
        public LocalizedText Description;
        /// <summary>所需进度</summary>
        public int RequiredProgress;
        /// <summary>目标物品 ID</summary>
        public int TargetItemID;

        private QuestNode _node;
        private int _index;

        public void Initialize(QuestNode node, int index) {
            _node = node;
            _index = index;
        }

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