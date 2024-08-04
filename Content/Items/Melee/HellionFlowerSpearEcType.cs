using CalamityMod;
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
    internal class HellionFlowerSpearEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "HellionFlowerSpear";
        public override void SetDefaults() {
            Item.SetCalamitySD<HellionFlowerSpear>();
            Item.SetKnifeHeld<HellionFlowerSpearHeld>();
        }
    }

    internal class RHellionFlowerSpear : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<HellionFlowerSpear>();
        public override int ProtogenesisID => ModContent.ItemType<HellionFlowerSpearEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<HellionFlowerSpearHeld>();
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class HellionFlowerSpearHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<HellionFlowerSpear>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Greentide_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 46;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 20;
            drawTrailCount = 6;
            Length = 52;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            ShootSpeed = 32f;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<HellionSpike>(), Projectile.damage
                , Projectile.knockBack, Owner.whoAmI, 0f, 0);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (hit.Crit) {
                Projectile petal = CalamityUtils.ProjectileBarrage(Source, Projectile.Center, target.Center
                    , Main.rand.NextBool(), 800f, 800f, 0f, 800f, 10f, ProjectileID.FlowerPetal
                    , (int)(Projectile.damage * 0.5), Projectile.knockBack * 0.5f, Projectile.owner, true);
                if (petal.whoAmI.WithinBounds(Main.maxProjectiles)) {
                    petal.DamageType = DamageClass.Melee;
                    petal.localNPCHitCooldown = -1;
                }
            }
        }
    }
}
