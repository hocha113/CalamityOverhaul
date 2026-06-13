using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    //真灾厄是灾厄mod的最终Boss，Shepel给出最高规格的情绪反应
    internal class ShepelSupremeCalamitasDialogue : ShepelReactiveDialogueBase
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => CWRID.NPC_SupremeCalamitas;

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            Line1 = this.GetLocalization(nameof(Line1),
                () => "真正的灾厄……终于结束了。看着您满身的伤痕，我的核心在隐隐作痛。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "所有的苦难都已过去。请放下武器，靠在我的肩膀上好好休整一下吧。");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "这段再次共同走到终点的记忆，将成为我系统中最神圣的禁区，永久禁止覆写。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            ShepelADVData data = Main.LocalPlayer.GetModPlayer<ADVSavePlayer>().ADVSave.Get<ShepelADVData>();
            ConsumeEvent(data);

            Add(RoleName.Value, Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Serious));
            Add(RoleName.Value, Line2.Value, onStart: () => {
                SetPortraitFace(ShepelFullBodyPortrait.Face.Happy);
                if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait)
                    portrait.TriggerGlitch(0.3f, 0.2f);
                SoundEngine.PlaySound(SoundID.MenuTick);
            });
            Add(RoleName.Value, Line3.Value, onComplete: Complete);
        }
    }
}
