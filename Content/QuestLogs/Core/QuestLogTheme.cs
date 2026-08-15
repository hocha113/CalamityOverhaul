using System;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    /// <summary>
    /// 任务书的 UI 空间换算、全屏分区几何与共用小工具
    /// </summary>
    public static class QuestLogTheme
    {
        #region UI 空间坐标

        //UIHandle 的 Update/Draw 跑在 InterfaceScaleType.UI 层里，此时 Main.screenWidth 已被 UIScale 除过；
        //逻辑帧（ModPlayer/ModSystem）读到的却是原始后台缓冲尺寸。
        //跨语境的布局一律走下面这组换算，禁止直读 Main.screenWidth/Height

        /// <summary>UI 空间下的屏幕宽</summary>
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;

        /// <summary>UI 空间下的屏幕高</summary>
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        /// <summary>UI 空间下的屏幕尺寸</summary>
        public static Vector2 UIScreenSize => new(UIScreenW, UIScreenH);

        /// <summary>UI 空间下的鼠标位置</summary>
        public static Vector2 UIMouse => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Main.UIScale;

        #endregion

        #region 分区几何

        /// <summary>页眉高</summary>
        public const int HeaderH = 62;

        /// <summary>左栏宽，站点页签与章节标尺</summary>
        public const int RailW = 172;

        /// <summary>右侧详情栏宽</summary>
        public const int DetailW = 408;

        /// <summary>页脚高，操作提示与总控</summary>
        public const int FooterH = 48;

        /// <summary>画布最小宽，窄屏时详情栏让位到这个下限</summary>
        public const int CanvasMinW = 360;

        /// <summary>左栏站点页签高</summary>
        public const int RailTabH = 32;

        /// <summary>左栏站点页签间距</summary>
        public const int RailTabGap = 7;

        /// <summary>第 index 个站点页签的命中区，容器判定与样式绘制共用</summary>
        public static Rectangle RailTab(in QuestLogLayout layout, int index)
            => new(layout.Rail.X + 16, layout.Rail.Y + 16 + index * (RailTabH + RailTabGap),
                Math.Max(40, layout.Rail.Width - 34), RailTabH);

        /// <summary>左栏页签之下的内容起点 Y</summary>
        public static float RailContentTop(in QuestLogLayout layout, int tabCount)
            => layout.Rail.Y + 16 + tabCount * (RailTabH + RailTabGap) + 14;

        /// <summary>章目行高</summary>
        public const int RailChapterH = 25;

        /// <summary>图例区高，钉在左栏底部</summary>
        public const int RailLegendH = 186;

        /// <summary>第 index 条章目的命中区</summary>
        public static Rectangle RailChapter(in QuestLogLayout layout, int index)
            => new(layout.Rail.X + 18, (int)RailContentTop(in layout, 2) + 24 + index * RailChapterH,
                Math.Max(40, layout.Rail.Width - 34), RailChapterH - 3);

        /// <summary>左栏容得下几条章目</summary>
        public static int RailChapterCapacity(in QuestLogLayout layout) {
            float top = RailContentTop(in layout, 2) + 24;
            float bottom = layout.Rail.Bottom - RailLegendH;
            return Math.Max(0, (int)((bottom - top) / RailChapterH));
        }

        /// <summary>图例区起点 Y</summary>
        public static float RailLegendTop(in QuestLogLayout layout)
            => layout.Rail.Bottom - RailLegendH;

        /// <summary>按当前屏幕尺寸与详情栏展开度算一份分区，左栏是全样式标配</summary>
        public static QuestLogLayout Layout(float detailProgress) {
            int w = (int)MathF.Max(UIScreenW, 640f);
            int h = (int)MathF.Max(UIScreenH, 480f);
            detailProgress = MathHelper.Clamp(detailProgress, 0f, 1f);

            Rectangle full = new(0, 0, w, h);
            Rectangle header = new(0, 0, w, HeaderH);
            int bodyTop = HeaderH;
            int bodyH = Math.Max(120, h - HeaderH - FooterH);
            Rectangle rail = new(0, bodyTop, RailW, bodyH);
            Rectangle footer = new(0, h - FooterH, w, FooterH);

            //详情栏滑入时画布同步收窄，任务图整体向左让位，节点不会被压在栏下
            int detailBite = (int)(DetailW * detailProgress);
            int canvasX = RailW;
            int canvasW = w - RailW - detailBite;
            if (canvasW < CanvasMinW) {
                canvasW = Math.Min(CanvasMinW, w - RailW);
            }
            Rectangle canvas = new(canvasX, bodyTop, Math.Max(80, canvasW), bodyH);
            Rectangle detail = new(w - detailBite, bodyTop, DetailW, bodyH);

            //合卷键锚点由分区统一给出，容器判定与样式绘制读同一份
            Rectangle mainClose = new(w - 46, 15, 32, 32);

            return new QuestLogLayout(full, header, rail, canvas, footer, detail,
                mainClose, detailProgress);
        }

        #endregion

        #region 页脚按钮位

        /// <summary>页脚图标键边长</summary>
        public const int FooterBtnH = 30;

        /// <summary>页脚一键领取牌宽，按最长的一版译文留的</summary>
        public const int FooterClaimW = 150;

        /// <summary>页脚按钮间距</summary>
        private const int FooterBtnGap = 8;

        /// <summary>页脚按钮距右缘</summary>
        private const int FooterBtnEdge = 14;

        //页脚右簇自右往左固定为 样式 / 夜间 / 归位 / 一键领取。
        //槽位是死的，缺席的样式（远征纪要没有夜间键）留着空位不往右挪——
        //换皮肤只该换风格，同一个键不该跑到屏幕另一边

        /// <summary>样式切换键，右簇最右</summary>
        public static Rectangle FooterStyleButton(Rectangle footer) => FooterSlot(footer, 0);

        /// <summary>夜间模式键</summary>
        public static Rectangle FooterNightButton(Rectangle footer) => FooterSlot(footer, 1);

        /// <summary>视角归位键</summary>
        public static Rectangle FooterResetButton(Rectangle footer) => FooterSlot(footer, 2);

        /// <summary>一键领取牌，接在三枚图标键左侧</summary>
        public static Rectangle FooterClaimButton(Rectangle footer) {
            Rectangle last = FooterSlot(footer, 2);
            return new Rectangle(last.X - FooterBtnGap - FooterClaimW, last.Y,
                FooterClaimW, FooterBtnH);
        }

        private static Rectangle FooterSlot(Rectangle footer, int slot)
            => new(footer.Right - FooterBtnEdge - (slot + 1) * FooterBtnH - slot * FooterBtnGap,
                footer.Y + (footer.Height - FooterBtnH) / 2, FooterBtnH, FooterBtnH);

        #endregion

        #region 共用小工具

        /// <summary>呼吸系数 [0,1]，phase 用于错开同类元素</summary>
        public static float Breath(float time, float phase, float speed = 2.2f)
            => MathF.Sin(time * speed + phase) * 0.5f + 0.5f;

        /// <summary>
        /// 确定性散列 [0,1)，供纸纤维、磨损、装订孔这类"不规整但每帧稳定"的细节取值<br/>
        /// 与 <c>OniBrush.Hash01</c> 同款，全新样式共用一支，避免各处再手搓噪声
        /// </summary>
        public static float Hash01(int n) {
            unchecked {
                n = n * 374761393 + 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0x7FFFFFFF) / (float)int.MaxValue;
            }
        }

        /// <summary>三次缓出</summary>
        public static float EaseOutCubic(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        #endregion
    }

    /// <summary>
    /// 任务书一帧的全屏分区，由容器每帧算好后交给样式绘制
    /// </summary>
    public readonly struct QuestLogLayout
    {
        /// <summary>整屏</summary>
        public readonly Rectangle Full;

        /// <summary>页眉带</summary>
        public readonly Rectangle Header;

        /// <summary>左栏</summary>
        public readonly Rectangle Rail;

        /// <summary>中央画布，详情栏展开时会收窄</summary>
        public readonly Rectangle Canvas;

        /// <summary>页脚带</summary>
        public readonly Rectangle Footer;

        /// <summary>右侧详情栏，未展开时整体位于屏幕右缘之外</summary>
        public readonly Rectangle Detail;

        /// <summary>合卷键命中区</summary>
        public readonly Rectangle MainClose;

        /// <summary>详情栏展开度 [0,1]</summary>
        public readonly float DetailProgress;

        public QuestLogLayout(Rectangle full, Rectangle header, Rectangle rail, Rectangle canvas,
            Rectangle footer, Rectangle detail, Rectangle mainClose, float detailProgress) {
            Full = full;
            Header = header;
            Rail = rail;
            Canvas = canvas;
            Footer = footer;
            Detail = detail;
            MainClose = mainClose;
            DetailProgress = detailProgress;
        }

        /// <summary>画布中心，任务图的坐标原点</summary>
        public Vector2 CanvasCenter => new(Canvas.X + Canvas.Width / 2f, Canvas.Y + Canvas.Height / 2f);
    }
}
