using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RDragonRage : BaseRItem
    {
        private int Level;
        private int LevelAlt;
        public override int TargetID => ModContent.ItemType<DragonRage>();
        public override int ProtogenesisID => ModContent.ItemType<DragonRageEcType>();
        public override string TargetToolTipItemName => "DragonRageEcType";
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[ModContent.ItemType<DragonRage>()] = true;
        public override void SetDefaults(Item item) {
            item.width = 74;
            item.height = 74;
            item.value = Item.sellPrice(gold: 75);
            item.useStyle = ItemUseStyleID.Shoot;
            item.useAnimation = 32;
            item.useTime = 32;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.damage = 850;
            item.crit = 16;
            item.knockBack = 7.5f;
            item.noUseGraphic = true;
            item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            item.noMelee = true;
            item.channel = true;
            item.shootSpeed = 10f;
            item.shoot = ModContent.ProjectileType<DragonRageHeld>();
            item.rare = ModContent.RarityType<Violet>();
            LevelAlt = Level = 0;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return DragonRageEcType.ShootFunc(ref Level, ref LevelAlt, item, player, source, position, velocity, type, damage, knockback);
        }

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? UseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] == 0;
    }
}
