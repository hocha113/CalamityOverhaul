using CalamityOverhaul.Content.Cyberwares.UIs;
using CalamityOverhaul.Content.NPCs.CommonUIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.TBUGs.UIs
{
    /// <summary>
    /// TBUG 对话条，右键开；布局与皮肤归 <see cref="NPCTalkUIBase"/>，
    /// 与维克托共用一张脸，这里只装配台词池/心情/命令项
    /// </summary>
    internal class TBUGTalkUI : NPCTalkUIBase, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static TBUGTalkUI Instance => UIHandleLoader.GetUIHandleOfType<TBUGTalkUI>();

        #region 本地化

        public static LocalizedText SpeakerName { get; private set; }
        public static LocalizedText ShopButtonText { get; private set; }
        public static LocalizedText ChatButtonText { get; private set; }
        public static LocalizedText LeaveButtonText { get; private set; }
        public static LocalizedText PriceFactorText { get; private set; }

        public override void SetStaticDefaults() {
            SpeakerName = this.GetLocalization(nameof(SpeakerName), () => "TBUG");
            ShopButtonText = this.GetLocalization(nameof(ShopButtonText), () => "Hack Shop");
            ChatButtonText = this.GetLocalization(nameof(ChatButtonText), () => "Small Talk");
            LeaveButtonText = this.GetLocalization(nameof(LeaveButtonText), () => "Leave");
            PriceFactorText = this.GetLocalization(nameof(PriceFactorText), () => "PRICE x{0}");
            //台词池统一由 TBUGDialogue 注册与分桶
            TBUGDialogue.Register(this);
        }

        #endregion

        [VaultLoaden("CalamityOverhaul/Content/NPCs/TBUGs/TBUG")]
        private static Asset<Texture2D> portraitAsset = null;

        protected override string SpeakerLabel => SpeakerName.Value;
        protected override Texture2D Portrait => portraitAsset?.Value;
        protected override int PortraitFrames => TBUG.FrameCount;
        protected override string PickDialogueLine() => TBUGDialogue.Pick();
        protected override double MoodFactor => TBUGMood.PriceAdjustment;
        protected override string MoodReportText => TBUGMood.Report;
        protected override string FormatPriceFactor(double factor)
            => PriceFactorText.Format(factor.ToString("0.00"));

        //绑定的 TBUG 没了（被杀/消失）就收窗
        protected override bool SessionAlive => TBUGSession.IsBoundNPCAlive();

        protected override void OnClose() => TBUGSession.MaybeEndSession();

        protected override TalkCommand[] BuildCommands() => [
            new(() => ShopButtonText.Value, CyberwareTheme.Accent, OpenShop),
            new(() => ChatButtonText.Value, CyberwareTheme.AccentCyan, Chat),
            new(() => LeaveButtonText.Value, CyberwareTheme.AccentGold, Close),
        ];

        private void OpenShop() {
            //静默关对话，只留商店 OpenSound；关闭回调会清会话，先存再重绑
            int who = TBUGSession.BoundWhoAmI;
            CloseSilent();
            TBUGSession.Bind(who);
            TBUGShopUI.Instance.Open();
        }

        private void Chat() {
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.3f });
            RePickDialogue();
        }
    }
}
