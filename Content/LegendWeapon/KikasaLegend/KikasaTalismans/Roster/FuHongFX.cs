using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 虹桥：墨瀑收势后拱在落点上空的七彩光拱，虹符（<see cref="FuHong"/>）专属。
    /// 仅所有者端生成（伤害自然同步）；虹下增益由各端本地给自家玩家挂 buff——
    /// 召唤伤在受益者自己的客户端结算，本地挂 buff 正是权威端。
    /// 拱身七色渐变条带为程序化像素段拼装（复用字形 DrawSeg 技法），
    /// 光是材质本体：条带+沿弧微光呼吸+两端光雨，无新 .fx
    /// </summary>
    internal class FuHongRainbowBridge : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>驻场帧数（5 秒）</summary>
        internal const int LifeFrames = 300;

        /// <summary>拱半跨与拱高（像素）</summary>
        private const float HalfSpanPx = 230f;
        private const float ArchRisePx = 120f;

        /// <summary>七色带间距（沿拱法线）</summary>
        private const float BandGapPx = 3.4f;

        /// <summary>拱身折线段数</summary>
        private const int SegCount = 26;

        /// <summary>虹下增益纵深：拱脚基线以下多远仍算"虹下"</summary>
        private const float BlessDepthPx = 280f;

        //七色渐淡的水墨虹色板：外红内紫
        private static readonly Color[] BandColors = [
            new(232, 84, 84),
            new(238, 146, 72),
            new(240, 208, 92),
            new(120, 206, 120),
            new(96, 196, 208),
            new(96, 136, 222),
            new(168, 110, 222),
        ];

        private float life;

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>淡入淡出包络：起 18 帧成形，末 30 帧散去</summary>
        private float Envelope => MathF.Min(MathHelper.Clamp(life / 18f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f));

        /// <summary>自拱心向两脚的展开进度（虹从中间亮起）</summary>
        private float SpanReveal => MathHelper.Clamp(life / 22f, 0f, 1f);

        /// <summary>拱上取点：t∈[-1,1]，0=拱心，±1=两脚（基线）</summary>
        private Vector2 ArchPoint(float t)
            => Projectile.Center + new Vector2(t * HalfSpanPx, -ArchRisePx * (1f - t * t));

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //穿虹判定 0.5s 一轮
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.netImportant = true;
        }

        public override void AI() {
            life++;
            Projectile.velocity = Vector2.Zero;

            //虹下眷护：各端只管自家本地玩家——移速/召唤伤都在受益者本机结算
            if (!Main.dedServ) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead && UnderArch(lp) && Envelope > 0.4f) {
                    lp.AddBuff(ModContent.BuffType<FuHongBlessBuff>(), 5);
                }
            }

            if (Main.dedServ) {
                return;
            }
            //两端光雨：拱脚洒下细碎光珠
            if (Envelope > 0.5f && Main.rand.NextBool(3)) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 foot = ArchPoint(side * Main.rand.NextFloat(0.82f, 1f));
                Color c = BandColors[Main.rand.Next(BandColors.Length)];
                PRTLoader.NewParticle<PRT_Light>(foot + Main.rand.NextVector2Circular(8f, 6f),
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(0.7f, 1.4f)),
                    c * 0.6f, Main.rand.NextFloat(0.14f, 0.24f))?.Configure(Main.rand.Next(20, 34), 0.7f);
            }
            //沿拱微闪：光在弧上呼吸
            if (Envelope > 0.5f && Main.rand.NextBool(7)) {
                float t = Main.rand.NextFloat(-1f, 1f) * SpanReveal;
                PRTLoader.NewParticle<PRT_Light>(ArchPoint(t), -Vector2.UnitY * 0.2f,
                    Color.Lerp(BandColors[Main.rand.Next(BandColors.Length)], Color.White, 0.4f) * 0.5f,
                    Main.rand.NextFloat(0.1f, 0.18f))?.Configure(Main.rand.Next(14, 22), 0.6f);
            }
            Vector2 apex = ArchPoint(0f);
            Lighting.AddLight(apex, 0.16f * Envelope, 0.12f * Envelope, 0.18f * Envelope);
        }

        /// <summary>虹下判定：横向在跨内、纵向在拱曲线之下且不超出纵深</summary>
        private bool UnderArch(Player player) {
            float dx = player.Center.X - Projectile.Center.X;
            if (MathF.Abs(dx) > HalfSpanPx) {
                return false;
            }
            float t = dx / HalfSpanPx;
            float archY = Projectile.Center.Y - ArchRisePx * (1f - t * t);
            return player.Center.Y >= archY && player.Center.Y <= Projectile.Center.Y + BlessDepthPx;
        }

        /// <summary>虹身判定：沿拱折线逐段线碰撞，与可见弧同源</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Envelope < 0.4f) {
                return false;
            }
            float _ = 0f;
            float reveal = SpanReveal;
            for (int i = 0; i < SegCount; i++) {
                float t0 = MathHelper.Lerp(-reveal, reveal, i / (float)SegCount);
                float t1 = MathHelper.Lerp(-reveal, reveal, (i + 1) / (float)SegCount);
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    ArchPoint(t0), ArchPoint(t1), 24f, ref _)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //棱光迸散：命中钩只在所有者端跑，小额点缀不外播
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Light>(target.Center + Main.rand.NextVector2Circular(12f, 12f),
                    Main.rand.NextVector2Circular(1.8f, 1.8f),
                    BandColors[Main.rand.Next(BandColors.Length)] * 0.7f,
                    Main.rand.NextFloat(0.14f, 0.22f))?.Configure(Main.rand.Next(12, 20), 0.7f);
            }
        }

        //====绘制：七色条带沿拱逐段拼装====

        public override bool PreDraw(ref Color lightColor) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            float env = Envelope;
            if (pixel == null || env <= 0.02f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Rectangle src = new(0, 0, 1, 1);
            float time = Main.GlobalTimeWrappedHourly;
            float reveal = SpanReveal;

            //底层软光：低占比垫在弧下，让条带有"发光体"的身
            if (glow != null) {
                for (int i = 0; i <= 4; i++) {
                    float t = MathHelper.Lerp(-reveal, reveal, i / 4f);
                    Vector2 pos = ArchPoint(t) - Main.screenPosition;
                    Color under = Color.Lerp(BandColors[1], BandColors[5], (t + 1f) * 0.5f) with { A = 0 };
                    sb.Draw(glow, pos, null, under * (0.10f * env), 0f, glow.Size() * 0.5f,
                        1.2f, SpriteEffects.None, 0f);
                }
            }

            //七色条带：逐段取拱线，沿法线摊开七道色带（A=0 加色，光的正当载体）
            for (int i = 0; i < SegCount; i++) {
                float t0 = MathHelper.Lerp(-reveal, reveal, i / (float)SegCount);
                float t1 = MathHelper.Lerp(-reveal, reveal, (i + 1) / (float)SegCount);
                Vector2 a = ArchPoint(t0);
                Vector2 b = ArchPoint(t1);
                Vector2 along = b - a;
                float len = along.Length();
                if (len < 0.5f) {
                    continue;
                }
                float rot = along.ToRotation();
                //法线朝拱外侧（上方）：红在外紫在内
                Vector2 normal = along.SafeNormalize(Vector2.UnitX).RotatedBy(-MathHelper.PiOver2);
                float tm = (t0 + t1) * 0.5f;
                //微光呼吸沿弧滚动，端部渐散
                float shimmer = 0.72f + 0.28f * MathF.Sin(time * 2.2f + tm * 5f + Seed);
                float endFade = 1f - MathF.Pow(MathF.Abs(tm), 3f) * 0.55f;
                for (int k = 0; k < BandColors.Length; k++) {
                    Vector2 off = normal * (3f - k) * BandGapPx;
                    Color c = BandColors[k] with { A = 0 };
                    sb.Draw(pixel, a + off - Main.screenPosition, src,
                        c * (0.15f * env * shimmer * endFade), rot,
                        new Vector2(0f, 0.5f), new Vector2(len + 0.7f, 2.6f), SpriteEffects.None, 0f);
                }
            }

            //两脚落光：拱脚一小团柔光接住条带的断口
            if (glow != null) {
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 foot = ArchPoint(s * reveal) - Main.screenPosition;
                    sb.Draw(glow, foot, null, (Color.White with { A = 0 }) * (0.14f * env), 0f,
                        glow.Size() * 0.5f, 0.7f, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }

    /// <summary>虹下眷护：移速 +10%、召唤伤 +10%；由虹桥逐帧续 5 帧，走出虹下自然过期</summary>
    internal sealed class FuHongBlessBuff : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "虹桥眷护");

        public override LocalizedText Description
            => this.GetLocalization(nameof(Description), () => "虹下移速 +10%，召唤伤害 +10%");

        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.moveSpeed += 0.10f;
            player.GetDamage<SummonDamageClass>() += 0.10f;
        }
    }
}
