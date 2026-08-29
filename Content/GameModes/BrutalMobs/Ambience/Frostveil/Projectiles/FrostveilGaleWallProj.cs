using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
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
    /// 具名缺口「明窗」：墙面上一条雪幕挖薄的横缝，边缘雪唇可读，站进窗内整浪安全通过。
    /// 可见体=雪浓度体：AmbientFogBody 密度场单 pass（前缘噪声撕裂+定向雪缕+浓核自影+明窗，
    /// 合成不透明度封顶，乘环境光），一张画布上下贯通、两端收口带跨过判定边界，墙不硬切；
    /// Spray 雪团只作材质点缀；前缘最浓、尾部稀薄，白是遮挡不是发光；发光只剩明窗一线弱光敷料。
    /// 可见墙=判定墙（绘制与判定读同一几何），一切施加只落在本机玩家
    /// </summary>
    internal class FrostveilGaleWallProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>在场帧戳：AI 每帧盖戳，氛围层据此跳过无风雪墙时的全表扫描</summary>
        internal static ActivityStamp PresenceStamp;

        //雪材贴图（Masking alpha 表已核：Spray 为真 alpha，可作遮挡体）
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Spray = null;

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
        /// <summary>墙端纵向收口长度：收口带跨过判定边界，浓段落在墙内、稀段溢出一点，两端不硬切</summary>
        private const float EndTaper = 260f;
        /// <summary>雪浓度体画布厚度（判定 ±80 藏在撕裂前缘之内）</summary>
        private const float CanvasThickness = 300f;
        /// <summary>雪浓度体画布长轴（±WallHalfHeight + 两端收口带）</summary>
        private const float CanvasLength = WallHalfHeight * 2f + EndTaper * 2f;

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
            PresenceStamp.Stamp();
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!Main.dedServ) {
                    //成墙远音：位置衰减天然给出"远处有什么起来了"
                    SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with {
                        Volume = 0.9f,
                        Pitch = -0.42f,
                        MaxInstances = 3
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

        /// <summary>幕内雪粒：密集横扫雪尘 + 斜扫流丝 + 前缘先导与翻卷团块（只在屏内花预算，风向一致）</summary>
        private void SpawnCurtainDust(float envelope) {
            if (envelope < 0.3f || Main.gamePaused) {
                return;
            }
            float screenCenterX = Main.screenPosition.X + Main.screenWidth * 0.5f;
            if (MathF.Abs(Projectile.Center.X - screenCenterX) > Main.screenWidth * 0.5f + 420f) {
                return;
            }
            //雪尘：幕内密集横扫
            for (int i = 0; i < 5; i++) {
                float y = SeamY + Main.rand.NextFloat(-WallHalfHeight, WallHalfHeight);
                if (MathF.Abs(y - SeamY) < SeamHalfHeight) {
                    continue;//明窗里不下雪
                }
                Dust dust = Dust.NewDustPerfect(
                    new Vector2(Projectile.Center.X + Main.rand.NextFloat(-HalfThickness, HalfThickness), y),
                    DustID.Snow, new Vector2(Dir * Main.rand.NextFloat(6f, 11f),
                        Main.rand.NextFloat(-0.5f, 1.8f)), 110, default, Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = true;
            }
            //斜扫流丝：幕内快速掠过，拉丝方向与风一致
            for (int i = 0; i < 2; i++) {
                float y = SeamY + Main.rand.NextFloat(-WallHalfHeight, WallHalfHeight) * 0.92f;
                if (MathF.Abs(y - SeamY) < SeamHalfHeight) {
                    continue;
                }
                PRTLoader.NewParticle<PRT_FrostveilFlake>(
                    new Vector2(Projectile.Center.X + Main.rand.NextFloat(-HalfThickness, HalfThickness) * 0.8f, y),
                    new Vector2(Dir * Main.rand.NextFloat(11f, 15f), Main.rand.NextFloat(0.5f, 2.5f)),
                    new Color(236, 244, 253) * 0.6f, Main.rand.NextFloat(1f, 1.6f))
                    ?.Configure(Main.rand.Next(24, 40), Dir * Main.rand.NextFloat(12f, 16f));
            }
            //前缘先导：墙未到、雪先到，锋面外抛出更急的斜丝
            if (Main.rand.NextBool(2)) {
                float y = SeamY + Main.rand.NextFloat(-WallHalfHeight, WallHalfHeight) * 0.85f;
                if (MathF.Abs(y - SeamY) >= SeamHalfHeight) {
                    PRTLoader.NewParticle<PRT_FrostveilFlake>(
                        new Vector2(Projectile.Center.X + Dir * (HalfThickness + Main.rand.NextFloat(10f, 120f)), y),
                        new Vector2(Dir * Main.rand.NextFloat(13f, 17f), Main.rand.NextFloat(1f, 3f)),
                        new Color(244, 249, 255) * 0.55f, Main.rand.NextFloat(0.9f, 1.4f))
                        ?.Configure(Main.rand.Next(18, 30), Dir * 15f);
                }
            }
            //前缘翻卷：低频抛出实心雪团滚过锋面
            if (Main.rand.NextBool(3)) {
                float y = SeamY + Main.rand.NextFloat(-WallHalfHeight, WallHalfHeight) * 0.8f;
                if (MathF.Abs(y - SeamY) >= SeamHalfHeight + 20f) {
                    PRTLoader.NewParticle<PRT_FrostveilClump>(
                        new Vector2(Projectile.Center.X + Dir * HalfThickness * Main.rand.NextFloat(0.5f, 0.95f), y),
                        new Vector2(Dir * Main.rand.NextFloat(5f, 8f), Main.rand.NextFloat(-1.2f, 0.2f)),
                        new Color(240, 246, 253) * 0.85f, Main.rand.NextFloat(0.8f, 1.3f))
                        ?.Configure(Main.rand.Next(26, 40), Dir * Main.rand.NextFloat(6f, 9f));
                }
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
                    Volume = 0.7f,
                    Pitch = 0.1f,
                    MaxInstances = 3
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

        /// <summary>确定性哈希（绘制专用，不耗 Main.rand）</summary>
        private static float Hash01(int i) {
            float f = MathF.Sin(i * 12.9898f + 78.233f) * 43758.5453f;
            return f - MathF.Floor(f);
        }

        /// <summary>雪是遮挡体：亮度乘所在处环境光（保 0.3 底，镜像 Woodsong 雾）</summary>
        private static float LightK(float x, float y) {
            Color c = Lighting.GetColor((int)(x / 16f), (int)(y / 16f));
            return 0.3f + 0.7f * ((c.R + c.G + c.B) / 765f);
        }

        /// <summary>Spray 九宫团块的源矩形</summary>
        private static Rectangle ClumpCell(int i) {
            int cell = i % 9;
            return new Rectangle(cell % 3 * 171, cell / 3 * 171, 170, 170);
        }

        public override bool PreDraw(ref Color lightColor) {
            float envelope = Envelope();
            if (envelope < 0.02f) {
                return false;
            }
            Texture2D spindle = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D clump = Spray?.Value;
            if (spindle == null || glow == null || clump == null) {
                return false;
            }
            Vector2 spindleOrigin = spindle.Size() * 0.5f;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            Vector2 clumpOrigin = new(85f, 85f);
            float time = Main.GlobalTimeWrappedHourly;
            float centerX = Projectile.Center.X;
            int seed = Projectile.identity * 97;

            //雪的三级明度在密度场内完成（撕缘新雪→雪体→浓核自影）；点缀层沿用体色
            Color body = new(226, 236, 248);
            Color fresh = new(247, 251, 255);
            Color seamLight = new Color(255, 250, 235, 0);//A=0：明窗仅存的一线弱光敷料

            float frontX = centerX + Dir * HalfThickness * 0.8f;

            //——雪浓度体：密度场单 pass（行铺堆叠病根治 2026-08-29）——
            //明窗/前缘撕裂/幕内流丝/两端收口全部入 shader，合成不透明度封顶 0.68
            var wall = AmbientFogDraw.WallSpec.Default;
            wall.Center = new Vector2(centerX, SeamY);
            wall.SizePx = new Vector2(CanvasThickness, CanvasLength);
            wall.Dir = Dir;
            wall.Body = body;
            wall.Edge = fresh;
            wall.MaxAlpha = 0.68f;
            wall.Density = envelope;
            wall.FlowPx = 420f;
            wall.Streak = 0.85f;
            wall.Seed = Projectile.identity * 0.37f;
            wall.FrontBias = 0.72f;
            wall.SeamV = 0.5f;//画布以 SeamY 为中，窗恒居中
            wall.SeamHalfV = SeamHalfHeight / CanvasLength;
            wall.TaperV = EndTaper / CanvasLength;
            AmbientFogDraw.DrawWallInEntityBatch(in wall);

            //——幕内实雪团：撕裂剪影错相翻滚，材质锚点（少量点缀，密度由雪体承担）——
            for (int i = 0; i < 5; i++) {
                float hy = Hash01(seed + i * 11);
                float y = SeamY + (hy * 2f - 1f) * WallHalfHeight * 0.86f;
                if (MathF.Abs(y - SeamY) < SeamHalfHeight + 20f) {
                    continue;
                }
                float churnPhase = Hash01(seed + i * 13) * MathHelper.TwoPi;
                float x = centerX + Dir * HalfThickness * (-0.1f + 0.55f * Hash01(seed + i * 17))
                    + Dir * MathF.Sin(time * (1f + Hash01(seed + i * 19)) + churnPhase) * 8f;
                float lightK = LightK(x, y);
                float pulse = 0.30f + 0.10f * MathF.Sin(time * 1.8f + churnPhase * 1.7f);
                Main.EntitySpriteDraw(clump, new Vector2(x, y) - Main.screenPosition,
                    ClumpCell(seed + i * 5),
                    Color.Lerp(body, fresh, Hash01(seed + i * 23)) * (pulse * envelope * lightK),
                    time * (0.5f + 0.6f * Hash01(seed + i * 29)) * (Hash01(seed + i * 31) > 0.5f ? 1f : -1f),
                    clumpOrigin, 0.55f + 0.30f * Hash01(seed + i * 37), SpriteEffects.None, 0);
            }

            //——前缘翻卷团块：锋面上错相位滚动的实雪块（前缘撕裂本身由密度场承担）——
            for (int i = 0; i < 9; i++) {
                float h = Hash01(seed + i + 500);
                float y = SeamY + (Hash01(seed + i + 510) * 2f - 1f) * WallHalfHeight * 0.88f;
                if (MathF.Abs(y - SeamY) < SeamHalfHeight + 26f) {
                    continue;
                }
                float phase = h * MathHelper.TwoPi;
                float x = frontX + Dir * (8f + MathF.Sin(time * (1.1f + h * 0.9f) + phase) * 14f);
                float lightK = LightK(x, y);
                float pulse = 0.75f + 0.25f * MathF.Sin(time * 2f + phase * 2.3f);
                Main.EntitySpriteDraw(clump, new Vector2(x, y) - Main.screenPosition,
                    ClumpCell(seed + i * 7 + 3),
                    fresh * (0.30f * pulse * envelope * lightK),
                    time * (0.7f + h) * (h > 0.5f ? 1f : -1f),
                    clumpOrigin, 0.42f + 0.30f * Hash01(seed + i + 520), SpriteEffects.None, 0);
            }

            //——前缘斜扫流丝：风撕开的雪粒线，斜向与风一致——
            for (int i = 0; i < 8; i++) {
                float h = MathF.Sin(Projectile.identity * 1.9f + i * 2.45f);
                float y = SeamY + h * WallHalfHeight * 0.86f;
                if (MathF.Abs(y - SeamY) < SeamHalfHeight + 12f) {
                    continue;
                }
                float streakWob = MathF.Sin(time * 4.6f + i * 1.7f) * 12f;
                float lightK = LightK(frontX, y);
                Main.EntitySpriteDraw(spindle,
                    new Vector2(frontX + Dir * (6f + streakWob), y) - Main.screenPosition,
                    null, fresh * (0.35f * envelope * lightK),
                    MathHelper.PiOver2 + Dir * 0.30f,
                    spindleOrigin, new Vector2(0.15f, 1.7f + 0.6f * MathF.Abs(h)), SpriteEffects.None, 0);
            }

            //——明窗：窗内薄幕与上下雪唇由密度场承担，此处只剩一线弱光，远读「那里能过」——
            float seamPulse = 0.8f + 0.2f * MathF.Sin(time * 2.4f + Projectile.identity);
            Main.EntitySpriteDraw(glow, new Vector2(centerX, SeamY) - Main.screenPosition, null,
                seamLight * (0.20f * envelope * seamPulse), 0f, glowOrigin,
                new Vector2(HalfThickness * 3.4f / 64f, 0.5f), SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            float screenCenterX = Main.screenPosition.X + Main.screenWidth * 0.5f;
            if (MathF.Abs(Projectile.Center.X - screenCenterX) > Main.screenWidth * 0.5f + 420f) {
                return;
            }
            //余韵：散幕时抖落一阵流丝与松脱的雪团，风一停雪就撒下来
            for (int i = 0; i < 10; i++) {
                float y = SeamY + Main.rand.NextFloat(-WallHalfHeight, WallHalfHeight) * 0.8f;
                PRTLoader.NewParticle<PRT_FrostveilFlake>(
                    new Vector2(Projectile.Center.X + Main.rand.NextFloat(-HalfThickness, HalfThickness), y),
                    new Vector2(Dir * Main.rand.NextFloat(2f, 6f), Main.rand.NextFloat(-1f, 2f)),
                    new Color(230, 240, 250) * 0.5f, Main.rand.NextFloat(0.9f, 1.4f))
                    ?.Configure(Main.rand.Next(40, 70), Dir * 4f);
            }
            for (int i = 0; i < 6; i++) {
                float y = SeamY + Main.rand.NextFloat(-WallHalfHeight, WallHalfHeight) * 0.7f;
                PRTLoader.NewParticle<PRT_FrostveilClump>(
                    new Vector2(Projectile.Center.X + Main.rand.NextFloat(-HalfThickness, HalfThickness), y),
                    new Vector2(Dir * Main.rand.NextFloat(1f, 4f), Main.rand.NextFloat(0f, 1.5f)),
                    new Color(236, 243, 251) * 0.7f, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(Main.rand.Next(34, 54), Dir * 2f);
            }
        }
    }
}
