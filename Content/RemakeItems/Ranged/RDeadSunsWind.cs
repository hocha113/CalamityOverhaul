using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityMod.Sounds;
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
    internal class RDeadSunsWind : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.DeadSunsWind>();
        public override int ProtogenesisID => ModContent.ItemType<DeadSunsWindEcType>();
        public override string TargetToolTipItemName => "DeadSunsWindEcType";
        public override void SetDefaults(Item item) {
            item.damage = 100;
            item.DamageType = DamageClass.Ranged;
            item.width = 70;
            item.height = 24;
            item.useTime = item.useAnimation = 22;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 3.5f;
            item.UseSound = DeadSunsWindEcType.UseShoot;
            item.value = CalamityGlobalItem.Rarity9BuyPrice;
            item.rare = ItemRarityID.Cyan;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<CosmicFire>();
            item.shootSpeed = 9f;
            item.useAmmo = AmmoID.Gel;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<DeadSunsWindHeldProj>();
            CWRUtils.EasySetLocalTextNameOverride(item, "DeadSunsWindEcType");
        }
    }
}
