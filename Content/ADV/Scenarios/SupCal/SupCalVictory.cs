using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal
{
    /// <summary>
    /// 选择迎战并击败至尊灾厄后的场景
    /// </summary>
    internal class SupCalVictory : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(SupCalVictory);
        public string LocalizationCategory => "ADV";

        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;

        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }

        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }
        public static LocalizedText Line8 { get; private set; }

        private const string expressionShock = " ";
        private const string expressionCloseEye = " " + " ";

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "硫火女巫");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "比目鱼");

            Line1 = this.GetLocalization(nameof(Line1), () => "什么......怎么可能!");
            Line2 = this.GetLocalization(nameof(Line2), () => "你竟然......打败了我?!");
            Line3 = this.GetLocalization(nameof(Line3), () => "不可能，这绝不可能......");
            Line4 = this.GetLocalization(nameof(Line4), () => "我可是已经超越了泰拉人的极限......");
            Line5 = this.GetLocalization(nameof(Line5), () => "......看来我确实小看你了");
            Line6 = this.GetLocalization(nameof(Line6), () => "但这不代表结束，我会回来的");
            Line7 = this.GetLocalization(nameof(Line7), () => "下次......下次我不会再大意了！");
            Line8 = this.GetLocalization(nameof(Line8), () => "......她逃走了");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalsADV[0]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename1.Value + expressionShock, ADVAsset.SupCalsADV[2]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + expressionShock, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename1.Value + expressionCloseEye, ADVAsset.SupCalsADV[4]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + expressionCloseEye, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.HelenADV);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: false);

            Add(Rolename1.Value + expressionShock, Line1.Value);
            Add(Rolename1.Value + expressionShock, Line2.Value);
            Add(Rolename1.Value, Line3.Value);
            Add(Rolename1.Value, Line4.Value);
            Add(Rolename1.Value + expressionCloseEye, Line5.Value);
            Add(Rolename1.Value, Line6.Value);

            bool hasHalibut = false;
            try {
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    hasHalibut = halibutPlayer.HasHalubut;
                }
            } catch {
                hasHalibut = false;
            }

            if (hasHalibut) {
                Add(Rolename1.Value, Line7.Value);
                Add(Rolename2.Value, Line8.Value, onComplete: GiveReward);
            }
            else {
                Add(Rolename1.Value, Line7.Value, onComplete: GiveReward);
            }
        }

        private static void GiveReward() {
            //给予奖励
            ADVRewardPopup.ShowReward(CWRID.Item_AshesofCalamity, 999, "",
                appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                anchorProvider: () => {
                    var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                    if (rect == Rectangle.Empty) {
                        return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                    }
                    return new Vector2(rect.Center.X, rect.Y - 70f);
                }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.Brimstone);
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (save.SupCalDefeat) {
                return;
            }

            if (!save.SupCalChoseToFight) {
                return;//玩家没有选择战斗
            }

            if (NPC.downedMoonlord) {
                return;//月球领主后不会触发
            }

            if (!SupCalVictoryNPC.Spawned) {
                return;
            }

            if (--SupCalVictoryNPC.RandomTimer > 0) {
                return;
            }

            if (ScenarioManager.Start<SupCalVictory>()) {
                save.SupCalDefeat = true;
                SupCalVictoryNPC.Spawned = false;
            }
        }
    }

    internal class SupCalVictoryNPC : DeathTrackingNPC, IWorldInfo
    {
        public static bool Spawned = false;
        public static int RandomTimer;

        void IWorldInfo.OnWorldLoad() {
            Spawned = false;
            RandomTimer = 0;
        }

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == CWRID.NPC_SupremeCalamitas;

        public override void OnKill(NPC npc) {
            if (FirstMetSupCal.ThisIsToFight && npc.type == CWRID.NPC_SupremeCalamitas) {
                Player player = Main.LocalPlayer;
                if (player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    if (halibutPlayer.ADCSave.SupCalChoseToFight) {
                        Spawned = true;
                        RandomTimer = 60 * Main.rand.Next(2, 4);
                    }
                }
                FirstMetSupCal.ThisIsToFight = false;//战斗结束
            }
        }
    }
}
