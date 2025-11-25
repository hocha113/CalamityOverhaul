using InnoVault.UIHandles;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs.UIContent
{
    /// <summary>
    /// 拖拽控制器 - 负责UI的拖拽移动
    /// </summary>
    internal class DragController
    {
        private readonly SupertableUI _mainUI;
        private static int _globalDontDragTime;

        private Vector2 _dragOffset;
        private bool _isDragging;

        public DragController(SupertableUI mainUI) {
            _mainUI = mainUI;
        }

        public void Update() {
            if (_globalDontDragTime > 0) {
                _globalDontDragTime--;
            }

            Vector2 dragHandlePos = _mainUI.DrawPosition + SupertableConstants.DRAG_BUTTON_OFFSET;
            Rectangle dragRect = new Rectangle((int)dragHandlePos.X, (int)dragHandlePos.Y, 50, 50);
            bool hoverDragHandle = dragRect.Intersects(_mainUI.MouseHitBox) && _globalDontDragTime <= 0;

            if (Main.mouseItem.type > ItemID.None && _mainUI.HoverInPutItemCellPage) {
                _globalDontDragTime = 2;
                _isDragging = false;
                return;
            }

            if (hoverDragHandle && _mainUI.keyLeftPressState == KeyPressState.Pressed && !_isDragging) {
                _isDragging = true;
                _dragOffset = _mainUI.MousePosition - dragHandlePos;
            }

            if (_isDragging) {
                if (_mainUI.keyLeftPressState == KeyPressState.Released) {
                    _isDragging = false;
                }
                else {
                    Vector2 targetPos = _mainUI.MousePosition - _dragOffset - SupertableConstants.DRAG_BUTTON_OFFSET;
                    _mainUI.DrawPosition = ClampToScreen(targetPos);
                }
            }
        }

        private Vector2 ClampToScreen(Vector2 position) {
            float x = MathHelper.Clamp(position.X, 0, Main.screenWidth - _mainUI.Texture.Width);
            float y = MathHelper.Clamp(position.Y, 0, Main.screenHeight - _mainUI.Texture.Height);
            return new Vector2(x, y);
        }

        public void SetDontDragTime(int frames) {
            _globalDontDragTime = frames;
        }

        public static void SetGlobalDontDragTime(int frames) {
            _globalDontDragTime = frames;
        }

        public bool IsDragging => _isDragging;
    }
}
