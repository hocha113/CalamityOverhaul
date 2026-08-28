using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 单螯刺击：近螯后拉张钳（长预摇 36f）→ 锁向 → 一拍弹出（弹簧冲量的急停急出）→ 收臂。
    /// 预告即承诺：f24 锁向后不再追瞄；伤害窗=弹出后 12 帧（ClawDamageWindow 举旗）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.ClawJab, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpClawJabState : SeaShrimpStateBase
    {
        public override string StateName => "ClawJab";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.ClawJab;

        private const int WindupEnd = 36;
        private const int LockFrame = 24;
        private const int StrikeEnd = 48;
        private const int Total = 66;
        private const float JabReach = 250f;

        private Vector2 lockDir = Vector2.UnitX;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;
            HoldInPlace(ctx);

            Vector2 shoulder = ctx.Owner.Skeleton.ShoulderWorld(0);

            if (t < LockFrame) {
                //追瞄段：预告线跟踪目标
                lockDir = (PredictTarget(ctx, 8f) - shoulder).SafeNormalize(Vector2.UnitX);
            }

            if (t < WindupEnd) {
                //后拉蓄势：pow 曲线迟滞收臂，钳全张
                float w = t / (float)WindupEnd;
                float reel = MathF.Pow(w, 3f) * 52f;
                ctx.Claws[0] = new ClawDirective {
                    Mode = ClawMode.Hold,
                    Target = shoulder - lockDir * (14f + reel) - ctx.Owner.Skeleton.HeadDown * 8f,
                    Spring = 0.3f,
                    Damping = 0.7f,
                    ClawOpen = w,
                };
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, w * 0.5f);
                float aimAlpha = MathHelper.Clamp((t - 8) / (float)(WindupEnd - 8), 0f, 1f)
                    * (t >= LockFrame ? 0.55f : 0.3f);
                ctx.AddTelegraph(shoulder + lockDir * 30f, lockDir, JabReach + 90f, aimAlpha,
                    t >= LockFrame ? 0.85f : 0.5f);

                if (t == 2 && !Main.dedServ) {
                    //收臂蓄势：轻水涌（原 Item32 是吹叶机气流，水下违和）
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.45f, Pitch = -0.2f, MaxInstances = 2 }, shoulder);
                }
                return null;
            }

            if (t == WindupEnd) {
                //弹出帧：一帧冲量 + 高刚度弹簧 = 急出急停
                ctx.Owner.Skeleton.Arms[0].Impulse(lockDir * 36f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = -0.35f, MaxInstances = 2 }, shoulder);
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = 0.25f, MaxInstances = 2 }, shoulder);
                    ShakeNearby(npc.Center, 3.5f);
                }
                if (!VaultUtils.isClient) {
                    int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.ClawStrikeDamage);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), shoulder, Vector2.Zero,
                        ModContent.ProjectileType<SeaShrimpClawHitbox>(), damage, 3f, Main.myPlayer,
                        npc.whoAmI, 0f);
                }
            }

            if (t < StrikeEnd) {
                //刺出持位：钳合拢成锥
                ctx.Claws[0] = new ClawDirective {
                    Mode = ClawMode.Strike,
                    Target = ctx.Owner.Skeleton.ShoulderWorld(0) + lockDir * JabReach,
                    Spring = 0.55f,
                    Damping = 0.8f,
                    ClawOpen = 0f,
                };
                ctx.ClawDamageWindow = true;
                ctx.AfterimageStrength = MathF.Max(ctx.AfterimageStrength, 0.5f);
                return null;
            }

            //收臂段走守位默认；双出口：定长即完成
            if (t >= Total) {
                //连击链：P2+ 且目标仍贴身，顺势接空泡拳（确定性条件，各端一致，不掷随机）
                if (ctx.Phase >= 2 && ctx.AttackIndex % 2 == 0
                    && Vector2.Distance(ctx.Target.Center, npc.Center) < 560f) {
                    ctx.QueuedChainState = (int)SeaShrimpStateIndex.CavitationPunch;
                }
                return EndAttack(ctx, 46);
            }
            return null;
        }
    }
}
