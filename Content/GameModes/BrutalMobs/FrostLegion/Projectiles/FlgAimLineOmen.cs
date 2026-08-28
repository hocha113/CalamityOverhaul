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
    /// 雪球点射短标线：ai[0]=(来源槽+1)|(来源类型&lt;&lt;8) ai[1]=档位 ai[2]=锁定方向+10（0=未锁定）。
    /// 瞄准期标线追踪目标，末段锁向白闪（锁向即承诺）；提交帧与间隔帧沿锁定线两连原版雪球
    /// （<see cref="ProjectileID.SnowBallHostile"/> 弹体），两发同线=一板一眼的点射军操。
    /// 双发落定前来源死亡/槽位复用即消散（击杀施法者连第二发一并取消），本体永不参与伤害
    /// </summary>
    internal class FlgAimLineOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>瞄准帧数（任务口径 ≥30，各档位一律不缩短）</summary>
        internal const int TelegraphFrames = 32;
        /// <summary>末段锁向帧</summary>
        internal const int LockFrames = 12;
        /// <summary>两连间隔帧（点射节奏，同线第二发）</summary>
        internal const int DoubleTapGapFrames = 7;
        /// <summary>余痕帧数</summary>
        internal const int RemnantFrames = 12;
        /// <summary>短标线长度（方向指示，非全程弹道承诺；雪球带原版微重力）</summary>
        private const float LaneLength = 190f;
        private const float LaneCoreWidth = 10f;
        private const float LaneGlowWidth = 30f;
        /// <summary>雪球出膛速度（档位只调弹速不换机制）</summary>
        private static readonly float[] BallSpeedByTier = [11.5f, 12.5f, 13.5f];

        //豁免声明：瞄准标线属光——纯加色发光体（A=0），按 M5 光类豁免不带遮挡外壳
        private static readonly Color LaneWarn = new Color(150, 200, 255, 0);
        private static readonly Color LaneCore = new Color(238, 248, 255, 0);

        private int SrcPacked => (int)Projectile.ai[0];
        private int SrcIndex => (SrcPacked & 255) - 1;
        private int SrcType => SrcPacked >> 8;
        private int Tier => Math.Clamp((int)Projectile.ai[1], 1, 3);
        private int TotalLife => TelegraphFrames + DoubleTapGapFrames + RemnantFrames;
        private int Elapsed => (int)Projectile.localAI[0] - Projectile.timeLeft;
        private bool Locked => Elapsed >= TelegraphFrames - LockFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 640;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 51;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害（伤害经由雪球）</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = TotalLife;
                Projectile.timeLeft = TotalLife;
                //迟入玩家：首帧 ai[2] 已非零=服务端早过锁向帧，本地相位快进到锁向起点
                if (Projectile.ai[2] != 0f) {
                    Projectile.timeLeft = LockFrames + DoubleTapGapFrames + RemnantFrames;
                }
            }

            //来源校验：索引+类型双检；双发落定前施法者死亡/槽位复用即消散
            if (SrcIndex < 0 || SrcIndex >= Main.maxNPCs) {
                Projectile.Kill();
                return;
            }
            NPC anchor = Main.npc[SrcIndex];
            if (!anchor.active || anchor.type != SrcType) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;

            if (Projectile.ai[2] != 0f) {
                Projectile.rotation = Projectile.ai[2] - 10f;
            }
            else if (!Locked) {
                //瞄准追踪期：直读目标方向
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers) {
                    Player player = Main.player[target];
                    if (player.Alives()) {
                        Projectile.rotation = (player.Center - Projectile.Center).ToRotation();
                    }
                }
            }

            int elapsed = Elapsed;
            if (!VaultUtils.isServer && elapsed == TelegraphFrames - LockFrames) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.38f, Pitch = 0.1f, MaxInstances = 4 }, Projectile.Center);
            }

            //提交帧与间隔帧两连雪球：同一锁定方向（两发同线）
            if (elapsed == TelegraphFrames || elapsed == TelegraphFrames + DoubleTapGapFrames) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 vel = Projectile.rotation.ToRotationVector2() * BallSpeedByTier[Tier - 1];
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        Projectile.Center + vel.SafeNormalize(Vector2.UnitX) * 12f, vel,
                        ProjectileID.SnowBallHostile, Projectile.damage, 1f, Main.myPlayer);
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            //瞄准期霜屑沿线（≤1 粒/2 帧）
            if (elapsed < TelegraphFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 dir = Projectile.rotation.ToRotationVector2();
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(14f, LaneLength * 0.6f),
                    DustID.Frost, dir * 0.5f, 150, default, 0.8f);
                dust.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, LaneWarn.R / 255f * 0.1f,
                LaneWarn.G / 255f * 0.1f, LaneWarn.B / 255f * 0.1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float strength;
            if (elapsed >= TelegraphFrames) {
                //出手余痕（覆盖两连窗，收势可读）
                strength = MathHelper.Clamp(1f - (elapsed - TelegraphFrames) / (float)(DoubleTapGapFrames + RemnantFrames), 0f, 1f) * 0.3f;
            }
            else {
                strength = MathHelper.Clamp(elapsed / 8f, 0f, 1f) * (Locked ? 1f : 0.55f);
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            float scaleX = LaneLength / tex.Width;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity);

            if (!Locked || elapsed >= TelegraphFrames) {
                Main.EntitySpriteDraw(tex, drawPos, null, LaneWarn * (0.45f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneCoreWidth / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, LaneWarn * (0.24f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneGlowWidth / tex.Height), SpriteEffects.None, 0);
            }
            else {
                //锁向期：白热窄闪，宣告弹线已承诺
                float lockT = MathHelper.Clamp((elapsed - (TelegraphFrames - LockFrames)) / (float)LockFrames, 0f, 1f);
                float flash = 0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 5f);
                Main.EntitySpriteDraw(tex, drawPos, null, LaneWarn * (0.6f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneGlowWidth + 10f) / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, LaneCore * (0.85f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneCoreWidth - 4f) / tex.Height), SpriteEffects.None, 0);
            }

            //枪口幽灵雪球：随瞄准进度凝实（原版贴图，所见即所射）
            if (elapsed < TelegraphFrames) {
                Main.instance.LoadProjectile(ProjectileID.SnowBallHostile);
                Texture2D ball = TextureAssets.Projectile[ProjectileID.SnowBallHostile].Value;
                int frames = Main.projFrames[ProjectileID.SnowBallHostile] > 0 ? Main.projFrames[ProjectileID.SnowBallHostile] : 1;
                Rectangle rect = ball.Frame(1, frames, 0, 0);
                float progress = elapsed / (float)TelegraphFrames;
                Vector2 muzzle = drawPos + Projectile.rotation.ToRotationVector2() * 18f;
                Main.EntitySpriteDraw(ball, muzzle, rect, new Color(220, 240, 255, 170) * (0.6f * progress * pulse),
                    Projectile.rotation, rect.Size() / 2f, 0.6f + 0.3f * progress, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
