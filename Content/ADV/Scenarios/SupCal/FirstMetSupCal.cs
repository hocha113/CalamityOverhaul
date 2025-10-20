using CalamityMod.Items.Materials;
using CalamityMod.NPCs.CalClone;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.Projectiles.Boss;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal
{
    internal class FirstMetSupCal : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(FirstMetSupCal);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        /// <summary>
        /// 玩家是否选择了战斗，并且正在进入战斗场景
        /// </summary>
        public static bool ThisIsToFight;
        //角色名称本地化
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }
        public static LocalizedText Rolename3 { get; private set; }

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
        public static LocalizedText Line10 { get; private set; }
        public static LocalizedText QuestionLine { get; private set; }
        public static LocalizedText Choice1Text { get; private set; }
        public static LocalizedText Choice2Text { get; private set; }
        public static LocalizedText Choice1Response { get; private set; }
        public static LocalizedText Choice2Response { get; private set; }

        //设置场景默认使用硫磺火风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "???");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "至尊灾厄");
            Rolename3 = this.GetLocalization(nameof(Rolename3), () => "比目鱼");

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
            QuestionLine = this.GetLocalization(nameof(QuestionLine), () => "那么，你的选择是？");
            Choice1Text = this.GetLocalization(nameof(Choice1Text), () => "(拔出武器)");
            Choice2Text = this.GetLocalization(nameof(Choice2Text), () => "(保持沉默)");
            Choice1Response = this.GetLocalization(nameof(Choice1Response), () => "哈，那么便让我来称量称量你吧");
            Choice2Response = this.GetLocalization(nameof(Choice2Response), () => "......真是杂鱼呢，那么给你一个见面礼，我们下次见");
        }

        protected override void OnScenarioStart() {
            //开始生成粒子
            SupCalSkyEffect.IsActive = true;
        }

        protected override void OnScenarioComplete() {
            //停止粒子生成
            SupCalSkyEffect.IsActive = false;
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
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalADV[4]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: true);

            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.SupCalADV[0]);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename2.Value + expressionCloseEye, ADVAsset.SupCalADV[4]);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value + expressionCloseEye, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename2.Value + expressionBeTo, ADVAsset.SupCalADV[3]);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value + expressionBeTo, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename2.Value + expressionDespise, ADVAsset.SupCalADV[5]);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value + expressionDespise, silhouette: false);

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
            Add(Rolename3.Value + helenSolemn, Line3.Value); //严肃表情 - 认出对方
            Add(Rolename3.Value + helenSolemn, Line4.Value); //严肃表情 - 说出对方身份
            Add(Rolename2.Value, Line5.Value);
            Add(Rolename3.Value + helenAmazed, Line6.Value); //惊讶表情 - 质疑为何还活着
            Add(Rolename2.Value + expressionCloseEye, Line7.Value);
            Add(Rolename3.Value + helenAmazed, Line8.Value); //惊讶表情 - 震惊于异类状态
            Add(Rolename2.Value + expressionCloseEye, Line9.Value);
            Add(Rolename2.Value + expressionBeTo, Line10.Value);

            AddWithChoices(Rolename2.Value + expressionBeTo, QuestionLine.Value, [
                new(Choice1Text.Value, () => {
                    //选择后继续对话
                    Add(Rolename2.Value, Choice1Response.Value, styleOverride: DefaultDialogueStyle);
                    //继续推进场景
                    DialogueUIRegistry.Current?.EnqueueDialogue(
                        Rolename2.Value,
                        Choice1Response.Value,
                        onFinish: () => Choice1()
                    );
                }),
                new(Choice2Text.Value, () => {
                     //选择后继续对话
                    Add(Rolename2.Value + expressionDespise, Choice2Response.Value, styleOverride: DefaultDialogueStyle);
                    //继续推进场景
                    DialogueUIRegistry.Current?.EnqueueDialogue(
                        Rolename2.Value + expressionDespise,
                        Choice2Response.Value,
                        onFinish: () => Choice2()
                    );
                }),
            ]);
        }

        public void Choice1() {
            Vector2 spawnPos = Main.LocalPlayer.Center;
            SoundEngine.PlaySound(SCalAltar.SummonSound, spawnPos);
            Projectile.NewProjectile(new EntitySource_WorldEvent(), spawnPos, Vector2.Zero
                , ModContent.ProjectileType<SCalRitualDrama>(), 0, 0f, Main.myPlayer, 0, 0);

            //标记玩家选择了战斗
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.ADCSave.SupCalChoseToFight = true;
            }

            ThisIsToFight = true;
            Complete();
        }

        public void Choice2() {
            ADVRewardPopup.ShowReward(ModContent.ItemType<AshesofCalamity>(), 999, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            Complete();
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
            if (NPC.AnyNPCs(ModContent.NPCType<SupremeCalamitas>())) {
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

    internal class FirstMetSupCalNPC : GlobalNPC
    {
        public static bool Spawned = false;
        public static int RandomTimer;
        public override bool SpecialOnKill(NPC npc) {
            if (npc.type == ModContent.NPCType<CalamitasClone>()) {
                Spawned = true;
                RandomTimer = 60 * Main.rand.Next(3, 5);//给一个3到5秒的缓冲时间，打完立刻触发不太合适
            }
            return false;
        }
    }
}
