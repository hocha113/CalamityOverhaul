using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.NPCs.TBUGs.UIs
{
    /// <summary>
    /// TBUG 界面色板、字号梯队与几何常量。
    /// <br/>材质："深空黑底上的一块冷蓝终端玻璃"——不是霓虹招牌，不是绿字矩阵。
    /// <para>
    /// 风格铁律（新增任何绘制前先对一遍，别现场编数值）：
    /// <br/>1. 底只有三档：<see cref="Void"/> 凹陷 / <see cref="Deep"/> 面板 / <see cref="Panel"/> 抬起面，越靠近用户越亮。
    /// <br/>2. 结构线、边框、标题、光标、常态高亮一律用蓝族（<see cref="Line"/>/<see cref="BlueDim"/>/<see cref="Blue"/>/<see cref="Ice"/>），不引入第三种冷色。
    /// <br/>3. <see cref="Amber"/> 只给货币与"已选中"，<see cref="Danger"/> 只给报错与买不起。这两色不作装饰用。
    /// <br/>4. 拐角语言只有一种：<see cref="Chamfer"/> 尺寸的切角。禁止 L 形角标、圆角、双层描边混用。
    /// <br/>5. 字号只能从下面的梯队里取，不许写字面量，也不再乘任何缩放系数。
    /// </para>
    /// </summary>
    internal static class TBUGTheme
    {
        #region UI 空间坐标

        //UIHandle 的 Update/Draw 跑在 UIScale 空间，逻辑帧里是原始后台缓冲尺寸，
        //跨语境布局一律走这组换算，禁止直接读 Main.screenWidth/Height
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        #endregion

        #region 字号梯队

        //基准：正文 1.0。这一档是"坐在屏幕前一眼能读"的下限，
        //历史教训是每次都往 0.5 上下飘，读起来像脚注，别再往下调
        /// <summary>面板主标题</summary>
        public const float FontDisplay = 1.24f;
        /// <summary>分区标题 / 物品名 / 说话人</summary>
        public const float FontTitle = 1.05f;
        /// <summary>对话正文 / 描述正文</summary>
        public const float FontBody = 1.00f;
        /// <summary>价格 / 余额 / 命令按钮</summary>
        public const float FontLabel = 0.86f;
        /// <summary>状态栏与角注，唯一允许的小字</summary>
        public const float FontMicro = 0.74f;

        #endregion

        #region 配色

        //底：三档，越靠近用户越亮
        public static readonly Color Void = new(2, 5, 10);
        public static readonly Color Deep = new(5, 11, 22);
        public static readonly Color Panel = new(9, 18, 34);
        /// <summary>悬停抬起面</summary>
        public static readonly Color Rise = new(16, 32, 56);

        //蓝族：结构与高亮
        /// <summary>常态结构线</summary>
        public static readonly Color Line = new(30, 58, 96);
        public static readonly Color BlueDim = new(36, 88, 152);
        /// <summary>主色</summary>
        public static readonly Color Blue = new(72, 158, 255);
        /// <summary>高光，最亮的冷点缀</summary>
        public static readonly Color Ice = new(176, 224, 255);

        //文字
        public static readonly Color Text = new(206, 226, 246);
        public static readonly Color TextDim = new(108, 140, 176);

        /// <summary>暖金：只给货币与已选中</summary>
        public static readonly Color Amber = new(255, 190, 92);
        /// <summary>报错红：只给错误与买不起，与裂缝的报错色同族</summary>
        public static readonly Color Danger = new(255, 62, 118);

        #endregion

        #region 几何

        /// <summary>统一切角尺寸，全部面板与格子共用</summary>
        public const int Chamfer = 7;

        /// <summary>商店格边长</summary>
        public const int CellSize = 84;
        /// <summary>商店格间距</summary>
        public const int CellGap = 10;
        /// <summary>商店网格列数</summary>
        public const int GridColumns = 4;

        /// <summary>立绘整数放大倍率（源帧 36×50）</summary>
        public const int PortraitScale = 4;

        #endregion

        /// <summary>异相位呼吸波，0-1 缓慢脉动</summary>
        public static float Breath(float time, float seed, float speed = 2f)
            => System.MathF.Sin(time * speed + seed * 17.39f) * 0.5f + 0.5f;
    }
}
