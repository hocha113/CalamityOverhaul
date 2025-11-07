using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers
{
    /// <summary>
    /// 信号塔搭建检测系统
    /// </summary>
    internal class DeploySignaltowerCheck : ModSystem
    {
        /// <summary>
        /// 世界上已搭建的信号塔数量
        /// </summary>
        public static int DeployedTowerCount { get; private set; }

        /// <summary>
        /// 目标信号塔数量
        /// </summary>
        public const int TargetTowerCount = 10;

        /// <summary>
        /// 初次搭建场景触发检测计时器
        /// </summary>
        private int scenarioCheckTimer;

        /// <summary>
        /// 任务完成场景触发计时器
        /// </summary>
        private int questCompleteCheckTimer;

        public override void PostUpdateEverything() {
            //统计世界上的信号塔数量
            UpdateTowerCount();

            //检测是否触发初次搭建场景
            CheckFirstTowerScenario();

            //检测任务完成
            CheckQuestComplete();
        }

        /// <summary>
        /// 更新世界上信号塔的数量
        /// </summary>
        private static void UpdateTowerCount() {
            DeployedTowerCount = 0;
            if (TileProcessorLoader.TP_ID_To_InWorld_Count.TryGetValue(ModContent.TileType<DeploySignaltowerTile>(), out int count)) {
                DeployedTowerCount = count;
            }
        }

        /// <summary>
        /// 检测是否触发初次搭建场景
        /// </summary>
        private void CheckFirstTowerScenario() {
            if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }

            ADVSave save = halibutPlayer.ADCSave;
            if (save == null) {
                return;
            }

            //检查是否已接受任务但未触发首次场景
            if (!save.DeploySignaltowerQuestAccepted || save.DeploySignaltowerFirstTowerBuilt) {
                return;
            }

            //检测是否有第一座信号塔被搭建
            if (DeployedTowerCount > 0) {
                scenarioCheckTimer++;

                //延迟2秒后触发场景避免在建造动画时触发
                if (scenarioCheckTimer >= 120) {
                    save.DeploySignaltowerFirstTowerBuilt = true;
                    TriggerFirstTowerScenario();
                    scenarioCheckTimer = 0;
                }
            }
            else {
                scenarioCheckTimer = 0;
            }
        }

        /// <summary>
        /// 检测任务完成
        /// </summary>
        private void CheckQuestComplete() {
            if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }

            ADVSave save = halibutPlayer.ADCSave;
            if (save == null) {
                return;
            }

            //检查是否已接受任务但未完成
            if (!save.DeploySignaltowerQuestAccepted || save.DeploySignaltowerQuestCompleted) {
                return;
            }

            //检测是否达到目标数量
            if (DeployedTowerCount >= TargetTowerCount) {
                questCompleteCheckTimer++;

                //延迟2秒后触发完成场景
                if (questCompleteCheckTimer >= 120) {
                    save.DeploySignaltowerQuestCompleted = true;
                    OnQuestComplete();
                    questCompleteCheckTimer = 0;
                }
            }
            else {
                questCompleteCheckTimer = 0;
            }
        }

        /// <summary>
        /// 触发第一座信号塔搭建后的场景
        /// </summary>
        private static void TriggerFirstTowerScenario() {
            //触发嘉登出现给予指示的场景
            ScenarioManager.Start<FirstTowerBuiltScenario>();
        }

        /// <summary>
        /// 任务完成时调用
        /// </summary>
        private static void OnQuestComplete() {
            //触发任务完成场景
            ScenarioManager.Start<QuestCompleteScenario>();
        }

        public override void ClearWorld() {
            DeployedTowerCount = 0;
            scenarioCheckTimer = 0;
            questCompleteCheckTimer = 0;
        }
    }
}
