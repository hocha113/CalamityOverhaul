using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Mono.Cecil;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 天神之剑
    /// </summary>
    internal class CelestialClaymoreEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "CelestialClaymore";
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 80;
            Item.height = 82;
            Item.damage = 75;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 19;
            Item.useTime = 19;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5.25f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.RarityLightRedBuyPrice;
            Item.rare = ItemRarityID.LightRed;
            Item.shoot = ModContent.ProjectileType<CosmicSpiritBombs>();
            Item.shootSpeed = 0.1f;
            Item.SetKnifeHeld<CelestialClaymoreHeld>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(4)) {
                int dustType = Main.rand.Next(2);
                dustType = dustType == 0 ? 15 : dustType == 1 ? 73 : 244;
                int swingDust = Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, dustType, player.direction * 2, 0f, 150, default, 1.3f);
                Main.dust[swingDust].velocity *= 0.2f;
                foreach (Projectile proj in Main.projectile) {
                    if (proj.type == ModContent.ProjectileType<CosmicSpiritBombs>()) {
                        if (proj.Hitbox.Intersects(hitbox)) {
                            Vector2 toMou = proj.Center.To(Main.MouseWorld).UnitVector();
                            proj.ai[0] += 1;
                            proj.velocity = toMou * 15;
                            proj.timeLeft = 150;
                            proj.damage = (int)(proj.originalDamage * 0.3f);
                            proj.netUpdate = true;
                        }
                    }
                }
            }
        }
    }

    internal class CelestialClaymoreHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<CelestialClaymore>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Excelsus_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 56;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 30;
            drawTrailCount = 8;
            Length = 68;
            SwingData.baseSwingSpeed = 6.65f;
            SwingAIType = SwingAITypeEnum.UpAndDown;
        }

        public override void Shoot() {
            Player player = Owner;
            for (int i = 0; i < 3; i++) {
                Vector2 realPlayerPos = new Vector2(player.position.X + (player.width * 0.5f) + (float)(Main.rand.Next(201) * -(float)player.direction)
                    + (Main.mouseX + Main.screenPosition.X - player.position.X), player.MountedCenter.Y);
                realPlayerPos.X = ((realPlayerPos.X + player.Center.X) / 2f) + Main.rand.Next(-200, 201);
                realPlayerPos.Y -= 100 * i;
                int proj = Projectile.NewProjectile(Source, realPlayerPos.X, realPlayerPos.Y, 0f, 0f
                    , ModContent.ProjectileType<CosmicSpiritBombs>(), Projectile.damage / 2, Projectile.knockBack, player.whoAmI, 0f, Main.rand.Next(3));
                CosmicSpiritBombs cosmicSpiritBombs = Main.projectile[proj].ModProjectile as CosmicSpiritBombs;
                cosmicSpiritBombs.overTextIndex = Main.rand.Next(1, 4);
            }
        }

        public override bool PreInOwnerUpdate() {
            if (Main.rand.NextBool(4)) {
                int dustType = Main.rand.Next(2);
                dustType = dustType == 0 ? 15 : dustType == 1 ? 73 : 244;
                int swingDust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType, Owner.direction * 2, 0f, 150, default, 1.3f);
                Main.dust[swingDust].velocity *= 0.2f;
                foreach (Projectile proj in Main.projectile) {
                    if (proj.type == ModContent.ProjectileType<CosmicSpiritBombs>()) {
                        if (proj.Hitbox.Intersects(Projectile.Hitbox)) {
                            Vector2 toMou = proj.Center.To(Main.MouseWorld).UnitVector();
                            proj.ai[0] += 1;
                            proj.velocity = toMou * 15;
                            proj.timeLeft = 150;
                            proj.damage = (int)(proj.originalDamage * 0.3f);
                            proj.netUpdate = true;
                        }
                    }
                }
            }
            return base.PreInOwnerUpdate();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
        }
    }
}
