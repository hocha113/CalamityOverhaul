using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class TaintedBladeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TaintedBlade";
        public override void SetDefaults() {
            Item.SetItemCopySD<TaintedBlade>();
            Item.SetKnifeHeld<TaintedBladeHeld>();
        }
    }

    internal class RTaintedBlade : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<TaintedBlade>();
        public override int ProtogenesisID => ModContent.ItemType<TaintedBladeEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<TaintedBladeHeld>();
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class TaintedBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<TaintedBlade>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "BloodRed_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 40;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            SwingData.starArg = 74;
            SwingData.baseSwingSpeed = 4f;
            drawTrailBtommWidth = 30;
            distanceToOwner = 10;
            drawTrailTopWidth = 20;
            Length = 60;
            unitOffsetDrawZkMode = -4;
            overOffsetCachesRoting = MathHelper.ToRadians(8);
            SwingData.starArg = 80;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 70;
            SwingData.maxClampLength = 80;
            SwingData.ler1_UpSizeSengs = 0.056f;
            ShootSpeed = 12;
        }

        public override void PostInOwnerUpdate() {
            if (Main.rand.NextBool(3 * UpdateRate)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , DustID.GreenFairy, 0f, 0f, 100, default, Main.rand.NextFloat(1.5f, 2f));
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0f;
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 240);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Poisoned, 240);
        }
    }
}
