using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    internal class MaterialOrganizerLeft : MaterialOrganizer
    {
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/TwoClick");
        protected override Vector2 offsetDraw => new Vector2(540, 330);
        protected override void ClickEvent() {
            SoundEngine.PlaySound(SoundID.Grab);
            mainUI.TakeAllItem();
            mainUI.FinalizeCraftingResult();
        }
    }
}
