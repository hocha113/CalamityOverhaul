using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Graveyard.Projectiles
{
    /// <summary>
    /// 渡鸦惊掠预告线：ai[0]=(来源槽+1)|(来源类型&lt;&lt;8) ai[1]=档位 ai[2]=锁定方向+10（0=未锁定）。
    /// 收翅期标线追踪目标并伴落羽尘，末段锁向白闪（锁向即承诺）；
    /// 掠面期保留为淡出余痕兼判定窗载体（命中黑暗减益据此判窗），本体永不参与伤害。
    /// 收翅期来源死亡/槽位复用即消散（击杀施法者是有效反制）
    /// </summary>
    internal class GyRavenSwoopOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>收翅预告帧数（≥30 契约；任务口径的 24 帧收翅姿态包含在内）</summary>
        internal const int TelegraphFrames = 30;
        /// <summary>末段锁定帧</summary>
        internal const int LockFrames = 10;
        /// <summary>掠面窗帧数（与 NPC 侧包络 rise+hold+decay 严格对齐）</summary>
        internal const int StrikeFrames = 24;
        /// <summary>标线长度</summary>
        private const float LaneLength = 320f;
        /// <summary>标线芯宽与柔光宽（画宽于鸦体，包住弧线俯冲的横向鼓包）</summary>
        private const float LaneCoreWidth = 16f;
        private const float LaneGlowWidth = 44f;

        //豁免声明：预告标线属光——纯加色发光体（A=0），按 M5 光类豁免不带遮挡外壳
        private static readonly Color LaneWarn = new Color(168, 140, 255, 0);
        private static readonly Color LaneCore = new Color(240, 236, 255, 0);

        private int SrcPacked => (int)Projectile.ai[0];
        private int SrcIndex => (SrcPacked & 255) - 1;
        private int SrcType => SrcPacked >> 8;
        private int TotalLife => TelegraphFrames + StrikeFrames;
        private int Elapsed => (int)Projectile.localAI[0] - Projectile.timeLeft;
        private bool Locked => Elapsed >= TelegraphFrames - LockFrames;
        internal bool InStrike => Elapsed >= TelegraphFrames;

        /// <summary>受害端判定：该渡鸦当前是否处于掠面窗（黑暗减益只在窗内挂）</summary>
        internal static bool IsStrikeWindowFor(int npcIndex) {
            int type = ModContent.ProjectileType<GyRavenSwoopOmen>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == type && ((int)proj.ai[0] & 255) - 1 == npcIndex
                    && proj.ModProjectile is GyRavenSwoopOmen omen && omen.InStrike) {
                    return true;
                }
            }
            return false;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 640;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 54;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = TotalLife;
                Projectile.timeLeft = TotalLife;
                //迟入玩家：首帧 ai[2] 已非零=服务端早过锁定帧，本地相位快进到锁定起点
                if (Projectile.ai[2] != 0f) {
                    Projectile.timeLeft = LockFrames + StrikeFrames;
                }
            }

            //来源校验：索引+类型双检，施法者死亡或槽位复用即消散
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
                //收翅追踪期：直读目标方向
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers) {
                    Player player = Main.player[target];
                    if (player.Alives()) {
                        Projectile.rotation = (player.Center - Projectile.Center).ToRotation();
                    }
                }
            }

            int elapsed = Elapsed;
            if (!VaultUtils.isServer) {
                if (elapsed == TelegraphFrames - LockFrames) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 4 }, Projectile.Center);
                }
                else if (elapsed == TelegraphFrames) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.55f, Pitch = 0.6f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            //收翅期落羽尘：鸦体处零星黑羽（≤1 粒/2 帧，所有端各自渲染=收翅可见）
            if (elapsed < TelegraphFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(anchor.position, anchor.width, anchor.height,
                    DustID.Smoke, 0f, 0.6f, 170, default, 0.8f);
                dust.noGravity = false;
                dust.velocity *= 0.3f;
            }

            Lighting.AddLight(Projectile.Center, LaneWarn.R / 255f * 0.1f,
                LaneWarn.G / 255f * 0.1f, LaneWarn.B / 255f * 0.1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float strength;
            if (InStrike) {
                //掠面余痕：可见窗与判定窗同一实体
                strength = MathHelper.Clamp(1f - (elapsed - TelegraphFrames) / (float)StrikeFrames, 0f, 1f) * 0.2f;
            }
            else {
                strength = MathHelper.Clamp(elapsed / 8f, 0f, 1f) * (Locked ? 1f : 0.5f);
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            float scaleX = LaneLength / tex.Width;
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);

            if (!Locked || InStrike) {
                Main.EntitySpriteDraw(tex, drawPos, null, LaneWarn * (0.45f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneCoreWidth / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, LaneWarn * (0.26f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneGlowWidth / tex.Height), SpriteEffects.None, 0);
            }
            else {
                //锁定期：白热窄闪，宣告掠面轨迹已承诺
                float lockT = MathHelper.Clamp((elapsed - (TelegraphFrames - LockFrames)) / (float)LockFrames, 0f, 1f);
                float flash = 0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 5f);
                Main.EntitySpriteDraw(tex, drawPos, null, LaneWarn * (0.6f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneGlowWidth + 14f) / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, LaneCore * (0.8f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneCoreWidth - 6f) / tex.Height), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
