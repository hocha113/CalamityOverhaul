using CalamityOverhaul.Common;
using System.Collections.Generic;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    public class QuestSaveData
    {
        public bool IsUnlocked;
        public bool IsCompleted;
        public List<int> ObjectiveProgress = new();
        public List<bool> RewardsClaimed = new();

        public static QuestSaveData Default => new() {
            IsUnlocked = false,
            IsCompleted = false,
            ObjectiveProgress = new(),
            RewardsClaimed = new()
        };

        public TagCompound Serialize() {
            return new TagCompound {
                ["IsUnlocked"] = IsUnlocked,
                ["IsCompleted"] = IsCompleted,
                ["ObjectiveProgress"] = ObjectiveProgress,
                ["RewardsClaimed"] = RewardsClaimed
            };
        }

        public static QuestSaveData Deserialize(TagCompound tag) {
            var data = new QuestSaveData();
            data.IsUnlocked = false;
            if (tag.TrySafeGet("IsUnlocked", out bool isUnlocked)) {
                data.IsUnlocked = isUnlocked;
            }
            data.IsCompleted = false;
            if (tag.TrySafeGet("IsCompleted", out bool isCompleted)) {
                data.IsCompleted = isCompleted;
            }
            data.ObjectiveProgress = [];
            if (tag.TrySafeGet("ObjectiveProgress", out List<int> objectiveProgress)) {
                data.ObjectiveProgress = objectiveProgress;
            }
            data.RewardsClaimed = [];
            if (tag.TrySafeGet("RewardsClaimed", out List<bool> rewardsClaimed)) {
                data.RewardsClaimed = rewardsClaimed;
            }
            return data;
        }
    }
}
