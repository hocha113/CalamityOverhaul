using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Marbles
{
    /// <summary>
    /// 瓶中大理石：双击方向进行水平滚动冲刺，冲刺期间获得无敌帧并拖出灰白石化残影
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
            => player.GetModPlayer<MarbleinaBottlePlayer>().Equipped = true;

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.MarbleBlock, 14)
                .AddIngredient(ItemID.Bottle)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 5)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class MarbleinaBottlePlayer : ModPlayer
    {
        public bool Equipped;
        private int dashDir;
        private int dashTimer;
        private int dashCooldown;
        private int rightTapTimer;
        private int leftTapTimer;

        private const int DashDuration = 18;
        private const int DashCooldownTime = 45;
        private const float DashVelocity = 11.5f;

        public override void ResetEffects() => Equipped = false;

        public override void PreUpdateMovement() {
            if (dashCooldown > 0) {
                dashCooldown--;
            }
            if (rightTapTimer > 0) {
                rightTapTimer--;
            }
            if (leftTapTimer > 0) {
                leftTapTimer--;
            }

            if (Equipped && dashTimer <= 0 && dashCooldown <= 0) {
                if (Player.controlRight && Player.releaseRight) {
                    if (rightTapTimer > 0) {
                        StartDash(1);
                    }
                    else {
                        rightTapTimer = 14;
                    }
                }
                if (Player.controlLeft && Player.releaseLeft) {
                    if (leftTapTimer > 0) {
                        StartDash(-1);
                    }
                    else {
                        leftTapTimer = 14;
                    }
                }
            }

            if (dashTimer > 0) {
                dashTimer--;
                float t = dashTimer / (float)DashDuration;
                Player.velocity.X = dashDir * DashVelocity * (0.4f + 0.6f * t);
                if (Math.Abs(Player.velocity.Y) < 0.1f || Player.velocity.Y > 0f) {
                    Player.velocity.Y *= 0.9f;
                }
                Player.GivePlayerImmuneState(4);

                if (!VaultUtils.isServer) {
                    Lighting.AddLight(Player.Center, GraniteMarbleVFX.MarbleDust.ToVector3() * 0.5f);
                    PRTLoader.NewParticle<PRT_Smoke>(Player.Center, new Vector2(-dashDir * Main.rand.NextFloat(1f, 3f), Main.rand.NextFloat(-1f, 1f))
                        , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.4f, 0.7f)).Configure(20, 0.7f, 0.04f);
                    if (Main.rand.NextBool(2)) {
                        PRTLoader.NewParticle<PRT_Sparkle>(Player.Center, Vector2.Zero
                            , GraniteMarbleVFX.MarbleCore, 0.5f).Configure(GraniteMarbleVFX.MarbleCore, 12, 0.2f, 0.5f);
                    }
                }
            }
        }

        private void StartDash(int dir) {
            dashDir = dir;
            dashTimer = DashDuration;
            dashCooldown = DashCooldownTime;
            rightTapTimer = 0;
            leftTapTimer = 0;
            Player.velocity.X = dir * DashVelocity;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.2f, Volume = 0.7f }, Player.Center);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(Player.Center, new Vector2(-dir * Main.rand.NextFloat(1f, 4f), Main.rand.NextFloat(-2f, 2f))
                        , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.4f, 0.7f)).Configure(22, 0.7f, 0.05f);
                }
            }
        }
    }
}
