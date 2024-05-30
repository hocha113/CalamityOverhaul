using CalamityMod;
using CalamityMod.Items.Weapons.Rogue;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Items.Rogue.Extras
{
    internal class NeutronScythe : ModItem
    {
        public override bool IsLoadingEnabled(Mod mod) {
            if (!CWRServerConfig.Instance.AddExtrasContent) {
                return false;
            }
            return base.IsLoadingEnabled(mod);
        }
        public override string Texture => CWRConstant.Item + "Rogue/NeutronScythe";
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 18));
        }
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.damage = 322;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.shootSpeed = 6;
            Item.rare = ItemRarityID.LightPurple;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.DamageType = ModContent.GetInstance<RogueDamageClass>();
            Item.shoot = ModContent.ProjectileType<NeutronScytheHeld>();
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 22;
        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 26;
    }
}
