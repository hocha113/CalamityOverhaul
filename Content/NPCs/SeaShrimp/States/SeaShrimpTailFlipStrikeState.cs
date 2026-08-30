using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 尾弹突袭：34f 迟滞后卷蓄力（预告线 f28 锁向承诺，双钳收肩上膛张开）→
    /// 一帧点火弹射穿场（虾式后向弹射：尾先行掠过目标——生物学发力保留，实机裁决 2026-08
    /// 补发力演出：点火帧双钳向头前猛推水，钳前冲击环+水锥即"反作用力把虾射出去"的
    /// 可视化声明，飞行段双钳前伸滑行）→ 硬刹归位。
    /// 接触伤害窗与速度门绑定（&gt;20px/f 才有伤），刹车段无害
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.TailFlipStrike, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpTailFlipStrikeState : SeaShrimpStateBase
    {
        public override string StateName => "TailFlipStrike";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.TailFlipStrike;

        private const int LockFrame = 28;
        private const int HardTimeout = 150;
        /// <summary>接触伤速度门 px/f</summary>
        private const float ContactSpeedGate = 20f;

        private Vector2 lockDir = Vector2.UnitX;
        private bool launched;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            ShrimpLocomotion loco = ctx.Owner.Locomotion;
            int t = (int)Timer;
            Timer++;

            if (!launched) {
                int windup = SeaShrimpDirector.TailFlipWindup;

                if (t < LockFrame) {
                    lockDir = (PredictTarget(ctx, 14f) - npc.Center).SafeNormalize(Vector2.UnitX);
                }

                float w = MathHelper.Clamp(t / (float)windup, 0f, 1f);
                //转身对线：尾扇对准冲刺线（身轴=冲刺线反向，虾式后向弹射的可读上膛），
                //转率随蓄力衰减——越接近锁定越慢，锁线本身就是预告的一部分
                float turnRate = MathHelper.Lerp(0.09f, 0.02f, w);
                HoldFacing(ctx, lockDir.ToRotation() + MathHelper.Pi, turnRate);

                float snap = MathF.Pow(w, 8f);
                ctx.SpineCurl = -(0.3f + 0.7f * snap);
                ctx.TailFlare = MathHelper.Clamp(0.35f - w * 0.35f, 0f, 1f);
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, w * 0.8f);
                ctx.WaveGain = 0.3f;

                //双钳收肩上膛：钳口随蓄力渐张——要推水先张钳，发力方式提前可读
                for (int a = 0; a < 2; a++) {
                    ctx.Claws[a] = new ClawDirective {
                        Mode = ClawMode.Hold,
                        Target = ctx.Owner.Skeleton.ShoulderWorld(a)
                            + ctx.Owner.Locomotion.Heading.ToRotationVector2() * 26f,
                        Spring = 0.3f,
                        Damping = 0.72f,
                        ClawOpen = w * 0.9f,
                    };
                }

                float aimAlpha = MathHelper.Clamp((t - 10) / 18f, 0f, 1f) * (t >= LockFrame ? 0.6f : 0.3f);
                ctx.AddTelegraph(npc.Center + lockDir * 60f, lockDir, 620f, aimAlpha,
                    t >= LockFrame ? 0.85f : 0.5f);

                if (t == 2 && !Main.dedServ) {
                    //蜷尾蓄势：低调水涌（原 Item32 是吹叶机气流，水下违和）
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = -0.5f, MaxInstances = 2 }, npc.Center);
                }

                if (t >= windup) {
                    launched = true;
                    //尾先行：飞行期身轴持续贴合速度轴反向，侧身滑行由此根除
                    loco.LaunchBallistic(lockDir * SeaShrimpDirector.TailFlipSpeed,
                        SeaShrimpDirector.TailFlipFrames, SeaShrimpDirector.TailFlipBrake,
                        BallisticHeading.TailFirst);
                    ctx.TailFlare = 1f;

                    //发力演出：双钳向头前猛推水——反作用力把虾往后射向玩家的可视化声明
                    Vector2 headFwd = loco.Heading.ToRotationVector2();
                    for (int a = 0; a < 2; a++) {
                        ctx.Owner.Skeleton.Arms[a].Impulse(headFwd * 46f);
                    }
                    if (!Main.dedServ) {
                        //甩尾出手：重挥低啸（原 Item86 在原版全源码零调用，听感无人能作保）
                        SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.85f, Pitch = -0.5f, MaxInstances = 2 }, npc.Center);
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.8f, Pitch = -0.3f, MaxInstances = 2 }, npc.Center);
                        ShakeNearby(npc.Center, 5.5f);
                        //钳前推水冲击环 + 推水锥：发力点在钳口，不在身后
                        Vector2 push = npc.Center + headFwd * 130f;
                        ctx.AddRing(push, 190f, 20, 1f);
                        EverdeepVFX.SplashBurst(push, headFwd * 13f, 1.2f);
                    }
                }
                return null;
            }

            //弹道段：伤害窗=速度门（刹车段自动无害），身体随速度渐展
            float speed = npc.velocity.Length();
            float unroll = MathHelper.Clamp(speed / SeaShrimpDirector.TailFlipSpeed, 0f, 1f);
            ctx.SpineCurl = -0.55f * unroll;
            ctx.TailFlare = 1f;
            ctx.WaveGain = 0.2f;
            ctx.AfterimageStrength = unroll;
            if (speed > ContactSpeedGate) {
                npc.damage = npc.defDamage;
            }

            //双钳前伸滑行：推完水的随动姿态，推水余流从钳侧洒出
            Vector2 glideFwd = loco.Heading.ToRotationVector2();
            for (int a = 0; a < 2; a++) {
                ctx.Claws[a] = new ClawDirective {
                    Mode = ClawMode.Strike,
                    Target = npc.Center + glideFwd * 185f + ctx.Owner.Skeleton.Lateral(a) * 42f,
                    Spring = 0.45f,
                    Damping = 0.8f,
                    ClawOpen = 0.12f,
                };
            }
            if (!Main.dedServ && speed > ContactSpeedGate && Main.GameUpdateCount % 4 == 0) {
                Vector2 clawTip = ctx.Owner.Skeleton.ClawTip((int)(Main.GameUpdateCount / 4 % 2));
                EverdeepVFX.ShedDroplet(clawTip, glideFwd * 2.2f
                    + Main.rand.NextVector2Circular(0.8f, 0.8f), 0.8f);
            }

            if (loco.BallisticDone || t >= HardTimeout) {
                //连击链：P2+ 穿场刹停后顺势卷尾接水弹齐射（确定性条件，各端一致）
                if (ctx.Phase >= 2 && ctx.AttackIndex % 3 == 1) {
                    ctx.QueuedChainState = (int)SeaShrimpStateIndex.WaterVolley;
                }
                return EndAttack(ctx, 68);
            }
            return null;
        }
    }
}
