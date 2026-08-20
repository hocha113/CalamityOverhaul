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
    /// <summary>瞬猎颅雨：幽火消散→侧翼重凝→佯扑+反抛物线颅火扇，三轮</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.SkullRainTeleport, typeof(SkeletronStateContext))]
    internal class SkeletronSkullRainTeleportState : SkeletronStateBase
    {
        public override string StateName => "SkullRainTeleport";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.SkullRainTeleport;

        private const int RoundFrames = 72;
        private const int DissolveEnd = 16;     //旧位散形
        private const int CondenseEnd = 28;     //新位聚形
        private const int PounceFrame = 32;     //佯扑

        /// <summary>缺口（契约3）：颅火扇中央 FanGapHalfWidth 个槽永空——迎着佯扑轴线冲脸是安全走廊
        /// （奖励贴头输出），发射循环按槽距直接跳过</summary>
        private const int FanGapHalfWidth = 1;

        private int round;
        private int roundTimer;

        public override void OnEnter(SkeletronStateContext context) {
            base.OnEnter(context);
            round = 0;
            roundTimer = 0;
        }

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            int maxRounds = context.DeathMode ? 4 : 3;

            UpdateRound(context, npc);

            roundTimer++;
            Timer++;
            if (roundTimer >= RoundFrames) {
                roundTimer = 0;
                round++;
                if (round >= maxRounds && !VaultUtils.isClient) {
                    npc.alpha = 0;
                    return new SkeletronHubState();
                }
            }

            //超时兜底
            if (Timer > 420 && !VaultUtils.isClient) {
                npc.alpha = 0;
                return new SkeletronHubState();
            }
            return null;
        }

        private void UpdateRound(SkeletronStateContext context, NPC npc) {
            Player target = context.Target;

            if (roundTimer < DissolveEnd) {
                //散形：幽火外涌，形体消解
                npc.damage = 0;
                npc.velocity *= 0.85f;
                npc.alpha = (int)MathHelper.Lerp(0f, 255f, roundTimer / (float)DissolveEnd);
                context.EyeFlame = 1f - roundTimer / (float)DissolveEnd;
                if (!VaultUtils.isServer && roundTimer % 2 == 0) {
                    PRTLoader.NewParticle<PRT_SkeleGhostFlame>(npc.Center + Main.rand.NextVector2Circular(40f, 40f),
                        Main.rand.NextVector2CircularEdge(3.4f, 3.4f),
                        SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(1.4f, 2.2f))?.Configure(Main.rand.Next(20, 32));
                }
                if (roundTimer == 2 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.7f }, npc.Center);
                }

                //服务端瞬移到侧翼
                if (roundTimer == DissolveEnd - 1 && !VaultUtils.isClient) {
                    int side = round % 2 == 0 ? 1 : -1;
                    Vector2 newPos = target.Center + new Vector2(side * 430f, -230f + Main.rand.NextFloat(-60f, 60f));
                    npc.Center = newPos;
                    npc.velocity = Vector2.Zero;
                    npc.ai[SkeletronAiSlots.HeadParamB] = (target.Center - newPos).ToRotation();
                    npc.netUpdate = true;
                }
            }
            else if (roundTimer < CondenseEnd) {
                //聚形：吸入幽火，眼火复燃
                npc.damage = 0;
                npc.velocity = Vector2.Zero;
                float t = (roundTimer - DissolveEnd) / (float)(CondenseEnd - DissolveEnd);
                npc.alpha = (int)MathHelper.Lerp(255f, 0f, t);
                context.EyeFlame = t * 1.5f;
                context.DashTelegraph = t;
                if (!VaultUtils.isServer && roundTimer % 2 == 0) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2CircularEdge(90f, 90f);
                    PRTLoader.NewParticle<PRT_SkeleGhostFlame>(pos, (npc.Center - pos) * 0.14f,
                        SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(1.2f, 1.8f))?.Configure(16, 0f);
                }
            }
            else if (roundTimer < PounceFrame) {
                //佯扑前的定身半拍
                npc.alpha = 0;
                npc.damage = npc.defDamage;
                npc.velocity = Vector2.Zero;
                SettleRotation(npc, 0.3f);
            }
            else if (roundTimer == PounceFrame) {
                //短促佯扑 + 反抛物线颅火扇
                Vector2 dir = npc.ai[SkeletronAiSlots.HeadParamB].ToRotationVector2();
                npc.velocity = dir * 15f;
                npc.damage = npc.defDamage;
                context.EyeFlame = 1.5f;

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.8f, Pitch = 0.1f }, npc.Center);
                    SkeletronScreenEffects.PushShake(npc.Center, 4f);
                }

                if (!VaultUtils.isClient) {
                    int damage = SkullDamage(context);
                    int numProj = context.DeathMode ? 7 : 5;
                    float spread = MathHelper.ToRadians(52f);
                    Vector2 baseVel = dir * 6.8f;
                    float centralCount = 0.5f * (numProj - 1f);
                    for (int i = 0; i < numProj; i++) {
                        //中央槽留缺口走廊
                        if (MathF.Abs(centralCount - i) < FanGapHalfWidth) {
                            continue;
                        }
                        //反抛物线扇：近央慢边缘快
                        float offset = MathHelper.Lerp(-spread * 0.5f, spread * 0.5f, i / (numProj - 1f));
                        float velMult = MathHelper.Lerp(0.55f, 1.45f, MathF.Abs(centralCount - i) / centralCount);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + baseVel * 4f,
                            baseVel.RotatedBy(offset) * velMult,
                            ModContent.ProjectileType<SkeletronCursedSkull>(), damage, 0f, Main.myPlayer, 0f, 0f);
                    }

                    //隔轮追加一只背袭幽灵臂
                    if (round % 2 == 1) {
                        float backAngle = (npc.Center - target.Center).SafeNormalize(Vector2.UnitX).ToRotation() + MathHelper.Pi;
                        Vector2 armPos = target.Center + backAngle.ToRotationVector2() * SkeletronGhostArmProj.LungeRingRadius;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), armPos, Vector2.Zero,
                            ModContent.ProjectileType<SkeletronGhostArmProj>(), damage, 0f, Main.myPlayer,
                            (float)SkeletronGhostArmProj.ArmMode.CircleLunge, backAngle, 20f);
                    }
                    npc.netUpdate = true;
                }
            }
            else {
                //佯扑急停回稳
                npc.damage = npc.defDamage;
                npc.velocity *= 0.86f;
                LeanByVelocity(npc);
                context.EyeFlame = MathHelper.Lerp(context.EyeFlame, 1f, 0.08f);
            }
        }

        public override void OnExit(SkeletronStateContext context) {
            base.OnExit(context);
            context.Npc.alpha = 0;
        }
    }
}
