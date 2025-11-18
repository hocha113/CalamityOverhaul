using InnoVault.GameSystem;
using Terraria;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV.MainMenuOvers
{
    /// <summary>
    /// 主菜单相关的持久化数据管理
    /// 用于保存跨世界的解锁状态(如立绘解锁)
    /// </summary>
    internal class MenuSave : SaveMod
    {
        //当前数据版本号(修改数据结构时递增此版本号)
        private const int CurrentDataVersion = 1;

        /// <summary>
        /// 是否解锁了"永恒燃烧的现在"结局的主菜单立绘
        /// </summary>
        public static bool ADV_SupCal_EBN { get; private set; }

        /// <summary>
        /// Helen立绘的偏移位置
        /// </summary>
        public static Vector2 Helen_PortraitOffset { get; private set; } = Vector2.Zero;

        /// <summary>
        /// SupCal立绘的表情状态(0=Default, 1=CloseEyes, 2=Smile)
        /// </summary>
        public static int SupCal_Expression { get; private set; } = 0;

        /// <summary>
        /// SupCal左侧立绘的偏移位置
        /// </summary>
        public static Vector2 SupCal_LeftPortraitOffset { get; private set; } = Vector2.Zero;

        /// <summary>
        /// SupCal右侧立绘的偏移位置
        /// </summary>
        public static Vector2 SupCal_RightPortraitOffset { get; private set; } = Vector2.Zero;

        /// <summary>
        /// SupCal立绘的显示状态
        /// </summary>
        public static bool SupCal_ShowFullPortrait { get; private set; } = false;

        public override void SetStaticDefaults() {
            if (!HasSave) {
                DoSave<MenuSave>();
            }
            DoLoad<MenuSave>();
        }

        public override void SaveData(TagCompound tag) {
            //保存版本戳
            tag["DataVersion"] = CurrentDataVersion;

            //保存所有数据
            tag["ADV_SupCal_EBN"] = ADV_SupCal_EBN;
            tag["SupCal_Expression"] = SupCal_Expression;
            tag["SupCal_LeftPortraitOffset"] = SupCal_LeftPortraitOffset;
            tag["SupCal_RightPortraitOffset"] = SupCal_RightPortraitOffset;
            tag["SupCal_ShowFullPortrait"] = SupCal_ShowFullPortrait;
            tag["Helen_PortraitOffset"] = Helen_PortraitOffset;
        }

        public override void LoadData(TagCompound tag) {
            //读取版本号
            if (!tag.TryGet("DataVersion", out int dataVersion)) {
                dataVersion = 0; //旧版本存档没有版本戳,默认为0
            }

            //根据版本号进行数据迁移
            MigrateData(tag, dataVersion);

            //加载数据(始终使用最新格式)
            LoadCurrentVersionData(tag);

            //加载后立即同步到UI状态(如果UI已初始化)
            if (ADV_SupCal_EBN) {
                SupCalPortraitUI.Instance?.LoadSavedState();
                HelenPortraitUI.Instance?.LoadSavedState();
            }
        }

        /// <summary>
        /// 数据迁移逻辑
        /// </summary>
        private static void MigrateData(TagCompound tag, int fromVersion) {

        }

        /// <summary>
        /// 加载当前版本的数据
        /// </summary>
        private static void LoadCurrentVersionData(TagCompound tag) {
            if (!tag.TryGet("Helen_PortraitOffset", out Vector2 helenOffset)) {
                helenOffset = Vector2.Zero;
            }
            Helen_PortraitOffset = helenOffset;

            if (!tag.TryGet("ADV_SupCal_EBN", out bool unlocked)) {
                unlocked = false;
            }
            ADV_SupCal_EBN = unlocked;

            if (!tag.TryGet("SupCal_Expression", out int expression)) {
                expression = 0;
            }
            SupCal_Expression = expression;

            if (!tag.TryGet("SupCal_LeftPortraitOffset", out Vector2 leftOffset)) {
                leftOffset = Vector2.Zero;
            }
            SupCal_LeftPortraitOffset = leftOffset;

            if (!tag.TryGet("SupCal_RightPortraitOffset", out Vector2 rightOffset)) {
                rightOffset = Vector2.Zero;
            }
            SupCal_RightPortraitOffset = rightOffset;

            if (!tag.TryGet("SupCal_ShowFullPortrait", out bool showFullPortrait)) {
                showFullPortrait = false;
            }
            SupCal_ShowFullPortrait = showFullPortrait;
        }

        /// <summary>
        /// 当玩家达成"永恒燃烧的现在"结局时调用
        /// 解锁主菜单立绘
        /// </summary>
        public static void UnlockEternalBlazingNowPortrait(Player player) {
            if (!ADV_SupCal_EBN) {
                ADV_SupCal_EBN = true;
                DoSave<MenuSave>();

                //立即更新UI状态
                SupCalPortraitUI.Instance?.LoadSavedState();
                HelenPortraitUI.Instance?.LoadSavedState();
            }
        }

        /// <summary>
        /// 检查玩家是否已解锁主菜单立绘
        /// </summary>
        public static bool IsPortraitUnlocked() => ADV_SupCal_EBN;

        /// <summary>
        /// 保存SupCal立绘的UI状态
        /// </summary>
        public static void SaveSupCalPortraitState(int expression, Vector2 leftOffset, Vector2 rightOffset, bool showFullPortrait) {
            bool changed = false;

            if (SupCal_Expression != expression) {
                SupCal_Expression = expression;
                changed = true;
            }

            if (SupCal_LeftPortraitOffset != leftOffset) {
                SupCal_LeftPortraitOffset = leftOffset;
                changed = true;
            }

            if (SupCal_RightPortraitOffset != rightOffset) {
                SupCal_RightPortraitOffset = rightOffset;
                changed = true;
            }

            if (SupCal_ShowFullPortrait != showFullPortrait) {
                SupCal_ShowFullPortrait = showFullPortrait;
                changed = true;
            }

            if (changed) {
                DoSave<MenuSave>();
            }
        }

        /// <summary>
        /// 保存Helen立绘的UI状态
        /// </summary>
        public static void SaveHelenPortraitState(Vector2 offset) {
            if (Helen_PortraitOffset != offset) {
                Helen_PortraitOffset = offset;
                DoSave<MenuSave>();
            }
        }

        /// <summary>
        /// 重置所有立绘位置到默认状态
        /// </summary>
        public static void ResetPortraitPositions() {
            SupCal_LeftPortraitOffset = Vector2.Zero;
            SupCal_RightPortraitOffset = Vector2.Zero;
            Helen_PortraitOffset = Vector2.Zero;
            DoSave<MenuSave>();

            //立即同步到UI
            SupCalPortraitUI.Instance?.LoadSavedState();
            HelenPortraitUI.Instance?.LoadSavedState();
        }

        /// <summary>
        /// 重置SupCal表情到默认状态
        /// </summary>
        public static void ResetSupCalExpression() {
            SupCal_Expression = 0;
            DoSave<MenuSave>();

            //立即同步到UI
            SupCalPortraitUI.Instance?.LoadSavedState();
        }
    }
}
