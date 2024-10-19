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
    internal class FeralthornClaymoreEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "FeralthornClaymore";
        public override void SetDefaults() {
            Item.SetItemCopySD<FeralthornClaymore>();
            Item.SetKnifeHeld<FeralthornClaymoreHeld>();
        }
    }

    internal class RFeralthornClaymore : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<FeralthornClaymore>();
        public override int ProtogenesisID => ModContent.ItemType<FeralthornClaymoreEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<FeralthornClaymoreHeld>();
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class FeralthornClaymoreHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<FeralthornClaymore>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "FeralthornClaymore_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 64;
            Projectile.scale = 1.24f;
            canDrawSlashTrail = true;
            overOffsetCachesRoting = MathHelper.ToRadians(6);
            SwingData.starArg = 54;
            SwingData.minClampLength = 110;
            SwingData.maxClampLength = 120;
            SwingData.baseSwingSpeed = 4f;
            distanceToOwner = 0;
            drawTrailTopWidth = 30;
            Length = 110;
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(4)) {
                Dust.NewDust(Projectile.position, Projectile.width
                    , Projectile.height, DustID.JungleSpore);
            }
        }

        public override void Shoot() {
            Vector2 spanPos = ShootSpanPos + ShootVelocity.UnitVector() * Length * Main.rand.NextFloat(0.6f, 8.2f);
            Projectile.NewProjectile(Source, spanPos, CWRUtils.randVr(23, 35)
                , ModContent.ProjectileType<ThornBase>(), (int)(Item.damage * 0.5), 0f, Main.myPlayer);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Venom, 300);
            Projectile.NewProjectile(Source, target.Center.X, target.Center.Y
                , Main.rand.NextFloat(-18f, 18f), Main.rand.NextFloat(-18f, 18f)
                , ModContent.ProjectileType<ThornBase>(), (int)(Item.damage * 0.5), 0f, Main.myPlayer);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Venom, 300);
            Projectile.NewProjectile(Source, target.Center.X, target.Center.Y
                , Main.rand.NextFloat(-18f, 18f), Main.rand.NextFloat(-18f, 18f)
                , ModContent.ProjectileType<ThornBase>(), (int)(Item.damage * 0.5), 0f, Main.myPlayer);
        }
    }
}
