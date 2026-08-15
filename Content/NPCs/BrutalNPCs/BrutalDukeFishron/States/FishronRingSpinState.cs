using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>
    /// 环舞爆发：绕圈布下气泡环→一拍死寂→全环径向齐射。
    /// 环内圆心是安全眼——敢贴近的人得到奖赏；提前打掉气泡能射穿缺口
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.RingSpin, typeof(FishronStateContext))]
    internal class FishronRingSpinState : FishronStateBase
    {
        public override string StateName => "RingSpin";
        public override FishronStateIndex StateIndex => FishronStateIndex.RingSpin;

        private const int SpinStart = 18;
        private const int SpinTime = 84;
        private const int FireFrame = SpinStart + SpinTime + 14;
        private const int TotalTime = FireFrame + 34;
        private const float SpinSpeed = 20f;
        private const float BurstSpeed = 9.5f;
        private static float AngularStep => MathHelper.TwoPi / SpinTime;

        private int spinSign;
        private bool fired;

        public FishronRingSpinState() {
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            spinSign = 0;
            fired = false;
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //旋向由相对位确定（各端一致）
            if (spinSign == 0) {
                spinSign = npc.Center.X < player.Center.X ? 1 : -1;
            }

            Timer++;

            //幕一：切向起手
            if (Timer <= SpinStart) {
                Vector2 toPlayer = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                Vector2 tangent = toPlayer.RotatedBy(spinSign * MathHelper.PiOver2);
                npc.velocity = Vector2.Lerp(npc.velocity, tangent * SpinSpeed, 0.2f);
                AimBodyAlongVelocity(npc);
                context.FrameCommand = 2;
                if (Timer == SpinStart) {
                    npc.velocity = npc.velocity.SafeNormalize(Vector2.UnitX) * SpinSpeed;
                    npc.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 0.9f, Pitch = -0.05f, MaxInstances = 3 }, npc.Center);
                }
                return null;
            }

            //幕二：绕环布泡（速度向量匀角旋转，确定性，无需圆心同步）
            if (Timer <= SpinStart + SpinTime) {
                npc.velocity = npc.velocity.RotatedBy(-AngularStep * spinSign);
                AimBodyAlongVelocity(npc);
                context.FrameCommand = 2;

                int t = (int)Timer - SpinStart;
                //每 3 帧从嘴位放一枚待发气泡（受全场容量上限约束）
                if (!VaultUtils.isClient && t % 3 == 0 && FishronBubbleMazeState.CountBubbles() < 70) {
                    Vector2 mouth = npc.Center + npc.velocity.SafeNormalize(Vector2.UnitX) * (npc.width * 0.42f);
                    int idx = NPC.NewNPC(npc.GetSource_FromAI(), (int)mouth.X, (int)mouth.Y, NPCID.DetonatingBubble);
                    if (idx >= 0 && idx < Main.maxNPCs) {
                        NPC bubble = Main.npc[idx];
                        bubble.ai[0] = 2f;
                        //待发计时对齐齐射帧
                        bubble.ai[1] = FireFrame - (int)Timer + 6;
                        bubble.velocity = Vector2.Zero;
                        bubble.netUpdate = true;
                    }
                }
                if (!VaultUtils.isServer && t % 3 == 0) {
                    SoundEngine.PlaySound(SoundID.NPCDeath19 with { Volume = 0.35f, Pitch = 0.3f, MaxInstances = 4 }, npc.Center);
                }
                return null;
            }

            //幕三：环成，死寂一拍——泡光收敛，只剩雨声
            if (Timer < FireFrame) {
                npc.velocity *= 0.82f;
                FaceBody(npc, player.Center, 0.12f);
                context.SetChargeState(3, (Timer - SpinStart - SpinTime) / 14f);
                return null;
            }

            //齐射帧：全环向外radial 放射（服务端统一点火）
            if (!fired) {
                fired = true;
                if (!VaultUtils.isClient) {
                    FireRing(npc);
                }
                FishronMotionFX.CameraPunch(npc.Center, 6f, 12, "FishronRingFire");
                SoundEngine.PlaySound(SoundID.Item96 with { Volume = 1f, Pitch = -0.3f, MaxInstances = 3 }, npc.Center);
                if (!VaultUtils.isServer) {
                    InnoVault.PRT.PRTLoader.NewParticle<CalamityOverhaul.Content.PRTTypes.PRT_DWave>(
                        npc.Center, Vector2.Zero, FishronMotionFX.SeaGreen, 0.3f)?
                        .Configure(new Vector2(1f, 1f), 0f, 2f, 20);
                }
            }

            //收势
            npc.velocity *= 0.92f;
            FaceBody(npc, player.Center, 0.1f);

            if (Timer >= TotalTime) {
                return new FishronHoverState();
            }
            return null;
        }

        /// <summary>点火：以当前绕环圆心为极心，所有待发气泡向外直射</summary>
        private void FireRing(NPC npc) {
            //圆心 = 本体位置 + 指向环心的半径向量（速度的旋向法线）
            float radius = SpinSpeed / AngularStep;
            Vector2 toCenter = npc.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(-spinSign * MathHelper.PiOver2);
            Vector2 ringCenter = npc.Center + toCenter * radius;

            foreach (var n in Main.ActiveNPCs) {
                if (n.type != NPCID.DetonatingBubble || (int)n.ai[0] != 2) {
                    continue;
                }
                if (Vector2.DistanceSquared(n.Center, ringCenter) > 900f * 900f) {
                    continue;
                }
                Vector2 dir = (n.Center - ringCenter).SafeNormalize(Vector2.UnitY);
                n.ai[1] = 0f;
                n.velocity = dir * BurstSpeed;
                n.netUpdate = true;
            }
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }
}
