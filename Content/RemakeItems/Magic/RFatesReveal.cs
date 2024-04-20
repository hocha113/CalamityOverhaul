using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RFatesReveal : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Magic.FatesReveal>();
        public override int ProtogenesisID => ModContent.ItemType<FatesRevealEcType>();
        public override string TargetToolTipItemName => "FatesRevealEcType";

        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override void SetDefaults(Item item) {
            item.damage = 56;
            item.DamageType = DamageClass.Magic;
            item.mana = 20;
            item.width = 80;
            item.height = 86;
            item.useTime = 35;
            item.useAnimation = 35;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.knockBack = 5.5f;
            item.UseSound = SoundID.Item20;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<FatesRevealHeldProj>();
            item.shootSpeed = 1f;
            item.value = CalamityGlobalItem.Rarity13BuyPrice;
            item.rare = ModContent.RarityType<PureGreen>();
            item.SetHeldProj<FatesRevealHeldProj>();
        }
    }
}
