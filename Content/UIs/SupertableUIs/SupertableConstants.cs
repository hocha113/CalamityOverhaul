namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    /// <summary>
    /// 超级工作台常量定义
    /// </summary>
    public static class SupertableConstants
    {
        //网格尺寸常量
        public const int CELL_WIDTH = 48;
        public const int CELL_HEIGHT = 46;
        public const int GRID_COLUMNS = 9;
        public const int GRID_ROWS = 9;
        public const int TOTAL_SLOTS = GRID_COLUMNS * GRID_ROWS; //81
        public const int RECIPE_LENGTH = TOTAL_SLOTS + 1; //81材料 + 1结果

        //UI动画常量
        public const float ANIMATION_SPEED_OPEN = 0.2f;
        public const float ANIMATION_SPEED_CLOSE = 0.14f;
        public const float HOVER_ANIMATION_SPEED = 0.1f;

        //音效音调常量
        public const float SOUND_PITCH_HIGH = 0.6f;
        public const float SOUND_PITCH_LOW = -0.5f;
        public const float SOUND_PITCH_CLOSE = -0.2f;

        //UI位置偏移常量
        public static readonly Vector2 MAIN_UI_OFFSET = new Vector2(16, 30);
        public static readonly Vector2 INPUT_SLOT_OFFSET = new Vector2(555, 215);
        public static readonly Vector2 CLOSE_BUTTON_OFFSET = new Vector2(0, 0);
        public static readonly Vector2 DRAG_BUTTON_OFFSET = new Vector2(554, 380);
        public static readonly Vector2 RECIPE_UI_OFFSET = new Vector2(545, 80);
        public static readonly Vector2 ORGANIZER_OFFSET = new Vector2(574, 330);
        public static readonly Vector2 ORGANIZER_LEFT_OFFSET = new Vector2(540, 330);
        public static readonly Vector2 HIGHLIGHTER_OFFSET = new Vector2(460, 420);

        //UI尺寸常量
        public const int INPUT_SLOT_SIZE = 92;
        public const int CLOSE_BUTTON_SIZE = 30;
        public const int SIDEBAR_WIDTH = 72;
        public const int SIDEBAR_ITEM_HEIGHT = 64;

        //空物品标识
        public const string NULL_ITEM_KEY = "Null/Null";
        public const string ZERO_ITEM_KEY = "0";
    }
}
