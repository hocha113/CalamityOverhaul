using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States
{
    /// <summary>推进枢纽：两招之间的死线压迫拍，洗牌袋选下一招</summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.Advance, typeof(WofStateContext))]
    internal class WofAdvanceState : WofStateBase
    {
        public override string StateName => "Advance";
        public override WofStateIndex StateIndex => WofStateIndex.Advance;

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            //推进本身就是压迫，间隔期不额外演出，只留一次咬牙宣告
            int gap = WofDirector.AdvanceGapFrames(context.Phase, context.IsDeathMode);
            if (Timer == gap - 24 && !VaultUtils.isServer && WofMotionFX.OnScreen(npc.Center)) {
                //下一招前的咬牙前摇
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.5f, Volume = 0.85f }, npc.Center);
            }
            if (Timer >= gap - 24) {
                context.MouthCommand = 2;
            }

            //脱屏激怒期不出招：狂奔本身是招，也避免屏外扫射
            if (context.FarEnraged) {
                Timer = System.Math.Min(Timer, gap - 30);
                return null;
            }

            if (Timer < gap || VaultUtils.isClient) {
                return null;
            }

            return PickNextAttack(context);
        }

        /// <summary>洗牌袋选招(服务端)</summary>
        private IWofState PickNextAttack(WofStateContext context) {
            if (context.AttackBag.Count == 0) {
                RefillBag(context);
            }

            WofStateIndex pick = context.AttackBag[0];
            context.AttackBag.RemoveAt(0);
            context.LastAttack = pick;

            return pick switch {
                WofStateIndex.SurgeDash => new WofSurgeDashState(),
                WofStateIndex.MawVortex => new WofMawVortexState(),
                WofStateIndex.EyeScan => new WofEyeScanState(),
                WofStateIndex.HungryNet => new WofHungryNetState(),
                WofStateIndex.LeechWave => new WofLeechWaveState(),
                WofStateIndex.FleshSpike => new WofFleshSpikeState(),
                WofStateIndex.TongueLash => new WofTongueLashState(),
                _ => new WofSurgeDashState(),
            };
        }

        /// <summary>重填洗牌袋：全招入袋、洗乱、防复读</summary>
        private void RefillBag(WofStateContext context) {
            List<WofStateIndex> pool = [
                WofStateIndex.SurgeDash,
                WofStateIndex.EyeScan,
                WofStateIndex.TongueLash,
                WofStateIndex.LeechWave,
            ];
            if (context.Phase >= 2) {
                pool.Add(WofStateIndex.MawVortex);
                pool.Add(WofStateIndex.HungryNet);
                pool.Add(WofStateIndex.FleshSpike);
            }
            //阶段3双倍突进权重：死线更凶
            if (context.Phase >= 3) {
                pool.Add(WofStateIndex.SurgeDash);
            }

            //洗牌
            for (int i = pool.Count - 1; i > 0; i--) {
                int j = Main.rand.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            //防复读：袋首与上一招相同则塞到袋尾
            if (pool.Count > 1 && pool[0] == context.LastAttack) {
                WofStateIndex first = pool[0];
                pool.RemoveAt(0);
                pool.Add(first);
            }

            context.AttackBag.Clear();
            context.AttackBag.AddRange(pool);
        }
    }
}
