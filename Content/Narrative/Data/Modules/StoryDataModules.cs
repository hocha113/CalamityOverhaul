using InnoVault.DataModules;

namespace CalamityOverhaul.Content.Narrative.Data.Modules
{
    public sealed class HalibutStoryData : DataModule
    {
        public bool HasCaughtHalibut;
        public bool FirstMet;
        public bool PostFirstMetIsComplete;
        public bool DyeProtest;
        public bool FishoilQuestDeclined;
        public bool FishoilQuestAccepted;
        public bool FishoilQuestCompleted;
        public bool FishoilQuestSuspended;
        public bool FirstResurrectionWarning;
    }

    public sealed class SupCalStoryData : DataModule
    {
        public bool FirstMetSupCal;
        public bool SupCalChoseToFight;
        public bool SupCalMoonLordReward;
        public bool SupCalDefeat;
        public bool SupCalQuestAccepted;
        public bool SupCalQuestDeclined;
        public bool SupCalQuestReward;
        public bool SupCalQuestRewardSceneComplete;
        public bool SupCalDoGQuestAccepted;
        public bool SupCalDoGQuestReward;
        public bool SupCalDoGQuestRewardSceneComplete;
        public bool SupCalDoGQuestDeclined;
        public bool SupCalYharonQuestReward;
        public bool SupCalYharonQuestAccepted;
        public bool SupCalYharonQuestDeclined;
        public bool SupCalYharonQuestRewardSceneComplete;
        public bool EternalBlazingNowTriggered;
        public bool EternalBlazingNowChoice1;
        public bool EternalBlazingNowChoice2;
        public bool GiveBlazingBud;
        public bool EternalBlazingNow;
        /// <summary>女巫告别带走了比目鱼，海伦尾声待兑现；旧档缺此字段读作 false</summary>
        public bool HelenEpiloguePending;
        /// <summary>海伦尾声已播完，防重播</summary>
        public bool HelenEpilogueSeen;
        public bool HelenInterferenceTriggered;
        public bool HelenInterferenceContinue;
        public bool HelenInterferenceStop;
    }

    public sealed class DraedonStoryData : DataModule
    {
        public bool DeploySignaltowerQuestAccepted;
        public bool DeploySignaltowerQuestDeclined;
        public bool DeploySignaltowerFirstTowerBuilt;
        public bool DeploySignaltowerQuestCompleted;
        public bool UseConstructionBlueprint;
        [DataModuleName(nameof(FirstExoMechdusaSum), "FristExoMechdusaSum")]
        public bool FirstExoMechdusaSum;
        public bool ExoMechEndingDialogue;
        public bool ExoMechSecondDefeat;
        public bool ExoMechThirdDefeat;
        public int ExoMechDefeatCount;
        public bool FirstMetTzeentch;
    }

    public enum OldDukeInteractionState
    {
        /// <summary>未遇见</summary>
        NotMet = 0,
        /// <summary>已遇见未选</summary>
        Met = 1,
        /// <summary>接受合作</summary>
        AcceptedCooperation = 2,
        /// <summary>拒绝合作，可重选</summary>
        DeclinedCooperation = 3,
        /// <summary>永久开战</summary>
        ChoseToFight = 4
    }

    public sealed class OldDukeStoryData : DataModule
    {
        [DataModuleName(nameof(OldDukeState), "OldDukeInteraction")]
        public OldDukeInteractionState OldDukeState { get; set; }
        public bool OldDukeFirstCampsiteDialogueCompleted;
        public bool OldDukeFindFragmentsQuestTriggered;
        public bool OldDukeFindFragmentsQuestCompleted;

        [DataModuleIgnore]
        public bool FirstMetOldDuke => OldDukeState != OldDukeInteractionState.NotMet;
        [DataModuleIgnore]
        public bool OldDukeCooperationAccepted => OldDukeState == OldDukeInteractionState.AcceptedCooperation;
        [DataModuleIgnore]
        public bool OldDukeCooperationDeclined => OldDukeState == OldDukeInteractionState.DeclinedCooperation;
        [DataModuleIgnore]
        public bool OldDukeChoseToFight => OldDukeState == OldDukeInteractionState.ChoseToFight;
    }

    public sealed class BossGiftStoryData : DataModule
    {
        public bool QueenBeeGift;
        public bool SkeletronGift;
        public bool EyeOfCthulhuGift;
        public bool KingSlimeGift;
        public bool CrabulonGift;
        public bool PerforatorGift;
        public bool HiveMindGift;
        public bool WallOfFleshGift;
        public bool SlimeGodGift;
        public bool CryogenGift;
        public bool BrimstoneElementalGift;
        public bool AquaticScourgeGift;
        public bool CalamitasCloneGift;
        public bool PlanteraGift;
        public bool GolemGift;
        public bool HellGift;
        public bool MoonLordGift;
        public bool LeviathanGift;
        public bool PlaguebringerGift;
        public bool ProvidenceGift;
        public bool DevourerOfGodsGift;
        public bool YharonGift;
        public bool SupremeCalamitasGift;
    }

    public sealed class ShepelStoryData : DataModule
    {
        public int IdleVariantSeed;
        public int StoryPhase;
        public int ReactiveEventFlags;
        public int LastDefeatedBossNpcType = -1;
        public bool FirstSHPCObtained;
        public int UnderworldVariantSeed;
        public int DungeonVariantSeed;
        public int OceanVariantSeed;
        public int SnowVariantSeed;
        public int JungleVariantSeed;
        public int NightVariantSeed;
        public bool FirstSHPCIntroCompleted;
    }

    public sealed class HimayoStoryData : DataModule
    {
        public bool FirstMet;
        /// <summary>初遇对话播完，试炼委托门禁</summary>
        public bool PostFirstMetIsComplete;
        public bool ToriiSwordTaken;
        /// <summary>试炼解锁保底剩余帧，0=未武装；拔刀武装，叙事忙暂停，到期强制初遇完成</summary>
        public int TrialUnlockSafetyTicks;
    }

    /// <summary>沈幽初遇（鬼雨世界）进度；随玩家存档</summary>
    public sealed class ShenyoStoryData : DataModule
    {
        public bool FirstMet;
        /// <summary>初遇对话播完，送出演出与发伞的门禁</summary>
        public bool PostFirstMetIsComplete;
        /// <summary>本次抵达深层的方式：true=被鬼奴杀死拖入，false=夺伞下潜；选项门禁用</summary>
        public bool ArrivedByDeath;
        /// <summary>鬼伞已发放，防重复发放</summary>
        public bool KikasaGranted;
    }

    /// <summary>鬼伞沉宴试炼节点礼物完成位，顺序对应 KikasaTrialQuestLine 的24关</summary>
    public sealed class ShenyoGiftStoryData : DataModule
    {
        public bool KingSlimeGift;
        public bool EyeOfCthulhuGift;
        public bool EvilBossGift;
        public bool CalamityEvilGift;
        public bool QueenBeeOrDeerclopsGift;
        public bool SkeletronGift;
        public bool SlimeGodGift;
        public bool WallOfFleshGift;
        public bool QueenSlimeGift;
        public bool AquaticScourgeGift;
        public bool MechsGift;
        public bool PlanteraGift;
        public bool LeviathanGift;
        public bool GolemGift;
        public bool DukeFishronGift;
        public bool EmpressGift;
        public bool CultistGift;
        public bool MoonLordGift;
        public bool PolterghastGift;
        public bool OldDukeGift;
        public bool DevourerOfGodsGift;
        public bool YharonGift;
        public bool ExoAndSCalGift;
        public bool BossRushGift;
    }

    /// <summary>鬼切试炼节点礼物完成位（双目标试炼共用一位）</summary>
    public sealed class HimayoGiftStoryData : DataModule
    {
        public override int Version => 2;

        public bool EyeOfCthulhuGift;
        public bool EvilBossGift;
        public bool CalamityEvilGift;
        public bool SlimeGodGift;
        public bool WallOfFleshGift;
        public bool AquaticScourgeGift;
        public bool BrimstoneElementalGift;
        public bool DestroyerGift;
        public bool TwinsGift;
        public bool SkeletronPrimeGift;
        public bool CalamitasCloneGift;
        public bool PlanteraGift;
        public bool GolemGift;
        public bool CultistGift;
        public bool MoonLordGift;
        public bool ProvidenceGift;
        public bool PolterghastGift;
        public bool DevourerOfGodsGift;
        public bool YharonGift;
        public bool ExoMechsGift;
        public bool SupremeCalamitasGift;
        public bool BossRushGift;
    }

    public sealed class ShepelGiftStoryData : DataModule
    {
        public bool EyeOfCthulhuGift;
        public bool EaterOfWorldsGift;
        public bool BrainOfCthulhuGift;
        public bool HiveMindGift;
        public bool PerforatorGift;
        public bool SlimeGodGift;
        public bool WallOfFleshGift;
        public bool AquaticScourgeGift;
        public bool BrimstoneElementalGift;
        public bool DestroyerGift;
        public bool TwinsGift;
        public bool SkeletronPrimeGift;
        public bool CalamitasCloneGift;
        public bool PlanteraGift;
        public bool GolemGift;
        public bool CultistGift;
        public bool MoonLordGift;
        public bool ProvidenceGift;
        public bool PolterghastGift;
        public bool DevourerofGodsGift;
        public bool YharonGift;
        public bool ExoMechsGift;
        public bool SupremeCalamitasGift;
    }

    public sealed class ApolliaStoryData : DataModule
    {
        public bool GalacticCrisisCompleted;
    }

    /// <summary>
    /// 旧版「只讲委托」引导的进度。委托并入任务书后由
    /// <see cref="QuestBookGuideData"/> 接管，此类只为老档迁移保留
    /// </summary>
    public sealed class EntrustGuideData : DataModule
    {
        public bool GuideSeen;
    }

    /// <summary>任务书教程进度（图谱 + 委托两章）；随玩家存档</summary>
    public sealed class QuestBookGuideData : DataModule
    {
        /// <summary>已完成的教程版本；0 = 从未走完</summary>
        public int CompletedVersion;
        /// <summary>第一章检查点：已讲完的最大步号，0 = 未开讲</summary>
        public int ChapterOneStep;
        /// <summary>第二章检查点：已讲完的最大步号，0 = 未开讲</summary>
        public int ChapterTwoStep;
        /// <summary>第一章已走完；第二章要等第一条委托到手才排队</summary>
        public bool ChapterOneDone;
        /// <summary>玩家至少自己开过一次书，第一章据此才占位</summary>
        public bool BookEverOpened;
        /// <summary>玩家主动收起过教程；只能由书内的「?」再启动</summary>
        public bool Declined;
        /// <summary>老档的 <see cref="EntrustGuideData"/> 已折算过，别重复折算</summary>
        public bool LegacyEntrustGuideMerged;
    }

    public sealed class HalibutGuideData : DataModule
    {
        public bool GuideSeen;
        /// <summary>是否已答复过开场询问；答复过就不再弹</summary>
        public bool AskAnswered;
        /// <summary>婉拒过引导；只能由引航海图再启动</summary>
        public bool Declined;
    }

    /// <summary>义体转盘引导进度；随玩家存档</summary>
    public sealed class CyberwareGuideData : DataModule
    {
        public bool GuideSeen;
    }

    /// <summary>鬼伞七步引导进度；随玩家存档</summary>
    public sealed class KikasaGuideData : DataModule
    {
        /// <summary>教程走完（含跳过收尾）；老档五步版的完成标记同样生效，不重讲</summary>
        public bool GuideSeen;
        /// <summary>已完成的教程版本；0 = 从未走完。步骤改版时 +1 让老玩家补讲</summary>
        public int CompletedVersion;
        /// <summary>已讲完的最大步号（1 起，对应步序），中断后从下一步续讲</summary>
        public int StepCheckpoint;
        /// <summary>玩家主动收起过教程；只能由湖心景的「?」再启动</summary>
        public bool Declined;
    }

    /// <summary>旧网深潜引导与首潜委托进度；随玩家存档</summary>
    public sealed class OldNetGuideData : DataModule
    {
        public bool GuideSeen;
        /// <summary>完成过一次安全登出（首潜委托的完成判据）</summary>
        public bool DiveCompleted;
    }

    /// <summary>鬼切教程进度；随玩家存档</summary>
    public sealed class OnikiriGuideData : DataModule
    {
        /// <summary>已完成的教程版本；0 = 从未完成</summary>
        public int CompletedVersion;
        /// <summary>段落检查点:0=HUD/改铭台/点鬼簿未完成,1=从鬼域步骤继续</summary>
        public int Checkpoint;
        /// <summary>实操检查点:0=未开始,1=表世界,2=里世界,3=已肢解,4=已收域</summary>
        public int PracticeCheckpoint;
        /// <summary>是否已答复过开场询问；答复过就不再弹</summary>
        public bool AskAnswered;
        /// <summary>婉拒过教程；只能由稽古符再启动</summary>
        public bool Declined;
        /// <summary>
        /// 最近一次「本版新增了内容，要不要补讲」问的是哪个教程版本。
        /// 小于当前版本 = 还没就这一版问过；旧档缺此字段读作 0
        /// </summary>
        public int RefresherAskedVersion;
    }
}
