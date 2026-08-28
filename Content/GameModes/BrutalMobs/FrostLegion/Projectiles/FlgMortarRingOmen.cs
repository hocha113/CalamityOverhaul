using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostLegion.Projectiles
{
    /// <summary>
    /// 曲射雪炮落点警示环：ai[0]=(来源槽+1)|(来源类型&lt;&lt;8) ai[1]=档位。
    /// 生成位置即锁定落点（预告即承诺，装填期同步渐亮）；装填结束由环解算抛物线并发射大雪球视觉载体，
    /// 雪球抵达帧在环心迸出 4 片小雪片——方向读固定角度表 <see cref="ShardAngles"/>（世界系常量=非追踪保证），
    /// 相邻角距即逃生间隙，发射循环与预告虚影同读同一张表。
    /// 装填期来源死亡/槽位复用即取消（炮弹已在空中则照常落地，物理诚实），本体永不参与伤害
    /// </summary>
    internal class FlgMortarRingOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>装填帧数（任务口径 ≥34，各档位一律不缩短）</summary>
        internal const int LoadFrames = 34;
        /// <summary>炮弹飞行帧数（定时长弹道，发射速度反解）</summary>
        internal const int FlightFrames = 52;
        private const int BurstFadeFrames = 12;
        /// <summary>警示环半径（威胁区可视半径，雪片从环心迸出）</summary>
        private const float RingRadius = 74f;
        private const int RingDots = 10;
        /// <summary>
        /// 公平阀门：雪片固定 4 向角度表（世界系常量=非追踪保证，绝不读玩家位置）；
        /// 相邻角距 ≥0.6 弧度即逃生间隙，发射与虚影同读此表
        /// </summary>
        internal static readonly float[] ShardAngles = [-2.55f, -1.85f, -1.25f, -0.55f];
        /// <summary>雪片初速（档位每级 +0.35，角度表不变）</summary>
        private const float ShardSpeedBase = 5.4f;

        private static readonly Color RingWarn = new Color(150, 205, 255, 0);

        private int SrcPacked => (int)Projectile.ai[0];
        private int SrcIndex => (SrcPacked & 255) - 1;
        private int SrcType => SrcPacked >> 8;
        private int Tier => Math.Clamp((int)Projectile.ai[1], 1, 3);
        private int TotalLife => LoadFrames + FlightFrames + BurstFadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//纯预告体，伤害经由雪片
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LoadFrames + FlightFrames + BurstFadeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            //装填期来源校验：索引+类型双检，施法者死亡/槽位复用即取消发射（击杀装填中的雪人是有效反制）
            if (!Cancelled && elapsed < LoadFrames) {
                if (SrcIndex < 0 || SrcIndex >= Main.maxNPCs || !Main.npc[SrcIndex].active
                    || Main.npc[SrcIndex].type != SrcType) {
                    Cancelled = true;
                }
            }

            //装填期雪尘在环内零星起雾（≤1 粒/2 帧）
            if (!Cancelled && elapsed < LoadFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-RingRadius, RingRadius) * 0.8f, 0f),
                    DustID.Snow, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.9f)), 140, default, 0.8f);
                dust.noGravity = true;
            }

            //装填结束：反解抛物线，从雪人处发射大雪球视觉载体（定时长弹道，落点=环心）
            if (elapsed == LoadFrames && !Cancelled) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC anchor = SrcIndex >= 0 && SrcIndex < Main.maxNPCs ? Main.npc[SrcIndex] : null;
                    if (anchor != null && anchor.active && anchor.type == SrcType) {
                        Vector2 d = Projectile.Center - anchor.Center;
                        Vector2 vel = new Vector2(d.X / FlightFrames,
                            d.Y / FlightFrames - 0.5f * FlgMortarShellProj.Gravity * FlightFrames);
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), anchor.Center, vel,
                            ModContent.ProjectileType<FlgMortarShellProj>(), 0, 0f, Main.myPlayer, FlightFrames);
                    }
                    else {
                        Cancelled = true;
                    }
                }
            }

            //雪球抵达帧：环心迸裂 4 片雪片（固定角度表）
            if (elapsed == LoadFrames + FlightFrames && !Cancelled) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Emit();
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.7f, Pitch = -0.1f, MaxInstances = 4 }, Projectile.Center);
                    for (int i = 0; i < 6; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Snow,
                            (-Vector2.UnitY).RotatedByRandom(1.2f) * Main.rand.NextFloat(2f, 6f), 90, default,
                            Main.rand.NextFloat(1f, 1.6f));
                        dust.noGravity = Main.rand.NextBool();
                    }
                }
            }

            if (!Cancelled && elapsed >= LoadFrames) {
                float progress = MathHelper.Clamp((elapsed - LoadFrames) / (float)FlightFrames, 0f, 1f);
                Lighting.AddLight(Projectile.Center, RingWarn.R / 255f * 0.2f * progress,
                    RingWarn.G / 255f * 0.2f * progress, RingWarn.B / 255f * 0.2f * progress);
            }
        }

        /// <summary>迸裂帧发射：与虚影同一角度表，<see cref="ShardAngles"/> 是循环真正读取的方向</summary>
        private void Emit() {
            float speed = ShardSpeedBase + 0.35f * (Tier - 1);
            int shardType = ModContent.ProjectileType<FlgSnowShardProj>();
            for (int i = 0; i < ShardAngles.Length; i++) {
                Vector2 vel = ShardAngles[i].ToRotationVector2() * speed;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center - Vector2.UnitY * 6f, vel,
                    shardType, Projectile.damage, 1f, Main.myPlayer);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float fade = 1f;
            if (Cancelled) {
                fade = MathHelper.Clamp(1f - elapsed / (float)LoadFrames, 0f, 1f) * 0.35f;
            }
            else if (elapsed >= LoadFrames + FlightFrames) {
                fade = MathHelper.Clamp(1f - (elapsed - LoadFrames - FlightFrames) / (float)BurstFadeFrames, 0f, 1f);
            }
            if (fade <= 0.01f) {
                return false;
            }

            Vector2 groundPos = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;

            if (elapsed < LoadFrames + FlightFrames || Cancelled) {
                //装填+飞行期：圆周光点环（椭圆透视贴地）渐亮，飞行期脉动加急
                float progress = MathHelper.Clamp(elapsed / (float)LoadFrames, 0f, 1f);
                float urgency = elapsed <= LoadFrames ? 0f
                    : MathHelper.Clamp((elapsed - LoadFrames) / (float)FlightFrames, 0f, 1f);
                float fadeIn = MathHelper.Clamp(elapsed / 10f, 0f, 1f);
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * (10f + 8f * urgency) + Projectile.identity);
                float spin = Main.GlobalTimeWrappedHourly * 1.2f + Projectile.identity;
                for (int i = 0; i < RingDots; i++) {
                    float ang = spin + i * (MathHelper.TwoPi / RingDots);
                    Vector2 dotPos = groundPos + new Vector2(MathF.Cos(ang) * RingRadius, MathF.Sin(ang) * RingRadius * 0.38f);
                    Main.EntitySpriteDraw(glow, dotPos, null, RingWarn * ((0.3f + 0.35f * progress) * fadeIn * pulse * fade), 0f,
                        glow.Size() / 2f, 0.13f, SpriteEffects.None, 0);
                }
                //环心凝雪：随读秒增亮增大
                Main.EntitySpriteDraw(glow, groundPos, null, RingWarn * ((0.22f + 0.4f * (progress + urgency) * 0.5f) * fadeIn * fade), 0f,
                    glow.Size() / 2f, new Vector2(0.4f + 0.25f * urgency, 0.24f + 0.16f * urgency), SpriteEffects.None, 0);

                //雪片方向虚影：与迸裂同一角度表（原版雪球贴图，所见即所射）
                if (!Cancelled) {
                    Main.instance.LoadProjectile(ProjectileID.SnowBallHostile);
                    Texture2D ball = TextureAssets.Projectile[ProjectileID.SnowBallHostile].Value;
                    int frames = Main.projFrames[ProjectileID.SnowBallHostile] > 0 ? Main.projFrames[ProjectileID.SnowBallHostile] : 1;
                    Rectangle rect = ball.Frame(1, frames, 0, 0);
                    float ghostDist = 16f + 14f * (progress + urgency) * 0.5f;
                    for (int i = 0; i < ShardAngles.Length; i++) {
                        Vector2 pos = groundPos + ShardAngles[i].ToRotationVector2() * ghostDist;
                        Color ghost = new Color(214, 238, 255, 150) * (0.45f * (0.3f + 0.7f * progress) * pulse * fade);
                        Main.EntitySpriteDraw(ball, pos, rect, ghost, ShardAngles[i], rect.Size() / 2f,
                            0.5f, SpriteEffects.None, 0);
                    }
                }
                return false;
            }

            //迸裂：暗雪穹衬（真 alpha 轮廓）+ 加色亮芯 + 首帧白闪
            float rise = MathHelper.Clamp((elapsed - LoadFrames - FlightFrames + 1) / 5f, 0f, 1f);
            Texture2D under = CWRAsset.Extra_98.Value;
            float radius = RingRadius * (0.5f + 0.5f * rise);
            Vector2 domePos = groundPos - new Vector2(0f, radius * 0.3f);
            Main.EntitySpriteDraw(under, domePos, null, new Color(96, 118, 148) * (0.75f * fade), 0f, under.Size() / 2f,
                new Vector2(radius * 2.2f / under.Width, radius * 1.6f / under.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, domePos, null, new Color(214, 240, 255, 0) * (0.8f * fade), 0f, glow.Size() / 2f,
                new Vector2(radius * 1.4f / 100f, radius * 1f / 100f), SpriteEffects.None, 0);
            float flash = MathHelper.Clamp(1f - (elapsed - LoadFrames - FlightFrames) / 5f, 0f, 1f);
            if (flash > 0f) {
                Main.EntitySpriteDraw(glow, domePos, null, (Color.White with { A = 0 }) * (0.6f * flash), 0f,
                    glow.Size() / 2f, radius * 1f / 100f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
