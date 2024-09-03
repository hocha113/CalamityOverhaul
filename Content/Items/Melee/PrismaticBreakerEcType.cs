using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Tools;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class PrismaticBreakerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "PrismaticBreaker";
        public override void SetStaticDefaults() {
            Item.staff[Item.type] = true;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Item.type] = true;
        }
        public override void SetDefaults() {
            Item.SetCalamitySD<PrismaticBreaker>();
            Item.SetKnifeHeld<PrismaticBreakerHeld>();
        }
        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 8;
        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Item.DrawItemGlowmaskSingleFrame(spriteBatch, rotation, ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Melee/PrismaticBreakerGlow").Value);
        }
        public override bool AltFunctionUse(Player player) => true;
        public override bool CanUseItem(Player player) => CanUseItemFunc(Item, player);
        public static bool CanUseItemFunc(Item Item, Player player) {
            if (player.altFunctionUse == 2) {
                Item.UseSound = SoundID.Item1;
                Item.useStyle = ItemUseStyleID.Swing;
                Item.useTurn = true;
                Item.autoReuse = true;
                Item.noMelee = false;
                Item.noUseGraphic = true;
                Item.channel = false;
            }
            else {
                Item.UseSound = CrystylCrusher.ChargeSound;
                Item.useStyle = ItemUseStyleID.Shoot;
                Item.useTurn = false;
                Item.autoReuse = false;
                Item.noMelee = true;
                Item.noUseGraphic = false;
                Item.channel = true;
            }
            return player.ownedProjectileCounts[ModContent.ProjectileType<PrismaticBreakerHeld>()] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<PrismaticBeam>()] == 0;
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(player, source, position, velocity, type, damage, knockback);
        }
        public static bool ShootFunc(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, type, (int)(damage * 1.1f), knockback, player.whoAmI);
            }
            else {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<PrismaticBeam>(), damage, knockback, player.whoAmI);
            }
            return false;
        }
    }

    internal class RPrismaticBreaker : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<PrismaticBreaker>();
        public override int ProtogenesisID => ModContent.ItemType<PrismaticBreakerEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<PrismaticBreakerHeld>();
        public override bool? On_CanUseItem(Item item, Player player) => PrismaticBreakerEcType.CanUseItemFunc(item, player);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return PrismaticBreakerEcType.ShootFunc(player, source, position, velocity, type, damage, knockback);
        }
    }

    internal class PrismaticBreakerHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<PrismaticBreaker>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "AbsoluteZero_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 40;
            canDrawSlashTrail = true;
            SwingData.starArg = 74;
            SwingData.baseSwingSpeed = 5f;
            drawTrailBtommWidth = 30;
            distanceToOwner = 14;
            drawTrailTopWidth = 20;
            Length = 50;
        }

        public override void PostInOwnerUpdate() {
            if (Main.rand.NextBool(4)) {
                Dust rainbow = Main.dust[Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.RainbowMk2, 0f, 0f, 50, Main.DiscoColor, 0.8f)];
                rainbow.noGravity = true;
            }
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, Owner.Center, UnitToMouseV * 32
                , ModContent.ProjectileType<PrismaticWave>(), Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Nightwither>(), 300);
            target.AddBuff(BuffID.Daybreak, 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<Nightwither>(), 300);
            target.AddBuff(BuffID.Daybreak, 300);
        }
    }
}
