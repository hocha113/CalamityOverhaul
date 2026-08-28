using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>
    /// 溢血喷泉：仰身自旋，向上扇喷重力血珠雨成区域压制；收势眩晕是刻意留给玩家的输出窗</summary>
    [InnoVault.StateMachines.VaultState((int)EocStateIndex.BloodFountain, typeof(EocStateContext))]
    internal class EocBloodFountainState : EocStateBase
    {
        public override string StateName => "EocBloodFountain";
        public override EocStateIndex StateIndex => EocStateIndex.BloodFountain;

        private const int AnchorTime = 36;
        private const int SprayTime = 118;
        private const int DizzyTime = 40;

        private int SprayInterval => Context.IsAsuraMode ? 4 : 5;

        private EocStateContext Context;
        private float spinSpeed;

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            Context = context;
            spinSpeed = 0f;
        }

        public override IEocState OnUpdate(EocStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            DisableContactDamage(npc);

            Timer++;

            if (Timer <= AnchorTime) {
                //占位仰身
                Vector2 anchor = player.Center + new Vector2(0f, -360f);
                EocMotion.SpringHover(npc, anchor, 0.022f, 0.11f, 26f);
                float progress = Timer / (float)AnchorTime;
                //瞳孔上仰
                npc.rotation = npc.rotation.AngleLerp(MathHelper.Pi, 0.12f);
                context.SetChargeState(1, progress);
                if (Timer == 1 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie2 with { Volume = 0.9f, Pitch = -0.3f }, npc.Center);
                }
                if (Timer % 2 == 0) {
                    EocMotion.ConvergeStreaks(npc.Center, progress, 110f);
                }
                return null;
            }

            if (Timer <= AnchorTime + SprayTime) {
                //自旋加速喷洒
                int sprayTimer = Timer - AnchorTime;
                float progress = sprayTimer / (float)SprayTime;
                spinSpeed = MathHelper.Lerp(0.06f, 0.4f, VaultUtils.EaseInQuad(MathHelper.Clamp(progress * 1.6f, 0f, 1f)));
                npc.rotation += spinSpeed;
                //悬停微沉，喷泉的后坐
                Vector2 anchor = player.Center + new Vector2(0f, -350f);
                EocMotion.SpringHover(npc, anchor, 0.012f, 0.12f, 14f);
                npc.position += Main.rand.NextVector2Circular(1.4f, 1.4f) * MathHelper.Clamp(progress * 2f, 0f, 1f);

                if (sprayTimer % SprayInterval == 0) {
                    if (!VaultUtils.isClient) {
                        //扇形喷泉弹×2，两成瞄压玩家
                        for (int i = 0; i < 2; i++) {
                            Vector2 vel;
                            if (Main.rand.NextBool(5)) {
                                Vector2 predicted = EocMotion.PredictTarget(player, npc.Center, 15f, 0.4f);
                                vel = (predicted - npc.Center).SafeNormalize(-Vector2.UnitY)
                                    .RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * 14.5f;
                            }
                            else {
                                float angle = -MathHelper.PiOver2 + Main.rand.NextFloat(-1.15f, 1.15f);
                                vel = angle.ToRotationVector2() * Main.rand.NextFloat(12f, 21f);
                            }
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                                ModContent.ProjectileType<EocBloodShot>(), 10, 0f, Main.myPlayer, 1f);
                        }
                    }
                    //喷洒飞沫与湿声
                    EocMotion.BloodSpray(npc.Center - Vector2.UnitY * 30f, -Vector2.UnitY, 3, 9f, 1f);
                    if (!VaultUtils.isServer && sprayTimer % (SprayInterval * 3) == 0) {
                        SoundEngine.PlaySound(SoundID.NPCDeath13 with {
                            Volume = 0.5f,
                            Pitch = 0.3f + progress * 0.3f
                        }, npc.Center);
                    }
                }

                context.PushIris(0.7f, EocMotion.Arterial);
                Lighting.AddLight(npc.Center, EocMotion.Arterial.ToVector3() * 0.8f);
                return null;
            }

            //收势眩晕：转速带过冲摆动衰减，明确的输出窗
            int dizzyTimer = Timer - AnchorTime - SprayTime;
            float dizzyT = dizzyTimer / (float)DizzyTime;
            spinSpeed *= 0.9f;
            npc.rotation += spinSpeed + (float)Math.Sin(dizzyT * MathHelper.Pi * 3f) * 0.05f * (1f - dizzyT);
            npc.velocity *= 0.93f;
            if (dizzyTimer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.7f, Pitch = -0.6f }, npc.Center);
            }

            if (Timer >= AnchorTime + SprayTime + DizzyTime) {
                if (VaultUtils.isClient) {
                    return null;
                }
                return new EocVeilHoverState(Context.IsAsuraMode ? 40 : 52);
            }

            return null;
        }
    }
}
