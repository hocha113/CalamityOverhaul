using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Frostveil.Projectiles
{
    /// <summary>
    /// 风雪墙：横扫地表雪原的阵风雪浪（场地实体，无伤害的控制机制）。
    /// ai[0]=明窗世界Y ai[1]=行进方向 ai[2]=保留位恒 0（档位只调浪频率，墙体形状恒定）。
    /// 预告=远处白幕逼近（1500px 外生成，约 214 帧后过顶，远超 45 帧底线）+ 风啸渐强（氛围层循环）；
    /// 落地=经过时视野雪幕 + 顺风轻推 + 数秒原版寒颤；余韵=散幕消融。
    /// 具名缺口「明窗」：墙面上一条亮缝，站进窗内整浪安全通过。
    /// 可见墙=判定墙（绘制与判定读同一几何），一切施加只落在本机玩家
    /// </summary>
    internal class FrostveilGaleWallProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>横扫速度（px/帧），调度器与实体同读</summary>
        internal const float SweepSpeed = 7f;
        /// <summary>墙体半厚</summary>
        private const float HalfThickness = 80f;
        /// <summary>墙体自明窗向上下各延伸的半高</summary>
        private const float WallHalfHeight = 560f;
        /// <summary>明窗半高（总高约 5.5 格，跳进去绰绰有余）</summary>
        private const float SeamHalfHeight = 46f;
        /// <summary>总寿命：行程约 3640px（两三屏）</summary>
        private const int LifeFrames = 520;
        private const int FadeInFrames = 30;
        private const int FadeOutFrames = 40;
        /// <summary>经过施加的寒颤时长（数秒）</summary>
        private const int ChillFrames = 240;
        /// <summary>顺风轻推：每帧增量与推速上限</summary>
        private const float PushAccel = 0.32f;
        private const float PushMaxSpeed = 6f;
        /// <summary>墙端纵向收口长度</summary>
        private const float EndTaper = 150f;
        /// <summary>绘制行距</summary>
        private const float RowSpacing = 68f;

        private float SeamY => Projectile.ai[0];
        private int Dir => (int)Projectile.ai[1];
        private int Elapsed => LifeFrames - Projectile.timeLeft;

        //本地表现私产（逐端各自推演，不入同步）
        private int townTimer;
        private bool nearTown;
        private bool contactPlayed;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1800;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//纯控制机制，恒无伤害
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>出生到消散的整体透明包络</summary>
        private float Envelope() {
            int elapsed = Elapsed;
            float fadeIn = MathHelper.Clamp(elapsed / (float)FadeInFrames, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / (float)FadeOutFrames, 0f, 1f);
            return fadeIn * fadeOut;
        }

        /// <summary>墙体判定矩形（绘制与判定共用同一几何）</summary>
        private Rectangle WallRect() => new(
            (int)(Projectile.Center.X - HalfThickness),
            (int)(SeamY - WallHalfHeight),
            (int)(HalfThickness * 2f),
            (int)(WallHalfHeight * 2f));

        /// <summary>命中盒是否稳稳待在明窗内（上下各放 6px 宽容）</summary>
        private bool InSeam(Rectangle hitbox)
            => hitbox.Top >= SeamY - SeamHalfHeight - 6f
            && hitbox.Bottom <= SeamY + SeamHalfHeight + 6f;

        public override bool? CanDamage() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!Main.dedServ) {
                    //成墙远音：位置衰减天然给出"远处有什么起来了"
                    SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with {
                        Volume = 0.9f, Pitch = -0.42f, MaxInstances = 3
                    }, Projectile.Center);
                }
            }

            float envelope = Envelope();
            Lighting.AddLight(new Vector2(Projectile.Center.X, SeamY),
                new Vector3(0.14f, 0.2f, 0.28f) * envelope);

            if (Main.dedServ) {
                return;//以下全是本地表现与本机施加
            }

            SpawnCurtainDust(envelope);
            ApplyToLocalPlayer(envelope);
        }

        /// <summary>幕内雪尘点缀：只在屏内花预算（≤2 粒/帧 + 低频流丝）</summary>
        private void SpawnCurtainDust(float envelope) {
            if (envelope < 0.3f || Main.gamePaused) {
                return;
            }
            float screenCenterX = Main.screenPosition.X + Main.screenWidth * 0.5f;
            if (MathF.Abs(Projectile.Center.X - screenCenterX) > Main.screenWidth * 0.5f + 420f) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                float y = SeamY + Main.rand.NextFloat(-WallHalfHeight, WallHalfHeight);
                if (MathF.Abs(y - SeamY) < SeamHalfHeight) {
                    continue;//明窗里不下雪
                }
                Dust dust = Dust.NewDustPerfect(
                    new Vector2(Projectile.Center.X + Main.rand.NextFloat(-HalfThickness, HalfThickness), y),
                    DustID.Snow, new Vector2(Dir * Main.rand.NextFloat(5f, 9f),
                        Main.rand.NextFloat(-0.5f, 1.5f)), 120, default, Main.rand.NextFloat(0.8f, 1.3f));
                dust.noGravity = true;
            }
            if (Main.rand.NextBool(3)) {
                float y = SeamY + Main.rand.NextFloat(-WallHalfHeight, WallHalfHeight) * 0.9f;
                PRTLoader.NewParticle<PRT_FrostveilFlake>(
                    new Vector2(Projectile.Center.X - Dir * HalfThickness, y),
                    new Vector2(Dir * 11f, Main.rand.NextFloat(-1f, 2f)),
                    new Color(232, 242, 252) * 0.55f, Main.rand.NextFloat(1f, 1.6f))
                    ?.Configure(Main.rand.Next(30, 50), Dir * 11f);
            }
        }

        /// <summary>本机玩家判定：雪幕上报、明窗豁免、寒颤与轻推（Boss/城镇双闸）</summary>
        private void ApplyToLocalPlayer(float envelope) {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) {
                return;
            }

            if (--townTimer <= 0) {
                townTimer = 30;
                nearTown = FrostveilAmbience.NearTown(player.Center);
            }

            Rectangle wall = WallRect();
            Rectangle hitbox = player.Hitbox;
            bool overlap = wall.Intersects(hitbox);
            bool seamSafe = overlap && InSeam(hitbox);

            //雪幕上报：接近先起薄幕，幕心吃满，明窗里只剩三成
            float dist = MathF.Abs(Projectile.Center.X - player.Center.X);
            float verticalIn = MathHelper.Clamp(
                1.4f - MathF.Abs(player.Center.Y - SeamY) / WallHalfHeight, 0f, 1f);
            float lead = 1f - MathHelper.Clamp((dist - HalfThickness) / 260f, 0f, 1f);
            float veil;
            if (overlap) {
                veil = seamSafe ? 0.3f : 1f;
            }
            else {
                veil = lead * 0.55f * verticalIn;
            }
            FrostveilAmbience.ReportWaveVeil(veil * envelope);

            if (!overlap) {
                return;
            }

            if (seamSafe) {
                //明窗甜头：窗里安全通过，给一点亮晶告诉玩家"站对了"
                if (Elapsed % 9 == 0 && !Main.gamePaused) {
                    PRTLoader.NewParticle<PRT_DefFrostGlint>(
                        player.Center + Main.rand.NextVector2Circular(30f, 18f), Vector2.Zero,
                        new Color(255, 250, 230), Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(Main.rand.Next(12, 18));
                }
                return;
            }

            //Boss 在场/城镇安宁：减益与位移暂停，只留雪幕视觉。
            //InZone 同时兜住两件事：中途关掉残酷模式的残墙只走视觉，
            //以及玩家已逃出地表雪原辖区（下洞/出界）就不再追打
            if (CWRWorld.HasBoss || nearTown || envelope < 0.5f
                || !FrostveilPlayer.InZone(player)) {
                return;
            }

            if (!contactPlayed) {
                contactPlayed = true;
                SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with {
                    Volume = 0.7f, Pitch = 0.1f, MaxInstances = 3
                }, player.Center);
            }

            player.AddBuff(BuffID.Chilled, ChillFrames);
            //顺风轻推：只加不减，推到上限就松手
            if (Dir > 0 && player.velocity.X < PushMaxSpeed) {
                player.velocity.X = MathF.Min(player.velocity.X + PushAccel, PushMaxSpeed);
            }
            else if (Dir < 0 && player.velocity.X > -PushMaxSpeed) {
                player.velocity.X = MathF.Max(player.velocity.X - PushAccel, -PushMaxSpeed);
            }
        }

        //==================== 绘制（与判定同几何）====================

        public override bool PreDraw(ref Color lightColor) {
            float envelope = Envelope();
            if (envelope < 0.02f) {
                return false;
            }
            Texture2D fog = CWRAsset.Fog?.Value;
            Texture2D spindle = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (fog == null || spindle == null || glow == null) {
                return false;
            }
            Vector2 fogOrigin = fog.Size() * 0.5f;
            Vector2 spindleOrigin = spindle.Size() * 0.5f;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            float time = Main.GlobalTimeWrappedHourly;
            float centerX = Projectile.Center.X;

            Color body = new(216, 230, 244);
            Color crest = new(242, 248, 255);
            Color seamLight = new Color(255, 250, 235, 0);//A=0：预乘批里的加色光

            //幕体：自明窗向上下铺行，行内两团雾错相翻滚，端部收口、窗带让空
            int rows = (int)(WallHalfHeight * 2f / RowSpacing);
            for (int i = 0; i <= rows; i++) {
                float y = SeamY - WallHalfHeight + i * RowSpacing;
                float seamDist = MathF.Abs(y - SeamY);
                if (seamDist < SeamHalfHeight) {
                    continue;//明窗净空
                }
                //窗缘薄化 + 墙端收口
                float seamMask = MathHelper.Clamp((seamDist - SeamHalfHeight) / 60f, 0.2f, 1f);
                float endDist = WallHalfHeight - seamDist;
                float endMask = MathHelper.Clamp(endDist / EndTaper, 0f, 1f);
                float rowAlpha = 0.42f * envelope * seamMask * endMask;
                if (rowAlpha < 0.01f) {
                    continue;
                }
                for (int c = 0; c < 2; c++) {
                    float h = MathF.Sin(Projectile.identity * 2.7f + i * 3.1f + c * 5.3f);
                    float x = centerX + h * HalfThickness * 0.62f;
                    float wob = MathF.Sin(time * 1.9f + i * 1.3f + c * 2.6f) * 9f;
                    DrawFogPuff(fog, fogOrigin, new Vector2(x, y + wob),
                        body * (rowAlpha * (c == 0 ? 1f : 0.7f)),
                        h * 3f + time * (0.24f + c * 0.1f),
                        2f + 0.5f * MathF.Abs(h));
                }
            }

            //浪头：行进侧的亮缘流丝，风撕开的白锋
            float frontX = centerX + Dir * HalfThickness * 0.85f;
            for (int i = 0; i < 8; i++) {
                float h = MathF.Sin(Projectile.identity * 1.9f + i * 2.45f);
                float y = SeamY + h * WallHalfHeight * 0.86f;
                if (MathF.Abs(y - SeamY) < SeamHalfHeight + 12f) {
                    continue;
                }
                float streakWob = MathF.Sin(time * 3.2f + i * 1.7f) * 10f;
                Main.EntitySpriteDraw(spindle, new Vector2(frontX + streakWob, y) - Main.screenPosition,
                    null, crest * (0.4f * envelope), MathHelper.PiOver2,
                    spindleOrigin, new Vector2(0.16f, 1.7f + 0.6f * MathF.Abs(h)), SpriteEffects.None, 0);
            }

            //明窗：横贯墙厚的一线暖光，从远处就能读出"那里能过"
            float seamPulse = 0.8f + 0.2f * MathF.Sin(time * 2.4f + Projectile.identity);
            Main.EntitySpriteDraw(glow, new Vector2(centerX, SeamY) - Main.screenPosition, null,
                seamLight * (0.55f * envelope * seamPulse), 0f, glowOrigin,
                new Vector2(HalfThickness * 3.4f / 64f, 0.62f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, new Vector2(centerX, SeamY) - Main.screenPosition, null,
                seamLight * (0.3f * envelope), 0f, glowOrigin,
                new Vector2(HalfThickness * 4.6f / 64f, 1.15f), SpriteEffects.None, 0);
            return false;
        }

        /// <summary>幕体雾团统一走 EntitySpriteDraw（默认批即预乘 AlphaBlend）</summary>
        private static void DrawFogPuff(Texture2D fog, Vector2 origin,
            Vector2 worldPos, Color color, float rotation, float scale) {
            Main.EntitySpriteDraw(fog, worldPos - Main.screenPosition, null, color,
                rotation, origin, scale, SpriteEffects.None, 0);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            float screenCenterX = Main.screenPosition.X + Main.screenWidth * 0.5f;
            if (MathF.Abs(Projectile.Center.X - screenCenterX) > Main.screenWidth * 0.5f + 420f) {
                return;
            }
            //余韵：散幕时抖落一阵流丝
            for (int i = 0; i < 10; i++) {
                float y = SeamY + Main.rand.NextFloat(-WallHalfHeight, WallHalfHeight) * 0.8f;
                PRTLoader.NewParticle<PRT_FrostveilFlake>(
                    new Vector2(Projectile.Center.X + Main.rand.NextFloat(-HalfThickness, HalfThickness), y),
                    new Vector2(Dir * Main.rand.NextFloat(2f, 6f), Main.rand.NextFloat(-1f, 2f)),
                    new Color(230, 240, 250) * 0.5f, Main.rand.NextFloat(0.9f, 1.4f))
                    ?.Configure(Main.rand.Next(40, 70), Dir * 4f);
            }
        }
    }
}
