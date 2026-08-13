using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>入场演出：暴雪合拢→雪幕剪影踏近→独眼点亮→怒吼亮相</summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.Intro, typeof(DeerclopsStateContext))]
    internal class DeerclopsIntroState : DeerclopsStateBase
    {
        public override string StateName => "Intro";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.Intro;

        private const int WalkEnd = 150;
        private const int EyeIgnite = 155;
        private const int RoarStart = 178;
        private const int IntroEnd = 248;

        public override void OnEnter(DeerclopsStateContext context) {
            base.OnEnter(context);
            context.Dissolve = 1f;
            context.EyeGlow = 0f;
            context.EyeHeat = 0f;
        }

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            //全程无害无伤，暴雪从零合拢
            npc.dontTakeDamage = true;
            npc.damage = 0;
            context.VeilTarget = MathHelper.Clamp(Timer / 90f, 0f, 1f) * 0.5f;

            if (Timer == 1 && !Main.dedServ) {
                //远方低吼，暴雪先声
                SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 0.45f, Pitch = -0.75f }, context.Target?.Center ?? npc.Center);
            }

            //幕一：雪幕中的剪影踏近
            if (Timer <= WalkEnd) {
                context.MoveSpeedMult = 0.55f;
                context.Dissolve = MathHelper.Clamp(1f - Timer / 120f, 0f, 1f);

                //沉重脚步的地面细尘(本端)
                if (!Main.dedServ && Timer % 24 == 0 && DeerclopsMotion.OnScreen(npc.Bottom)) {
                    DeerclopsMotion.CameraPunch(npc.Bottom, 1.6f, 10, "DeerIntroStep", Vector2.UnitY);
                    for (int i = 0; i < 5; i++) {
                        Dust dust = Dust.NewDustPerfect(npc.Bottom + new Vector2(Main.rand.NextFloat(-40f, 40f), 0f),
                            DustID.Snow, new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.5f, 2f)), 90, default, Main.rand.NextFloat(1f, 1.6f));
                        dust.noGravity = Main.rand.NextBool();
                    }
                }
                return null;
            }

            //幕二：站定，独眼点亮
            context.HaltMovement = true;
            context.Dissolve = 0f;

            if (Timer <= RoarStart) {
                float p = MathHelper.Clamp((Timer - EyeIgnite) / 18f, 0f, 1f);
                context.EyeGlow = Math.Max(context.EyeGlow, p);
                if (Timer == EyeIgnite && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with { Volume = 0.7f, Pitch = 0.35f }, npc.Center);
                }
                return null;
            }

            //幕三：怒吼亮相
            context.AnimMode = DeerAnimMode.Roar;
            context.AnimTimer = Timer - RoarStart;
            context.EyeGlow = 1f;
            context.VeilTarget = 0.68f;

            if (Timer == RoarStart + 8 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 1.2f }, npc.Center);
            }
            if (Timer == RoarStart + 14) {
                DeerclopsMotion.CameraPunch(npc.Center, 9f, 26, "DeerIntroRoar");
                //环状雪爆(本端表现)
                if (!Main.dedServ) {
                    for (int i = 0; i < 34; i++) {
                        float angle = MathHelper.TwoPi * i / 34f;
                        Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 9f);
                        Dust dust = Dust.NewDustPerfect(npc.Center, DustID.Snow, vel, 60, default, Main.rand.NextFloat(1.2f, 2f));
                        dust.noGravity = true;
                    }
                    for (int i = 0; i < 10; i++) {
                        Dust dust = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(40f, 60f),
                            DustID.Frost, -Vector2.UnitY * Main.rand.NextFloat(2f, 6f), 80, default, Main.rand.NextFloat(1f, 1.7f));
                        dust.noGravity = true;
                    }
                }
            }
            //吼声中的持续微震
            if (Timer > RoarStart + 14 && Timer < IntroEnd - 20 && Timer % 8 == 0) {
                DeerclopsMotion.CameraPunch(npc.Center, 2.4f, 10, "DeerIntroRumble");
            }

            if (Timer >= IntroEnd) {
                return new DeerclopsStalkState();
            }
            return null;
        }

        public override void OnExit(DeerclopsStateContext context) {
            base.OnExit(context);
            context.Dissolve = 0f;
            context.Npc.dontTakeDamage = false;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
