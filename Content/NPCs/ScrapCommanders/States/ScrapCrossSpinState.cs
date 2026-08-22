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
    /// 链锤十字旋：四工具收拢成十字（预警辐条亮线）→ 随头自旋，
    /// 辐条半径先扩到三百再收回，外扩逼走位、内收给反打窗。
    /// 辐条间隙与外圈是安全读法，本体缓压玩家保持威胁
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.CrossSpin, typeof(ScrapStateContext))]
    internal class ScrapCrossSpinState : ScrapStateBase
    {
        public override string StateName => "CrossSpin";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.CrossSpin;

        //==================== 时序 ====================

        private const int GatherFrames = 30;
        private const int SpinEnd = 110;
        private const int StateEnd = 130;
        /// <summary>辐条判定第二波补位拍（判定线 56f 寿命，旋转期需要两茬）</summary>
        private const int SecondHitboxBeat = 82;

        private float spinAngle;
        private bool hitboxWave1;
        private bool hitboxWave2;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            int t = (int)Timer;

            if (t < GatherFrames) {
                //==================== 收拢与预警 ====================
                if (t == 0 && ctx.Owner.TargetInvalid()) {
                    return EndAttack(ctx, 45);
                }
                npc.velocity *= 0.9f;
                spinAngle = owner.Seed % MathHelper.TwoPi;
                for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                    float ang = spinAngle + i * MathHelper.PiOver2;
                    ctx.Arms[i] = new ArmDirective {
                        Mode = ArmMode.Hold,
                        Target = npc.Center + ang.ToRotationVector2() * 74f,
                        Spring = 0.3f,
                        Damping = 0.74f,
                        UseRot = true,
                        WantRot = ang + MathHelper.PiOver2,
                        RotRate = 0.4f,
                    };
                    //辐条预警线：读出即将展开的旋切面
                    ctx.AddTelegraph(npc.Center, ang.ToRotationVector2(), 320f,
                        MathHelper.Clamp(t / (float)GatherFrames, 0f, 0.8f), 0.5f);
                }
                if (t == 4) {
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.5f, Pitch = -0.55f, MaxInstances = 2 }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.45f, Pitch = -0.25f, MaxInstances = 2 }, npc.Center);
                }
                Timer++;
                return null;
            }

            if (t < SpinEnd) {
                //==================== 自旋扩张-收拢 ====================
                float spinT = (t - GatherFrames) / (float)(SpinEnd - GatherFrames);
                //转速爬坡
                float spinSpeed = MathHelper.Lerp(0.06f, 0.2f, MathHelper.Clamp(spinT * 1.6f, 0f, 1f));
                spinAngle += spinSpeed;
                //半径波：外扩 → 收拢
                float radius = spinT < 0.5f
                    ? MathHelper.Lerp(74f, 300f, MathF.Sin(spinT * MathHelper.Pi))
                    : MathHelper.Lerp(120f, 300f, MathF.Sin(spinT * MathHelper.Pi));

                for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                    float ang = spinAngle + i * MathHelper.PiOver2;
                    ctx.Arms[i] = new ArmDirective {
                        Mode = ArmMode.Snap,
                        Target = npc.Center + ang.ToRotationVector2() * radius,
                        UseRot = true,
                        WantRot = ang + MathHelper.PiOver2,
                        RotRate = 0.6f,
                    };
                }
                owner.SawSpinning = true;

                //本体缓压玩家：旋切的威胁必须在推进
                Vector2 to = ctx.Target.Center - npc.Center;
                if (to.Length() > 40f) {
                    npc.velocity = Vector2.Lerp(npc.velocity, to.SafeNormalize(Vector2.Zero) * 3.4f, 0.06f);
                }

                //判定辐条两茬补位（生成只在权威端）
                if (!hitboxWave1 && t == GatherFrames) {
                    hitboxWave1 = true;
                    SpawnSpokeHitboxes(ctx, owner, npc);
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 1 }, npc.Center);
                    ShakeNearby(npc.Center, 2.5f);
                }
                if (!hitboxWave2 && t == SecondHitboxBeat) {
                    hitboxWave2 = true;
                    SpawnSpokeHitboxes(ctx, owner, npc);
                }
                //旋切呼啸：音高随转速走
                if (t % 12 == 0) {
                    SoundEngine.PlaySound(SoundID.Item22 with {
                        Volume = 0.4f,
                        Pitch = -0.2f + spinSpeed * 2.4f,
                        MaxInstances = 3
                    }, npc.Center);
                }
                Timer++;
                return null;
            }

            //==================== 收势泄压 ====================
            npc.velocity *= 0.9f;
            if (t == SpinEnd + 4) {
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.4f, Pitch = 0.05f, MaxInstances = 2 }, npc.Center);
            }
            Timer++;
            if (t >= StateEnd) {
                return EndAttack(ctx, 70);
            }
            return null;
        }

        private static void SpawnSpokeHitboxes(ScrapStateContext ctx, ScrapCommander owner, NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.ArmStrikeDamage);
            for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                Projectile.NewProjectile(npc.GetSource_FromAI(),
                    owner.GetArmPos(i), Vector2.Zero,
                    ModContent.ProjectileType<ScrapArmHitbox>(), damage, 4f,
                    Main.myPlayer, npc.whoAmI, i);
            }
        }
    }
}
