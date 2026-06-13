using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>超载核心：球满后右键过载 90 帧增伤扩半径，超 60 帧炸膛反冲烧蓝</summary>
    internal sealed class OverloadCoreModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //圣焰金
        public override Color TintColor => new(255, 210, 110);

        /// <summary>超载读满所需帧数（此区间内为纯收益）</summary>
        private const int OverloadFrames = 90;
        /// <summary>红线后允许的危险帧数，超过即炸膛</summary>
        private const int DangerFrames = 60;

        private static readonly Color HolyCore = new(255, 240, 190);
        private static readonly Color HolyGlow = new(255, 180, 70);
        private static readonly Color DangerGlow = new(255, 70, 40);

        private int overloadTimer;
        private int baseDamage;
        private float baseRadiusMul;

        public override void Apply(ref ShootContext ctx) {
            ctx.ChargeTimeMul += -0.15f;
            ctx.OrbSpeedMul += 0.2f;
        }

        public override void OnOrbCharging(CyberChargeOrbProj orb, Player owner) {
            if (orb.ChargeRatio < 1f) {
                ResetState();
                return;
            }

            //进入超载：捕获基准值
            if (baseDamage == 0) {
                baseDamage = orb.Projectile.damage;
                baseRadiusMul = orb.ExplosionRadiusMul;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item113 with { Volume = 0.6f, Pitch = -0.2f }, orb.Projectile.Center);
                }
            }

            overloadTimer++;
            float overload = MathHelper.Clamp(overloadTimer / (float)OverloadFrames, 0f, 1f);
            orb.Projectile.damage = (int)(baseDamage * (1f + overload));
            orb.ExplosionRadiusMul = baseRadiusMul * (1f + overload * 0.35f);

            bool danger = overloadTimer > OverloadFrames;
            //圣火向心吸入：超载越深粒子越密
            if (Main.netMode != NetmodeID.Server) {
                int interval = danger ? 1 : (overload > 0.5f ? 2 : 3);
                if (overloadTimer % interval == 0) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 spawnPos = orb.Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(70f, 150f);
                    PRTLoader.NewParticle<PRT_CyberConverge>(spawnPos, Vector2.Zero,
                        danger ? DangerGlow : HolyCore, Main.rand.NextFloat(0.6f, 1.1f))
                        .Configure(orb.Projectile.Center, HolyGlow, Main.rand.Next(14, 24), 1f);
                }
                //节拍提示音：超载中音调持续走高，红线后变急促警报
                int tickInterval = danger ? 10 : 20;
                if (overloadTimer % tickInterval == 0) {
                    float pitch = danger ? 0.9f : MathHelper.Lerp(-0.1f, 0.7f, overload);
                    SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.35f, Pitch = pitch }, orb.Projectile.Center);
                }
                if (overloadTimer % 12 == 0) {
                    PRTLoader.NewParticle<PRT_StarPulseRing>(orb.Projectile.Center, Vector2.Zero,
                        (danger ? DangerGlow : HolyGlow) with { A = 0 }, 0.05f).Configure(0.05f, 0.3f + overload * 0.25f, 16);
                }
            }

            if (danger) {
                SHPCNaturalFx.Shake(MathHelper.Lerp(0.5f, 3f, (overloadTimer - OverloadFrames) / (float)DangerFrames));
                //炸膛判定
                if (overloadTimer > OverloadFrames + DangerFrames) {
                    Rupture(orb, owner);
                }
            }
        }

        /// <summary>炸膛：球口爆裂仍伤敌，玩家击退且法力减半</summary>
        private void Rupture(CyberChargeOrbProj orb, Player owner) {
            ResetState();
            if (orb.Projectile.owner != Main.myPlayer) return;

            int dmg = Math.Max(orb.Projectile.damage / 2, 1);
            int idx = Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                orb.Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 8f, orb.Projectile.owner, ai0: 1f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[2] = 200f;
            }

            //反冲与法力烧蚀
            if (owner != null && owner.active) {
                Vector2 push = (owner.Center - orb.Projectile.Center).SafeNormalize(-Vector2.UnitX);
                owner.velocity = push * 11f - Vector2.UnitY * 3f;
                owner.fallStart = (int)(owner.position.Y / 16f);
                owner.statMana /= 2;
                owner.manaRegenDelay = Math.Max(owner.manaRegenDelay, 60);
            }

            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.9f, Pitch = -0.3f }, orb.Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath3 with { Volume = 0.5f, Pitch = 0.4f }, orb.Projectile.Center);
                for (int i = 0; i < 22; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(9f, 9f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(orb.Projectile.Center, vel,
                        i % 3 == 0 ? DangerGlow : HolyGlow, Main.rand.NextFloat(0.9f, 1.8f))
                        .Configure(DangerGlow, Main.rand.Next(20, 38));
                }
            }
            SHPCNaturalFx.Shake(9f);
            orb.Projectile.Kill();
        }

        public override void OnOrbLaunched(CyberChargeOrbProj orb) {
            //超载发射：额外的金色爆发提示玩家这发被强化了
            if (overloadTimer > 0 && Main.netMode != NetmodeID.Server) {
                float overload = MathHelper.Clamp(overloadTimer / (float)OverloadFrames, 0f, 1f);
                SoundEngine.PlaySound(SoundID.Item68 with { Volume = 0.4f + overload * 0.4f, Pitch = 0.2f }, orb.Projectile.Center);
                int count = (int)(8 + overload * 14);
                for (int i = 0; i < count; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f) + orb.Projectile.velocity * 0.25f;
                    PRTLoader.NewParticle<PRT_CyberSquare>(orb.Projectile.Center, vel,
                        HolyCore, Main.rand.NextFloat(0.7f, 1.4f)).Configure(HolyGlow, Main.rand.Next(16, 30));
                }
            }
            ResetState();
        }

        public override void OnOrbKill(CyberChargeOrbProj orb, int timeLeft) => ResetState();

        private void ResetState() {
            overloadTimer = 0;
            baseDamage = 0;
            baseRadiusMul = 1f;
        }
    }
}
