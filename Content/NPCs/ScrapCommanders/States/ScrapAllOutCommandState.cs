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
    /// 总攻指令：目镜指挥扫光 → 军团三波齐射，本体每波补一发迫击。
    /// 编排成可读的三个波次，而不是糊脸弹墙
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.AllOutCommand, typeof(ScrapStateContext))]
    internal class ScrapAllOutCommandState : ScrapStateBase
    {
        public override string StateName => "AllOutCommand";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.AllOutCommand;

        private const int CommandPose = 30;
        private static readonly int[] WaveBeats = { 30, 58, 86 };
        private const int StateEnd = 116;

        private bool commanded;
        /// <summary>已下达的最高波号（单调闩）</summary>
        private int lastWave = -1;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            int t = (int)Timer;

            npc.velocity *= 0.94f;
            LeanByVelocity(npc, 0.08f);

            //炮臂全程举着迫击姿态
            float side = MathF.Sign(ctx.Target.Center.X - npc.Center.X);
            if (side == 0f) {
                side = 1f;
            }
            Vector2 aim = new(side * MathF.Cos(1.05f), -MathF.Sin(1.05f));
            ctx.Arms[ScrapCommander.ArmCannon] = new ArmDirective {
                Mode = ArmMode.Hold,
                Target = npc.Center + npc.velocity + new Vector2(side * 104f, 42f),
                Spring = 0.2f,
                Damping = 0.8f,
                UseRot = true,
                WantRot = aim.ToRotation() - MathHelper.PiOver2,
                RotRate = 0.22f,
            };

            //指挥红线：目镜到每台仆从的细实线，起手渐亮、波拍打闪
            float lineAlpha = t < CommandPose
                ? t / (float)CommandPose * 0.4f
                : 0.28f + (lastWave >= 0 && t - WaveBeats[Math.Max(lastWave, 0)] < 6 ? 0.4f : 0f);
            DrawCommandLines(ctx, npc, lineAlpha);

            if (t < CommandPose) {
                //==================== 指挥起手 ====================
                if (!commanded) {
                    commanded = true;
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.6f, Pitch = -0.45f, MaxInstances = 1 }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 2 }, npc.Center);
                }
                ctx.EyeScan = t / (float)CommandPose;
                Timer++;
                return null;
            }

            //==================== 三波齐射 ====================
            for (int wave = 0; wave < WaveBeats.Length; wave++) {
                if (t == WaveBeats[wave] && lastWave < wave) {
                    lastWave = wave;
                    FireWave(ctx, owner, aim, wave);
                }
            }

            Timer++;
            if (t >= StateEnd) {
                return EndAttack(ctx, 125);
            }
            return null;
        }

        /// <summary>指挥红线：目镜到每台在场仆从</summary>
        private static void DrawCommandLines(ScrapStateContext ctx, NPC npc, float alpha) {
            if (alpha < 0.04f) {
                return;
            }
            Vector2 eye = npc.Center + new Vector2(0f, 8f);
            int probeType = ModContent.NPCType<ScrapLegionProbe>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC probe = Main.npc[i];
                if (probe.active && probe.type == probeType && (int)probe.ai[0] == npc.whoAmI) {
                    Vector2 dir = (probe.Center - eye).SafeNormalize(Vector2.UnitX);
                    ctx.AddSolidBeam(eye, dir, Vector2.Distance(eye, probe.Center), alpha, 0.45f);
                }
            }
        }

        /// <summary>一波总攻：军团全员点射 + 本体一发迫击（生成全在权威端）</summary>
        private void FireWave(ScrapStateContext ctx, ScrapCommander owner, Vector2 mortarAim, int wave) {
            NPC npc = ctx.Npc;
            SoundEngine.PlaySound(SoundID.Item61 with { Volume = 0.7f, Pitch = -0.35f + wave * 0.08f, MaxInstances = 2 }, npc.Center);
            owner.ImpulseArm(ScrapCommander.ArmCannon, -mortarAim * 8f);
            owner.CannonHeat = 26;
            ShakeNearby(npc.Center, 1.6f);

            if (VaultUtils.isClient) {
                return;
            }
            Player target = ctx.Target;
            //军团齐射：每台仆从一发脉冲，带小散布
            int probeType = ModContent.NPCType<ScrapLegionProbe>();
            int pulseDamage = (int)npc.GetAttackDamage_ForProjectiles(26f, 22f);
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC probe = Main.npc[i];
                if (!probe.active || probe.type != probeType || (int)probe.ai[0] != npc.whoAmI) {
                    continue;
                }
                Vector2 pAim = (target.Center + target.velocity * 10f - probe.Center)
                    .SafeNormalize(Vector2.UnitX).RotatedBy(Main.rand.NextFloat(-0.08f, 0.08f));
                Projectile.NewProjectile(npc.GetSource_FromAI(), probe.Center + pAim * 14f, pAim * 21f,
                    ModContent.ProjectileType<ScrapLaserPulse>(), pulseDamage, 1f, Main.myPlayer);
            }

            //本体补一发迫击：波内落点交替左右
            Vector2 muzzle = owner.GetArmPos(ScrapCommander.ArmCannon) + mortarAim * 28f;
            Vector2 landing = target.Center + new Vector2((wave - 1) * 130f + target.velocity.X * 16f, 0f);
            Vector2 arc = ScrapMortarState.SolveArcVelocity(muzzle, landing);
            int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.MortarDamage);
            Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, arc,
                ModContent.ProjectileType<ScrapMortarShell>(), damage, 5f, Main.myPlayer);
        }
    }
}
