using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.PlagueProj;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RPlagueKeeper : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<PlagueKeeper>();
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(player, source, position, velocity, type, damage, knockback);
        }

        public static void SetDefaultsFunc(Item Item) {
            Item.width = 74;
            Item.damage = 75;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 20;
            Item.useTurn = true;
            Item.knockBack = 6f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 90;
            Item.value = CalamityGlobalItem.RarityPurpleBuyPrice;
            Item.rare = ItemRarityID.Red;
            Item.shoot = ModContent.ProjectileType<PlagueBeeWave>();
            Item.shootSpeed = 9f;
            Item.SetKnifeHeld<PlagueKeeperHeld>();
        }

        public static bool ShootFunc(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class PlagueKeeperHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<PlagueKeeper>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Greentide_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            distanceToOwner = 32;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 20;
            drawTrailCount = 6;
            Length = 78;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<GouldBee>(), (int)(Projectile.damage * 0.75f), 0, Owner.whoAmI);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Plague>(), 300);
            for (int i = 0; i < 3; i++) {
                int bee = Projectile.NewProjectile(Source, Owner.Center, Vector2.Zero, Owner.beeType(),
                    Owner.beeDamage(Item.damage / 3), Owner.beeKB(0f), Owner.whoAmI);
                if (bee.WithinBounds(Main.maxProjectiles)) {
                    Main.projectile[bee].penetrate = 1;
                    Main.projectile[bee].DamageType = DamageClass.Melee;
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<Plague>(), 300);
            for (int i = 0; i < 3; i++) {
                int bee = Projectile.NewProjectile(Source, Owner.Center, Vector2.Zero, Owner.beeType(),
                    Owner.beeDamage(Item.damage / 3), Owner.beeKB(0f), Owner.whoAmI);
                if (bee.WithinBounds(Main.maxProjectiles)) {
                    Main.projectile[bee].penetrate = 1;
                    Main.projectile[bee].DamageType = DamageClass.Melee;
                }
            }
        }
    }
}
