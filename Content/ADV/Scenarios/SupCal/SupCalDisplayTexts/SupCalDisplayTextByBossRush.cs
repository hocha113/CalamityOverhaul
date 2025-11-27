using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.ModifySupCalNPCs;
using CalamityOverhaul.Content.Items.Accessories;
using CalamityOverhaul.Content.Items.Magic.Pandemoniums;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.SupCalDisplayTexts
{
    internal class SupCalDisplayTextByBossRush : ModifyDisplayText, ILocalizedModType
    {
        #region 本地化文本字段
        //BossRush召唤文本
        public LocalizedText SummonBossRush { get; private set; }
        public LocalizedText SummonBossRushWithProverbs { get; private set; }

        //BossRush开始文本
        public LocalizedText StartBossRush { get; private set; }

        //BossRush阶段文本
        public LocalizedText BH2BossRush { get; private set; }
        public LocalizedText BH3BossRush { get; private set; }
        public LocalizedText BrothersBossRush { get; private set; }
        public LocalizedText Phase2BossRush { get; private set; }
        public LocalizedText BH4BossRush { get; private set; }
        public LocalizedText SeekerRingBossRush { get; private set; }
        public LocalizedText BH5BossRush { get; private set; }
        public LocalizedText Sepulcher2BossRush { get; private set; }

        //BossRush濒死文本
        public LocalizedText Desperation1BossRush { get; private set; }
        public LocalizedText Desperation2BossRush { get; private set; }
        public LocalizedText Desperation3BossRush { get; private set; }
        public LocalizedText Desperation4BossRush { get; private set; }

        //BossRush战败文本
        public LocalizedText Acceptance1BossRush { get; private set; }
        public LocalizedText Acceptance2BossRush { get; private set; }
        #endregion

        private void LoadLocalization() {
            //召唤文本
            SummonBossRush = this.GetLocalization(nameof(SummonBossRush), () => "呵……你身上的气息和记忆，让我想起了早已死去的那个人");
            SummonBossRushWithProverbs = this.GetLocalization(nameof(SummonBossRushWithProverbs), () => "那枚戒指……连我这样的残影，也能感到它的颤动呢");

            //开始文本
            StartBossRush = this.GetLocalization(nameof(StartBossRush), () => "我不过是金源魄的灵异产物……但有的事情，我也必须完成");

            //阶段文本
            BH2BossRush = this.GetLocalization(nameof(BH2BossRush), () => "你的实力确实不差……但还不够");
            BH3BossRush = this.GetLocalization(nameof(BH3BossRush), () => "真正的她……早已化作灰烬，随风散在你走过的道路上，呵呵……");
            BrothersBossRush = this.GetLocalization(nameof(BrothersBossRush), () => "这些灵魂……是影子的影子。越是复制，越显得悲哀，不是吗？");
            Phase2BossRush = this.GetLocalization(nameof(Phase2BossRush), () => "即便是被复制的生命……也有需要拼尽全力的理由");
            BH4BossRush = this.GetLocalization(nameof(BH4BossRush), () => "站住！让我好好称量一下你……到底能否承担一个时代重量！");
            SeekerRingBossRush = this.GetLocalization(nameof(SeekerRingBossRush), () => "你的力量……与她记忆中的那个人太相似了，是巧合，还是命运？");
            BH5BossRush = this.GetLocalization(nameof(BH5BossRush), () => "作为灵异产物，我被创造，就是为了迎接今日这场战斗");
            Sepulcher2BossRush = this.GetLocalization(nameof(Sepulcher2BossRush), () => "最后的试炼就在此刻……让我看看，你是否有资格替她继续前行");

            //濒死文本
            Desperation1BossRush = this.GetLocalization(nameof(Desperation1BossRush), () => "即使只是残片……");
            Desperation2BossRush = this.GetLocalization(nameof(Desperation2BossRush), () => "别以为胜利已定……");
            Desperation3BossRush = this.GetLocalization(nameof(Desperation3BossRush), () => "呵……影子的寿命，本就脆弱到可笑吗……");
            Desperation4BossRush = this.GetLocalization(nameof(Desperation4BossRush), () => "你赢了……但她的道路，还远远没有结束");

            //战败文本
            Acceptance1BossRush = this.GetLocalization(nameof(Acceptance1BossRush), () => "这大概，就是我的最终归宿吧");
            Acceptance2BossRush = this.GetLocalization(nameof(Acceptance2BossRush), () => "若是她在此……");
        }

        public override void SetStaticDefaults() {
            LoadLocalization();

            //召唤对话
            SetDynamicDialogue("SCalSummonText", () => {
                var player = Main.LocalPlayer;
                if (player.TryGetModPlayer<ProverbsPlayer>(out var proverbsPlayer) && proverbsPlayer.HasProverbs) {
                    return new DialogueOverride(SummonBossRushWithProverbs, Color.Orange);
                }
                return new DialogueOverride(SummonBossRush, Color.Orange);
            });

            //开始对话
            SetDynamicDialogue("SCalStartText", () => {
                return new DialogueOverride(StartBossRush, Color.Orange);
            });

            //阶段对话
            SetDynamicDialogue("SCalBH2Text", () => {
                return new DialogueOverride(BH2BossRush, Color.Orange);
            });

            SetDynamicDialogue("SCalBH3Text", () => {
                return new DialogueOverride(BH3BossRush, Color.Orange);
            });

            SetDynamicDialogue("SCalBrothersText", () => {
                return new DialogueOverride(BrothersBossRush, Color.Orange);
            });

            SetDynamicDialogue("SCalPhase2Text", () => {
                return new DialogueOverride(Phase2BossRush, Color.Orange);
            });

            SetDynamicDialogue("SCalBH4Text", () => {
                return new DialogueOverride(BH4BossRush, Color.OrangeRed);
            });

            SetDynamicDialogue("SCalSeekerRingText", () => {
                return new DialogueOverride(SeekerRingBossRush, Color.Orange);
            });

            SetDynamicDialogue("SCalBH5Text", () => {
                return new DialogueOverride(BH5BossRush, Color.Orange);
            });

            SetDynamicDialogue("SCalSepulcher2Text", () => {
                return new DialogueOverride(Sepulcher2BossRush, Color.Orange);
            });

            //濒死对话
            SetDynamicDialogue("SCalDesparationText1", () => {
                return new DialogueOverride(Desperation1BossRush, Color.Orange);
            });

            SetDynamicDialogue("SCalDesparationText2", () => {
                return new DialogueOverride(Desperation2BossRush, Color.Orange);
            });

            SetDynamicDialogue("SCalDesparationText3", () => {
                return new DialogueOverride(Desperation3BossRush, Color.Orange);
            });

            SetDynamicDialogue("SCalDesparationText4", () => {
                return new DialogueOverride(Desperation4BossRush, Color.Orange);
            });

            //战败对话
            SetDynamicDialogue("SCalAcceptanceText1", () => {
                return new DialogueOverride(Acceptance1BossRush, Color.Orange);
            });

            SetDynamicDialogue("SCalAcceptanceText2", () => {
                return new DialogueOverride(Acceptance2BossRush, Color.Orange);
            });

            //重复战斗对话（BossRush模式下使用相同的濒死对话）
            SetDynamicDialogue("SCalDesparationText1Rematch", () => {
                return new DialogueOverride(Desperation1BossRush, Color.Orange);
            });

            SetDynamicDialogue("SCalDesparationText2Rematch", () => {
                return new DialogueOverride(Desperation2BossRush, Color.Orange);
            });

            SetDynamicDialogue("SCalDesparationText3Rematch", () => {
                return new DialogueOverride(Desperation3BossRush, Color.Orange);
            });

            SetDynamicDialogue("SCalDesparationText4Rematch", () => {
                return new DialogueOverride(Desperation4BossRush, Color.Orange);
            });
        }

        public override bool PreHandle(ref string key, ref Color color) {
            string result = key.Split('.').Last();
            if (result == "SCalAcceptanceText3" && !VaultUtils.isClient) {
                VaultUtils.SpwanItem(new EntitySource_WorldEvent("BOSSRUSH"), new Item(ModContent.ItemType<Pandemonium>()));
                return false;
            }
            return base.PreHandle(ref key, ref color);
        }

        public override bool Alive(Player player) {
            //已经完成'永恒燃烧'结局，并且处于BossRush模式下，且SupCal正在场上时才会触发这些台词
            return ModifySupCalNPC.SetAIState() && EbnPlayer.OnEbn(player)
                && ModifySupCalNPC.TrueBossRushStateByAI
                && NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas);
        }
    }
}
