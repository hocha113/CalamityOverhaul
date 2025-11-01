using CalamityMod.NPCs.DevourerofGods;
using CalamityOverhaul.Content.ADV.Scenarios.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.UIHandles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest.DoGQuest
{
    /// <summary>
    /// 神明吞噬者任务奖励场景
    /// </summary>
    internal class SupCalDoGQuestReward : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(SupCalDoGQuestReward);
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

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "至尊灾厄");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "比目鱼");

            Line1 = this.GetLocalization(nameof(Line1), () => "干净利落");
            Line2 = this.GetLocalization(nameof(Line2), () => "这把刀，一如既往地令人满意");
            Line3 = this.GetLocalization(nameof(Line3), () => "当年我还是凡人之躯时，就是用它亲手挖出老师的心脏，很好用，不是吗？");
            Line4 = this.GetLocalization(nameof(Line4), () => "拿好");
            Line5 = this.GetLocalization(nameof(Line5), () => "你有没有想过，如果下一次，我是委托你来杀我，你会怎么做？");
            Line6 = this.GetLocalization(nameof(Line6), () => "真遗憾，你和他注定见不了面。不然你们一定聊得很投机");
            Line7 = this.GetLocalization(nameof(Line7), () => "......我越来越受不了这家伙了");
        }

        protected override void OnScenarioStart() {
            SupCalSkyEffect.IsActive = true;
        }

        protected override void OnScenarioComplete() {
            SupCalSkyEffect.IsActive = false;
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalADV[0]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: false);
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

            Add(Rolename1.Value, Line1.Value);
            Add(Rolename1.Value, Line2.Value);
            Add(Rolename1.Value, Line3.Value);
            Add(Rolename1.Value, Line4.Value); //奖励
            Add(Rolename1.Value, Line5.Value);
            Add(Rolename1.Value, Line6.Value);

            if (hasHalibut) {
                Add(Rolename2.Value, Line7.Value);
            }
        }

        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 3) { //Line4时发放奖励
                ADVRewardPopup.ShowReward(ModContent.ItemType<OniMachete>(), 1, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.Brimstone);
            }
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (!save.SupCalDoGQuestReward) {
                return;
            }
            if (save.SupCalDoGQuestRewardSceneComplete) {
                return;
            }
            if (!Spawned) {
                return;
            }
            if (--RandomTimer > 0) {
                return;
            }
            if (ScenarioManager.Start<SupCalDoGQuestReward>()) {
                save.SupCalDoGQuestRewardSceneComplete = true;
                Spawned = false;
            }
        }
    }

    /// <summary>
    /// 追踪玩家使用Heartcarver击杀神明吞噬者
    /// </summary>
    internal class DoGQuestTracker : BaseDamageTracker
    {
        private const float REQUIRED_CONTRIBUTION = 0.8f; //80%伤害贡献度要求

        internal override int TargetNPCType => ModContent.NPCType<DevourerofGodsHead>();

        internal override HashSet<int> OtherNPCType => [ModContent.NPCType<DevourerofGodsBody>(), ModContent.NPCType<DevourerofGodsTail>()];

        internal override int[] TargetWeaponTypes => new[] { ModContent.ItemType<Heartcarver>() };

        internal override int[] TargetProjectileTypes => [
            ModContent.ProjectileType<HeartcarverHeld>(),
            ModContent.ProjectileType<HeartcarverDash>(),
            ModContent.ProjectileType<HeartcarverDagger>()
        ];

        internal override float RequiredContribution => REQUIRED_CONTRIBUTION;

        public override void OnQuestCompleted(Player player, float contribution) {
            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }

            if (!halibutPlayer.ADCSave.SupCalQuestReward//先完成前置任务
                || halibutPlayer.ADCSave.SupCalDoGQuestDeclined//且未拒绝当前任务
                ) {
                return;
            }

            //标记任务完成
            halibutPlayer.ADCSave.SupCalDoGQuestReward = true;

            //延迟触发奖励场景
            SupCalDoGQuestReward.Spawned = true;
            SupCalDoGQuestReward.RandomTimer = 60 * Main.rand.Next(3, 5);
        }
    }

    internal class DoGQuestTrackerUI : BaseQuestTrackerUI
    {
        public override string LocalizationCategory => "UI";
        public static DoGQuestTrackerUI Instance => UIHandleLoader.GetUIHandleOfType<DoGQuestTrackerUI>();

        public override bool CanOpne {
            get {
                if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    return false;
                }

                //检查任务是否激活
                if (!halibutPlayer.ADCSave.SupCalQuestReward//先完成前置任务
                || halibutPlayer.ADCSave.SupCalDoGQuestDeclined//且未拒绝当前任务
                ) {
                    return false;
                }

                //检查是否已完成
                if (halibutPlayer.ADCSave.SupCalDoGQuestReward) {
                    return false;
                }

                //获取战斗状态
                return BaseDamageTracker.GetDamageTrackingData().isActive;
            }
        }

        protected override void SetupLocalizedTexts() {
            QuestTitle = this.GetLocalization(nameof(QuestTitle), () => "委托：猎杀神明吞噬者");
            DamageContribution = this.GetLocalization(nameof(DamageContribution), () => "刻心者伤害");
            RequiredContribution = this.GetLocalization(nameof(RequiredContribution), () => "需求: 85%");
        }

        protected override (float current, float total, bool isActive) GetTrackingData() {
            return BaseDamageTracker.GetDamageTrackingData();
        }

        protected override float GetRequiredContribution() {
            return 0.85f; //85%
        }
    }
}
