using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RExcelsus : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Excelsus>();
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);

        public static void SetDefaultsFunc(Item Item) {
            Item.width = 78;
            Item.damage = 220;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.knockBack = 8f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 94;
            Item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            Item.rare = ModContent.RarityType<DarkBlue>();
            Item.shoot = ModContent.ProjectileType<ExcelsusMain>();
            Item.shootSpeed = 12f;
            Item.SetKnifeHeld<ExcelsusHeld>();
        }
    }

    internal class ExcelsusHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Excelsus>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail2";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Excelsus_Bar";
        public override string GlowTexturePath => "CalamityMod/Items/Weapons/Melee/ExcelsusGlow";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 70;
            drawTrailTopWidth = 20;
            drawTrailCount = 16;
            overOffsetCachesRoting = MathHelper.ToRadians(2);
            Projectile.scale = 1.25f;
            SwingData.starArg = 60;
            SwingData.baseSwingSpeed = 4.2f;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 130;
            SwingData.maxClampLength = 140;
            SwingData.ler1_UpSizeSengs = 0.056f;
            ShootSpeed = 12;
            Length = 130;
        }

        public override void Shoot() {
            int type = ModContent.ProjectileType<ExcelsusMain>();
            if (DownRight) {
                type = ModContent.ProjectileType<ExcelsusBomb>();
                Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                    , type, (int)(Projectile.damage * 3.55f), Projectile.knockBack, Owner.whoAmI);
                return;
            }
            for (int i = 0; i < 3; ++i) {
                float speedX = ShootVelocity.X + Main.rand.NextFloat(-1.5f, 1.5f);
                float speedY = ShootVelocity.Y + Main.rand.NextFloat(-1.5f, 1.5f);
                switch (i) {
                    case 0:
                        type = ModContent.ProjectileType<ExcelsusMain>();
                        break;
                    case 1:
                        type = ModContent.ProjectileType<ExcelsusBlue>();
                        break;
                    case 2:
                        type = ModContent.ProjectileType<ExcelsusPink>();
                        break;
                }

                Projectile.NewProjectile(Source, ShootSpanPos, new Vector2(speedX, speedY), type, Projectile.damage, Projectile.knockBack, Owner.whoAmI);
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.NewProjectile(Source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<DeathLaserSpwan>(), Projectile.damage, 0f, Owner.whoAmI, target.whoAmI);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Projectile.NewProjectile(Source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<DeathLaserSpwan>(), Projectile.damage, 0f, Owner.whoAmI, target.whoAmI);
        }
    }
}
