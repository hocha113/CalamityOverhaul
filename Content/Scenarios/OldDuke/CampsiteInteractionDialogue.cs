using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.OldDuke.OceanRaiderses;
using CalamityOverhaul.Content.Scenarios.OldDuke.OldDukeShops;
using CalamityOverhaul.Content.Scenarios.OldDuke.Quest;
using CalamityOverhaul.OtherMods.ImproveGame;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke
{
    internal sealed class CampsiteInteractionDialogue : NarrativeScenario, ILocalizedModType
    {
        public enum InteractionEntryMode
        {
            Default,
            SparOnly,
            StrollEnd,
        }

        private const string MainLabel = "main";
        private const string TradeLabel = "trade";
        private const string SubmitLabel = "submit";
        private const string StrollLabel = "stroll";
        private const string SparLabel = "spar";
        private const string DoneLabel = "done";

        public static InteractionEntryMode EntryMode = InteractionEntryMode.Default;
        public static bool GiveTeaOnStart;

        public string LocalizationCategory => "ADV";

        public static LocalizedText GreetingLine { get; private set; }
        public static LocalizedText Choice1Text { get; private set; }
        public static LocalizedText Choice2Text { get; private set; }
        public static LocalizedText Choice3Text { get; private set; }
        public static LocalizedText Choice4Text { get; private set; }
        public static LocalizedText Choice5Text { get; private set; }
        public static LocalizedText Choice2DisabledHint { get; private set; }
        public static LocalizedText Choice1Response { get; private set; }
        public static LocalizedText Choice2Response { get; private set; }
        public static LocalizedText Choice3Response { get; private set; }
        public static LocalizedText QuestCompleteLine { get; private set; }
        public static LocalizedText B1 { get; private set; }
        public static LocalizedText Choice4_R1 { get; private set; }
        public static LocalizedText Choice4_R2 { get; private set; }
        public static LocalizedText Choice4_R3 { get; private set; }

        public override StyleId DefaultStyle => "Sulfsea";

        public override void SetStaticDefaults() {
            GreetingLine = this.GetLocalization(nameof(GreetingLine), () => "你需要什么？");
            Choice1Text = this.GetLocalization(nameof(Choice1Text), () => "我来找你交易一下");
            Choice2Text = this.GetLocalization(nameof(Choice2Text), () => "你要的东西我都弄来了");
            Choice3Text = this.GetLocalization(nameof(Choice3Text), () => "我只是来溜达一圈");
            Choice4Text = this.GetLocalization(nameof(Choice4Text), () => "我要和你切磋一下");
            Choice5Text = this.GetLocalization(nameof(Choice5Text), () => "和我聊聊你的过去吧");
            Choice2DisabledHint = this.GetLocalization(nameof(Choice2DisabledHint), () => "海洋残片不足");
            Choice1Response = this.GetLocalization(nameof(Choice1Response), () => "看看我这里有什么吧");
            Choice2Response = this.GetLocalization(nameof(Choice2Response), () => "很好，这些碎片足够了。这是你应得的奖励");
            Choice3Response = this.GetLocalization(nameof(Choice3Response), () => "......");
            QuestCompleteLine = this.GetLocalization(nameof(QuestCompleteLine), () => "嗯...你有什么新发现吗？我现在很忙");
            B1 = this.GetLocalization(nameof(B1), () => "我未来几天会把全部时间花在解读这些残片上，完成了之后我会通知你的");
            Choice4_R1 = this.GetLocalization(nameof(Choice4_R1), () => "我虽然老了，但年轻时候的技巧可还没落下");
            Choice4_R2 = this.GetLocalization(nameof(Choice4_R2), () => "那就让我看看，你最近有没有长进");
            Choice4_R3 = this.GetLocalization(nameof(Choice4_R3), () => "把我当磨刀石并不是个明智的举动...");
        }

        protected override void Build(NarrativeComposer n) {
            InteractionEntryMode mode = EntryMode;
            EntryMode = InteractionEntryMode.Default;

            if (mode == InteractionEntryMode.StrollEnd) {
                n.Say("OldDuke", Choice3Response.Value).End();
                return;
            }

            if (mode == InteractionEntryMode.SparOnly) {
                n.SayTimed("OldDuke", PickSparLine(), TimedSettings.Of(2f), onExit: SpawnSparBoss).End();
                return;
            }

            if (GiveTeaOnStart) {
                GiveTeaOnStart = false;
                n.Reward(ItemID.Teacup, 1, string.Empty);
            }

            bool questCompleted = OldDukeStorySync.Read(
                d => d.OldDukeFindFragmentsQuestCompleted,
                d => d.OldDukeFindFragmentsQuestCompleted);

            if (questCompleted) {
                n.Choice("OldDuke", QuestCompleteLine.Value, c => c
                    .Option("trade", Choice1Text.Value, NarrativeTarget.Goto(TradeLabel))
                    .Option("chat", Choice5Text.Value, NarrativeTarget.Goto(DoneLabel), onSelect: () => NarrativeRouter.Begin<CampsiteChatDialogue>())
                    .Option("spar", Choice4Text.Value, NarrativeTarget.Goto(SparLabel), enabled: () => !NPC.AnyNPCs(CWRID.NPC_OldDuke))
                    .Option("leave", Choice3Text.Value, NarrativeTarget.Goto(StrollLabel)));
            }
            else {
                int fragmentCount = FindFragmentQuestEntry.GetFragmentCount();
                bool hasEnoughFragments = fragmentCount >= 777;

                n.Choice("OldDuke", GreetingLine.Value, c => c
                    .Option("trade", Choice1Text.Value, NarrativeTarget.Goto(TradeLabel))
                    .Option("submit", Choice2Text.Value, NarrativeTarget.Goto(SubmitLabel), enabled: () => hasEnoughFragments, disabledHint: hasEnoughFragments ? string.Empty : Choice2DisabledHint.Value)
                    .Option("spar", Choice4Text.Value, NarrativeTarget.Goto(SparLabel), enabled: () => !NPC.AnyNPCs(CWRID.NPC_OldDuke))
                    .Option("leave", Choice3Text.Value, NarrativeTarget.Goto(StrollLabel)));
            }

            n.Label(TradeLabel)
             .Say("OldDuke", Choice1Response.Value, onExit: OpenShop)
             .End()
             .Label(SubmitLabel)
             .Command(CompleteFragmentSubmit)
             .Reward(ModContent.ItemType<OceanRaiders>(), 1, string.Empty)
             .Say("OldDuke", Choice2Response.Value)
             .Say("OldDuke", B1.Value)
             .End()
             .Label(StrollLabel)
             .Say("OldDuke", Choice3Response.Value)
             .End()
             .Label(SparLabel)
             .SayTimed("OldDuke", PickSparLine(), TimedSettings.Of(2f), onExit: SpawnSparBoss)
             .End()
             .Label(DoneLabel)
             .End();
        }

        private static string PickSparLine() {
            List<LocalizedText> lines = [Choice4_R1, Choice4_R2, Choice4_R3];
            return lines[Main.rand.Next(lines.Count)].Value;
        }

        private static void OpenShop() {
            OldDukeShopUI.Instance.InitializeShop();
            OldDukeShopUI.Instance.Active = true;
        }

        private static void CompleteFragmentSubmit() {
            ConsumeFragments(777);
            OldDukeStorySync.Write(
                d => d.OldDukeFindFragmentsQuestCompleted = true,
                d => d.OldDukeFindFragmentsQuestCompleted = true);
        }

        private static void SpawnSparBoss() {
            if (NPC.AnyNPCs(CWRID.NPC_OldDuke) || CWRMod.Instance.calamity is null) {
                return;
            }

            Projectile.NewProjectile(Main.LocalPlayer.FromObjectGetParent(),
                Main.LocalPlayer.Center, Vector2.Zero,
                ModContent.ProjectileType<SpawnOldDukeWannaToFight>(), 0, 0, Main.myPlayer);
        }

        private static void ConsumeFragments(int amount) {
            Player player = Main.LocalPlayer;
            int fragmentType = ModContent.ItemType<Oceanfragments>();
            int remaining = amount;
            var bigBags = player.GetBigBagItems() ?? [];
            Item[][] inventories = [
                player.inventory,
                player.bank.item,
                player.bank2.item,
                player.bank3.item,
                player.bank4.item,
                [.. bigBags],
            ];

            foreach (var inventorie in inventories) {
                for (int i = 0; i < inventorie.Length && remaining > 0; i++) {
                    if (inventorie[i].type == fragmentType) {
                        int toConsume = Math.Min(inventorie[i].stack, remaining);
                        inventorie[i].stack -= toConsume;
                        remaining -= toConsume;

                        if (inventorie[i].stack <= 0) {
                            inventorie[i].TurnToAir();
                        }
                    }
                }
            }
        }
    }
}
