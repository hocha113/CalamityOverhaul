using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSurgeDriver : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.SurgeDriver>();
        public override int ProtogenesisID => ModContent.ItemType<SurgeDriverEcType>();
        public override void SetDefaults(Item item) {
            item.damage = 140;
            item.DamageType = DamageClass.Ranged;
            item.width = 164;
            item.height = 58;
            item.useTime = item.useAnimation = 20;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.channel = true;
            item.knockBack = 8f;
            item.value = CalamityGlobalItem.RarityVioletBuyPrice;
            item.rare = ModContent.RarityType<Violet>();
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<PrismEnergyBullet>();
            item.shootSpeed = 11f;
            item.useAmmo = AmmoID.Bullet;
            item.UseSound = CommonCalamitySounds.LaserCannonSound;
            item.SetHeldProj<SurgeDriverHeldProj>();
            item.CWR().HasCartridgeHolder = true;
            item.CWR().AmmoCapacity = 30;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "SurgeDriverEcType");
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }
    }
}
