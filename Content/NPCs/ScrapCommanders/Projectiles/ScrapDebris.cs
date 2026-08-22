using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles
{
    /// <summary>
    /// 废钢碎片：磁暴收束的风暴弹药。ai[0]=统帅 whoAmI（&lt;0 为自由坠落件），
    /// ai[1]=环绕倒计时（各端确定性同拍翻转为切线甩出，不吃服务器推送；
    /// -2 为"来袭件"，从场边被磁力拽向统帅、沿途咬人），
    /// ai[2]=环号（0 内环 / 1 外环，半径与甩出拍不同，两波交错风暴）。
    /// 环绕聚拢段无伤害（公平阀），甩出/坠落/来袭段才咬人
    /// </summary>
    internal class ScrapDebris : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private bool FreeFall => Projectile.ai[0] < 0f;
        private bool Inbound => Projectile.ai[1] == -2f;
        private ref float OrbitCountdown => ref Projectile.ai[1];
        private float RingRadius => Projectile.ai[2] > 0.5f ? 300f : 200f;
        private float RingPhase => Projectile.ai[2] > 0.5f ? 1.7f : 0f;
        private bool flung;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 420;
        }

        /// <summary>环绕聚拢是演出，甩出/坠落/来袭才有伤害窗</summary>
        public override bool? CanDamage() => (flung || FreeFall || Inbound) ? null : false;

        public override void AI() {
            Projectile.rotation += 0.18f + Projectile.velocity.Length() * 0.012f;

            if (FreeFall) {
                //自落/瀑布件：缓旋坠地
                Projectile.tileCollide = true;
                Projectile.velocity.X *= 0.99f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.35f, 13f);
                if (!Main.dedServ && Projectile.timeLeft % 5 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, Main.rand.NextVector2Circular(1.5f, 1.5f),
                        new Color(255, 150, 58) * 0.6f, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(true, Main.rand.Next(8, 12));
                }
                return;
            }

            NPC boss = Main.npc[(int)Projectile.ai[0]];
            bool bossAlive = boss != null && boss.active;

            if (Inbound) {
                //来袭件：从场边被磁力拽向统帅，沿途是走位压力
                if (!bossAlive) {
                    Projectile.Kill();
                    return;
                }
                Vector2 want = (boss.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 13.5f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.07f);
                if (!Main.dedServ && Projectile.timeLeft % 4 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, -Projectile.velocity * 0.12f,
                        new Color(255, 150, 58) * 0.55f, Main.rand.NextFloat(0.3f, 0.55f))
                        ?.Configure(false, Main.rand.Next(7, 11));
                }
                if (Vector2.Distance(Projectile.Center, boss.Center) < 60f) {
                    Projectile.Kill();
                }
                return;
            }

            if (!flung && !bossAlive) {
                Projectile.Kill();
                return;
            }

            if (OrbitCountdown > 0f) {
                //环绕聚拢：半径随倒计时展开，相位由 identity + 倒计时播种
                //（identity/倒计时/环号都在生成包里，各端角度严格一致）
                OrbitCountdown--;
                float progress = 1f - OrbitCountdown / 90f;
                float radius = MathHelper.Lerp(54f, RingRadius, MathHelper.Clamp(progress, 0f, 1f));
                float ang = Projectile.identity * 2.399f + RingPhase + progress * 3.4f;
                Projectile.Center = boss.Center + ang.ToRotationVector2() * radius;
                Projectile.velocity = Vector2.Zero;
                return;
            }

            if (!flung) {
                //切线甩出：纯几何决定方向，端间一致
                flung = true;
                float ang = Projectile.identity * 2.399f + RingPhase + 3.4f;
                Vector2 radial = ang.ToRotationVector2();
                Vector2 tangent = radial.RotatedBy(MathHelper.PiOver2);
                Projectile.velocity = tangent * 15f + radial * 4f;
                Projectile.tileCollide = true;
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 150);
            }
            //甩出段轻微下坠
            Projectile.velocity.Y += 0.08f;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //瀑布/自落件砸地：小型重砸配方；其余只溅火花
            if (FreeFall && timeLeft > 0) {
                ScrapVfx.GroundSlam(Projectile.Center, 0.6f);
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Circular(3f, 3f),
                    new Color(255, 150, 58) * 0.8f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //种子选件：齿轮/弹壳/链环三选一
            int kind = Projectile.identity % 3;
            Texture2D tex;
            if (kind == 0) {
                Main.instance.LoadItem(ItemID.Cog);
                tex = TextureAssets.Item[ItemID.Cog]?.Value;
            }
            else if (kind == 1) {
                Main.instance.LoadItem(ItemID.Cannonball);
                tex = TextureAssets.Item[ItemID.Cannonball]?.Value;
            }
            else {
                tex = TextureAssets.Chain22?.Value;
            }
            if (tex == null) {
                return false;
            }
            Color tint = lightColor.MultiplyRGB(new Color(200, 145, 108));
            float scale = kind == 2 ? 0.8f : 0.9f;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, tint,
                Projectile.rotation, tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
