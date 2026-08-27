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
    /// 刺客突进刀光标线：ai[0]=(来源槽+1)|(来源类型&lt;&lt;8) ai[1]=档位 ai[2]=锁定方向+10（0=未锁定）。
    /// 压身期标线追踪目标横向朝向，末段锁向并"刀光一闪"（锁向即承诺）；
    /// 突进期保留为淡出余痕（余痕可见窗=突进窗）。压身期来源死亡/槽位复用即消散，本体永不参与伤害
    /// </summary>
    internal class FlgStabGlintOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>压身预告帧数（≥30 契约；任务口径的 ≥24 帧压身包含在内）</summary>
        internal const int TelegraphFrames = 30;
        /// <summary>末段锁向帧（刀光一闪窗）</summary>
        internal const int LockFrames = 10;
        /// <summary>突进窗帧数（与 NPC 侧包络 rise+hold+decay 严格对齐）</summary>
        internal const int StrikeFrames = 22;
        /// <summary>标线长度（贴合突进实际行程）</summary>
        private const float LaneLength = 300f;
        private const float LaneCoreWidth = 8f;
        private const float LaneGlowWidth = 26f;

        //豁免声明：刀光标线属光——纯加色发光体（A=0），按 M5 光类豁免不带遮挡外壳
        private static readonly Color GlintWarn = new Color(190, 214, 240, 0);
        private static readonly Color GlintCore = new Color(250, 252, 255, 0);

        private int SrcPacked => (int)Projectile.ai[0];
        private int SrcIndex => (SrcPacked & 255) - 1;
        private int SrcType => SrcPacked >> 8;
        private int TotalLife => TelegraphFrames + StrikeFrames;
        private int Elapsed => (int)Projectile.localAI[0] - Projectile.timeLeft;
        private bool Locked => Elapsed >= TelegraphFrames - LockFrames;
        private bool InStrike => Elapsed >= TelegraphFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 640;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 52;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = TotalLife;
                Projectile.timeLeft = TotalLife;
                //迟入玩家：首帧 ai[2] 已非零=服务端早过锁向帧，本地相位快进到锁向起点
                if (Projectile.ai[2] != 0f) {
                    Projectile.timeLeft = LockFrames + StrikeFrames;
                }
            }

            //来源校验：索引+类型双检，施法者死亡或槽位复用即消散（玻璃刺客死了刀就出不来）
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
                //压身追踪期：只追横向朝向（地面直线突进，标线水平）
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers) {
                    Player player = Main.player[target];
                    if (player.Alives()) {
                        Projectile.rotation = player.Center.X >= Projectile.Center.X ? 0f : MathHelper.Pi;
                    }
                }
            }

            int elapsed = Elapsed;
            if (!VaultUtils.isServer) {
                if (elapsed == TelegraphFrames - LockFrames) {
                    //刀光一闪：出鞘脆响
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.42f, Pitch = 0.35f, MaxInstances = 4 }, Projectile.Center);
                }
                else if (elapsed == TelegraphFrames) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = 0.45f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            //压身期寒芒（≤1 粒/2 帧，读作低伏蓄力）
            if (elapsed < TelegraphFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(anchor.position, anchor.width, anchor.height,
                    DustID.Ice, Projectile.rotation.ToRotationVector2().X * 0.6f, 0.2f, 150, default, 0.8f);
                dust.noGravity = true;
                dust.velocity *= 0.4f;
            }

            Lighting.AddLight(Projectile.Center, GlintWarn.R / 255f * 0.08f,
                GlintWarn.G / 255f * 0.08f, GlintWarn.B / 255f * 0.08f);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float strength;
            if (InStrike) {
                //突进余痕：可见窗与突进窗同一实体
                strength = MathHelper.Clamp(1f - (elapsed - TelegraphFrames) / (float)StrikeFrames, 0f, 1f) * 0.22f;
            }
            else {
                strength = MathHelper.Clamp(elapsed / 8f, 0f, 1f) * (Locked ? 1f : 0.45f);
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            float scaleX = LaneLength / tex.Width;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);

            if (!Locked || InStrike) {
                Main.EntitySpriteDraw(tex, drawPos, null, GlintWarn * (0.4f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneCoreWidth / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, GlintWarn * (0.22f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneGlowWidth / tex.Height), SpriteEffects.None, 0);
            }
            else {
                //锁向期：刀光一闪——白热窄线急促明灭，宣告突进已承诺
                float lockT = MathHelper.Clamp((elapsed - (TelegraphFrames - LockFrames)) / (float)LockFrames, 0f, 1f);
                float flash = 0.6f + 0.4f * MathF.Sin(lockT * MathHelper.Pi * 6f);
                Main.EntitySpriteDraw(tex, drawPos, null, GlintWarn * (0.55f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneGlowWidth + 8f) / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, GlintCore * (0.9f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneCoreWidth - 3f) / tex.Height), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
