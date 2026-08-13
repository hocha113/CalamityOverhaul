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
    /// <summary>共振反应堆，蓄力共振环，引爆时脚下冲击</summary>
    internal sealed class ResonanceReactorModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //共振翠绿青
        public override Color TintColor => new(80, 240, 200);

        private const int RingInterval = 45;
        private int _ringTimer;

        public override void Apply(ref ShootContext ctx) {
            ctx.ChargeTimeMul += 0.18f;
            ctx.OrbExplosionRadiusMul += 0.2f;
            ctx.ManaCostMul += 0.24f;
        }

        public override void OnOrbCharging(CyberChargeOrbProj orb, Player owner) {
            _ringTimer++;
            if (_ringTimer < RingInterval) return;
            _ringTimer = 0;
            if (Main.netMode == NetmodeID.Server) return;

            //薄锐谐振环为结构载体，替换旧的等角方粒喷雾
            PRTLoader.NewParticle<PRT_StarPulseRing>(orb.Projectile.Center, Vector2.Zero,
                new Color(140, 255, 220), 0.06f).Configure(0.06f, 0.6f, 24);
            //环上抖落碎屑，角向抖动+速差
            int count = 10;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4.2f, 7.5f);
                PRTLoader.NewParticle<PRT_CyberSquare>(orb.Projectile.Center + vel * 3f, vel,
                    new Color(140, 255, 220), Main.rand.NextFloat(0.7f, 1.4f))
                    .Configure(new Color(40, 200, 170), Main.rand.Next(16, 28));
            }
            //低频谐振闷响
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.28f, Pitch = -0.7f }, orb.Projectile.Center);
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            //脚下共振冲击，补近战空档
            if (orb.Projectile.owner != Main.myPlayer) return;
            Player owner = Main.player[orb.Projectile.owner];
            if (owner == null || !owner.active) return;
            int dmg = Math.Max(orb.Projectile.damage / 2, 1);
            //半径200px，ai2 走生成包同步
            Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                owner.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, orb.Projectile.owner, ai0: 0.3f, ai1: 0f, ai2: 200f);
        }
    }
}
