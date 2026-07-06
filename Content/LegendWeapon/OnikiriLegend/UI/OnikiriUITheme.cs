using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 鬼切 UI 色板与几何常量。<br/>
    /// 绯红家族(纸/墨/朱)为人间侧——刀、驭鬼者意志、簿与契；<br/>
    /// 鬼火青为界面唯一冷色,专属于"鬼"——鬼影之眼、失控预兆、焦边燃焰。<br/>
    /// 驾驭度的拉锯直接用"朱红压青焰"讲
    /// </summary>
    internal static class OnikiriUITheme
    {
        #region UI空间坐标（与调用语境无关）
        //UIHandle 的 Update/Draw 运行在 InterfaceScaleType.UI 层内，此时 Main.screenWidth 已被
        //除以 UIScale；但 ModPlayer.PostUpdate 等逻辑帧里它是原始后台缓冲尺寸。
        //任何跨语境的布局计算都必须使用下面这组换算，禁止直接读 Main.screenWidth/Height
        /// <summary>UI空间下的屏幕宽度（任何调用语境下取值一致）</summary>
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        /// <summary>UI空间下的屏幕高度（任何调用语境下取值一致）</summary>
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        /// <summary>UI空间下的屏幕尺寸</summary>
        public static Vector2 UIScreenSize => new(UIScreenW, UIScreenH);
        /// <summary>UI空间下的鼠标位置（任何调用语境下取值一致）</summary>
        public static Vector2 UIMouse => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Main.UIScale;
        #endregion

        //====人间侧：纸墨绯红(LDR，与 CrimsonSlashRenderer 四色 HDR 同源)====
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

        //====鬼侧：鬼火青(界面唯一冷色)====
        /// <summary>鬼火亮青：鬼影之眼/火苗芯</summary>
        public static readonly Color GhostFire = new(150, 226, 205);
        /// <summary>鬼火暗青：焰裙/余烬</summary>
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
        /// <summary>HUD 绳结锚点：距屏幕左下角的偏移</summary>
        public static readonly Vector2 HudAnchorOffset = new(64f, -168f);
        /// <summary>纸札宽</summary>
        public const float HudTalismanW = 34f;
        /// <summary>纸札高(修长的长条,存在感靠"长"不靠"宽")</summary>
        public const float HudTalismanH = 112f;
        /// <summary>绳结到纸札顶的绳长</summary>
        public const float HudRopeLen = 18f;
        #endregion

        /// <summary>异相位呼吸波，给定相位种子返回 0-1 的缓慢脉动</summary>
        public static float Breath(float time, float seed, float speed = 2f) {
            return (float)System.Math.Sin(time * speed + seed * 17.39f) * 0.5f + 0.5f;
        }
    }
}
