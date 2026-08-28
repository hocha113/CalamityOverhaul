using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>
    /// 低头冲撞：刨地蓄势(反向预备)→贴地猛冲，尾迹冰刺封路，硬刹踉跄后喘息破绽。
    /// 二阶段回身再冲一趟。伤害窗严格贴合冲刺速度
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.AvalancheCharge, typeof(DeerclopsStateContext))]
    internal class DeerclopsAvalancheChargeState : DeerclopsStateBase
    {
        public override string StateName => "AvalancheCharge";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.AvalancheCharge;

        private const int WindupTime = 46;
        private const int TurnTelegraph = 26;
        private const int PantTime = 26;
        private const float DashDistance = 1150f;

        //子阶段：0蓄势 1冲刺 2刹车 3喘息 4回身蓄势(二阶段) 5回刺 6回刹 7终喘
        private int phase;
        private int phaseTimer;
        private float traveled;

        private int Dir(DeerclopsStateContext ctx) => (int)ctx.Npc.ai[1] >= 0 ? 1 : -1;
        private float DashSpeed(DeerclopsStateContext ctx) => ctx.IsPhase2 ? 26f : 23f;

        public override void OnEnter(DeerclopsStateContext context) {
            base.OnEnter(context);
            phase = 0;
            phaseTimer = 0;
            traveled = 0f;
            if (!VaultUtils.isClient) {
                context.Npc.ai[1] = DirToTarget(context);
                context.Npc.netUpdate = true;
            }
        }

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Timer++;
            phaseTimer++;

            context.SkipDefaultMovement = true;
            int dir = Dir(context);
            npc.direction = npc.spriteDirection = dir;

            switch (phase) {
                case 0:
                case 4:
                    UpdateWindup(context, dir, phase == 4 ? TurnTelegraph : WindupTime);
                    break;
                case 1:
                case 5:
                    UpdateDash(context, dir);
                    break;
                case 2:
                case 6:
                    UpdateBrake(context);
                    break;
                case 3:
                    UpdatePant(context, thenTurn: context.IsPhase2);
                    break;
                default:
                    UpdatePant(context, thenTurn: false);
                    break;
            }

            //安全阀：任何原因卡住都收尾
            if (Timer > 430) {
                return new DeerclopsStalkState();
            }
            if (phase >= 8) {
                return new DeerclopsStalkState();
            }
            return null;
        }

        /// <summary>刨地蓄势：后撤呼吸(晚爆反向预备)，头压低</summary>
        private void UpdateWindup(DeerclopsStateContext context, int dir, int windupLength) {
            NPC npc = context.Npc;
            npc.damage = 0;

            float t = MathHelper.Clamp(phaseTimer / (float)windupLength, 0f, 1f);
            //pow(t,8)：几乎不动→最后几帧猛然后吸
            npc.velocity.X = -dir * (float)Math.Pow(t, 8) * 8f;
            DeerclopsMotion.ApplyVertical(npc, context, allowJump: false);

            context.BodyLean = -0.15f * t;
            context.EyeGlow = Math.Max(context.EyeGlow, 0.4f + t * 0.5f);
            //刨地帧：12/13交替
            context.AnimMode = DeerAnimMode.Stomp;
            context.AnimTimer = phaseTimer % 16;

            if (phaseTimer == 6 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 0.6f, Pitch = -0.55f }, npc.Center);
            }
            //刨地雪屑向后踢(本端)
            if (!Main.dedServ && phaseTimer % 8 == 0) {
                SoundEngine.PlaySound(SoundID.DeerclopsStep with { Volume = 0.6f, MaxInstances = 3 }, npc.Bottom);
                for (int i = 0; i < 6; i++) {
                    Dust dust = Dust.NewDustPerfect(npc.Bottom + new Vector2(dir * Main.rand.NextFloat(0f, 40f), 0f),
                        DustID.Snow, new Vector2(-dir * Main.rand.NextFloat(2f, 6f), -Main.rand.NextFloat(1f, 3.5f)),
                        70, default, Main.rand.NextFloat(1.1f, 1.9f));
                    dust.noGravity = Main.rand.NextBool(3);
                }
            }

            if (phaseTimer >= windupLength) {
                //起跑一帧点火
                npc.velocity.X = dir * DashSpeed(context);
                DeerclopsMotion.CameraPunch(npc.Center, 6.5f, 16, "DeerChargeLaunch", Vector2.UnitX * dir);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.ForceRoar with { Volume = 0.95f, Pitch = -0.35f }, npc.Center);
                }
                traveled = 0f;
                AdvancePhase();
            }
        }

        /// <summary>冲刺：贴地全速，尾迹冰刺封路，伤害窗=速度窗</summary>
        private void UpdateDash(DeerclopsStateContext context, int dir) {
            NPC npc = context.Npc;
            float speed = DashSpeed(context);

            npc.velocity.X = dir * speed;
            DeerclopsMotion.ApplyVertical(npc, context, allowJump: true);
            traveled += speed;

            //前倾扑进(与蓄势后仰形成对拍)
            context.BodyLean = 0.14f;
            context.AnimMode = DeerAnimMode.Locomotion;
            //伤害窗严格贴合可见冲刺
            npc.damage = Math.Abs(npc.velocity.X) > 12f ? (int)(npc.defDamage * 1.35f) : 0;

            //尾迹冰刺(服务端)：迟到20帧破土，惩罚贴身跟跑
            if (!VaultUtils.isClient && phaseTimer % 9 == 0) {
                Point feet = npc.Bottom.ToTileCoordinates();
                int damage = context.IsAsuraMode ? 19 : 15;
                DeerIceSpikeProj.TrySpawn(npc, feet.X - dir * 3, feet.Y, -dir * 0.14f, 0.85f,
                    TelegraphTime(context, 20, 14), damage);
            }

            //雪浪(本端)
            if (!Main.dedServ) {
                for (int i = 0; i < 3; i++) {
                    Dust dust = Dust.NewDustPerfect(npc.Bottom + new Vector2(Main.rand.NextFloat(-30f, 30f), 0f),
                        DustID.Snow, new Vector2(-dir * Main.rand.NextFloat(1f, 4f), -Main.rand.NextFloat(1f, 5f)),
                        60, default, Main.rand.NextFloat(1.2f, 2.1f));
                    dust.noGravity = Main.rand.NextBool(3);
                }
            }

            if (traveled >= DashDistance) {
                AdvancePhase();
            }
        }

        /// <summary>硬刹：阶梯减速+前扑点头踉跄</summary>
        private void UpdateBrake(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            npc.velocity.X *= 0.78f;
            DeerclopsMotion.ApplyVertical(npc, context, allowJump: false);

            //急刹惯性：先猛点头(前倾加深)再回正
            float nodT = MathHelper.Clamp(phaseTimer / 5f, 0f, 1f);
            float settleT = MathHelper.Clamp((phaseTimer - 5) / 12f, 0f, 1f);
            context.BodyLean = MathHelper.Lerp(0.14f, 0.3f, nodT) * (1f - settleT);
            context.AnimMode = DeerAnimMode.Locomotion;
            npc.damage = Math.Abs(npc.velocity.X) > 12f ? (int)(npc.defDamage * 1.35f) : 0;

            if (phaseTimer == 4) {
                DeerclopsMotion.CameraPunch(npc.Bottom, 4f, 14, "DeerChargeBrake", Vector2.UnitY);
                if (!Main.dedServ) {
                    for (int i = 0; i < 10; i++) {
                        Dust dust = Dust.NewDustPerfect(npc.Bottom, DustID.Snow,
                            new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1f, 4f)), 70, default, Main.rand.NextFloat(1f, 1.8f));
                        dust.noGravity = Main.rand.NextBool(3);
                    }
                }
            }

            if (Math.Abs(npc.velocity.X) < 1f && phaseTimer > 8) {
                npc.velocity.X = 0f;
                AdvancePhase();
            }
        }

        /// <summary>喘息破绽：站桩26帧；二阶段第一趟喘完回身再冲</summary>
        private void UpdatePant(DeerclopsStateContext context, bool thenTurn) {
            NPC npc = context.Npc;
            npc.damage = 0;
            npc.velocity.X *= 0.8f;
            DeerclopsMotion.ApplyVertical(npc, context, allowJump: false);
            context.AnimMode = DeerAnimMode.Crouch;
            context.BodyLean = MathHelper.Lerp(context.BodyLean, 0f, 0.15f);

            if (phaseTimer >= PantTime) {
                if (thenTurn && phase == 3) {
                    //回身第二趟
                    if (!VaultUtils.isClient) {
                        context.Npc.ai[1] = DirToTarget(context);
                        context.Npc.netUpdate = true;
                    }
                    phase = 4;
                    phaseTimer = 0;
                }
                else {
                    phase = 8;
                }
            }
        }

        private void AdvancePhase() {
            phase++;
            phaseTimer = 0;
        }

        public override void OnExit(DeerclopsStateContext context) {
            base.OnExit(context);
            context.BodyLean = 0f;
        }
    }
}
