using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Silkcrypt.Projectiles
{
    /// <summary>
    /// 垂袭蛛影。ai[0]=垂降行程(像素) ai[1]=视觉种子。
    /// 预告 46 帧（丝线反光闪烁 + 两声丝弦，视听双通道）→ 黑影沿丝速降（仅降程与
    /// 底端 4 帧有判定，擦中只掉微量血）→ 收回（无判定，影子爬回顶洞）→ 丝线残留
    /// 飘动 150 帧渐散。这是环境"影"，不生成任何真蜘蛛 NPC；
    /// 全时间轴是 timeLeft 的确定函数，各端各自展开
    /// </summary>
    internal class SilkcryptDropShadowProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WebSpit;

        /// <summary>预告帧数（公平契约 ≥45，档位一律不缩短）</summary>
        private const int TelegraphFrames = 46;
        /// <summary>底端悬停帧数（判定随降程一起结束）</summary>
        private const int DwellFrames = 4;
        /// <summary>丝线残留飘动帧数（余韵）</summary>
        private const int LingerFrames = 150;
        /// <summary>速降速度（像素/帧，决定降程时长）</summary>
        private const float DescendSpeed = 26f;
        /// <summary>收回速度（略慢于速降，读得出"收"）</summary>
        private const float RetractSpeed = 17f;
        /// <summary>判定盒尺寸（贴着可见剪影）</summary>
        private const int HitW = 30;
        private const int HitH = 34;

        private float DropLen => Projectile.ai[0];
        private int Seed => (int)Projectile.ai[1];

        private int DescFrames => Math.Clamp((int)(DropLen / DescendSpeed) + 1, 8, 18);
        private int RetractFrames => Math.Clamp((int)(DropLen / RetractSpeed) + 1, 10, 26);
        private int TotalLife => TelegraphFrames + DescFrames + DwellFrames + RetractFrames + LingerFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 460;

        public override void SetDefaults() {
            Projectile.width = HitW;
            Projectile.height = HitH;
            Projectile.hostile = false;//仅降程窗口置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>黑影当前沿丝进度 0~1（0=锚点 1=底端），时间轴的纯函数</summary>
        private float ShadowProgress {
            get {
                int t = Elapsed - TelegraphFrames;
                if (t < 0) {
                    return 0f;
                }
                if (t < DescFrames) {
                    //速降带一点加速度：越垂越快
                    float x = t / (float)DescFrames;
                    return MathF.Pow(x, 1.3f);
                }
                t -= DescFrames;
                if (t < DwellFrames) {
                    return 1f;
                }
                t -= DwellFrames;
                if (t < RetractFrames) {
                    return 1f - t / (float)RetractFrames;
                }
                return 0f;
            }
        }

        /// <summary>黑影仍挂在丝上（降/停/收三段）</summary>
        private bool ShadowVisible
            => Elapsed >= TelegraphFrames
            && Elapsed < TelegraphFrames + DescFrames + DwellFrames + RetractFrames;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //行程由 ai[0] 决定，两端以同一 ai 值各自展开时间轴
                Projectile.timeLeft = TotalLife;
            }
            int elapsed = Elapsed;

            //判定窗 = 可见降程窗（含底端短停）；Boss 在场则伤害通道静默
            Projectile.hostile = !CWRWorld.HasBoss
                && elapsed >= TelegraphFrames
                && elapsed < TelegraphFrames + DescFrames + DwellFrames;

            if (Main.dedServ) {
                return;
            }

            //预告：两声丝弦 + 线上反光（绘制层），听觉通道在这
            if (elapsed == 0) {
                SoundEngine.PlaySound(SoundID.Item17 with {
                    Volume = 0.24f, Pitch = -0.4f, MaxInstances = 4,
                }, Projectile.Center);
            }
            else if (elapsed == 28) {
                SoundEngine.PlaySound(SoundID.Item17 with {
                    Volume = 0.18f, Pitch = -0.1f, MaxInstances = 4,
                }, Projectile.Center);
            }
            else if (elapsed == TelegraphFrames) {
                //落影：翼风般的一声急掠
                SoundEngine.PlaySound(SoundID.Item32 with {
                    Volume = 0.4f, Pitch = -0.3f, MaxInstances = 4,
                }, Projectile.Center + new Vector2(0f, DropLen * 0.5f));
            }
            else if (elapsed == TelegraphFrames + DescFrames + DwellFrames) {
                //收回起手：丝线回卷的轻响
                SoundEngine.PlaySound(SoundID.Item17 with {
                    Volume = 0.14f, Pitch = 0.2f, MaxInstances = 4,
                }, Projectile.Center);
            }

            //降程沿途网尘（≤0.5 粒/帧）
            if (ShadowVisible && elapsed % 2 == 0
                && elapsed < TelegraphFrames + DescFrames) {
                Dust dust = Dust.NewDustPerfect(ShadowPos() + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.Web, new Vector2(0f, -0.4f), 150, default, 0.7f);
                dust.noGravity = true;
            }
        }

        private Vector2 ShadowPos() => Projectile.Center + new Vector2(0f, DropLen * ShadowProgress);

        /// <summary>判定贴着可见剪影走（hostile 已按窗口门控）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            Vector2 pos = ShadowPos();
            Rectangle box = new((int)pos.X - HitW / 2, (int)pos.Y - HitH / 2, HitW, HitH);
            return box.Intersects(targetHitbox);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) {
                return false;
            }
            Vector2 anchor = Projectile.Center - Main.screenPosition;

            //丝线可见长度与透明度分相
            float lineLen;
            float lineAlpha;
            bool linger = elapsed >= TotalLife - LingerFrames;
            if (elapsed < TelegraphFrames) {
                //预告：丝线自顶向下缓缓放到全长，低透明度 + 闪烁
                float t = elapsed / (float)TelegraphFrames;
                lineLen = DropLen * Math.Min(t * 1.25f, 1f);
                float flicker = 0.72f + 0.28f * MathF.Sin(Main.GlobalTimeWrappedHourly * 17f + Seed);
                lineAlpha = 0.24f * flicker;
            }
            else if (!linger) {
                lineLen = DropLen;
                lineAlpha = 0.34f;
            }
            else {
                //余韵：残丝飘动渐散
                float t = (elapsed - (TotalLife - LingerFrames)) / (float)LingerFrames;
                lineLen = DropLen * (1f - 0.15f * t);
                lineAlpha = 0.3f * (1f - t);
            }
            if (lineAlpha > 0.01f && lineLen > 4f) {
                DrawSilkLine(px, anchor, lineLen, lineAlpha, linger, elapsed);
            }

            //锚口小网团（丝从哪来要有交代）
            Texture2D wad = TextureAssets.Projectile[Type].Value;
            float wadAlpha = linger
                ? MathHelper.Clamp(1f - (elapsed - (TotalLife - LingerFrames)) / (float)LingerFrames, 0f, 1f)
                : 1f;
            Color wadColor = Color.Lerp(lightColor, new Color(226, 226, 236), 0.6f) * (0.8f * wadAlpha);
            Main.EntitySpriteDraw(wad, anchor, null, wadColor, Seed * 0.9f,
                wad.Size() / 2f, 0.62f, SpriteEffects.None, 0);

            //黑影本体：近黑剪影 + 双残影承载速度
            if (ShadowVisible) {
                DrawShade(anchor, elapsed);
            }
            return false;
        }

        /// <summary>丝线：分四段画，余韵期逐段横摆（越靠线尾摆幅越大）+ 尾端散丝</summary>
        private void DrawSilkLine(Texture2D px, Vector2 anchor, float lineLen, float alpha, bool linger, int elapsed) {
            Color lineColor = new Color(214, 218, 232) * alpha;
            const int Segs = 4;
            float segLen = lineLen / Segs;
            Vector2 segTop = anchor;
            for (int i = 0; i < Segs; i++) {
                float sway = linger
                    ? MathF.Sin(Main.GlobalTimeWrappedHourly * 2.3f + Seed + i * 0.9f) * 3.4f * (i + 1) / Segs
                    : 0f;
                Vector2 segBottom = anchor + new Vector2(sway, segLen * (i + 1));
                Vector2 delta = segBottom - segTop;
                float rot = delta.ToRotation();
                Main.EntitySpriteDraw(px, segTop, null, lineColor, rot,
                    new Vector2(0f, 0.5f), new Vector2(delta.Length(), 1.4f), SpriteEffects.None, 0);
                segTop = segBottom;
            }

            //预告期的行进反光点：一小节加色亮段沿线下走（"有东西要顺着它下来"）
            if (elapsed < TelegraphFrames) {
                float glintT = elapsed % 23 / 23f;
                Vector2 glintPos = anchor + new Vector2(0f, lineLen * glintT);
                Color glint = new Color(255, 255, 255, 0) * (alpha * 2.2f);
                Main.EntitySpriteDraw(px, glintPos, null, glint, MathHelper.PiOver2,
                    new Vector2(0f, 0.5f), new Vector2(16f, 1.8f), SpriteEffects.None, 0);
            }

            //余韵尾端散丝：断口不许平切
            if (linger) {
                Color fray = lineColor * 0.7f;
                Main.EntitySpriteDraw(px, segTop, null, fray, MathHelper.PiOver2 + 0.45f,
                    new Vector2(0f, 0.5f), new Vector2(9f, 1.1f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(px, segTop, null, fray, MathHelper.PiOver2 - 0.4f,
                    new Vector2(0f, 0.5f), new Vector2(7f, 1.1f), SpriteEffects.None, 0);
            }
        }

        /// <summary>黑影剪影：借爬墙蛛贴图压近黑，足步换帧，残影拖在运动反向</summary>
        private void DrawShade(Vector2 anchor, int elapsed) {
            Main.instance.LoadNPC(NPCID.WallCreeperWall);
            Texture2D spider = TextureAssets.Npc[NPCID.WallCreeperWall].Value;
            int frameCount = Math.Max(Main.npcFrameCount[NPCID.WallCreeperWall], 1);

            float progress = ShadowProgress;
            Vector2 pos = anchor + new Vector2(0f, DropLen * progress);
            bool descending = elapsed < TelegraphFrames + DescFrames + DwellFrames;
            //降=头朝下(Pi)，收=头朝上(0)；足步按走过的丝长换帧
            float rot = descending ? MathHelper.Pi : 0f;
            int frame = (int)(DropLen * progress / 13f) % frameCount;
            Rectangle src = spider.Frame(1, frameCount, 0, frame);
            Vector2 origin = src.Size() * 0.5f;

            //底端短停时微微张足（擦中判定的可读锚点）
            bool dwelling = elapsed >= TelegraphFrames + DescFrames
                && elapsed < TelegraphFrames + DescFrames + DwellFrames;
            Vector2 scale = new(dwelling ? 1.14f : 1f, 1f);

            Color body = new Color(13, 9, 17) * 0.9f;
            float ghostStep = descending ? -14f : 10f;//残影拖在运动反向
            Main.EntitySpriteDraw(spider, pos + new Vector2(0f, ghostStep * 2f), src,
                body * 0.18f, rot, origin, scale * 0.94f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(spider, pos + new Vector2(0f, ghostStep), src,
                body * 0.42f, rot, origin, scale * 0.97f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(spider, pos, src, body, rot, origin, scale, SpriteEffects.None, 0);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 6f),
                    DustID.Web, new Vector2(0f, Main.rand.NextFloat(0.2f, 0.7f)),
                    140, default, 0.8f);
                dust.noGravity = true;
            }
        }
    }
}
