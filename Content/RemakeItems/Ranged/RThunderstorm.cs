using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityMod.Projectiles.Magic;
using CalamityMod.Sounds;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RThunderstorm : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Magic.Thunderstorm>();
        public override int ProtogenesisID => ModContent.ItemType<ThunderstormEcType>();
        public override string TargetToolTipItemName => "ThunderstormEcType";
        public override void SetDefaults(Item item) {
            item.damage = 132;
            item.mana = 50;
            item.DamageType = DamageClass.Magic;
            item.width = 48;
            item.height = 22;
            item.useTime = 24;
            item.useAnimation = 24;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 2f;
            item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            item.rare = ModContent.RarityType<Turquoise>();
            item.UseSound = CommonCalamitySounds.PlasmaBlastSound;
            item.autoReuse = true;
            item.shootSpeed = 6f;
            item.shoot = ModContent.ProjectileType<ThunderstormShot>();
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<ThunderstormHeldProj>();
            item.CWR().Scope = true;
            CWRUtils.EasySetLocalTextNameOverride(item, "ThunderstormEcType");
        }
    }
}
