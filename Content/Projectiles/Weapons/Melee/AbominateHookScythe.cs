using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class AbominateHookScythe : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "BansheeHookScythe";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 38;
            Projectile.height = 38;
            Projectile.scale = 1f;
            Projectile.alpha = 100;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 160;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
        }

        public Vector2 DashVr = Vector2.Zero;
        public ref float Time => ref Projectile.localAI[0];
        public override void AI() {
            Lighting.AddLight(Projectile.Center, (255 - Projectile.alpha) * 0.6f / 255f, 0f, 0f);
            Projectile.localAI[0] += MathHelper.ToRadians(35);

            if (Projectile.ai[0] == 0) {
                Projectile.velocity *= 0.95f;
            }
            if (Projectile.ai[0] == 1) {
                NPC target = CWRUtils.GetNPCInstance((int)Projectile.ai[2]);
                if (target != null) {
                    if (Projectile.ai[1] == 0) {
                        DashVr = Projectile.Center.To(target.Center);
                        Projectile.ai[1] = 1;
                        Time = 0;
                    }
                    if (Projectile.ai[1] == 1) {
                        Projectile.velocity = DashVr.UnitVector() * 32;
                        if (Time > 20) {
                            Projectile.ai[1] = 0;
                            Time = 0;
                        }
                    }
                    Projectile.localAI[1] = target.lifeMax;
                }
                else {
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Vector2 spanPos = Projectile.Center;
                        Projectile.NewProjectile(
                            Projectile.FromObjectGetParent(),
                            spanPos,
                            CWRUtils.GetRandomVevtor(0, 360, 3),
                            ModContent.ProjectileType<AbominateSpirit>(),
                            Projectile.damage,
                            Projectile.knockBack,
                            Projectile.owner,
                            3,
                            Projectile.localAI[1]
                            );
                        Projectile.Kill();
                    }
                }
            }
            Time++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.rand.NextBool(5) && Projectile.numHits < 5) {
                Vector2 offset = CWRUtils.GetRandomVevtor(70, 110, Main.rand.Next(500, 600));
                Vector2 spanPos = target.Center + offset;
                int status = Main.rand.Next(3);
                Projectile.NewProjectile(
                    Projectile.FromObjectGetParent(),
                    spanPos,
                    offset.UnitVector() * -13,
                    ModContent.ProjectileType<AbominateSpirit>(),
                    Projectile.damage / 3,
                    0,
                    Projectile.owner,
                    status
                    );
            }
            Projectile.timeLeft -= 10;
            Projectile.ai[0] = 1;
            if (Projectile.ai[2] == 0)
                Projectile.ai[2] = target.whoAmI;
        }

        public override Color? GetAlpha(Color lightColor) {
            if (Projectile.timeLeft < 85) {
                byte b = (byte)(Projectile.timeLeft * 3);
                byte alpha = (byte)(100f * (b / 255f));
                return new Color(b, b, b, alpha);
            }

            return new Color(255, 255, 255, 100);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                null,
                CWRUtils.RecombinationColor((Color.Red, 0.3f), (Projectile.GetAlpha(Color.Gold), 0.7f)),
                Projectile.localAI[0],
                CWRUtils.GetOrig(mainValue),
                Projectile.scale,
                SpriteEffects.None,
                0
                );
            return false;
        }
    }
}
