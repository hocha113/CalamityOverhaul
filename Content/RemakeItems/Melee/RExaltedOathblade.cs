using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RExaltedOathblade : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<ExaltedOathblade>();
        public override void SetDefaults(Item item) {
            item.damage = 300;
            item.SetKnifeHeld<ExaltedOathbladeHeld>();
        }
    }

    internal class ExaltedOathbladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<ExaltedOathblade>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "ExaltedOathblade_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 70;
            drawTrailTopWidth = 20;
            drawTrailCount = 6;
            Length = 82;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 5;
            ShootSpeed = 11;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, Owner.Center, UnitToMouseV * 6
                , ModContent.ProjectileType<EXOathblade2>(), Projectile.damage
                , Projectile.knockBack, Owner.whoAmI);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 150);
            target.AddBuff(BuffID.OnFire, 300);
            if (hit.Crit) {
                target.AddBuff(BuffID.ShadowFlame, 450);
                target.AddBuff(BuffID.OnFire, 900);
                Owner.ApplyDamageToNPC(target, Projectile.damage, 0f, 0, false);
                float firstDustScale = 1.7f;
                float secondDustScale = 0.8f;
                float thirdDustScale = 2f;
                Vector2 dustRotation = (target.rotation - MathHelper.PiOver2).ToRotationVector2();
                Vector2 dustVelocity = dustRotation * target.velocity.Length();
                SoundEngine.PlaySound(SoundID.Item14, target.Center);
                for (int i = 0; i < 40; i++) {
                    int swingDust = Dust.NewDust(target.position, target.width, target.height, DustID.ShadowbeamStaff, 0f, 0f, 200, default, firstDustScale);
                    Dust dust = Main.dust[swingDust];
                    dust.position = target.Center + Vector2.UnitY.RotatedByRandom(Math.PI) * (float)Main.rand.NextDouble() * target.width / 2f;
                    dust.noGravity = true;
                    dust.velocity.Y -= 4.5f;
                    dust.velocity *= 3f;
                    dust.velocity += dustVelocity * Main.rand.NextFloat();
                    swingDust = Dust.NewDust(target.position, target.width, target.height, DustID.ShadowbeamStaff, 0f, 0f, 100, default, secondDustScale);
                    dust.position = target.Center + Vector2.UnitY.RotatedByRandom(Math.PI) * (float)Main.rand.NextDouble() * target.width / 2f;
                    dust.velocity.Y -= 3f;
                    dust.velocity *= 2f;
                    dust.noGravity = true;
                    dust.fadeIn = 1f;
                    dust.color = Color.Crimson * 0.5f;
                    dust.velocity += dustVelocity * Main.rand.NextFloat();
                }
                for (int j = 0; j < 20; j++) {
                    int swingDust2 = Dust.NewDust(target.position, target.width, target.height, DustID.ShadowbeamStaff, 0f, 0f, 0, default, thirdDustScale);
                    Dust dust = Main.dust[swingDust2];
                    dust.position = target.Center + Vector2.UnitX.RotatedByRandom(Math.PI).RotatedBy((double)target.velocity.ToRotation(), default) * target.width / 3f;
                    dust.noGravity = true;
                    dust.velocity.Y -= 1.5f;
                    dust.velocity *= 0.5f;
                    dust.velocity += dustVelocity * (0.6f + 0.6f * Main.rand.NextFloat());
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<Shadowflame>(), 450);
            target.AddBuff(BuffID.OnFire, 900);
            SoundEngine.PlaySound(SoundID.Item14, target.Center);
        }
    }

    internal class EXOathblade2 : BaseHeldProj
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "ForbiddenOathbladeProjectile";
        public Vector2 MoveVector2;
        public Vector2 FromeOwnerMoveSet;
        public Vector2 pos = new Vector2(0, -5);
        public ref float Rand => ref Projectile.localAI[0];
        private int Time;
        private int Time2;
        private bool shoot;
        private bool shoot2;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.DontCancelChannelOnKill[Type] = true;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 9;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }
        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.penetrate = 1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 60;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 1;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Rand = Main.rand.Next(50, 100);
            double angle = Main.rand.NextDouble() * 2d * Math.PI;
            MoveVector2.X = (float)(Math.Sin(angle) * Rand);
            MoveVector2.Y = (float)(Math.Cos(angle) * Rand);
            Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Projectile.spriteDirection = Main.rand.NextBool() ? 1 : -1;

        }

        public override bool? CanCutTiles() => Projectile.ai[0] != 0;

        public override bool? CanHitNPC(NPC target) => !target.friendly && Projectile.ai[0] != 0 ? null : false;

        public override void AI() {
            Projectile.rotation += 0.1f * Projectile.spriteDirection;

            if (Time == 0) {
                pos = ToMouse.UnitVector() * -5;
                FromeOwnerMoveSet = UnitToMouseV * 124;
            }
            FromeOwnerMoveSet = Vector2.Lerp(FromeOwnerMoveSet, UnitToMouseV * 124, 0.01f);
            if (!shoot) {

            }
            else {
                float targetA = ToMouseA;
                if (shoot2) {
                    targetA = Projectile.velocity.ToRotation();
                }

                if (++Time2 > 60) {
                    shoot2 = true;
                }
            }

            if (Projectile.alpha > 0) {
                Projectile.alpha -= 5;
            }

            if (Projectile.ai[1]++ < 60) {
                pos *= 0.98f;
            }
            else {
                if (Projectile.localAI[1] == 0) {
                    pos.Y += 0.03f;
                    if (pos.Y > 0.7f) {
                        Projectile.localAI[1] = 1;
                    }

                }
                else if (Projectile.localAI[1] == 1) {
                    pos.Y -= 0.03f;
                    if (pos.Y < -0.7f) {
                        Projectile.localAI[1] = 0;
                    }
                }
            }
            if (Projectile.ai[0] == 0) {
                Projectile.timeLeft = 200;
                Projectile.position = Owner.GetPlayerStabilityCenter() + MoveVector2 + FromeOwnerMoveSet;
                MoveVector2 += pos;
                if (shoot && shoot2 && Projectile.alpha <= 0) {
                    SoundEngine.PlaySound(SoundID.Item70, Projectile.position);
                    Projectile.velocity = Projectile.DirectionTo(InMousePos) * 20;
                    Projectile.ai[0] = 1;
                }
            }
            if (!DownLeft || Time > 60) {
                shoot = true;
            }

            Time++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 180);
            target.AddBuff(BuffID.ShadowFlame, 90);
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode();
            CWRDust.SplashDust(Projectile, 21, DustID.FireworksRGB, DustID.Firework_Blue, 13, Main.DiscoColor);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() / 2;
            if (Projectile.ai[0] == 1) {
                for (int k = 0; k < Projectile.oldPos.Length; k++) {
                    Vector2 offsetPos = Projectile.oldPos[k].To(Projectile.position);
                    Vector2 drawPos = Projectile.Center - Main.screenPosition - offsetPos;
                    Color color = Projectile.GetAlpha(Color.Pink) * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
                    Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
                }
            }

            VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, texture, Time, Projectile.Center - Main.screenPosition
                , null, Color.Pink, Projectile.rotation, drawOrigin, Projectile.scale, 0);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(lightColor)
                , Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
