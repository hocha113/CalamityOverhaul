using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.AnnihilatingUniverseProj
{
    internal class DivineDevourerIllusionHead : BaseHeldProj
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "AnnihilatingUniverseProj/" + "DivineDevourerIllusionHead";
        public override void SetDefaults() {
            Projectile.height = 54;
            Projectile.width = 54;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 260;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        private Vector2 targetPos;

        public override void AI() {
            if (Projectile.ai[0] == 0 && Projectile.IsOwnedByLocalPlayer()) {
                targetPos = Main.player[Projectile.owner].Center;
                int index = Projectile.whoAmI;
                int maxnum = (int)Projectile.ai[1];
                for (int i = 0; i < maxnum; i++) {
                    int types = i == (maxnum - 1) ? ModContent.ProjectileType<DivineDevourerIllusionTail>()
                        : ModContent.ProjectileType<DivineDevourerIllusionBody>();
                    int proj = Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, Vector2.Zero
                        , types, Projectile.damage / 2, Projectile.knockBack / 2, Projectile.owner, ai1: index);
                    Main.projectile[proj].netUpdate = true;
                    Main.projectile[proj].netUpdate2 = true;
                    index = proj;
                }
                Projectile.ai[0] = 1;
            }
            NPC target = Projectile.Center.FindClosestNPC(1900);
            if (target != null) {
                Projectile.SmoothHomingBehavior(target.Center, 1, 0.2f);
            }
            else {
                Projectile.SmoothHomingBehavior(InMousePos, 1, 0.2f);
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool PreDraw(ref Color lightColor) {
            int tail = ModContent.ProjectileType<DivineDevourerIllusionTail>();
            int body = ModContent.ProjectileType<DivineDevourerIllusionBody>();

            //Main.spriteBatch.SetAdditiveState();

            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == tail || proj.type == body) {
                    Texture2D value = CWRUtils.GetT2DValue(proj.ModProjectile.Texture);
                    Main.EntitySpriteDraw(value, proj.Center - Main.screenPosition, null, Color.White * (proj.timeLeft / 60f), proj.rotation
                        , CWRUtils.GetOrig(value), proj.scale, SpriteEffects.None);
                }
            }
            Texture2D head = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(head, Projectile.Center - Main.screenPosition, null, Color.White * (Projectile.timeLeft / 30f), Projectile.rotation + MathHelper.PiOver2
                , CWRUtils.GetOrig(head) - new Vector2(8, 0), Projectile.scale, SpriteEffects.None);

            //Main.spriteBatch.ResetBlendState();
            return false;
        }
    }
}
