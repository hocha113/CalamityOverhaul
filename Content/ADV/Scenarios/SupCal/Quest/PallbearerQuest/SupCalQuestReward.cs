using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest.PallbearerQuest
{
    /// <summary>
    /// 完成扶柩者任务后的奖励场景
    /// </summary>
    internal class SupCalQuestReward : ADVScenarioBase, ILocalizedModType
    {
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;

        public static bool Spawned = false;
        public static int RandomTimer;

        //角色名称本地化
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }

        //对话文本本地化
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }

        private const string expressionCloseEye = " ";
        private const string expressionSmile = " " + " ";

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "硫火女巫");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "比目鱼");

            Line1 = this.GetLocalization(nameof(Line1), () => "做的不错");
            Line2 = this.GetLocalization(nameof(Line2), () => "这把弩挺适合你的");
            Line3 = this.GetLocalization(nameof(Line3), () => "你帮我解决了一个麻烦");
            Line4 = this.GetLocalization(nameof(Line4), () => "作为奖励，这些就归你了");
            Line5 = this.GetLocalization(nameof(Line5), () => "还有这把刀，我需要你拿着它去干掉那只虫子，放心，报酬会更丰富");
            Line6 = this.GetLocalization(nameof(Line6), () => "我施加了一部分硫火灵异在上面，可以让你轻松一些");
            Line7 = this.GetLocalization(nameof(Line7), () => "......");
        }

        protected override void OnScenarioStart() {
            SupCalEffect.IsActive = true;
        }

        protected override void OnScenarioComplete() {
            SupCalEffect.IsActive = false;
        }

        protected override void Build() {
            //注册立绘
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalsADV[0]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename1.Value + expressionCloseEye, ADVAsset.SupCalsADV[4]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + expressionCloseEye, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename1.Value + expressionSmile, ADVAsset.SupCalsADV[1]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + expressionSmile, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.Helen_solemnADV);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: false);

            bool hasHalibut = false;
            try {
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    hasHalibut = halibutPlayer.HasHalubut;
                }
            } catch {
                hasHalibut = false;
            }

            //添加对话
            Add(Rolename1.Value + expressionSmile, Line1.Value);
            Add(Rolename1.Value, Line2.Value);
            Add(Rolename1.Value, Line3.Value);
            Add(Rolename1.Value + expressionCloseEye, Line4.Value, onStart: ShowPanelReward(CWRID.Item_AshesofAnnihilation, 199, "", ADVRewardPopup.RewardStyle.Brimstone));//奖励
            Add(Rolename1.Value + expressionCloseEye, Line5.Value, onStart: ShowPanelReward(ModContent.ItemType<Heartcarver>(), 1, "", ADVRewardPopup.RewardStyle.Brimstone, -60f));
            Add(Rolename1.Value, Line6.Value);
            if (hasHalibut) {
                Add(Rolename2.Value, Line7.Value);
            }
        }

        public override void Update(ADVSave save, Player player) {
            if (!save.Get<SupCalADVData>().SupCalQuestReward) {
                return;
            }

            if (save.Get<SupCalADVData>().SupCalQuestRewardSceneComplete) {
                return;
            }

            if (!Spawned) {
                return;
            }

            if (--RandomTimer > 0) {
                return;
            }

            if (ScenarioManager.Start<SupCalQuestReward>()) {
                save.Get<SupCalADVData>().SupCalQuestRewardSceneComplete = true;
                Spawned = false;
            }
        }
    }

    /// <summary>
    /// 追踪玩家使用扶柩者击杀亵渎天神
    /// </summary>
    internal class PallbearerQuestTracker : BaseDamageTracker
    {
        internal const float REQUIRED_CONTRIBUTION = 0.8f; //80%伤害贡献度要求

        internal override int TargetNPCType => CWRID.NPC_Providence;

        internal override int[] TargetWeaponTypes => new[] { ModContent.ItemType<Pallbearer>() };

        internal override int[] TargetProjectileTypes => new[] {
            ModContent.ProjectileType<PallbearerHeld>(),
            ModContent.ProjectileType<PallbearerArrow>(),
            ModContent.ProjectileType<PallbearerBoomerang>()
        };

        internal override float RequiredContribution => REQUIRED_CONTRIBUTION;

        public override bool IsQuestActive(Player player) {
            if (!player.TryGetADVSave(out var save)) {
                return false;
            }

            //检查是否接受了任务
            if (!save.Get<SupCalADVData>().SupCalQuestAccepted || save.Get<SupCalADVData>().SupCalQuestDeclined) {
                return false;
            }

            if (save.Get<SupCalADVData>().SupCalQuestReward) {
                return false;//任务已经完成
            }

            return true;
        }

        public override void OnQuestCompleted(Player player, float contribution) {
            if (!player.TryGetADVSave(out var save)) {
                return;
            }

            //标记任务完成
            save.Get<SupCalADVData>().SupCalQuestReward = true;

            //延迟触发奖励场景
            SupCalQuestReward.Spawned = true;
            SupCalQuestReward.RandomTimer = 60 * Main.rand.Next(3, 5);
        }
    }
}
