using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityMod.Rarities;
using CalamityMod;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RContagion : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Contagion>();
        public override int ProtogenesisID => ModContent.ItemType<ContagionEcType>();
        public override string TargetToolTipItemName => "ContagionEcType";
        public override void SetDefaults(Item item) {
            item.damage = 280;
            item.DamageType = DamageClass.Ranged;
            item.width = 22;
            item.height = 50;
            item.useTime = 20;
            item.useAnimation = 20;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.channel = true;
            item.knockBack = 5f;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<ContagionBow>();
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.UseSound = SoundID.Item5;
            item.value = CalamityGlobalItem.RarityHotPinkBuyPrice;
            item.rare = ModContent.RarityType<HotPink>();
            item.Calamity().devItem = true;
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().heldProjType = ModContent.ProjectileType<ContagionHeldProj>();
            item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
