using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Martians.Projectiles
{
    /// <summary>
    /// 灰皮步兵呼叫增援·电台信号：ai[0]=步兵索引，ai[1]=步兵类型（索引+类型双校验）。
    /// 悬在步兵头顶 <see cref="SignalFrames"/>（≥40）帧作可见前摇（天线火花+闪烁灯点），
    /// 期满由 NPC 侧权威端复验本实体后生成增援；步兵中途倒下则实体消散，增援不会发生
    /// （击杀呼叫者是有效反制）。本实体永不伤害，纯预告
    /// </summary>
    internal class MrtReinforceSignalProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>电台前摇帧（任务口径 ≥40，各档位一律不缩短）</summary>
        internal const int SignalFrames = 45;
        private const int FadeFrames = 8;

        private static readonly Color SignalCyan = new(120, 230, 255);

        private int AnchorIndex => (int)Projectile.ai[0];
        private int AnchorType => (int)Projectile.ai[1];
        private int Elapsed => SignalFrames + FadeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = SignalFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //步兵索引+类型双校验：呼叫者倒下（或槽位复用）→ 信号消散，增援不会发生
            NPC anchor = AnchorIndex >= 0 && AnchorIndex < Main.maxNPCs ? Main.npc[AnchorIndex] : null;
            if (anchor == null || !anchor.active || anchor.type != AnchorType) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Top + new Vector2(anchor.direction * 5f, -14f);

            if (VaultUtils.isServer) {
                return;
            }
            int elapsed = Elapsed;
            //电台滴答：低频短音，节拍随进度加快
            if (elapsed < SignalFrames && elapsed % (elapsed < SignalFrames / 2 ? 15 : 9) == 0) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.3f, Pitch = 0.65f, MaxInstances = 4 }, Projectile.Center);
            }
            //天线火花（≤2 粒/帧）
            if (elapsed < SignalFrames) {
                if (Main.rand.NextBool(2)) {
                    Dust spark = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-3f, 3f), -6f),
                        DustID.Electric, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.8f)), 100, default, 0.55f);
                    spark.noGravity = true;
                }
                if (Main.rand.NextBool(5)) {
                    Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.MartianSaucerSpark,
                        Main.rand.NextVector2Circular(1.2f, 1.2f) - Vector2.UnitY, 0, default, 0.7f);
                    spark.noGravity = true;
                }
            }
            Lighting.AddLight(Projectile.Center, SignalCyan.ToVector3() * 0.2f);
        }

        public override void OnKill(int timeLeft) {
            //自然走满（=增援落地帧附近）才放到场爆闪；中途被打断只安静消散
            if (VaultUtils.isServer || Elapsed < SignalFrames) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.MartianSaucerSpark,
                    Main.rand.NextVector2Circular(3f, 3f), 0, default, Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = Projectile.timeLeft <= FadeFrames
                ? Projectile.timeLeft / (float)FadeFrames
                : MathHelper.Clamp(Elapsed / 6f, 0f, 1f);
            if (fade <= 0.02f) {
                return false;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float urgency = MathHelper.Clamp(Elapsed / (float)SignalFrames, 0f, 1f);
            //灯点闪烁随进度加急（视觉即计时）
            float blink = 0.55f + 0.45f * MathF.Sin(Elapsed * (0.25f + 0.35f * urgency));
            Color tint = SignalCyan with { A = 0 };

            //天线杆：柔光竖条
            Main.EntitySpriteDraw(glow, drawPos + new Vector2(0f, 6f), null, tint * (0.4f * fade), 0f,
                glow.Size() / 2f, new Vector2(0.06f, 0.42f), SpriteEffects.None, 0);
            //顶端信号灯：加色亮点 + 白芯
            Main.EntitySpriteDraw(glow, drawPos + new Vector2(0f, -8f), null, tint * (0.9f * fade * blink), 0f,
                glow.Size() / 2f, 0.16f + 0.1f * urgency, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos + new Vector2(0f, -8f), null,
                new Color(255, 255, 255, 0) * (0.5f * fade * blink), 0f,
                glow.Size() / 2f, 0.07f, SpriteEffects.None, 0);
            return false;
        }
    }
}
