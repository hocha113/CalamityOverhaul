using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaCultist
{
    /// <summary>
    /// 鬼奴邪教徒的水面符文法阵：铭刻在湖面上的血色符文环，
    /// 逐笔点亮伴随低吟，铭满静默一拍后向上喷发一柱血水（伤害只在喷发窗）。
    /// 点名模式铭刻期缓慢滑向目标脚下、爆发前锁死（给躲窗）；三阵模式按 ai[1] 错拍轮爆。
    /// 各端 Life 本地推进（帧数确定性，spawn 迟到一两帧的演出差可容忍），伤害由 owner 端结算
    /// </summary>
    internal class KikasaCultistRuneCircle : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>spawn 起算的基准爆发帧（铭刻 64 + 静默 12），与主体节拍表对齐</summary>
        internal const int BurstAtFrames = 76;

        /// <summary>三阵轮爆的错拍间隔</summary>
        internal const int BurstStagger = 12;

        private const int RuneCount = 8;
        private const float Radius = 86f;
        /// <summary>爆发前的锁死窗：法阵停止跟随，给目标读出"要炸了"</summary>
        private const int LockFrames = 20;
        private const int BurstActive = 14;
        private const int FadeFrames = 26;

        /// <summary>水柱命中范围：法阵上方的竖直区</summary>
        private const int ColumnHalfWidth = 62;
        private const int ColumnHeight = 340;

        /// <summary>基准爆发帧（=BurstAtFrames，spawn 传入以便将来按仪式变体调速）</summary>
        private int BurstBase => (int)Projectile.ai[0];
        /// <summary>错拍序号 0/1/2</summary>
        private int StaggerIndex => (int)Projectile.ai[1];
        /// <summary>点名目标 whoAmI+1；0=锚定 spawn 点不跟随</summary>
        private int NamedTarget => (int)Projectile.ai[2] - 1;

        private int BurstFrame => BurstBase + StaggerIndex * BurstStagger;

        private ref float Life => ref Projectile.localAI[0];

        //本地表现闩
        private int lastLitRune = -1;
        private bool burstDone;

        private float Seed => Projectile.identity * 0.7391f % 5.13f;

        public override void SetStaticDefaults()
            => ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 520;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.timeLeft = BurstAtFrames + BurstStagger * 2 + BurstActive + FadeFrames + 30;
        }

        /// <summary>伤害窗严格对齐可见的水柱喷发</summary>
        public override bool? CanDamage()
            => (int)Life > BurstFrame && (int)Life <= BurstFrame + BurstActive ? null : false;

        /// <summary>命中判定：法阵上方的竖直水柱区，柱高随可见水柱同曲线生长（伤害窗与画面严格对齐）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float columnK = MathHelper.Clamp(((int)Life - BurstFrame) / 5f, 0f, 1f);
            int height = (int)(ColumnHeight * (0.55f + 0.45f * columnK));
            Rectangle column = new(
                (int)(Projectile.Center.X - ColumnHalfWidth),
                (int)(Projectile.Center.Y - height),
                ColumnHalfWidth * 2, height + 12);
            return targetHitbox.Intersects(column);
        }

        public override bool? CanCutTiles() => false;

        public override void AI() {
            Life++;
            int t = (int)Life;
            bool viewed = ViewedOwner;

            //铭刻进度：0~1 铺满铭刻期（锁死窗前写完全部符文）
            float inscribeT = InscribeT(t);

            //点名跟随：铭刻期缓慢滑向目标脚下，锁死窗与爆发后钉死。
            //目标位置服务器权威、各端一致；dedServ 无领域也照走 X（Y 恒 spawn 值）
            if (NamedTarget >= 0 && t < BurstFrame - LockFrames) {
                NPC npc = Main.npc[NamedTarget];
                if (npc?.active == true) {
                    float dx = npc.Center.X - Projectile.Center.X;
                    Projectile.position.X += MathHelper.Clamp(dx, -3.2f, 3.2f);
                }
            }

            //逐笔点亮节拍：tick 声 + 落笔涟漪
            int lit = (int)(inscribeT * RuneCount);
            if (lit > lastLitRune && lit <= RuneCount && t < BurstFrame) {
                lastLitRune = lit;
                SoundEngine.PlaySound(SoundID.Item29 with {
                    Volume = 0.26f,
                    Pitch = -0.55f + lit * 0.06f + StaggerIndex * 0.05f,
                    MaxInstances = 3
                }, Projectile.Center);
                if (viewed) {
                    float angle = -MathHelper.PiOver2 + (lit - 1) / (float)RuneCount * MathHelper.TwoPi;
                    KikasaDomainDeco.RippleAt(KikasaCultistRunes.RingSlot(Projectile.Center, Radius, angle), 0.3f);
                }
            }

            //锁死窗入拍：环光一沉，低鸣警告
            if (t == BurstFrame - LockFrames) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Volume = 0.4f, Pitch = -0.95f, MaxInstances = 3 }, Projectile.Center);
            }

            //喷发拍
            if (!burstDone && t > BurstFrame) {
                burstDone = true;
                Burst(viewed);
            }

            //喷发窗内持续的水柱粒子
            if (!Main.dedServ && burstDone && t <= BurstFrame + BurstActive && t % 2 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-ColumnHalfWidth * 0.7f, ColumnHalfWidth * 0.7f), -4f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(10f, 15f)),
                    KikasaCultistServant.BloodMain * Main.rand.NextFloat(0.5f, 0.7f),
                    Main.rand.NextFloat(0.5f, 0.85f))?.Configure(Main.rand.Next(26, 40), Main.rand.NextFloat(-0.3f, 0.3f));
            }

            //光照随进度
            float glowLevel = burstDone
                ? MathHelper.Clamp(1.4f - (t - BurstFrame) / (float)(BurstActive + FadeFrames), 0f, 1.4f)
                : inscribeT * 0.6f;
            if (glowLevel > 0.05f) {
                Lighting.AddLight(Projectile.Center, 0.5f * glowLevel, 0.13f * glowLevel, 0.12f * glowLevel);
            }

            //谢幕：淡出后自杀（各端同帧规则，无需 kill 包）
            if (t >= BurstFrame + BurstActive + FadeFrames) {
                Projectile.Kill();
            }
        }

        private static float InscribeT(int t)
            => MathHelper.Clamp(t / (float)(BurstAtFrames - LockFrames - 4), 0f, 1f);

        /// <summary>喷发：环闪白 + 血水柱 + 扩散环 + 大涟漪，湖面被仪式撕开一柱</summary>
        private void Burst(bool viewed) {
            SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.7f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.9f, Pitch = -0.25f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.35f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
            if (!viewed) {
                return;
            }
            KikasaDomainDeco.RippleAt(Projectile.Center, 1.8f);
            KikasaDomainDeco.SplashAt(Projectile.Center + new Vector2(-14f, 0f), 8);
            KikasaDomainDeco.SplashAt(Projectile.Center + new Vector2(14f, 0f), 8);
            //主柱：中央高抛密集血珠
            for (int i = 0; i < 16; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), -4f),
                    new Vector2(Main.rand.NextFloat(-0.9f, 0.9f), -Main.rand.NextFloat(11f, 16f)),
                    KikasaCultistServant.BloodMain * Main.rand.NextFloat(0.5f, 0.7f),
                    Main.rand.NextFloat(0.55f, 0.9f))?.Configure(Main.rand.Next(36, 52), Main.rand.NextFloat(-0.3f, 0.3f));
            }
            //侧帘：环缘两翼低抛
            for (int i = 0; i < 10; i++) {
                float side = i % 2 == 0 ? 1f : -1f;
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + new Vector2(side * Main.rand.NextFloat(30f, Radius * 0.9f), -2f),
                    new Vector2(side * Main.rand.NextFloat(0.4f, 1.4f), -Main.rand.NextFloat(5f, 9f)),
                    KikasaCultistServant.BloodDeep * Main.rand.NextFloat(0.5f, 0.7f),
                    Main.rand.NextFloat(0.45f, 0.7f))?.Configure(Main.rand.Next(24, 38), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            //竖直扩散环：水柱的冲击读数
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center + new Vector2(0f, -30f), Vector2.Zero,
                KikasaCultistServant.FoamGlow, 0.09f)
                ?.Configure(new Vector2(0.5f, 1f), -MathHelper.PiOver2, 0.3f, 10);
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center + new Vector2(0f, -14f),
                new Vector2(0f, -0.7f), KikasaCultistServant.MistBlood * 0.8f,
                Main.rand.NextFloat(0.7f, 1f))?.Configure(Main.rand.Next(50, 80));
            Main.LocalPlayer?.CWR()?.GetScreenShake(3f);
        }

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            int t = (int)Life;
            float inscribeT = InscribeT(t);
            float fade = burstDone
                ? MathHelper.Clamp(1f - (t - BurstFrame - BurstActive) / (float)FadeFrames, 0f, 1f)
                : 1f;
            if (fade <= 0.02f) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //锁死窗：环光整体压暗三成再回弹（吸气），爆发帧闪白
            float dim = 1f;
            if (!burstDone && t > BurstFrame - LockFrames) {
                float k = (t - (BurstFrame - LockFrames)) / (float)LockFrames;
                dim = 0.7f + 0.3f * MathF.Pow(k, 3f);
            }
            float flash = burstDone ? MathHelper.Clamp(1.6f - (t - BurstFrame) * 0.12f, 0f, 1.6f) : 0f;

            float spin = Main.GlobalTimeWrappedHourly * 0.9f + Seed;
            Color main = KikasaCultistServant.BloodMain;
            Color core = KikasaCultistServant.RuneCore;
            KikasaCultistRunes.DrawWaterRing(sb, Projectile.Center, Radius, RuneCount,
                inscribeT, spin, Seed, main, core, (0.85f * dim + flash * 0.5f) * fade);

            //爆发窗的水柱亮带
            if (burstDone && t <= BurstFrame + BurstActive + 8) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    float columnK = MathHelper.Clamp((t - BurstFrame) / 5f, 0f, 1f);
                    float columnFade = MathHelper.Clamp(1f - (t - BurstFrame - BurstActive) / 8f, 0f, 1f);
                    float h = ColumnHeight * (0.55f + 0.45f * columnK);
                    Vector2 mid = Projectile.Center + new Vector2(0f, -h * 0.5f);
                    sb.Draw(glow, mid - Main.screenPosition, null, main * (0.5f * columnFade), 0f,
                        glow.Size() * 0.5f,
                        new Vector2(ColumnHalfWidth * 1.5f * 2f / glow.Width, h * 1.15f * 2f / glow.Height), SpriteEffects.None, 0f);
                    sb.Draw(glow, mid - Main.screenPosition, null, core * (0.35f * columnFade), 0f,
                        glow.Size() * 0.5f,
                        new Vector2(ColumnHalfWidth * 0.6f * 2f / glow.Width, h * 1.05f * 2f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        //==================== 命中 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //水柱托举命中：溅血向上（OnHit 只在 owner 端跑）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    target.Center + Main.rand.NextVector2Circular(16f, 16f),
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(2f, 5f)),
                    KikasaCultistServant.BloodMain * 0.6f, Main.rand.NextFloat(0.4f, 0.65f))
                    ?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.55f, Pitch = -0.35f, MaxInstances = 3 }, target.Center);
        }
    }
}
