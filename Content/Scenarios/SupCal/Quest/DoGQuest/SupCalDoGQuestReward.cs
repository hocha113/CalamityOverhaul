using System.Collections.Generic;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Narrative.Common;
using CalamityOverhaul.Content.Narrative.Scenarios.Helen.Gifts;
using CalamityOverhaul.Content.Scenarios.Helen;
using CalamityOverhaul.Content.Scenarios.SupCal;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.Quest.DoGQuest
{
    /// <summary>神明吞噬者任务奖励场景</summary>
    internal sealed class SupCalDoGQuestReward : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }

        public override StyleId DefaultStyle => "Brimstone";

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "硫火女巫");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "比目鱼");

            Line1 = this.GetLocalization(nameof(Line1), () => "干净利落");
            Line2 = this.GetLocalization(nameof(Line2), () => "这把刀，一如既往地令人满意");
            Line3 = this.GetLocalization(nameof(Line3), () => "当年我还是凡人之躯时，就是用它亲手挖出老师的心脏，很好用，不是吗？");
            Line4 = this.GetLocalization(nameof(Line4), () => "拿好");
            Line5 = this.GetLocalization(nameof(Line5), () => "你有没有想过，如果下一次，我是委托你来杀我，你会怎么做？");
            Line6 = this.GetLocalization(nameof(Line6), () => "真遗憾，你和他注定见不了面。不然你们一定聊得很投机");
            Line7 = this.GetLocalization(nameof(Line7), () => "......我越来越受不了这家伙了");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("SupCal", Line1.Value)
             .Say("SupCal", Line2.Value)
             .Say("SupCal", Line3.Value)
             .Say("SupCal", Line4.Value)
             .Reward(ModContent.ItemType<OniMachete>(), 1, string.Empty)
             .Say("SupCal", Line5.Value)
             .Say("SupCal", Line6.Value);

            if (HasHalibut()) {
                n.Say("Helen", "SlightAnnoyed", Line7.Value);
            }
        }

        protected override void OnStarted() => SupCalEffect.IsActive = true;

        protected override void OnCompleted() {
            SupCalEffect.IsActive = false;
            HelensInterference.DelayTimer = Main.rand.Next(60 * 5, 60 * 6);
            HalibutStorySync.WriteSupCal(
                d => d.SupCalDoGQuestRewardSceneComplete = true,
                d => d.SupCalDoGQuestRewardSceneComplete = true);
        }

        private static bool HasHalibut() {
            try {
                return Main.LocalPlayer.TryGetOverride(out HalibutPlayer halibutPlayer) && halibutPlayer.HasHalubut;
            } catch {
                return false;
            }
        }
    }

    /// <summary>
    /// 追踪玩家使用刻心者击杀神明吞噬者
    /// </summary>
    internal class DoGQuestTracker : BaseDamageTracker
    {
        internal const float REQUIRED_CONTRIBUTION = 0.8f;

        internal override int TargetNPCType => CWRID.NPC_DevourerofGodsHead;

        internal override HashSet<int> OtherNPCType => [CWRID.NPC_DevourerofGodsBody, CWRID.NPC_DevourerofGodsTail];

        internal override int[] TargetWeaponTypes => [ModContent.ItemType<Heartcarver>()];

        internal override int[] TargetProjectileTypes => [
            ModContent.ProjectileType<HeartcarverHeld>(),
            ModContent.ProjectileType<HeartcarverDash>(),
            ModContent.ProjectileType<HeartcarverDagger>()
        ];

        internal override float RequiredContribution => REQUIRED_CONTRIBUTION;

        public override bool IsQuestActive(Player player) {
            if (!HalibutStorySync.ReadSupCal(d => d.SupCalQuestReward, d => d.SupCalQuestReward)) {
                return false;
            }

            if (HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestDeclined, d => d.SupCalDoGQuestDeclined)) {
                return false;
            }

            if (HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestReward, d => d.SupCalDoGQuestReward)) {
                return false;
            }

            return true;
        }

        public override void OnQuestCompleted(Player player, float contribution) {
            HalibutStorySync.WriteSupCal(
                d => d.SupCalDoGQuestReward = true,
                d => d.SupCalDoGQuestReward = true);
            SupCalQuestRewardTracker.NotifyDoGComplete();
        }
    }
}
