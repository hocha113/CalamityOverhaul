using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Meteorite.Projectiles
{
    /// <summary>
    /// 火斑：熔滴落地留下的小型燃烧区。ai[0]=档位。
    /// 生命周期：引燃期（≥30 帧无害，可见即倒计时）→ 燃烧期（~90 帧，判定窗=火光可见窗）→ 熄灭收场。
    /// 生成位置即锁定（地面静物，预告即承诺）；hostile 由各端从同步 timeLeft 确定性推得，不碰同步伤害值
    /// </summary>
    internal class MeteoriteFireSpot : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>火斑最小间距（像素）：陨石头布点循环与熔滴落点复查共读此常量，逃生步隙由它保证</summary>
        internal const float EmberGapPx = 120f;
        /// <summary>火斑全局并发上限（M7 并发闸，布点端读取）</summary>
        internal const int FireSpotCap = 6;
        /// <summary>判定高度（布点端用于贴地对位）</summary>
        internal const float SpotHeight = 18f;

        /// <summary>引燃预告帧（公平契约 ≥30，档位不缩短）</summary>
        private const int KindleFrames = 30;
        /// <summary>燃烧存续帧</summary>
        private const int LitFrames = 90;
        private const int FadeFrames = 12;
        private const int TotalLife = KindleFrames + LitFrames + FadeFrames;

        private static readonly Color EmberWarm = new Color(255, 168, 72);
        private static readonly Color EmberDeep = new Color(110, 40, 18);

        private int Tier => Math.Clamp((int)Projectile.ai[0], 1, 3);
        private int Elapsed => TotalLife - Projectile.timeLeft;
        private bool Lit => Elapsed >= KindleFrames && Elapsed < KindleFrames + LitFrames;

        /// <summary>燃势 0~1（引燃渐起、熄灭渐落），绘制/灯光/判定同源</summary>
        private float Blaze {
            get {
                int elapsed = Elapsed;
                if (elapsed < KindleFrames) {
                    return 0.25f * (elapsed / (float)KindleFrames);
                }
                if (elapsed < KindleFrames + LitFrames) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - (elapsed - KindleFrames - LitFrames) / (float)FadeFrames, 0f, 1f);
            }
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = (int)SpotHeight;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //判定窗=火光可见窗（各端从同步 timeLeft 确定性推得同一结论）
            Projectile.hostile = Lit;

            if (Elapsed == KindleFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 4 }, Projectile.Center);
            }

            if (Main.dedServ) {
                return;
            }
            if (Elapsed < KindleFrames) {
                //引燃期：细烟与零星火星（≤2 粒/帧）
                if (Main.rand.NextBool(3)) {
                    Dust smoke = Dust.NewDustPerfect(Projectile.Top + new Vector2(Main.rand.NextFloat(-8f, 8f), 2f),
                        DustID.Smoke, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.1f)), 150, default, 0.8f);
                    smoke.noGravity = true;
                }
            }
            else if (Lit && Main.rand.NextBool(2)) {
                //燃烧期：稳定火舌（≤2 粒/帧）
                Dust flame = Dust.NewDustPerfect(Projectile.Top + new Vector2(Main.rand.NextFloat(-10f, 10f), 4f),
                    DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(0.8f, 2f)), 90, default,
                    Main.rand.NextFloat(1f, 1.4f));
                flame.noGravity = true;
            }

            float blaze = Blaze;
            if (blaze > 0.05f) {
                Lighting.AddLight(Projectile.Center, EmberWarm.ToVector3() * 0.45f * blaze);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //命中方本机结算，减益原生同步；时长随档位
            target.AddBuff(BuffID.OnFire, 120 + 60 * (Tier - 1));
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust ember = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.3f, 1.2f)), 110, default, 1f);
                ember.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float blaze = Blaze;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Texture2D body = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity * 0.7f);

            //暗色熔渣底（真 alpha 才能压亮背景，火斑的实体轮廓）
            Vector2 baseScale = new Vector2(Projectile.width / (float)body.Width * 1.5f,
                Projectile.height / (float)body.Height * 1.1f);
            Main.EntitySpriteDraw(body, drawPos + new Vector2(0f, 4f), null,
                EmberDeep * (0.45f + 0.45f * blaze), 0f, body.Size() / 2f, baseScale, SpriteEffects.None, 0);

            //焰芯（加色，火光高度随燃势起伏）
            if (blaze > 0.03f) {
                Color flame = (EmberWarm with { A = 0 }) * (0.6f * blaze * pulse);
                Main.EntitySpriteDraw(body, drawPos + new Vector2(0f, -4f - 4f * blaze), null, flame, 0f,
                    body.Size() / 2f, baseScale * new Vector2(0.7f, 0.9f + 0.4f * blaze * pulse), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, drawPos + new Vector2(0f, -6f * blaze), null,
                    (EmberWarm with { A = 0 }) * (0.4f * blaze * pulse), 0f,
                    glow.Size() / 2f, new Vector2(0.34f, 0.3f) * (0.7f + 0.5f * blaze), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
