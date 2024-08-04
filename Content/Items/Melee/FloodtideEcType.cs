using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class FloodtideEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Floodtide";
        public override void SetDefaults() {
            Item.SetCalamitySD<Floodtide>();
            Item.SetKnifeHeld<FloodtideHeld>();
        }
    }

    internal class RFloodtide : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Floodtide>();
        public override int ProtogenesisID => ModContent.ItemType<FloodtideEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<FloodtideHeld>();
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class FloodtideHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Floodtide>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "Floodtide_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            drawTrailTopWidth = 30;
            distanceToOwner = 30;
            drawTrailBtommWidth = 50;
            SwingData.starArg = 66;
            SwingData.baseSwingSpeed = 3.5f;
            Projectile.width = Projectile.height = 66;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Length = 86;
            ShootSpeed = 18;
        }

        public override void PostInOwnerUpdare() {
            if (Main.rand.NextBool(5 * updateCount)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.FishronWings);
            }
        }

        public override void Shoot() {
            Vector2 velocity = ShootVelocity;
            for (int i = 0; i < 2; i++) {
                float SpeedX = velocity.X + Main.rand.Next(-20, 21) * 0.05f;
                float SpeedY = velocity.Y + Main.rand.Next(-20, 21) * 0.05f;
                Projectile.NewProjectile(Source, ShootSpanPos, new Vector2(SpeedX, SpeedY)
                    , ModContent.ProjectileType<FloodtideShark>(), Projectile.damage
                    , Projectile.knockBack, Owner.whoAmI, 0f, 0f);
            }
        }
    }
}
