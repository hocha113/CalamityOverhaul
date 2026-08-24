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
    /// 废钢堆：迫击弹砸出的场上物资，无伤害的舞台道具，
    /// 齿轮与弹壳的小堆冒着余烟；P2 磁暴收束会把它们吸走当弹药
    /// </summary>
    internal class ScrapJunkPile : ScrapModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>存留帧数（约 12 秒）</summary>
        private const int LifeFrames = 720;

        private bool Grounded { get => Projectile.ai[0] != 0f; set => Projectile.ai[0] = value ? 1f : 0f; }
        /// <summary>被磁吸走（服务端标记并 netUpdate；堆是服务端自有弹幕，推送有效）</summary>
        internal bool Sucked { get => Projectile.ai[1] != 0f; set => Projectile.ai[1] = value ? 1f : 0f; }
        /// <summary>被吸收（谢幕种类闩，本地）</summary>
        private bool absorbed;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 26;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = LifeFrames;
        }

        public override void AI() {
            //==================== 磁吸飞行：整座堆被拽向统帅 ====================
            if (Sucked) {
                Projectile.tileCollide = false;
                NPC boss = FindBoss();
                if (boss == null) {
                    Projectile.Kill();
                    return;
                }
                Vector2 want = (boss.Center - Projectile.Center).SafeNormalize(Vector2.UnitY)
                    * MathF.Min(6f + Projectile.localAI[0] * 0.55f, 24f);
                Projectile.localAI[0]++;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.16f);
                Projectile.rotation += 0.12f;
                if (!Main.dedServ && Projectile.timeLeft % 3 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        -Projectile.velocity * 0.15f, new Color(255, 150, 58) * 0.6f,
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(false, Main.rand.Next(8, 12));
                }
                if (Vector2.Distance(Projectile.Center, boss.Center) < 72f) {
                    absorbed = true;
                    Projectile.Kill();
                }
                return;
            }

            if (!Grounded) {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.5f, 14f);
            }
            else {
                Projectile.velocity = Vector2.Zero;
            }

            //余烟：刚落地浓、之后转稀
            if (!Main.dedServ && Grounded) {
                int gap = Projectile.timeLeft > LifeFrames - 90 ? 9 : 42;
                if (Projectile.timeLeft % gap == 0) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), -8f),
                        new Vector2(Main.rand.NextFloat(-0.15f, 0.15f), -Main.rand.NextFloat(0.3f, 0.7f)),
                        new Color(52, 48, 44) * 0.8f, Main.rand.NextFloat(0.4f, 0.65f))
                        ?.Configure(Main.rand.Next(36, 60));
                }
            }
        }

        private NPC FindBoss() {
            int type = ModContent.NPCType<ScrapCommander>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == type) {
                    return npc;
                }
            }
            return null;
        }

        public override void OnKill(int timeLeft) {
            //被吸进统帅：一口火星与脉冲环的吸收拍
            if (absorbed && !Main.dedServ) {
                PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(Projectile.Center, Vector2.Zero,
                    new Color(255, 150, 58) * 0.7f, 1f)?.Configure(0.22f, 0.04f, 9);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Circular(3f, 3f), new Color(255, 150, 58) * 0.8f,
                        Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(8, 14));
                }
            }
        }

        /// <summary>服务端把全场废钢堆标记为被吸（磁暴/转阶段回收）；返回标了几座</summary>
        internal static int SuckAll(int limit = 99) {
            int marked = 0;
            int pileType = ModContent.ProjectileType<ScrapJunkPile>();
            for (int i = 0; i < Main.maxProjectiles && marked < limit; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == pileType && p.ModProjectile is ScrapJunkPile pile && !pile.Sucked) {
                    pile.Sucked = true;
                    p.netUpdate = true;
                    marked++;
                }
            }
            return marked;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Grounded = true;
            Projectile.velocity = Vector2.Zero;
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.Cog);
            Main.instance.LoadItem(ItemID.Cannonball);
            Texture2D cog = TextureAssets.Item[ItemID.Cog]?.Value;
            Texture2D ball = TextureAssets.Item[ItemID.Cannonball]?.Value;
            if (cog == null || ball == null) {
                return false;
            }

            float fade = MathHelper.Clamp(Projectile.timeLeft / 40f, 0f, 1f);
            Color tint = lightColor.MultiplyRGB(new Color(190, 140, 104)) * fade;
            //种子摆位：每座堆的歪斜都不一样
            float seed = Projectile.identity * 1.317f;
            Vector2 basePos = Projectile.Center + new Vector2(0f, 6f) - Main.screenPosition;

            Main.EntitySpriteDraw(cog, basePos + new Vector2(-14f, 2f), null, tint,
                seed % MathHelper.TwoPi, cog.Size() * 0.5f, 0.9f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(cog, basePos + new Vector2(13f, 4f), null, tint * 0.92f,
                seed * 1.7f % MathHelper.TwoPi, cog.Size() * 0.5f, 0.75f, SpriteEffects.FlipHorizontally, 0);
            Main.EntitySpriteDraw(ball, basePos + new Vector2(MathF.Sin(seed) * 6f, -6f), null, tint,
                MathF.Sin(seed * 2.3f) * 0.5f, ball.Size() * 0.5f, 0.85f, SpriteEffects.None, 0);
            return false;
        }
    }
}
