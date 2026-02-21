using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest.PallbearerQuest
{
    internal class SupCalMoonLordReward : ADVScenarioBase, ILocalizedModType
    {
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

        //记录Line6的实际索引，用于PreProcessSegment中准确触发奖励
        private int rewardLineIndex = -1;

        //设置场景默认使用硫磺火风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => " 硫火女巫");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "比目鱼");

            Line1 = this.GetLocalization(nameof(Line1), () => "呵呵呵");
            Line2 = this.GetLocalization(nameof(Line2), () => "那个家伙……竟已落到这种地步");
            Line3 = this.GetLocalization(nameof(Line3), () => "你知道现在的地底是什么景象吗?");
            Line4 = this.GetLocalization(nameof(Line4), () => "......所以你这次来是?");
            Line5 = this.GetLocalization(nameof(Line5), () => "送你点小玩具，顺带有个委托交给你");
            Line6 = this.GetLocalization(nameof(Line6), () => "一把小巧的弩，我需要你拿它干掉下面的那个苟延残喘吸食地热的家伙，记住只能用这个弩");
            Line7 = this.GetLocalization(nameof(Line7), () => "如果你做到了，我们的合作还能继续");
        }

        protected override void OnScenarioStart() {
            //开始生成粒子
            SupCalEffect.IsActive = true;
        }

        protected override void OnScenarioComplete() {
            //停止粒子生成
            SupCalEffect.IsActive = false;
        }

        protected override void Build() {
            //注册立绘
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalsADV[0]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: false);
            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.Helen_solemnADV);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: false);

            //添加对话
            //计数器跟踪当前索引
            int currentIndex = 0;

            Add(Rolename1.Value, Line1.Value);
            currentIndex++; //0

            Add(Rolename1.Value, Line2.Value);
            currentIndex++; //1

            Add(Rolename1.Value, Line3.Value);
            currentIndex++; //2

            //条件对话：只有在特定情况下才添加
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer) && halibutPlayer.HasHalubut) {
                Add(Rolename2.Value, Line4.Value);
                currentIndex++; //3 (如果添加)
            }

            Add(Rolename1.Value, Line5.Value);
            currentIndex++; //3 or 4

            //记录奖励对话的实际索引
            rewardLineIndex = currentIndex;
            Add(Rolename1.Value, Line6.Value);//奖励
            currentIndex++; //4 or 5

            Add(Rolename1.Value, Line7.Value);
            currentIndex++; //5 or 6
        }

        public override void PreProcessSegment(DialoguePreProcessArgs args) {
            //使用动态计算的索引而不是硬编码的5
            if (args.Index == rewardLineIndex) { //Line6 - 奖励物品
                ADVRewardPopup.ShowReward(ModContent.ItemType<Pallbearer>(), 1, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero
                    , styleProvider: () => ADVRewardPopup.RewardStyle.Brimstone);
            }
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (save.SupCalMoonLordReward) {
                return;
            }
            //必须先触发过FirstMetSupCal场景
            if (!save.FirstMetSupCal) {
                return;
            }
            //如果玩家拿着大比目鱼，则必须先获得过比目鱼小姐给的礼物才能触发，避免这两个场景冲突
            if (halibutPlayer.HeldHalibut && !save.MoonLordGift) {
                return;
            }
            //必须选择了Choice1（拔出武器）
            if (!save.SupCalChoseToFight) {
                return;
            }
            //必须击败了月球领主
            if (!SupCalMoonLordRewardNPC.Spawned) {
                return;
            }
            if (--SupCalMoonLordRewardNPC.RandomTimer > 0) {
                return;
            }
            if (ScenarioManager.Start<SupCalMoonLordReward>()) {
                save.SupCalMoonLordReward = true;
                SupCalMoonLordRewardNPC.Spawned = false;
            }
        }
    }

    internal class SupCalMoonLordRewardNPC : DeathTrackingNPC, IWorldInfo
    {
        public static bool Spawned = false;
        public static int RandomTimer;
        void IWorldInfo.OnWorldLoad() {
            Spawned = false;
            RandomTimer = 0;
        }
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.MoonLordCore;
        public override void OnNPCDeath(NPC npc) {
            if (npc.type == NPCID.MoonLordCore && !CWRWorld.BossRush) {
                Spawned = true;
                RandomTimer = 60 * Main.rand.Next(3, 5);//给一个3到5秒的缓冲时间，打完立刻触发不太合适
            }
        }
    }
}
