using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using CalamityOverhaul.OtherMods.SubWorld;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using InnoVault.Narrative.Services;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers
{
    internal sealed class DeploySignaltowerScenario : NarrativeScenario, ILocalizedModType
    {
        //登门倒计时,ScenarioTicker世界加载重置
        private static int delayTimer;
        private static bool offeredThisSession;

        public string LocalizationCategory => "ADV.Draedon";
        public static LocalizedText IntroLine1 { get; private set; }
        public static LocalizedText IntroLine2 { get; private set; }
        public static LocalizedText IntroLine3 { get; private set; }
        public static LocalizedText IntroLine4 { get; private set; }
        public static LocalizedText IntroLine5 { get; private set; }
        public static LocalizedText IntroLine6 { get; private set; }
        public static LocalizedText IntroLine7 { get; private set; }
        public static LocalizedText IntroLine8 { get; private set; }
        public static LocalizedText TechExplainLine1 { get; private set; }
        public static LocalizedText TechExplainLine2 { get; private set; }
        public static LocalizedText TaskLine { get; private set; }
        public static LocalizedText AcceptPrompt { get; private set; }
        public static LocalizedText ChoiceAccept { get; private set; }
        public static LocalizedText ChoiceDecline { get; private set; }
        public static LocalizedText AcceptResponse { get; private set; }
        public static LocalizedText AcceptL1 { get; private set; }
        public static LocalizedText AcceptL2 { get; private set; }
        public static LocalizedText AcceptL3 { get; private set; }
        public static LocalizedText DeclineResponse { get; private set; }

        public override StyleId DefaultStyle => "Draedon";

        public override void SetStaticDefaults() {
            IntroLine1 = this.GetLocalization(nameof(IntroLine1), () => "我需要执行一项关键实验，而你是当前最适合协助我的个体");
            IntroLine2 = this.GetLocalization(nameof(IntroLine2), () => "一百个泰拉年前，一场能量风暴横扫星系，摧毁了我在泰拉的绝大部分设施");
            IntroLine3 = this.GetLocalization(nameof(IntroLine3), () => "一切都太过遥远，当我意识到和泰拉的意识体断连时，已经一个世纪过去了");
            IntroLine4 = this.GetLocalization(nameof(IntroLine4), () => "在那场风暴后，星际跃迁开始变得极度不稳定，因此我必须重建泰拉上的基础系统");
            IntroLine5 = this.GetLocalization(nameof(IntroLine5), () => "我需要你协助搭建最新的量子纠缠阵列");
            IntroLine6 = this.GetLocalization(nameof(IntroLine6), () => "这项技术可以突破光速限制，实现跨越星际的即时通讯");
            IntroLine7 = this.GetLocalization(nameof(IntroLine7), () => "但在实际应用之前，这个世界需要有足够多的量子纠缠节点");
            IntroLine8 = this.GetLocalization(nameof(IntroLine8), () => "这是我设计的量子信号塔，它的核心采用了纠缠态稳定器和零点能量放大器");
            TechExplainLine1 = this.GetLocalization(nameof(TechExplainLine1), () => "每座信号塔都能与其他节点建立量子纠缠链接，形成一个覆盖全星系的通讯网络");
            TechExplainLine2 = this.GetLocalization(nameof(TechExplainLine2), () => "在理论条件下，它的传输延迟可以无限接近于零");
            TaskLine = this.GetLocalization(nameof(TaskLine), () => "我需要你在世界各地部署这些信号塔，建立起完整的纠缠网络");
            AcceptPrompt = this.GetLocalization(nameof(AcceptPrompt), () => "那么，你是否接受？");
            ChoiceAccept = this.GetLocalization(nameof(ChoiceAccept), () => "接受委托");
            ChoiceDecline = this.GetLocalization(nameof(ChoiceDecline), () => "以后再说");
            AcceptResponse = this.GetLocalization("Choice_Accept.AcceptResponse", () => "很好，这是信号塔的建造蓝图。完成后向我汇报进度");
            AcceptL1 = this.GetLocalization("Choice_Accept.L1", () => "这是第一批建材，数量有限，先搭建信号塔的主要框架");
            AcceptL2 = this.GetLocalization("Choice_Accept.L2", () => "当第一个节点投入运作后，通讯链路将趋于稳定");
            AcceptL3 = this.GetLocalization("Choice_Accept.L3", () => "到那时，我就能通过亚空间持续输送更多资源，用于更大规模的建造");
            DeclineResponse = this.GetLocalization("Choice_Decline.DeclineResponse", () => "我理解，当你准备好时随时可以回来找我");
        }

        public static void ResetWorldState() {
            delayTimer = 0;
            offeredThisSession = false;
        }

        public static void Tick() {
            if (Main.dedServ) {
                return;
            }

            if (!ShouldOffer()) {
                delayTimer = 0;
                return;
            }

            //Boss战/子世界/对话中挂起倒计时
            if (IsEnvironmentBlocked()) {
                return;
            }

            if (delayTimer <= 0) {
                delayTimer = Main.rand.Next(60 * 32, 60 * 40);//星流巨械后32-40秒
                return;
            }

            if (--delayTimer > 0) {
                return;
            }

            if (NarrativeRouter.Begin<DeploySignaltowerScenario>()) {
                offeredThisSession = true;
                delayTimer = 0;
            }
        }

        /// <summary>持久标记+本世界目标点判定登门</summary>
        private static bool ShouldOffer() {
            if (offeredThisSession) {
                return false;
            }
            //已部署或委托完成不再登门
            if (SignalTowerTargetManager.IsGenerated
                || DraedonStorySync.ReadDraedon(d => d.DeploySignaltowerQuestCompleted, d => d.DeploySignaltowerQuestCompleted)) {
                return false;
            }
            //前置仅本世界星流巨械已败
            return InWorldBossPhase.Downed29.Invoke();
        }

        private static bool IsEnvironmentBlocked()
            => EbnEffect.IsActive
            || CWRWorld.HasBoss
            || CWRWorld.BossRush
            || NPC.AnyNPCs(CWRID.NPC_Draedon)
            || NarrativeTriggerGate.IsBusy
            || SubWorldRef.AnyActiveSubWorld();

        protected override void Build(NarrativeComposer n) {
            bool declined = DraedonStorySync.ReadDraedon(d => d.DeploySignaltowerQuestDeclined, d => d.DeploySignaltowerQuestDeclined);
            if (!declined) {
                n.Say("Draedon", IntroLine1.Value)
                 .Say("Draedon", IntroLine2.Value)
                 .Say("Draedon", IntroLine3.Value)
                 .Say("Draedon", IntroLine4.Value)
                 .Say("Draedon", IntroLine5.Value)
                 .Say("Draedon", IntroLine6.Value)
                 .Say("Draedon", IntroLine7.Value)
                 .Say("Draedon", "Red", IntroLine8.Value, onEnter: DeploySignaltowerRender.ShowTowerImage)
                 .Say("Draedon", TechExplainLine1.Value)
                 .Say("Draedon", TechExplainLine2.Value)
                 .Say("Draedon", "Red", TaskLine.Value);
            }

            n.Choice("Draedon", "Red", AcceptPrompt.Value, c => c
                .Option("accept", ChoiceAccept.Value, NarrativeTarget.Goto("accept"))
                .Option("decline", ChoiceDecline.Value, NarrativeTarget.Goto("decline")))
             .Label("accept")
             .Say("Draedon", AcceptResponse.Value, onEnter: () => GiveBlueprint())
             .Say("Draedon", AcceptL1.Value, onEnter: GiveMaterials)
             .Say("Draedon", AcceptL2.Value)
             .Say("Draedon", AcceptL3.Value, onExit: AcceptQuest)
             .End()
             .Label("decline")
             .Say("Draedon", "Alt", DeclineResponse.Value, onExit: DeclineQuest)
             .End();
        }

        protected override void OnStarted() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
            DeploySignaltowerRender.RegisterShowEffect();
        }

        protected override void OnCompleted() {
            DeploySignaltowerRender.Cleanup();
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();
        }

        protected override NarrativePolicy ConfigurePolicy() => new() {
            IsCompleted = _ => DraedonStorySync.ReadDraedon(d => d.DeploySignaltowerQuestCompleted, d => d.DeploySignaltowerQuestCompleted),
            CanTrigger = (_, _) => false,//仅倒计时手动启动
        };

        private static void GiveBlueprint() {
            NarrativeServices.RewardGrant?.Grant(new RewardPayload {
                ItemType = ModContent.ItemType<ConstructionBlueprintQET>(),
                Stack = 1
            }, Main.LocalPlayer);
        }

        private static void GiveMaterials() {
            NarrativeServices.RewardGrant?.Grant(new RewardPayload {
                ItemType = CWRID.Item_ExoPrism,
                Stack = 8082 + Main.rand.Next(30)
            }, Main.LocalPlayer);
        }

        private static void AcceptQuest() {
            SignalTowerTargetManager.GenerateTargetPoints();
            //接取清拒绝标记
            DraedonStorySync.WriteDraedon(
                d => {
                    d.DeploySignaltowerQuestAccepted = true;
                    d.DeploySignaltowerQuestDeclined = false;
                },
                d => {
                    d.DeploySignaltowerQuestAccepted = true;
                    d.DeploySignaltowerQuestDeclined = false;
                });
        }

        private static void DeclineQuest() {
            DraedonStorySync.WriteDraedon(
                d => d.DeploySignaltowerQuestDeclined = true,
                d => d.DeploySignaltowerQuestDeclined = true);
        }
    }
}
