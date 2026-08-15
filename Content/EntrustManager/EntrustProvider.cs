using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.EntrustManager
{
    /// <summary>
    /// 委托提供者：一次声明，处处使用。<br/>
    /// 身份由「行右缘徽记 + 展开区落款」承载——徽记框的画法归界面样式
    /// （<see cref="IEntrustManagerStyle.DrawProviderBadge"/>），本类只出纹样、主色与头像源
    /// </summary>
    internal sealed class EntrustProvider
    {
        /// <summary>提供者名，落款与徽记悬停用</summary>
        public LocalizedText Name;

        /// <summary>归一 [-1,1] 的 SVG 纹样串，走 SvgPathPen（M/L/H/V/C/Q，禁 A 弧）</summary>
        public string GlyphD;

        /// <summary>提供者主色，自旧条目样式的色板裁剪继承</summary>
        public Color Accent;

        /// <summary>头像用物品贴图，0 则不用；消费端负责 LoadItem</summary>
        public int AvatarItemType;

        /// <summary>头像贴图路径，物品与路径都缺则退回纹样</summary>
        public string AvatarTexturePath;

        /// <summary>
        /// 徽记内部自定义填充（中心 / 半径 / alpha），给想保留一小块签名质感的提供者。<br/>
        /// 框由界面样式压在其上，填充只管圆内
        /// </summary>
        public Action<SpriteBatch, Vector2, float, float> BadgeFill;
    }

    /// <summary>
    /// 既有提供者注册表。名字本地化由 <see cref="QuestManagerUI.SetStaticDefaults"/>
    /// 调 <see cref="InitLocalization"/> 注册；实例惰性构建，物品 ID 在入世后首次取用时已就绪
    /// </summary>
    internal static class EntrustProviders
    {
        public static LocalizedText NameHalibut { get; private set; }
        public static LocalizedText NameOnikiri { get; private set; }
        public static LocalizedText NameSHPC { get; private set; }
        public static LocalizedText NameOldDuke { get; private set; }
        public static LocalizedText NameSupCal { get; private set; }
        public static LocalizedText NameDraedon { get; private set; }

        public static void InitLocalization(ILocalizedModType host) {
            NameHalibut = host.GetLocalization("ProviderHalibut", () => "比目鱼");
            NameOnikiri = host.GetLocalization("ProviderOnikiri", () => "鬼切");
            NameSHPC = host.GetLocalization("ProviderSHPC", () => "SHPC");
            NameOldDuke = host.GetLocalization("ProviderOldDuke", () => "老公爵");
            NameSupCal = host.GetLocalization("ProviderSupCal", () => "至尊灾厄");
            NameDraedon = host.GetLocalization("ProviderDraedon", () => "嘉登");
        }

        public static void UnloadInstances() {
            halibut = onikiri = shpc = oldDuke = supCal = draedon = null;
        }

        #region 纹样

        //比目鱼：侧扁鱼身 + 尾鳍两线 + 一记眼点
        private const string HalibutGlyphD =
            "M -0.88,0.04 C -0.5,-0.52 0.18,-0.58 0.6,-0.2"
            + " C 0.82,0.0 0.82,0.1 0.58,0.28"
            + " C 0.18,0.58 -0.5,0.56 -0.88,0.04 Z"
            + " M 0.58,-0.06 L 0.92,-0.34 M 0.6,0.14 L 0.92,0.38"
            + " M -0.5,-0.14 L -0.42,-0.14";

        //鬼切：反りの太刀身 + 切先返し + 镡的一横
        private const string OnikiriGlyphD =
            "M -0.82,0.6 C -0.3,0.28 0.24,-0.2 0.72,-0.78"
            + " M 0.52,-0.6 L 0.72,-0.4"
            + " M -0.66,0.36 L -0.42,0.62";

        //海妖珍珠：合抱贝口 + 一粒珠
        private const string SHPCGlyphD =
            "M -0.78,0.34 C -0.5,-0.5 0.5,-0.5 0.78,0.34"
            + " M -0.56,0.34 L 0.56,0.34"
            + " M 0,-0.04 C 0.15,-0.04 0.26,0.07 0.26,0.2"
            + " C 0.26,0.33 0.15,0.44 0,0.44"
            + " C -0.15,0.44 -0.26,0.33 -0.26,0.2"
            + " C -0.26,0.07 -0.15,-0.04 0,-0.04 Z";

        //老公爵：硫磺海面一道浪 + 两粒上浮的酸泡
        private const string OldDukeGlyphD =
            "M -0.82,0.5 C -0.4,0.28 0.4,0.7 0.82,0.44"
            + " M -0.34,0.02 C -0.34,-0.18 -0.02,-0.18 -0.02,0.02"
            + " C -0.02,0.22 -0.34,0.22 -0.34,0.02 Z"
            + " M 0.26,-0.36 C 0.26,-0.5 0.46,-0.5 0.46,-0.36"
            + " C 0.46,-0.22 0.26,-0.22 0.26,-0.36 Z";

        //至尊灾厄：硫火外焰 + 内舌
        private const string SupCalGlyphD =
            "M 0,-0.82 C 0.38,-0.44 0.58,-0.14 0.52,0.26"
            + " C 0.46,0.6 0.24,0.78 0,0.84"
            + " C -0.24,0.78 -0.46,0.6 -0.52,0.26"
            + " C -0.58,-0.14 -0.38,-0.44 0,-0.82 Z"
            + " M 0,-0.28 C 0.14,-0.04 0.18,0.16 0,0.4"
            + " C -0.18,0.16 -0.14,-0.04 0,-0.28 Z";

        //嘉登：菱形基板 + 走线十字与一条斜跳线
        private const string DraedonGlyphD =
            "M 0,-0.78 L 0.78,0 L 0,0.78 L -0.78,0 Z"
            + " M 0,-0.34 L 0,0.34 M -0.34,0 L 0.34,0"
            + " M 0.08,-0.08 L 0.3,-0.3";

        #endregion

        #region 实例

        private static EntrustProvider halibut;
        private static EntrustProvider onikiri;
        private static EntrustProvider shpc;
        private static EntrustProvider oldDuke;
        private static EntrustProvider supCal;
        private static EntrustProvider draedon;

        /// <summary>比目鱼：鱼油采集与比目鱼试炼都是它发的</summary>
        public static EntrustProvider Halibut => halibut ??= new EntrustProvider {
            Name = NameHalibut,
            GlyphD = HalibutGlyphD,
            Accent = new Color(75, 175, 215),
            AvatarItemType = HalibutOverride.ID,
        };

        public static EntrustProvider Onikiri => onikiri ??= new EntrustProvider {
            Name = NameOnikiri,
            GlyphD = OnikiriGlyphD,
            Accent = new Color(196, 64, 58),
            AvatarItemType = OnikiriOverride.ID,
        };

        public static EntrustProvider SHPC => shpc ??= new EntrustProvider {
            Name = NameSHPC,
            GlyphD = SHPCGlyphD,
            Accent = new Color(96, 132, 240),
            AvatarItemType = SHPCOverride.ID,
        };

        /// <summary>老公爵：无本模组物品可用作头像，落款退回纹样</summary>
        public static EntrustProvider OldDuke => oldDuke ??= new EntrustProvider {
            Name = NameOldDuke,
            GlyphD = OldDukeGlyphD,
            Accent = new Color(140, 180, 70),
        };

        public static EntrustProvider SupCal => supCal ??= new EntrustProvider {
            Name = NameSupCal,
            GlyphD = SupCalGlyphD,
            Accent = new Color(220, 88, 36),
        };

        public static EntrustProvider Draedon => draedon ??= new EntrustProvider {
            Name = NameDraedon,
            GlyphD = DraedonGlyphD,
            Accent = new Color(118, 196, 205),
        };

        #endregion
    }
}
