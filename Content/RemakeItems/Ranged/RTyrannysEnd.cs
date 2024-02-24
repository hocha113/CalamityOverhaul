using CalamityMod.Items;
using CalamityMod.Projectiles.Magic;
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
using CalamityMod;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Common;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTyrannysEnd : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.TyrannysEnd>();
        public override int ProtogenesisID => ModContent.ItemType<TyrannysEnd>();
        public override void SetDefaults(Item item) {
            item.damage = 2000;
            item.knockBack = 9.5f;
            item.DamageType = DamageClass.Ranged;
            item.useTime = 60;
            item.useAnimation = 60;
            item.shoot = ProjectileID.BulletHighVelocity;
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Bullet;
            item.autoReuse = true;
            item.width = 150;
            item.height = 48;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.UseSound = CommonCalamitySounds.LargeWeaponFireSound;
            item.value = CalamityGlobalItem.Rarity15BuyPrice;
            item.rare = ModContent.RarityType<Violet>();
            item.Calamity().donorItem = true;
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<TyrannysEndHeldProj>();
        }
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "TyrannysEnd");
            tooltips.ReplaceTooltip("[KEY]", CWRKeySystem.ADS_Key.TooltipHotkeyString(), CWRMod.Instance.Name);
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
    }
}
