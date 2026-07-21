using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Marbles
{
    /// <summary>瓶中大理石，沉重二段跳，按住跳可借升力</summary>
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

        //必须为0，原版维持钉 jumpSpeed≈5.01低于8.2冲量；升力改走 MarbleBalloonPlayer
        public override float GetDurationMultiplier(Player player) => 0f;

        public override bool CanStart(Player player) => !player.GetModPlayer<MarbleBalloonPlayer>().Slamming;

        public override void OnStarted(Player player, ref bool playSound) {
            playSound = false;
            player.velocity.Y = -DoubleJumpStrength * player.gravDir;
            player.GetModPlayer<MarbleBalloonPlayer>().StartBottleLift();

            if (VaultUtils.isServer) {
                return;
            }

            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = -0.45f, MaxInstances = 3 }, player.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.4f, Pitch = -0.6f, MaxInstances = 3 }, player.Center);

            if (CWRServerConfig.Instance.ScreenVibration && player.whoAmI == Main.myPlayer) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(player.Center
                    , Vector2.UnitY * player.gravDir, 2.5f, 5f, 8, 700f, "MarbleinaBottleJump"));
            }

            bool cloud = player.GetModPlayer<MarbleBalloonPlayer>().CloudJumpVariant;
            Vector2 feet = player.gravDir == 1f ? player.Bottom : player.Top;
            float up = -player.gravDir;

            const int ringCount = 12;
            for (int i = 0; i < ringCount; i++) {
                float ang = MathHelper.TwoPi * i / ringCount;
                Vector2 dir = new Vector2(MathF.Cos(ang), MathF.Sin(ang) * 0.35f);
                Vector2 vel = dir * Main.rand.NextFloat(2.2f, 3.4f) + Vector2.UnitY * up * 0.6f;
                PRTLoader.NewParticle<PRT_Smoke>(feet + dir * 10f, vel, GraniteMarbleVFX.MarbleDust
                    , Main.rand.NextFloat(0.38f, 0.58f)).Configure(24, 0.7f, 0.05f);
            }

            //云朵气球叠白云团
            if (cloud) {
                for (int i = 0; i < 6; i++) {
                    Vector2 puffPos = feet + new Vector2(Main.rand.NextFloat(-16f, 16f), up * Main.rand.NextFloat(0f, 8f));
                    Vector2 puffVel = new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), up * Main.rand.NextFloat(0.4f, 1.2f));
                    PRTLoader.NewParticle<PRT_Smoke>(puffPos, puffVel, Color.White
                        , Main.rand.NextFloat(0.65f, 1f)).Configure(32, 0.8f, 0.02f);
                }
            }

            int chips = cloud ? 7 : 4;
            for (int i = 0; i < chips; i++) {
                PRTLoader.NewParticle<PRT_MarbleChip>(feet + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f)
                    , new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), up * Main.rand.NextFloat(2.5f, 5f))
                    , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.7f)).Configure(Main.rand.Next(18, 28));
            }
        }
    }
}
