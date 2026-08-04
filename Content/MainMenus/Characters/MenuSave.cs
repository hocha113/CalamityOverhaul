using InnoVault.GameSystem;
using Terraria;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.MainMenus.Characters
{
    /// <summary>主菜单立绘存档</summary>
    internal class MenuSave : SaveMod
    {

        private const int CurrentDataVersion = 1;

        /// <summary>解锁"永恒燃烧的现在"结局立绘</summary>
        public static bool ADV_SupCal_EBN { get; private set; }

        public static Vector2 Helen_PortraitOffset { get; private set; } = Vector2.Zero;

        /// <summary>SupCal表情 0/1/2=Default/CloseEyes/Smile</summary>
        public static int SupCal_Expression { get; private set; } = 0;

        public static Vector2 SupCal_LeftPortraitOffset { get; private set; } = Vector2.Zero;

        public static Vector2 SupCal_RightPortraitOffset { get; private set; } = Vector2.Zero;

        public static bool SupCal_ShowFullPortrait { get; private set; } = false;

        public static float SupCal_LeftPortraitScale { get; private set; } = 2.0f;

        public static float SupCal_RightPortraitScale { get; private set; } = 0.85f;

        public override void SetStaticDefaults() {
            if (!HasSave) {
                DoSave<MenuSave>();
            }
            DoLoad<MenuSave>();
        }

        public override void SaveData(TagCompound tag) {

            tag["DataVersion"] = CurrentDataVersion;

            tag["ADV_SupCal_EBN"] = ADV_SupCal_EBN;
            tag["SupCal_Expression"] = SupCal_Expression;
            tag["SupCal_LeftPortraitOffset"] = SupCal_LeftPortraitOffset;
            tag["SupCal_RightPortraitOffset"] = SupCal_RightPortraitOffset;
            tag["SupCal_ShowFullPortrait"] = SupCal_ShowFullPortrait;
            tag["SupCal_LeftPortraitScale"] = SupCal_LeftPortraitScale;
            tag["SupCal_RightPortraitScale"] = SupCal_RightPortraitScale;
            tag["Helen_PortraitOffset"] = Helen_PortraitOffset;
        }

        public override void LoadData(TagCompound tag) {

            if (!tag.TryGet("DataVersion", out int dataVersion)) {
                dataVersion = 0;//旧档无版本戳
            }

            MigrateData(tag, dataVersion);

            LoadCurrentVersionData(tag);

            //加载后同步UI(若已init)
            if (ADV_SupCal_EBN) {
                SupCalPortraitUI.Instance?.LoadSavedState();
                HelenPortraitUI.Instance?.LoadSavedState();
            }
        }

        private static void MigrateData(TagCompound tag, int fromVersion) {

        }

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

            if (!tag.TryGet("SupCal_LeftPortraitScale", out float leftScale)) {
                leftScale = 2.0f;
            }
            SupCal_LeftPortraitScale = leftScale;

            if (!tag.TryGet("SupCal_RightPortraitScale", out float rightScale)) {
                rightScale = 0.85f;
            }
            SupCal_RightPortraitScale = rightScale;
        }

        /// <summary>达成结局时解锁主菜单立绘</summary>
        public static void UnlockEternalBlazingNowPortrait(Player player) {
            if (!ADV_SupCal_EBN) {
                ADV_SupCal_EBN = true;
                DoSave<MenuSave>();

                SupCalPortraitUI.Instance?.LoadSavedState();
                HelenPortraitUI.Instance?.LoadSavedState();
            }
        }

        public static bool IsPortraitUnlocked() => ADV_SupCal_EBN;

        public static void SaveSupCalPortraitState(int expression, Vector2 leftOffset, Vector2 rightOffset, bool showFullPortrait, float leftScale = 2.0f, float rightScale = 0.85f) {
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

            if (SupCal_LeftPortraitScale != leftScale) {
                SupCal_LeftPortraitScale = leftScale;
                changed = true;
            }

            if (SupCal_RightPortraitScale != rightScale) {
                SupCal_RightPortraitScale = rightScale;
                changed = true;
            }

            if (changed) {
                DoSave<MenuSave>();
            }
        }

        public static void SaveHelenPortraitState(Vector2 offset) {
            if (Helen_PortraitOffset != offset) {
                Helen_PortraitOffset = offset;
                DoSave<MenuSave>();
            }
        }

        public static void ResetPortraitPositions() {
            SupCal_LeftPortraitOffset = Vector2.Zero;
            SupCal_RightPortraitOffset = Vector2.Zero;
            Helen_PortraitOffset = Vector2.Zero;
            DoSave<MenuSave>();

            SupCalPortraitUI.Instance?.LoadSavedState();
            HelenPortraitUI.Instance?.LoadSavedState();
        }

        public static void ResetSupCalExpression() {
            SupCal_Expression = 0;
            DoSave<MenuSave>();

            SupCalPortraitUI.Instance?.LoadSavedState();
        }

        public static void ResetSupCalPortraitScale() {
            SupCal_LeftPortraitScale = 2.0f;
            SupCal_RightPortraitScale = 0.85f;
            DoSave<MenuSave>();

            SupCalPortraitUI.Instance?.LoadSavedState();
        }
    }
}

