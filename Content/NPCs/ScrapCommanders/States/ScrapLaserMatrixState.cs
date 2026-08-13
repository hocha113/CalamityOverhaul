using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.States
{
    /// <summary>
    /// 镭射矩阵（P2 组合招，需军团在场）：统帅目镜 + 全体仆从的瞄准线
    /// 追着玩家收束 → 末段冻结成交叉网格（最后的走位窗）→ 齐射快脉冲。
    /// 网格线本身就是预警——线越亮离齐射越近
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.LaserMatrix, typeof(ScrapStateContext))]
    internal class ScrapLaserMatrixState : ScrapStateBase
    {
        public override string StateName => "LaserMatrix";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.LaserMatrix;

        //==================== 时序 ====================

        private const int TrackEnd = 60;
        private const int FreezeEnd = 80;
        private const int StateEnd = 100;

        private bool commanded;
        private bool frozen;
        private bool fired;
        /// <summary>冻结的射击表（各端本地冻各自的视图，齐射用权威端的）</summary>
        private readonly List<(Vector2 From, Vector2 Dir)> frozenAims = new(6);

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            int t = (int)Timer;

            npc.velocity *= 0.93f;
            LeanByVelocity(npc, 0.08f);

            if (t == 0 && ctx.Owner.TargetInvalid()) {
                return EndAttack(ctx, 45);
            }
            if (!commanded) {
                commanded = true;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 1 }, npc.Center);
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 2 }, npc.Center);
            }
            ctx.EyeScan = (t % 20) / 20f;

            //镭射臂持位
            Vector2 armAim = (ctx.Target.Center - owner.GetArmPos(ScrapCommander.ArmLaser))
                .SafeNormalize(Vector2.UnitX);
            ctx.Arms[ScrapCommander.ArmLaser] = new ArmDirective {
                Mode = ArmMode.Hold,
                Target = npc.Center + npc.velocity + new Vector2(System.MathF.Sign(armAim.X) * 122f, -4f),
                Spring = 0.2f,
                Damping = 0.78f,
                UseRot = true,
                WantRot = armAim.ToRotation() - MathHelper.PiOver2,
                RotRate = 0.35f,
            };

            if (t < TrackEnd) {
                //==================== 追踪收束：活线咬着玩家走 ====================
                float alpha = 0.3f + t / (float)TrackEnd * 0.3f;
                foreach ((Vector2 from, Vector2 dir) in EnumerateShooters(ctx, npc, owner)) {
                    ctx.AddSolidBeam(from, dir, 1100f, alpha, 0.35f);
                }
                if (t % 20 == 6) {
                    SoundEngine.PlaySound(SoundID.Item15 with {
                        Volume = 0.3f,
                        Pitch = -0.3f + t / (float)TrackEnd * 0.6f,
                        MaxInstances = 2
                    }, npc.Center);
                }
                Timer++;
                return null;
            }

            if (t < FreezeEnd) {
                //==================== 网格冻结：最后的走位窗 ====================
                if (!frozen) {
                    frozen = true;
                    frozenAims.Clear();
                    frozenAims.AddRange(EnumerateShooters(ctx, npc, owner));
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.6f, Pitch = 0.45f, MaxInstances = 2 }, npc.Center);
                }
                float blink = 0.5f + 0.5f * ((t - TrackEnd) / (float)(FreezeEnd - TrackEnd));
                for (int i = 0; i < frozenAims.Count; i++) {
                    ctx.AddSolidBeam(frozenAims[i].From, frozenAims[i].Dir, 1100f, blink * 0.85f, 0.6f);
                }
                Timer++;
                return null;
            }

            //==================== 齐射 ====================
            if (!fired) {
                fired = true;
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.75f, Pitch = -0.2f, MaxInstances = 2 }, npc.Center);
                ShakeNearby(npc.Center, 3f);
                owner.LaserFlash = 5;
                if (!VaultUtils.isClient) {
                    int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.LaserPulseDamage) + 4;
                    foreach ((Vector2 from, Vector2 dir) in frozenAims) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), from + dir * 20f, dir * 30f,
                            ModContent.ProjectileType<ScrapLaserPulse>(), damage, 2f, Main.myPlayer);
                    }
                }
            }
            //齐射后线残光速褪
            float linger = MathHelper.Clamp((StateEnd - t) / 14f, 0f, 0.4f);
            for (int i = 0; i < frozenAims.Count; i++) {
                ctx.AddSolidBeam(frozenAims[i].From, frozenAims[i].Dir, 1100f, linger, 0.9f);
            }

            Timer++;
            if (t >= StateEnd) {
                return EndAttack(ctx, 90);
            }
            return null;
        }

        /// <summary>射击表：统帅镭射臂 + 全体在场仆从，各自指向玩家预测点</summary>
        private static IEnumerable<(Vector2 From, Vector2 Dir)> EnumerateShooters(
            ScrapStateContext ctx, NPC npc, ScrapCommander owner) {
            Vector2 aimPos = ctx.Target.Center + ctx.Target.velocity * 6f;
            Vector2 armFrom = owner.GetArmPos(ScrapCommander.ArmLaser);
            yield return (armFrom, (aimPos - armFrom).SafeNormalize(Vector2.UnitX));

            int probeType = ModContent.NPCType<ScrapLegionProbe>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC probe = Main.npc[i];
                if (probe.active && probe.type == probeType && (int)probe.ai[0] == npc.whoAmI) {
                    yield return (probe.Center, (aimPos - probe.Center).SafeNormalize(Vector2.UnitX));
                }
            }
        }
    }
}
