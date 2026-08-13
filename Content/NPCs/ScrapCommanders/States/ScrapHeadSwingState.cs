using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.States
{
    /// <summary>
    /// 头锤摆荡：反向拉起蓄势（目镜警闪 + 越缩越紧）→ 一帧定初速的钟摆冲撞 →
    /// 越过目标即硬刹（早退计时）→ 拖链回摆。接触伤害只在速度过门槛时开——
    /// 冲势可见才咬人。过载阶段升级三连摆
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.HeadSwing, typeof(ScrapStateContext))]
    internal class ScrapHeadSwingState : ScrapStateBase
    {
        public override string StateName => "HeadSwing";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.HeadSwing;

        //==================== 单摆时序：蓄势[0,36) 飞行[36,62) 刹车[62,78) ====================

        private const int LaunchBeat = ScrapDirector.SwingWindup;   //36
        private const int FlightEnd = LaunchBeat + 26;              //62
        private const int SwingEnd = FlightEnd + 16;                //78

        private bool launched;
        private Vector2 launchAim = Vector2.UnitX;
        /// <summary>已完成的摆数（Counter 不够表达 bool 组合时的本地量）</summary>
        private int swingsDone;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            int t = (int)Timer;
            int swingsTotal = ctx.Phase >= 3 ? 3 : 1;

            //接触伤害严格对齐可见冲势
            bool striking = launched && npc.velocity.Length() > ScrapDirector.SwingContactSpeed;
            npc.damage = striking ? npc.defDamage : 0;

            if (t < LaunchBeat) {
                //==================== 反向拉起蓄势 ====================
                if (ctx.Owner.TargetInvalid()) {
                    return EndAttack(ctx, 45);
                }
                Vector2 away = (npc.Center - ctx.Target.Center).SafeNormalize(-Vector2.UnitY);
                //晚爆式收势：前 2/3 几乎不动，最后猛地向后吸一口
                float k = MathF.Pow(t / (float)LaunchBeat, 6f);
                npc.velocity = Vector2.Lerp(npc.velocity, away * (11f * k), 0.2f);
                npc.rotation = npc.rotation.AngleLerp(-MathF.Sign(ctx.Target.Center.X - npc.Center.X) * 0.22f * k, 0.2f);

                //目镜警闪加急
                if (t % Math.Max(2, 10 - t / 5) == 0 && t > 8) {
                    ctx.EyeScan = 0.5f;
                }
                if (t == LaunchBeat - 12) {
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.5f, Pitch = -0.65f, MaxInstances = 2 }, npc.Center);
                }
                Timer++;
                return null;
            }

            if (t < FlightEnd) {
                //==================== 钟摆冲撞 ====================
                if (!launched) {
                    launched = true;
                    launchAim = (PredictTarget(ctx, 12f) - npc.Center).SafeNormalize(Vector2.UnitX);
                    npc.velocity = launchAim * ScrapDirector.SwingLaunchSpeed;
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 2 }, npc.Center);
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.8f, Pitch = -0.15f, MaxInstances = 2 }, npc.Center);
                    ShakeNearby(npc.Center, 3f);
                    //出手爆点：枪口拍级别的挣脱感
                    ScrapVfx.MuzzleFlash(npc.Center, launchAim, 1.4f);
                    //链条被头拽直
                    owner.TautVibe = 12;
                    for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                        owner.ImpulseArm(i, -launchAim * 3f);
                    }
                }
                //热残影全程拉满：冲撞的速度语言
                ctx.AfterimageStrength = 1f;
                //微量追踪后迅速锁线：直线才读得出快
                if (t < LaunchBeat + 8) {
                    Vector2 want = (ctx.Target.Center - npc.Center).SafeNormalize(launchAim);
                    launchAim = Vector2.Lerp(launchAim, want, 0.06f).SafeNormalize(launchAim);
                    npc.velocity = launchAim * npc.velocity.Length();
                }
                //冲势逐帧递增：越冲越快
                npc.velocity *= 1.012f;
                npc.rotation = npc.rotation.AngleLerp(npc.velocity.X * 0.02f, 0.3f);

                //速度火星（速度门控的修饰层）
                if (!Main.dedServ && npc.velocity.Length() > 24f && t % 2 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        npc.Center + Main.rand.NextVector2Circular(28f, 28f),
                        -npc.velocity * 0.12f,
                        ScrapCommander.WeldOrange * 0.7f, Main.rand.NextFloat(0.5f, 0.9f))
                        ?.Configure(false, Main.rand.Next(8, 13));
                }

                //越过目标 220px 即早退，别飞出屏兜圈
                Vector2 toTarget = ctx.Target.Center - npc.Center;
                if (Vector2.Dot(toTarget, launchAim) < -220f) {
                    Timer = FlightEnd;
                    return null;
                }
                Timer++;
                return null;
            }

            if (t < SwingEnd) {
                //==================== 硬刹回摆 ====================
                npc.velocity *= 0.82f;
                npc.rotation = npc.rotation.AngleLerp(0f, 0.15f);
                Timer++;
                return null;
            }

            //一摆完成：过载阶段连三摆，摆间只留半拍衔接
            swingsDone++;
            if (swingsDone < swingsTotal && !ctx.Owner.TargetInvalid()) {
                launched = false;
                Timer = LaunchBeat - 14;
                return null;
            }
            return EndAttack(ctx, 70);
        }
    }
}
