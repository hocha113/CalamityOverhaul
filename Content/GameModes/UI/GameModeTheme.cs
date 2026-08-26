using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.GameModes.UI
{
    /// <summary>
    /// 游戏模式标签的色板与布局唯一来源。
    /// 跨语境布局一律走 UI 空间访问器，禁止直读 <see cref="Main.screenWidth"/>
    /// </summary>
    internal static class GameModeTheme
    {
        /// <summary>UI 空间下的屏幕宽</summary>
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;

        /// <summary>UI 空间下的屏幕高</summary>
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        //——布局：背包右缘的模式标签行（任务书 launcher 默认在 (572,108)，标签行放它上方并水平展开）——

        public const int TabW = 46;
        public const int TabH = 58;
        public const int TabGapX = 14;

        /// <summary>标签行锚点（残酷标签左上角）</summary>
        public static readonly Point TabAnchor = new(578, 22);

        /// <summary>残酷模式标签矩形</summary>
        public static Rectangle BrutalTab => new(TabAnchor.X, TabAnchor.Y, TabW, TabH);

        /// <summary>修罗模式标签矩形；reveal 0..1 表示从残酷标签背后滑出的进度</summary>
        public static Rectangle AsuraTab(float reveal) {
            int x = TabAnchor.X + (int)((TabW + TabGapX) * reveal);
            return new Rectangle(x, TabAnchor.Y, TabW, TabH);
        }

        /// <summary>神匠模式标签矩形；reveal 0..1 表示滑向第三席位的进度（错帧跟在修罗之后）</summary>
        public static Rectangle GodSmithTab(float reveal) {
            int x = TabAnchor.X + (int)((TabW + TabGapX) * 2 * reveal);
            return new Rectangle(x, TabAnchor.Y, TabW, TabH);
        }

        //——色板：残酷=血红族，修罗=黑金紫红族，毁灭=苍银冷白族，神匠=熔金鎏金族；shader 背景与 CPU 前景同族取色——

        /// <summary>近黑底色</summary>
        public static readonly Color NightBase = new(14, 10, 12);
        /// <summary>残酷主 accent（血红）</summary>
        public static readonly Color BrutalAccent = new(214, 46, 40);
        /// <summary>残酷余烬暖色</summary>
        public static readonly Color BrutalEmber = new(255, 122, 58);
        /// <summary>修罗主 accent（紫红）</summary>
        public static readonly Color AsuraAccent = new(176, 54, 152);
        /// <summary>修罗描金</summary>
        public static readonly Color AsuraGold = new(224, 176, 92);
        /// <summary>毁灭主 accent（苍银，死神的褪色相）</summary>
        public static readonly Color AnnihilationAccent = new(186, 196, 212);
        /// <summary>毁灭冷白余烬</summary>
        public static readonly Color AnnihilationEmber = new(240, 246, 255);
        /// <summary>神匠主 accent（熔金橙，出炉铁水色）</summary>
        public static readonly Color GodSmithAccent = new(232, 146, 38);
        /// <summary>神匠鎏金余烬（近白的鎏金亮）</summary>
        public static readonly Color GodSmithEmber = new(255, 226, 142);
        /// <summary>休眠态骨灰色</summary>
        public static readonly Color BoneDim = new(118, 106, 100);

        /// <summary>表现脸的主 accent</summary>
        public static Color Accent(GameModeFace face) => face switch {
            GameModeFace.Brutal => BrutalAccent,
            GameModeFace.Annihilation => AnnihilationAccent,
            GameModeFace.GodSmith => GodSmithAccent,
            _ => AsuraAccent,
        };

        /// <summary>表现脸的余烬色</summary>
        public static Color Ember(GameModeFace face) => face switch {
            GameModeFace.Brutal => BrutalEmber,
            GameModeFace.Annihilation => AnnihilationEmber,
            GameModeFace.GodSmith => GodSmithEmber,
            _ => AsuraGold,
        };
    }
}
