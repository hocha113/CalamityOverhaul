using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class Feathers : ModProjectile
    {
        public override string Texture => "CalamityMod/Projectiles/Magic/TradewindsProjectile";
        private Vector2 hitOffsetVr = Vector2.Zero;
        private float hitOffsetRot = 0;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }
        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Projectile.ai[0] == 0) {
                Projectile.velocity += new Vector2(0, 0.1f);
            }
            if (Projectile.ai[0] == 1) {
                Projectile.velocity *= 1.02f;
                Projectile.scale *= 1.002f;
            }
            if (Projectile.ai[0] == 2) {
                Projectile.penetrate = 6;
                Projectile.usesLocalNPCImmunity = true;
                Projectile.localNPCHitCooldown = 60;
                if (Projectile.timeLeft <= 60) {
                    Projectile.tileCollide = true;
                }
                if (Projectile.ai[1] == 1) {
                    NPC hitTarget = CWRUtils.GetNPCInstance((int)Projectile.localAI[0]);
                    if (hitTarget.Alives()) {
                        Projectile.velocity = Vector2.Zero;
                        Projectile.Center = hitTarget.Center + hitOffsetVr;
                        Projectile.rotation = hitOffsetRot;
                    }
                    else {
                        Projectile.Kill();
                    }
                }
                if (Projectile.localAI[0] != 0) {
                    Projectile.velocity *= 0.97f;
                }
            }
            if (Projectile.ai[0] == 3) {
                Projectile.scale *= 1.01f;
                Projectile.velocity *= 1.01f;
                Projectile.alpha -= 30;
                if (Projectile.numHits > 2) {
                    Projectile.Kill();
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.ai[0] == 2) {
                if (Projectile.ai[1] == 0) {
                    Projectile.localAI[0] = target.whoAmI;
                    Projectile.usesLocalNPCImmunity = true;
                    Projectile.localNPCHitCooldown = 20;
                    Projectile.timeLeft = 120;
                    hitOffsetVr = target.Center.To(Projectile.Center);
                    hitOffsetRot = Projectile.rotation;
                    Projectile.ai[1] = 1;
                }
                Projectile.damage -= Projectile.damage / 8;
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height
                    , DustID.YellowTorch, Projectile.velocity.X * 0.1f, Projectile.velocity.Y * 0.1f);
            }
            if (Projectile.ai[0] == 2) {
                Lighting.AddLight(Projectile.Center, Color.Gold.ToVector3());
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = CWRUtils.GetRec(texture);
            Vector2 drawOrigin = rectangle.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = lightColor * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.rotation
                    , drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle
                , lightColor, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
