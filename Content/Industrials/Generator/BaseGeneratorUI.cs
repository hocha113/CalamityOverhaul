using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Industrials.Generator
{
    public abstract class BaseGeneratorUI : UIHandle
    {
        internal bool IsActive;
        public override bool Active => IsActive;
        private bool onDrag;
        private Vector2 dragOffset;
        internal BaseGeneratorTP GeneratorTP;
        public override Texture2D Texture => CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/GeneratorPanel");
        public sealed override void Update() {
            UIHitBox = DrawPosition.GetRectangle(Texture.Size());
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1));

            UpdateElement();

            if (hoverInMainPage) {
                if (keyRightPressState == KeyPressState.Pressed) {
                    if (!onDrag) {
                        dragOffset = MousePosition.To(DrawPosition);
                    }
                    onDrag = true;
                }
            }
            if (onDrag) {
                DrawPosition = MousePosition + dragOffset;
                if (keyRightPressState == KeyPressState.Released) {
                    onDrag = false;
                }
            }
        }

        public virtual void RightClickByTile(bool newTP) {

        }

        public virtual void ByTPCloaseFunc() {

        }

        public virtual void UpdateElement() {

        }
    }
}
