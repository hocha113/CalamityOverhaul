using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.States
{
    /// <summary>
    /// 钳爪绞刑·虚实二连：长预警回缩 → 半程假突刺急停（骗位移）→
    /// 读玩家当前走位的全程真突刺 → 液压重咬 → 棘轮收回。
    /// 判定线走几何门控：急停回收段臂缩回肩窝，伤害窗自动关闭
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.ViceSnatch, typeof(ScrapStateContext))]
    internal class ScrapViceSnatchState : ScrapStateBase
    {
        public override string StateName => "ViceSnatch";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.ViceSnatch;

        //==================== 时序 ====================

        private const int FeintBeat = ScrapDirector.ViceWindup;    //45 假突刺
        private const int FeintStop = FeintBeat + 8;               //53 急停
        private const int RealBeat = FeintStop + 10;               //63 真突刺
        private const int ClampStart = RealBeat + 10;              //73
        private const int ClampEnd = ClampStart + 12;              //85
        private const int StateEnd = ClampEnd + 17;                //102
        private static readonly int[] RatchetBeats = { 88, 93, 98 };

        private Vector2 aim = -Vector2.UnitY;
        private bool feinted;
        private bool realDarted;
        private bool clamped;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            const int arm = ScrapCommander.ArmVice;
            int t = (int)Timer;

            npc.velocity *= 0.93f;
            LeanByVelocity(npc, 0.08f);

            if (t < FeintBeat) {
                //==================== 长预警回缩 ====================
                if (ctx.Owner.TargetInvalid()) {
                    return EndAttack(ctx, 45);
                }
                Vector2 aimPos = PredictTarget(ctx, 10f);
                aim = (aimPos - owner.GetArmPos(arm)).SafeNormalize(Vector2.UnitY);

                ctx.Arms[arm] = new ArmDirective {
                    Mode = ArmMode.Hold,
                    Target = owner.ShoulderWorld(arm) - aim * 40f + new Vector2(0f, -8f),
                    Spring = 0.2f,
                    Damping = 0.8f,
                    UseRot = true,
                    WantRot = aim.ToRotation() - MathHelper.PiOver2,
                    RotRate = 0.35f,
                };
                //预警线后半程亮起
                ctx.AddTelegraph(owner.GetArmPos(arm), aim, ScrapDirector.ViceMaxReach,
                    MathHelper.Clamp((t - FeintBeat * 0.3f) / (FeintBeat * 0.55f), 0f, 1f));

                if (t == 8) {
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 2 }, owner.GetArmPos(arm));
                }
                Timer++;
                return null;
            }

            if (t < FeintStop) {
                //==================== 假突刺：半程弹出 ====================
                if (!feinted) {
                    feinted = true;
                    Vector2 aimPos = PredictTarget(ctx, 6f);
                    aim = (aimPos - owner.GetArmPos(arm)).SafeNormalize(Vector2.UnitY);
                    owner.BeginDart(arm, aim, ScrapDirector.ViceMaxReach * 0.45f);
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.55f, Pitch = 0.25f, MaxInstances = 3 }, owner.GetArmPos(arm));
                    //判定线现在就位：几何门控保证急停段不打人
                    if (!VaultUtils.isClient) {
                        int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.ArmStrikeDamage);
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            owner.GetArmPos(arm), Vector2.Zero,
                            ModContent.ProjectileType<ScrapArmHitbox>(), damage, 5f,
                            Main.myPlayer, npc.whoAmI, arm);
                    }
                }
                ctx.Arms[arm] = BallisticAim(aim);
                Timer++;
                return null;
            }

            if (t < RealBeat) {
                //==================== 急停回收：骗完位移的那口气 ====================
                if (t == FeintStop) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.55f, Pitch = 0.4f, MaxInstances = 2 }, owner.GetArmPos(arm));
                    ScrapVfx.HitSparks(owner.GetArmPos(arm), -aim, 0.8f);
                    owner.TautVibe = 8;
                }
                //急拽回肩窝
                ctx.Arms[arm] = new ArmDirective {
                    Mode = ArmMode.Hold,
                    Target = owner.ShoulderWorld(arm) - aim * 30f,
                    Spring = 0.34f,
                    Damping = 0.7f,
                    UseRot = true,
                    WantRot = aim.ToRotation() - MathHelper.PiOver2,
                    RotRate = 0.3f,
                };
                //预警线急闪重亮：第二段要来了
                ctx.AddTelegraph(owner.GetArmPos(arm), aim, ScrapDirector.ViceMaxReach,
                    0.4f + 0.6f * ((t - FeintStop) / (float)(RealBeat - FeintStop)), 0.85f);
                Timer++;
                return null;
            }

            if (t < ClampStart) {
                //==================== 真突刺：读当前走位，全程链长 ====================
                if (!realDarted) {
                    realDarted = true;
                    Vector2 aimPos = PredictTarget(ctx, 5f);
                    aim = (aimPos - owner.GetArmPos(arm)).SafeNormalize(Vector2.UnitY);
                    float reach = MathHelper.Clamp(
                        Vector2.Distance(aimPos, owner.ShoulderWorld(arm)) + 26f, 190f, ScrapDirector.ViceMaxReach);
                    owner.BeginDart(arm, aim, reach);
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.75f, Pitch = 0.02f, MaxInstances = 3 }, owner.GetArmPos(arm));
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.55f, Pitch = 0.15f, MaxInstances = 3 }, owner.GetArmPos(arm));
                    ShakeNearby(npc.Center, 2.8f);
                }
                ctx.Arms[arm] = BallisticAim(aim);
                Timer++;
                return null;
            }

            if (t < ClampEnd) {
                //==================== 液压重咬 ====================
                if (!clamped) {
                    clamped = true;
                    owner.ViceClampFrames = 8;
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.9f, Pitch = -0.55f, MaxInstances = 2 }, owner.GetArmPos(arm));
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 2 }, owner.GetArmPos(arm));
                    ShakeNearby(owner.GetArmPos(arm), 2.2f, 900f);
                    ScrapVfx.HitSparks(owner.GetArmPos(arm), aim);
                }
                ctx.Arms[arm] = new ArmDirective {
                    Mode = ArmMode.Hold,
                    Target = owner.GetArmPos(arm) + aim * 0.4f,
                    Spring = 0.3f,
                    Damping = 0.72f,
                    UseRot = true,
                    WantRot = aim.ToRotation() - MathHelper.PiOver2,
                    RotRate = 0.45f,
                };
                Timer++;
                return null;
            }

            //==================== 棘轮收回 ====================
            ctx.Arms[arm] = new ArmDirective {
                Mode = ArmMode.Hold,
                Target = owner.RestTarget(arm),
                Spring = 0.16f,
                Damping = 0.8f,
            };
            for (int i = 0; i < RatchetBeats.Length; i++) {
                if (t == RatchetBeats[i]) {
                    SoundEngine.PlaySound(SoundID.Item37 with {
                        Volume = 0.35f,
                        Pitch = 0.35f - i * 0.1f,
                        MaxInstances = 3
                    }, owner.GetArmPos(arm));
                }
            }

            Timer++;
            if (t >= StateEnd) {
                return EndAttack(ctx, 55);
            }
            return null;
        }

        private static ArmDirective BallisticAim(Vector2 aim) => new() {
            Mode = ArmMode.Ballistic,
            UseRot = true,
            WantRot = aim.ToRotation() - MathHelper.PiOver2,
            RotRate = 0.5f,
        };
    }
}
