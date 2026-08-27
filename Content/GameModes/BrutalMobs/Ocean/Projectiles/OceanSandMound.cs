using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ocean.Projectiles
{
    /// <summary>
    /// 螃蟹掘沙伏击的沙堆标记（预告实体+镜像盖戳）。
    /// ai[0]=来源打包（whoAmI+1 | type&lt;&lt;8，宿主死亡/槽位复用即消散并归还透明度）
    /// ai[1]=状态（0 半埋潜伏 / 1 破土前摇；权威端写入并 netUpdate）。
    /// 半埋期由本实体在所有端把宿主 alpha 抬到 <see cref="BuriedAlpha"/>（只抬不压、只收自己抬的区间，
    /// 不与出生淡入等原版自管值打架）；破土前摇 30 帧沙尘鼓包（≥30 契约，档位不缩短），
    /// 前摇期快速归还 alpha=现身。本体永不造成伤害
    /// </summary>
    internal class OceanSandMound : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int StateBuried = 0;
        internal const int StateBurst = 1;

        /// <summary>半埋透明度上限（本层只在 [0,150] 区间内经营，不碰原版更高的自管值）</summary>
        internal const int BuriedAlpha = 150;
        /// <summary>沙堆成形帧</summary>
        private const int FormFrames = 14;
        /// <summary>消散帧</summary>
        private const int FadeFrames = 12;
        /// <summary>沙堆半宽（像素，绘制用）</summary>
        private const float MoundHalfWidth = 26f;
        /// <summary>Extra_98 可见幅（像素@scale1，量测值）</summary>
        private const float MaskContentPx = 47f;

        private int State => (int)Projectile.ai[1];
        private ref float Age => ref Projectile.localAI[0];
        /// <summary>消散计时（宿主消失或被提前收场后启动）</summary>
        private ref float FadeOut => ref Projectile.localAI[2];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 16;
            Projectile.hostile = false;//纯标记，恒无伤害
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        private bool TryHost(out NPC host) {
            host = null;
            int packed = (int)Projectile.ai[0];
            int src = (packed & 255) - 1;
            if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                || Main.npc[src].type != packed >> 8) {
                return false;
            }
            host = Main.npc[src];
            return true;
        }

        /// <summary>归还本层抬上去的透明度区间（只收 [0,BuriedAlpha]，每帧一步）</summary>
        private static void ReturnAlpha(NPC host, int step) {
            if (host.alpha > 0 && host.alpha <= BuriedAlpha) {
                host.alpha = Math.Max(0, host.alpha - step);
            }
        }

        public override void AI() {
            Age++;
            bool hostValid = TryHost(out NPC host);

            if (!hostValid || FadeOut > 0f) {
                //宿主消失或收场：消散并逐帧归还透明度
                FadeOut++;
                if (hostValid) {
                    ReturnAlpha(host, 24);
                }
                if (FadeOut >= FadeFrames) {
                    Projectile.Kill();
                }
                return;
            }

            //贴住宿主脚底（各端从同步的 NPC 位置确定性推得）
            Projectile.Center = host.Bottom + new Vector2(0f, -6f);
            Projectile.timeLeft = 90;//宿主在则常驻，由 NPC 相位机负责收场

            if (State == StateBuried) {
                //半埋盖戳：alpha 只抬不压，渐进到上限（所有端一致执行，伏击可见性由实体保证）
                if (host.alpha < BuriedAlpha) {
                    host.alpha = Math.Min(BuriedAlpha, host.alpha + 10);
                }
                //潜伏微沙（≤1 粒/3帧）
                if (!Main.dedServ && Main.rand.NextBool(3)) {
                    Dust sand = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-MoundHalfWidth, MoundHalfWidth) * 0.8f, 2f),
                        DustID.Sand, new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.7f)), 150, default, 0.8f);
                    sand.noGravity = true;
                }
            }
            else {
                //破土前摇：快速现身 + 沙尘鼓包（各端读同步的 ai[1] 状态位驱动）
                ReturnAlpha(host, 14);
                if (Projectile.localAI[1] != 1f) {
                    Projectile.localAI[1] = 1f;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = -0.2f, MaxInstances = 4 },
                            Projectile.Center);
                    }
                }
                if (!Main.dedServ) {
                    for (int i = 0; i < 2; i++) {
                        Dust burst = Dust.NewDustPerfect(
                            Projectile.Center + new Vector2(Main.rand.NextFloat(-MoundHalfWidth, MoundHalfWidth), 0f),
                            DustID.Sand, new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1.5f, 3.5f)),
                            90, default, Main.rand.NextFloat(1f, 1.5f));
                        burst.noGravity = false;
                    }
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //兜底归还：无论何种路径死亡，都把本层可能抬着的透明度一次收回（防隐身残留）
            if (TryHost(out NPC host) && host.alpha <= BuriedAlpha) {
                host.alpha = 0;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float form = MathHelper.Clamp(Age / FormFrames, 0f, 1f);
            float alpha = form * MathHelper.Clamp(1f - FadeOut / FadeFrames, 0f, 1f);
            if (alpha <= 0.01f) {
                return false;
            }

            Texture2D dome = CWRAsset.Extra_98.Value;
            Vector2 orig = dome.Size() / 2f;
            bool burst = State == StateBurst && FadeOut <= 0f;
            //破土前摇的鼓包颤动（读作要出事了）
            float jitterX = burst ? MathF.Sin(Main.GlobalTimeWrappedHourly * 55f + Projectile.identity) * 2.2f : 0f;
            float swell = burst ? 1.12f + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 30f) : 1f;
            Vector2 center = Projectile.Center + new Vector2(jitterX, 0f) - Main.screenPosition;

            //沙丘穹体：真 alpha 双层（暗基座+亮沙面），宽度盖住宿主
            Color baseSand = new Color(140, 112, 66) * (0.55f * alpha);
            Main.EntitySpriteDraw(dome, center + new Vector2(0f, 2f), null, baseSand, 0f, orig,
                new Vector2(MoundHalfWidth * 2.3f / MaskContentPx, 0.42f) * swell, SpriteEffects.None, 0);
            Color topSand = Color.Lerp(lightColor, new Color(214, 186, 124), 0.65f) * (0.85f * alpha);
            Main.EntitySpriteDraw(dome, center, null, topSand, 0f, orig,
                new Vector2(MoundHalfWidth * 2f / MaskContentPx, 0.36f) * swell, SpriteEffects.None, 0);

            //碎沙粒（确定性排布的小凸起，强化"这里有个包"）
            for (int i = 0; i < 3; i++) {
                float seed = Projectile.identity * 2.1f + i * 2.7f;
                Vector2 pos = center + new Vector2(MathF.Sin(seed) * MoundHalfWidth * 0.6f, -3f - (i % 2) * 3f);
                Main.EntitySpriteDraw(dome, pos, null, topSand * 0.8f, seed * 0.3f, orig,
                    new Vector2(0.10f, 0.06f), SpriteEffects.None, 0);
            }

            //破土警示微光（加色敷料，不承担形体）
            if (burst) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 26f + Projectile.identity);
                Main.EntitySpriteDraw(glow, center, null, new Color(255, 190, 90, 0) * (0.30f * alpha * pulse),
                    0f, glow.Size() / 2f, new Vector2(0.22f, 0.08f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
