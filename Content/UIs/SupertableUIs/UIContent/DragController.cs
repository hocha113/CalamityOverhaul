using InnoVault.UIHandles;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs.UIContent
{
    /// <summary>
    /// 拖拽控制器，负责UI的拖拽移动
    /// </summary>
    internal class DragController
    {
        private readonly SupertableUI _mainUI;
        private static int _globalDontDragTime;

        private Vector2 _dragOffset;
        private bool _isDragging;
        private Rectangle _dragArea;

        public bool IsDragging => _isDragging;
        public Rectangle DragArea => _dragArea;

        public DragController(SupertableUI mainUI) {
            _mainUI = mainUI;
        }

        public void Update() {
            if (_globalDontDragTime > 0) {
                _globalDontDragTime--;
            }

            //检查鼠标是否在拖拽区域内
            bool hoverDragHandle =
                                   _mainUI.hoverInMainPage; //确保在主UI范围内

            //如果鼠标拿着物品且在材料格子区域，禁止拖拽
            if (Main.mouseItem.type > ItemID.None && _mainUI.HoverInPutItemCellPage) {
                _globalDontDragTime = 2;
                _isDragging = false;
                return;
            }

            //开始拖拽
            if (hoverDragHandle && _mainUI.keyLeftPressState == KeyPressState.Pressed && !_isDragging) {
                _isDragging = true;
                _dragOffset = _mainUI.MousePosition - _mainUI.DrawPosition;
            }

            //拖拽过程
            if (_isDragging) {
                if (_mainUI.keyLeftPressState == KeyPressState.Released) {
                    _isDragging = false;
                }
                else {
                    //直接根据鼠标位置和偏移计算新位置
                    Vector2 targetPos = _mainUI.MousePosition - _dragOffset;
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
    }
}
