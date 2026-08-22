using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.States
{
    /// <summary>
    /// 锯轮放犬：蓄力回缩 tell（锯提前起转）→ 一帧弹出突刺 →
    /// 钉在伸展位撕磨并放出两只脱链地锯犬 → 棘轮收回。
    /// 臂击伤害由 ScrapArmHitbox 弹幕承载，各端判定线贴本地画面
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.SawLaunch, typeof(ScrapStateContext))]
    internal class ScrapSawLaunchState : ScrapStateBase
    {
        public override string StateName => "SawLaunch";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.SawLaunch;

        //==================== 时序 ====================

        private const int DartBeat = ScrapDirector.DartWindup;                  //40
        private const int GrindStart = DartBeat + ScrapDirector.DartExtendFrames; //50
        private const int GrindEnd = GrindStart + 22;                           //72
        private const int StateEnd = GrindEnd + 20;                             //92
        private static readonly int[] RatchetBeats = { 76, 82, 88 };

        /// <summary>各端本地瞄准（弹出拍锁定；判定线贴本地臂，允许端间微差）</summary>
        private Vector2 aim = -Vector2.UnitY;
        private bool darted;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            const int arm = ScrapCommander.ArmSaw;
            int t = (int)Timer;

            //中枢突刺期持位微刹，臂是主角
            npc.velocity *= 0.93f;
            LeanByVelocity(npc, 0.08f);
            if (t >= 10) {
                owner.SawSpinning = true;
            }

            if (t < DartBeat) {
                //==================== 蓄力回缩 tell ====================
                if (ctx.Owner.TargetInvalid()) {
                    return EndAttack(ctx, 45);
                }
                Vector2 aimPos = PredictTarget(ctx, 8f);
                aim = (aimPos - owner.GetArmPos(arm)).SafeNormalize(Vector2.UnitY);

                ctx.Arms[arm] = new ArmDirective {
                    Mode = ArmMode.Hold,
                    Target = owner.ShoulderWorld(arm) - aim * 36f + new Vector2(0f, -6f),
                    Spring = 0.22f,
                    Damping = 0.8f,
                    UseRot = true,
                    WantRot = aim.ToRotation() - MathHelper.PiOver2,
                    RotRate = 0.4f,
                };

                if (t == 10) {
                    SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.5f, Pitch = -0.15f, MaxInstances = 2 }, owner.GetArmPos(arm));
                }
                //蓄势收拢火星，72% 后静默，弹出前的吸气
                if (!Main.dedServ && t < DartBeat * 0.72f && t % 3 == 1) {
                    Vector2 from = owner.GetArmPos(arm) + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 84f);
                    PRTLoader.NewParticle<PRT_Spark>(from, (owner.GetArmPos(arm) - from) * 0.14f,
                        ScrapCommander.WeldOrange * 0.6f, Main.rand.NextFloat(0.35f, 0.55f))
                        ?.Configure(false, 9);
                }
                Timer++;
                return null;
            }

            if (t < GrindStart) {
                //==================== 一帧弹出 ====================
                if (!darted) {
                    darted = true;
                    Vector2 aimPos = PredictTarget(ctx, 6f);
                    aim = (aimPos - owner.GetArmPos(arm)).SafeNormalize(Vector2.UnitY);
                    //链长按敌距收口：锯口正好啃在目标身上，不越过去磨空气
                    float reach = MathHelper.Clamp(
                        Vector2.Distance(aimPos, owner.ShoulderWorld(arm)) + 30f, 170f, ScrapDirector.DartMaxReach);
                    owner.BeginDart(arm, aim, reach);
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.65f, Pitch = 0.1f, MaxInstances = 3 }, owner.GetArmPos(arm));
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 3 }, owner.GetArmPos(arm));
                    ShakeNearby(npc.Center, 2.5f);
                    //臂击判定线只在权威端生成，各端贴本地臂画面判碰撞
                    if (!VaultUtils.isClient) {
                        int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.ArmStrikeDamage);
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            owner.GetArmPos(arm), Vector2.Zero,
                            ModContent.ProjectileType<ScrapArmHitbox>(), damage, 4f,
                            Main.myPlayer, npc.whoAmI, arm);
                    }
                }
                ctx.Arms[arm] = new ArmDirective {
                    Mode = ArmMode.Ballistic,
                    UseRot = true,
                    WantRot = aim.ToRotation() - MathHelper.PiOver2,
                    RotRate = 0.5f,
                };
                Timer++;
                return null;
            }

            if (t < GrindEnd) {
                //==================== 撕磨 + 放出地锯犬 ====================
                ctx.Arms[arm] = new ArmDirective {
                    Mode = ArmMode.Hold,
                    Target = owner.GetArmPos(arm) + aim * 1.4f,
                    Spring = 0.3f,
                    Damping = 0.72f,
                    UseRot = true,
                    WantRot = aim.ToRotation() - MathHelper.PiOver2,
                    RotRate = 0.45f,
                };

                if (t % 8 == 2) {
                    SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.42f, Pitch = 0.25f, MaxInstances = 2 }, owner.GetArmPos(arm));
                }
                if (!Main.dedServ) {
                    if (t % 2 == 0) {
                        PRTLoader.NewParticle<PRT_Spark>(
                            owner.GetArmPos(arm) + Main.rand.NextVector2Circular(16f, 16f),
                            aim.RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f)) * Main.rand.NextFloat(3f, 8f),
                            Color.Lerp(ScrapCommander.WeldOrange, Color.White, Main.rand.NextFloat(0.5f)),
                            Main.rand.NextFloat(0.7f, 1.2f))?.Configure(true, Main.rand.Next(10, 18));
                    }
                    //锯口甩油
                    if (t % 4 == 1) {
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(
                            owner.GetArmPos(arm) + Main.rand.NextVector2Circular(14f, 14f),
                            aim.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(2f, 5f),
                            ScrapCommander.OilDark * 0.7f, Main.rand.NextFloat(0.3f, 0.5f))
                            ?.Configure(Main.rand.Next(14, 24), 0f);
                    }
                }

                //两只地锯犬先后脱链（拍各端一致，生成只在权威端）
                if (t == GrindStart + 4 || t == GrindStart + 14) {
                    SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.55f, Pitch = 0.45f, MaxInstances = 2 }, owner.GetArmPos(arm));
                    if (!VaultUtils.isClient) {
                        int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.GroundSawDamage);
                        float dir = MathF.Sign(ctx.Target.Center.X - owner.GetArmPos(arm).X);
                        if (dir == 0f) {
                            dir = 1f;
                        }
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            owner.GetArmPos(arm) + aim * 12f, new Vector2(dir * 6.5f, 3f),
                            ModContent.ProjectileType<ScrapGroundSaw>(), damage, 3f,
                            Main.myPlayer, dir, npc.whoAmI);
                    }
                }
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
                return EndAttack(ctx, 50);
            }
            return null;
        }
    }
}
