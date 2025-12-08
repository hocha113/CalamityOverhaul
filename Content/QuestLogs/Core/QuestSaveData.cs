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
            return new QuestSaveData {
                IsUnlocked = tag.GetBool("IsUnlocked"),
                IsCompleted = tag.GetBool("IsCompleted"),
                ObjectiveProgress = tag.GetList<int>("ObjectiveProgress") as List<int> ?? new(),
                RewardsClaimed = tag.GetList<bool>("RewardsClaimed") as List<bool> ?? new()
            };
        }
    }
}
