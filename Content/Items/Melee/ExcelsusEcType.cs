using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 宙宇波能刃
    /// </summary>
    internal class ExcelsusEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Excelsus";
        public override void SetDefaults() => SetDefaultsFunc(Item);
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

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Item.DrawItemGlowmaskSingleFrame(spriteBatch, rotation
                , ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Melee/ExcelsusGlow").Value);
        }
    }

    internal class ExcelsusHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Excelsus>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail4";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Excelsus_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 70;
            drawTrailTopWidth = 20;
            drawTrailCount = 6;
            Length = 82;
            SwingData.starArg = 84;
            SwingData.baseSwingSpeed = 5;
            ShootSpeed = 12;
        }

        public override void Shoot() {
            int type = ModContent.ProjectileType<ExcelsusMain>();
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

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.NewProjectile(Source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<LaserFountains>(), Projectile.damage, 0f, Owner.whoAmI, target.whoAmI);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Projectile.NewProjectile(Source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<LaserFountains>(), Projectile.damage, 0f, Owner.whoAmI, target.whoAmI);
        }
    }
}
