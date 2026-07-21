using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 比目鱼 UI 色板与几何常量
    /// 黑蓝底 + 青辉光 + 暖金选中 + 危险红，与 SeaDialogueBox.fx 对齐
    /// </summary>
    internal static class HalibutTheme
    {
        #region UI空间坐标（与调用语境无关）
        //UIHandle 的 Update/Draw 运行在 InterfaceScaleType.UI 层内，此时 Main.screenWidth 已被
        //除以UIScale；逻辑帧里是原始后台缓冲尺寸
        //任何跨语境的布局计算都必须使用下面这组换算，禁止直接读 Main.screenWidth/Height
        /// <summary>
        /// UI空间下的屏幕宽度（任何调用语境下取值一致）
        /// </summary>
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        /// <summary>
        /// UI空间下的屏幕高度（任何调用语境下取值一致）
        /// </summary>
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        /// <summary>
        /// UI空间下的屏幕尺寸
        /// </summary>
        public static Vector2 UIScreenSize => new(UIScreenW, UIScreenH);
        /// <summary>
        /// UI空间下的鼠标位置（任何调用语境下取值一致）
        /// </summary>
        public static Vector2 UIMouse => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Main.UIScale;
        #endregion

        //基底色，由深到浅
        public static readonly Color Void = new(1, 3, 6);
        public static readonly Color Deep = new(3, 10, 15);
        public static readonly Color Mid = new(8, 28, 38);
        public static readonly Color Teal = new(15, 66, 79);
        //冷光主色，描边/辉光/激活
        public static readonly Color Glow = new(77, 199, 250);
        //高光，强调/扫光
        public static readonly Color GlowHi = new(158, 240, 255);
        //焦散白，最亮的点缀
        public static readonly Color Caustic = new(200, 240, 255);
        //暖金，选中/传奇强调，避免整屏冷色单调
        public static readonly Color Accent = new(255, 200, 97);
        //危险红，复苏临界与死机状态
        public static readonly Color Danger = new(255, 77, 77);
        //深红，死机暗部
        public static readonly Color DangerDim = new(160, 45, 45);
        //紫罗兰，深渊深处的点缀色
        public static readonly Color Violet = new(122, 92, 255);
        //文字主色与次色
        public static readonly Color Text = new(225, 240, 248);
        public static readonly Color TextDim = new(130, 170, 188);
        //面板底色（CPU回退绘制用）
        public static readonly Color PanelBg = new(6, 16, 26);
        //禁用灰
        public static readonly Color Disabled = new(70, 90, 100);

        #region HUD几何
        //HUD主环（当前技能）半径
        public const float HudCoreR = 27f;
        //HUD主环描边半径
        public const float HudCoreRingR = 33f;
        //HUD锚点、距左下偏移
        public static readonly Vector2 HudAnchorOffset = new(86f, -78f);
        //复苏深度计尺寸
        public const float HudGaugeHeight = 64f;
        public const float HudGaugeWidth = 7f;
        //领域技能卫星环半径
        public const float HudSatelliteR = 15f;
        #endregion

        #region 轮盘几何
        //轮盘扇区内外半径
        public const float WheelInnerR = 64f;
        public const float WheelOuterR = 118f;
        //中心死区半径，光标退回此区=不选择
        public const float WheelDeadZoneR = 38f;
        //图标摆放半径
        public const float WheelIconR = (WheelInnerR + WheelOuterR) * 0.5f;
        //扇区之间的角度间隙
        public const float WheelSectorGap = 0.035f;
        //轮盘锚点Y占屏比
        public const float WheelAnchorYRatio = 0.5f;
        #endregion

        #region 图鉴几何
        //深度带名索引、0浅滩1远洋2深海3深渊
        public const int AtlasTierCount = 4;
        //每个深度带的像素高度
        public const float AtlasTierHeight = 360f;
        //节点网格列数
        public const int AtlasNodeColumns = 6;
        //节点间距
        public const float AtlasNodeSpacingX = 92f;
        public const float AtlasNodeSpacingY = 96f;
        //装备坞槽位数量上限（与 HalibutSave.LoadoutCap 一致）
        public const int DockSlotCount = 10;
        //装备坞槽位半径
        public const float DockSlotR = 23f;
        #endregion

        /// <summary>
        /// 给定深度带索引返回该带的主题色（浅滩青绿 → 深渊紫黑）
        /// </summary>
        public static Color TierColor(int tier) {
            return tier switch {
                0 => new Color(95, 220, 215),
                1 => new Color(77, 199, 250),
                2 => new Color(96, 130, 255),
                _ => new Color(140, 100, 255),
            };
        }

        /// <summary>
        /// 异相位呼吸波，给定相位种子返回 0-1 的缓慢脉动
        /// </summary>
        public static float Breath(float time, float seed, float speed = 2f) {
            return (float)System.Math.Sin(time * speed + seed * 17.39f) * 0.5f + 0.5f;
        }
    }
}
