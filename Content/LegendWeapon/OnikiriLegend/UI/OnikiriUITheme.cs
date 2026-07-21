using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>鬼切 UI 色板与几何常量(绯红人间侧 / 鬼火青唯一冷色 / 焚烧暖焰)</summary>
    internal static class OnikiriUITheme
    {
        #region UI空间坐标（与调用语境无关）
        //UI 层已除 UIScale,逻辑帧仍是后台缓冲;跨语境布局用下面换算,禁直接读 Main.screenWidth
        /// <summary>UI 空间屏宽</summary>
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        /// <summary>UI 空间屏高</summary>
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        /// <summary>UI 空间屏尺寸</summary>
        public static Vector2 UIScreenSize => new(UIScreenW, UIScreenH);
        /// <summary>UI 空间鼠标</summary>
        public static Vector2 UIMouse => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Main.UIScale;
        #endregion

        //====人间侧 纸墨绯红(LDR,与 CrimsonSlashRenderer HDR 同源)====
        /// <summary>和纸白，正文主色</summary>
        public static readonly Color Paper = new(242, 234, 222);
        /// <summary>白热，刀痕前芯/强调</summary>
        public static readonly Color HotWhite = new(255, 243, 226);
        /// <summary>亮绯红，湿墨/扫光</summary>
        public static readonly Color Bright = new(255, 41, 26);
        /// <summary>深红，框线/绳结</summary>
        public static readonly Color Deep = new(158, 13, 18);
        /// <summary>暗酒红，阴影/底衬</summary>
        public static readonly Color Dark = new(41, 4, 9);
        /// <summary>墨黑，面板底</summary>
        public static readonly Color Ink = new(24, 12, 15);
        /// <summary>朱印红，印章体</summary>
        public static readonly Color Seal = new(186, 32, 26);
        /// <summary>焚烧暗部,炭边外暗红橙</summary>
        public static readonly Color BurnDim = new(184, 43, 12);
        /// <summary>焚烧高温芯,低蓝橙黄防过曝银白</summary>
        public static readonly Color BurnHot = new(255, 196, 64);

        //====鬼侧 鬼火青(唯一冷色)====
        /// <summary>鬼火亮青,眼/火苗芯</summary>
        public static readonly Color GhostFire = new(150, 226, 205);
        /// <summary>鬼火暗青,焰裙/余烬</summary>
        public static readonly Color GhostDim = new(62, 112, 104);

        //====文本====
        /// <summary>文本次色，来历/提示</summary>
        public static readonly Color TextDim = new(158, 134, 124);
        /// <summary>禁用/未知灰</summary>
        public static readonly Color Disabled = new(96, 78, 74);

        #region 点鬼簿几何
        /// <summary>卷轴纸面 shader 边沿外扩(墨缘侵蚀住在这一圈)</summary>
        public const int ScrollEdgePad = 14;
        /// <summary>卷轴宽度上限</summary>
        public const float ScrollMaxWidth = 560f;
        /// <summary>卷轴宽度占屏比</summary>
        public const float ScrollWidthRatio = 0.42f;
        /// <summary>卷轴中心 X 占屏比(偏左，右侧留给影绘细节板)</summary>
        public const float ScrollCenterXRatio = 0.32f;
        /// <summary>名录竖列宽度</summary>
        public const float EntryColumnW = 46f;
        /// <summary>名录竖列间距</summary>
        public const float EntryColumnGap = 16f;
        #endregion

        #region 封印札 HUD 几何
        /// <summary>HUD 绳结锚点,距左下角偏移</summary>
        public static readonly Vector2 HudAnchorOffset = new(64f, -168f);
        /// <summary>纸札宽</summary>
        public const float HudTalismanW = 34f;
        /// <summary>纸札高(修长的长条,存在感靠"长"不靠"宽")</summary>
        public const float HudTalismanH = 112f;
        /// <summary>绳结到纸札顶的绳长</summary>
        public const float HudRopeLen = 18f;
        #endregion

        #region 鬼域之眼 HUD 几何
        /// <summary>眼心相对绳结锚点的偏移(眼悬在上,整簇纸札挂在眼下)</summary>
        public static readonly Vector2 HudEyeOffset = new(0f, -27f);
        /// <summary>眼 quad 半尺寸(OniEye.fx 的眼形几乎铺满 quad 宽)</summary>
        public const float HudEyeHalf = 22f;
        /// <summary>眼的圆形命中半径</summary>
        public const float HudEyeHitRadius = 18f;
        /// <summary>HUD 队列上缘扩展:绳结到眼顶再留辉光余量(|HudEyeOffset.Y|+HudEyeHalf+余量)</summary>
        public const float HudEyeTopExtent = 56f;
        #endregion

        #region 气力墨脉 HUD 几何
        /// <summary>气力墨脉 quad 左上角相对绳结锚点的偏移(不随纸札摆角,战斗中读数不晃)</summary>
        public static readonly Vector2 HudVigorOffset = new(28f, 61f);
        /// <summary>气力墨脉 quad 宽(笔画本体约 184px,余量留给飞墨/洇边)</summary>
        public const float HudVigorQuadW = 204f;
        /// <summary>气力墨脉 quad 高(笔画核心约 16px,余量留给蒸散残丝)</summary>
        public const float HudVigorQuadH = 50f;
        /// <summary>笔画两端在 quad 内的横向留白(与 OniVigorInk.fx 的 padL/padR 同值)</summary>
        public const float HudVigorPad = 10f;
        /// <summary>墨脉形状种子,会话内恒定</summary>
        public const float HudVigorSeed = 7.31f;
        #endregion

        #region 底墨横扫 HUD 几何
        /// <summary>底墨 quad 左上角相对绳结锚点的偏移(X 越过屏幕左缘,笔自屏外扫入)</summary>
        public static readonly Vector2 HudInkWashOffset = new(-104f, -18f);
        /// <summary>底墨 quad 宽(右缘盖过鞘刀锋尖并留余量)</summary>
        public const float HudInkWashW = 356f;
        /// <summary>底墨 quad 高(上起绳结上方,下没过鞘刀下摆)</summary>
        public const float HudInkWashH = 176f;
        /// <summary>底墨形状种子:会话内恒定,画底不逐帧变形</summary>
        public const float HudInkWashSeed = 5.83f;
        #endregion

        #region 架势鞘刀 HUD 几何
        //柄头与墨脉朱印同左轨,锋尖齐墨尾,刀让开纸札摆动列
        /// <summary>鞘刀柄头中心相对绳结锚点的偏移(不随纸札摆角)</summary>
        public static readonly Vector2 HudStanceOffset = new(26f, 124f);
        /// <summary>鞘刀倾角(弧度),放平与墨脉同轨</summary>
        public const float HudStanceCant = 0f;
        /// <summary>柄长(柄头到镡)</summary>
        public const float HudStanceTsukaLen = 34f;
        /// <summary>刃/鞘段 quad 宽(镡到鞘尾)</summary>
        public const float HudStanceBladeW = 164f;
        /// <summary>刃/鞘段 quad 高(刀身核心约 10px,余量给刃口辉光与拔刀闪)</summary>
        public const float HudStanceBladeH = 36f;
        /// <summary>柄随架势后撤的最大距离(拔刀的第二动势;全撤时仍不进纸札摆动列)</summary>
        public const float HudStanceTsukaRecede = 8f;
        /// <summary>架势鞘刀形状种子(刃文/肌理,会话内恒定)</summary>
        public const float HudStanceSeed = 3.77f;
        #endregion

        /// <summary>异相位呼吸波 0~1</summary>
        public static float Breath(float time, float seed, float speed = 2f) {
            return (float)System.Math.Sin(time * speed + seed * 17.39f) * 0.5f + 0.5f;
        }
    }
}
