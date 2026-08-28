using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>旋杀骨风暴：反向蓄势→直线旋冲，沿途侧向抛撒骨片幕</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.SpinBoneStorm, typeof(SkeletronStateContext))]
    internal class SkeletronSpinBoneStormState : SkeletronStateBase
    {
        public override string StateName => "SpinBoneStorm";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.SpinBoneStorm;

        /// <summary>缺口（契约3）：骨片幕周期性断档，每 CurtainGapPeriod 帧停撒 CurtainGapFrames 帧，
        /// 沿冲刺路径留出可穿行的幕墙豁口，发射循环直接读取</summary>
        private const int CurtainGapPeriod = 24;
        private const int CurtainGapFrames = 9;

        private int phase;      //0预备 1冲刺 2刹车 3收势
        private int phaseTimer;
        private Vector2 dashDir;

        public override void OnEnter(SkeletronStateContext context) {
            base.OnEnter(context);
            phase = 0;
            phaseTimer = 0;
        }

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            bool p2 = (int)npc.ai[SkeletronAiSlots.HeadPhase] >= SkeletronPhase.Unbound;
            int maxPasses = p2 ? 3 : 2;

            switch (phase) {
                case 0:
                    UpdateWindup(context, npc);
                    break;
                case 1:
                    UpdateDash(context, npc, p2);
                    break;
                case 2:
                    UpdateBrake(context, npc, maxPasses);
                    break;
                default:
                    UpdateSettle(context, npc);
                    if (phaseTimer > 24 && !VaultUtils.isClient) {
                        return new SkeletronHubState();
                    }
                    break;
            }

            phaseTimer++;
            Timer++;

            //超时兜底：任何原因卡死都退回 hub
            if (Timer > 480 && !VaultUtils.isClient) {
                return new SkeletronHubState();
            }
            return null;
        }

        private void Advance(int next) {
            phase = next;
            phaseTimer = 0;
        }

        /// <summary>预备：追踪锁角→晚锁定→pow8 反向抽身</summary>
        private void UpdateWindup(SkeletronStateContext context, NPC npc) {
            int telegraph = SkeletronDirector.DashTelegraphFrames;
            npc.damage = npc.defDamage;

            //前 2/3 持续追角，末 1/3 锁死给读秒
            if (phaseTimer < telegraph - 12) {
                dashDir = DirectionToTarget(context);
                if (!VaultUtils.isClient) {
                    npc.ai[SkeletronAiSlots.HeadParamB] = dashDir.ToRotation();
                }
            }
            else {
                dashDir = npc.ai[SkeletronAiSlots.HeadParamB].ToRotationVector2();
            }

            //反向抽身（幅度后段陡增）
            float t = phaseTimer / (float)telegraph;
            npc.velocity = -dashDir * MathF.Pow(t, 8f) * 21f;

            //转速攀升
            npc.rotation += 0.1f + t * 0.26f;
            context.SpinVortex = t * 0.7f;
            context.DashTelegraph = MathHelper.Clamp(t * 1.2f, 0f, 1f);
            context.EyeFlame = 1f + t * 0.5f;

            if (phaseTimer == 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Volume = 0.9f, Pitch = -0.2f }, npc.Center);
            }

            if (phaseTimer >= telegraph) {
                //一帧定速：直线读得快
                npc.velocity = dashDir * SkeletronDirector.SpinDashSpeed(context.AsuraMode, IsP2(npc));
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 1f, Pitch = -0.3f }, npc.Center);
                    SkeletronScreenEffects.PushShake(npc.Center, 5f);
                }
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                Advance(1);
            }
        }

        /// <summary>冲刺：旋转+侧向骨片幕，越过目标即离场</summary>
        private void UpdateDash(SkeletronStateContext context, NPC npc, bool p2) {
            npc.damage = (int)(npc.defDamage * SkeletronDirector.SpinDamageMult);
            SpinRotation(npc, 0.36f);
            context.SpinVortex = 1f;
            context.EyeFlame = 1.4f;

            //侧向抛撒骨片（骨风暴主体）；周期断档留幕墙豁口
            int shedInterval = p2 ? 3 : 4;
            bool inGap = phaseTimer % CurtainGapPeriod < CurtainGapFrames;
            if (!VaultUtils.isClient && !inGap && phaseTimer % shedInterval == 0) {
                int damage = SkullDamage(context);
                Vector2 dir = npc.velocity.SafeNormalize(Vector2.UnitX);
                for (int side = -1; side <= 1; side += 2) {
                    Vector2 vel = dir.RotatedBy(side * MathHelper.PiOver2) * Main.rand.NextFloat(2.6f, 3.8f)
                        + dir * Main.rand.NextFloat(0.5f, 1.4f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + vel * 4f, vel,
                        ModContent.ProjectileType<SkeletronBoneShard>(), damage, 0f, Main.myPlayer,
                        side * 0.011f, 0f);
                }
            }

            //幽火尾迹
            if (!VaultUtils.isServer && phaseTimer % 3 == 0) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(npc.Center + Main.rand.NextVector2Circular(30f, 30f),
                    -npc.velocity * 0.12f, SkeletronRenderHelper.GhostCyan,
                    Main.rand.NextFloat(1.4f, 2.2f))?.Configure(Main.rand.Next(18, 30));
            }

            //已越过目标且拉开距离，或超时
            Vector2 toTarget = context.Target.Center - npc.Center;
            bool passed = Vector2.Dot(toTarget, dashDir) < 0f && toTarget.Length() > 420f;
            if (passed || phaseTimer > 46) {
                Advance(2);
            }
        }

        /// <summary>刹车：硬减速定格</summary>
        private void UpdateBrake(SkeletronStateContext context, NPC npc, int maxPasses) {
            npc.damage = npc.defDamage;
            npc.velocity *= 0.68f;
            context.SpinVortex *= 0.86f;
            SpinRotation(npc, 0.2f * MathHelper.Clamp(1f - phaseTimer / 12f, 0f, 1f));

            if (phaseTimer >= 12) {
                Counter++;
                if (Counter >= maxPasses) {
                    Advance(3);
                }
                else {
                    Advance(0);
                }
            }
        }

        /// <summary>收势回正</summary>
        private void UpdateSettle(SkeletronStateContext context, NPC npc) {
            npc.damage = npc.defDamage;
            npc.velocity *= 0.9f;
            context.SpinVortex *= 0.85f;
            context.EyeFlame = MathHelper.Lerp(context.EyeFlame, 1f, 0.1f);
            SettleRotation(npc, 0.14f);
        }

        private static bool IsP2(NPC npc) {
            return (int)npc.ai[SkeletronAiSlots.HeadPhase] >= SkeletronPhase.Unbound;
        }
    }
}
