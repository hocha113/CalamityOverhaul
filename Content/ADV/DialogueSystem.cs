using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.Scenarios;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV
{
    internal class DialogueSystem : ModSystem, ILocalizedModType
    {
        public static LocalizedText ContinueHint;
        public static LocalizedText AutoHint;
        public static LocalizedText SkipHint;
        public static LocalizedText FastHint;
        public string LocalizationCategory => "UI";

        public override void SetStaticDefaults() {
            ContinueHint = this.GetLocalization(nameof(ContinueHint), () => "继续");
            AutoHint = this.GetLocalization(nameof(AutoHint), () => "自动");
            SkipHint = this.GetLocalization(nameof(SkipHint), () => "跳过");
            FastHint = this.GetLocalization(nameof(FastHint), () => "加速");
        }

        public override void PostSetupContent() {
            //注册框架级阻塞器，模块特有的阻塞器由各自模块注册
            ADVScenarioScheduler.RegisterBlocker(() =>
                CWRWorld.HasBoss ? ScenarioBlockers.Boss : ScenarioBlockers.None);
            ADVScenarioScheduler.RegisterBlocker(() =>
                CWRWorld.BossRush ? ScenarioBlockers.BossRush : ScenarioBlockers.None);
            ADVScenarioScheduler.RegisterBlocker(() =>
                ScenarioManager.IsActive() ? ScenarioBlockers.ActiveScenario : ScenarioBlockers.None);
        }

        public override void Unload() {
            ADVScenarioScheduler.Unload();
        }

        public override void OnWorldLoad() {
            //世界切换时清理对话框和场景管理器的运行状态，
            //防止上个世界残留的Active状态阻塞新世界的场景启动
            DialogueUIRegistry.ResetAll();
            ScenarioManager.OnWorldCleanup();
        }

        public override void UpdateUI(GameTime gameTime) {
            DialogueUIRegistry.Current?.SetTargetScale(CWRServerConfig.Instance.DialogueBox_Scale_Value);
            DialogueUIRegistry.Current?.LogicUpdate();
            ADVRewardPopup.Instance?.LogicUpdate();
            //驱动场景管理器的待启动队列，让上一场景结束后自动衔接下一场景
            ScenarioManager.UpdatePending();
        }
    }
}