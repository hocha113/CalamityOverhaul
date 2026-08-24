using CalamityOverhaul.Content.Cyberwares.UIs;
using CalamityOverhaul.Content.NPCs.CommonUIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Victors.UIs
{
    /// <summary>维克托对话条，右键开；布局与皮肤归 <see cref="NPCTalkUIBase"/>，这里只装配内容</summary>
    internal class VictorTalkUI : NPCTalkUIBase, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static VictorTalkUI Instance => UIHandleLoader.GetUIHandleOfType<VictorTalkUI>();

        #region 本地化

        public static LocalizedText SpeakerName { get; private set; }
        public static LocalizedText ClinicButtonText { get; private set; }
        public static LocalizedText ChatButtonText { get; private set; }
        public static LocalizedText LeaveButtonText { get; private set; }

        public override void SetStaticDefaults() {
            SpeakerName = this.GetLocalization(nameof(SpeakerName), () => "VICTOR");
            ClinicButtonText = this.GetLocalization(nameof(ClinicButtonText), () => "Cyberware Clinic");
            ChatButtonText = this.GetLocalization(nameof(ChatButtonText), () => "Small Talk");
            LeaveButtonText = this.GetLocalization(nameof(LeaveButtonText), () => "Leave");
            //台词池（含旧 Greet 键）统一由 VictorDialogue 注册与分桶
            VictorDialogue.Register(this);
        }

        #endregion

        [VaultLoaden("CalamityOverhaul/Content/NPCs/Victors/Victor")]
        private static Asset<Texture2D> portraitAsset = null;

        protected override string SpeakerLabel => SpeakerName.Value;
        protected override Texture2D Portrait => portraitAsset?.Value;
        protected override int PortraitFrames => Victor.FrameCount;
        protected override string PickDialogueLine() => VictorDialogue.Pick();
        protected override double MoodFactor => VictorMood.PriceAdjustment;
        protected override string MoodReportText => VictorMood.Report;
        protected override string FormatPriceFactor(double factor)
            => VictorClinicUI.PriceFactorText.Format(factor.ToString("0.00"));

        protected override TalkCommand[] BuildCommands() => [
            new(() => ClinicButtonText.Value, CyberwareTheme.Accent, OpenClinic),
            new(() => ChatButtonText.Value, CyberwareTheme.AccentCyan, Chat),
            new(() => LeaveButtonText.Value, CyberwareTheme.AccentGold, Close),
        ];

        private void OpenClinic() {
            //静默关对话，只留诊所 OpenSound
            CloseSilent();
            VictorClinicUI.Instance.Open();
        }

        private void Chat() {
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.3f });
            RePickDialogue();
        }
    }
}
