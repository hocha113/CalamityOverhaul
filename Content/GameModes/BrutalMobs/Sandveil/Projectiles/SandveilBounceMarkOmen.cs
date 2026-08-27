using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Sandveil.Projectiles
{
    /// <summary>
    /// 滚草压制弹跳的双模预兆。ai[0]=来源NPC+1|类型&lt;&lt;8 ai[1]=可见帧数 ai[2]=模式。
    /// 模式 0=落点标记：起跳帧锁定的着地点（预告即承诺，可见时长=滞空+余辉，一律 ≥30 帧）；
    /// 模式 1=压地蓄力环：起跳前 24 帧的压扁蓄力可见信号（无落点承诺语义）。
    /// 来源死亡则黯淡余痕（弹跳不会再来）；永不造成伤害
    /// </summary>
    internal class SandveilBounceMarkOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>落点标记的落地后余辉帧</summary>
        internal const int LingerFrames = 12;

        /// <summary>落点标记半宽：比滚草判定更宽，把弹道横向余差包进警示范围</summary>
        private const float MarkHalfWidth = 58f;
        /// <summary>蓄力环半宽</summary>
        private const float CrouchHalfWidth = 40f;

        //色板参考 DuneStorm 沙漠色板，数值抄色、代码独立
        private static readonly Color SandDeep = new(140, 108, 62);
        private static readonly Color WarnGlow = new(255, 200, 110, 0);
        private static readonly Color SandBright = new(232, 202, 126, 0);

        private int Packed => (int)Projectile.ai[0];
        private int SourceIndex => (Packed & 255) - 1;
        private int SourceType => (Packed >> 8) & 0xFFF;
        private int TotalLife => Math.Max(1, (int)Projectile.ai[1]);
        private bool CrouchMode => Projectile.ai[2] == 1f;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                //可见帧数从同步的 ai[1] 各端确定性展开（滞空随跳序递增，标记时长同步递增）
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(CrouchMode
                        ? SoundID.WormDig with { Volume = 0.45f, Pitch = -0.3f, MaxInstances = 5 }
                        : SoundID.Item1 with { Volume = 0.45f, Pitch = -0.4f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            //来源校验：滚草死亡/槽位复用则黯淡（击杀施法者是有效反制）
            if (!Cancelled) {
                if (SourceIndex < 0 || SourceIndex >= Main.maxNPCs || !Main.npc[SourceIndex].active
                    || Main.npc[SourceIndex].type != SourceType) {
                    Cancelled = true;
                }
            }
            if (Cancelled || Main.dedServ) {
                return;
            }

            float progress = MathHelper.Clamp(Elapsed / (float)TotalLife, 0f, 1f);
            if (CrouchMode) {
                //压地蓄力：贴地横喷的压扁尘（≤2 粒/帧）
                if (Main.rand.NextBool(2)) {
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    Dust press = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(side * Main.rand.NextFloat(6f, CrouchHalfWidth * 0.6f), 2f),
                        DustID.Sand, new Vector2(side * Main.rand.NextFloat(1.5f, 3f + 2f * progress), -0.3f),
                        120, default, 0.9f + 0.5f * progress);
                    press.noGravity = true;
                }
            }
            else if (Main.rand.NextBool(3)) {
                //落点标记：地表低频警示沙星
                Dust seep = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-0.7f, 0.7f) * MarkHalfWidth, 2f),
                    DustID.Sand, new Vector2(0f, -0.5f - 1.2f * progress), 130, default, 0.8f + 0.4f * progress);
                seep.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.14f, 0.11f, 0.05f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Elapsed / 6f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity * 0.8f);
            float strength = fadeIn * fadeOut * pulse * (Cancelled ? 0.3f : 1f);
            if (strength <= 0.01f) {
                return false;
            }
            float progress = MathHelper.Clamp(Elapsed / (float)TotalLife, 0f, 1f);

            Texture2D rim = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 pos = Projectile.Center + new Vector2(0f, 2f) - Main.screenPosition;
            float halfWidth = CrouchMode ? CrouchHalfWidth : MarkHalfWidth;

            //暗沙实底外圈（真 alpha）+ 加色芯；落点标记随临近着地增亮增宽（紧迫度）
            float urgency = CrouchMode ? progress : 0.55f + 0.45f * progress;
            Vector2 rimScale = new Vector2(halfWidth * 2f * urgency / rim.Width, 24f / rim.Height) * 1.15f;
            Main.EntitySpriteDraw(rim, pos, null, SandDeep * (0.7f * strength), 0f,
                rim.Size() / 2f, rimScale, SpriteEffects.None, 0);
            Color core = CrouchMode ? SandBright : WarnGlow;
            Main.EntitySpriteDraw(glow, pos, null, core * (0.75f * strength * urgency), 0f,
                glow.Size() / 2f, new Vector2(halfWidth * 2f * urgency / glow.Width, 20f / glow.Height),
                SpriteEffects.None, 0);

            //蓄力环：两侧内挤的短光楔，读作被压扁的势能
            if (CrouchMode) {
                for (int side = -1; side <= 1; side += 2) {
                    Vector2 wedge = pos + new Vector2(side * (CrouchHalfWidth - 14f * progress), -4f);
                    Main.EntitySpriteDraw(glow, wedge, null, WarnGlow * (0.5f * strength * progress), 0f,
                        glow.Size() / 2f, new Vector2(0.45f, 0.16f), SpriteEffects.None, 0);
                }
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
