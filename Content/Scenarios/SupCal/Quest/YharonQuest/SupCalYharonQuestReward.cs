using CalamityOverhaul.Content.Items.Accessories;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Narrative.Common;
using CalamityOverhaul.Content.Scenarios.Helen;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.Quest.YharonQuest
{
    /// <summary>完成鬼面刀任务后的奖励场景</summary>
    internal sealed class SupCalYharonQuestReward : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.SupCal";
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }

        public override StyleId DefaultStyle => "Brimstone";

        public override void SetStaticDefaults() {

            Line1 = this.GetLocalization(nameof(Line1), () => "啊......终于结束了");
            Line2 = this.GetLocalization(nameof(Line2), () => "对我来说......那条龙，是极少数值得尊敬的生物");
            Line3 = this.GetLocalization(nameof(Line3), () => "明知自己会死，却仍然选择站在那里......即使借助金源魄来重启，归来的也只是复制体");
            Line4 = this.GetLocalization(nameof(Line4), () => "可惜，它擅长服从，而你擅长......嗯，活下来？");
            Line5 = this.GetLocalization(nameof(Line5), () => "拿着。金源锭。曾被叫做‘炼狱之金’，是凡人所能触碰的力量极限");
            Line6 = this.GetLocalization(nameof(Line6), () => "接下来，轮到我了");
            Line7 = this.GetLocalization(nameof(Line7), () => "你在开什么玩笑......？");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("SupCal", "Sigh", Line1.Value)
             .Say("SupCal", Line2.Value)
             .Say("SupCal", "CloseEye", Line3.Value)
             .Say("SupCal", Line4.Value)
             .SayReward("SupCal", "CloseEye", Line5.Value, CWRID.Item_AuricBar, stack: 302, title: string.Empty)
             .SayReward("SupCal", Line6.Value, ModContent.ItemType<Proverbs>(), title: string.Empty, anchorYOffset: -60f);

            if (HasHalibut()) {
                n.Say("Helen", "Solemn", Line7.Value);
            }
        }

        protected override void OnStarted() => SupCalEffect.IsActive = true;

        protected override void OnCompleted() {
            SupCalEffect.IsActive = false;
            HalibutStorySync.WriteSupCal(
                d => d.SupCalYharonQuestRewardSceneComplete = true,
                d => d.SupCalYharonQuestRewardSceneComplete = true);
        }

        private static bool HasHalibut() {
            try {
                return Main.LocalPlayer.TryGetOverride(out HalibutPlayer halibutPlayer) && halibutPlayer.HasHalubut;
            } catch {
                return false;
            }
        }
    }

    /// <summary>鬼面刀击杀焚世龙追踪</summary>
    internal class YharonQuestTracker : BaseDamageTracker
    {
        internal const float REQUIRED_CONTRIBUTION = 0.75f;

        internal override int TargetNPCType => CWRID.NPC_Yharon;

        internal override int[] TargetWeaponTypes => [ModContent.ItemType<OniMachete>()];

        internal override int[] TargetProjectileTypes => [
            ModContent.ProjectileType<OniHandMinion>(),
            ModContent.ProjectileType<OniFireBall>(),
            ModContent.ProjectileType<OniHandExplode>(),
            ModContent.ProjectileType<OniMacheteHeld>()
        ];

        internal override float RequiredContribution => REQUIRED_CONTRIBUTION;

        public override bool IsQuestActive(Player player) {
            if (!HalibutStorySync.ReadSupCal(d => d.SupCalYharonQuestAccepted, d => d.SupCalYharonQuestAccepted)) {
                return false;
            }

            if (HalibutStorySync.ReadSupCal(d => d.SupCalYharonQuestDeclined, d => d.SupCalYharonQuestDeclined)) {
                return false;
            }

            if (HalibutStorySync.ReadSupCal(d => d.SupCalYharonQuestReward, d => d.SupCalYharonQuestReward)) {
                return false;
            }

            return true;
        }

        public override void OnQuestCompleted(Player player, float contribution) {
            HalibutStorySync.WriteSupCal(
                d => d.SupCalYharonQuestReward = true,
                d => d.SupCalYharonQuestReward = true);
            SupCalQuestRewardTracker.NotifyYharonComplete();
        }
    }
}
