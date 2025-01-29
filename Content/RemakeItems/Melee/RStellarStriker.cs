using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Mono.Cecil;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RStellarStriker : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<StellarStriker>();
 
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? UseItem(Item item, Player player) => UseItemFunc(item, player);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(player, source, position, velocity, type, damage, knockback);
        }

        public static void SetDefaultsFunc(Item Item) {
            Item.width = 90;
            Item.height = 100;
            Item.scale = 1.5f;
            Item.damage = 480;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 20;
            Item.useTurn = true;
            Item.knockBack = 7.75f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.RarityPurpleBuyPrice;
            Item.rare = ItemRarityID.Red;
            Item.shoot = ProjectileID.LunarFlare;
            Item.shootSpeed = 12f;
            Item.SetKnifeHeld<StellarStrikerHeld>();
        }

        public static bool? UseItemFunc(Item Item, Player player) {
            Item.useAnimation = Item.useTime = 15;
            if (player.altFunctionUse == 2) {
                Item.useAnimation = Item.useTime = 20;
            }
            return true;
        }

        public static bool ShootFunc(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.GetItem().GiveMeleeType();
            if (player.altFunctionUse == 2) {
                player.GetItem().GiveMeleeType(true);
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 1);
                return false;
            }

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class StellarStrikerHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<StellarStriker>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "StellarStriker_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 86;
            canDrawSlashTrail = true;
            distanceToOwner = -20;
            drawTrailBtommWidth = 30;
            drawTrailTopWidth = 70;
            drawTrailCount = 16;
            Length = 100;
            unitOffsetDrawZkMode = 0;
            SwingData.starArg = 60;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 110;
            SwingData.maxClampLength = 120;
            SwingData.ler1_UpSizeSengs = 0.016f;
            ShootSpeed = 12;
            inWormBodysDamageFaul = 0.5f;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 1) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item88, Owner.Center);
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<StellarStrikerBeam>(), Projectile.damage / 2
                , Projectile.knockBack, Owner.whoAmI, 0f, 0);
        }

        public override bool PreInOwnerUpdate() {
            OtherMeleeSize = 0.8f;
            if (Projectile.ai[0] == 1) {
                OtherMeleeSize = 1.1f;
            }
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.3f, phase1SwingSpeed: 8.2f, phase2SwingSpeed: 6f);
            return base.PreInOwnerUpdate();
        }

        public override void MeleeEffect() => Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Vortex);

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.ai[0] != 1 || Projectile.numHits > 0) {
                return;
            }

            SpawnFlares(target);
            Projectile.numHits++;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => SpawnFlares(target);

        public void SpawnFlares(Entity target) {
            SoundEngine.PlaySound(SoundID.Item88, Owner.Center);
            for (int i = 0; i < 6; i++) {
                Vector2 spanPos = target.Center + new Vector2(Main.rand.Next(-900, 900), Main.rand.Next(-700, 600));
                Vector2 ver = spanPos.To(target.Center + new Vector2(Main.rand.Next(-target.width, target.width), Main.rand.Next(-target.height, target.height)));
                ver = ver.UnitVector() * 13;
                Projectile.NewProjectile(Source, spanPos, ver
                , ModContent.ProjectileType<StellarStrikerBeam>(), Projectile.damage / 2
                , Projectile.knockBack, Owner.whoAmI, 0f, 0f, 1);
            }
        }
    }
}
