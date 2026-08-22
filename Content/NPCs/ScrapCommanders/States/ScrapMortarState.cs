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
    /// 废钢迫击：炮臂液压抬管就位 → 三发点射不同落点（炮口下压后坐逐发读出重量）→
    /// 泄压回摆。弹头落地砸出废钢堆，是 P2 磁暴的伏笔
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.Mortar, typeof(ScrapStateContext))]
    internal class ScrapMortarState : ScrapStateBase
    {
        public override string StateName => "Mortar";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.Mortar;

        //==================== 时序 ====================

        private const int VolleyStart = ScrapDirector.MortarPoseFrames;  //24
        private const int VolleyEnd = VolleyStart + ScrapDirector.MortarShotGap * ScrapDirector.MortarShots; //66
        private const int StateEnd = VolleyEnd + 22;                     //88

        private bool posed;
        private bool vented;
        /// <summary>已开火的最高发号（单调闩）</summary>
        private int lastShotFired = -1;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            const int arm = ScrapCommander.ArmCannon;
            int t = (int)Timer;

            npc.velocity *= 0.93f;
            LeanByVelocity(npc, 0.08f);

            Vector2 aim = MortarAimDir(ctx);
            //炮臂就位点：朝目标一侧半举
            Vector2 posePoint = npc.Center + npc.velocity + new Vector2(MathF.Sign(aim.X) * 104f, 42f);

            if (t < VolleyStart) {
                //==================== 液压抬管 ====================
                if (!posed) {
                    posed = true;
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.42f, Pitch = -0.6f, MaxInstances = 2 }, owner.GetArmPos(arm));
                    if (!Main.dedServ) {
                        PRTLoader.NewParticle<PRT_GhostRainMist>(owner.GetArmPos(arm),
                            new Vector2(0f, -0.5f), ScrapCommander.SmokeGray * 0.8f, 0.6f)?.Configure(34);
                    }
                }
                if (ctx.Owner.TargetInvalid()) {
                    return EndAttack(ctx, 45);
                }
                ApplyCannonPose(ctx, arm, posePoint, aim, 0.18f);
                Timer++;
                return null;
            }

            if (t < VolleyEnd) {
                //==================== 三发点射 ====================
                ApplyCannonPose(ctx, arm, posePoint, aim, 0.2f);

                int shotIndex = (t - VolleyStart) / ScrapDirector.MortarShotGap;
                if ((t - VolleyStart) % ScrapDirector.MortarShotGap == 0
                    && shotIndex < ScrapDirector.MortarShots && lastShotFired < shotIndex) {
                    lastShotFired = shotIndex;
                    FireShell(ctx, owner, arm, aim, shotIndex);
                }
                Timer++;
                return null;
            }

            //==================== 泄压回摆 ====================
            if (!vented) {
                vented = true;
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.35f, Pitch = 0.1f, MaxInstances = 2 }, owner.GetArmPos(arm));
                if (!Main.dedServ) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(owner.GetArmPos(arm) + new Vector2(0f, -10f),
                        new Vector2(0f, -0.7f), ScrapCommander.SmokeGray, 0.75f)?.Configure(44);
                }
            }

            Timer++;
            if (t >= StateEnd) {
                return EndAttack(ctx, 60);
            }
            return null;
        }

        private static void ApplyCannonPose(ScrapStateContext ctx, int arm, Vector2 posePoint, Vector2 aim, float spring) {
            ctx.Arms[arm] = new ArmDirective {
                Mode = ArmMode.Hold,
                Target = posePoint,
                Spring = spring,
                Damping = 0.8f,
                UseRot = true,
                WantRot = aim.ToRotation() - MathHelper.PiOver2,
                RotRate = 0.22f,
            };
        }

        /// <summary>炮管瞄向：朝目标一侧上扬 60° 的迫击姿态</summary>
        private static Vector2 MortarAimDir(ScrapStateContext ctx) {
            float side = MathF.Sign(ctx.Target.Center.X - ctx.Npc.Center.X);
            if (side == 0f) {
                side = 1f;
            }
            return new Vector2(side * MathF.Cos(1.05f), -MathF.Sin(1.05f));
        }

        /// <summary>开火拍：后坐 + 闷响两端都放，弹头只在权威端生成（spawn 参数自带全部初值）</summary>
        private void FireShell(ScrapStateContext ctx, ScrapCommander owner, int arm, Vector2 aim, int shotIndex) {
            NPC npc = ctx.Npc;
            Vector2 muzzle = owner.GetArmPos(arm) + aim * 28f;

            //炮口下压后坐：知重量者先退半步
            owner.ImpulseArm(arm, -aim * 9f);
            owner.CannonHeat = 30;
            SoundEngine.PlaySound(SoundID.Item61 with { Volume = 0.8f, Pitch = -0.4f + shotIndex * 0.06f, MaxInstances = 3 }, muzzle);
            SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.35f, Pitch = -0.5f, MaxInstances = 3 }, muzzle);
            ShakeNearby(npc.Center, 2f);
            if (!Main.dedServ) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(muzzle, aim * 1.2f, ScrapCommander.SmokeGray, 0.65f)?.Configure(34);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(muzzle + Main.rand.NextVector2Circular(4f, 4f),
                        aim.RotatedByRandom(0.3f) * Main.rand.NextFloat(4f, 9f),
                        Color.Lerp(ScrapCommander.WeldOrange, Color.White, Main.rand.NextFloat(0.4f)),
                        Main.rand.NextFloat(0.5f, 0.9f))?.Configure(true, Main.rand.Next(10, 18));
                }
            }

            if (VaultUtils.isClient) {
                return;
            }
            Player target = ctx.Target;
            float spreadX = (shotIndex - 1) * 140f;
            Vector2 landing = target.Center + new Vector2(spreadX + target.velocity.X * 18f, 0f);
            int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.MortarDamage);
            Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, SolveArcVelocity(muzzle, landing),
                ModContent.ProjectileType<ScrapMortarShell>(), damage, 5f, Main.myPlayer);
        }

        /// <summary>弹道解算：先定"必须有的弧顶高度"再反推初速与滞空
        /// 目标再低也保证一段明显的迫击弧线，绝不平射</summary>
        internal static Vector2 SolveArcVelocity(Vector2 muzzle, Vector2 landing) {
            const float gravity = ScrapDirector.MortarGravity;
            float dy = landing.Y - muzzle.Y;
            float vy = -MathF.Sqrt(MathF.Max(-2f * gravity * dy, 0f) + 92f);
            float flight = (-vy + MathF.Sqrt(MathF.Max(vy * vy + 2f * gravity * dy, 1f))) / gravity;
            float vx = MathHelper.Clamp((landing.X - muzzle.X) / flight, -17f, 17f);
            return new Vector2(vx, vy);
        }
    }
}
