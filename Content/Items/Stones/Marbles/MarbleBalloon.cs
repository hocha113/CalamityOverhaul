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
    /// <summary>大理石气球，空中↓砸地，蓄势越久冲击越大</summary>
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
            player.noFallDmg = true;
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player)
            => CanEquipWithBalloon(equippedItem, incomingItem);

        internal static bool CanEquipWithBalloon(Item equippedItem, Item incomingItem)
            => !(IsMarbleBalloon(equippedItem) && IsBalloon(incomingItem))
                && !(IsMarbleBalloon(incomingItem) && IsBalloon(equippedItem));

        private static bool IsMarbleBalloon(Item item)
            => item.type == ModContent.ItemType<MarbleBalloon>()
                || item.type == ModContent.ItemType<MarbleCloudBalloon>();

        private static bool IsBalloon(Item item) => item.balloonSlot > 0 || IsMarbleBalloon(item);

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Marble, 16)
                .AddIngredient(ItemID.ShinyRedBalloon)
                .AddIngredient(ItemID.LuckyHorseshoe)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class MarbleBalloonPlayer : ModPlayer
    {
        public bool Equipped;
        //云朵气球，二段跳粒子双份
        public bool CloudJumpVariant;
        private bool slamming;
        private int slamTimer;
        //升力窗口剩余tick
        private int bottleLiftTimer;
        //尘土波推进，>0 逐tick双向铺开
        private int dustWaveStep;
        private int dustWaveMaxStep;
        private float dustWaveGrowth;
        private float dustWaveX;
        private float dustWaveLeftY;
        private float dustWaveRightY;

        private const float SlamSpeed = 19f;
        //砸地保险，超时强制结束
        private const int MaxSlamTime = 90;
        //冲击封顶下坠时长，伤20→48、半径135→170
        private const int GrowthCap = 28;
        //升力窗口，约多抬一格
        private const int BottleLiftWindow = 12;

        public bool Slamming => slamming;

        public override void ResetEffects() {
            Equipped = false;
            CloudJumpVariant = false;
        }

        /// <summary>二段跳起跳开升力窗</summary>
        public void StartBottleLift() => bottleLiftTimer = BottleLiftWindow;

        //PreUpdateMovement 前 controlDown 已进 fallThrough，须在 SetControls 清↓才能停平台
        public override void SetControls() {
            if (slamming) {
                Player.controlDown = false;
            }
        }

        public override void PreUpdateMovement() {
            UpdateBottleLift();
            UpdateGroundDustWave();

            if (!Equipped) {
                slamming = false;
                slamTimer = 0;
                return;
            }

            bool grounded = GraniteMarbleVFX.IsGrounded(Player);

            //着地或保险超时→落地
            if (slamming && (grounded || slamTimer > MaxSlamTime)) {
                OnSlamLand();
                slamming = false;
                slamTimer = 0;
            }

            //空中↓开砸
            if (!slamming && !grounded && Player.controlDown && Player.velocity.Y * Player.gravDir > -2f) {
                slamming = true;
                slamTimer = 0;
                Player.StopExtraJumpInProgress();
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.65f, Pitch = -0.2f, MaxInstances = 3 }, Player.Center);
                }
            }

            //强制下坠（↓已在 SetControls 清）
            if (slamming) {
                slamTimer++;
                Player.velocity.Y = SlamSpeed * Player.gravDir;
                SpawnSlamDescentDust();
            }
        }

        //按住跳上升抵消重力，松手/下落关窗
        private void UpdateBottleLift() {
            if (bottleLiftTimer <= 0) {
                return;
            }
            if (Player.controlJump && Player.velocity.Y * Player.gravDir < 0f) {
                Player.velocity.Y -= 0.1f * Player.gravDir;
                bottleLiftTimer--;
            }
            else {
                bottleLiftTimer = 0;
            }
        }

        //下砸风纹
        private void SpawnSlamDescentDust() {
            if (VaultUtils.isServer) {
                return;
            }
            float side = Main.rand.NextBool() ? -1f : 1f;
            Vector2 pos = Player.Center + new Vector2(side * Main.rand.NextFloat(8f, 15f), Main.rand.NextFloat(-18f, 22f));
            PRTLoader.NewParticle<PRT_Smoke>(pos, -Player.velocity * 0.16f, GraniteMarbleVFX.MarbleDust
                , Main.rand.NextFloat(0.28f, 0.42f)).Configure(13, 0.5f, 0.03f);
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_MarbleChip>(pos, -Player.velocity * Main.rand.NextFloat(0.12f, 0.2f)
                    , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.35f, 0.55f)).Configure(Main.rand.Next(10, 16), 0.02f);
            }
        }

        private void OnSlamLand() {
            float growth = Math.Min(slamTimer, GrowthCap) / (float)GrowthCap;
            Vector2 feet = Player.gravDir == 1f ? Player.Bottom : Player.Top;
            float up = -Player.gravDir;

            if (!VaultUtils.isServer) {
                //砸击分层音
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Volume = 0.7f + growth * 0.4f, Pitch = -0.35f - growth * 0.3f, MaxInstances = 3
                }, feet);
                SoundEngine.PlaySound(SoundID.Tink with {
                    Volume = 0.35f + growth * 0.2f, Pitch = 0.1f - growth * 0.4f, MaxInstances = 3
                }, feet);
                if (growth > 0.6f) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = -0.7f, MaxInstances = 2 }, feet);
                }

                //落点石尘+石屑，尘土波另推
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(feet, new Vector2(Main.rand.NextFloat(-5f, 5f), up * Main.rand.NextFloat(0.6f, 3f))
                        , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.42f, 0.66f)).Configure(24, 0.7f, 0.05f);
                }
                int chips = 4 + (int)(growth * 5f);
                for (int i = 0; i < chips; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(feet, new Vector2(Main.rand.NextFloat(-4f, 4f), up * Main.rand.NextFloat(2.5f, 6.5f))
                        , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.5f, 0.85f)).Configure(Main.rand.Next(22, 34));
                }

                dustWaveStep = 1;
                dustWaveMaxStep = 10 + (int)(growth * 4f);
                dustWaveGrowth = growth;
                dustWaveX = feet.X;
                dustWaveLeftY = dustWaveRightY = feet.Y;
            }

            if (CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(feet, Main.rand.NextVector2Unit()
                    , 5f + growth * 4f, 6f, 12 + (int)(growth * 8f), 800f, "MarbleBalloonSlam"));
            }

            if (Player.whoAmI == Main.myPlayer) {
                //伤20+下坠tick封顶48，半径135→170
                int damage = 20 + Math.Min(slamTimer, GrowthCap);
                float radius = 135f + growth * 35f;
                Projectile.NewProjectile(Player.FromObjectGetParent(), feet, Vector2.Zero
                    , ModContent.ProjectileType<MarbleShockwave>(), damage, 5f, Player.whoAmI, 0f, radius);
            }
        }

        //尘土波双向，每tick每侧一列，±5格贴地
        private void UpdateGroundDustWave() {
            if (dustWaveStep <= 0 || VaultUtils.isServer) {
                return;
            }
            if (dustWaveStep > dustWaveMaxStep) {
                dustWaveStep = 0;
                return;
            }
            float fade = 1f - dustWaveStep / (float)dustWaveMaxStep * 0.6f;
            for (int dir = -1; dir <= 1; dir += 2) {
                float surfaceY = dir < 0 ? dustWaveLeftY : dustWaveRightY;
                float x = dustWaveX + dir * dustWaveStep * 13f;
                if (!TryFindWaveSurface(x, ref surfaceY)) {
                    continue;
                }
                if (dir < 0) {
                    dustWaveLeftY = surfaceY;
                }
                else {
                    dustWaveRightY = surfaceY;
                }

                float up = -Player.gravDir;
                Vector2 pos = new Vector2(x, surfaceY + up * 4f);
                PRTLoader.NewParticle<PRT_Smoke>(pos, new Vector2(dir * (1.6f + dustWaveGrowth * 1.2f), up * Main.rand.NextFloat(0.8f, 1.8f))
                    , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.35f, 0.55f) * fade).Configure(20, 0.65f * fade, 0.04f);
                if (Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(pos, new Vector2(dir * Main.rand.NextFloat(0.8f, 2.4f), up * Main.rand.NextFloat(1.8f, 4f))
                        , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.35f, 0.6f) * fade).Configure(Main.rand.Next(16, 26));
                }
            }
            dustWaveStep++;
        }

        //从上列地表定位本列，地里回退/空中下搜
        private bool TryFindWaveSurface(float x, ref float y) {
            int step = (int)Player.gravDir;
            Point probe = new Vector2(x, y + step * 4f).ToTileCoordinates();
            if (!WorldGen.InWorld(probe.X, probe.Y, 10)) {
                return false;
            }
            //反重力地表=天花板下缘
            float surfaceEdge = step < 0 ? 16f : 0f;
            if (IsWaveGround(probe.X, probe.Y)) {
                for (int i = 0; i < 5; i++) {
                    if (!IsWaveGround(probe.X, probe.Y - step * (i + 1))) {
                        y = (probe.Y - step * i) * 16f + surfaceEdge;
                        return true;
                    }
                }
                return false;
            }
            for (int i = 1; i <= 5; i++) {
                if (IsWaveGround(probe.X, probe.Y + step * i)) {
                    y = (probe.Y + step * i) * 16f + surfaceEdge;
                    return true;
                }
            }
            return false;
        }

        //地面=实心或平台
        private static bool IsWaveGround(int x, int y) {
            Tile tile = Framing.GetTileSafely(x, y);
            return tile.HasTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }
    }
}
