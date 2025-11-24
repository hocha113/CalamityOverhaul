using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.PlagueProj;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RPlagueKeeper : CWRItemOverride
    {
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);

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
            Item.rare = ItemRarityID.Red;
            Item.shoot = ModContent.ProjectileType<PlagueBeeWave>();
            Item.shootSpeed = 9f;
            Item.SetKnifeHeld<PlagueKeeperHeld>();
        }
    }

    internal class PlagueKeeperHeld : BaseKnife
    {
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
            target.AddBuff(CWRID.Buff_, 300);
            for (int i = 0; i < 3; i++) {
                int bee = Projectile.NewProjectile(Source, Owner.Center, Vector2.Zero, Owner.beeType(),
                    Owner.beeDamage(Item.damage / 3), Owner.beeKB(0f), Owner.whoAmI);
                Main.projectile[bee].penetrate = 1;
                Main.projectile[bee].DamageType = DamageClass.Melee;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(CWRID.Buff_, 300);
            for (int i = 0; i < 3; i++) {
                int bee = Projectile.NewProjectile(Source, Owner.Center, Vector2.Zero, Owner.beeType(),
                    Owner.beeDamage(Item.damage / 3), Owner.beeKB(0f), Owner.whoAmI);
                Main.projectile[bee].penetrate = 1;
                Main.projectile[bee].DamageType = DamageClass.Melee;
            }
        }
    }
}
