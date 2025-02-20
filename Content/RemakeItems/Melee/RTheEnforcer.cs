using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTheEnforcer : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<TheEnforcer>();
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 100;
            Item.height = 100;
            Item.scale = 1f;
            Item.damage = 690;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 17;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 17;
            Item.useTurn = true;
            Item.knockBack = 9f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            Item.rare = ModContent.RarityType<DarkBlue>();
            Item.shoot = ModContent.ProjectileType<EnforcerFlame>();
            Item.shootSpeed = 2;
            Item.SetKnifeHeld<TheEnforcerHeld>();
        }
    }

    internal class TheEnforcerHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<TheEnforcer>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "TheEnforcer_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 86;
            canDrawSlashTrail = true;
            distanceToOwner = -30;
            drawTrailBtommWidth = 30;
            drawTrailTopWidth = 80;
            drawTrailCount = 16;
            Length = 110;
            unitOffsetDrawZkMode = 0;
            overOffsetCachesRoting = MathHelper.ToRadians(8);
            SwingData.starArg = 60;
            SwingData.baseSwingSpeed = 4.2f;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 120;
            SwingData.maxClampLength = 130;
            SwingData.ler1_UpSizeSengs = 0.056f;
        }

        public override void Shoot() {
            for (int i = 0; i < 4; i++) {
                Vector2 realPlayerPos = new Vector2(Owner.position.X + (Owner.width * 0.5f) + (float)(Main.rand.Next(1358) * -(float)Owner.direction)
                    + (Main.mouseX + Main.screenPosition.X - Owner.position.X), Owner.MountedCenter.Y);
                realPlayerPos.X = ((realPlayerPos.X + Owner.Center.X) / 2f) + Main.rand.Next(-350, 351);
                realPlayerPos.Y -= 100 * i;
                Projectile.NewProjectile(Source, realPlayerPos, Vector2.Zero
                    , ModContent.ProjectileType<EnforcerFlame>(), Projectile.damage / 4
                    , Projectile.knockBack, Owner.whoAmI, 0f, Main.rand.Next(3));
            }
        }

        public override void MeleeEffect() => Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.ShadowbeamStaff);

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 4; i++) {
                Projectile.NewProjectile(Source, target.Center + CWRUtils.randVr(660, 880), Vector2.Zero
                    , ModContent.ProjectileType<EnforcerFlame>(), Projectile.damage / 4
                    , Projectile.knockBack, Owner.whoAmI, 0f, Main.rand.Next(3));
            }
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.3f, phase1Ratio: 0.2f, phase1SwingSpeed: 6.2f
                    , phase2SwingSpeed: 2f, phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0, swingSound: SoundID.Item20);
            return base.PreInOwner();
        }
    }
}
