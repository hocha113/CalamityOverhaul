using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class TheBurningSkyHeld : BaseKnife
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TheBurningSky";
        public override string gradientTexturePath => CWRConstant.ColorBar + "DragonRage_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 40;
            distanceToOwner = 10;
            drawTrailBtommWidth = 30;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 90;
            Length = 90;
            unitOffsetDrawZkMode = 8;
            SwingDrawRotingOffset = MathHelper.ToRadians(12);
            autoSetShoot = true;
        }

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase1Ratio: 0.3f, phase0SwingSpeed: -0.1f
                , phase1SwingSpeed: 6.2f, phase2SwingSpeed: 4f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwnerUpdate();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Dragonfire>(), 180);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<Dragonfire>(), 180);
        }

        public override void Shoot() {
            SoundEngine.PlaySound(SoundID.Item70, Owner.Center);
            for (int i = 0; i < 9; ++i) {
                float randomSpeed = ShootSpeed * Main.rand.NextFloat(0.7f, 1.4f) / SwingMultiplication;
                CalamityUtils.ProjectileRain(Projectile.GetSource_FromAI(), InMousePos
                    , 290f, 130f, 850f, 1100f, randomSpeed, ShootD
                    , Projectile.damage / 6, 6f, Owner.whoAmI);
            }
        }
    }
}
