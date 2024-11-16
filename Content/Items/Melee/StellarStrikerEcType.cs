using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using System;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 月炎之锋
    /// </summary>
    internal class StellarStrikerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "StellarStriker";
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        public override bool AltFunctionUse(Player player) => true;
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 90;
            Item.height = 100;
            Item.scale = 1.5f;
            Item.damage = 480;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 20;
            Item.useTurn = true;
            Item.knockBack = 7.75f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.RarityPurpleBuyPrice;
            Item.rare = ItemRarityID.Red;
            Item.shoot = ProjectileID.LunarFlare;
            Item.shootSpeed = 12f;
            Item.SetKnifeHeld<StellarStrikerHeld>();
        }

        public override bool? UseItem(Player player) => UseItemFunc(Item, player);

        public static bool? UseItemFunc(Item Item, Player player) {
            Item.useAnimation = Item.useTime = 15;
            if (player.altFunctionUse == 2) {
                Item.useAnimation = Item.useTime = 20;
            }
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(player, source, position, velocity, type, damage, knockback);
        }

        public static bool ShootFunc(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.GetItem().GiveMeleeType();
            if (player.altFunctionUse == 2) {
                player.GetItem().GiveMeleeType(true);
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 1);
                return false;
            }

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class StellarStrikerHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<StellarStriker>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "StellarStriker_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 86;
            canDrawSlashTrail = true;
            distanceToOwner = -20;
            drawTrailBtommWidth = 30;
            drawTrailTopWidth = 70;
            drawTrailCount = 16;
            Length = 100;
            unitOffsetDrawZkMode = 0;
            SwingData.starArg = 60;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 110;
            SwingData.maxClampLength = 120;
            SwingData.ler1_UpSizeSengs = 0.016f;
            ShootSpeed = 12;
            inWormBodysDamageFaul = 0.5f;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 1) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item88, Owner.Center);
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<StellarStrikerBeam>(), Projectile.damage / 2
                , Projectile.knockBack, Owner.whoAmI, 0f, 0);
        }

        public override bool PreInOwnerUpdate() {
            OtherMeleeSize = 0.8f;
            if (Projectile.ai[0] == 1) {
                OtherMeleeSize = 1.1f;
            }
            return base.PreInOwnerUpdate();
        }

        public override void MeleeEffect() {
            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Vortex);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.ai[0] != 1) {
                return;
            }
            if (CWRLoad.WormBodys.Contains(target.type) && !Main.rand.NextBool(10)) {
                return;
            }
            SpawnFlares(Item, Owner, Item.knockBack, Item.damage, hit.Crit);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            SpawnFlares(Item, Owner, Item.knockBack, Item.damage, false);
        }

        public static void SpawnFlares(Item item, Player player, float knockback, int damage, bool crit) {
            IEntitySource source = player.GetSource_ItemUse(item);
            _ = SoundEngine.PlaySound(SoundID.Item88, player.Center);
            int i = Main.myPlayer;
            float cometSpeed = item.shootSpeed;
            Vector2 realPlayerPos = player.RotatedRelativePoint(player.MountedCenter, true);
            float mouseXDist = Main.mouseX + Main.screenPosition.X - realPlayerPos.X;
            float mouseYDist = Main.mouseY + Main.screenPosition.Y - realPlayerPos.Y;
            if (player.gravDir == -1f) {
                mouseYDist = Main.screenPosition.Y + Main.screenHeight - Main.mouseY - realPlayerPos.Y;
            }
            float mouseDistance = (float)Math.Sqrt((double)((mouseXDist * mouseXDist) + (mouseYDist * mouseYDist)));
            _ = (float.IsNaN(mouseXDist) && float.IsNaN(mouseYDist)) || (mouseXDist == 0f && mouseYDist == 0f)
                ? player.direction
                : cometSpeed / mouseDistance;

            if (crit) {
                damage /= 2;
            }

            for (int j = 0; j < 2; j++) {
                realPlayerPos = new Vector2(player.Center.X + (float)(Main.rand.Next(201) * -(float)player.direction)
                    + (Main.mouseX + Main.screenPosition.X - player.position.X), player.MountedCenter.Y - 600f);
                realPlayerPos.X = ((realPlayerPos.X + player.Center.X) / 2f) + Main.rand.Next(-200, 201);
                realPlayerPos.Y -= 100 * j;
                mouseXDist = Main.mouseX + Main.screenPosition.X - realPlayerPos.X + (Main.rand.Next(-40, 41) * 0.03f);
                mouseYDist = Main.mouseY + Main.screenPosition.Y - realPlayerPos.Y;
                if (mouseYDist < 0f) {
                    mouseYDist *= -1f;
                }
                if (mouseYDist < 20f) {
                    mouseYDist = 20f;
                }
                mouseDistance = (float)Math.Sqrt((double)((mouseXDist * mouseXDist) + (mouseYDist * mouseYDist)));
                mouseDistance = cometSpeed / mouseDistance;
                mouseXDist *= mouseDistance;
                mouseYDist *= mouseDistance;
                float speedX = mouseXDist;
                float speedY = mouseYDist + (Main.rand.Next(-80, 81) * 0.02f);
                int proj = Projectile.NewProjectile(source, realPlayerPos.X, realPlayerPos.Y, speedX, speedY
                    , ProjectileID.LunarFlare, (int)(damage * 0.5), knockback, i, 0f, Main.rand.Next(3));
                if (proj.WithinBounds(Main.maxProjectiles)) {
                    Main.projectile[proj].DamageType = DamageClass.Melee;
                }
            }
        }
    }
}
