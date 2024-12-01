using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class SkyGlazeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "SkyGlaze";
        public override void SetDefaults() {
            Item.SetItemCopySD<SkyGlaze>();
            Item.SetHeldProj<SkyGlazeHeld>();
        }
    }

    internal class RSkyGlaze : BaseRItem
    {
        public override bool DrawingInfo => false;
        public override int TargetID => ModContent.ItemType<SkyGlaze>();
        public override int ProtogenesisID => ModContent.ItemType<SkyGlazeEcType>();
        public override void SetDefaults(Item item) => item.SetHeldProj<SkyGlazeHeld>();
    }

    internal class SkyGlazeHeld : BaseMagicStaff<SkyGlaze>
    {
        public override void SetStaffProperty() {
            HandFireDistance = 0;
        }
        public override void FiringShoot() {
            for (int i = 3; i > 0; i--) {
                int leftorright = (InMousePos - Owner.Center).X > 0 ? 1 : -1;
                Vector2 starpos = Owner.Center + new Vector2(Main.rand.NextFloat(-120, 240) * leftorright, Main.rand.NextFloat(-600, -800));
                Vector2 vel = (InMousePos - starpos).UnitVector() * 16f;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item8);
                Projectile.NewProjectile(Source, starpos, vel
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }
}
