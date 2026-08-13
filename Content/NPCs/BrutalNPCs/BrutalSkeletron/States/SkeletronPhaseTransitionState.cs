using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>断手狂化：绞回双手→逐手殉解→死寂→冠火爆发</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.PhaseTransition, typeof(SkeletronStateContext))]
    internal class SkeletronPhaseTransitionState : SkeletronStateBase
    {
        public override string StateName => "PhaseTransition";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.PhaseTransition;

        internal const int LeftTornBeat = 48;   //左手殉解拍（与 HandTornState 对齐）
        internal const int RightTornBeat = 78;
        internal const int SilenceStart = 92;
        internal const int BurstFrame = 126;
        internal const int TransitionEnd = 204;

        public override void OnEnter(SkeletronStateContext context) {
            base.OnEnter(context);
            //公平阀：清空本Boss敌对弹幕
            SkeletronFacts.ClearHostileProjectiles();
        }

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = true;
            npc.velocity *= 0.9f;

            //稳在玩家上空
            Vector2 hold = context.Target.Center + new Vector2(0f, -340f);
            npc.Center = Vector2.Lerp(npc.Center, hold, 0.035f);

            UpdateBeats(context, npc);

            Timer++;
            if (Timer >= TransitionEnd && !VaultUtils.isClient) {
                return new SkeletronHubState();
            }
            return null;
        }

        private void UpdateBeats(SkeletronStateContext context, NPC npc) {
            //殉手吸收：手侧位置的幽火束涌向头（拍点与 HandTornState 对齐）
            if (!VaultUtils.isServer && (Timer == LeftTornBeat || Timer == RightTornBeat)) {
                int side = Timer == LeftTornBeat ? -1 : 1;
                Vector2 handPos = npc.Center + new Vector2(side * 120f, -30f);
                SoundEngine.PlaySound(SoundID.NPCDeath2 with { Volume = 1f, Pitch = -0.5f }, handPos);
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.7f, Pitch = -0.3f }, handPos);
                SkeletronScreenEffects.PushShake(npc.Center, 6f);
                for (int i = 0; i < 18; i++) {
                    Vector2 pos = handPos + Main.rand.NextVector2Circular(36f, 36f);
                    PRTLoader.NewParticle<PRT_SkeleGhostFlame>(pos, (npc.Center - pos) * 0.08f,
                        SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(1.4f, 2.4f))?.Configure(Main.rand.Next(22, 36));
                }
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_SkeleBoneChip>(handPos + Main.rand.NextVector2Circular(28f, 28f),
                        new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, -1f)),
                        Color.White, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(38, 66));
                }
            }

            //吞手后的眼火涌动
            if (Timer > LeftTornBeat && Timer < SilenceStart) {
                context.EyeFlame = 1.2f + 0.3f * (float)System.Math.Sin(Timer * 0.4f);
            }

            //死寂：头颅缓慢前倾，眼火将熄
            if (Timer >= SilenceStart && Timer < BurstFrame) {
                float t = (Timer - SilenceStart) / (float)(BurstFrame - SilenceStart);
                npc.rotation = npc.rotation.AngleLerp(0.4f, 0.06f);
                context.EyeFlame = MathHelper.Lerp(1.2f, 0.25f, t);
                SkeletronScreenEffects.RequestDomain(t * 0.5f);
            }

            //爆发：冠火点燃，环状颅火带缺口迸射
            if (Timer == BurstFrame) {
                npc.rotation = 0f;
                npc.ai[SkeletronAiSlots.HeadPhase] = SkeletronPhase.Unbound;
                context.EyeFlame = 1.6f;

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.2f, Pitch = -0.5f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 1f, Pitch = -0.6f }, npc.Center);
                    SkeletronScreenEffects.PushShockRing(npc.Center, 1.1f, 760f, 30);
                    SkeletronScreenEffects.PushShake(npc.Center, 10f);
                    for (int i = 0; i < 40; i++) {
                        PRTLoader.NewParticle<PRT_SkeleGhostFlame>(npc.Center + Main.rand.NextVector2Circular(50f, 50f),
                            Main.rand.NextVector2CircularEdge(8f, 8f),
                            SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(1.6f, 2.6f))?.Configure(Main.rand.Next(26, 44));
                    }
                }

                if (!VaultUtils.isClient) {
                    //十二向颅火环，留两处缺口
                    int damage = SkullDamage(context);
                    float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    for (int i = 0; i < 12; i++) {
                        if (i == 3 || i == 8) {
                            continue;
                        }
                        Vector2 vel = (baseAngle + MathHelper.TwoPi * i / 12f).ToRotationVector2() * 4.6f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                            ModContent.ProjectileType<SkeletronCursedSkull>(), damage, 0f, Main.myPlayer, 0f, 0f);
                    }
                    npc.netUpdate = true;
                }
            }

            //爆发后收束
            if (Timer > BurstFrame) {
                context.EyeFlame = MathHelper.Lerp(context.EyeFlame, 1.1f, 0.06f);
            }
        }

        public override void OnExit(SkeletronStateContext context) {
            base.OnExit(context);
            context.Npc.dontTakeDamage = false;
            context.Npc.rotation = 0f;
        }
    }
}
