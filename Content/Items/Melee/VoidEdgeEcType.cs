using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class VoidEdgeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "VoidEdge";
        public override void SetDefaults() {
            Item.SetCalamitySD<VoidEdge>();
            Item.SetKnifeHeld<VoidEdgeHeld>();
        }
    }

    internal class RVoidEdge : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<VoidEdge>();
        public override int ProtogenesisID => ModContent.ItemType<VoidEdgeEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<VoidEdgeHeld>();
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class VoidEdgeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<VoidEdge>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "VoidEdge_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 70;
            drawTrailTopWidth = 20;
            drawTrailCount = 6;
            Length = 82;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 5;
            ShootSpeed = 10;
        }

        public override void Shoot() {
            int type = ModContent.ProjectileType<GhastlySoulLarge>();
            float knockback = Projectile.knockBack;
            Vector2 velocity = ShootVelocity;
            for (int i = 0; i < 3; i++) {
                velocity = velocity.RotatedByRandom(0.35) * Main.rand.NextFloat(0.9f, 1.1f);
                float ai1 = MathHelper.Lerp(0.75f, 1.25f, Main.rand.NextFloat());
                switch (i) {
                    default:
                    case 0:
                        break;

                    case 1:
                        type = ModContent.ProjectileType<GhastlySoulMedium>();
                        knockback *= 1.25f;
                        break;

                    case 2:
                        type = ModContent.ProjectileType<GhastlySoulSmall>();
                        knockback *= 0.76f;
                        break;
                }

                int proj = Projectile.NewProjectile(Source, ShootSpanPos, velocity
                    , type, Projectile.damage, knockback, Main.myPlayer, 0f, ai1);
                Main.projectile[proj].netUpdate = true;
            }
        }

        public override bool PreInOwnerUpdate() {
            if (Main.rand.NextBool(updateCount)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , DustID.RainbowTorch, 0f, 0f, 0, Color.Plum, Main.rand.NextFloat(0.65f, 1.2f));
                Main.dust[dust].noGravity = true;
            }
            return base.PreInOwnerUpdate();
        }

        private void HitEffect(Entity target, int damageDone) {
            SoundStyle sound = new("CalamityMod/Sounds/Item/PhantomSpirit");
            SoundEngine.PlaySound(sound with { Volume = 0.65f, PitchVariance = 0.3f, Pitch = -0.5f }, target.Center);
            for (int i = 0; i <= 30; i++) {
                Dust dust = Dust.NewDustPerfect(target.Center, 66, new Vector2(0, -18).RotatedByRandom(MathHelper.ToRadians(15f)) * Main.rand.NextFloat(0.1f, 1.9f));
                dust.noGravity = true;
                dust.scale = Main.rand.NextFloat(0.7f, 1.6f);
                Dust dust2 = Dust.NewDustPerfect(target.Center, 66, new Vector2(0, -7).RotatedByRandom(MathHelper.ToRadians(35f)) * Main.rand.NextFloat(0.1f, 1.9f));
                dust2.noGravity = true;
                dust2.scale = Main.rand.NextFloat(0.7f, 1.6f);
            }

            float ai1 = MathHelper.Lerp(0.75f, 1.25f, Main.rand.NextFloat());
            int soulDamage = damageDone / 3;
            Vector2 velocity = new Vector2(0f, -14f).RotatedByRandom(0.65f) * Main.rand.NextFloat(0.9f, 1.1f);
            Projectile.NewProjectile(Source, target.Center + new Vector2(0, 1300f), velocity.RotatedByRandom(0.4f) * Main.rand.NextFloat(0.9f, 1.1f)
                , ModContent.ProjectileType<GhastlySoulLarge>(), soulDamage, 0f, Owner.whoAmI, 0f, ai1);
            Projectile.NewProjectile(Source, target.Center + new Vector2(0, 1300f), velocity.RotatedByRandom(0.4f) * Main.rand.NextFloat(0.9f, 1.1f)
                , ModContent.ProjectileType<GhastlySoulMedium>(), soulDamage, 0f, Owner.whoAmI, 0f, ai1);
            Projectile.NewProjectile(Source, target.Center + new Vector2(0, 1300f), velocity.RotatedByRandom(0.4f) * Main.rand.NextFloat(0.9f, 1.1f)
                , ModContent.ProjectileType<GhastlySoulSmall>(), soulDamage, 0f, Owner.whoAmI, 0f, ai1);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => HitEffect(target, damageDone);

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => HitEffect(target, info.SourceDamage);
    }
}
