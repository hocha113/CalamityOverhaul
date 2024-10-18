using CalamityMod;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.RebelBladeProj;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class RebelBlade : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "RebelBlade";
        public override void SetDefaults() {
            Item.width = Item.height = 54;
            Item.shootSpeed = 9;
            Item.crit = 8;
            Item.damage = 186;
            Item.useTime = 15;
            Item.useAnimation = 15;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(0, 83, 55, 0);
            Item.rare = ItemRarityID.Lime;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Item.shoot = ModContent.ProjectileType<RebelBladeFlyAttcke>();
            Item.CWR().isHeldItem = true;
            Item.SetKnifeHeld<RebelBladeHeld>();
        }

        public override void UseStyle(Player player, Rectangle heldItemFrame) => player.itemLocation = player.GetPlayerStabilityCenter();

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<RebelBladeFlyAttcke>()] == 0;

        public override void HoldItem(Player player) {
            if (Main.myPlayer != player.whoAmI || player.PressKey()) {
                return;
            }

            bool spwan = true;

            int rebelBladeBack = ModContent.ProjectileType<RebelBladeBack>();
            int rebelBladeFlyAttcke = ModContent.ProjectileType<RebelBladeFlyAttcke>();
            int rebelBladeHeld = ModContent.ProjectileType<RebelBladeHeld>();

            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI) {
                    continue;
                }
                if (proj.type == rebelBladeBack || proj.type == rebelBladeFlyAttcke || proj.type == rebelBladeHeld) {
                    spwan = false;
                    break;
                }
            }

            if (spwan) {
                Projectile.NewProjectileDirect(player.GetSource_FromThis(), player.Center, Vector2.Zero, rebelBladeBack, 0, 0, player.whoAmI);
            }
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<RebelBladeFlyAttcke>(), damage, knockback, player.whoAmI);
                return false;
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.LunarBar, 10)
                .AddIngredient(ItemID.SoulofMight, 15)
                .AddIngredient(ItemID.SoulofLight, 15)
                .AddIngredient(ItemID.SoulofNight, 15)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    internal class RebelBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<RebelBlade>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail2";
        public override string gradientTexturePath => CWRConstant.ColorBar + "RebelBlade_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            distanceToOwner = -20;
            drawTrailBtommWidth = 110;
            drawTrailTopWidth = 150;
            drawTrailCount = 6;
            Length = 200;
            unitOffsetDrawZkMode = 0;
            Projectile.width = Projectile.height = 186;
            distanceToOwner = -60;
            SwingData.starArg = 60;
            SwingData.ler1_UpLengthSengs = 0.05f;
            SwingData.minClampLength = 200;
            SwingData.maxClampLength = 210;
            SwingData.ler1_UpSizeSengs = 0.016f;
            SwingData.baseSwingSpeed = 4.2f;
            ShootSpeed = 12;
            maxSwingTime = 60;
        }

        public override bool PreInOwnerUpdate() {
            if (Time == 0) {
                OtherMeleeSize = 0.64f;
            }
            float swingUp = SetSwingSpeed(1f);
            int time1 = (int)(maxSwingTime / 3 * updateCount * swingUp);
            int time2 = (int)(maxSwingTime * 0.6f * updateCount * swingUp);
            if (Time > time1) {
                canDrawSlashTrail = true;
                SwingData.baseSwingSpeed = 6.2f;
                if (Time == time1 + 1) {
                    speed = MathHelper.ToRadians(SwingData.baseSwingSpeed) / swingUp;
                }
            }
            else {
                OtherMeleeSize += 0.01f;
                SwingData.baseSwingSpeed = 1f;
                speed = MathHelper.ToRadians(SwingData.baseSwingSpeed) / swingUp;
                canDrawSlashTrail = false;
            }

            if (Time > time2) {
                OtherMeleeSize -= 0.004f;
                SwingData.baseSwingSpeed = 8f;
                if (Time == time2 + 1) {
                    speed = MathHelper.ToRadians(SwingData.baseSwingSpeed) / swingUp;
                }
            }
            return base.PreInOwnerUpdate();
        }

        public override void MeleeEffect() {
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height
                , DustID.FireworkFountain_Yellow, 0, 0, 55);
            dust.noGravity = true;
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.FromWormBodysRandomSet(5)) {
                return;
            }

            int type = ModContent.ProjectileType<RebelBladeOrb>();
            if (Owner.ownedProjectileCounts[type] > 33) {
                return;
            }

            for (int i = 0; i < 3; i++) {
                Vector2 spwanPos = target.position + new Vector2(target.width * Main.rand.NextFloat(), target.height * Main.rand.NextFloat());
                Projectile.NewProjectile(Source, spwanPos, Vector2.Zero
                    , ModContent.ProjectileType<RebelBladeOrb>(), Item.damage / 5, 0, Owner.whoAmI);
                Owner.ownedProjectileCounts[type]++;
        }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            for (int i = 0; i < 3; i++) {
                Vector2 spwanPos = target.position + new Vector2(target.width * Main.rand.NextFloat(), target.height * Main.rand.NextFloat());
                Projectile.NewProjectile(Source, spwanPos, Vector2.Zero
                    , ModContent.ProjectileType<RebelBladeOrb>(), Item.damage / 5, 0, Owner.whoAmI);
            }
        }
    }
}
