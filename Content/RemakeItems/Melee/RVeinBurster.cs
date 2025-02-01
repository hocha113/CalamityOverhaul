using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RVeinBurster : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<VeinBurster>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<VeinBursterHeld>();
    }

    internal class VeinBursterHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<VeinBurster>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "BloodRed_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 36;
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 3f;
            drawTrailBtommWidth = 30;
            distanceToOwner = 20;
            drawTrailTopWidth = 24;
            drawTrailCount = 8;
            Length = 50;
            ShootSpeed = 16;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<BloodBall>(), Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<BurningBlood>(), 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<BurningBlood>(), 300);
        }
    }
}
