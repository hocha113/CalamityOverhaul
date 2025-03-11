using CalamityMod;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.AnnihilatingUniverseProj
{
    internal class DivineDevourerIllusionHead : BaseHeldProj
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "AnnihilatingUniverseProj/" + "DivineDevourerIllusionHead";
        private bool spawn;
        private Vector2 targetPos;
        private Vector2 targetOffsetPos {
            get => new Vector2(Projectile.ai[1], Projectile.ai[2]);
            set {
                Projectile.ai[1] = value.X;
                Projectile.ai[2] = value.Y;
            }
        }
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

        public override void AI() {
            if (!spawn && Projectile.IsOwnedByLocalPlayer()) {
                targetOffsetPos = Vector2.Zero;
                targetPos = Owner.Center;
                int index = Projectile.whoAmI;
                int maxnum = (int)Projectile.ai[0];
                for (int i = 0; i < maxnum; i++) {
                    int types = i == (maxnum - 1) ? ModContent.ProjectileType<DivineDevourerIllusionTail>()
                        : ModContent.ProjectileType<DivineDevourerIllusionBody>();
                    int proj = Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, Vector2.Zero
                        , types, Projectile.damage / 2, Projectile.knockBack / 2, Projectile.owner, ai1: index);
                    Main.projectile[proj].netUpdate = true;
                    Main.projectile[proj].netUpdate2 = true;
                    index = proj;
                }
                spawn = true;
            }

            NPC target = Projectile.Center.FindClosestNPC(1900);
            targetPos = (target != null ? target.Center : InMousePos) + targetOffsetPos;
            Projectile.SmoothHomingBehavior(targetPos, 1, 0.2f);
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Projectile.IsOwnedByLocalPlayer() && ++Projectile.localAI[0] > 30) {
                targetOffsetPos = CWRUtils.randVr(180, 300);
                NetUpdate();
                Projectile.localAI[0] = 0;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int tail = ModContent.ProjectileType<DivineDevourerIllusionTail>();
            int body = ModContent.ProjectileType<DivineDevourerIllusionBody>();
            Texture2D head = TextureAssets.Projectile[Type].Value;

            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == tail || proj.type == body) {
                    Texture2D value = TextureAssets.Projectile[proj.type].Value;
                    Main.EntitySpriteDraw(value, proj.Center - Main.screenPosition, null, Color.White * (proj.timeLeft / 60f), proj.rotation
                        , CWRUtils.GetOrig(value), proj.scale, SpriteEffects.None);
                }
            }
            Main.EntitySpriteDraw(head, Projectile.Center - Main.screenPosition, null
                , Color.White * (Projectile.timeLeft / 30f), Projectile.rotation + MathHelper.PiOver2
                , CWRUtils.GetOrig(head) - new Vector2(8, 0), Projectile.scale, SpriteEffects.None);
            return false;
        }
    }
}
