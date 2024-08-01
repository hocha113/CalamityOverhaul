using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 风之刃
    /// </summary>
    internal class WindBladeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "WindBlade";
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 58;
            Item.damage = 41;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 20;
            Item.useStyle = 1;
            Item.useTime = 20;
            Item.useTurn = true;
            Item.knockBack = 5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.height = 58;
            Item.value = CalamityGlobalItem.RarityOrangeBuyPrice;
            Item.rare = ItemRarityID.Orange;
            Item.shoot = ModContent.ProjectileType<Cyclones>();
            Item.shootSpeed = 3f;
            Item.SetKnifeHeld<WindBladeHeld>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(player, source, position, velocity, type, damage, knockback);
        }

        public static bool ShootFunc(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 1);
                return false;
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override bool AltFunctionUse(Player player) => true;
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

        public override void Initialize() {
            base.Initialize();
            if (Projectile.ai[0] == 0) {
                SoundEngine.PlaySound(SoundID.Item1, Owner.Center);
            }
            else {
                drawTrailCount = 60;
                drawTrailCount *= updateCount;
                oldRotate = new float[drawTrailCount];
                oldDistanceToOwner = new float[drawTrailCount];
                oldLength = new float[drawTrailCount];
                InitializeCaches();
                SoundEngine.PlaySound(SoundID.Item84 with { MaxInstances = 6 }, Owner.Center);
            }
        }

        public override void Shoot() {
            int proj = Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<Cyclones>(), Projectile.damage / 2, Projectile.knockBack, Owner.whoAmI);
            if (Projectile.ai[0] == 1) {
                Main.projectile[proj].ai[0] = 1;
                Main.projectile[proj].timeLeft = 360;
                Main.projectile[proj].damage = Projectile.damage / 4;

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
            return base.PreInOwnerUpdate();
        }
    }
}
