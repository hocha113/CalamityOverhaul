using CalamityMod.Items;
using CalamityMod;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Content.Items.Melee;
using System.Collections.Generic;
using Terraria.Localization;
using System.Security.Policy;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RSubmarineShocker : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.SubmarineShocker>();
        public override int ProtogenesisID => ModContent.ItemType<SubmarineShockerEcType>();
        public override string TargetToolTipItemName => "SubmarineShockerEcType";

        public override void SetDefaults(Item item) {
            item.width = 32;
            item.height = 32;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.autoReuse = true;
            item.useStyle = ItemUseStyleID.Rapier;
            item.damage = 90;
            item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            item.useAnimation = item.useTime = 10;
            item.shoot = ModContent.ProjectileType<RSubmarineShockerProj>();
            item.shootSpeed = 2f;
            item.knockBack = 7f;
            item.UseSound = SoundID.Item1;
            item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            item.rare = ItemRarityID.Pink;
            item.EasySetLocalTextNameOverride("SubmarineShockerEcType");
        }

    }
}
