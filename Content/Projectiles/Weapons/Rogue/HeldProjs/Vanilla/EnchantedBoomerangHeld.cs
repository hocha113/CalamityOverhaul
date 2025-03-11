using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs.Vanilla
{
    internal class EnchantedBoomerangHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.EnchantedBoomerang].Value;
        private bool onHit;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetThrowable() {
            CWRUtils.SafeLoadItem(ItemID.EnchantedBoomerang);
            HandOnTwringMode = -30;
            OffsetRoting = MathHelper.ToRadians(30 + 180);
            Projectile.CWR().HitAttribute.WormResistance = true;
            Projectile.CWR().HitAttribute.WormResistanceACValue = 0.6f;
            Projectile.CWR().HitAttribute.NeverCrit = true;
            UseDrawTrail = true;
        }

        public override void PostSetThrowable() {
            if (stealthStrike && Projectile.ai[2] == 0) {
                Projectile.scale *= 1.25f;
            }
        }

        public override void FlyToMovementAI() {
            base.FlyToMovementAI();
            int dust = Dust.NewDust(Projectile.position, (int)Projectile.Size.X, (int)Projectile.Size.Y
                    , DustID.MagicMirror, Projectile.velocity.X * -0.1f, Projectile.velocity.Y * -0.1f);
            Main.dust[dust].noGravity = true;
        }

        public override bool PreThrowOut() {
            if (stealthStrike && Projectile.ai[2] == 0 && Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 2; i++) {
                    Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromAI(), Projectile.Center
                        , Projectile.velocity.RotatedBy(i == 0 ? -0.3f : 0.3f), Type, Projectile.damage, 0.2f, Owner.whoAmI, ai2: 1);
                    proj.CWR().HitAttribute.WormResistanceACValue = 0.3f;
                }
            }
            return base.PreThrowOut();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (stealthStrike && Projectile.ai[2] == 0 && !onHit) {
                onHit = true;
                for (int i = 0; i < 6; i++) {
                    Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromAI()
                        , Projectile.Center + new Vector2(0, -588).RotatedByRandom(0.35f)
                        , new Vector2(0, 20).RotatedByRandom(0.2f) * Main.rand.NextFloat(0.6f, 1f)
                        , ProjectileID.SuperStar, Projectile.damage / 2, 0.2f, Owner.whoAmI, ai2: 1);
                    proj.extraUpdates = 3;
                    proj.scale *= Main.rand.NextFloat(0.3f, 0.6f);
                    proj.DamageType = CWRLoad.RogueDamageClass;
                    proj.CWR().HitAttribute.WormResistance = true;
                    proj.CWR().HitAttribute.WormResistanceACValue = 0.2f;
                    proj.CWR().HitAttribute.NeverCrit = true;
                }
            }
        }
    }
}
