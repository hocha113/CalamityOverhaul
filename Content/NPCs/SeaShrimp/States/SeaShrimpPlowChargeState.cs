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
    /// 犁浪冲锋（P1+，头先行贴地冲刺）：30f 转身对线蓄力（车道虚线 f24 锁定即承诺）
    /// → 头先行 30px/f 冲锋 26f，犁开船首浪（头前水花锥+残影满开），
    /// 弹道期身轴贴合速度轴（朝向修复的展示位）→ 硬刹收尾。
    /// 接触伤速度门 >18（刹车段自动无害）；俯仰钳制 ≤0.30rad 不追高
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.PlowCharge, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpPlowChargeState : SeaShrimpStateBase
    {
        public override string StateName => "PlowCharge";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.PlowCharge;

        private const int WindupEnd = 30;
        private const int LockFrame = 24;
        private const int HardTimeout = 150;
        private const float ContactSpeedGate = 18f;

        private Vector2 lockDir = Vector2.UnitX;
        private bool launched;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            ShrimpLocomotion loco = ctx.Owner.Locomotion;
            int t = (int)Timer;
            Timer++;

            if (!launched) {
                if (t < LockFrame) {
                    //贴地冲锋不追高：以左右水平向为基角，俯仰相对角钳在 ±PlowMaxPitch 内
                    Vector2 to = PredictTarget(ctx, 12f) - npc.Center;
                    float baseAng = to.X >= 0f ? 0f : MathHelper.Pi;
                    float rel = MathHelper.WrapAngle(to.ToRotation() - baseAng);
                    rel = MathHelper.Clamp(rel, -SeaShrimpDirector.PlowMaxPitch, SeaShrimpDirector.PlowMaxPitch);
                    lockDir = (baseAng + rel).ToRotationVector2();
                }

                float w = MathHelper.Clamp(t / (float)WindupEnd, 0f, 1f);
                //转身对线：头对准冲刺线，转率随蓄力衰减
                HoldFacing(ctx, lockDir.ToRotation(), MathHelper.Lerp(0.09f, 0.02f, w));

                //蓄势：身体低伏微卷，尾扇收拢，晶光渐起
                float snap = MathF.Pow(w, 6f);
                ctx.SpineCurl = -0.22f * snap;
                ctx.TailFlare = 0.3f;
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, w * 0.7f);
                ctx.WaveGain = 0.3f;

                float aimAlpha = MathHelper.Clamp((t - 8) / 16f, 0f, 1f) * (t >= LockFrame ? 0.6f : 0.3f);
                ctx.AddTelegraph(npc.Center + lockDir * 70f, lockDir, 700f, aimAlpha,
                    t >= LockFrame ? 0.85f : 0.5f);

                if (t == 2 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.55f, Pitch = -0.4f, MaxInstances = 2 }, npc.Center);
                }

                if (t >= WindupEnd) {
                    launched = true;
                    //头先行：飞行期身轴贴合速度轴
                    loco.LaunchBallistic(lockDir * SeaShrimpDirector.PlowSpeed,
                        SeaShrimpDirector.PlowFrames, 0.82f, BallisticHeading.HeadFirst);
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item86 with { Volume = 0.85f, Pitch = -0.25f, MaxInstances = 2 }, npc.Center);
                        SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.8f, Pitch = -0.1f, MaxInstances = 2 }, npc.Center);
                        ShakeNearby(npc.Center, 5f);
                        ctx.AddRing(npc.Center + lockDir * 60f, 170f, 20, 1f);
                    }
                }
                return null;
            }

            //冲锋段：船首浪——头前水花锥逐帧犁开，残影满开
            float speed = npc.velocity.Length();
            float thrust = MathHelper.Clamp(speed / SeaShrimpDirector.PlowSpeed, 0f, 1f);
            ctx.SpineCurl = 0.08f * thrust;
            ctx.TailFlare = 0.9f;
            ctx.WaveGain = 0.2f;
            ctx.AfterimageStrength = thrust;
            if (speed > ContactSpeedGate) {
                npc.damage = npc.defDamage;
            }
            if (!Main.dedServ && speed > ContactSpeedGate && Main.GameUpdateCount % 2 == 0) {
                Vector2 prow = npc.Center + ctx.Owner.Locomotion.Heading.ToRotationVector2() * 92f;
                EverdeepVFX.SplashBurst(prow, npc.velocity * 0.5f, 0.65f);
            }

            if (loco.BallisticDone || t >= HardTimeout) {
                return EndAttack(ctx, 52);
            }
            return null;
        }
    }
}
