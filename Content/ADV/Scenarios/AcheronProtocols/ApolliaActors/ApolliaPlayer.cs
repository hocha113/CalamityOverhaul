using CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors.States;
using CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines;
using CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.Gargoyles;
using CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.LandingScens;
using InnoVault.Actors;
using InnoVault.Cinematics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors
{
    /// <summary>
    /// 管理阿波利娅演出的生命周期：
    /// 1. 检测着陆完成 → 延迟生成 ApolliaActor
    /// 2. 登场运镜交由 <see cref="ApolliaCutscene"/>（InnoVault 演出系统）按 Actor 状态自动表现
    /// </summary>
    internal class ApolliaPlayer : ModPlayer
    {
        /// <summary>阿波利娅出场延迟计时器</summary>
        private int spawnDelay;

        /// <summary>是否已经生成过阿波利娅</summary>
        private bool spawned;

        /// <summary>着陆完成时记录的玩家位置</summary>
        private Vector2 landingPodCenter;

        /// <summary>是否已检测到玩家弹出空降仓完成</summary>
        private bool ejectDetected;

        /// <summary>当前场景中的阿波利娅Actor引用（弱引用方式通过索引）</summary>
        private int apolliaActorIndex = -1;

        /// <summary>是否已触发过阿波利娅到达后的对话场景</summary>
        private bool dialogueTriggered;

        /// <summary>英雄面板是否已激活</summary>
        internal bool HeroPanelActivated;

        /// <summary>石像鬼序列阶段：0=未开始  1=延迟计时（8秒）  2=演出播放中  3=警示对话已启动</summary>
        private int gargoylePhase;
        /// <summary>延迟计时器（目标 480 帧 ≈ 8 秒）</summary>
        private int gargoyleDelayTimer;

        public override void PostUpdate() {
            if (!Player.Alives()) {
                return;
            }

            if (!MachineWorld.Active) {
                ResetState();
                return;
            }

            if (!MachineWorld.landingCompleted) {
                return;
            }

            //阶段1：检测玩家完全弹出空降仓
            if (!ejectDetected) {
                //必须获取到PlayerOverride且着陆和弹出都已完成才算弹出成功
                //TryGetOverride返回false或仍在着陆/弹出中时都不应继续
                if (!Player.TryGetOverride<MachineWorldLandingPlayer>(out var lp)
                    || lp.LandingActive || lp.EjectAnimating) {
                    return;
                }
                ejectDetected = true;
                landingPodCenter = Player.Center;
                return;
            }

            //阶段2：延迟生成阿波利娅
            if (!spawned) {
                spawnDelay++;
                if (spawnDelay >= 120) {
                    SpawnApollia();
                    spawned = true;
                }
                return;
            }

            //阶段3：阿波利娅到达玩家面前后触发对话场景
            if (!dialogueTriggered) {
                ApolliaActor actor = GetApolliaActor();
                if (actor?.CurrentState is ApolliaArrivedState) {
                    dialogueTriggered = true;
                    ScenarioManager.Reset<FirstMetApollia>();
                    ScenarioManager.Start<FirstMetApollia>();
                }
            }

            //阶段4：英雄面板同步
            if (HeroPanelActivated && ApolliaHeroPanelUI.Instance != null) {
                ApolliaHeroPanelUI.Instance.Unlocked = true;
            }

            //阶段5：英雄面板刚激活时，开始 8 秒倒计时
            if (HeroPanelActivated && gargoylePhase == 0) {
                gargoylePhase = 1;
            }

            if (gargoylePhase == 1) {
                gargoyleDelayTimer++;
                if (gargoyleDelayTimer >= 480) {
                    gargoylePhase = 2;
                    GargoyleSwarmPlayer.StartCutscene();
                }
            }

            //阶段6：等待演出结束，触发警示对话
            if (gargoylePhase == 2 && !GargoyleSwarmPlayer.IsActive) {
                gargoylePhase = 3;
                ScenarioManager.Reset<GargoyleWarningScenario>();
                ScenarioManager.Start<GargoyleWarningScenario>();
            }
        }

        private void SpawnApollia() {
            int index = ActorLoader.NewActor<ApolliaActor>(Vector2.Zero, Vector2.Zero);
            if (index >= 0 && index < ActorLoader.MaxActorCount
                && ActorLoader.Actors[index] is ApolliaActor apollia) {
                apollia.StartLandingCutscene(landingPodCenter);
                apolliaActorIndex = index;
            }
        }

        /// <summary>
        /// 获取当前场景中的阿波利娅Actor实例，无效时返回null
        /// </summary>
        internal ApolliaActor GetApolliaActor() {
            if (apolliaActorIndex >= 0 && apolliaActorIndex < ActorLoader.MaxActorCount
                && ActorLoader.Actors[apolliaActorIndex] is ApolliaActor actor
                && actor.Active) {
                return actor;
            }
            apolliaActorIndex = -1;
            return null;
        }

        /// <summary>
        /// 启动引路行为——阿波利娅开始向右引导玩家前往要塞
        /// </summary>
        internal void StartLeadToFortress() {
            ApolliaActor actor = GetApolliaActor();
            actor?.TransitionTo(new ApolliaLeadRightState());
        }

        /// <summary>
        /// 激活英雄面板——在对话场景完成时调用
        /// </summary>
        internal void ActivateHeroPanel() {
            HeroPanelActivated = true;

            //平滑关闭登场运镜
            if (CutsceneDirector.CurrentClip is ApolliaCutscene) {
                CutsceneDirector.Stop();
            }

            if (ApolliaHeroPanelUI.Instance != null) {
                ApolliaHeroPanelUI.Instance.Unlocked = true;
                ApolliaHeroPanelUI.Instance.StartFlyIn();
            }
        }

        private void ResetState() {
            spawnDelay = 0;
            spawned = false;
            ejectDetected = false;
            dialogueTriggered = false;
            HeroPanelActivated = false;
            landingPodCenter = Vector2.Zero;
            apolliaActorIndex = -1;
            gargoylePhase = 0;
            gargoyleDelayTimer = 0;

            if (ApolliaHeroPanelUI.Instance != null) {
                ApolliaHeroPanelUI.Instance.Unlocked = false;
            }
        }
    }
}
