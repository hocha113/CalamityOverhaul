using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    //双子眼，Retinazer或Spazmatism任意一只最后死亡均可触发
    internal class ShepelTwinsDialogue : ShepelReactiveDialogueBase
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            Line1 = this.GetLocalization(nameof(Line1),
                () => "机械双子眼已双双坠毁。它们的火力覆盖网已被完全撕裂。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "无论它们如何进行视觉共享和战术协同，也无法逃脱我对您的绝对聚焦。");
        }

        //双子眼可能以任意一只的NPC类型触发BossDefeated，需要同时检测两种类型
        protected override bool CheckConditions(Player player, ADVSave save) {
            ShepelADVData data = save.Get<ShepelADVData>();
            return ShepelReactiveEvents.HasFlag(data, HandledEvent)
                && (data.LastDefeatedBossNpcType == NPCID.Retinazer
                    || data.LastDefeatedBossNpcType == NPCID.Spazmatism);
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            ShepelADVData data = Main.LocalPlayer.GetModPlayer<ADVSavePlayer>().ADVSave.Get<ShepelADVData>();
            ConsumeEvent(data);

            Add(RoleName.Value, Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Serious));
            Add(RoleName.Value, Line2.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Smirk),
                onComplete: Complete);
        }
    }
}
