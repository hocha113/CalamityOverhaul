using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 转阶段演出：生命过半后机体过载——四条机械臂依次殉爆（由机械臂侧按各自延迟自毁），
    /// 警报与连环爆炸中头部升至高空；死亡模式下注能修复装甲并召回双子魔眼，
    /// 最后一声咆哮宣告狂暴阶段（<see cref="PrimePhase.Rage"/>）开始。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.PhaseTransition, typeof(PrimeStateContext))]
    internal class PrimePhaseTransitionState : PrimeStateBase
    {
        public override string StateName => "PhaseTransition";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.PhaseTransition;

        /// <summary>殉爆窗口长度，机械臂的自毁延迟都安排在该窗口内</summary>
        internal const int DetonationWindow = 80;

        private int healDuration;
        private int healPerFrame;
        private bool healStarted;

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            healStarted = false;
            if (!VaultUtils.isClient) {
                context.Npc.TargetClosest();
                context.Npc.netUpdate = true;
            }
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.Zero;
            context.FrameMode = 0;

            //两端确定性 Lerp 升至高空定点
            Vector2 toPoint = context.Target.Center + new Vector2(0, context.DeathMode ? -400 : -500);
            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.05f);
            LeanTowards(npc, context.Target.Center);

            int totalDuration = DetonationWindow + HealDuration(context);
            context.SetChargeState(2, Timer / (float)totalDuration);

            if (Timer < DetonationWindow) {
                UpdateDetonationWindow(context);
            }
            else {
                UpdateOverloadReboot(context);
            }

            Timer++;
            if (Timer >= totalDuration) {
                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage * 2;
                npc.ai[PrimeAiSlots.HeadPhase] = PrimePhase.Rage;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                }
                if (!VaultUtils.isClient) {
                    return new PrimeRageHoverState();
                }
            }
            return null;
        }

        private int HealDuration(PrimeStateContext context) {
            //死亡模式（非Boss急速）才有完整的注能修复，其余难度只是短暂排气重启
            return context.DeathMode && !context.BossRush ? 280 : 60;
        }

        /// <summary>殉爆窗口：警报蜂鸣，机械臂逐一爆裂，机体接缝喷溅火花</summary>
        private void UpdateDetonationWindow(PrimeStateContext context) {
            NPC npc = context.Npc;

            if (VaultUtils.isServer) {
                return;
            }

            if (Timer == 5) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.7f, Volume = 0.9f }, npc.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.5f, Volume = 0.6f }, npc.Center);
            }

            //与机械臂自毁节拍呼应的震屏
            if (Timer % 15 == 5) {
                PrimeDeathPerformancePlayer.RequestShake(5f, 10);
            }

            if (Timer % 4 == 0) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(2f, 7f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, Color.OrangeRed,
                    Main.rand.NextFloat(0.8f, 1.4f)).Configure(true, Main.rand.Next(12, 22));
            }
            Lighting.AddLight(npc.Center, new Color(255, 120, 50).ToVector3() * 0.9f);
        }

        /// <summary>过载重启：死亡模式注能回血并召回双子，其余难度排气整备</summary>
        private void UpdateOverloadReboot(PrimeStateContext context) {
            NPC npc = context.Npc;
            int healTime = Timer - DetonationWindow;

            if (!healStarted) {
                healStarted = true;
                healDuration = HealDuration(context);
                bool fullHeal = context.DeathMode && !context.BossRush;
                healPerFrame = fullHeal ? System.Math.Max((npc.lifeMax - npc.life) / healDuration, 0) : 0;

                if (!VaultUtils.isServer && healPerFrame > 0) {
                    SoundEngine.PlaySound(CWRSound.MechanicalFullBloodFlow, Main.LocalPlayer.Center);
                }
            }

            //死亡模式召回双子魔眼协同狂暴阶段
            if (healTime == 10 && context.DeathMode && !context.BossRush && !VaultUtils.isClient) {
                context.Owner.SpawnEye();
            }

            if (healPerFrame > 0) {
                if (npc.life >= npc.lifeMax) {
                    npc.life = npc.lifeMax;
                }
                else {
                    npc.life += healPerFrame;
                    Lighting.AddLight(npc.Center, Color.White.ToVector3());
                    if (healTime % 4 == 0) {
                        CombatText.NewText(npc.Hitbox, CombatText.HealLife, healPerFrame);
                    }
                }
            }
            else if (!VaultUtils.isServer && healTime % 8 == 0) {
                //无注能时的排气烟雾
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f);
                PRTLoader.NewParticle<PRT_Smoke>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.8f, 1.6f),
                    Color.Lerp(new Color(60, 56, 54), new Color(24, 22, 22), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.7f, 1.1f)).Configure(Main.rand.Next(40, 70), 0.7f, Main.rand.NextFloat(-0.04f, 0.04f));
            }
        }
    }
}
