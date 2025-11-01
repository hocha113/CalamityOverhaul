using CalamityMod.Items.Materials;
using CalamityMod.NPCs.Providence;
using CalamityOverhaul.Content.ADV.Scenarios.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest.PallbearerQuest
{
    /// <summary>
    /// 完成扶柩者任务后的奖励场景
    /// </summary>
    internal class SupCalQuestReward : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(SupCalQuestReward);
        public string LocalizationCategory => "Legend.HalibutText.ADV";

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
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "至尊灾厄");
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
            SupCalSkyEffect.IsActive = true;
        }

        protected override void OnScenarioComplete() {
            SupCalSkyEffect.IsActive = false;
        }

        protected override void Build() {
            //注册立绘
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalADV[0]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename1.Value + expressionCloseEye, ADVAsset.SupCalADV[4]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + expressionCloseEye, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename1.Value + expressionSmile, ADVAsset.SupCalADV[1]);
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
            Add(Rolename1.Value + expressionSmile, Line4.Value);//奖励
            Add(Rolename1.Value + expressionCloseEye, Line5.Value);
            Add(Rolename1.Value, Line6.Value);
            if (hasHalibut) {
                Add(Rolename2.Value, Line7.Value);
            }
        }

        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 3) {//在Line4时发放奖励
                ADVRewardPopup.ShowReward(ModContent.ItemType<AshesofAnnihilation>(), 199, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.Brimstone);
            }
            if (args.Index == 4) {//在Line5时发放新的任务武器
                ADVRewardPopup.ShowReward(ModContent.ItemType<Heartcarver>(), 1, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f - 60);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f - 60);//高一点点
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.Brimstone);
            }
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (!save.SupCalQuestReward) {
                return;
            }

            if (save.SupCalQuestRewardSceneComplete) {
                return;
            }

            if (!Spawned) {
                return;
            }

            if (--RandomTimer > 0) {
                return;
            }

            if (ScenarioManager.Start<SupCalQuestReward>()) {
                save.SupCalQuestRewardSceneComplete = true;
                Spawned = false;
            }
        }
    }

    /// <summary>
    /// 追踪玩家使用扶柩者击杀亵渎天神
    /// </summary>
    internal class PallbearerQuestTracker : BaseDamageTracker
    {
        private const float REQUIRED_CONTRIBUTION = 0.8f; //80%伤害贡献度要求

        internal override int TargetNPCType => ModContent.NPCType<Providence>();

        internal override int[] TargetWeaponTypes => new[] { ModContent.ItemType<Pallbearer>() };

        internal override int[] TargetProjectileTypes => new[] {
            ModContent.ProjectileType<PallbearerHeld>(),
            ModContent.ProjectileType<PallbearerArrow>(),
            ModContent.ProjectileType<PallbearerBoomerang>()
        };

        internal override float RequiredContribution => REQUIRED_CONTRIBUTION;

        public override void OnQuestCompleted(Player player, float contribution) {
            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }

            //检查是否接受了任务
            if (!halibutPlayer.ADCSave.SupCalQuestAccepted || halibutPlayer.ADCSave.SupCalQuestDeclined) {
                return;
            }

            //标记任务完成
            halibutPlayer.ADCSave.SupCalQuestReward = true;

            //延迟触发奖励场景
            SupCalQuestReward.Spawned = true;
            SupCalQuestReward.RandomTimer = 60 * Main.rand.Next(3, 5);
        }
    }

    /// <summary>
    /// 扶柩者任务追踪UI，显示伤害贡献度
    /// </summary>
    internal class PallbearerQuestTrackerUI : BaseQuestTrackerUI
    {
        public override string LocalizationCategory => "UI";
        public static PallbearerQuestTrackerUI Instance => UIHandleLoader.GetUIHandleOfType<PallbearerQuestTrackerUI>();

        public override bool CanOpne {
            get {
                if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    return false;
                }

                //检查任务是否激活
                if (!halibutPlayer.ADCSave.SupCalQuestAccepted || halibutPlayer.ADCSave.SupCalQuestDeclined) {
                    return false;
                }

                //检查是否已完成
                if (halibutPlayer.ADCSave.SupCalQuestReward) {
                    return false;
                }

                //获取战斗状态
                return BaseDamageTracker.GetDamageTrackingData().isActive;
            }
        }

        protected override void SetupLocalizedTexts() {
            QuestTitle = this.GetLocalization(nameof(QuestTitle), () => "委托：猎杀亵渎天神");
            DamageContribution = this.GetLocalization(nameof(DamageContribution), () => "扶柩者伤害");
            RequiredContribution = this.GetLocalization(nameof(RequiredContribution), () => "需求: 80%");
        }

        protected override (float current, float total, bool isActive) GetTrackingData() {
            return BaseDamageTracker.GetDamageTrackingData();
        }

        protected override float GetRequiredContribution() {
            return 0.8f; //80%
        }
    }
}
