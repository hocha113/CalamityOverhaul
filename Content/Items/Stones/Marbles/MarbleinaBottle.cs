using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Marbles
{
    /// <summary>
    /// 瓶中大理石：额外赋予一段沉重二段跳
    /// </summary>
    internal class MarbleinaBottle : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 28;
            Item.accessory = true;
            Item.value = Item.sellPrice(0, 0, 55, 0);
            Item.rare = ItemRarityID.Green;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
            => player.GetJumpState<MarbleinaBottleJump>().Enable();

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Marble, 14)
                .AddIngredient(ItemID.Bottle)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 5)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class MarbleinaBottleJump : ExtraJump
    {
        private const float DoubleJumpStrength = 8.2f;

        public override Position GetDefaultPosition() => AfterBottleJumps;

        public override float GetDurationMultiplier(Player player) => 0f;

        public override bool CanStart(Player player) => !player.GetModPlayer<MarbleBalloonPlayer>().Slamming;

        public override void OnStarted(Player player, ref bool playSound) {
            playSound = false;
            player.velocity.Y = -DoubleJumpStrength * player.gravDir;
            if (VaultUtils.isServer) {
                return;
            }

            SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.2f }, player.Center);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(player.Bottom, Main.rand.NextVector2Circular(3f, 1f) + Vector2.UnitY
                    , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.35f, 0.6f)).Configure(22, 0.7f, 0.05f);
            }
        }
    }
}
