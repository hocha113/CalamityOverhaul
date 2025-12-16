using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RExcelsus : CWRItemOverride
    {
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
            Item.shootSpeed = 12f;
            Item.SetKnifeHeld<ExcelsusHeld>();
        }
    }

    internal class ExcelsusHeld : BaseKnife
    {
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
            int type = CWRID.Proj_ExcelsusMain;
            for (int i = 0; i < 3; ++i) {
                float speedX = ShootVelocity.X + Main.rand.NextFloat(-1.5f, 1.5f);
                float speedY = ShootVelocity.Y + Main.rand.NextFloat(-1.5f, 1.5f);
                switch (i) {
                    case 0:
                        type = CWRID.Proj_ExcelsusMain;
                        break;
                    case 1:
                        type = CWRID.Proj_ExcelsusBlue;
                        break;
                    case 2:
                        type = CWRID.Proj_ExcelsusPink;
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
