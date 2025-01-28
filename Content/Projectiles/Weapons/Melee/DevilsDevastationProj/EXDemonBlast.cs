using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DevilsDevastationProj
{
    internal class EXDemonBlast : BaseHeldProj
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "DemonBlast";
        public Vector2 MoveVector2;
        public Vector2 FromeOwnerMoveSet;
        public Vector2 pos = new Vector2(0, -1);
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
            Projectile.penetrate = 16;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Rand = Main.rand.Next(50, 100);
            double angle = Main.rand.NextDouble() * 2d * Math.PI;
            MoveVector2.X = (float)(Math.Sin(angle) * Rand);
            MoveVector2.Y = (float)(Math.Cos(angle) * Rand);
            Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Projectile.spriteDirection = Main.rand.NextBool() ? 1 : -1;
            Projectile.CWR().Viscosity = true;
        }

        public override bool? CanCutTiles() => Projectile.ai[0] != 0;

        public override bool? CanHitNPC(NPC target) => !target.friendly && Projectile.ai[0] != 0 ? null : false;

        public override void AI() {
            if (Time == 0) {
                pos = UnitToMouseV * 2;
                FromeOwnerMoveSet = UnitToMouseV * 124;
            }

            FromeOwnerMoveSet = Vector2.Lerp(FromeOwnerMoveSet, UnitToMouseV * 124, 0.01f);

            if (!shoot) {
                Projectile.rotation += (Projectile.ai[0] == 0 ? 0.01f : 0.2f) * Projectile.spriteDirection;
            }
            else {
                float targetA = ToMouseA;
                if (shoot2) {
                    targetA = Projectile.velocity.ToRotation();
                }
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetA + MathHelper.PiOver4, 0.2f);
                if (++Time2 > 60) {
                    Projectile.extraUpdates = 3;
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

        public override void OnKill(int timeLeft) {
            Projectile.Explode();
            CWRDust.SplashDust(Projectile, 21, DustID.ShadowbeamStaff, DustID.ShadowbeamStaff, 13, Main.DiscoColor);
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
