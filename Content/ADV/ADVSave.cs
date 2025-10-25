using System.Reflection;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV
{
    internal class ADVSave
    {
        public bool FirstMet;
        public bool FishoilQuestDeclined;
        public bool FishoilQuestAccepted;
        public bool FishoilQuestCompleted;
        public bool FirstResurrectionWarning;
        public bool QueenBeeGift;
        public bool SkeletronGift;
        public bool EyeOfCthulhuGift;
        public bool KingSlimeGift;
        public bool CrabulonGift;
        public bool PerforatorGift;
        public bool HiveMindGift;
        public bool WallOfFleshGift;
        public bool SlimeGodGift;
        public bool CryogenGift;
        public bool BrimstoneElementalGift;
        public bool AquaticScourgeGift;
        public bool CalamitasCloneGift;
        public bool PlanteraGift;
        public bool GolemGift;
        public bool MoonLordGift;
        public bool LeviathanGift;
        public bool PlaguebringerGift;
        public bool ProvidenceGift;
        public bool DevourerOfGodsGift;
        public bool YharonGift;
        public bool SupremeCalamitasGift;
        public bool FirstMetSupCal;
        public bool SupCalChoseToFight;
        public bool SupCalMoonLordReward;
        public bool SupCalDefeat;
        public bool SupCalQuestAccepted;//玩家是否接受了任务
        public bool SupCalQuestDeclined;//玩家是否拒绝了任务
        public bool SupCalQuestReward;//玩家是否完成了任务（击杀了Providence）
        public bool SupCalQuestRewardSceneComplete;//任务完成后的奖励场景是否已播放
        public bool SupCalDoGQuestReward; //玩家是否完成了神明吞噬者任务
        public bool SupCalDoGQuestRewardSceneComplete; //神明吞噬者奖励场景是否已播放
        public bool SupCalDoGQuestDeclined; //玩家是否拒绝了神明吞噬者任务

        public virtual TagCompound SaveData() {
            TagCompound tag = [];

            //使用反射自动保存所有公共字段
            FieldInfo[] fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields) {
                if (field.FieldType != typeof(bool)) {
                    continue;
                }
                tag[field.Name] = field.GetValue(this);
            }

            return tag;
        }

        public virtual void LoadData(TagCompound tag) {
            //使用反射自动加载所有公共字段
            FieldInfo[] fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields) {
                if (tag.TryGet(field.Name, out bool value)) {
                    field.SetValue(this, value);
                }
            }
        }
    }
}
