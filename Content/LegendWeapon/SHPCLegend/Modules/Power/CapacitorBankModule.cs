using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>
    /// 储能阵列（电容银行）：右键蓄力时把溢出能量存入电容；之后左键命中会逐格放电，
    /// 在目标处迸发小型储能脉冲，把蓄力的盈余转化为持续的额外打击。
    /// </summary>
    internal sealed class CapacitorBankModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //储能黄绿
        public override Color TintColor => new(200, 255, 80);

        private const int MaxCharge = 8;
        private int _capacitor;
        private int _chargeTick;

        public override void Apply(ref ShootContext ctx) {
            ctx.ChargeTimeMul += -0.32f;
            ctx.OrbSpeedMul += -0.12f;
            ctx.AttackSpeedMul += -0.06f;
        }

        public override void OnOrbCharging(CyberChargeOrbProj orb, Player owner) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            if (++_chargeTick < 12) return;
            _chargeTick = 0;
            _capacitor = Math.Min(_capacitor + 1, MaxCharge);
            if (Main.netMode != NetmodeID.Server) {
                Vector2 pos = orb.Projectile.Center + Main.rand.NextVector2Circular(orb.Projectile.width, orb.Projectile.height);
                PRTLoader.NewParticle<PRT_CyberSquare>(pos, (orb.Projectile.Center - pos) * 0.08f, new Color(220, 255, 120), Main.rand.NextFloat(0.4f, 0.8f)).Configure(new Color(150, 220, 40), Main.rand.Next(8, 14));
            }
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer || _capacitor <= 0) return;
            _capacitor--;
            int dmg = Math.Max((int)(beam.Projectile.damage * 0.4f), 1);
            int idx = Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                target.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, beam.Projectile.owner, ai0: 0.15f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[2] = 60f;
                Main.projectile[idx].usesLocalNPCImmunity = true;
                Main.projectile[idx].localNPCHitCooldown = -1;
            }
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.3f, Pitch = 0.5f }, target.Center);
            }
        }
    }
}
