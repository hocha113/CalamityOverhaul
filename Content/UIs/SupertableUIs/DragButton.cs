using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    //拖拽按钮
    internal class DragButton : UIHandle
    {
        public override Texture2D Texture => CWRAsset.Placeholder_ERROR.Value;
        public static DragButton Instance => UIHandleLoader.GetUIHandleOfType<DragButton>();
        public override float RenderPriority => 0.5f;
        public override bool Active {
            get {
                if (SupertableUI.Instance == null) {
                    return false;
                }
                return SupertableUI.Instance.Active;
            }
        }
        public Vector2 SupPos => SupertableUI.Instance.DrawPosition;
        public Vector2 InSupPosOffset => new Vector2(554, 380);
        public Vector2 InPosOffsetDragToPos;
        public Vector2 DragVelocity;
        public static int DontDragTime;
        public static bool OnDrag;
        public void Initialize() {
            if (DontDragTime > 0) {
                DontDragTime--;
            }
            DrawPosition = SupertableUI.Instance.DrawPosition + InSupPosOffset;
            hoverInMainPage = SupertableUI.Instance.hoverInMainPage && DontDragTime <= 0;
            if (Main.mouseItem.type > ItemID.None && SupertableUI.Instance.hoverInPutItemCellPage) {
                DontDragTime = 2;
                OnDrag = false;
                hoverInMainPage = false;
            }
        }
        public override void Update() {
            if (SupertableUI.Instance == null) {
                return;
            }
            Initialize();
            if (hoverInMainPage) {
                if (keyLeftPressState == KeyPressState.Pressed && !OnDrag) {
                    OnDrag = true;
                    InPosOffsetDragToPos = DrawPosition.To(MousePosition);
                }
            }
            if (OnDrag) {
                if (keyLeftPressState == KeyPressState.Released) {
                    OnDrag = false;
                }
                DragVelocity = (DrawPosition + InPosOffsetDragToPos).To(MousePosition);
                SupertableUI.Instance.DrawPosition += DragVelocity;
            }
            else {
                DragVelocity = Vector2.Zero;
            }
            Prevention();
        }
        public void Prevention() {
            if (SupertableUI.Instance.DrawPosition.X < 0) {
                SupertableUI.Instance.DrawPosition.X = 0;
            }
            if (SupertableUI.Instance.DrawPosition.X + SupertableUI.Instance.Texture.Width > Main.screenWidth) {
                SupertableUI.Instance.DrawPosition.X = Main.screenWidth - SupertableUI.Instance.Texture.Width;
            }
            if (SupertableUI.Instance.DrawPosition.Y < 0) {
                SupertableUI.Instance.DrawPosition.Y = 0;
            }
            if (SupertableUI.Instance.DrawPosition.Y + SupertableUI.Instance.Texture.Height > Main.screenHeight) {
                SupertableUI.Instance.DrawPosition.Y = Main.screenHeight - SupertableUI.Instance.Texture.Height;
            }
        }
    }
}
