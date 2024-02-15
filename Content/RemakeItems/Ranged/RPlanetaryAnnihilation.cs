using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityMod.Projectiles.Ranged;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RPlanetaryAnnihilation : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.PlanetaryAnnihilation>();
        public override int ProtogenesisID => ModContent.ItemType<PlanetaryAnnihilation>();
        public override void SetDefaults(Item item) {
            item.damage = 66;
            item.DamageType = DamageClass.Ranged;
            item.width = 58;
            item.height = 102;
            item.useTime = 22;
            item.useAnimation = 22;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 5.5f;
            item.value = CalamityGlobalItem.Rarity11BuyPrice;
            item.rare = ItemRarityID.Purple;
            item.UseSound = SoundID.Item75;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<PlanetaryAnnihilationProj>();
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Arrow;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<PlanetaryAnnihilationHeldProj>();
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "PlanetaryAnnihilation");

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
    }
}
