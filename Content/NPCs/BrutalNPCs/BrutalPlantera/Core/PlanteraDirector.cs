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
        /// <summary>激怒追加速度</summary>
        public static float EnrageSpeedBonus => 7f;
        /// <summary>悬吊leash 一阶段(远程压制保持距离)</summary>
        public static float LeashP1 => 460f;
        /// <summary>悬吊leash 二阶段(近身狂化贴脸)</summary>
        public static float LeashP2 => 250f;
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

        /// <summary>死亡模式时间压缩系数(蓄力/间隔乘它)</summary>
        public static float DeathTimeScale(PlanteraStateContext ctx) => ctx.IsDeathMode ? 0.8f : 1f;

        /// <summary>一阶段攻击池</summary>
        private static readonly PlanteraStateIndex[] Phase1Pool = [
            PlanteraStateIndex.SeedGatling,
            PlanteraStateIndex.GrapplePounce,
            PlanteraStateIndex.VineLattice,
            PlanteraStateIndex.SporeSow,
        ];

        /// <summary>二阶段攻击池</summary>
        private static readonly PlanteraStateIndex[] Phase2Pool = [
            PlanteraStateIndex.FrenzyPounce,
            PlanteraStateIndex.TentacleRing,
            PlanteraStateIndex.WhipBarrage,
            PlanteraStateIndex.SeedGatling,
        ];

        /// <summary>洗牌袋抽下一招，反连击；仅权威端调用</summary>
        public static PlanteraStateIndex NextAttack(PlanteraStateContext ctx) {
            if (ctx.AttackBag.Count == 0) {
                RefillBag(ctx);
            }

            PlanteraStateIndex pick = ctx.AttackBag[0];
            ctx.AttackBag.RemoveAt(0);
            ctx.LastAttack = pick;
            return pick;
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
                PlanteraStateIndex.BloomNova => new States.PlanteraBloomNovaState(),
                PlanteraStateIndex.Despawn => new States.PlanteraDespawnState(),
                PlanteraStateIndex.Death => new States.PlanteraDeathState(),
                _ => new States.PlanteraCanopyState(),
            };
        }
    }
}
