using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria.Graphics.Shaders;
using Terraria;
using Terraria.ModLoader;
using System.Collections.Generic;
using CalamityMod;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.AnnihilatingUniverseProj
{
    internal class DivineDevourerIllusionHead : ModProjectile
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
                    int proj = Projectile.NewProjectile(Projectile.parent(), Projectile.Center, Vector2.Zero
                        , types, Projectile.damage / 2, Projectile.knockBack / 2, Projectile.owner, ai1: index);
                    Main.projectile[proj].netUpdate = true;
                    Main.projectile[proj].netUpdate2 = true;
                    index = proj;
                }
                Projectile.ai[0] = 1;
            }
            NPC target = Projectile.Center.FindClosestNPC(1900);
            if (target != null) {
                Projectile.ChasingBehavior2(target.Center, 1, 0.2f);
            }
            Projectile.ChasingBehavior2(targetPos, 1.001f, 0.15f);
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D head = CWRUtils.GetT2DValue(Texture);
            Main.spriteBatch.SetAdditiveState();

            Main.EntitySpriteDraw(head, Projectile.Center - Main.screenPosition, null, Color.White * (Projectile.timeLeft / 30f), Projectile.rotation + MathHelper.PiOver2
                , CWRUtils.GetOrig(head) - new Vector2(8, 0), Projectile.scale, SpriteEffects.None);
            Main.spriteBatch.ResetBlendState();
            return false;
        }
    }
}
