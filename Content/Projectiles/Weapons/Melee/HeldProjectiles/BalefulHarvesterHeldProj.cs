using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class BalefulHarvesterHeldProj : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "BalefulHarvester";
        public const float MaxChargeTime = 20f;

        private Player Owner => Main.player[Projectile.owner];

        private Item balefulHarvester => Owner.ActiveItem();

        private Item IndsItem;
        private int Dir;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = GetInstance<TrueMeleeDamageClass>();
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 3;
        }

        public override bool ShouldUpdatePosition() {
            return Projectile.ai[2] != 0;
        }

        public override void AI() {
            if (Owner is null || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (Projectile.ai[2] == 0) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    InOwner();
                }
            }
            if (Projectile.ai[2] == 1) {
                const float gravity = 0.22f;  // 重力加速度
                const float airResistance = 0.015f;  // 空气阻力

                if (Projectile.ai[1] == 0) {
                    Projectile.velocity = Owner.Center.To(Main.MouseWorld).UnitVector() * 16;
                }
                // 模拟重力
                Projectile.velocity.Y += gravity;
                // 模拟空气阻力
                Projectile.velocity *= 1 - airResistance;
                // 更新位置
                Projectile.position += Projectile.velocity;
                // 根据速度调整旋转角度
                Projectile.rotation += Math.Sign(Projectile.velocity.X) * 0.25f;

                Projectile.ai[1]++;

                Projectile.scale += 0.01f;

                Dir = Math.Sign(Projectile.velocity.X);
            }
            if (Projectile.ai[2] == 2) {
                Projectile.timeLeft = 2;
                Projectile.velocity = Vector2.Zero;

                if (Owner.Center.Distance(Projectile.Center) < 120) {
                    if (Main.myPlayer == Owner.whoAmI) {
                        if (Owner.HeldItem.type == ItemID.None && Main.mouseItem.type == ItemID.None) {
                            Main.mouseItem = IndsItem;
                        }
                        else {
                            _ = Owner.QuickSpawnItem(Projectile.parent(), IndsItem);
                        }
                    }

                    Projectile.Kill();
                }
            }

            Lighting.AddLight(Projectile.Center, Color.Yellow.ToVector3());
        }

        public void InOwner() {
            Projectile.timeLeft = 600;
            Projectile.Center = Owner.Center + (Projectile.rotation.ToRotationVector2() * 32);
            if (Owner.PressKey(false)) {
                if (ContentConfig.Instance.ForceReplaceResetContent) {
                    if (balefulHarvester.type != ItemType<CalamityMod.Items.Weapons.Melee.BalefulHarvester>()) {
                        Projectile.Kill();
                    }
                }
                else {
                    if (balefulHarvester.type != ItemType<BalefulHarvester>()) {
                        Projectile.Kill();
                    }
                }

                float rotSpeed = 0.01f + (Projectile.ai[1] * 0.001f);
                if (rotSpeed > 0.3f) {
                    rotSpeed = 0.3f;
                }

                Projectile.ai[0] += rotSpeed;
                Projectile.ai[1]++;
                Projectile.rotation = Projectile.ai[0];
                Owner.direction = Math.Sign((Projectile.rotation.ToRotationVector2() * 32).X);
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
                Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
                if ((int)Projectile.ai[1] % (60 - (int)(rotSpeed * 160)) == 0) {
                    SoundStyle sound = SoundID.Item1;
                    _ = SoundEngine.PlaySound(sound, Owner.Center);
                }
            }
            else {
                if (Projectile.ai[1] < 20) {
                    Projectile.Kill();
                }
                Projectile.ai[2] = 1;
                Projectile.ai[1] = 0;
                Dir = Math.Sign(Projectile.velocity.X);
                IndsItem = balefulHarvester.Clone();
                balefulHarvester.TurnToAir(true);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[2] == 1) {
                Projectile.ai[2] = 2;
                Projectile.rotation = Dir * MathHelper.PiOver2;
                if (CWRUtils.GetTile(CWRUtils.WEPosToTilePos(Projectile.Center)).HasTile) {
                    Projectile.position.Y += 16f;
                }
                if (Math.Abs(Projectile.Center.X - Owner.Center.X) < 680 && Math.Abs(Projectile.Center.Y - Owner.Center.Y) < 110) {
                    Owner.velocity += new Vector2(0, -11);
                }
                PunchCameraModifier modifier = new(Projectile.Center, new Vector2(0, Main.rand.NextFloat(-2, 2)), 20f, 6f, 20, 1000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
                SpanBall();
            }
            return false;
        }

        public void SpanBall() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 73; i++) {
                    _ = Projectile.NewProjectile(Projectile.parent(), new Vector2(-680 + (1360 / 73f * i), 0) + Projectile.Center, Vector2.Zero
                        , ProjectileType<BalefulBall>(), Projectile.damage, 0, Projectile.owner);
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

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = Request<Texture2D>(Texture).Value;
            if (Projectile.ai[2] == 1) {
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    Main.spriteBatch.Draw(value, Projectile.oldPos[i] - Main.screenPosition + (value.Size() / 2), null, Color.White
                        , Projectile.rotation + (Projectile.ai[2] == 0 ? MathHelper.PiOver2 : 0), value.Size() / 2
                    , Projectile.scale, Dir == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
                }
            }

            Main.spriteBatch.Draw(value, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(lightColor)
                , Projectile.rotation + (Projectile.ai[2] == 0 ? MathHelper.PiOver2 : 0), value.Size() / 2
                , Projectile.scale, Dir == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
            return false;
        }
    }
}
