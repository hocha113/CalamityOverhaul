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

        [Header("CWRModCompat")]

        /// <summary>
        /// 灾厄的燃金/异域彩虹稀有度与 [ceffect] 标签名称特效在绑定后备缓冲时切换渲染目标，
        /// FNA 以 DiscardContents 重绑后备缓冲会把整帧清黑；开启后改用无渲染目标的等效绘制
        /// </summary>
        [BackgroundColor(70, 110, 160, 255)]
        [DefaultValue(true)]
        public bool CalamityRarityTextFix { get; set; }

        /// <summary>
        /// 灾厄 HolyBurnOrbDrawer 缓存的弹幕引用在槽位被复用后仍按原类型取 ModProjectile 导致空引用崩溃；
        /// 开启后在绘制前剔除失效引用
        /// </summary>
        [BackgroundColor(70, 110, 160, 255)]
        [DefaultValue(true)]
        public bool CalamityHolyBurnOrbFix { get; set; }

        public override void OnLoaded() {
            Instance = this;
        }
    }
}
