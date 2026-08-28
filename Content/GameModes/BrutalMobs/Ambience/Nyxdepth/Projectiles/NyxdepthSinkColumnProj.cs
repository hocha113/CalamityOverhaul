using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Nyxdepth.Projectiles
{
    /// <summary>
    /// 「下沉流」垂直下沉水流场地实体（本原型深渊独有：竖直向下的水体拖拽，
    /// 与 Tidecall 海面离岸流、Lumindepth 水下旋涡在深度与形状上分层）。<br/>
    /// ai[0]=半宽像素 ai[1]=拖拽存续帧。时间轴：预告 78 帧（一列气泡反向上涌+低鸣，
    /// 双通道≥45 帧）→ 温和向下拖拽 → 平息淡出。<br/>
    /// 恒零伤害，纯浮力管理挑战：拖拽只作用于区内入水的本机玩家（本机改自身速度，原生同步），
    /// 力度可挣脱（竖向可游离、横向 260 像素即出界）；Boss 在场时拖拽悬停只留画面。
    /// 可见区=判定区，绘制与判定读同一几何
    /// </summary>
    internal class NyxdepthSinkColumnProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧数（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int TelegraphFrames = 78;
        private const int FadeFrames = 40;
        /// <summary>柱体半高（像素）</summary>
        private const float HalfHeightPx = 380f;
        /// <summary>每帧下拽加速度</summary>
        private const float SinkAccel = 0.18f;
        /// <summary>下拽速度封顶（低于自由下落尾速，保证可挣脱）</summary>
        private const float SinkMax = 3.2f;

        private float HalfWidth => Projectile.ai[0];
        private int PullFrames => (int)Projectile.ai[1];
        private int TotalLife => TelegraphFrames + PullFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 520;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//恒零伤害的环境场地
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //存续期由 ai[1] 决定，两端以同一 ai 值各自展开时间轴
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    //预告听觉通道其一：低鸣起势
                    SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with {
                        Volume = 0.6f,
                        Pitch = -0.55f,
                        MaxInstances = 3
                    }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;
            bool pulling = elapsed >= TelegraphFrames && elapsed < TelegraphFrames + PullFrames;

            if (elapsed < TelegraphFrames) {
                UpdateTelegraph(elapsed);
                return;
            }
            if (elapsed == TelegraphFrames && !Main.dedServ) {
                //落地拍：水流咬合的深部闷响+重水声
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.85f,
                    Pitch = -0.8f,
                    MaxInstances = 3
                }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with {
                    Volume = 0.5f,
                    Pitch = -0.95f,
                    MaxInstances = 2
                }, Projectile.Center);
            }
            if (!pulling) {
                return;//平息期：只剩淡出的残流画面
            }

            //Boss 在场或残酷模式中途关闭：位移机制悬停，画面走完（公约：造成位移的机制暂停）
            if (GameModeSystem.BrutalActive && !CWRWorld.HasBoss && !Main.dedServ) {
                Player player = Main.LocalPlayer;
                if (player.active && !player.dead && player.wet && InZone(player.Hitbox)
                    && player.velocity.Y < SinkMax) {
                    //温和下拽：只加不设，跳跃与上游动作照常生效
                    player.velocity.Y = MathF.Min(player.velocity.Y + SinkAccel, SinkMax);
                }
            }

            if (Main.dedServ) {
                return;
            }
            //拖拽期画面：下行水痕（≤2 粒/帧）+偶发被拽下的气泡
            for (int i = 0; i < 2; i++) {
                if (!Main.rand.NextBool(3)) {
                    continue;
                }
                Dust streak = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth),
                        Main.rand.NextFloat(-HalfHeightPx, HalfHeightPx * 0.6f)),
                    DustID.DungeonWater, new Vector2(0f, Main.rand.NextFloat(3f, 4.6f)),
                    120, new Color(50, 90, 110), Main.rand.NextFloat(1.1f, 1.6f));
                streak.noGravity = true;
            }
            if (Main.rand.NextBool(9)) {
                Dust bubble = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth * 0.8f, HalfWidth * 0.8f),
                        -Main.rand.NextFloat(0f, HalfHeightPx)),
                    DustID.BubbleBlock, new Vector2(0f, Main.rand.NextFloat(1.2f, 2.2f)),
                    100, default, Main.rand.NextFloat(0.8f, 1.2f));
                bubble.noGravity = true;
            }
            //拖拽期的持续水涌低音
            if (elapsed % 34 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with {
                    Volume = 0.22f,
                    Pitch = -0.8f,
                    MaxInstances = 2
                }, Projectile.Center);
            }
        }

        /// <summary>预告：一列气泡反向上涌（视觉通道）+周期软水泡声与中段低鸣（听觉通道）</summary>
        private void UpdateTelegraph(int elapsed) {
            if (Main.dedServ) {
                return;
            }
            float spread = elapsed / (float)TelegraphFrames;
            //气泡自柱体下半段向上急涌，与即将到来的下拽方向刻意相反
            for (int i = 0; i < 2; i++) {
                if (!Main.rand.NextBool(2)) {
                    continue;
                }
                Dust bubble = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(
                        Main.rand.NextFloat(-HalfWidth, HalfWidth) * spread,
                        Main.rand.NextFloat(0f, HalfHeightPx)),
                    DustID.BubbleBlock, new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(2.6f, 4.4f)),
                    80, default, Main.rand.NextFloat(0.9f, 1.4f));
                bubble.noGravity = true;
            }
            if (elapsed % 16 == 0) {
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.13f,
                    Pitch = Main.rand.NextFloat(0.2f, 0.5f),
                    MaxInstances = 4
                }, Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), Main.rand.NextFloat(-60f, 60f)));
            }
            if (elapsed == 42) {
                //中段低鸣补一拍，预告全程听觉不断档
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with {
                    Volume = 0.4f,
                    Pitch = -0.65f,
                    MaxInstances = 3
                }, Projectile.Center);
            }
        }

        /// <summary>判定盒与绘制共用同一几何（可见区=判定区）</summary>
        private bool InZone(Rectangle hitbox) {
            Rectangle zone = new((int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - HalfHeightPx),
                (int)(HalfWidth * 2f), (int)(HalfHeightPx * 2f));
            return zone.Intersects(hitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D spindle = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (spindle == null || spindle.IsDisposed || glow == null || glow.IsDisposed) {
                return false;
            }
            int elapsed = Elapsed;
            float spread = elapsed < TelegraphFrames ? elapsed / (float)TelegraphFrames : 1f;
            float alpha;
            if (elapsed >= TelegraphFrames + PullFrames) {
                alpha = MathHelper.Clamp(1f - (elapsed - TelegraphFrames - PullFrames) / (float)FadeFrames, 0f, 1f);
            }
            else if (elapsed < TelegraphFrames) {
                alpha = 0.3f + 0.3f * spread;
            }
            else {
                alpha = 1f;
            }
            if (alpha <= 0.01f) {
                return false;
            }

            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 origin = spindle.Size() * 0.5f;
            float widthPx = HalfWidth * 2f * spread;
            //柱身暗水体：Extra_98 真 alpha，读作一道更深色的水（预告期薄，拖拽期成形）
            float bodyA = (elapsed < TelegraphFrames ? 0.16f : 0.34f) * alpha;
            DrawSprite(spindle, center, new Color(8, 18, 28) * bodyA,
                new Vector2(widthPx / 47f, HalfHeightPx * 2f / 47f), origin);

            //两缘细流光（SoftGlow 黑底 A=0 加色敷料，可见长约 52×scale）：标出边界，玩家横向脱离有明确读数
            Vector2 glowOrigin = glow.Size() * 0.5f;
            Color edge = new Color(60, 140, 150, 0) * (0.35f * alpha);
            for (int i = -1; i <= 1; i += 2) {
                DrawSprite(glow, center + new Vector2(i * HalfWidth * spread, 0f), edge,
                    new Vector2(0.10f, HalfHeightPx * 2f / 52f), glowOrigin);
            }

            //拖拽期内部下行流线（SoftGlow 黑底 A=0 加色）：确定性相位滚动，读出水在往下走
            if (elapsed >= TelegraphFrames && elapsed < TelegraphFrames + PullFrames) {
                for (int i = 0; i < 3; i++) {
                    float phase = (Main.GlobalTimeWrappedHourly * 1.6f + i * 0.37f + Projectile.identity * 0.13f) % 1f;
                    float x = MathF.Sin(Projectile.identity * 2.1f + i * 2.7f) * HalfWidth * 0.6f;
                    float y = -HalfHeightPx + phase * HalfHeightPx * 2f;
                    float streakA = MathF.Sin(phase * MathHelper.Pi);
                    DrawSprite(glow, center + new Vector2(x, y),
                        new Color(70, 130, 150, 0) * (0.30f * alpha * streakA),
                        new Vector2(0.06f, 1.45f), glowOrigin);
                }
            }
            return false;
        }

        private static void DrawSprite(Texture2D tex, Vector2 pos, Color color, Vector2 scale, Vector2 origin) {
            Main.EntitySpriteDraw(tex, pos, null, color, 0f, origin, scale, SpriteEffects.None, 0);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //收场轻叹：残余气泡缓缓归位
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Volume = 0.4f,
                Pitch = -0.25f,
                MaxInstances = 2
            }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust bubble = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth),
                        Main.rand.NextFloat(-HalfHeightPx * 0.5f, HalfHeightPx * 0.5f)),
                    DustID.BubbleBlock, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.4f)),
                    100, default, Main.rand.NextFloat(0.8f, 1.1f));
                bubble.noGravity = true;
            }
        }
    }
}
