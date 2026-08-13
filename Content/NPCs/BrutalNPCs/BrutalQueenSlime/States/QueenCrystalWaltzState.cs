using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>水晶圆舞：旋身消隐→绕位重现→珍珠环收放(外扩-凝滞-向心收拢)</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.CrystalWaltz, typeof(QueenSlimeStateContext))]
    internal class QueenCrystalWaltzState : QueenSlimeStateBase
    {
        public override string StateName => "CrystalWaltz";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.CrystalWaltz;

        private const int ShrinkTime = 22;
        private const int BlinkHold = 8;
        private const int GrowTime = 14;
        private const int RingHold = 44;
        private const int StepLength = ShrinkTime + BlinkHold + GrowTime + RingHold;//88

        private int StepCount(QueenSlimeStateContext ctx) => ctx.IsDeathMode ? 4 : 3;

        private int currentStep = -1;

        public QueenCrystalWaltzState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            DisableContactDamage(context.Npc);
            currentStep = -1;
            context.Npc.noGravity = false;
            context.Npc.noTileCollide = false;
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            DisableContactDamage(npc);

            int step = Timer / StepLength;
            int t = Timer % StepLength;

            if (step >= StepCount(context)) {
                //复原并退场
                QueenMotion.SetScaleAnchored(npc, 1f);
                npc.dontTakeDamage = false;
                if (!VaultUtils.isClient) {
                    return new QueenBallroomStepState(1);
                }
                return null;
            }

            if (step != currentStep) {
                currentStep = step;
                SoundEngine.PlaySound(SoundID.Item155 with { Volume = 0.5f, Pitch = 0.5f + step * 0.1f, MaxInstances = 3 }, npc.Center);
            }

            if (t < ShrinkTime) {
                //旋身消隐：缩小+凝胶尘
                float p = t / (float)ShrinkTime;
                QueenMotion.SetScaleAnchored(npc, MathHelper.Lerp(1f, 0.42f, QueenMotion.SnapOut(p, 4)));
                npc.velocity.X *= 0.8f;
                npc.dontTakeDamage = p > 0.4f;
                context.PushSquash(-0.2f * p);
                EmitTeleportDust(npc, 0.6f);

                //旋走时甩下王冠(纯演出)
                if (t == ShrinkTime - 2 && !Main.dedServ) {
                    Gore.NewGore(npc.GetSource_FromAI(), npc.Center + new Vector2(-40f, -npc.height / 2), npc.velocity, GoreID.QueenSlimeCrown);
                }
            }
            else if (t < ShrinkTime + BlinkHold) {
                //瞬位帧(服务端定点)
                npc.dontTakeDamage = true;
                if (t == ShrinkTime && !VaultUtils.isClient) {
                    Vector2 target = player.Bottom;
                    //绕位候选：本步基准角起最多试4个方位，跳过会把身体埋进实心块的落点
                    for (int attempt = 0; attempt < 4; attempt++) {
                        float angle = MathHelper.TwoPi * step / StepCount(context) + Main.rand.NextFloat(-0.3f, 0.3f)
                            - MathHelper.PiOver2 + attempt * MathHelper.TwoPi / 4f;
                        Vector2 candidate = player.Center + angle.ToRotationVector2() * 340f;
                        //贴地修正：往下探地，太深则悬空
                        Vector2 ground = QueenMotion.FindGroundBelow(candidate);
                        if (ground.Y - candidate.Y < 380f) {
                            candidate = ground;
                        }
                        //身体体积不埋进实心块才采纳
                        Vector2 bodyTopLeft = candidate - new Vector2(57f, 100f);
                        if (!Collision.SolidCollision(bodyTopLeft, 114, 96)) {
                            target = candidate;
                            break;
                        }
                    }
                    npc.Bottom = target;
                    npc.velocity = Vector2.Zero;
                    npc.netUpdate = true;
                }
            }
            else if (t < ShrinkTime + BlinkHold + GrowTime) {
                //重现：长回+虹彩
                float p = (t - ShrinkTime - BlinkHold) / (float)GrowTime;
                QueenMotion.SetScaleAnchored(npc, MathHelper.Lerp(0.42f, 1f, QueenMotion.SnapOut(p, 3)));
                context.PrismShimmer = 0.7f;
                EmitTeleportDust(npc, 1.1f);
                npc.dontTakeDamage = p < 0.6f;
                FaceTarget(npc, player.Center);

                //满形帧放珍珠环
                if (t == ShrinkTime + BlinkHold + GrowTime - 1) {
                    ReleaseConvergeRing(context, step);
                }
            }
            else {
                //凝滞欣赏拍：珍珠外扩悬停，皇后行屈膝礼
                npc.dontTakeDamage = false;
                if (npc.velocity.Y == 0f) {
                    npc.velocity.X *= 0.8f;
                }
                if (t < ShrinkTime + BlinkHold + GrowTime + 18) {
                    context.PoseCommand = 3;
                }
            }

            return null;
        }

        /// <summary>向心珍珠环(服务端)：外扩→凝滞→向心收拢</summary>
        private void ReleaseConvergeRing(QueenSlimeStateContext context, int step) {
            NPC npc = context.Npc;
            context.PushSquash(0.4f);
            QueenMotion.Shake(npc.Center, 2.5f, 8, "QueenWaltzRing");
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.85f, Pitch = 0.35f }, npc.Center);

            if (VaultUtils.isClient) {
                return;
            }
            int pearls = context.IsDeathMode ? 12 : 10;
            float baseAngle = step * 0.35f;
            for (int i = 0; i < pearls; i++) {
                float angle = MathHelper.TwoPi * i / pearls + baseAngle;
                Vector2 vel = angle.ToRotationVector2() * 7.6f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center - new Vector2(0f, npc.height * 0.35f), vel,
                    ModContent.ProjectileType<QueenShardProj>(), QueenShardProj.PearlDamage, 0f, Main.myPlayer,
                    (int)QueenShardProj.Mode.Converge, 0f, i / (float)pearls);
            }
        }

        private static void EmitTeleportDust(NPC npc, float strength) {
            if (VaultUtils.isServer) {
                return;
            }
            Color color = QueenMotion.GetQueenDustColor();
            color.A = 150;
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustDirect(npc.position + Vector2.UnitX * -20f, npc.width + 40, npc.height,
                    DustID.TintableDust, npc.velocity.X, npc.velocity.Y, 50, color, 1.4f);
                d.noGravity = true;
                d.velocity *= strength;
            }
        }

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            QueenMotion.SetScaleAnchored(npc, 1f);
            npc.dontTakeDamage = false;
        }
    }
}
