using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 焚焰弧弹：vanilla 467 火球精灵做体，慢启动增压<br/>
    /// ai[0]=1 时前 20 帧弱追踪（限转速，之后直线——公平阀）
    /// </summary>
    internal class CultistFlameBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CultistBossFireBall;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 4;
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 330;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            int age = 330 - Projectile.timeLeft;

            //弱追踪窗口：限转速，只修正不锁死
            if (Projectile.ai[0] == 1f && age < 20) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.Alives()) {
                    float speed = Projectile.velocity.Length();
                    float desired = (target.Center - Projectile.Center).ToRotation();
                    Projectile.velocity = Projectile.velocity.ToRotation().AngleTowards(desired, 0.045f).ToRotationVector2() * speed;
                }
            }

            //慢启动增压：飞行期速度有演变
            if (age > 14 && Projectile.velocity.Length() < 13.5f) {
                Projectile.velocity *= 1.028f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //帧动画
            if (++Projectile.frameCounter >= 4) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }

            //沿途余烬
            if (!VaultUtils.isServer && Main.rand.NextBool(4) && CultistMotion.OnScreen(Projectile.Center, 200f)) {
                PRTLoader.NewParticle<PRT_CultistEmber>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -Projectile.velocity * 0.1f, Color.Lerp(CultistMotion.FlameCore, CultistMotion.FlameEdge, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(14, 24), 0.06f);
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.FlameEdge.ToVector3() * 0.5f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire3, 150);
        }

        public override void OnKill(int timeLeft) {
            CultistMotion.ImpactBurst(Projectile.Center, 0, 0.9f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.CultistBossFireBall);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.CultistBossFireBall].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            int frameHeight = tex.Height / Main.projFrames[Type];
            Rectangle frame = new(0, frameHeight * Projectile.frame, tex.Width, frameHeight);
            Vector2 origin = frame.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //旧位残迹：体帧渐隐重画，同材质拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldPos, frame, CultistMotion.FlameEdge with { A = 0 } * (0.28f * t),
                    Projectile.rotation, origin, Projectile.scale * (0.55f + 0.35f * t), SpriteEffects.None, 0);
            }

            //底晕（加色修饰层，居于体下）
            Main.EntitySpriteDraw(glow, pos, null, CultistMotion.FlameEdge with { A = 0 } * 0.55f,
                0f, glow.Size() * 0.5f, Projectile.scale * 0.62f, SpriteEffects.None, 0);
            //vanilla 精灵体：不透明遮挡层
            Main.EntitySpriteDraw(tex, pos, frame, Color.White, Projectile.rotation, origin,
                Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
