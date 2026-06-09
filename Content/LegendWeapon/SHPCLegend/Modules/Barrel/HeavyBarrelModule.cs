using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 重型枪管（重炮蓄势）：极低射速换取沉重单发。每束光束在飞行尽头或命中后炸裂出一圈重型冲击环，
    /// 把单发的分量延伸成范围打击。
    /// </summary>
    internal sealed class HeavyBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //重型炮管赤红
        public override Color TintColor => new(220, 40, 60);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += 0.36f;
            ctx.AttackSpeedMul += -0.75f;
            ctx.SpreadMul += -0.4f;
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            int dmg = Math.Max((int)(beam.Projectile.damage * 0.8f), 1);
            int idx = Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, beam.Projectile.knockBack, beam.Projectile.owner, ai0: 0.25f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                //重炮冲击环：90px
                Main.projectile[idx].localAI[2] = 90f;
                Main.projectile[idx].usesLocalNPCImmunity = true;
                Main.projectile[idx].localNPCHitCooldown = -1;
            }
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = -0.4f }, beam.Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(beam.Projectile.Center, vel, new Color(255, 90, 90), Main.rand.NextFloat(0.6f, 1.2f)).Configure(new Color(150, 20, 30), Main.rand.Next(12, 22));
                }
            }
        }
    }
}
