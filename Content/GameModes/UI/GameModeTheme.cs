using System;
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

        /// <summary>标签行最大席位数（残酷/修罗/神匠）</summary>
        public const int TabSeats = 3;

        /// <summary>行右缘留白</summary>
        private const int RowMarginX = 10;

        /// <summary>标签行锚点（残酷标签左上角）</summary>
        public static readonly Point TabAnchor = new(578, 22);

        /// <summary>满席时的整行宽</summary>
        public static int RowWidth => TabW * TabSeats + TabGapX * (TabSeats - 1);

        /// <summary>
        /// 整行水平让位：UI 空间窄到装不下末席时整行左移，右缘留一点余白。
        /// 引擎把 UIScaleMax 钳到屏宽/800，UI 空间宽恒 ≥ 800，当前锚点下这里恒为 0；
        /// 它是锚点或席位数日后变动时的兜底，不是眼下能复现的修正
        /// </summary>
        public static int RowShiftX {
            get {
                float overflow = TabAnchor.X + RowWidth + RowMarginX - UIScreenW;
                if (overflow <= 0f) {
                    return 0;
                }
                //左移不越过屏幕左缘
                return -Math.Min((int)MathF.Ceiling(overflow), TabAnchor.X - 8);
            }
        }

        /// <summary>残酷模式标签矩形</summary>
        public static Rectangle BrutalTab => SeatTab(0f);

        /// <summary>修罗模式标签矩形；reveal 0..1 表示从残酷标签背后滑出的进度</summary>
        public static Rectangle AsuraTab(float reveal) => SeatTab(reveal);

        /// <summary>
        /// 神匠模式标签矩形：独立开关，恒占修罗之后的一席。
        /// asuraReveal 0..1 是修罗的滑出进度，神匠始终比它靠右一席，两旗在滑轨上不会交叠
        /// </summary>
        public static Rectangle GodSmithTab(float asuraReveal) => SeatTab(1f + asuraReveal);

        /// <summary>按席位号（可为小数，滑轨插值）取标签矩形</summary>
        private static Rectangle SeatTab(float seat)
            => new(TabAnchor.X + RowShiftX + (int)((TabW + TabGapX) * seat), TabAnchor.Y, TabW, TabH);

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
        /// <summary>悬停面板正文灰白</summary>
        public static readonly Color TextBody = new(212, 205, 198);
        /// <summary>拒绝/锁定警示红</summary>
        public static readonly Color DangerRed = new(226, 74, 64);

        //——Boss 锁定封条的冷钢族（锁链/挂锁）：中性钢色，不与任何模式旗色抢戏——

        /// <summary>锁链正面环暗钢</summary>
        public static readonly Color ChainSteel = new(62, 58, 70);
        /// <summary>锁链侧立环沉钢</summary>
        public static readonly Color ChainSteelDark = new(46, 43, 52);
        /// <summary>锁链受光亮缘</summary>
        public static readonly Color ChainSteelLit = new(142, 136, 152);
        /// <summary>挂锁体填充</summary>
        public static readonly Color LockBodyFill = new(44, 41, 50);
        /// <summary>挂锁顶缘受光</summary>
        public static readonly Color LockBevel = new(150, 145, 160);
        /// <summary>锁孔近黑（兼作挂锁底缘沉影）</summary>
        public static readonly Color KeyholeDark = new(8, 7, 10);
        /// <summary>链上巡行冷光</summary>
        public static readonly Color ChainGlint = new(214, 218, 230);

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
