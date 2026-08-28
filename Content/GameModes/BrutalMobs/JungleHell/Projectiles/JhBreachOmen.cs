using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.JungleHell.Projectiles
{
    /// <summary>
    /// 破土预兆：骨蛇在地下逼近时的尘柱示警，跟随蛇头并在其上方地表冒灰烬柱。
    /// 蛇头越接近地表尘柱越密。纯预告体；骨蛇的伏击提速只在本预兆在场时生效。<br/>
    /// ai[0]=骨蛇头NPC索引
    /// </summary>
    internal class JhBreachOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>在地下时每帧续到的剩余寿命（破土/失踪后自然滑向渐隐）</summary>
        private const int RefreshLife = 26;
        private const int FadeFrames = 14;
        /// <summary>地表上扫周期与最大扫描高度（格）</summary>
        private const int ScanInterval = 5;
        private const int MaxScanTiles = 44;

        private int SerpentIndex => (int)Projectile.ai[0];

        private int scanTimer;
        /// <summary>蛇头上方地表的世界 Y（0=未找到，回退在蛇头处冒尘）</summary>
        private float surfaceWorldY;
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 700;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = RefreshLife;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Age++;
            if (SerpentIndex.TryGetNPC(out NPC serpent) && serpent.Alives()
                && serpent.type == NPCID.BoneSerpentHead) {
                Projectile.Center = serpent.Center;
                bool underground = Collision.SolidCollision(serpent.position, serpent.width, serpent.height);
                if (underground) {
                    //在地下就续命；破土后停止续命，自然渐隐
                    Projectile.timeLeft = RefreshLife;
                    //回写"预兆在场"帧号：伏击提速只许在预告可见时生效
                    if (serpent.TryGetGlobalNPC(out JungleHellNPC global)) {
                        global.lastOmenFrame = (int)Main.GameUpdateCount;
                    }
                }

                if (!Main.dedServ && --scanTimer <= 0) {
                    scanTimer = ScanInterval;
                    surfaceWorldY = FindSurfaceY(serpent);
                }
            }

            //尘柱表现（≤4/帧）：越接近地表越密
            if (Main.dedServ) {
                return;
            }
            float depth = surfaceWorldY > 0f ? Projectile.Center.Y - surfaceWorldY : 300f;
            float urgency = MathHelper.Clamp(1.2f - depth / 620f, 0.25f, 1f);
            Vector2 vent = surfaceWorldY > 0f
                ? new Vector2(Projectile.Center.X, surfaceWorldY)
                : Projectile.Center;

            int dustCount = Main.rand.NextBool() ? 2 : 1;
            for (int i = 0; i < dustCount; i++) {
                Dust ash = Dust.NewDustPerfect(vent + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-6f, 2f)),
                    DustID.Ash, new Vector2(0f, -Main.rand.NextFloat(1f, 2.6f + 2f * urgency)), 100, default, 1f + 0.5f * urgency);
                ash.noGravity = true;
            }
            if (Main.rand.NextBool(3)) {
                Dust ember = Dust.NewDustPerfect(vent + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f),
                    DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(1.5f, 3f)), 120, default, 0.9f + 0.4f * urgency);
                ember.noGravity = true;
            }
            Lighting.AddLight(vent, 0.24f * urgency, 0.12f * urgency, 0.04f * urgency);
        }

        /// <summary>从蛇头向上找第一处可破土的空气边界，返回其世界 Y（找不到返回 0）</summary>
        private static float FindSurfaceY(NPC serpent) {
            int tx = (int)(serpent.Center.X / 16f);
            int ty = (int)(serpent.Center.Y / 16f);
            if (tx < 5 || tx > Main.maxTilesX - 5) {
                return 0f;
            }
            int top = Math.Max(5, ty - MaxScanTiles);
            for (int y = ty; y > top; y--) {
                if (!WorldGen.SolidTile(tx, y) && WorldGen.SolidTile(tx, y + 1)) {
                    return (y + 1) * 16f;
                }
            }
            return 0f;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (surfaceWorldY <= 0f) {
                return false;
            }
            Texture2D core = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 vent = new Vector2(Projectile.Center.X, surfaceWorldY) - Main.screenPosition;

            float fade = Math.Min(MathHelper.Clamp(Age / 10f, 0f, 1f),
                MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f));
            float depth = Projectile.Center.Y - surfaceWorldY;
            float urgency = MathHelper.Clamp(1.2f - depth / 620f, 0.25f, 1f);
            float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity);

            //地面裂隙标记：暗底+余烬光，宽度随逼近放大
            Main.EntitySpriteDraw(core, vent, null, new Color(70, 40, 30) * (0.7f * fade),
                MathHelper.PiOver2, core.Size() / 2f, new Vector2(0.16f, 0.4f + 0.3f * urgency), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, vent, null, new Color(255, 130, 50, 0) * (0.5f * fade * pulse * urgency),
                0f, glow.Size() / 2f, new Vector2(0.9f + 0.5f * urgency, 0.3f), SpriteEffects.None, 0);
            return false;
        }
    }
}
