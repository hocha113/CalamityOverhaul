using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Legion.Projectiles
{
    /// <summary>
    /// 水面破浪预告：血鳗跃击的水面警戒环 / 血鲨跃咬的泡沫痕。锚定在锁死的破浪点
    /// （位置即承诺，生成后不追踪），倒数结束由 NPC 相位机执行跃出，本体只作可见窗。
    /// ai[0]=来源打包（whoAmI+1 | type&lt;&lt;8，施法者死亡或槽位复用即取消）
    /// ai[1]=模式（0 血鳗环 / 1 血鲨泡沫） ai[2]=锁定横向 ±1（跃弧方向指示）。永不造成伤害
    /// </summary>
    internal class LegionSurfOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int ModeEelRing = 0;
        internal const int ModeSharkFoam = 1;

        /// <summary>预告帧数（任务底线 ≥34，档位一律不缩短）</summary>
        internal const int TelegraphFrames = 34;
        /// <summary>预告完成后的消散帧</summary>
        private const int FadeFrames = 8;
        /// <summary>血鳗警戒环横半径（跃出点落在环内，环宽吸收预告期微小漂移）</summary>
        internal const float RingRadiusX = 48f;
        /// <summary>警戒环纵半径（水面侧视的扁椭圆）</summary>
        private const float RingRadiusY = 14f;
        /// <summary>血鲨泡沫痕半长</summary>
        private const float FoamHalfLength = 60f;

        private int Mode => (int)Projectile.ai[1];
        private float DirSign => Projectile.ai[2];
        private int Elapsed => TelegraphFrames + FadeFrames - Projectile.timeLeft;
        private float Charge => MathHelper.Clamp(Elapsed / (float)TelegraphFrames, 0f, 1f);

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>纯预告体，永不参与伤害（危险窗=鳗/鲨实际出水的接触段）</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            //来源校验：施法者死亡则取消（击杀施法者=有效反制）；类型比对防槽位复用
            if (!Cancelled && Elapsed < TelegraphFrames) {
                int packed = (int)Projectile.ai[0];
                int src = (packed & 255) - 1;
                if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                    || Main.npc[src].type != packed >> 8) {
                    Cancelled = true;
                }
            }

            if (Main.dedServ) {
                return;
            }
            Lighting.AddLight(Projectile.Center, 0.22f * Charge, 0.04f, 0.05f);

            //破浪瞬间的水花与出水声（各端本地，锚在实体相位上；跃击本体由 NPC 侧执行）
            if (Elapsed == TelegraphFrames && !Cancelled) {
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.7f, MaxInstances = 4 }, Projectile.Center);
                for (int i = 0; i < 10; i++) {
                    Dust splash = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-RingRadiusX, RingRadiusX) * 0.6f, 0f),
                        DustID.Water, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(2f, 5f)),
                        60, default, Main.rand.NextFloat(1f, 1.6f));
                    splash.noGravity = false;
                }
                return;
            }
            if (Cancelled || Elapsed >= TelegraphFrames || Main.rand.NextBool(3)) {
                return;
            }
            //预告期涌泡（≤2 粒/帧）：血鳗绕环冒泡，血鲨沿痕推沫
            if (Mode == ModeEelRing) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(ang) * RingRadiusX, MathF.Sin(ang) * RingRadiusY);
                Dust bubble = Dust.NewDustPerfect(pos, DustID.Water,
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.4f + Charge)), 80, default, 1.1f);
                bubble.noGravity = true;
                if (Main.rand.NextBool(4)) {
                    Dust blood = Dust.NewDustPerfect(pos, DustID.Blood, new Vector2(0f, -0.6f), 120, default, 0.9f);
                    blood.noGravity = true;
                }
            }
            else {
                float off = Main.rand.NextFloat(-FoamHalfLength, FoamHalfLength);
                Dust foam = Dust.NewDustPerfect(Projectile.Center + new Vector2(off, Main.rand.NextFloat(-3f, 3f)),
                    DustID.Water, new Vector2(DirSign * (0.8f + Charge), -Main.rand.NextFloat(0.3f, 1f)),
                    70, default, 1f + 0.5f * Charge);
                foam.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade;
            if (Cancelled) {
                fade = 0.35f * MathHelper.Clamp(1f - Elapsed / (float)TelegraphFrames, 0f, 1f);
            }
            else if (Elapsed >= TelegraphFrames) {
                fade = MathHelper.Clamp(1f - (Elapsed - TelegraphFrames) / (float)FadeFrames, 0f, 1f);
            }
            else {
                fade = MathHelper.Clamp(Elapsed / 8f, 0f, 1f);
            }
            if (fade <= 0.01f) {
                return false;
            }
            float charge = Charge;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);
            //末段增亮：即将破浪
            float urgency = charge > 0.7f ? 1.3f : 1f;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 orig = glow.Size() / 2f;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Color warn = new Color(255, 70, 60, 0) * (fade * pulse * urgency);

            if (Mode == ModeEelRing) {
                //扁椭圆警戒环：黑底 SoftGlow 点阵加色排布，环随蓄力缓旋
                const int dots = 14;
                float spin = Main.GlobalTimeWrappedHourly * 1.6f;
                for (int i = 0; i < dots; i++) {
                    float ang = MathHelper.TwoPi * i / dots + spin;
                    Vector2 pos = center + new Vector2(MathF.Cos(ang) * RingRadiusX, MathF.Sin(ang) * RingRadiusY);
                    Main.EntitySpriteDraw(glow, pos, null, warn * 0.55f, 0f, orig,
                        0.028f + 0.012f * charge, SpriteEffects.None, 0);
                }
                //环心水下红晕 + 跃弧方向指示（沿锁定横向的偏置亮点）
                Main.EntitySpriteDraw(glow, center, null, warn * 0.30f, 0f, orig,
                    new Vector2(0.24f, 0.08f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, center + new Vector2(DirSign * (RingRadiusX + 16f) * charge, -6f),
                    null, warn * 0.45f, 0f, orig, 0.05f, SpriteEffects.None, 0);
            }
            else {
                //泡沫痕：水面横向亮痕，随蓄力拉长增亮，端点偏向跃咬方向
                Color foam = new Color(220, 240, 255, 0) * (fade * 0.5f * pulse);
                Main.EntitySpriteDraw(glow, center, null, foam, 0f, orig,
                    new Vector2(0.10f + 0.16f * charge, 0.035f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, center + new Vector2(DirSign * FoamHalfLength * charge, 0f),
                    null, warn * 0.5f, 0f, orig, 0.045f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
