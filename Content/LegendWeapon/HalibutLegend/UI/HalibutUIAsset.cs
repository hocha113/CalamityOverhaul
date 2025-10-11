using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    [VaultLoaden(CWRConstant.UI + "Halibut/")]//用反射标签加载对应文件夹下的所有资源
    internal static class HalibutUIAsset
    {
        //奈落之眼纹理，共四帧动画，第一帧是闭眼，第二帧是睁眼，单帧大小40(宽)*30(高)，后面两帧是死机版本，大小和状态同上
        public static Texture2D SeaEye = null;
        //按钮，大小46*26
        public static Texture2D Button = null;
        //大比目鱼的头像图标，放置在屏幕左下角作为UI的入口，大小74*74
        public static Texture2D Head = null;
        //左侧边栏，大小218*42
        public static Texture2D LeftSidebar = null;
        //面板，大小242*214
        public static Texture2D Panel = null;
        //提示面板，大小214*206
        public static Texture2D TooltipPanel = null;
        //图标栏，大小60*52
        public static Texture2D PictureSlot = null;
        //技能图标，大小170*34，共五帧，对应五种技能的图标
        public static Texture2D Skillcon = null;
        //左侧方向按钮
        public static Texture2D LeftButton = null;
        //右侧方向按钮
        public static Texture2D RightButton = null;
        //下划线花边纹理
        public static Texture2D TooltiplineBorder = null;
        //复苏进度条的底部边框，大小82*32
        public static Texture2D Resurrection = null;
        //复苏进度条的进度填充，大小52*8，和底部边框配合使用，偏移值为(24, 12)
        public static Texture2D ResurrectionTop = null;
    }
}
