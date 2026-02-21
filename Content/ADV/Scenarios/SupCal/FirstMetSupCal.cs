using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal
{
    internal class FirstMetSupCal : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        /// <summary>
        /// 玩家是否选择了战斗，并且正在进入战斗场景
        /// </summary>
        public static bool ThisIsToFight;
        //角色名称本地化
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }
        public static LocalizedText Rolename3 { get; private set; }

        //对话文本本地化(有比目鱼版本)
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }
        public static LocalizedText Line8 { get; private set; }
        public static LocalizedText Line9 { get; private set; }
        public static LocalizedText Line10 { get; private set; }

        //无比目鱼版本文本
        public static LocalizedText NoFishLine1 { get; private set; }
        public static LocalizedText NoFishLine2 { get; private set; }
        public static LocalizedText NoFishLine3 { get; private set; }
        public static LocalizedText NoFishLine4 { get; private set; }
        public static LocalizedText NoFishLine5 { get; private set; }
        public static LocalizedText NoFishLine6 { get; private set; }

        public static LocalizedText QuestionLine { get; private set; }
        public static LocalizedText Choice1Text { get; private set; }
        public static LocalizedText Choice2Text { get; private set; }
        public static LocalizedText Choice1Response { get; private set; }
        public static LocalizedText Choice2Response { get; private set; }

        //设置场景默认使用硫磺火风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;

        void IWorldInfo.OnWorldLoad() {
            ThisIsToFight = false;
        }

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "???");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "硫火女巫");
            Rolename3 = this.GetLocalization(nameof(Rolename3), () => "比目鱼");

            //有比目鱼版本
            Line1 = this.GetLocalization(nameof(Line1), () => "没想到你这么快就杀掉了我的'妹妹'");
            Line2 = this.GetLocalization(nameof(Line2), () => "你的成长速度确实有些快了");
            Line3 = this.GetLocalization(nameof(Line3), () => "你是......我对你有印象");
            Line4 = this.GetLocalization(nameof(Line4), () => "你是那个焚烧掉了一半海域的女巫");
            Line5 = this.GetLocalization(nameof(Line5), () => "哈?!呵呵，竟然有人...或者鱼认得我，你们倒也算有趣");
            Line6 = this.GetLocalization(nameof(Line6), () => "......你，为什么还活着?我记得你在上世纪就已经死了");
            Line7 = this.GetLocalization(nameof(Line7), () => "呵呵呵，连这事都有听说过吗?和你这条有趣的鱼解释一下也无妨，" +
            "我的意识早已经熔铸进硫磺火中，这具躯体只不过是被火焰操纵的尸体");
            Line8 = this.GetLocalization(nameof(Line8), () => "......活人的意识，非人的躯体，依靠媒介行走世间，你成为了异类?!");
            Line9 = this.GetLocalization(nameof(Line9), () => "你的层次太低，理解不了我现在的状态");
            Line10 = this.GetLocalization(nameof(Line10), () => "况且我来这里也不是为了这事儿的......");

            //无比目鱼版本
            NoFishLine1 = this.GetLocalization(nameof(NoFishLine1), () => "没想到你这么快就杀掉了我的'妹妹'，独自一人来的？");
            NoFishLine2 = this.GetLocalization(nameof(NoFishLine2), () => "你的成长速度比我预期的要快");
            NoFishLine3 = this.GetLocalization(nameof(NoFishLine3), () => "看来你已经开始触碰那些不该知道的东西");
            NoFishLine4 = this.GetLocalization(nameof(NoFishLine4), () => "我的意识早已熔铸进硫磺火，这具躯体只是被火焰驱动的尸体");
            NoFishLine5 = this.GetLocalization(nameof(NoFishLine5), () => "不过，你的层次太低，无法理解我现在的状态");
            NoFishLine6 = this.GetLocalization(nameof(NoFishLine6), () => "我现身也不是为了解释这些的");

            QuestionLine = this.GetLocalization(nameof(QuestionLine), () => "那么，你的选择是？");
            Choice1Text = this.GetLocalization(nameof(Choice1Text), () => "(拔出武器)");
            Choice2Text = this.GetLocalization(nameof(Choice2Text), () => "(保持沉默)");
            Choice1Response = this.GetLocalization(nameof(Choice1Response), () => "那么便让我来称量称量你吧");
            Choice2Response = this.GetLocalization(nameof(Choice2Response), () => "......真是杂鱼，那么给你一个见面礼，我们下次见");
        }

        protected override void OnScenarioStart() {
            //开始生成粒子
            SupCalEffect.IsActive = true;
        }

        //他妈的我最开始设计的时候为什么没考虑到一个角色多种表情的问题，结果现在只能用这种丑陋的方式来实现
        //你麻痹的为什么要把角色名字和头像强绑定，现在改又不敢改，妈的被自己的设计坑死了
        private const string expressionCloseEye = " ";
        private const string expressionBeTo = " " + " ";
        private const string expressionDespise = " " + " " + " ";

        //比目鱼的表情常量
        private const string helenAmazed = " ";
        private const string helenSolemn = " " + " ";

        protected override void Build() {
            //注册立绘
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalsADV[4]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: true);

            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.SupCalsADV[0]);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename2.Value + expressionCloseEye, ADVAsset.SupCalsADV[4]);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value + expressionCloseEye, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename2.Value + expressionBeTo, ADVAsset.SupCalsADV[3]);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value + expressionBeTo, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename2.Value + expressionDespise, ADVAsset.SupCalsADV[5]);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value + expressionDespise, silhouette: false);

            bool hasHalibut = false;
            try {
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    hasHalibut = halibutPlayer.HasHalubut;
                }
            } catch {
                hasHalibut = false;
            }

            if (hasHalibut) {
                //注册比目鱼的不同表情
                DialogueBoxBase.RegisterPortrait(Rolename3.Value, ADVAsset.HelenADV);
                DialogueBoxBase.SetPortraitStyle(Rolename3.Value, silhouette: false);

                DialogueBoxBase.RegisterPortrait(Rolename3.Value + helenAmazed, ADVAsset.Helen_amazeADV);
                DialogueBoxBase.SetPortraitStyle(Rolename3.Value + helenAmazed, silhouette: false);

                DialogueBoxBase.RegisterPortrait(Rolename3.Value + helenSolemn, ADVAsset.Helen_solemnADV);
                DialogueBoxBase.SetPortraitStyle(Rolename3.Value + helenSolemn, silhouette: false);

                //添加对话（使用本地化文本）
                Add(Rolename1.Value, Line1.Value);
                Add(Rolename1.Value, Line2.Value);
                Add(Rolename3.Value + helenSolemn, Line3.Value); //严肃表情，认出对方
                Add(Rolename3.Value + helenAmazed, Line4.Value); //惊讶表情，说出对方身份
                Add(Rolename2.Value, Line5.Value);
                Add(Rolename3.Value + helenAmazed, Line6.Value); //惊讶表情，质疑为何还活着
                Add(Rolename2.Value + expressionCloseEye, Line7.Value);
                Add(Rolename3.Value + helenAmazed, Line8.Value); //惊讶表情，震惊于异类状态
                Add(Rolename2.Value + expressionCloseEye, Line9.Value);
                Add(Rolename2.Value + expressionBeTo, Line10.Value);
            }
            else {
                //无比目鱼版本添加对话
                Add(Rolename1.Value, NoFishLine1.Value);
                Add(Rolename1.Value, NoFishLine2.Value);
                Add(Rolename2.Value + expressionCloseEye, NoFishLine3.Value);
                Add(Rolename2.Value + expressionCloseEye, NoFishLine4.Value);
                Add(Rolename2.Value + expressionBeTo, NoFishLine5.Value);
                Add(Rolename2.Value + expressionBeTo, NoFishLine6.Value);
            }

            //统一的选择部分（使用硫磺火风格的选项框）
            AddWithChoices(Rolename2.Value + expressionBeTo, QuestionLine.Value, [
                new Choice(Choice1Text.Value, Choice1),
                new Choice(Choice2Text.Value, Choice2),
            ], onStart: null, styleOverride: () => BrimstoneDialogueBox.Instance, choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Brimstone);
        }

        public void Choice1() {
            ScenarioManager.Start<FirstMetSupCal_Choice1>();
            Complete();
        }

        private class FirstMetSupCal_Choice1 : ADVScenarioBase
        {
            public override string Key => nameof(FirstMetSupCal_Choice1);
            //设置场景默认使用硫磺火风格
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;
            protected override void Build() => Add(Rolename2.Value, Choice1Response.Value);
            protected override void OnScenarioComplete() {
                //确保至尊灾厄不存在，才进行召唤
                if (!NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas)) {
                    CWRRef.SummonSupCal(Main.LocalPlayer.Center);
                }

                //标记玩家选择了战斗
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    halibutPlayer.ADVSave.SupCalChoseToFight = true;
                }

                ThisIsToFight = true;
                //停止粒子生成
                SupCalEffect.IsActive = false;
            }
        }

        public void Choice2() {
            ScenarioManager.Start<FirstMetSupCal_Choice2>();
            Complete();
        }

        private class FirstMetSupCal_Choice2 : ADVScenarioBase
        {
            public override string Key => nameof(FirstMetSupCal_Choice2);
            //设置场景默认使用硫磺火风格
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;
            protected override void Build() => Add(Rolename2.Value + expressionDespise, Choice2Response.Value);
            protected override void OnScenarioStart() {
                ADVRewardPopup.ShowReward(CWRID.Item_AshesofCalamity, 999, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero
                    , styleProvider: () => ADVRewardPopup.RewardStyle.Brimstone); //使用硫磺火风格
            }
            protected override void OnScenarioComplete() {
                //停止粒子生成
                SupCalEffect.IsActive = false;
            }
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (save.FirstMetSupCal) {
                return;
            }
            if (InWorldBossPhase.Downed30.Invoke()) {
                return;//如果已经打过至尊灾厄，则不触发
            }
            if (halibutPlayer.HeldHalibut && !save.CalamitasCloneGift) {//如果玩家拿着大比目鱼，则必须先获得过比目鱼小姐给的灾厄克隆的礼物才能触发，避免这两个场景冲突
                return;
            }
            if (!FirstMetSupCalNPC.Spawned) {
                return;
            }
            if (NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas)) {
                FirstMetSupCalNPC.Spawned = false;//如果至尊灾厄已经存在，则重置状态，避免重复触发
                return;
            }
            if (--FirstMetSupCalNPC.RandomTimer > 0) {
                return;
            }
            if (ScenarioManager.Start<FirstMetSupCal>()) {
                save.FirstMetSupCal = true;
                FirstMetSupCalNPC.Spawned = false;
            }
        }
    }

    internal class FirstMetSupCalNPC : DeathTrackingNPC, IWorldInfo
    {
        public static bool Spawned = false;
        public static int RandomTimer;
        void IWorldInfo.OnWorldLoad() {
            Spawned = false;
            RandomTimer = 0;
        }
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == CWRID.NPC_CalamitasClone;//应用于目标NPC
        public override void OnNPCDeath(NPC npc) {
            if (npc.type == CWRID.NPC_CalamitasClone && !CWRWorld.BossRush) {
                Spawned = true;
                RandomTimer = 60 * Main.rand.Next(3, 5);//给一个3到5秒的缓冲时间，打完立刻触发不太合适
            }
        }
    }
}
