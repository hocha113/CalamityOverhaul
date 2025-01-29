using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Mono.Cecil;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class MirrorBladeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "MirrorBlade";
        public override void SetDefaults() {
            Item.SetItemCopySD<MirrorBlade>();
            Item.UseSound = null;
            Item.SetKnifeHeld<MirrorBladeHeld>();
        }
    }

    internal class RMirrorBlade : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<MirrorBlade>();
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<MirrorBladeHeld>();
        }
    }

    internal class MirrorBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<MirrorBlade>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "BrinyBaron_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 30;
            distanceToOwner = 12;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = 50;
            Projectile.height = 50;
            Length = 54;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<MirrorBlast>()
                , Projectile.damage, Projectile.knockBack, Owner.whoAmI, 0f, 0f);
        }

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.3f
                , phase1SwingSpeed: 8.2f, phase2SwingSpeed: 6f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwnerUpdate();
        }
    }
}
