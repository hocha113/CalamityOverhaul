using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Narrative.Common;
using CalamityOverhaul.Content.Scenarios.Helen;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.Quest.PallbearerQuest
{
    /// <summary>完成扶柩者任务后的奖励场景</summary>
    internal sealed class SupCalQuestReward : NarrativeScenario, ILocalizedModType
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

            Line1 = this.GetLocalization(nameof(Line1), () => "做的不错");
            Line2 = this.GetLocalization(nameof(Line2), () => "这把弩挺适合你的");
            Line3 = this.GetLocalization(nameof(Line3), () => "你帮我解决了一个麻烦");
            Line4 = this.GetLocalization(nameof(Line4), () => "作为奖励，这些就归你了");
            Line5 = this.GetLocalization(nameof(Line5), () => "还有这把刀，我需要你拿着它去干掉那只虫子，放心，报酬会更丰富");
            Line6 = this.GetLocalization(nameof(Line6), () => "我施加了一部分硫火灵异在上面，可以让你轻松一些");
            Line7 = this.GetLocalization(nameof(Line7), () => "......");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("SupCal", "Smile", Line1.Value)
             .Say("SupCal", Line2.Value)
             .Say("SupCal", Line3.Value)
             .SayReward("SupCal", "CloseEye", Line4.Value, CWRID.Item_AshesofAnnihilation, stack: 199, title: string.Empty)
             .SayReward("SupCal", "CloseEye", Line5.Value, ModContent.ItemType<Heartcarver>(), title: string.Empty, anchorYOffset: -60f)
             .Say("SupCal", Line6.Value);

            if (HasHalibut()) {
                n.Say("Helen", "Solemn", Line7.Value);
            }
        }

        protected override void OnStarted() => SupCalEffect.IsActive = true;

        protected override void OnCompleted() {
            SupCalEffect.IsActive = false;
            HalibutStorySync.WriteSupCal(
                d => d.SupCalQuestRewardSceneComplete = true,
                d => d.SupCalQuestRewardSceneComplete = true);
        }

        private static bool HasHalibut() {
            try {
                return Main.LocalPlayer.TryGetOverride(out HalibutPlayer halibutPlayer) && halibutPlayer.HasHalubut;
            } catch {
                return false;
            }
        }
    }

    /// <summary>扶柩者击杀亵渎天神追踪</summary>
    internal class PallbearerQuestTracker : BaseDamageTracker
    {
        internal const float REQUIRED_CONTRIBUTION = 0.8f;

        internal override int TargetNPCType => CWRID.NPC_Providence;

        internal override int[] TargetWeaponTypes => [ModContent.ItemType<Pallbearer>()];

        internal override int[] TargetProjectileTypes => [
            ModContent.ProjectileType<PallbearerHeld>(),
            ModContent.ProjectileType<PallbearerArrow>(),
            ModContent.ProjectileType<PallbearerBoomerang>(),
            ModContent.ProjectileType<PallbearerCoffinSeal>()
        ];

        internal override float RequiredContribution => REQUIRED_CONTRIBUTION;

        public override bool IsQuestActive(Player player) {
            if (!HalibutStorySync.ReadSupCal(d => d.SupCalQuestAccepted, d => d.SupCalQuestAccepted)) {
                return false;
            }

            if (HalibutStorySync.ReadSupCal(d => d.SupCalQuestDeclined, d => d.SupCalQuestDeclined)) {
                return false;
            }

            if (HalibutStorySync.ReadSupCal(d => d.SupCalQuestReward, d => d.SupCalQuestReward)) {
                return false;
            }

            return true;
        }

        public override void OnQuestCompleted(Player player, float contribution) {
            HalibutStorySync.WriteSupCal(
                d => d.SupCalQuestReward = true,
                d => d.SupCalQuestReward = true);
            SupCalQuestRewardTracker.NotifyPallbearerComplete();
        }
    }
}
