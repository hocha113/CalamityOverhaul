using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 尾弹突袭：34f 迟滞后卷蓄力（预告线 f28 锁向承诺）→ 一帧点火弹射穿场
    /// （虾式弹射：身体朝向冻结，常以尾先行掠过目标）→ 硬刹归位。
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
                HoldInPlace(ctx);
                int windup = SeaShrimpDirector.TailFlipWindup;

                if (t < LockFrame) {
                    lockDir = (PredictTarget(ctx, 14f) - npc.Center).SafeNormalize(Vector2.UnitX);
                }

                float w = MathHelper.Clamp(t / (float)windup, 0f, 1f);
                float snap = MathF.Pow(w, 8f);
                ctx.SpineCurl = -(0.3f + 0.7f * snap);
                ctx.TailFlare = MathHelper.Clamp(0.35f - w * 0.35f, 0f, 1f);
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, w * 0.8f);
                ctx.WaveGain = 0.3f;

                float aimAlpha = MathHelper.Clamp((t - 10) / 18f, 0f, 1f) * (t >= LockFrame ? 0.6f : 0.3f);
                ctx.AddTelegraph(npc.Center + lockDir * 60f, lockDir, 620f, aimAlpha,
                    t >= LockFrame ? 0.85f : 0.5f);

                if (t == 2 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.55f, Pitch = -0.5f, MaxInstances = 2 }, npc.Center);
                }

                if (t >= windup) {
                    launched = true;
                    loco.LaunchBallistic(lockDir * SeaShrimpDirector.TailFlipSpeed,
                        SeaShrimpDirector.TailFlipFrames, SeaShrimpDirector.TailFlipBrake);
                    ctx.TailFlare = 1f;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item86 with { Volume = 0.85f, Pitch = -0.1f, MaxInstances = 2 }, npc.Center);
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.8f, Pitch = -0.3f, MaxInstances = 2 }, npc.Center);
                        ShakeNearby(npc.Center, 5.5f);
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

            if (loco.BallisticDone || t >= HardTimeout) {
                return EndAttack(ctx, 68);
            }
            return null;
        }
    }
}
