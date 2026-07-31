using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Wraiths.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    /// <summary>
    /// 鬼切教程步骤状态机（声明式）。
    /// 步骤序列：[0]HUD认知 → [1]点鬼簿 → [2]改铭台 → [3]五拍连斩 → [4]疾走纳刀 → [5]残心
    ///           → [6]樱流 → [7]灭世一闪 → [8]终结乱舞 → [9]里世界肢解 → [10]收域结束。
    /// 每步有"演示→尝试→确认→暂停"子状态；状态机只消费 <see cref="OnikiriTutorialEvents"/> 事件。
    /// 段落检查点在步骤组完成时写入存档，重进世界从对应段开头恢复。
    /// </summary>
    internal static class OnikiriTutorialFlow
    {
        //====步骤索引常量====
        internal const int Step_HudIntro = 0;
        internal const int Step_Register = 1;
        internal const int Step_Mei = 2;
        internal const int Step_Combo = 3;
        internal const int Step_DashJudge = 4;
        internal const int Step_Zanshin = 5;
        internal const int Step_Sakura = 6;
        internal const int Step_Annihilate = 7;
        internal const int Step_Finale = 8;
        internal const int Step_Dismember = 9;
        internal const int Step_Close = 10;
        internal const int Step_Done = 11;

        //====检查点段落边界（供测试入口使用）====
        internal const int Checkpoint_Hud = 1;      //步骤 0..2 完成后写入
        internal const int Checkpoint_Combat = 2;   //步骤 3..5 完成后写入
        private const int Checkpoint_Advanced = 3; //步骤 6..10 完成后写入

        //====运行时状态====
        private static int currentStep = -1;
        private static int stepTimer;       //本步内帧计数
        private static bool stepPending;    //等待条件触发
        private static bool initialized;

        //====连斩进度（步骤3专用）====
        private static int comboBeatReached;
        private static bool comboInProgress;

        //====疾走步骤（步骤4专用）====
        private static bool dashSweepDone;

        //====处决资源辅助快照====
        private static float savedVigor;
        private static float savedStance;

        //====铭位临时快照====
        private static OniMeiSnapshot meiSnapshot;

        //====改铭台认知：打开过再关/确认才推进====
        private static bool meiOpenedThisStep;

        //====外部只读接口====
        internal static int CurrentStep => currentStep;
        internal static int StepTimer => stepTimer;
        internal static bool IsRunning => currentStep >= 0 && currentStep < Step_Done;
        internal static OniMeiSnapshot PendingMeiRestore => meiSnapshot;

        /// <summary>渲染层按钮 / 卡住跳过：推进到下一步</summary>
        internal static void RequestAdvance()
        {
            if (currentStep < 0 || currentStep >= Step_Done) return;
            AdvanceStep();
        }

        //====生命周期====

        internal static void Reset()
        {
            Unsubscribe();
            currentStep = -1;
            stepTimer = 0;
            stepPending = false;
            initialized = false;
            comboBeatReached = 0;
            comboInProgress = false;
            dashSweepDone = false;
            meiOpenedThisStep = false;
            meiSnapshot = null;
            OnikiriTutorialWraith.ClearServerState();
            OnikiriTutorialEvents.ClearAll();
        }

        internal static void ResetIfHolderLost()
        {
            if (initialized && currentStep >= 0 && currentStep < Step_Done)
            {
                //教程被打断，从当前段落起点恢复（重进世界时重建练习鬼影）
                RestoreMeiSnapshotIfNeeded();
                Unsubscribe();
                initialized = false;
            }
        }

        internal static void Tick(GameTime _)
        {
            if (!initialized) Initialize();
            if (currentStep < 0 || currentStep >= Step_Done) return;

            stepTimer++;
            AdvanceIfReady();
        }

        //====初始化====

        private static void Initialize()
        {
            initialized = true;
            Subscribe();

            //从检查点恢复起步位置
            var guide = Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>();
            currentStep = guide.Checkpoint switch
            {
                Checkpoint_Hud => Step_Combo,       //HUD段已完成，从战斗段开始
                Checkpoint_Combat => Step_Sakura,   //战斗段已完成，从进阶段开始
                _ => Step_HudIntro,                 //从头开始
            };
            stepTimer = 0;
            stepPending = true;
            EnterStep(currentStep);
        }

        //====事件订阅====

        private static void Subscribe()
        {
            OnikiriTutorialEvents.OnComboBeatHit += HandleComboBeatHit;
            OnikiriTutorialEvents.OnDashJudged += HandleDashJudged;
            OnikiriTutorialEvents.OnZanshinHit += HandleZanshinHit;
            OnikiriTutorialEvents.OnSakuraReleased += HandleSakuraReleased;
            OnikiriTutorialEvents.OnExecutionAnnihilate += HandleExecutionAnnihilate;
            OnikiriTutorialEvents.OnExecutionFinale += HandleExecutionFinale;
            OnikiriTutorialEvents.OnDomainPhaseSettled += HandleDomainPhaseSettled;
            OnikiriTutorialEvents.OnDismemberLanded += HandleDismemberLanded;
        }

        private static void Unsubscribe()
        {
            OnikiriTutorialEvents.OnComboBeatHit -= HandleComboBeatHit;
            OnikiriTutorialEvents.OnDashJudged -= HandleDashJudged;
            OnikiriTutorialEvents.OnZanshinHit -= HandleZanshinHit;
            OnikiriTutorialEvents.OnSakuraReleased -= HandleSakuraReleased;
            OnikiriTutorialEvents.OnExecutionAnnihilate -= HandleExecutionAnnihilate;
            OnikiriTutorialEvents.OnExecutionFinale -= HandleExecutionFinale;
            OnikiriTutorialEvents.OnDomainPhaseSettled -= HandleDomainPhaseSettled;
            OnikiriTutorialEvents.OnDismemberLanded -= HandleDismemberLanded;
        }

        //====步骤进入====

        private static void EnterStep(int step)
        {
            stepTimer = 0;
            stepPending = false;

            switch (step)
            {
                case Step_HudIntro:
                    //HUD认知：无需互动目标，纯高亮说明；主路径靠「已知晓」
                    stepPending = false;
                    break;

                case Step_Mei:
                    meiOpenedThisStep = false;
                    break;

                case Step_Combo:
                    OnikiriTutorialNet.RequestEnsureTarget();
                    comboBeatReached = 0;
                    comboInProgress = false;
                    break;

                case Step_DashJudge:
                    dashSweepDone = false;
                    PrepareVigor(OnikiriPlayer.VigorMax);
                    break;

                case Step_Annihilate:
                    SaveResourceSnapshot();
                    PrepareVigor(OnikiriPlayer.VigorMax);
                    PrepareStance(OnikiriPlayer.AnnihilateCost);
                    break;

                case Step_Finale:
                    PrepareVigor(OnikiriPlayer.VigorMax);
                    PrepareStance(OnikiriPlayer.StanceMax);
                    break;

                case Step_Close:
                    //收域步骤：监听 DomainPhaseSettled(Closed)
                    break;

                case Step_Done:
                    FinishTutorial();
                    break;
            }
        }

        //====事件处理====

        private static void HandleComboBeatHit(int beat, NPC target)
        {
            if (currentStep != Step_Combo) return;
            if (!IsOurTarget(target)) return;
            if (beat != comboBeatReached) { comboBeatReached = 0; return; } //顺序断开则重置
            comboBeatReached++;
            if (comboBeatReached >= 5) AdvanceStep();
        }

        private static void HandleDashJudged()
        {
            if (currentStep != Step_DashJudge) return;
            AdvanceStep();
        }

        private static void HandleZanshinHit(NPC target)
        {
            if (currentStep != Step_Zanshin) return;
            if (!IsOurTarget(target)) return;
            AdvanceStep();
            WriteCheckpoint(Checkpoint_Combat);
        }

        private static void HandleSakuraReleased()
        {
            if (currentStep != Step_Sakura) return;
            AdvanceStep();
        }

        private static void HandleExecutionAnnihilate()
        {
            if (currentStep != Step_Annihilate) return;
            AdvanceStep();
        }

        private static void HandleExecutionFinale(NPC target)
        {
            if (currentStep != Step_Finale) return;
            if (!IsOurTarget(target)) return;
            AdvanceStep();
        }

        private static void HandleDomainPhaseSettled(OniDomains.OniDomainPhase phase)
        {
            switch (currentStep)
            {
                case Step_Sakura:
                    //等待表世界开启
                    if (phase == OniDomains.OniDomainPhase.Omote) { /* 告知玩家可疾走 */ }
                    break;
                case Step_Dismember:
                    //等待里世界开启
                    if (phase == OniDomains.OniDomainPhase.Ura) { /* 面影已自动快门 */ }
                    break;
                case Step_Close:
                    if (phase == OniDomains.OniDomainPhase.Closed) AdvanceStep();
                    break;
            }
        }

        private static void HandleDismemberLanded(NPC target)
        {
            if (currentStep != Step_Dismember) return;
            if (!IsOurTarget(target)) return;
            AdvanceStep();
        }

        //====推进逻辑====

        private static void AdvanceIfReady()
        {
            //HUD 认知：较久无操作也自动推进（主路径靠「已知晓」按钮）
            if (currentStep == Step_HudIntro && stepTimer > 60 * 20)
            {
                AdvanceStep();
                return;
            }

            //点鬼簿：打开即视为找到入口
            if (currentStep == Step_Register && (OniRegisterUI.Instance?.IsOpen ?? false))
            {
                AdvanceStep();
                return;
            }

            //改铭台：打开过再收台，或渲染层「已知晓」推进
            if (currentStep == Step_Mei)
            {
                if (OniMeiUI.Instance?.IsOpen ?? false) {
                    meiOpenedThisStep = true;
                }
                else if (meiOpenedThisStep) {
                    AdvanceStep();
                }
            }
        }

        private static void AdvanceStep()
        {
            //检查点写入：段落组完成时落盘
            if (currentStep == Step_Mei) WriteCheckpoint(Checkpoint_Hud);
            if (currentStep == Step_Zanshin) WriteCheckpoint(Checkpoint_Combat);
            if (currentStep == Step_Close) WriteCheckpoint(Checkpoint_Advanced);

            currentStep++;
            if (currentStep <= Step_Done) EnterStep(currentStep);
        }

        //====辅助====

        private static bool IsOurTarget(NPC npc)
            => npc != null && npc.active
            && OnikiriTutorialWraith.GetLocalTarget()?.whoAmI == npc.whoAmI;

        private static void WriteCheckpoint(int checkpoint)
        {
            var guide = Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<OnikiriGuideData>();
            if (guide.Checkpoint < checkpoint) guide.Checkpoint = checkpoint;
        }

        private static void PrepareVigor(float amount)
        {
            if (Main.LocalPlayer.TryGetModPlayer(out OnikiriPlayer okp))
                okp.Vigor = System.Math.Min(amount, okp.VigorMaxCurrent);
        }

        private static void PrepareStance(float amount)
        {
            if (Main.LocalPlayer.TryGetModPlayer(out OnikiriPlayer okp))
                okp.Stance = System.Math.Min(amount, OnikiriPlayer.StanceMax);
        }

        private static void SaveResourceSnapshot()
        {
            if (Main.LocalPlayer.TryGetModPlayer(out OnikiriPlayer okp))
            {
                savedVigor = okp.Vigor;
                savedStance = okp.Stance;
            }
        }

        private static void RestoreMeiSnapshotIfNeeded()
        {
            if (meiSnapshot == null) return;
            var data = OnikiriData.TryGet(Main.LocalPlayer?.GetItem());
            if (data == null) { meiSnapshot = null; return; }
            data.Mei.CopyFrom(meiSnapshot.Store);
            WraithVessels.SyncSlot(Main.LocalPlayer, Main.LocalPlayer.GetItem());
            meiSnapshot = null;
        }

        private static void FinishTutorial()
        {
            RestoreMeiSnapshotIfNeeded();
            OnikiriTutorialNet.RequestReleaseTarget();
            OnikiriTutorialLead.MarkComplete();
            Unsubscribe();
        }

        //====铭位临时快照类型====
        internal sealed class OniMeiSnapshot
        {
            internal readonly Inscriptions.OniMeiStore Store = new();
            internal OniMeiSnapshot(Inscriptions.OniMeiStore source) => Store.CopyFrom(source);
        }

        /// <summary>教程进入改铭台仪式前调用：记录铭位快照；退出时自动恢复</summary>
        internal static void BeginMeiTransaction(Inscriptions.OniMeiStore current)
            => meiSnapshot = new OniMeiSnapshot(current);

        /// <summary>OniMeiUI.OnClose 回调：若有教程快照则恢复铭位，保证练习铭不写入存档</summary>
        internal static void RestoreMeiSnapshotOnClose()
            => RestoreMeiSnapshotIfNeeded();
    }
}