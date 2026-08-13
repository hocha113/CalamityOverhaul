using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.States
{
    /// <summary>
    /// 熔断全械（过载终局招）：怒吼过载 → 四工具脱链化作自主猎手各占一角 →
    /// 裸头连做三记摆荡冲撞（残影拉满）→ 工具飞回归位。
    /// 全场压力最高的一招，押在 20% 之后才解锁
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.FusedFrenzy, typeof(ScrapStateContext))]
    internal class ScrapFusedFrenzyState : ScrapStateBase
    {
        public override string StateName => "FusedFrenzy";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.FusedFrenzy;

        //==================== 时序 ====================

        private const int DetachBeat = 26;
        /// <summary>三记摆荡：每记 窗 22 + 飞 24 + 刹 14 = 60</summary>
        private const int SwingCycle = 60;
        private const int SwingsStart = DetachBeat + 6;    //32
        private const int SwingsEnd = SwingsStart + SwingCycle * 3; //212
        private const int RecallBeat = 216;
        private const int StateEnd = 240;

        private bool roared;
        private bool detached;
        private bool recalled;
        private bool launched;
        private Vector2 launchAim = Vector2.UnitX;
        private int lastSwing = -1;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            int t = (int)Timer;

            ctx.WeldHeat = 1f;

            if (t < DetachBeat) {
                //==================== 怒吼过载 ====================
                if (!roared) {
                    roared = true;
                    if (ctx.Owner.TargetInvalid()) {
                        return EndAttack(ctx, 45);
                    }
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.9f, Pitch = 0f, MaxInstances = 1 }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 2 }, npc.Center);
                }
                npc.velocity *= 0.9f;
                ctx.EyeScan = (t % 6) / 6f;
                if (t % 5 == 0) {
                    ShakeNearby(npc.Center, 1f);
                }
                Timer++;
                return null;
            }

            //==================== 工具脱链 ====================
            if (!detached) {
                detached = true;
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.9f, Pitch = -0.5f, MaxInstances = 1 }, npc.Center);
                ShakeNearby(npc.Center, 4f);
                ScrapVfx.MetalExplosion(npc.Center, 1.1f);
                if (!VaultUtils.isClient) {
                    int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.ArmStrikeDamage);
                    damage = (int)(damage * 0.8f);
                    for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), owner.GetArmPos(i),
                            (owner.GetArmPos(i) - npc.Center).SafeNormalize(-Vector2.UnitY) * 7f,
                            ModContent.ProjectileType<ScrapAutonomousTool>(), damage, 4f,
                            Main.myPlayer, i, npc.whoAmI);
                    }
                }
            }
            //脱链期本体不画工具（自主工具弹幕在场上）
            if (t < RecallBeat) {
                for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                    ctx.ToolAlpha[i] = 0f;
                }
            }
            else {
                //归位渐显
                float back = MathHelper.Clamp((t - RecallBeat) / 14f, 0f, 1f);
                for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                    ctx.ToolAlpha[i] = back;
                }
            }

            if (t < SwingsEnd && t >= SwingsStart) {
                //==================== 裸头三连摆 ====================
                int local = (t - SwingsStart) % SwingCycle;
                int swingIndex = (t - SwingsStart) / SwingCycle;
                bool striking = launched && npc.velocity.Length() > ScrapDirector.SwingContactSpeed;
                npc.damage = striking ? npc.defDamage : 0;

                if (local < 22) {
                    //窗：反向拉起
                    launched = false;
                    Vector2 away = (npc.Center - ctx.Target.Center).SafeNormalize(-Vector2.UnitY);
                    float k = MathF.Pow(local / 22f, 6f);
                    npc.velocity = Vector2.Lerp(npc.velocity, away * (12f * k), 0.22f);
                    ctx.EyeScan = 0.5f;
                    if (local == 12) {
                        SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.45f, Pitch = -0.6f, MaxInstances = 2 }, npc.Center);
                    }
                }
                else if (local < 46) {
                    //飞：一帧定初速
                    if (lastSwing < swingIndex) {
                        lastSwing = swingIndex;
                        launched = true;
                        launchAim = (PredictTarget(ctx, 11f) - npc.Center).SafeNormalize(Vector2.UnitX);
                        npc.velocity = launchAim * (ScrapDirector.SwingLaunchSpeed + 4f);
                        SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.8f, Pitch = -0.1f + swingIndex * 0.08f, MaxInstances = 2 }, npc.Center);
                        ScrapVfx.MuzzleFlash(npc.Center, launchAim, 1.3f);
                        ShakeNearby(npc.Center, 2.6f);
                        owner.TautVibe = 10;
                    }
                    npc.velocity *= 1.012f;
                    npc.rotation = npc.rotation.AngleLerp(npc.velocity.X * 0.02f, 0.3f);
                    ctx.AfterimageStrength = 1f;
                    //越过目标即早退到刹车
                    Vector2 toTarget = ctx.Target.Center - npc.Center;
                    if (Vector2.Dot(toTarget, launchAim) < -200f && local < 44) {
                        Timer = SwingsStart + swingIndex * SwingCycle + 46;
                        return null;
                    }
                }
                else {
                    //刹
                    npc.velocity *= 0.8f;
                    npc.rotation = npc.rotation.AngleLerp(0f, 0.15f);
                }
                Timer++;
                return null;
            }

            //==================== 归位收势 ====================
            npc.damage = 0;
            npc.velocity *= 0.9f;
            if (t >= RecallBeat && !recalled) {
                recalled = true;
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = 0.1f, MaxInstances = 2 }, npc.Center);
                owner.TautVibe = 10;
            }
            Timer++;
            if (t >= StateEnd) {
                return EndAttack(ctx, 85);
            }
            return null;
        }
    }
}
