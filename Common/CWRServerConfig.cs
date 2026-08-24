using CalamityOverhaul.Content.UIs.OverhaulSettings;
using System.ComponentModel;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace CalamityOverhaul.Common
{
    [BackgroundColor(49, 32, 36, 216)]
    public class CWRServerConfig : ModConfig
    {
        public static LocalizedText ConfigChangePrefix { get; private set; }
        public static LocalizedText ConfigChangeSuffix { get; private set; }

        //Instance 勿懒加载
        public static CWRServerConfig Instance { get; private set; }
        public override ConfigScope Mode => ConfigScope.ServerSide;

        [Header("CWRSystem")]

        [BackgroundColor(35, 185, 78, 255)]
        [ReloadRequired]
        [DefaultValue(true)]
        public bool QuestLog { get; set; }//任务书开关

        //生物大修配置已移除：BrutalNPCs 的 AI 重制现在由残酷模式（世界级游戏模式）管理
        //联机 PvP 骇入无独立开关：开了原版 PvP 即启用

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

        [Header("CWRWorldGen")]

        [BackgroundColor(100, 160, 80, 255)]
        [DefaultValue(true)]
        public bool GenWindGrivenGenerator { get; set; }

        [BackgroundColor(100, 160, 80, 255)]
        [DefaultValue(true)]
        public bool GenWGGCollector { get; set; }

        [BackgroundColor(100, 160, 80, 255)]
        [DefaultValue(true)]
        public bool GenJunkmanBase { get; set; }

        [BackgroundColor(100, 160, 80, 255)]
        [DefaultValue(true)]
        public bool GenRocketHut { get; set; }

        [BackgroundColor(100, 160, 80, 255)]
        [DefaultValue(true)]
        public bool GenSylvanOutpost { get; set; }

        [BackgroundColor(100, 160, 80, 255)]
        [DefaultValue(true)]
        public bool GenSHPCCradle { get; set; }

        public override void OnLoaded() {
            Instance = this;
            ConfigChangePrefix = this.GetLocalization(nameof(ConfigChangePrefix), () => "用户");
            ConfigChangeSuffix = this.GetLocalization(nameof(ConfigChangeSuffix), () => "修改了服务端配置");
        }

        public override void OnChanged() {
            if (!VaultLoad.LoadenContent) {
                return;
            }
            WorldGenDensitySave.SyncFromConfig();
        }

        public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message) {
            string text = ConfigChangePrefix.Value
                + Main.player[whoAmI].name + ConfigChangeSuffix.Value;
            VaultUtils.Text(text);
            return true;
        }
    }
}
