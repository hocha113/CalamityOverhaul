using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class ElementalRay : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "RayBeam";
        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 5000;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.scale = 1;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public int Status { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public int Time { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }
        public float Lengs { get => Projectile.localAI[0]; set => Projectile.localAI[0] = value; }

        private Vector2 targetPos = Vector2.Zero;
        private Vector2 newTargetPos = Vector2.Zero;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(targetPos.X);
            writer.Write(targetPos.Y);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            targetPos.X = reader.ReadSingle();
            targetPos.Y = reader.ReadSingle();
        }

        public override void AI() {
            if (!Owner.Alives()) {
                Projectile.Kill();
                return;
            }
            Vector2 offsetToVr = (MathHelper.ToRadians(Time * 7) + MathHelper.TwoPi / 5f * Projectile.localAI[1]).ToRotationVector2();
            if (Projectile.IsOwnedByLocalPlayer()) {
                float wit = Projectile.Center.To(Main.MouseWorld).Length() / 10;
                if (wit > 64) wit = 64;
                targetPos = Main.MouseWorld + offsetToVr * wit;
                if (newTargetPos == Vector2.Zero)
                    newTargetPos = Main.MouseWorld;
                if (newTargetPos.To(targetPos).LengthSquared() > 9)
                    Projectile.netUpdate = true;
            }

            newTargetPos = Vector2.Lerp(newTargetPos, targetPos, 0.5f);

            Vector2 toPos = Projectile.Center.To(newTargetPos);
            Projectile.rotation = toPos.ToRotation();
            Lengs = toPos.Length();

            Time++;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(
                        targetHitbox.TopLeft(),
                        targetHitbox.Size(),
                        Projectile.Center,
                        Projectile.rotation.ToRotationVector2() * Lengs + Projectile.Center,
                        8,
                        ref point
                    );
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D body = CWRUtils.GetT2DValue(Texture + "Body");
            Texture2D head = CWRUtils.GetT2DValue(Texture + "Head");
            Texture2D dons = CWRUtils.GetT2DValue(Texture + "Don");
            Color color = Color.White;
            float lerps = MathF.Sin(MathHelper.ToRadians(Time * Status));
            switch (Status) {
                case 0:
                    color = Color.Lerp(new Color(255, 127, 80), Color.White, lerps);
                    break;
                case 1:
                    color = Color.Lerp(new Color(255, 215, 0), Color.WhiteSmoke, lerps);
                    break;
                case 2:
                    color = Color.Lerp(new Color(0, 0, 255), new Color(253, 245, 230), lerps);
                    break;
                case 3:
                    color = Color.Lerp(new Color(85, 107, 47), Color.Red, lerps);
                    break;
                case 4:
                    color = Color.Lerp(Color.Red, new Color(255, 127, 80), lerps);
                    break;
            }

            float rots = Projectile.rotation - MathHelper.PiOver2;
            float slp = 1;
            Vector2 dir = Projectile.rotation.ToRotationVector2();

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.ZoomMatrix);

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
                new Rectangle(0, Time * -5, body.Width, (int)Lengs + 1),
                color,
                rots,
                new Vector2(body.Width * 0.5f, 0),
                slp,
                SpriteEffects.None,
                0
                );

            Main.EntitySpriteDraw(
                head,
                Projectile.Center + dir * Lengs - Main.screenPosition + dir * dons.Height,
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
