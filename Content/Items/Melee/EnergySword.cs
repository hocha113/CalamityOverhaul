using CalamityMod;
using CalamityMod.Items.Materials;
using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class EnergySword : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "EnergySword";
        public override void SetDefaults() {
            Item.height = 44;
            Item.width = 44;
            Item.damage = 12;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 20;
            Item.scale = 1;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.knockBack = 2.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(0, 0, 75, 0);
            Item.rare = ItemRarityID.Green;
            Item.shoot = ProjectileID.MiniRetinaLaser;
            Item.shootSpeed = 12f;
            Item.SetKnifeHeld<EnergySwordHeld>();
            Item.Calamity().UsesCharge = true;
            Item.Calamity().MaxCharge = 40;
        }

        public override bool CanUseItem(Player player) {
            Item.Calamity().Charge -= 0.12f;
            if (Item.Calamity().Charge < 0) {
                Item.Calamity().Charge = 0;
            }
            return base.CanUseItem(player);
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<DubiousPlating>(5).
                AddIngredient<MysteriousCircuitry>(4).
                AddRecipeGroup(CWRRecipes.TinBarGroup, 2).
                AddRecipeGroup(CWRRecipes.GoldBarGroup, 2).
                AddTile(TileID.Anvils).
                Register();
        }
    }

    internal class EnergySwordHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<EnergySword>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "Red_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 40;
            distanceToOwner = -22;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 46;
            Length = 46;
        }

        public override void Shoot() {
            if (Item.Calamity().Charge < 0.2f) {
                return;
            }

            int proj = Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ProjectileID.MiniRetinaLaser
                , Projectile.damage, Projectile.knockBack, Owner.whoAmI);
            Main.projectile[proj].DamageType = DamageClass.Melee;
            Main.projectile[proj].penetrate = 6;
            Main.projectile[proj].usesLocalNPCImmunity = true;
            Main.projectile[proj].localNPCHitCooldown = -1;
            Main.projectile[proj].netUpdate = true;
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.3f
                , phase1SwingSpeed: 8.2f, phase2SwingSpeed: 6f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }
    }
}
