using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class EarthEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Earth";
        public override void SetDefaults() {
            Item.SetCalamitySD<Earth>();
            Item.GiveMeleeType();
            Item.SetKnifeHeld<EarthHeld>();
        }
    }

    internal class REarth : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Earth>();
        public override int ProtogenesisID => ModContent.ItemType<EarthEcType>();
        public override void SetDefaults(Item item) {
            item.GiveMeleeType();
            item.SetKnifeHeld<EarthHeld>();
        }
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class EarthHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Earth>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "Swordsplosion_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 86;
            canDrawSlashTrail = true;
            distanceToOwner = -10;
            drawTrailBtommWidth = 30;
            drawTrailTopWidth = 80;
            drawTrailCount = 14;
            Length = 120;
            unitOffsetDrawZkMode = 6;
            overOffsetCachesRoting = MathHelper.ToRadians(8);
            SwingData.starArg = 60;
            SwingData.baseSwingSpeed = 4.2f;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 130;
            SwingData.maxClampLength = 140;
            SwingData.ler1_UpSizeSengs = 0.056f;
            ShootSpeed = 20;
        }

        public override bool PreInOwnerUpdate() {
            if (Time % (10 * updateCount) == 0) {
                canShoot = true;
            }
            return base.PreInOwnerUpdate();
        }

        public override void Shoot() {
            int type = ModContent.ProjectileType<EarthProj>();
            Vector2 orig = ShootSpanPos + new Vector2(0, -800);
            Vector2 toMou = orig.To(InMousePos);
            for (int i = 0; i < 3; i++) {
                Vector2 spwanPos = orig + toMou.UnitVector() * 600;
                spwanPos.X += Main.rand.Next(-1260, 1260);
                spwanPos.Y -= 660;
                Vector2 ver = (spwanPos.To(InMousePos)).UnitVector() * 26;
                ver = ver.RotatedByRandom(0.2f);
                ver *= Main.rand.NextFloat(0.6f, 1.33f);
                Projectile.NewProjectile(Source, spwanPos, ver, type, Projectile.damage, Projectile.knockBack, Owner.whoAmI, 0f, (float)Main.rand.Next(10));
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int heal = Main.rand.Next(1, 70);
            Owner.lifeSteal -= heal;
            Owner.statLife += heal;
            Owner.HealEffect(heal);
            if (Owner.statLife > Owner.statLifeMax2)
                Owner.statLife = Owner.statLifeMax2;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            int heal = Main.rand.Next(1, 70);
            Owner.lifeSteal -= heal;
            Owner.statLife += heal;
            Owner.HealEffect(heal);
            if (Owner.statLife > Owner.statLifeMax2)
                Owner.statLife = Owner.statLifeMax2;
        }
    }
}
