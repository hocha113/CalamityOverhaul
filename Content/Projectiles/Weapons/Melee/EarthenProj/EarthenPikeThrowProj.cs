using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.EarthenProj
{
    internal class EarthenPikeThrowProj : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "REarthenPikeSpear";
        Player Owner => Main.player[Projectile.owner];
        public Item earthenPike;
        int Dir;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.DamageType = GetInstance<TrueMeleeDamageClass>();
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 3;
        }

        public override bool ShouldUpdatePosition() => Projectile.ai[0] == 0;

        public override void AI() {
            if (Projectile.ai[0] == 0) {
                const float gravity = 0.22f;  // 重力加速度
                const float airResistance = 0.015f;  // 空气阻力
                if (Projectile.ai[1] == 0) {
                    Projectile.velocity = Owner.Center.To(Main.MouseWorld).UnitVector() * 16;
                }
                // 模拟重力
                Projectile.velocity.Y += gravity;
                // 模拟空气阻力
                Projectile.velocity *= (1 - airResistance);
                // 根据速度调整旋转角度
                Projectile.rotation = Projectile.velocity.ToRotation();

                Projectile.ai[1]++;

                Projectile.scale += 0.01f;
                if (Projectile.scale > 3.5f) {
                    Projectile.scale = 3.5f;
                }

                Dir = Math.Sign(Projectile.velocity.X);
            }
            if (Projectile.ai[0] == 1) {
                Projectile.timeLeft = 2;
                Projectile.velocity = Vector2.Zero;

                if (Owner.Center.Distance(Projectile.Center) < 120) {
                    if (Main.myPlayer == Owner.whoAmI) {
                        if (Owner.HeldItem.type == ItemID.None && Main.mouseItem.type == ItemID.None) {
                            Main.mouseItem = earthenPike;
                        }
                        else {
                            Owner.QuickSpawnItem(Projectile.parent(), earthenPike);
                        }
                    }

                    Projectile.Kill();
                }
            }
        }

        public override bool? CanDamage() {
            if (Projectile.numHits > 8) {
                return false;
            }
            return base.CanDamage();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.damage -= 5;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[0] == 0) {
                Projectile.ai[0] = 1;

                if (CWRUtils.GetTile(CWRUtils.WEPosToTilePos(Projectile.Center)).HasTile) {
                    Projectile.position.Y += 16f;
                }
                if (Math.Abs(Projectile.Center.X - Owner.Center.X) < 680 && Math.Abs(Projectile.Center.Y - Owner.Center.Y) < 110) {
                    Owner.velocity += new Vector2(0, -11);
                }
                PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center, new Vector2(0, Main.rand.NextFloat(-2, 2)), 20f, 6f, 20, 1000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
                SpanBall();
            }
            return false;
        }

        public void SpanBall() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                const int wid = 380;
                for (int i = 0; i < 43; i++) {
                    Projectile.NewProjectile(Projectile.parent(), new Vector2(-wid + (wid * 2) / 43f * i, 0) + Projectile.Center, Vector2.Zero
                        , ProjectileType<EarthenBall>(), Projectile.damage, 0, Projectile.owner);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = Request<Texture2D>(Texture).Value;
            if (Projectile.ai[2] == 1) {
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    Main.spriteBatch.Draw(value, Projectile.oldPos[i] - Main.screenPosition + value.Size() / 2, null, Color.White
                        , Projectile.rotation + MathHelper.PiOver4 + MathHelper.PiOver2, value.Size() / 2
                    , Projectile.scale, SpriteEffects.None, 0f);
                }
            }

            Main.spriteBatch.Draw(value, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(lightColor)
                , Projectile.rotation + MathHelper.PiOver4 + MathHelper.PiOver2, value.Size() / 2
                , Projectile.scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
