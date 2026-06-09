using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>
    /// 狙击瞄具（超距狙击）：大幅提升弹速射程，牺牲追踪与攻速。命中越远的目标越能积累瞄准环，
    /// 环满后下一次远距命中会射出一道贯穿长枪；近距离则无法积累，逼迫玩家拉开身位。
    /// </summary>
    internal sealed class SniperOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //狙击冷白
        public override Color TintColor => new(220, 240, 255);

        private const float FarRange = 760f;
        private const float NearRange = 360f;
        private const int RingFull = 5;
        private int _aim;

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSpeedMul += 1f;
            ctx.BeamLifeMul += 0.64f;
            ctx.DamageMul += 0.24f;
            ctx.AttackSpeedMul += -0.54f;
            ctx.HomingMul += -1f;
            ctx.SpreadMul += -1f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            Player p = Main.player[beam.Projectile.owner];
            float dist = Vector2.Distance(p.Center, target.Center);
            if (dist < NearRange) {
                //贴脸时无法维持超距瞄准
                _aim = Math.Max(_aim - 1, 0);
                return;
            }
            if (dist < FarRange) return;

            _aim = Math.Min(_aim + 1, RingFull);
            if (Main.netMode != NetmodeID.Server) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, new Color(200, 230, 255, 0), 0.05f).Configure(0.4f - _aim * 0.05f, 0.12f, 12);
            }
            if (_aim < RingFull) return;

            _aim = 0;
            FireSniper(beam.Projectile, p.Center, target.Center);
        }

        private static void FireSniper(Projectile src, Vector2 from, Vector2 to) {
            Vector2 dir = (to - from).SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(src.GetSource_FromThis(), from, dir * 34f,
                ModContent.ProjectileType<SHPCPrecisionLanceProj>(),
                Math.Max((int)(src.damage * 1.5f), 1), 2f, src.owner);
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item40 with { Volume = 0.6f, Pitch = -0.2f }, from);
            }
            SHPCNaturalFx.Shake(3f);
        }
    }
}
