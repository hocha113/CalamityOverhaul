using CalamityOverhaul.Content.TimeFreezes;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core
{
    /// <summary>战斗调参中心+出招洗牌袋</summary>
    internal static class PlanteraDirector
    {
        /// <summary>猛扑预警帧数</summary>
        public static int PounceTelegraphFrames => 36;
        /// <summary>鞭刑单根预警帧数</summary>
        public static int WhipTelegraphFrames => 26;
        /// <summary>悬吊漂移基速 一阶段</summary>
        public static float DriftSpeedP1 => 6.2f;
        /// <summary>悬吊漂移基速 二阶段</summary>
        public static float DriftSpeedP2 => 8.8f;
        /// <summary>悬吊leash 一阶段(远程压制保持距离)</summary>
        public static float LeashP1 => 460f;
        /// <summary>悬吊leash 二阶段(近身狂化贴脸)</summary>
        public static float LeashP2 => 250f;
        /// <summary>二阶段悬吊移速全局倍率</summary>
        public static float Phase2SpeedMult => 1.35f;
        /// <summary>二阶段加速率倍率，压低=惯性更强(转向漂、刹不住)</summary>
        public static float Phase2InertiaMult => 0.65f;
        /// <summary>猛扑速度 一阶段</summary>
        public static float PounceSpeedP1 => 46f;
        /// <summary>猛扑速度 二阶段</summary>
        public static float PounceSpeedP2 => 54f;
        /// <summary>猛扑接触伤害速度门槛</summary>
        public static float PounceContactSpeedGate => 22f;
        /// <summary>格栅锚点半径</summary>
        public static float LatticeRadius => 540f;
        /// <summary>格栅梁基础耐久(承受玩家伤害量)</summary>
        public static int LatticeBeamHP => 900;
        /// <summary>触手处刑圈半径</summary>
        public static float TentacleRingRadius => 300f;
        /// <summary>场上孢子地雷上限</summary>
        public static int MaxSporeMines => 22;
        /// <summary>孢子连锁引信半径</summary>
        public static float SporeChainRadius => 150f;
        /// <summary>凋零绽放触发血量比</summary>
        public static float NovaLifeRatio => 0.25f;
        /// <summary>死亡演出触发血量</summary>
        public static int DeathPerformanceTriggerLife => 60;
        /// <summary>投技冷却(选择器层)</summary>
        public static int FeastCooldownTicks => 840;
        /// <summary>蜕壳后首个投技的最短延迟(转阶段宽限阀)</summary>
        public static int FeastPhaseEntryDelay => 480;
        /// <summary>投技点名最近距离(防贴脸难反应)</summary>
        public static float FeastMinRange => 300f;
        /// <summary>投技点名最远距离(超出必然空挥)</summary>
        public static float FeastMaxRange => 980f;

        /// <summary>时间压缩系数(蓄力/间隔乘它)：修罗模式0.8×，激怒再0.5×(间隔减半=攻速翻倍)，可叠加</summary>
        public static float TimeScale(PlanteraStateContext ctx) {
            float scale = ctx.IsAsuraMode ? 0.8f : 1f;
            if (ctx.IsEnraged) {
                scale *= 0.5f;
            }
            return scale;
        }

        /// <summary>一阶段攻击池</summary>
        private static readonly PlanteraStateIndex[] Phase1Pool = [
            PlanteraStateIndex.SeedGatling,
            PlanteraStateIndex.GrapplePounce,
            PlanteraStateIndex.VineLattice,
            PlanteraStateIndex.SporeSow,
        ];

        /// <summary>二阶段攻击池：狂扑双份权重，冲刺更频繁</summary>
        private static readonly PlanteraStateIndex[] Phase2Pool = [
            PlanteraStateIndex.FrenzyPounce,
            PlanteraStateIndex.FrenzyPounce,
            PlanteraStateIndex.TentacleRing,
            PlanteraStateIndex.WhipBarrage,
            PlanteraStateIndex.SeedGatling,
            PlanteraStateIndex.VineFeast,
        ];

        /// <summary>洗牌袋抽下一招，反连击；仅权威端调用</summary>
        public static PlanteraStateIndex NextAttack(PlanteraStateContext ctx) {
            if (ctx.AttackBag.Count == 0) {
                RefillBag(ctx);
            }

            PlanteraStateIndex pick = ctx.AttackBag[0];
            ctx.AttackBag.RemoveAt(0);

            //投技被点名但条件不齐→改打压制招；袋里还有别的招才顺延，
            //袋空时直接丢弃(下轮重洗自然回来)，防止袋永不见底饿死其他招式
            if (pick == PlanteraStateIndex.VineFeast && !FeastReady(ctx)) {
                if (ctx.AttackBag.Count > 0) {
                    ctx.AttackBag.Add(PlanteraStateIndex.VineFeast);
                }
                pick = PlanteraStateIndex.SeedGatling;
            }

            ctx.LastAttack = pick;
            return pick;
        }

        /// <summary>投技点名条件：冷却毕+目标有效+距离带内+无时停/运镜占用；权威端调用</summary>
        private static bool FeastReady(PlanteraStateContext ctx) {
            if (ctx.VineFeastCooldown > 0 || !ctx.Target.Alives()) {
                return false;
            }
            float dist = ctx.Npc.Distance(ctx.Target.Center);
            if (dist < FeastMinRange || dist > FeastMaxRange) {
                return false;
            }
            //世界时停/本体被冻结时不出投技
            if (TimeFreezeSystem.IsAnyGlobalFreezeActive || TimeFreezeSystem.IsFrozen(ctx.Npc)) {
                return false;
            }
            //单人下权威端即本地端：有别的运镜在播就不抢镜头
            if (!Main.dedServ && InnoVault.Cinematics.CutsceneDirector.CurrentClip != null) {
                return false;
            }
            return true;
        }

        private static void RefillBag(PlanteraStateContext ctx) {
            PlanteraStateIndex[] pool = ctx.IsPhase2 ? Phase2Pool : Phase1Pool;
            List<PlanteraStateIndex> bag = [.. pool];

            //Fisher-Yates 洗牌
            for (int i = bag.Count - 1; i > 0; i--) {
                int j = Main.rand.Next(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }

            //袋首等于上次出招则塞到袋尾
            if (bag.Count > 1 && bag[0] == ctx.LastAttack) {
                PlanteraStateIndex head = bag[0];
                bag.RemoveAt(0);
                bag.Add(head);
            }

            ctx.AttackBag.Clear();
            ctx.AttackBag.AddRange(bag);
        }

        /// <summary>状态工厂，按索引产实例(客户端恢复走 VaultStateRegistry，这里给权威端切换用)</summary>
        public static IPlanteraState CreateState(PlanteraStateIndex index) {
            return index switch {
                PlanteraStateIndex.Intro => new States.PlanteraIntroState(),
                PlanteraStateIndex.Canopy => new States.PlanteraCanopyState(),
                PlanteraStateIndex.SeedGatling => new States.PlanteraSeedGatlingState(),
                PlanteraStateIndex.GrapplePounce => new States.PlanteraGrapplePounceState(),
                PlanteraStateIndex.VineLattice => new States.PlanteraVineLatticeState(),
                PlanteraStateIndex.SporeSow => new States.PlanteraSporeSowState(),
                PlanteraStateIndex.PhaseTransition => new States.PlanteraPhaseTransitionState(),
                PlanteraStateIndex.FrenzyPounce => new States.PlanteraFrenzyPounceState(),
                PlanteraStateIndex.TentacleRing => new States.PlanteraTentacleRingState(),
                PlanteraStateIndex.WhipBarrage => new States.PlanteraWhipBarrageState(),
                PlanteraStateIndex.VineFeast => new States.PlanteraVineFeastState(),
                PlanteraStateIndex.BloomNova => new States.PlanteraBloomNovaState(),
                PlanteraStateIndex.Despawn => new States.PlanteraDespawnState(),
                PlanteraStateIndex.Death => new States.PlanteraDeathState(),
                _ => new States.PlanteraCanopyState(),
            };
        }
    }
}
