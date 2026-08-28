using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>转阶段演出</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.PhaseTransition, typeof(PrimeStateContext))]
    internal class PrimePhaseTransitionState : PrimeStateBase
    {
        public override string StateName => "PhaseTransition";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.PhaseTransition;

        /// <summary>殉爆窗口帧</summary>
        internal static int DetonationWindow => 80;

        private bool healStarted;

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            healStarted = false;

            //立刻挂狂暴阶段标记，防重复进转阶段
            context.Npc.ai[PrimeAiSlots.HeadPhase] = PrimePhase.Rage;

            if (!VaultUtils.isClient) {
                context.Npc.TargetClosest();
                context.Npc.netUpdate = true;
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.hostile) {
                        p.Kill();
                    }
                }
            }
            if (!VaultUtils.isServer) {
                PrimeScreenEffects.PushShockRing(context.Npc.Center, 0.75f, 520f);
            }
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.Zero;
            context.FrameMode = 0;

            //每帧重申阶段标记
            npc.ai[PrimeAiSlots.HeadPhase] = PrimePhase.Rage;

            //Lerp升至高空定点
            Vector2 toPoint = context.Target.Center + new Vector2(0, context.AsuraMode ? -400 : -500);
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
                //兜底
                npc.life = npc.lifeMax;

                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage * 2;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                }
                if (!VaultUtils.isClient) {
                    return new PrimeRageConnectorState();
                }
            }
            return null;
        }

        private int HealDuration(PrimeStateContext context) {
            //BossRush压缩注能时长
            return context.BossRush ? 120 : 260;
        }

        /// <summary>殉爆窗口</summary>
        private void UpdateDetonationWindow(PrimeStateContext context) {
            NPC npc = context.Npc;

            if (VaultUtils.isServer) {
                return;
            }

            if (Timer == 5) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.7f, Volume = 0.9f }, npc.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.5f, Volume = 0.6f }, npc.Center);
            }

            //呼应臂自毁的震屏
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

        /// <summary>过载重启</summary>
        private void UpdateOverloadReboot(PrimeStateContext context) {
            NPC npc = context.Npc;
            int healTime = Timer - DetonationWindow;
            int healDuration = HealDuration(context);

            if (!healStarted) {
                healStarted = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.MechanicalFullBloodFlow, Main.LocalPlayer.Center);
                }
            }

            //按剩余缺口/剩余帧补血，窗末满血
            int remainingFrames = System.Math.Max(healDuration - healTime, 1);
            int missing = npc.lifeMax - npc.life;
            if (missing > 0) {
                int addNum = System.Math.Max(missing / remainingFrames, 1);
                npc.life = System.Math.Min(npc.life + addNum, npc.lifeMax);
                Lighting.AddLight(npc.Center, Color.White.ToVector3());
                if (healTime % 4 == 0) {
                    CombatText.NewText(npc.Hitbox, CombatText.HealLife, addNum);
                }
            }
            else if (!VaultUtils.isServer && healTime % 8 == 0) {
                //满血后排气烟雾
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f);
                PRTLoader.NewParticle<PRT_Smoke>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.8f, 1.6f),
                    Color.Lerp(new Color(60, 56, 54), new Color(24, 22, 22), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.7f, 1.1f)).Configure(Main.rand.Next(40, 70), 0.7f, Main.rand.NextFloat(-0.04f, 0.04f));
            }
        }
    }
}
