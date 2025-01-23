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
    /// <summary>
    /// 风暴管束者
    /// </summary>
    internal class StormRulerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "StormRuler";
        public override void SetDefaults() {
            Item.SetItemCopySD<StormRuler>();
            Item.UseSound = null;
            Item.SetKnifeHeld<StormRulerHeld>();
        }
    }

    internal class RStormRuler : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<StormRuler>();
        public override int ProtogenesisID => ModContent.ItemType<StormRulerEcType>();
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<StormRulerHeld>();
        }
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class StormRulerHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<StormRuler>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "AbsoluteZero_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 70;
            drawTrailTopWidth = 20;
            drawTrailCount = 6;
            Length = 82;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 3.65f;
            ShootSpeed = 20;
            SwingAIType = SwingAITypeEnum.UpAndDown;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<StormRulerProj>()
                , Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }

        public override bool PreInOwnerUpdate() {
            if (Main.rand.NextBool(5 * UpdateRate)) {
                int swingDust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , DustID.Flare_Blue, Owner.direction * 2, 0f, 150, default, 1.3f);
                Main.dust[swingDust].velocity *= 0.2f;
            }
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.3f, phase1SwingSpeed: 6.2f, phase2SwingSpeed: 4f);
            return base.PreInOwnerUpdate();
        }
    }
}
