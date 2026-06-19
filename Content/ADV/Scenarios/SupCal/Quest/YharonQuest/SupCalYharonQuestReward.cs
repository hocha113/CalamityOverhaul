using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using CalamityOverhaul.Content.ADV.Scenarios.Helen.Gifts;
using CalamityOverhaul.Content.Items.Accessories;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest.YharonQuest
{
    /// <summary>
    /// 完成鬼面刀任务后的奖励场景
    /// </summary>
    internal class SupCalYharonQuestReward : ADVScenarioBase, ILocalizedModType
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
        public static LocalizedText Line8 { get; private set; }
        public static LocalizedText Line9 { get; private set; }

        private const string expressionCloseEye = " ";
        private const string expressionSigh = " " + " ";

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "硫火女巫");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "比目鱼");

            Line1 = this.GetLocalization(nameof(Line1), () => "啊......终于结束了");
            Line2 = this.GetLocalization(nameof(Line2), () => "对我来说......那条龙，是极少数值得尊敬的生物");
            Line3 = this.GetLocalization(nameof(Line3), () => "明知自己会死，却仍然选择站在那里......即使借助金源魄来重启，归来的也只是复制体");
            Line4 = this.GetLocalization(nameof(Line4), () => "可惜，它擅长服从，而你擅长......嗯，活下来？");
            Line5 = this.GetLocalization(nameof(Line5), () => "拿着。金源锭。曾被叫做‘炼狱之金’，是凡人所能触碰的力量极限");
            Line6 = this.GetLocalization(nameof(Line6), () => "接下来，轮到我了");
            Line7 = this.GetLocalization(nameof(Line7), () => "你在开什么玩笑......？");
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

            DialogueBoxBase.RegisterPortrait(Rolename1.Value + expressionSigh, ADVAsset.SupCalsADV[5]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + expressionSigh, silhouette: false);

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
            Add(Rolename1.Value + expressionSigh, Line1.Value);
            Add(Rolename1.Value, Line2.Value);
            Add(Rolename1.Value + expressionCloseEye, Line3.Value);
            Add(Rolename1.Value, Line4.Value);
            AddReward(Rolename1.Value + expressionCloseEye, Line5.Value, CWRID.Item_AuricBar, 302, "", ADVRewardPopup.RewardStyle.Brimstone);
            Add(Rolename1.Value, Line6.Value, onStart: ShowPanelReward(ModContent.ItemType<Proverbs>(), 1, "", ADVRewardPopup.RewardStyle.Brimstone, -60f));
            if (hasHalibut) {
                Add(Rolename2.Value, Line7.Value);
            }
        }

        public override void Update(ADVSave save, Player player) {
            if (!save.Get<SupCalADVData>().SupCalYharonQuestReward) {
                return;
            }

            if (save.Get<SupCalADVData>().SupCalYharonQuestRewardSceneComplete) {
                return;
            }

            //如果玩家拿着大比目鱼，则必须先获得过比目鱼小姐给的礼物才能触发，避免这两个场景冲突
            var halibutPlayer = player.GetOverride<HalibutPlayer>();
            if (halibutPlayer.HeldHalibut && !save.Get<BossGiftADVData>().YharonGift) {
                return;
            }

            if (!Spawned) {
                return;
            }

            if (--RandomTimer > 0) {
                return;
            }

            if (ScenarioManager.Start<SupCalYharonQuestReward>()) {
                save.Get<SupCalADVData>().SupCalYharonQuestRewardSceneComplete = true;
                Spawned = false;
            }
        }
    }

    /// <summary>
    /// 追踪玩家使用鬼面刀击杀焚世龙
    /// </summary>
    internal class YharonQuestTracker : BaseDamageTracker
    {
        internal const float REQUIRED_CONTRIBUTION = 0.75f; //75%伤害贡献度要求

        internal override int TargetNPCType => CWRID.NPC_Yharon;

        internal override int[] TargetWeaponTypes => new[] { ModContent.ItemType<OniMachete>() };

        internal override int[] TargetProjectileTypes => new[] {
            ModContent.ProjectileType<OniHandMinion>(),
            ModContent.ProjectileType<OniFireBall>(),
            ModContent.ProjectileType<OniHandExplode>()
        };

        internal override float RequiredContribution => REQUIRED_CONTRIBUTION;

        public override bool IsQuestActive(Player player) {
            if (!player.TryGetADVSave(out var save)) {
                return false;
            }

            //检查是否接受了任务
            if (!save.Get<SupCalADVData>().SupCalYharonQuestAccepted || save.Get<SupCalADVData>().SupCalYharonQuestDeclined) {
                return false;
            }

            //检查是否已完成
            if (save.Get<SupCalADVData>().SupCalYharonQuestReward) {
                return false;
            }

            return true;
        }

        public override void OnQuestCompleted(Player player, float contribution) {
            if (!player.TryGetADVSave(out var save)) {
                return;
            }

            //标记任务完成
            save.Get<SupCalADVData>().SupCalYharonQuestReward = true;

            //延迟触发奖励场景
            SupCalYharonQuestReward.Spawned = true;
            SupCalYharonQuestReward.RandomTimer = 60 * Main.rand.Next(3, 5);
        }
    }
}
