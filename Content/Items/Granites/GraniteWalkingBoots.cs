using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Granites
{
    /// <summary>
    /// 花岗行走靴：显著提升跑速与加速度，全速奔跑时脚下迸发蓝色电火花残影
    /// </summary>
    internal class GraniteWalkingBoots : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 30;
            Item.accessory = true;
            Item.value = Item.sellPrice(0, 0, 60, 0);
            Item.rare = ItemRarityID.Green;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetModPlayer<GraniteWalkingBootsPlayer>().Equipped = true;
            player.moveSpeed += 0.12f;
            player.accRunSpeed = Math.Max(player.accRunSpeed, 6.85f);
            player.runAcceleration *= 1.7f;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Granite, 16)
                .AddIngredient(ItemID.Aglet)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 6)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class GraniteWalkingBootsPlayer : ModPlayer
    {
        public bool Equipped;

        public override void ResetEffects() => Equipped = false;

        public override void PostUpdate() {
            if (!Equipped || VaultUtils.isServer) {
                return;
            }

            bool grounded = Player.velocity.Y == 0f && !Player.mount.Active;
            bool fullSpeed = Math.Abs(Player.velocity.X) >= Player.accRunSpeed * 0.85f;
            if (!grounded || !fullSpeed) {
                return;
            }

            Vector2 feet = Player.Bottom + new Vector2(0f, -4f);
            Lighting.AddLight(feet, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.7f);

            Vector2 back = new Vector2(-Math.Sign(Player.velocity.X), 0f);
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Spark>(feet + new Vector2(Main.rand.Next(-6, 6), 2f)
                    , back * Main.rand.NextFloat(2f, 5f) + new Vector2(0f, Main.rand.NextFloat(-2f, -0.5f))
                    , GraniteMarbleVFX.GraniteSpark, Main.rand.NextFloat(0.5f, 0.9f)).Configure(false, Main.rand.Next(10, 18));
            }
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Light>(feet + new Vector2(Main.rand.Next(-8, 8), 0f)
                    , back * Main.rand.NextFloat(1f, 3f), GraniteMarbleVFX.GraniteCore
                    , Main.rand.NextFloat(0.25f, 0.45f)).Configure(14, 1f, 1.2f);
            }
        }
    }
}
