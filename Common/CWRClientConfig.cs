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

        public override void OnLoaded() {
            Instance = this;
        }
    }
}
