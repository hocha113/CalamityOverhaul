using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Marbles
{
    /// <summary>
    /// 大理石气球：提升跳跃高度并额外赋予一段沉重二段跳；空中按↓砸地，落地产生大理石冲击波
    /// </summary>
    internal class MarbleBalloon : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 30;
            Item.accessory = true;
            Item.value = Item.sellPrice(0, 0, 55, 0);
            Item.rare = ItemRarityID.Green;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetModPlayer<MarbleBalloonPlayer>().Equipped = true;
            player.jumpSpeedBoost += 1.6f;
            player.noFallDmg = true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Marble, 16)
                .AddIngredient(ItemID.ShinyRedBalloon)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class MarbleBalloonPlayer : ModPlayer
    {
        public bool Equipped;
        private bool canDoubleJump;
        private bool slamming;
        private bool prevJump;
        private int slamTimer;

        private const float DoubleJumpStrength = 8.2f;
        private const float SlamSpeed = 19f;
        //砸地保险计时：即便迟迟未检测到落地，也强制结束下砸，杜绝"卡在下砸态"
        private const int MaxSlamTime = 90;

        public override void ResetEffects() => Equipped = false;

        public override void PreUpdateMovement() {
            if (!Equipped) {
                slamming = false;
                slamTimer = 0;
                prevJump = Player.controlJump;
                return;
            }

            bool grounded = Player.velocity.Y < 1f && !Player.mount.Active;
            if (grounded) {
                canDoubleJump = true;
            }

            //落地判定优先于一切：着地或保险超时即结束下砸并触发落地效果
            if (slamming && (grounded || slamTimer > MaxSlamTime)) {
                OnSlamLand();
                slamming = false;
                slamTimer = 0;
                canDoubleJump = true;
            }

            bool jumpPressed = Player.controlJump && !prevJump;

            //沉重二段跳
            if (!slamming && !grounded && jumpPressed && canDoubleJump && Player.jump == 0) {
                canDoubleJump = false;
                Player.velocity.Y = -DoubleJumpStrength;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.2f }, Player.Center);
                    for (int i = 0; i < 10; i++) {
                        PRTLoader.NewParticle<PRT_Smoke>(Player.Bottom, Main.rand.NextVector2Circular(3f, 1f) + Vector2.UnitY
                            , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.35f, 0.6f)).Configure(22, 0.7f, 0.05f);
                    }
                }
            }

            //空中按↓开始砸地
            if (!slamming && !grounded && Player.controlDown && Player.velocity.Y > -2f) {
                slamming = true;
                slamTimer = 0;
            }

            //砸地中：强制下坠并清除"按↓"输入，避免穿过平台导致永远落不了地
            if (slamming) {
                slamTimer++;
                Player.velocity.Y = SlamSpeed;
                Player.controlDown = false;
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_Smoke>(Player.Center, Vector2.UnitY * -1f
                        , GraniteMarbleVFX.MarbleDust, 0.4f).Configure(16, 0.6f, 0.05f);
                }
            }

            prevJump = Player.controlJump;
        }

        private void OnSlamLand() {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f }, Player.Bottom);
                for (int i = 0; i < 14; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(Player.Bottom, new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-3f, 0f))
                        , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.4f, 0.7f)).Configure(26, 0.7f, 0.05f);
                }
            }

            if (CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(Player.Bottom, Main.rand.NextVector2Unit()
                    , 6f, 6f, 14, 800f, "MarbleBalloonSlam"));
            }

            if (Player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(Player.FromObjectGetParent(), Player.Bottom, Vector2.Zero
                    , ModContent.ProjectileType<MarbleShockwave>(), 30, 5f, Player.whoAmI, 0f, 135f);
            }
        }
    }
}
