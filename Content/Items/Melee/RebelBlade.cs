using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.RebelBladeProj;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 叛逆之刃
    /// </summary>
    internal class RebelBlade : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "RebelBlade";
        public override void SetDefaults() {
            Item.width = Item.height = 54;
            Item.shootSpeed = 9;
            Item.crit = 8;
            Item.damage = 286;
            Item.useTime = 30;
            Item.useAnimation = 15;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(0, 83, 55, 0);
            Item.rare = ItemRarityID.Lime;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = null;
            Item.DamageType = CWRRef.GetTrueMeleeDamageClass();
            Item.shoot = ModContent.ProjectileType<RebelBladeFlyAttcke>();
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.CWR().isHeldItem = true;
            Item.SetKnifeHeld<RebelBladeHeld>(true);
        }

        public override void UseStyle(Player player, Rectangle heldItemFrame) => player.itemLocation = player.GetPlayerStabilityCenter();

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<RebelBladeFlyAttcke>()] == 0;

        public override void HoldItem(Player player) {
            if (Main.myPlayer != player.whoAmI || player.PressKey() || !CWRServerConfig.Instance.WeaponHandheldDisplay) {
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
                SoundEngine.PlaySound(SoundID.Item1, position);
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<RebelBladeFlyAttcke>(), (int)(damage * 0.6f), knockback, player.whoAmI);
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
            drawTrailTopWidth = 130;
            drawTrailCount = 6;
            Length = 200;
            unitOffsetDrawZkMode = 0;
            Projectile.width = Projectile.height = 186;
            distanceToOwner = -60;
            SwingData.starArg = 30;
            SwingData.ler1_UpLengthSengs = 0.05f;
            SwingData.minClampLength = 200;
            SwingData.maxClampLength = 210;
            SwingData.ler1_UpSizeSengs = 0.016f;
            SwingData.baseSwingSpeed = 4.2f;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            ShootSpeed = 12;
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(
            phase0SwingSpeed: -0.4f,
            phase1SwingSpeed: 3.4f,
            phase2SwingSpeed: 7f,
            swingSound: SoundID.Item71 with { Pitch = -0.6f });
            return base.PreInOwner();
        }

        public override void MeleeEffect() {
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height
                , DustID.FireworkFountain_Blue, 0, 0, 55);
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
