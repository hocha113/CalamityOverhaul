using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>入场演出：血雾自四野涌聚凝成眼球→死寂凝视→怒吼血爆开战</summary>
    [InnoVault.StateMachines.VaultState((int)EocStateIndex.Intro, typeof(EocStateContext))]
    internal class EocIntroState : EocStateBase
    {
        public override string StateName => "EocIntro";
        public override EocStateIndex StateIndex => EocStateIndex.Intro;
        public override bool AllowFogStep => false;

        private const int CondenseEnd = 120;   //凝聚完成
        private const int StareEnd = 142;      //死寂凝视
        private const int RoarFrame = 142;     //怒吼帧
        private const int TotalTime = 196;

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.velocity = Vector2.Zero;
            //以血雾形态入场
            context.FogHide = 1f;
            context.FogHideGoal = 1f;
        }

        public override IEocState OnUpdate(EocStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            //悬于玩家上方，慢慢压近
            Vector2 anchor = player.Center + new Vector2(0f, -390f);
            npc.Center = Vector2.Lerp(npc.Center, anchor, 0.035f);
            FaceTarget(npc, player.Center, 0.1f);

            if (Timer <= CondenseEnd) {
                //凝聚段：血丝内收+雾涌
                float progress = Timer / (float)CondenseEnd;
                context.FogHideGoal = MathHelper.Clamp(1.15f - progress * 1.3f, 0f, 1f);
                EocScreenFX.PushVignette(0.32f * progress);

                if (!VaultUtils.isServer) {
                    if (Timer % 2 == 0) {
                        EocMotion.ConvergeStreaks(npc.Center, progress * 0.7f, 300f * (1f - progress * 0.4f));
                    }
                    if (Timer % 5 == 0) {
                        EocMotion.MistPuff(npc.Center + Main.rand.NextVector2Circular(120f, 120f), 1, 1.2f, 0.4f);
                    }
                    //湿滞心跳，三声渐急
                    if (Timer == 46 || Timer == 82 || Timer == 108) {
                        SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.75f, Pitch = -0.8f }, npc.Center);
                        EocMotion.Shake(npc.Center, 2.2f, 7);
                        EocScreenFX.PushPulse(0.45f);
                    }
                }
            }
            else if (Timer <= StareEnd) {
                //死寂凝视：粒子全停，只有虹膜慢慢亮起
                context.FogHideGoal = 0f;
                npc.velocity = Vector2.Zero;
                float stareT = (Timer - CondenseEnd) / (float)(StareEnd - CondenseEnd);
                context.PushIris(stareT * 0.9f, EocMotion.IrisRed);
            }
            else {
                //怒吼开战
                if (Timer == RoarFrame + 1) {
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.25f, Pitch = -0.12f }, npc.Center);
                        SoundEngine.PlaySound(SoundID.NPCDeath12 with { Volume = 0.9f, Pitch = -0.4f }, npc.Center);
                        EocMotion.BloodBurst(npc.Center, 1.7f, playSound: false);
                        //双层扩散环
                        PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, EocMotion.Arterial, 0.3f)?
                            .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 2.1f, 24);
                        PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, EocMotion.BrightBlood * 0.7f, 0.18f)?
                            .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 1.4f, 18);
                    }
                    EocMotion.Shake(npc.Center, 9f, 18);
                    EocScreenFX.PushFlash(0.5f, 10);
                    context.PushIris(1f, EocMotion.IrisRed);
                    context.ScalePulse = 1.14f;
                }
                EocScreenFX.PushVignette(0.3f * (1f - (Timer - RoarFrame) / 54f));
            }

            Timer++;

            if (Timer >= TotalTime) {
                npc.dontTakeDamage = false;
                if (VaultUtils.isClient) {
                    return null;
                }
                return new EocVeilHoverState(46);
            }

            return null;
        }

        public override void OnExit(EocStateContext context) {
            base.OnExit(context);
            context.Npc.dontTakeDamage = false;
            context.FogHideGoal = 0f;
        }
    }
}
