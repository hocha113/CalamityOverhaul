using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>
    /// 口器狂化锯齿撕咬（二阶段核心）：绕直线交替左右偏角的短促连冲，缝合线式逼近，<br/>
    /// 每跳收口一记撕咬；跳间微歇与收尾长喘是公平阀
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EocStateIndex.MawFrenzy, typeof(EocStateContext))]
    internal class EocMawFrenzyState : EocStateBase
    {
        public override string StateName => "EocMawFrenzy";
        public override EocStateIndex StateIndex => EocStateIndex.MawFrenzy;

        private enum FrenzyPhase
        {
            Approach,   //入位
            PreSnap,    //张口预备
            Hop,        //锯齿冲刺
            Gap,        //跳间微歇
            Recover,    //收尾长喘
        }

        private const int ApproachTime = 30;
        private const int PreSnapTime = 8;
        private const int HopFlight = 10;
        private const int GapTime = 6;
        private const int RecoverTime = 44;

        private int MaxHops => Context.IsDeathMode ? 7 : 6;
        private float HopSpeed => Context.IsDeathMode ? 56f : 52f;

        private EocStateContext Context;
        private FrenzyPhase phase;
        private int hopIndex;

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            Context = context;
            phase = FrenzyPhase.Approach;
            hopIndex = 0;
            context.FrameRate = 3;
        }

        public override IEocState OnUpdate(EocStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            DisableContactDamage(npc);

            switch (phase) {
                case FrenzyPhase.Approach:
                    UpdateApproach(npc, player, context);
                    break;
                case FrenzyPhase.PreSnap:
                    UpdatePreSnap(npc, player, context);
                    break;
                case FrenzyPhase.Hop:
                    UpdateHop(npc, player, context);
                    break;
                case FrenzyPhase.Gap:
                    UpdateGap(npc, context);
                    break;
                case FrenzyPhase.Recover:
                    return UpdateRecover(npc, player, context);
            }

            return null;
        }

        private void SwitchPhase(FrenzyPhase next) {
            phase = next;
            Timer = 0;
        }

        private void UpdateApproach(NPC npc, Player player, EocStateContext context) {
            //入位到侧翼中距
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 point = player.Center + new Vector2(side * 480f, -40f);
            EocMotion.CurveChase(npc, point, 24f, 0.13f);
            FaceTarget(npc, player.Center, 0.3f);

            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.85f, Pitch = 0.28f }, npc.Center);
            }

            Timer++;
            if (Timer >= ApproachTime || npc.Distance(point) < 70f) {
                SwitchPhase(FrenzyPhase.PreSnap);
            }
        }

        private void UpdatePreSnap(NPC npc, Player player, EocStateContext context) {
            float progress = Timer / (float)PreSnapTime;
            //口器弹性大张（帧动画提速+身体前倾微缩）
            context.FrameRate = 2;
            context.ScalePulse = 1f + 0.08f * progress;
            context.SetChargeState(3, progress);
            context.PushIris(progress, EocMotion.IrisRed);
            //微幅反向预备
            Vector2 awayDir = (npc.Center - player.Center).SafeNormalize(Vector2.UnitY);
            npc.velocity = npc.velocity * 0.7f + awayDir * progress * 3.4f;
            FaceTarget(npc, player.Center, 0.65f);

            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.65f, Pitch = 0.5f }, npc.Center);
            }

            Timer++;
            if (Timer >= PreSnapTime) {
                //起跳：锯齿偏角，左右交替
                if (!VaultUtils.isClient) {
                    float sawSign = hopIndex % 2 == 0 ? 1f : -1f;
                    float sawAngle = MathHelper.ToRadians(30f + Main.rand.NextFloat(0f, 9f)) * sawSign;
                    Vector2 predicted = EocMotion.PredictTarget(player, npc.Center, HopSpeed, 0.3f);
                    Vector2 dir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY).RotatedBy(sawAngle);
                    EocMotion.DashLaunch(npc, context, dir, HopSpeed, 0.9f);
                    npc.netUpdate = true;
                }
                else {
                    context.PushDashVisuals(1f, 1f);
                }
                SwitchPhase(FrenzyPhase.Hop);
            }
        }

        private void UpdateHop(NPC npc, Player player, EocStateContext context) {
            FaceVelocity(npc);
            EnableContactDamageIfFast(npc, 26f, 1.35f);
            context.PushDashVisuals(1f, 1f);
            context.FrameRate = 2;

            Timer++;
            if (Timer >= HopFlight) {
                //收口撕咬
                npc.velocity *= 0.5f;
                context.ScalePulse = 0.94f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 1f, Pitch = -0.1f }, npc.Center);
                    EocMotion.BloodSpray(npc.Center + (npc.rotation + MathHelper.PiOver2).ToRotationVector2() * 44f,
                        (npc.rotation + MathHelper.PiOver2).ToRotationVector2(), 4, 6f, 0.9f);
                }
                EocMotion.Shake(npc.Center, 3.2f, 7);

                //隔跳侧向啐血，交叉火力
                if (hopIndex % 2 == 1 && !VaultUtils.isClient) {
                    Vector2 perp = npc.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                    for (int i = -1; i <= 1; i += 2) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, perp * i * 9.5f,
                            ModContent.ProjectileType<EocBloodShot>(), 9, 0f, Main.myPlayer, 0f);
                    }
                }

                hopIndex++;
                SwitchPhase(hopIndex >= MaxHops ? FrenzyPhase.Recover : FrenzyPhase.Gap);
            }
        }

        private void UpdateGap(NPC npc, EocStateContext context) {
            npc.velocity *= 0.86f;
            EocMotion.BrakeDroplets(npc);
            FaceTarget(npc, context.Target.Center, 0.4f);

            Timer++;
            if (Timer >= GapTime) {
                SwitchPhase(FrenzyPhase.PreSnap);
            }
        }

        private IEocState UpdateRecover(NPC npc, Player player, EocStateContext context) {
            //长喘：明确的输出窗
            npc.velocity *= 0.92f;
            context.FrameRate = 5;
            FaceTarget(npc, player.Center, 0.15f);
            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.8f, Pitch = -0.55f }, npc.Center);
            }
            if (!VaultUtils.isServer && Timer % 7 == 0) {
                Vector2 mawDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
                EocMotion.BloodSpray(npc.Center + mawDir * 40f, mawDir, 1, 3f, 0.5f);
            }

            Timer++;
            if (Timer >= RecoverTime) {
                if (VaultUtils.isClient) {
                    return null;
                }
                return new EocVeilHoverState(context.IsDeathMode ? 42 : 56);
            }
            return null;
        }

        public override void OnExit(EocStateContext context) {
            base.OnExit(context);
            context.FrameRate = context.IsSecondPhase ? 4 : 6;
        }
    }
}
