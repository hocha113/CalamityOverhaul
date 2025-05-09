﻿using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs.Vanilla
{
    internal class FlamarangHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Flamarang].Value;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }
        public override void SetThrowable() {
            VaultUtils.SafeLoadItem(ItemID.Flamarang);
            HandOnTwringMode = -30;
            OffsetRoting = MathHelper.ToRadians(30 + 180);
            Projectile.CWR().HitAttribute.WormResistance = 0.6f;
            UseDrawTrail = true;
        }

        public override void PostSetThrowable() {
            if (stealthStrike && Projectile.ai[2] == 0) {
                Projectile.scale *= 1.25f;
            }
        }

        public override bool PreThrowOut() {
            if (stealthStrike && Projectile.ai[2] == 0 && Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 2; i++) {
                    Projectile.NewProjectileDirect(Projectile.GetSource_FromAI(), Projectile.Center
                        , Projectile.velocity.RotatedBy(i == 0 ? -0.3f : 0.3f), Type, Projectile.damage, 0.2f, Owner.whoAmI, ai2: 1);
                }
            }
            return base.PreThrowOut();
        }

        public override void FlyToMovementAI() {
            base.FlyToMovementAI();
            for (int i = 0; i < 3; i++) {
                int dust = Dust.NewDust(Projectile.position, (int)Projectile.Size.X, (int)Projectile.Size.Y, DustID.InfernoFork);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (stealthStrike && Projectile.ai[2] == 0 && Projectile.numHits == 0) {
                Projectile.numHits++;
                Projectile.Explode();
                Projectile projectile = Projectile.NewProjectileDirect(Projectile.GetSource_FromAI(), target.Bottom, new Vector2(0, -6)
                        , ProjectileID.DD2ExplosiveTrapT3Explosion, Projectile.damage, 0.2f, Owner.whoAmI, ai2: 1);
                projectile.CWR().HitAttribute.WormResistance = 0.4f;
            }
        }
    }
}
