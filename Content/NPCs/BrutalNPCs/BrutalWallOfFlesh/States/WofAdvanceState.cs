using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.TimeFreezes;
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

        public override void OnEnter(WofStateContext context) {
            base.OnEnter(context);
            //间隔长度按上一招重量伸缩(节奏波形)；LastAttack仅服务端可信，
            //镜像进ai[0]随NPC同步，客户端速度包络与之一致(防墙体橡皮筋)
            if (!VaultUtils.isClient) {
                context.Npc.ai[0] = WofDirector.AdvanceGapFrames(context.Phase, context.IsDeathMode)
                    * WofDirector.AttackGapMul(context.LastAttack);
                context.Npc.netUpdate = true;
            }
        }

        public override void OnExit(WofStateContext context) {
            base.OnExit(context);
            if (!VaultUtils.isClient) {
                context.Npc.ai[0] = 0f;
            }
        }

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            int gap = npc.ai[0] > 0f ? (int)npc.ai[0]
                : WofDirector.AdvanceGapFrames(context.Phase, context.IsDeathMode);

            //推进速度即节奏乐器：前段喘息回稳→中段常速→末段蓄势跃进，
            //出招瞬间各招自带的减速前摇与跃进形成对比刹车
            float t = MathHelper.Clamp(Timer / (float)gap, 0f, 1f);
            if (t < WofDirector.GapLullFraction) {
                context.AdvanceFactor = MathHelper.Lerp(WofDirector.GapLullFactor, 1f,
                    t / WofDirector.GapLullFraction);
            }
            else if (t > 1f - WofDirector.GapChargeFraction) {
                float c = (t - (1f - WofDirector.GapChargeFraction)) / WofDirector.GapChargeFraction;
                context.AdvanceFactor = MathHelper.Lerp(1f, WofDirector.GapChargePeak, c * c);
                //蓄势可视化：潮红上涌、渗血变密(盖过主控基线心跳)
                context.WallFlush = System.Math.Max(context.WallFlush, 0.25f + 0.45f * c);
                if (!VaultUtils.isServer && Timer % 3 == 0) {
                    WofMotionFX.SpawnWallSeep(npc, 1f + c * 2f);
                }
            }

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

            //防重招连击：上一招是重招且袋首也是重招时，换到首个轻/中招(波形保证)
            if (WofDirector.IsHeavyAttack(context.LastAttack)
                && WofDirector.IsHeavyAttack(context.AttackBag[0])) {
                for (int i = 1; i < context.AttackBag.Count; i++) {
                    if (!WofDirector.IsHeavyAttack(context.AttackBag[i])) {
                        (context.AttackBag[0], context.AttackBag[i]) = (context.AttackBag[i], context.AttackBag[0]);
                        break;
                    }
                }
            }

            WofStateIndex pick = context.AttackBag[0];
            context.AttackBag.RemoveAt(0);

            //投技冷却中或目标不在推进前方 → 退化为普通舌鞭
            if (pick == WofStateIndex.TongueGrab
                && (context.GrabCooldown > 0 || !TargetInFront(context)
                    || TimeFreezeSystem.IsAnyGlobalFreezeActive)) {
                pick = WofStateIndex.TongueLash;
            }
            //记实际出的招：间隔权重与防复读都按真实招计算
            context.LastAttack = pick;

            return pick switch {
                WofStateIndex.SurgeDash => new WofSurgeDashState(),
                WofStateIndex.MawVortex => new WofMawVortexState(),
                WofStateIndex.EyeScan => new WofEyeScanState(),
                WofStateIndex.HungryNet => new WofHungryNetState(),
                WofStateIndex.LeechWave => new WofLeechWaveState(),
                WofStateIndex.FleshSpike => new WofFleshSpikeState(),
                WofStateIndex.TongueLash => new WofTongueLashState(),
                WofStateIndex.TongueGrab => new WofTongueGrabState(),
                WofStateIndex.JawRipple => new WofJawRippleState(),
                WofStateIndex.RotGuillotine => new WofRotGuillotineState(),
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
                //舌卷回吞投技：扣押到二阶段(冷却由PickNextAttack兜底)
                pool.Add(WofStateIndex.TongueGrab);
                //腐眼断头闸：水平斩束封锁跑道，逼迫起跳
                pool.Add(WofStateIndex.RotGuillotine);
            }
            //阶段3双倍突进权重：死线更凶；饥饿长城入袋(大迁徙后首秀过的王牌)
            if (context.Phase >= 3) {
                pool.Add(WofStateIndex.SurgeDash);
                pool.Add(WofStateIndex.JawRipple);
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
