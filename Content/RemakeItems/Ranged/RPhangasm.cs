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
    internal class RPhangasm : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Phangasm>();
        public override int ProtogenesisID => ModContent.ItemType<Phangasm>();
        public override void SetDefaults(Item item) {
            item.damage = 160;
            item.width = 48;
            item.height = 82;
            item.useTime = 12;
            item.useAnimation = 12;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 3f;
            item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            item.UseSound = SoundID.Item5;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.DamageType = DamageClass.Ranged;
            item.channel = true;
            item.autoReuse = true;
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.rare = ModContent.RarityType<DarkBlue>();
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().heldProjType = ModContent.ProjectileType<PhangasmBowHeldProj>();
            item.CWR().hasHeldNoCanUseBool = true;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "Phangasm");

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
    }
}
