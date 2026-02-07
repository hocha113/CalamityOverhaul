using CalamityOverhaul.Content.RemakeItems;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
{
    internal class BalefulHarvesterHeldThrow : BaseHeldProj
    {
        public override LocalizedText DisplayName => ItemLoader.GetItem(CWRItemOverride.GetCalItemID("BalefulHarvester")).GetLocalization("DisplayName");
        public override string Texture => CWRConstant.Cay_Wap_Melee + "BalefulHarvester";
        public override bool IsLoadingEnabled(Mod mod) => CWRItemOverride.GetCalItemID("BalefulHarvester") > ItemID.None;
        public override void AutoStaticDefaults() => AutoProj.AutoStaticDefaults(this);
        public const float MaxChargeTime = 20f;
        private Item balefulHarvester => Owner.GetItem();
        private Item IndsItem;
        private int Dir;
        private const float gravity = 0.22f;  //重力加速度
        private const float airResistance = 0.015f;  //空气阻力
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = CWRRef.GetTrueMeleeDamageClass();
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 3;
        }

        public override bool ShouldUpdatePosition() => Projectile.ai[2] != 0;

        public override bool ExtraPreSet() {
            if (Owner is null || Owner.dead) {
                Projectile.Kill();
                return false;
            }
            return true;
        }

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    InOwner();
                }
            }
            if (Projectile.ai[2] == 1) {
                if (Projectile.ai[1] == 0) {
                    Projectile.velocity = Owner.Center.To(Main.MouseWorld).UnitVector() * 16;
                }
                //模拟重力
                Projectile.velocity.Y += gravity;
                //模拟空气阻力
                Projectile.velocity *= 1 - airResistance;
                //更新位置
                Projectile.position += Projectile.velocity;
                //根据速度调整旋转角度
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
                            _ = Owner.QuickSpawnItem(Projectile.FromObjectGetParent(), IndsItem);
                        }
                    }

                    Projectile.Kill();
                }
            }

            Lighting.AddLight(Projectile.Center, Color.Yellow.ToVector3());
        }

        public void InOwner() {
            if (DownRight) {
                if (balefulHarvester.type != CWRItemOverride.GetCalItemID("BalefulHarvester")) {
                    Projectile.Kill();
                    return;
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
                    return;
                }
                Projectile.ai[2] = 1;
                Projectile.ai[1] = 0;
                Dir = Math.Sign(Projectile.velocity.X);
                IndsItem = balefulHarvester.Clone();
                IndsItem.Initialize();
                balefulHarvester.Initialize();
                IndsItem.CWR().ai[0] = balefulHarvester.CWR().ai[0];
                balefulHarvester.TurnToAir(true);
            }
            Projectile.timeLeft = 600;
            Projectile.Center = Owner.GetPlayerStabilityCenter() + (Projectile.rotation.ToRotationVector2() * 42);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[2] == 1) {
                Projectile.ai[2] = 2;
                Projectile.rotation = Dir * MathHelper.PiOver2;
                if (Framing.GetTileSafely(Projectile.Center).HasTile) {
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
                    _ = Projectile.NewProjectile(Projectile.FromObjectGetParent(), new Vector2(-680 + (1360 / 73f * i), 0) + Projectile.Center, Vector2.Zero
                        , ProjectileType<BalefulBall>(), Projectile.damage, 0, Projectile.owner);
                }
            }
        }

        public override bool? CanDamage() => Projectile.numHits > 8 ? false : base.CanDamage();

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Projectile.damage -= 5;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            float rot = Projectile.rotation + (Projectile.ai[2] == 0 ? MathHelper.PiOver2 : 0) + MathHelper.ToRadians(30);
            if (Projectile.ai[2] == 1) {
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    Main.spriteBatch.Draw(value, Projectile.oldPos[i] - Main.screenPosition + (value.Size() / 2), null, Color.White
                        , rot, value.Size() / 2, Projectile.scale, Dir == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
                }
            }

            Main.spriteBatch.Draw(value, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(lightColor)
                , rot, value.Size() / 2, Projectile.scale, Dir == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
            return false;
        }
    }
}
