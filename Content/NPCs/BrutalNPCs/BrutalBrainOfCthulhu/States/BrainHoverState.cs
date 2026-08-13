using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States
{
    /// <summary>连接拍：短暂喘息+洗牌袋选招（服务端）</summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.Hover, typeof(BrainStateContext))]
    internal class BrainHoverState : BrainStateBase
    {
        public override string StateName => "Hover";
        public override BrainStateIndex StateIndex => BrainStateIndex.Hover;

        private int duration = 48;

        public BrainHoverState() {
        }

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.damage = 0;

            if (!VaultUtils.isClient) {
                //二阶段与低血时喘息更短——节奏越打越快
                duration = context.IsPhase2 ? Main.rand.Next(26, 40) : Main.rand.Next(40, 62);
                if (context.IsLowLife) {
                    duration = Math.Max(20, duration - 10);
                }
                //侧翼方位走 ai[1] 同步，客户端预测不至于往反方向漂
                npc.ai[1] = Main.rand.NextBool() ? 1f : -1f;
                npc.netUpdate = true;
            }
        }

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            npc.damage = 0;

            //侧翼呼吸位悬停（方位读同步槽）
            float sideSign = npc.ai[1] >= 0f ? 1f : -1f;
            Vector2 anchor = player.Center + new Vector2(sideSign * 400f, -170f);
            anchor.Y += (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.2f) * 22f;
            BrainMotion.SpringHover(npc, anchor, 0.014f, 0.09f, context.IsPhase2 ? 20f : 15f);

            //转阶段最高优先（进攻空窗期检查，防打断演出）
            if (!VaultUtils.isClient && !context.IsPhase2 && context.LifeRatio <= BrainOfCthulhuAI.Phase2LifeRatio) {
                return new BrainPhaseTransitionState();
            }

            if (Timer < duration || VaultUtils.isClient) {
                return null;
            }

            //低血大招：冷却结束保证插入
            if (context.IsPhase2 && context.IsLowLife && context.HeartAttackCooldown <= 0) {
                context.HeartAttackCooldown = 60 * 32;
                context.LastAttack = BrainStateIndex.HeartAttack;
                return new BrainHeartAttackState();
            }

            return PickNextAttack(context);
        }

        /// <summary>洗牌袋抽招（服务端专用）：袋空重洗，避免与上一招复读</summary>
        private static IBrainState PickNextAttack(BrainStateContext context) {
            if (context.AttackBag.Count == 0) {
                RefillBag(context);
            }

            BrainStateIndex pick = context.AttackBag[0];
            context.AttackBag.RemoveAt(0);
            context.LastAttack = pick;
            return CreateAttack(pick);
        }

        private static void RefillBag(BrainStateContext context) {
            List<BrainStateIndex> pool = [];
            if (!context.IsPhase2) {
                pool.Add(BrainStateIndex.MirrorFeint);
                pool.Add(BrainStateIndex.MirrorStrike);
                pool.Add(BrainStateIndex.BloodPulse);
                //飞眼编队招式需要活眼
                if (context.Creepers.Count >= 5) {
                    pool.Add(BrainStateIndex.OrbitCage);
                }
                if (context.Creepers.Count >= 4) {
                    pool.Add(BrainStateIndex.LanceWaves);
                }
            }
            else {
                pool.Add(BrainStateIndex.MirrorFeint);
                pool.Add(BrainStateIndex.MirrorStrike);
                pool.Add(BrainStateIndex.FrenzyChase);
                pool.Add(BrainStateIndex.MirrorMaze);
                pool.Add(BrainStateIndex.BloodRain);
            }

            //Fisher-Yates 洗牌
            for (int i = pool.Count - 1; i > 0; i--) {
                int j = Main.rand.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            //防复读：袋首与上一招相同则塞到袋尾
            if (pool.Count > 1 && pool[0] == context.LastAttack) {
                BrainStateIndex first = pool[0];
                pool.RemoveAt(0);
                pool.Add(first);
            }
            context.AttackBag.Clear();
            context.AttackBag.AddRange(pool);
        }

        private static IBrainState CreateAttack(BrainStateIndex index) {
            return index switch {
                BrainStateIndex.MirrorFeint => new BrainMirrorFeintState(),
                BrainStateIndex.MirrorStrike => new BrainMirrorStrikeState(),
                BrainStateIndex.OrbitCage => new BrainOrbitCageState(),
                BrainStateIndex.LanceWaves => new BrainLanceWavesState(),
                BrainStateIndex.BloodPulse => new BrainBloodPulseState(),
                BrainStateIndex.FrenzyChase => new BrainFrenzyChaseState(),
                BrainStateIndex.MirrorMaze => new BrainMirrorMazeState(),
                BrainStateIndex.BloodRain => new BrainBloodRainState(),
                _ => new BrainMirrorStrikeState(),
            };
        }

        public override void OnExit(BrainStateContext context) {
            base.OnExit(context);
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
