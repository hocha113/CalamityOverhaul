using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Gifts
{
    /// <summary>
    /// Shepel礼物场景抽象基类，重写持有者条件以检测SHPC而非比目鱼
    /// </summary>
    internal abstract class ShepelGiftScenarioBase : GiftScenarioBase
    {
        public override string LocalizationCategory => "ADV.Shepel";

        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SHPCDialogueBox.Instance;

        protected override bool CheckHolderCondition(ADVSave save, Player player) {
            if (!player.HasItem(SHPCOverride.ID)) {
                return false;
            }
            return save.Get<ShepelADVData>().FirstSHPCObtained;
        }

        protected static void ShowPortraitWithFace(ShepelFullBodyPortrait.Face face) {
            SHPCDialogueBox.Instance?.ShowFullBodyPortrait<ShepelFullBodyPortrait>();
            if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                portrait.SkipFadeIn();
                portrait.currentFace = face;
            }
        }

        protected static void SetPortraitFace(ShepelFullBodyPortrait.Face face) {
            if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait)
                portrait.currentFace = face;
        }
    }
}
