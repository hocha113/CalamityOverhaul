using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 纯本地视觉/手感偏好，不影响其他玩家也无需服务器仲裁，故与 <see cref="CWRServerConfig"/> 分离为客户端配置
    /// </summary>
    [BackgroundColor(49, 32, 36, 216)]
    public class CWRClientConfig : ModConfig
    {
        //Instance 勿懒加载
        public static CWRClientConfig Instance { get; private set; }
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Header("CWRWeapon")]

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool ScreenVibration { get; set; }//武器屏幕振动

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(false)]
        public bool DomainConciseDisplay { get; set; }//领域简约显示（赛博空间/海域；不含鬼域）

        /// <summary>
        /// 屏蔽其他玩家的领域演出（鬼切鬼域、鬼伞血湖/鬼雨/鬼梦、大范围重启的旁观演出）。
        /// 只动观看选择：他人域不再入选 <c>Viewed</c>，状态机、网络、功能判定一律照旧；
        /// 他人域在功能上触达本机（站上其湖面、身处其鬼梦圆、被其沉人）时仍强制显示，
        /// 契约见 <c>Content/LegendWeapon/LegendDomainView.cs</c>
        /// </summary>
        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool HideOtherDomains { get; set; }

        [BackgroundColor(192, 54, 94, 255)]
        [DefaultValue(true)]
        public bool LensEasing { get; set; }//镜头缓动

        /// <summary>
        /// 氛围密度总闸：只缩放装饰性氛围粒子与音景的生成密度；
        /// 预告体、危害实体及其可见性绝不随之缩减（否则等于降画质换公平劣势）
        /// </summary>
        [BackgroundColor(192, 54, 94, 255)]
        [Range(0.25f, 1f)]
        [DefaultValue(1f)]
        public float AmbienceDensity { get; set; }

        [Header("CWRDisplay")]

        /// <summary>本模组稀有度的名称特效（金屑/润光/热浪/镜面/虹彩/传奇渐变），关掉只剩纯色</summary>
        [BackgroundColor(120, 92, 160, 255)]
        [DefaultValue(true)]
        public bool RarityTextEffects { get; set; }

        public override void OnLoaded() {
            Instance = this;
        }
    }
}
