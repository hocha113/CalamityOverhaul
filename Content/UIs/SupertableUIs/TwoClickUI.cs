using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    internal class TwoClickUI : OneClickUI
    {
        public new static TwoClickUI Instance;
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/TwoClick");
        public override void Load() => Instance = this;
        protected override Vector2 offsetDraw => new Vector2(548, 300);
        protected override void ClickEvent() {
            SupertableUI.PlayGrabSound();
            mainUI.TakeAllItem();
            mainUI.OutItem();
        }
    }
}
