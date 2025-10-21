using CalamityMod.Items.Materials;
using CalamityMod.NPCs.DevourerofGods;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.GameSystem;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.PallbearerQuest
{
    /// <summary>
    /// ��������������������
    /// </summary>
    internal class SupCalDoGQuestReward : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(SupCalDoGQuestReward);
        public string LocalizationCategory => "Legend.HalibutText.ADV";

        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;

        //��ɫ���Ʊ��ػ�
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }

        //�Ի��ı����ػ�
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "�����ֶ�");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "��Ŀ��");

            Line1 = this.GetLocalization(nameof(Line1), () => "�ɾ�����");
            Line2 = this.GetLocalization(nameof(Line2), () => "��ѵ���һ���������������");
            Line3 = this.GetLocalization(nameof(Line3), () => "�����һ��Ƿ���֮��ʱ���������������ڳ���ʦ�����࣬�ܺ��ã�������");
            Line4 = this.GetLocalization(nameof(Line4), () => "�ú�");
            Line5 = this.GetLocalization(nameof(Line5), () => "����û������������һ�Σ�����ί������ɱ�ң������ô����");
            Line6 = this.GetLocalization(nameof(Line6), () => "���ź��������ע���������档��Ȼ����һ���ĵú�Ͷ��");
            Line7 = this.GetLocalization(nameof(Line7), () => "......��Խ��Խ�ܲ�����һ���");
        }

        protected override void OnScenarioStart() {
            SupCalSkyEffect.IsActive = true;
        }

        protected override void OnScenarioComplete() {
            SupCalSkyEffect.IsActive = false;
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalADV[0]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: false);
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

            Add(Rolename1.Value, Line1.Value);
            Add(Rolename1.Value, Line2.Value);
            Add(Rolename1.Value, Line3.Value);
            Add(Rolename1.Value, Line4.Value); //����
            Add(Rolename1.Value, Line5.Value);
            Add(Rolename1.Value, Line6.Value);

            if (hasHalibut) {
                Add(Rolename2.Value, Line7.Value);
            }
        }

        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 3) { //Line4ʱ���Ž���
                ADVRewardPopup.ShowReward(ModContent.ItemType<CosmiliteBar>(), 99, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (!save.SupCalDoGQuestReward) {
                return;
            }
            if (save.SupCalDoGQuestRewardSceneComplete) {
                return;
            }
            if (!DoGQuestRewardTrigger.Spawned) {
                return;
            }
            if (--DoGQuestRewardTrigger.RandomTimer > 0) {
                return;
            }
            if (ScenarioManager.Start<SupCalDoGQuestReward>()) {
                save.SupCalDoGQuestRewardSceneComplete = true;
                DoGQuestRewardTrigger.Spawned = false;
            }
        }
    }

    /// <summary>
    /// ׷�����ʹ��Heartcarver��ɱ����������
    /// </summary>
    internal class DoGQuestTracker : GlobalNPC//����ҵ�
    {
        private static void Check(NPC npc) {
            if (npc.type != ModContent.NPCType<DevourerofGodsHead>()) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }
            if (!halibutPlayer.ADCSave.SupCalQuestReward || halibutPlayer.ADCSave.SupCalDoGQuestDeclined) {
                return;
            }
            Item heldItem = player.GetItem();
            if (heldItem.type != ModContent.ItemType<Heartcarver>()) {
                return;
            }
            halibutPlayer.ADCSave.SupCalDoGQuestReward = true;
            DoGQuestRewardTrigger.Spawned = true;
            DoGQuestRewardTrigger.RandomTimer = 60 * Main.rand.Next(3, 5);
        }

        public override bool SpecialOnKill(NPC npc) {
            Check(npc);
            return false;
        }
    }

    internal static class DoGQuestRewardTrigger
    {
        public static bool Spawned = false;
        public static int RandomTimer;
    }
}
