using CalamityMod;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class DeathLaser : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder3;
        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 5000;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.scale = 1;
            Projectile.alpha = 80;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override bool ShouldUpdatePosition() => false;

        public int Status { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public int HeldProj { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }
        public int Time { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }
        private ref float wit => ref Projectile.localAI[0];
        public float Leng {
            get => Projectile.localAI[1];
            set => Projectile.localAI[1] = value;
        }

        public override void AI() {
            if (Leng == 0)
                Leng = 5000;
            Projectile.alpha += 15;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Vector2 toRot = Projectile.rotation.ToRotationVector2();
            Vector2 ordPos = Projectile.Center;

            wit = (10 - Projectile.timeLeft) / 15f;

            if (Status == 0) {
                Projectile heldBow = CWRUtils.GetProjectileInstance(HeldProj);
                if (!Owner.Alives() || !(heldBow != null && heldBow.type == ModContent.ProjectileType<DeathwindHeldProj>())) {
                    Projectile.Kill();
                    return;
                }
                Projectile.Center = heldBow.Center;

                for (int i = 0; i < 100; i++) {
                    Vector2 offsetPos = toRot * i * 16;
                    Lighting.AddLight(ordPos + offsetPos, 0.4f, 0.2f, 0.4f);
                    Dust obj = Main.dust[Dust.NewDust(Projectile.position + offsetPos, 26, 26
                            , Main.rand.NextBool(3) ? 56 : 242, Projectile.velocity.X, Projectile.velocity.Y, 100)];
                    obj.velocity = Vector2.Zero;
                    obj.position -= Projectile.velocity / 5f * i;
                    obj.noGravity = true;
                    obj.scale = 0.8f;
                    obj.noLight = true;
                }
            }

            Time++;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(
                        targetHitbox.TopLeft(),
                        targetHitbox.Size(),
                        Projectile.Center,
                        Projectile.rotation.ToRotationVector2() * Leng + Projectile.Center,
                        8,
                        ref point
                    );
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D body = CWRUtils.GetT2DValue("CalamityMod/ExtraTextures/Lasers/UltimaRayMid");
            Texture2D head = CWRUtils.GetT2DValue("CalamityMod/ExtraTextures/Lasers/UltimaRayEnd");
            Texture2D dons = CWRUtils.GetT2DValue("CalamityMod/ExtraTextures/Lasers/UltimaRayStart");
            Color color = CalamityUtils.ColorSwap(new Color(119, 210, 255), new Color(247, 119, 255), 0.9f); ;

            float rots = Projectile.rotation - MathHelper.PiOver2;
            Vector2 slp = new Vector2(wit, 1);
            Vector2 dir = Projectile.rotation.ToRotationVector2();

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.ZoomMatrix);

            Main.EntitySpriteDraw(
                dons,
                Projectile.Center - Main.screenPosition,
                null,
                color,
                rots,
                new Vector2(dons.Width * 0.5f, 0),
                slp,
                SpriteEffects.None,
                0
                );

            Main.EntitySpriteDraw(
                body,
                Projectile.Center - Main.screenPosition + dir * dons.Height,
                new Rectangle(0, Time * -5, body.Width, (int)(Leng + 1)),
                color,
                rots,
                new Vector2(body.Width * 0.5f, 0),
                slp,
                SpriteEffects.None,
                0
                );

            Main.EntitySpriteDraw(
                head,
                Projectile.Center + dir * Leng - Main.screenPosition + dir * dons.Height,
                null,
                color,
                rots,
                new Vector2(body.Width * 0.5f, 0),
                slp,
                SpriteEffects.None,
                0
                );
            Main.spriteBatch.ResetBlendState();
            return false;
        }
    }
}
