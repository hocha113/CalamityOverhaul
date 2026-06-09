using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>
    /// 超载核心（普罗维登斯）：神圣过热，可控失控。
    /// 蓄力越久热量越高；高热发射的能量球飞行时持续泄出圣焰，引爆时追加一圈过载裂变。
    /// 若满蓄后仍长时间持有，会在能量球附近提前释放不稳定环爆（对玩家无害）。
    /// 全程复用共享的 CyberDetonationProj，热量状态由模块按弹幕 whoAmI 私有持有。
    /// </summary>
    internal sealed class OverloadCoreModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //过载电浆紫
        public override Color TintColor => new(180, 80, 255);

        private const int FullFrames = 110;
        private const int OverheatFrames = 150;
        private const float HotThreshold = 0.55f;

        private int _chargingOrb = -1;
        private int _chargeFrames;
        private readonly Dictionary<int, float> _heat = new();

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbSpeedMul += 0.36f;
            ctx.ChargeTimeMul += -0.2f;
        }

        public override void OnOrbCharging(CyberChargeOrbProj orb, Player owner) {
            if (_chargingOrb != orb.Projectile.whoAmI) {
                _chargingOrb = orb.Projectile.whoAmI;
                _chargeFrames = 0;
            }
            _chargeFrames++;

            //满蓄后仍持有：周期性提前释放不稳定环爆
            if (_chargeFrames >= OverheatFrames && (_chargeFrames - OverheatFrames) % 45 == 0
                && orb.Projectile.owner == Main.myPlayer) {
                int dmg = Math.Max(orb.Projectile.damage / 3, 1);
                int idx = Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                    orb.Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<CyberDetonationProj>(),
                    dmg, 0f, orb.Projectile.owner, ai0: 0.6f);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].localAI[2] = 175f;
                }
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, Pitch = -0.2f }, orb.Projectile.Center);
                    SpawnHolySparks(orb.Projectile.Center, 14);
                }
                SHPCNaturalFx.Shake(3.5f);
            }
        }

        public override void OnOrbLaunched(CyberChargeOrbProj orb) {
            float heat = MathHelper.Clamp(_chargeFrames / (float)FullFrames, 0f, 1f);
            _heat[orb.Projectile.whoAmI] = heat;
            _chargeFrames = 0;
            _chargingOrb = -1;
        }

        public override void OnOrbFlyingAI(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            if (!_heat.TryGetValue(orb.Projectile.whoAmI, out float heat) || heat < HotThreshold) return;
            int frame = (int)Main.GameUpdateCount + orb.Projectile.whoAmI;
            if (frame % 9 != 0) return;
            //泄出圣焰电弧：沿轨迹两侧的小型爆裂
            Vector2 perp = orb.Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Vector2 pos = orb.Projectile.Center + perp * Main.rand.NextFloat(-26f, 26f);
            int dmg = Math.Max(orb.Projectile.damage / 5, 1);
            int idx = Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                pos, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, orb.Projectile.owner, ai0: 0.3f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[2] = 55f;
                Main.projectile[idx].usesLocalNPCImmunity = true;
                Main.projectile[idx].localNPCHitCooldown = 20;
            }
            if (Main.netMode != NetmodeID.Server) {
                SpawnHolySparks(pos, 3);
            }
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (!_heat.TryGetValue(orb.Projectile.whoAmI, out float heat)) return;
            _heat.Remove(orb.Projectile.whoAmI);
            if (heat < HotThreshold || orb.Projectile.owner != Main.myPlayer) return;
            //高热引爆追加一圈过载裂变
            int dmg = Math.Max(orb.Projectile.damage, 1);
            int idx = Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                orb.Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, orb.Projectile.owner, ai0: 0.95f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[2] = MathHelper.Lerp(220f, 320f, heat);
            }
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.6f, Pitch = -0.3f }, orb.Projectile.Center);
                SpawnHolySparks(orb.Projectile.Center, 26);
            }
            SHPCNaturalFx.Shake(6f);
        }

        public override void OnOrbKill(CyberChargeOrbProj orb, int timeLeft) {
            _heat.Remove(orb.Projectile.whoAmI);
        }

        private static void SpawnHolySparks(Vector2 center, int count) {
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f) * Main.rand.NextFloat(0.5f, 1.2f);
                Color main = Main.rand.NextBool() ? new Color(255, 230, 150) : new Color(200, 110, 255);
                PRTLoader.NewParticle<PRT_CyberSquare>(center, vel, main, Main.rand.NextFloat(0.7f, 1.6f)).Configure(new Color(150, 60, 220), Main.rand.Next(14, 28));
            }
        }
    }
}
