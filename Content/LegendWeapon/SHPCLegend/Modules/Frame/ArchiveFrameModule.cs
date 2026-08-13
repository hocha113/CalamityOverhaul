using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>归档机匣，累积命中伤达阈值释归档爆破</summary>
    internal sealed class ArchiveFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //归档琥珀金
        public override Color TintColor => new(255, 200, 100);

        private static readonly Color ArchiveAmber = new(255, 200, 100);
        private static readonly Color ArchiveDim = new(160, 110, 35);

        private const float Threshold = 6000f;
        private const int CooldownFrames = 120;

        private float _accumulated;
        private int _cooldown;
        private float _cooldownCarry;

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += 0.06f;
            ctx.ManaCostMul += 0.18f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            Accumulate(beam.Projectile, damageDone);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            //激光半额计入
            Accumulate(laser.Projectile, damageDone / 2);
        }

        private void Accumulate(Projectile source, int damageDone) {
            if (source.owner != Main.myPlayer) return;
            if (damageDone <= 0) return;
            _accumulated += damageDone;
            if (_cooldown > 0 || _accumulated < Threshold) return;

            //达阈值扣额并在玩家处爆破
            _accumulated -= Threshold;
            _cooldown = CooldownFrames;
            _cooldownCarry = 0f;
            Player owner = Main.player[source.owner];
            if (owner == null || !owner.active) return;

            int dmg = Math.Max((int)(Threshold * 0.25f), 1);
            //半径300px，ai2 走生成包同步
            Projectile.NewProjectile(source.GetSource_FromThis(),
                owner.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, source.owner, ai0: 0.7f, ai1: 0f, ai2: 300f);
            SpawnReleaseBurst(owner);
            if (source.owner == Main.myPlayer) {
                CombatText.NewText(owner.getRect(), new Color(255, 200, 60),
                    "// ARCHIVE", true, false);
            }
        }

        /// <summary>释放拍，玩家处琥珀环+数据方粒外泄，拥有者端；爆炸本体走共享弹幕</summary>
        private static void SpawnReleaseBurst(Player owner) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item114 with { Volume = 0.32f, Pitch = -0.25f, MaxInstances = 2 }, owner.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(owner.Center, Vector2.Zero, ArchiveAmber, 0.06f)
                .Configure(0.06f, 0.5f, 20);
            for (int k = 0; k < 10; k++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.5f, 3.5f) - Vector2.UnitY * 1.2f;
                PRTLoader.NewParticle<PRT_CyberSquare>(owner.Center + vel * 3f, vel,
                    ArchiveAmber, Main.rand.NextFloat(0.5f, 1.0f)).Configure(ArchiveDim, Main.rand.Next(14, 24));
            }
        }

        public override void OnPlayerUpdate(Player player) {
            TickDown(ref _cooldown, ref _cooldownCarry);
        }
    }
}
