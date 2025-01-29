using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RGreatswordofJudgement : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<GreatswordofJudgement>();
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 78;
            Item.damage = 40;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 18;
            Item.useTurn = true;
            Item.knockBack = 7f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.height = 78;
            Item.value = CalamityGlobalItem.RarityPurpleBuyPrice;
            Item.rare = ItemRarityID.Red;
            Item.shoot = ModContent.ProjectileType<JudgementBeam>();
            Item.shootSpeed = 15f;
            Item.SetKnifeHeld<GreatswordofJudgementHeld>();
        }
    }

    internal class GreatswordofJudgementHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<GreatswordofJudgement>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "GreatswordofJudgement_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 56;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 30;
            drawTrailCount = 8;
            Length = 68;
            SwingData.baseSwingSpeed = 4.65f;
            SwingAIType = SwingAITypeEnum.UpAndDown;
        }

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.3f, phase1SwingSpeed: 4.2f, phase2SwingSpeed: 6f, phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwnerUpdate();
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity.UnitVector() * Item.shootSpeed
                    , ModContent.ProjectileType<JudgementBeam>(), Projectile.damage
                    , Projectile.knockBack, Owner.whoAmI);
        }
    }
}
