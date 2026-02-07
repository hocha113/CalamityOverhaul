using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DevilsDevastationProj
{
    internal class EXOathblade : BaseHeldProj
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "ForbiddenOathbladeProjectile";
        public override void AutoStaticDefaults() => AutoProj.AutoStaticDefaults(this);
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 25;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 58;
            Projectile.height = 58;
            Projectile.aiStyle = ProjAIStyleID.Sickle;
            Projectile.alpha = 100;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            AIType = ProjectileID.DemonScythe;
            Projectile.extraUpdates = 3;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 25;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 0.45f, 0f, 0.45f);

            //更快的速度衰减让其更有冲击力
            Projectile.velocity *= 0.99f;

            //增强粒子效果
            if (Main.rand.NextBool(2)) {
                Dust trail = Dust.NewDustDirect(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height,
                    DustID.ShadowbeamStaff, Projectile.velocity.X * 0.3f, Projectile.velocity.Y * 0.3f);
                trail.noGravity = true;
                trail.scale = 1.5f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 240);
            target.AddBuff(BuffID.ShadowFlame, 120);

            //添加击中粒子效果
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                Dust hitDust = Dust.NewDustPerfect(target.Center, DustID.ShadowbeamStaff, vel, 0, Color.Purple, 1.5f);
                hitDust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() / 2;

            //增强拖尾效果
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 offsetPos = Projectile.oldPos[k].To(Projectile.position);
                Vector2 drawPos = Projectile.Center - Main.screenPosition - offsetPos;
                float trailProgress = k / (float)Projectile.oldPos.Length;
                Color color = Color.Lerp(Color.Pink, Color.Purple, trailProgress) * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, drawOrigin,
                    Projectile.scale * (1f - trailProgress * 0.3f), SpriteEffects.None, 0);
            }

            //增强旋转光效
            VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, texture, Projectile.timeLeft,
                Projectile.Center - Main.screenPosition, null, Color.Pink * 0.7f, Projectile.rotation,
                drawOrigin, Projectile.scale * 1.1f, 0);

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                Projectile.GetAlpha(lightColor), Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
