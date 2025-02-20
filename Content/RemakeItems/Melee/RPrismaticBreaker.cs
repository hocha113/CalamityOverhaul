using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Tools;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RPrismaticBreaker : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<PrismaticBreaker>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<PrismaticBreakerHeld>();
        public override bool? On_CanUseItem(Item item, Player player) => CanUseItemFunc(item, player);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(player, source, position, velocity, type, damage, knockback);
        }

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

        public override void PostInOwner() {
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
