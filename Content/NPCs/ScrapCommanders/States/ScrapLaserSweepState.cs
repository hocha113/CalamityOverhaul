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
    /// 镭射扫削：拉栓滑步 → 交叉双发 → 反向滑步 → 再双发 →
    /// 细射线预扫一道弧（虚线读程）→ 同弧热射线回扫 → 收势。
    /// 首组脉冲 80% 速热身（转移后弹速公平阀），第二组满速
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.LaserSweep, typeof(ScrapStateContext))]
    internal class ScrapLaserSweepState : ScrapStateBase
    {
        public override string StateName => "LaserSweep";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.LaserSweep;

        //==================== 时序：滑步A 双发B 滑步C 双发D 扫削射线 收势 ====================

        private const int StrafeA = ScrapDirector.LaserStrafeFrames;      //12
        private const int VolleyB = StrafeA + ScrapDirector.LaserVolleyFrames; //28
        private const int StrafeC = VolleyB + ScrapDirector.LaserStrafeFrames; //40
        private const int VolleyD = StrafeC + ScrapDirector.LaserVolleyFrames; //56
        private const int BeamEnd = VolleyD + 62;                         //118 预扫30+回扫30+余量
        private const int StateEnd = BeamEnd + 12;                        //130

        private bool strafedA;
        private bool strafedC;
        private bool beamCast;
        /// <summary>已开火的最高脉冲号（单调闩，0..3）</summary>
        private int lastPulseFired = -1;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            const int arm = ScrapCommander.ArmLaser;
            int t = (int)Timer;

            //镭射臂全程持位瞄准；扫削期锁向射线当前角
            Vector2 aimPos = ctx.Target.Center + ctx.Target.velocity * 6f;
            Vector2 aim = (aimPos - owner.GetArmPos(arm)).SafeNormalize(Vector2.UnitX);
            float wantRot = aim.ToRotation() - MathHelper.PiOver2;
            if (t >= VolleyD) {
                //跟着场上的扫削射线转管（射线角是各端本地计时的确定性函数）
                int beamType = ModContent.ProjectileType<ScrapSweepBeam>();
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.type == beamType && (int)p.ai[0] == npc.whoAmI) {
                        wantRot = p.rotation - MathHelper.PiOver2;
                        break;
                    }
                }
            }
            ctx.Arms[arm] = new ArmDirective {
                Mode = ArmMode.Hold,
                Target = npc.Center + npc.velocity + new Vector2(MathF.Sign(aim.X) * 122f, -4f),
                Spring = 0.2f,
                Damping = 0.78f,
                UseRot = true,
                WantRot = wantRot,
                RotRate = t >= VolleyD ? 0.5f : 0.35f,
            };
            LeanByVelocity(npc, 0.1f);

            if (t < StrafeA || (t >= VolleyB && t < StrafeC)) {
                //==================== 快速滑步：一帧定横速，硬刹收尾 ====================
                bool first = t < StrafeA;
                ref bool strafed = ref first ? ref strafedA : ref strafedC;
                if (!strafed) {
                    strafed = true;
                    if (first && ctx.Owner.TargetInvalid()) {
                        return EndAttack(ctx, 45);
                    }
                    float side = MathF.Sign(ctx.Target.Center.X - npc.Center.X);
                    if (side == 0f) {
                        side = 1f;
                    }
                    //先撤半步再切回，两段滑步方向相反
                    float dir = first ? -side : side;
                    npc.velocity = new Vector2(dir * 13f, npc.velocity.Y * 0.4f);
                    //拉栓音——脉冲前的机械应答
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 3 }, owner.GetArmPos(arm));
                    if (first) {
                        SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.32f, Pitch = -0.35f, MaxInstances = 3 }, owner.GetArmPos(arm));
                    }
                }
                int local = first ? t : t - VolleyB;
                npc.velocity.X *= local < 7 ? 0.98f : 0.82f;
                Timer++;
                return null;
            }

            if (t < VolleyB || (t >= StrafeC && t < VolleyD)) {
                //==================== 双发短脉冲：滑行余势中点射 ====================
                npc.velocity *= 0.9f;
                bool firstVolley = t < VolleyB;
                int local = firstVolley ? t - StrafeA : t - StrafeC;
                int baseIndex = firstVolley ? 0 : 2;

                //出弹前 4 帧积光
                if (local == 0 || local == 6) {
                    owner.LaserFlash = 5;
                }
                int shot = local == 4 ? 0 : local == 10 ? 1 : -1;
                if (shot >= 0 && lastPulseFired < baseIndex + shot) {
                    lastPulseFired = baseIndex + shot;
                    //首组 80% 速热身，第二组满速
                    FirePulse(ctx, owner, arm, firstVolley ? 0.8f : 1f);
                }
                Timer++;
                return null;
            }

            if (t < BeamEnd) {
                //==================== 扫削射线：预扫读程 + 热回扫 ====================
                npc.velocity *= 0.92f;
                if (!beamCast) {
                    beamCast = true;
                    owner.LaserFlash = 5;
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.55f, Pitch = 0.1f, MaxInstances = 2 }, owner.GetArmPos(arm));
                    if (!VaultUtils.isClient) {
                        //扫向朝目标运动方向切，velocity 携带瞄准向量
                        float dir = MathF.Sign(ctx.Target.velocity.X);
                        if (dir == 0f) {
                            dir = MathF.Sign(ctx.Target.Center.X - npc.Center.X);
                        }
                        if (dir == 0f) {
                            dir = 1f;
                        }
                        Vector2 beamAim = (ctx.Target.Center - owner.GetArmPos(arm)).SafeNormalize(Vector2.UnitX);
                        int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.LaserPulseDamage) + 6;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), owner.GetArmPos(arm),
                            beamAim, ModContent.ProjectileType<ScrapSweepBeam>(), damage, 3f,
                            Main.myPlayer, npc.whoAmI, dir);
                    }
                }
                Timer++;
                return null;
            }

            //==================== 收势 ====================
            npc.velocity *= 0.88f;
            Timer++;
            if (t >= StateEnd) {
                return EndAttack(ctx, 65);
            }
            return null;
        }

        /// <summary>脉冲开火拍：细快锈弹（权威端生成），臂小幅后坐</summary>
        private static void FirePulse(ScrapStateContext ctx, ScrapCommander owner, int arm, float speedScale) {
            NPC npc = ctx.Npc;
            Vector2 aimPos = ctx.Target.Center + ctx.Target.velocity * 6f;
            Vector2 aim = (aimPos - owner.GetArmPos(arm)).SafeNormalize(Vector2.UnitX);
            Vector2 muzzle = owner.GetArmPos(arm) + aim * 24f;

            owner.ImpulseArm(arm, -aim * 3.5f);
            SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.42f, Pitch = 0.4f, MaxInstances = 3 }, muzzle);
            SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.3f, Pitch = 0.1f, MaxInstances = 3 }, muzzle);
            if (!Main.dedServ) {
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(muzzle,
                        aim.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(4f, 9f),
                        Color.Lerp(ScrapCommander.WeldOrange, Color.White, Main.rand.NextFloat(0.4f)),
                        Main.rand.NextFloat(0.6f, 1f))?.Configure(false, Main.rand.Next(8, 14));
                }
            }
            ShakeNearby(npc.Center, 0.8f, 900f);

            if (!VaultUtils.isClient) {
                int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.LaserPulseDamage);
                Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle,
                    aim * (ScrapDirector.LaserPulseSpeed * speedScale),
                    ModContent.ProjectileType<ScrapLaserPulse>(), damage, 2f, Main.myPlayer);
            }
        }
    }
}
