﻿using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs.Vanilla
{
    internal class ShroomerangHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Shroomerang].Value;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }
        public override void SetThrowable() {
            VaultUtils.SafeLoadItem(ItemID.Shroomerang);
            HandOnTwringMode = -30;
            OffsetRoting = MathHelper.ToRadians(30 + 180);
            UseDrawTrail = true;
            Projectile.CWR().HitAttribute.WormResistance = 0.6f;
            FlightSpeedTime = 22;
        }

        public override void PostSetThrowable() {
            if (stealthStrike) {
                Projectile.scale *= 1.25f;
            }
        }

        public override void FlyToMovementAI() {
            base.FlyToMovementAI();
            int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.FungiHit);
            Main.dust[dust].noGravity = true;
        }

        public override bool PreDeparture() {
            if (stealthStrike) {
                if (++Projectile.ai[2] > 6 && Projectile.IsOwnedByLocalPlayer()) {
                    float rand = Main.rand.NextFloat(MathHelper.TwoPi);
                    for (int i = 0; i < 3; i++) {
                        Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromAI()
                            , Projectile.Center, (MathHelper.TwoPi / 3 * i + rand).ToRotationVector2() * 6
                            , ProjectileID.Mushroom, Projectile.damage / 5, 0.2f, Owner.whoAmI);
                        proj.DamageType = CWRLoad.RogueDamageClass;
                    }
                    Projectile.ai[2] = 0;
                }
            }

            return true;
        }
    }
}
