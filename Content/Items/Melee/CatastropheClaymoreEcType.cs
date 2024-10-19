using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class CatastropheClaymoreEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "CatastropheClaymore";
        public override void SetDefaults() {
            Item.SetItemCopySD<CatastropheClaymore>();
            Item.SetKnifeHeld<CatastropheClaymoreHeld>();
        }
    }

    internal class RCatastropheClaymore : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CatastropheClaymore>();
        public override int ProtogenesisID => ModContent.ItemType<CatastropheClaymoreEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<CatastropheClaymoreHeld>();
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class CatastropheClaymoreHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<CatastropheClaymore>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "CatastropheClaymore_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 60;
            distanceToOwner = -22;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 46;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 22;
            Length = 56;
            ShootSpeed = 11;
        }

        public override bool PreInOwnerUpdate() {
            if (Main.rand.NextBool(3 * updateCount)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.PinkFairy);
            }
            return base.PreInOwnerUpdate();
        }

        public override void Shoot() {
            int type = ModContent.ProjectileType<CatastropheClaymoreSparkle>();
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity, type
                , Projectile.damage, Projectile.knockBack, Main.myPlayer, Main.rand.Next(3));
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.rand.NextBool(3)) {
                target.AddBuff(BuffID.Ichor, 60);
                target.AddBuff(BuffID.OnFire3, 180);
                target.AddBuff(BuffID.Frostburn2, 120);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Main.rand.NextBool(3)) {
                target.AddBuff(BuffID.Ichor, 60);
                target.AddBuff(BuffID.OnFire3, 180);
                target.AddBuff(BuffID.Frostburn2, 120);
            }
        }
    }
}
