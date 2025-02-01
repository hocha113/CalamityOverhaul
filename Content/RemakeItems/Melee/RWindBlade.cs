using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RWindBlade : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<WindBlade>();
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[ModContent.ItemType<WindBlade>()] = true;
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);

        public static void SetDefaultsFunc(Item Item) {
            Item.width = 58;
            Item.damage = 41;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 20;
            Item.useTurn = true;
            Item.knockBack = 5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.height = 58;
            Item.value = CalamityGlobalItem.RarityOrangeBuyPrice;
            Item.rare = ItemRarityID.Orange;
            Item.shoot = ModContent.ProjectileType<AirBomb>();
            Item.shootSpeed = 3f;
            Item.SetKnifeHeld<WindBladeHeld>();
        }
        public static bool ShootFunc(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 1);
                return false;
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class WindBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<WindBlade>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "WindBlade_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 46;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommWidth = 40;
            drawTrailTopWidth = 16;
            drawTrailCount = 6;
            Length = 52;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            ShootSpeed = 3f;
        }

        public override void KnifeInitialize() {
            if (Projectile.ai[0] != 0) {
                drawTrailCount = 60;
                drawTrailCount *= UpdateRate;
                oldRotate = new float[drawTrailCount];
                oldDistanceToOwner = new float[drawTrailCount];
                oldLength = new float[drawTrailCount];
                InitializeCaches();
            }
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<AirBomb>(), Projectile.damage / 2
                , Projectile.knockBack, Owner.whoAmI, Projectile.ai[0]);
            if (Projectile.ai[0] == 1) {
                SoundEngine.PlaySound(SoundID.Item84 with { MaxInstances = 6, Pitch = -0.2f, Volume = 0.8f }, Owner.Center);
                for (int i = 0; i <= 360; i += 3) {
                    Vector2 vr = new Vector2(3f, 3f).RotatedBy(MathHelper.ToRadians(i));
                    int num = Dust.NewDust(ShootSpanPos, Owner.width, Owner.height
                        , DustID.Smoke, vr.X, vr.Y, 200, new Color(232, 251, 250, 200), 1.4f);
                    Main.dust[num].noGravity = true;
                    Main.dust[num].position = ShootSpanPos;
                    Main.dust[num].velocity = vr;
                }
            }
        }

        public override bool PreInOwnerUpdate() {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.BlueTorch);
            }
            if (Projectile.ai[0] == 1) {
                SwingData.baseSwingSpeed = 7.25f;
            }
            else {
                ExecuteAdaptiveSwing(phase0SwingSpeed: 0.3f, phase1SwingSpeed: 4.2f
                    , phase2SwingSpeed: 6f, phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            }

            return base.PreInOwnerUpdate();
        }
    }
}
