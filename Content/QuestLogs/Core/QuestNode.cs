using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    public abstract class QuestNode : VaultType<QuestNode>, ILocalizedModType
    {
        private readonly static Dictionary<string, QuestNode> _quests = [];
        public static IReadOnlyCollection<QuestNode> AllQuests => _quests.Values;

        /// <summary>
        /// 节点ID
        /// </summary>
        public virtual string ID => Name;

        /// <summary>
        /// 节点名称
        /// </summary>
        public LocalizedText DisplayName { get; private set; }

        /// <summary>
        /// 节点描述
        /// </summary>
        public LocalizedText Description { get; private set; }

        /// <summary>
        /// 详细任务描述
        /// </summary>
        public LocalizedText DetailedDescription { get; private set; }

        /// <summary>
        /// 节点在图表中的位置
        /// </summary>
        public Vector2 Position;

        /// <summary>
        /// 前置任务ID列表
        /// </summary>
        public List<string> ParentIDs = new();

        /// <summary>
        /// 子任务ID列表
        /// </summary>
        public List<string> ChildIDs = new();

        /// <summary>
        /// 图标类型
        /// </summary>
        public QuestIconType IconType = QuestIconType.Texture;

        /// <summary>
        /// 图标纹理路径
        /// </summary>
        public string IconTexturePath;

        /// <summary>
        /// 图标物品ID(当IconType为Item时使用)
        /// </summary>
        public int IconItemType;

        /// <summary>
        /// 图标NPC类型(当IconType为NPC时使用)
        /// </summary>
        public int IconNPCType;

        /// <summary>
        /// 缓存的图标纹理
        /// </summary>
        private Asset<Texture2D> _iconTextureCache;

        /// <summary>
        /// 任务奖励列表
        /// </summary>
        public List<QuestReward> Rewards = new();

        /// <summary>
        /// 任务目标列表
        /// </summary>
        public List<QuestObjective> Objectives = new();

        /// <summary>
        /// 任务类型
        /// </summary>
        public QuestType QuestType;

        /// <summary>
        /// 任务难度
        /// </summary>
        public QuestDifficulty Difficulty;

        public bool IsCompleted {
            get => Main.LocalPlayer.GetModPlayer<QLPlayer>().GetQuestData(ID).IsCompleted;
            set => Main.LocalPlayer.GetModPlayer<QLPlayer>().GetQuestData(ID).IsCompleted = value;
        }

        public bool IsUnlocked {
            get => Main.LocalPlayer.GetModPlayer<QLPlayer>().GetQuestData(ID).IsUnlocked;
            set => Main.LocalPlayer.GetModPlayer<QLPlayer>().GetQuestData(ID).IsUnlocked = value;
        }

        public string LocalizationCategory => "QuestLogs.QuestNode";

        /// <summary>
        /// 获取任务图标纹理
        /// </summary>
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
                    if (!string.IsNullOrEmpty(IconTexturePath)) {
                        if (_iconTextureCache == null || !_iconTextureCache.IsLoaded) {
                            _iconTextureCache = ModContent.Request<Texture2D>(IconTexturePath);
                        }
                        return _iconTextureCache?.Value;
                    }
                    break;
            }

            return null;
        }

        /// <summary>
        /// 获取图标源矩形(用于动画帧)
        /// </summary>
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

        /// <summary>
        /// 设置物品图标
        /// </summary>
        public void SetItemIcon(int itemType) {
            IconType = QuestIconType.Item;
            IconItemType = itemType;
        }

        /// <summary>
        /// 设置NPC图标
        /// </summary>
        public void SetNPCIcon(int npcType) {
            IconType = QuestIconType.NPC;
            IconNPCType = npcType;
        }

        /// <summary>
        /// 设置纹理图标
        /// </summary>
        public void SetTextureIcon(string texturePath) {
            IconType = QuestIconType.Texture;
            IconTexturePath = texturePath;
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
            DisplayName = this.GetLocalization(nameof(DisplayName), () => Name);
            Description = this.GetLocalization(nameof(Description), () => " ");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => " ");

            OnLoad();

            for (int i = 0; i < Rewards.Count; i++) {
                Rewards[i].Initialize(this, i);
            }
            for (int i = 0; i < Objectives.Count; i++) {
                Objectives[i].Initialize(this, i);
            }

            SetStaticDefaults();
        }

        /// <summary>
        /// 每帧更新逻辑，用于检查任务完成条件
        /// </summary>
        public virtual void OnUpdate() { }

        public virtual void OnLoad() { }
    }

    /// <summary>
    /// 图标类型枚举
    /// </summary>
    public enum QuestIconType
    {
        /// <summary>
        /// 使用纹理文件
        /// </summary>
        Texture,
        /// <summary>
        /// 使用物品图标
        /// </summary>
        Item,
        /// <summary>
        /// 使用NPC图标
        /// </summary>
        NPC
    }

    /// <summary>
    /// 任务奖励结构
    /// </summary>
    public class QuestReward
    {
        /// <summary>
        /// 奖励物品ID
        /// </summary>
        public int ItemType;
        /// <summary>
        /// 奖励数量
        /// </summary>
        public int Amount;
        /// <summary>
        /// 奖励描述
        /// </summary>
        public LocalizedText Description;

        private QuestNode _node;
        private int _index;

        public void Initialize(QuestNode node, int index) {
            _node = node;
            _index = index;
        }

        /// <summary>
        /// 是否已领取
        /// </summary>
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

    /// <summary>
    /// 任务目标结构
    /// </summary>
    public class QuestObjective
    {
        /// <summary>
        /// 目标描述
        /// </summary>
        public LocalizedText Description;
        /// <summary>
        /// 所需进度
        /// </summary>
        public int RequiredProgress;

        private QuestNode _node;
        private int _index;

        public void Initialize(QuestNode node, int index) {
            _node = node;
            _index = index;
        }

        /// <summary>
        /// 当前进度
        /// </summary>
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

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted => CurrentProgress >= RequiredProgress;
    }

    /// <summary>
    /// 任务类型枚举
    /// </summary>
    public enum QuestType
    {
        Main,
        Side,
        Daily,
        Achievement
    }

    /// <summary>
    /// 任务难度枚举
    /// </summary>
    public enum QuestDifficulty
    {
        Easy,
        Normal,
        Hard,
        Expert,
        Master
    }
}
