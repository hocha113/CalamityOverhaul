using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    internal class MaterialOrganizerLeft : MaterialOrganizer
    {
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/TwoClick");
        protected override Vector2 offsetDraw => new Vector2(546, 330);
        protected override void ClickEvent() {
            SupertableUI.PlayGrabSound();
            mainUI.TakeAllItem();
            mainUI.FinalizeCraftingResult();
        }
    }
}
