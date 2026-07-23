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

        //====改铭台侧 金象嵌(本屏专属 accent,不进点鬼簿)====
        /// <summary>金象嵌亮部,填缝/点亮/扇骨高光</summary>
        public static readonly Color GoldInlay = new(218, 172, 82);
        /// <summary>金象嵌暗部,氧化边/衬底</summary>
        public static readonly Color GoldDeep = new(140, 96, 34);
        /// <summary>烛焰暖色,底缘光源(与细节板背光同源)</summary>
        public static readonly Color CandleWarm = new(226, 160, 108);

        //====文本====
        /// <summary>文本次色，来历/提示</summary>
        public static readonly Color TextDim = new(158, 134, 124);
        /// <summary>禁用/未知灰</summary>
        public static readonly Color Disabled = new(96, 78, 74);

        #region 改铭台几何
        /// <summary>刀身 quad 宽占屏比</summary>
        public const float MeiBladeWidthRatio = 0.72f;
        /// <summary>刀身 quad 宽上限</summary>
        public const float MeiBladeMaxW = 1150f;
        /// <summary>刀身 quad 高(刀身核心约 46px,余量给刃辉/字形/烛照)</summary>
        public const float MeiBladeQuadH = 190f;
        /// <summary>刀心占屏比(横陈偏上,下方留给鏨盘扇与木牌)</summary>
        public static readonly Vector2 MeiBladeCenterRatio = new(0.46f, 0.40f);
        /// <summary>横陈微倾(弧度),轴向锋(左)→茎(右),正值=锋侧略抬——鉴刀不摆正</summary>
        public const float MeiBladeCant = 0.03f;
        /// <summary>茎段占刀长比(右侧,裸茎见锉痕与锈)</summary>
        public const float MeiTangFraction = 0.24f;
        /// <summary>樋位归一位置(锋 0 → 茎尾 1)</summary>
        public const float MeiSlotUHi = 0.26f;
        /// <summary>雕位归一位置</summary>
        public const float MeiSlotUHorimono = 0.52f;
        /// <summary>茎铭位归一位置(落在裸茎上)</summary>
        public const float MeiSlotUNakago = 0.875f;
        /// <summary>铭位命中半径</summary>
        public const float MeiSlotRadius = 34f;
        /// <summary>刀上铭字形尺寸</summary>
        public const float MeiGlyphOnBlade = 54f;
        /// <summary>鏨盘扇骨长(轴心到纹章心)</summary>
        public const float MeiFanRibLen = 150f;
        /// <summary>扇骨纹章尺寸</summary>
        public const float MeiFanGlyphSize = 44f;
        /// <summary>扇面全张角(弧度)</summary>
        public const float MeiFanSpread = 1.55f;
        /// <summary>烙印木牌尺寸</summary>
        public static readonly Vector2 MeiTagSize = new(380f, 196f);
        /// <summary>右缘竖排大字中轴 X 占屏比</summary>
        public const float MeiNameColXRatio = 0.905f;
        /// <summary>竖排大字字号</summary>
        public const float MeiNameScale = 1.6f;
        /// <summary>白布横幅高</summary>
        public const float MeiClothH = 330f;
        /// <summary>刀身形状种子(肌理/刃文/锈斑,会话内恒定)</summary>
        public const float MeiBladeSeed = 11.73f;
        #endregion

        #region 吊挂切换门
        /// <summary>梁下微缩物整体倍率(门要一眼看见,别缩成页签)</summary>
        public const float HangSwitchScale = 2.25f;
        /// <summary>点鬼簿屏上挂轴命中外包</summary>
        public static readonly Vector2 HangScrollHit = new(76f, 207f);
        /// <summary>改铭台屏上挂刀命中外包</summary>
        public static readonly Vector2 HangTachiHit = new(68f, 225f);
        #endregion

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
