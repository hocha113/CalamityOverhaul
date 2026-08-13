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
    /// 锯炮协奏（P2 组合招）：锯臂连打三记短突刺贴身施压（striker），
    /// 炮臂同拍持续迫击封走位（zoning）——一主一辅的显式分工，
    /// 双臂错拍让屏幕没有静止帧
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.SawCannonCombo, typeof(ScrapStateContext))]
    internal class ScrapSawCannonComboState : ScrapStateBase
    {
        public override string StateName => "SawCannonCombo";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.SawCannonCombo;

        //==================== 时序 ====================

        private const int PoseFrames = 20;
        /// <summary>锯的三记短突起拍（每记：收 10f + 弹 8f + 钉 6f + 回 6f）</summary>
        private static readonly int[] DartBeats = { 20, 50, 80 };
        private const int DartCycle = 30;
        /// <summary>炮的三发错拍</summary>
        private static readonly int[] ShellBeats = { 35, 65, 95 };
        private const int StateEnd = 128;

        private Vector2 sawAim = -Vector2.UnitY;
        private int lastDart = -1;
        private int lastShell = -1;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            int t = (int)Timer;

            npc.velocity *= 0.94f;
            LeanByVelocity(npc, 0.08f);

            if (t == 0 && ctx.Owner.TargetInvalid()) {
                return EndAttack(ctx, 45);
            }
            if (t == 2) {
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.45f, Pitch = -0.3f, MaxInstances = 1 }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.45f, Pitch = -0.2f, MaxInstances = 2 }, npc.Center);
            }
            owner.SawSpinning = true;

            //==================== 炮臂：全程迫击姿态，错拍点射 ====================
            float side = MathF.Sign(ctx.Target.Center.X - npc.Center.X);
            if (side == 0f) {
                side = 1f;
            }
            Vector2 mortarAim = new(side * MathF.Cos(1.05f), -MathF.Sin(1.05f));
            ctx.Arms[ScrapCommander.ArmCannon] = new ArmDirective {
                Mode = ArmMode.Hold,
                Target = npc.Center + npc.velocity + new Vector2(side * 104f, 42f),
                Spring = 0.2f,
                Damping = 0.8f,
                UseRot = true,
                WantRot = mortarAim.ToRotation() - MathHelper.PiOver2,
                RotRate = 0.22f,
            };
            for (int i = 0; i < ShellBeats.Length; i++) {
                if (t == ShellBeats[i] && lastShell < i) {
                    lastShell = i;
                    FireShell(ctx, owner, npc, mortarAim, i);
                }
            }

            //==================== 锯臂：三记短突 ====================
            int dartIndex = -1;
            for (int i = 0; i < DartBeats.Length; i++) {
                if (t >= DartBeats[i] && t < DartBeats[i] + DartCycle) {
                    dartIndex = i;
                    break;
                }
            }
            if (dartIndex >= 0) {
                int local = t - DartBeats[dartIndex];
                const int arm = ScrapCommander.ArmSaw;
                if (local < 10) {
                    //收劲：口咬目标
                    Vector2 aimPos = PredictTarget(ctx, 6f);
                    sawAim = (aimPos - owner.GetArmPos(arm)).SafeNormalize(Vector2.UnitY);
                    ctx.Arms[arm] = new ArmDirective {
                        Mode = ArmMode.Hold,
                        Target = owner.ShoulderWorld(arm) - sawAim * 30f,
                        Spring = 0.26f,
                        Damping = 0.76f,
                        UseRot = true,
                        WantRot = sawAim.ToRotation() - MathHelper.PiOver2,
                        RotRate = 0.4f,
                    };
                    ctx.AddTelegraph(owner.GetArmPos(arm), sawAim, 340f, local / 10f, 0.55f);
                }
                else if (local < 18) {
                    //短弹出
                    if (local == 10 && lastDart < dartIndex) {
                        lastDart = dartIndex;
                        owner.BeginDart(arm, sawAim, 320f);
                        SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.55f, Pitch = 0.2f, MaxInstances = 3 }, owner.GetArmPos(arm));
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
                        WantRot = sawAim.ToRotation() - MathHelper.PiOver2,
                        RotRate = 0.5f,
                    };
                }
                else if (local < 24) {
                    //钉住磨半拍
                    ctx.Arms[arm] = new ArmDirective {
                        Mode = ArmMode.Hold,
                        Target = owner.GetArmPos(arm) + sawAim * 1.2f,
                        Spring = 0.3f,
                        Damping = 0.72f,
                        UseRot = true,
                        WantRot = sawAim.ToRotation() - MathHelper.PiOver2,
                        RotRate = 0.45f,
                    };
                }
                else {
                    //快收
                    ctx.Arms[arm] = new ArmDirective {
                        Mode = ArmMode.Hold,
                        Target = owner.RestTarget(arm),
                        Spring = 0.2f,
                        Damping = 0.78f,
                    };
                }
            }

            Timer++;
            if (t >= StateEnd) {
                return EndAttack(ctx, 70);
            }
            return null;
        }

        /// <summary>炮的错拍点射：空爆迫击弹（沿用统一弧线解算）</summary>
        private static void FireShell(ScrapStateContext ctx, ScrapCommander owner, NPC npc, Vector2 aim, int index) {
            Vector2 muzzle = owner.GetArmPos(ScrapCommander.ArmCannon) + aim * 28f;
            owner.ImpulseArm(ScrapCommander.ArmCannon, -aim * 9f);
            owner.CannonHeat = 30;
            SoundEngine.PlaySound(SoundID.Item61 with { Volume = 0.75f, Pitch = -0.35f + index * 0.07f, MaxInstances = 3 }, muzzle);
            ScrapVfx.MuzzleFlash(muzzle, aim);
            ShakeNearby(npc.Center, 1.8f);

            if (VaultUtils.isClient) {
                return;
            }
            Player target = ctx.Target;
            Vector2 landing = target.Center + new Vector2((index - 1) * 150f + target.velocity.X * 16f, 0f);
            int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.MortarDamage);
            Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle,
                ScrapMortarState.SolveArcVelocity(muzzle, landing),
                ModContent.ProjectileType<ScrapMortarShell>(), damage, 5f, Main.myPlayer);
        }
    }
}
