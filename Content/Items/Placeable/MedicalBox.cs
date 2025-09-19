﻿using CalamityOverhaul.Content.Projectiles.AmmoBoxs;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class MedicalBox : ModItem
    {
        public override string Texture => CWRConstant.Item + "Placeable/MedicalBox";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.value = 890;
            Item.useTime = Item.useAnimation = 22;
            Item.consumable = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.LightRed;
            Item.value = Item.buyPrice(0, 0, 15, 25);
            Item.maxStack = 64;
            Item.SetHeldProj<MedicalBoxHeld>();
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 25)
                .AddIngredient(ItemID.LifeCrystal, 1)
                .AddIngredient(ItemID.HealingPotion, 5)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    internal class MedicalBoxHeld : BaseHeldBox
    {
        public override string Texture => CWRConstant.Item + "Placeable/MedicalBox";
        public override void SetBox() {
            TargetItemID = ModContent.ItemType<MedicalBox>();
            AmmoBoxID = ModContent.ProjectileType<MedicalBoxProj>();
            MaxCharge = 80;
            MaxBoxCount = 3;
        }
    }

    internal class MedicalBoxProj : BaseAmmoBox
    {
        public override string Texture => CWRConstant.Item + "Placeable/MedicalBox";
        public override bool CanClick(Item item) => true;
        public override void Preprocessing(Player player, Item item) { }
        public override bool ClickBehavior(Player player, CWRItem cwr) {
            _ = SoundEngine.PlaySound(SoundID.Item3, Projectile.Center);
            Owner.Heal(150);
            for (int i = 0; i < 30; i++) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.VampireHeal);
            }
            for (int i = 0; i < 6; i++) {
                Vector2 spanPos = Owner.position + new Vector2(Main.rand.Next(Owner.width), Main.rand.Next(Owner.height));
                Gore.NewGore(Projectile.FromObjectGetParent(), spanPos, new Vector2(0, -1), 331, 1);
            }
            return true;
        }
    }
}
