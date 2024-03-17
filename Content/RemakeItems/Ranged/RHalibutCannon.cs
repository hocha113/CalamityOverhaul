using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RHalibutCannon : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.HalibutCannon>();
        public override int ProtogenesisID => ModContent.ItemType<HalibutCannonEcType>();
        public override string TargetToolTipItemName => "HalibutCannonEcType";
        public override void SetDefaults(Item item) {
            item.damage = 50;
            item.DamageType = DamageClass.Ranged;
            item.width = 118;
            item.height = 56;
            item.useTime = 10;
            item.useAnimation = 20;
            item.useStyle = ItemUseStyleID.Shoot;
            item.rare = ModContent.RarityType<HotPink>();
            item.noMelee = true;
            item.knockBack = 1f;
            item.value = CalamityGlobalItem.Rarity16BuyPrice;
            item.UseSound = SoundID.Item38;
            item.autoReuse = true;
            item.shoot = ProjectileID.Bullet;
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<HalibutCannonHeldProj>();
        }
    }
}
