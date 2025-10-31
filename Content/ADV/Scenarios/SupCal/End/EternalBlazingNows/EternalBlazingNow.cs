using CalamityMod.NPCs.SupremeCalamitas;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows
{
    /// <summary>
    /// 永恒燃烧的如今，坏结局场景
    /// </summary>
    internal class EternalBlazingNow : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(EternalBlazingNow);
        public string LocalizationCategory => "Legend.HalibutText.ADV";

        //角色名称
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }

        //对话台词
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
        public static LocalizedText Line11 { get; private set; }
        public static LocalizedText Line12 { get; private set; }
        public static LocalizedText Line13 { get; private set; }
        public static LocalizedText Line14 { get; private set; }
        public static LocalizedText Line15 { get; private set; }

        //选项文本
        public static LocalizedText QuestionLine { get; private set; }
        public static LocalizedText Choice1Text { get; private set; }
        public static LocalizedText Choice2Text { get; private set; }

        //选择1后的对话
        public static LocalizedText Choice1Line1 { get; private set; }
        public static LocalizedText Choice1Line2 { get; private set; }

        //设置场景默认使用硫磺火风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;

        //比目鱼表情常量
        private const string helenShock = " ";
        private const string helenSolemn = " " + " ";

        //至尊灾厄表情常量
        private const string supCalDespise = " ";

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "比目鱼");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "???硫火女巫???");

            Line1 = this.GetLocalization(nameof(Line1), () => "开什么玩笑？！这些火是从那里冒出来的");
            Line2 = this.GetLocalization(nameof(Line2), () => "过去身无法调动，过去正在被封锁");
            Line3 = this.GetLocalization(nameof(Line3), () => "这火是从过去燃烧到现在的，那个女巫，她没死，但为什么......");
            Line4 = this.GetLocalization(nameof(Line4), () => "明明刚才的战斗中，她一直没有表现出入侵过去的能力，现在'死后'却出现了这种情况");
            Line5 = this.GetLocalization(nameof(Line5), () => "我是不会死的，不过也差不多了");
            Line6 = this.GetLocalization(nameof(Line6), () => "你们很不错，或许真的有能力终结这个绝望的时代");
            Line7 = this.GetLocalization(nameof(Line7), () => "所以我想最后拜托你们一件事");
            Line8 = this.GetLocalization(nameof(Line8), () => "只要这世间的过去与现今还存在一缕硫磺火，'我'就不会消亡");
            Line9 = this.GetLocalization(nameof(Line9), () => "但我的意识在沉沉浮浮的火海中终将被消磨殆尽");
            Line10 = this.GetLocalization(nameof(Line10), () => "我有预感，如果没有遇到你们，我最多只能再撑30年");
            Line11 = this.GetLocalization(nameof(Line11), () => "......你希望他代替你的意识？");
            Line12 = this.GetLocalization(nameof(Line12), () => "没错，这是必须的");
            Line13 = this.GetLocalization(nameof(Line13), () => "当我意识完全消散后，我身上近乎完整的硫磺火将会复苏，彻底焚烧掉整个世界");
            Line14 = this.GetLocalization(nameof(Line14), () => "况且，如果你们想终结这个时代，凡人之躯太过弱小，以我的躯体作为踏板，不更好吗？");
            Line15 = this.GetLocalization(nameof(Line15), () => "我绝对不允许！让他变成你这副鬼样子？！先从我的尸体上跨过去吧！");

            QuestionLine = this.GetLocalization(nameof(QuestionLine), () => "......");
            Choice1Text = this.GetLocalization(nameof(Choice1Text), () => "(阻止比目鱼拼命)");
            Choice2Text = this.GetLocalization(nameof(Choice2Text), () => "(保持沉默)");

            Choice1Line1 = this.GetLocalization(nameof(Choice1Line1), () => "......这就是你的选择吗？");
            Choice1Line2 = this.GetLocalization(nameof(Choice1Line2), () => "你在这条路上无论走多远，为此要变成什么样子，我都会陪着你");
        }

        protected override void OnScenarioStart() {
            //开始生成粒子效果
            EbnSkyEffect.IsActive = true;
        }

        protected override void OnScenarioComplete() {
            //停止粒子生成
            EbnSkyEffect.IsActive = false;
        }

        protected override void Build() {
            //注册比目鱼立绘
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.HelenADV);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename1.Value + helenShock, ADVAsset.Helen_amazeADV);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + helenShock, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename1.Value + helenSolemn, ADVAsset.Helen_solemnADV);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + helenSolemn, silhouette: false);

            //注册至尊灾厄立绘（使用剪影效果）
            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.SupCalADV[4]);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: true);

            DialogueBoxBase.RegisterPortrait(Rolename2.Value + supCalDespise, ADVAsset.SupCalADV[3]);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value + supCalDespise, silhouette: true);

            //检查是否拥有比目鱼
            bool hasHalibut = false;
            try {
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    hasHalibut = halibutPlayer.HasHalubut;
                }
            } catch {
                hasHalibut = false;
            }

            if (hasHalibut) {
                //有比目鱼版本对话
                Add(Rolename1.Value + helenShock, Line1.Value);
                Add(Rolename1.Value + helenShock, Line2.Value);
                Add(Rolename2.Value, Line3.Value);
                Add(Rolename2.Value, Line4.Value);
                Add(Rolename2.Value, Line5.Value);
                Add(Rolename1.Value + helenSolemn, Line6.Value);
                Add(Rolename2.Value + supCalDespise, Line7.Value);
                Add(Rolename1.Value + helenShock, Line8.Value);

                //添加选项
                AddWithChoices(Rolename1.Value + helenSolemn, QuestionLine.Value, [
                    new(Choice1Text.Value, () => {
                        //选择阻止比目鱼
                        Add(Rolename1.Value + helenShock, Choice1Line1.Value, styleOverride: DefaultDialogueStyle);
                        Add(Rolename1.Value, Choice1Line2.Value, styleOverride: DefaultDialogueStyle, onComplete: () => Choice1());

                        DialogueUIRegistry.Current?.EnqueueDialogue(
                            Rolename1.Value + helenShock,
                            Choice1Line1.Value,
                            onFinish: () => {
                                DialogueUIRegistry.Current?.EnqueueDialogue(
                                    Rolename1.Value,
                                    Choice1Line2.Value,
                                    onFinish: () => Choice1()
                                );
                            }
                        );
                    }),
                    new(Choice2Text.Value, () => {
                        //选择保持沉默，直接触发演出并结束场景
                        Choice2();
                    }),
                ]);
            }
            else {
                //无比目鱼版本 - 简化对话后直接结束
                Add(Rolename2.Value, Line3.Value);
                Add(Rolename2.Value, Line4.Value);
                Add(Rolename2.Value, Line5.Value, onComplete: () => Complete());
            }
        }

        private void Choice1() {
            //选择1：阻止比目鱼拼命
            //海伦透露真名，承诺陪伴
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.ADCSave.EternalBlazingNowChoice1 = true;
            }

            Complete();
        }

        private void Choice2() {
            //选择2：保持沉默
            //触发演出效果
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.ADCSave.EternalBlazingNowChoice2 = true;
            }

            //播放音效
            SoundEngine.PlaySound(CWRSound.ShurikenOut with { Volume = 0.7f, Pitch = -0.3f });

            //这里可以添加特殊的视觉效果或粒子效果
            //例如：屏幕震动、闪光等

            Complete();
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            //如果已经触发过场景，不再触发
            if (save.EternalBlazingNowTriggered) {
                return;
            }

            //必须击败了至尊灾厄
            if (!InWorldBossPhase.Downed30.Invoke()) {
                return;
            }

            //如果玩家选择了战斗路线（FirstMetSupCal场景），则不触发此坏结局
            if (save.SupCalChoseToFight) {
                return;
            }

            //必须拥有比目鱼
            if (!halibutPlayer.HasHalubut) {
                return;
            }

            //检查是否满足触发条件
            if (!EternalBlazingNowNPC.Spawned) {
                return;
            }

            if (--EternalBlazingNowNPC.RandomTimer > 0) {
                return;
            }

            //触发场景
            if (ScenarioManager.Start<EternalBlazingNow>()) {
                save.EternalBlazingNowTriggered = true;
                EternalBlazingNowNPC.Spawned = false;
            }
        }
    }

    /// <summary>
    /// 用于检测至尊灾厄击败并触发场景的全局NPC类
    /// </summary>
    internal class EternalBlazingNowNPC : GlobalNPC
    {
        public static bool Spawned = false;
        public static int RandomTimer;

        public override void OnKill(NPC npc) {
            //检测至尊灾厄被击败
            if (npc.type == ModContent.NPCType<SupremeCalamitas>()) {
                //检查玩家是否拥有比目鱼且没有选择战斗路线
                Player player = Main.LocalPlayer;
                if (player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    if (halibutPlayer.HasHalubut && !halibutPlayer.ADCSave.SupCalChoseToFight) {
                        Spawned = true;
                        RandomTimer = 60 * Main.rand.Next(3, 5); //3-5秒缓冲时间
                    }
                }
            }
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (!VaultUtils.isClient) {
                return; //仅客户端处理
            }
        }
    }
}
