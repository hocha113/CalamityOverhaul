using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityMod.Rarities;
using CalamityMod;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTheMaelstrom : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.TheMaelstrom>();
        public override int ProtogenesisID => ModContent.ItemType<TheMaelstromEcType>();
        public override string TargetToolTipItemName => "TheMaelstromEcType";
        public override void SetDefaults(Item item) {
            item.damage = 530;
            item.width = 20;
            item.height = 12;
            item.useTime = 15;
            item.useAnimation = 15;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 3f;
            item.value = CalamityGlobalItem.Rarity13BuyPrice;
            item.rare = ModContent.RarityType<PureGreen>();
            item.noMelee = true;
            item.noUseGraphic = true;
            item.DamageType = DamageClass.Ranged;
            item.channel = true;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<MaelstromHoldout>();
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.Calamity().canFirePointBlankShots = true;
            item.Calamity().donorItem = true;
            item.CWR().heldProjType = ModContent.ProjectileType<TheMaelstromHeldProj>();
            item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
