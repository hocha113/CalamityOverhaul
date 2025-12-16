using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class NurgleArrow : ModProjectile
    {
        public override bool IsLoadingEnabled(Mod mod) => CWRRef.Has;
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "ContagionArrow";
        private int addBallTimer = 10;
        private float rot;
        private Vector2 pos;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.penetrate = 3;
            Projectile.MaxUpdates = 3;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.timeLeft = 420;
            Projectile.SetProjPointBlankShotDuration(18);
        }

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                Projectile.rotation = (float)Math.Atan2(Projectile.velocity.Y, Projectile.velocity.X) + 1.57f;
                addBallTimer--;
                if (addBallTimer <= 0) {
                    if (Projectile.IsOwnedByLocalPlayer() && Main.player[Projectile.owner].ownedProjectileCounts[ModContent.ProjectileType<NurgleTheOfBall>()] < 100 && Projectile.ai[1] == 0) {
                        _ = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center.X, Projectile.Center.Y, 0f, 0f
                            , ModContent.ProjectileType<NurgleTheOfBall>(), Projectile.damage, Projectile.knockBack, Projectile.owner, 0f, 0f);
                    }
                    addBallTimer = 10;
                }
                Lighting.AddLight(Projectile.Center, (255 - Projectile.alpha) * 0.15f / 255f, (255 - Projectile.alpha) * 0.25f / 255f, (255 - Projectile.alpha) * 0f / 255f);
                if (Projectile.ai[0] <= 60f) {
                    Projectile.ai[0] += 1f;
                    return;
                }
            }
            else {
                Projectile.rotation = rot;
                Projectile.position = pos;
                if (Projectile.IsOwnedByLocalPlayer() && Main.player[Projectile.owner].ownedProjectileCounts[ModContent.ProjectileType<NurgleTheOfBall>()] < 100 && Projectile.timeLeft % 30 == 0) {
                    _ = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, VaultUtils.RandVr(3, 5)
                        , ModContent.ProjectileType<NurgleTheOfBall>(), Projectile.damage, Projectile.knockBack, Projectile.owner, 1, 0f);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(CWRID.Buff_Plague, 6000);
            CWRNpc npc = target.CWR();
            npc.CreateHitPlayer = Main.player[Projectile.owner];
            npc.ContagionOnHitNum++;
            if (npc.ContagionOnHitNum > 30) {
                npc.ContagionOnHitNum = 30;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(CWRID.Buff_Plague, 600);
        }

        public override void OnKill(int timeLeft) {
            int inc;
            for (int i = 4; i < 31; i = inc + 1) {
                float oldXPos = Projectile.oldVelocity.X * (30f / i);
                float oldYPos = Projectile.oldVelocity.Y * (30f / i);
                int killDust = Dust.NewDust(new Vector2(Projectile.oldPosition.X - oldXPos, Projectile.oldPosition.Y - oldYPos), 8, 8, DustID.Blood, Projectile.oldVelocity.X, Projectile.oldVelocity.Y, 100, default, 1.8f);
                Main.dust[killDust].noGravity = true;
                Dust dust2 = Main.dust[killDust];
                dust2.velocity *= 0.5f;
                dust2.color = Color.Green;
                killDust = Dust.NewDust(new Vector2(Projectile.oldPosition.X - oldXPos, Projectile.oldPosition.Y - oldYPos), 8, 8, DustID.JungleSpore, Projectile.oldVelocity.X, Projectile.oldVelocity.Y, 100, default, 1.4f);
                dust2 = Main.dust[killDust];
                dust2.velocity *= 0.05f;
                dust2.noGravity = true;
                inc = i;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[1] == 0) {
                Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
                _ = SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
                Projectile.ai[1]++;
                Projectile.ai[2] = 1;
                rot = Projectile.rotation;
                pos = Projectile.position;
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            CWRRef.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            return false;
        }
    }
}
