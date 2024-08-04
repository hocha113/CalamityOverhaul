using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 携序之剑
    /// </summary>
    internal class OrderbringerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Orderbringer";
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = Item.height = 108;
            Item.damage = 228;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 18;
            Item.useTurn = true;
            Item.knockBack = 7f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            Item.rare = ModContent.RarityType<DarkBlue>();
            Item.shoot = ModContent.ProjectileType<OrderbringerBeams>();
            Item.shootSpeed = 6f;
            Item.SetKnifeHeld<OrderbringerHeld>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            _ = Projectile.NewProjectile(source, position, velocity, Item.shoot, damage, knockback, player.whoAmI);
            return false;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3)) {
                _ = Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.GoldCoin, 0f, 0f, 0, new Color(255, Main.DiscoG, 53));
            }
        }
    }

    internal class OrderbringerHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Orderbringer>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "GreatswordofJudgement_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 86;
            canDrawSlashTrail = true;
            distanceToOwner = 0;
            drawTrailBtommWidth = 30;
            drawTrailTopWidth = 80;
            drawTrailCount = 16;
            Length = 120;
            unitOffsetDrawZkMode = 0;
            SwingData.starArg = 80;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 130;
            SwingData.maxClampLength = 140;
            SwingData.ler1_UpSizeSengs = 0.056f;
            ShootSpeed = 6;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<OrderbringerBeams>(), Projectile.damage
                , Projectile.knockBack, Owner.whoAmI, 0f, 0);
        }

        public override bool PreInOwnerUpdate() {
            if (Main.rand.NextBool(3 * updateCount)) {
                _ = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , DustID.GoldCoin, 0f, 0f, 0, new Color(255, Main.DiscoG, 53));
            }
            return base.PreInOwnerUpdate();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {

        }
    }
}
