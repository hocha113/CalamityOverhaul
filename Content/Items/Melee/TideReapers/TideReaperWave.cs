using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.TideReapers
{
    /// <summary>
    /// 镰渊新月。镰渊飞行途中甩出的双臂涡旋，短暂直冲后减速并追猎最近敌人
    /// </summary>
    internal class TideReaperWave : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "TideReaperWave";

        private const int MaxLife = 80;

        private int Age => MaxLife - Projectile.timeLeft;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = MaxLife;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
            Projectile.scale = 0.72f;
        }

        public override void AI() {
            Projectile.rotation += 0.3f;

            //直冲一小段后减速,再靠追踪咬住目标,不做匀速直线
            if (Age > 8) {
                float speed = Projectile.velocity.Length();
                if (speed > 6.5f) {
                    Projectile.velocity *= 0.984f;
                }
                NPC target = Projectile.Center.FindClosestNPC(500f);
                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1.03f, 0.06f);
                }
            }

            Lighting.AddLight(Projectile.Center, 0.07f, 0.24f, 0.34f);

            if (VaultUtils.isServer) {
                return;
            }
            if (Age % 3 == 0) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , -Projectile.velocity * 0.08f
                    , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.2f, 0.36f))
                    .Configure(10, 1.3f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 150);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(target.Center, Main.rand.NextVector2Circular(3.5f, 3.5f)
                    , AbyssrendFX.Body, Main.rand.NextFloat(0.26f, 0.45f))
                    .Configure(12);
            }
            PRTLoader.NewParticle<PRT_AbyssSpark>(target.Center, Main.rand.NextVector2Circular(3f, 3f)
                , AbyssrendFX.Cyan, Main.rand.NextFloat(0.7f, 1f))
                .Configure(9);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center, Main.rand.NextVector2Circular(2.5f, 2.5f)
                    , AbyssrendFX.Deep, Main.rand.NextFloat(0.25f, 0.42f))
                    .Configure(12);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            //蓝焰新月是发光体,残影走加色,主体亮化不吃满环境压暗
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    continue;
                }
                float prog = 1f - k / (float)Projectile.oldPos.Length;
                Color ghost = new Color(AbyssrendFX.Cyan.R, AbyssrendFX.Cyan.G, AbyssrendFX.Cyan.B, 0) * (prog * 0.3f);
                Main.EntitySpriteDraw(tex, Projectile.oldPos[k] + Projectile.Size / 2f - Main.screenPosition, null
                    , ghost, Projectile.rotation - k * 0.18f, origin
                    , Projectile.scale * MathHelper.Lerp(0.7f, 1f, prog), SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null
                , Color.Lerp(lightColor, Color.White, 0.5f), Projectile.rotation, origin
                , Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
