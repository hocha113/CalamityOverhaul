using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV
{
    internal class DialogueSystem : ModSystem
    {
        public override void UpdateUI(GameTime gameTime) {
            DialogueUIRegistry.Current?.LogicUpdate();
            ADVRewardPopup.Instance?.LogicUpdate();
        }
    }
}