using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.PumpkinMoon.Projectiles
{
    /// <summary>
    /// 火种祭圈灯魂：ai[0]=圈心X ai[1]=圈心Y ai[2]=档位×100+槽位。
    /// 圈心在施法瞬间锁定于世界坐标（预告即承诺，不追玩家）；全体灯魂共享由圈心坐标
    /// 确定性推得的基准相位并匀速公转（各端零同步一致）。缺口=RingGapSlots 个连续槽位
    /// 从不生成（物理缺口），随公转成为可学习的旋转安全扇区；圈外恒安全。
    /// 判定窗=点燃可见窗（引燃期灵蓝无害，点燃转橙才有判定）
    /// </summary>
    internal class PmkRitualWispProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlamingJack;

        internal const int RingSlots = 10;
        /// <summary>具名缺口：生成循环跳过的连续槽位数（108° 安全扇区）</summary>
        internal const int RingGapSlots = 3;
        /// <summary>圈半径（圈外恒安全的具名边界）</summary>
        private const float RingRadius = 176f;
        /// <summary>公转角速度（弧度/帧）</summary>
        private const float RingSpin = 0.021f;
        /// <summary>引燃预告帧（≥30 契约）</summary>
        private const int TelegraphFrames = 42;
        /// <summary>点燃存续帧（档位只延长存续）</summary>
        private static readonly int[] LitByTier = [168, 204, 240];
        private const int FadeFrames = 20;

        private static readonly Color WispGhost = new Color(150, 196, 255);
        private static readonly Color WispFlame = new Color(255, 168, 56);

        private Vector2 RingCenter => new Vector2(Projectile.ai[0], Projectile.ai[1]);
        private int Tier => (int)MathHelper.Clamp((int)Projectile.ai[2] / 100, 1, 3);
        private int Slot => (int)Projectile.ai[2] % 100;
        private int LitFrames => LitByTier[Tier - 1];
        private int TotalLife => TelegraphFrames + LitFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;
        private bool Lit => Elapsed >= TelegraphFrames && Elapsed < TelegraphFrames + LitFrames;

        /// <summary>该档位一环的完整时长（NPC 侧 busy 计时用）</summary>
        internal static int TotalFrames(int tier) => TelegraphFrames + LitByTier[(int)MathHelper.Clamp(tier, 1, 3) - 1] + FadeFrames;

        /// <summary>基准相位由圈心坐标确定性推得（ai 已同步，各端一致）</summary>
        private float BaseAngle => (Projectile.ai[0] + Projectile.ai[1]) * 0.0037f % MathHelper.TwoPi;

        /// <summary>当前公转角</summary>
        private float OrbitAngle(int backFrames = 0)
            => BaseAngle + Slot * (MathHelper.TwoPi / RingSlots) + RingSpin * (Elapsed - backFrames);

        /// <summary>就位半径：引燃期自内向外撑开</summary>
        private float OrbitRadius {
            get {
                float p = MathHelper.Clamp(Elapsed / (float)TelegraphFrames, 0f, 1f);
                return RingRadius * (0.55f + 0.45f * (1f - (1f - p) * (1f - p) * (1f - p)));
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
            }

            //几何全部由同步量确定性推得
            Projectile.Center = RingCenter + OrbitAngle().ToRotationVector2() * OrbitRadius;
            //判定窗=点燃可见窗
            Projectile.hostile = Lit;

            //首个实槽位担任报幕员，避免七魂齐鸣
            if (Slot == RingGapSlots && !Main.dedServ) {
                if (Elapsed == 1) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.5f }, RingCenter);
                }
                else if (Elapsed == TelegraphFrames) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.55f, Pitch = -0.2f, MaxInstances = 3 }, RingCenter);
                }
            }

            if (!Main.dedServ) {
                float litness = Lit ? 1f : 0.35f;
                if (Main.rand.NextBool(Lit ? 3 : 6)) {
                    int dustType = Lit ? DustID.Torch : DustID.BlueTorch;
                    Dust flame = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                        dustType, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.4f)), 120, default, 0.9f);
                    flame.noGravity = true;
                }
                Color glow = Lit ? WispFlame : WispGhost;
                Lighting.AddLight(Projectile.Center, glow.ToVector3() * 0.35f * litness);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire, 90);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            //可见度：引燃渐显、熄灭渐隐（与判定窗同一时间轴）
            float vis;
            if (elapsed < TelegraphFrames) {
                vis = 0.35f + 0.5f * (elapsed / (float)TelegraphFrames);
            }
            else if (Lit) {
                vis = 1f;
            }
            else {
                vis = MathHelper.Clamp(1f - (elapsed - TelegraphFrames - LitFrames) / (float)FadeFrames, 0f, 1f);
            }
            if (vis <= 0.01f) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            int frameCount = Math.Max(1, Main.projFrames[ProjectileID.FlamingJack]);
            Rectangle frame = tex.Frame(1, frameCount, 0, elapsed / 5 % frameCount);
            Vector2 origin = frame.Size() / 2f;
            float litT = Lit ? 1f : elapsed < TelegraphFrames ? elapsed / (float)TelegraphFrames * 0.4f : 0.6f;
            //引燃期灵蓝、点燃转暖：状态变化可读
            Color tint = Color.Lerp(WispGhost, Color.White, litT);

            //公转拖尾：同贴图后置相位重画（旋转拖影，横轴粗细=本体量级）
            for (int k = 2; k >= 1; k--) {
                float ghostAngle = OrbitAngle(k * 4);
                Vector2 ghostPos = RingCenter + ghostAngle.ToRotationVector2() * OrbitRadius - Main.screenPosition;
                float t = 1f - k * 0.3f;
                Main.EntitySpriteDraw(tex, ghostPos, frame, tint * (0.35f * t * vis), Projectile.rotation,
                    origin, 0.92f - 0.08f * k, SpriteEffects.None, 0);
            }

            //本体（原版灯笼贴图，实体层）
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float wobble = MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + Slot * 1.7f) * 0.12f;
            Main.EntitySpriteDraw(tex, drawPos, frame, tint * vis, wobble, origin, 1f, SpriteEffects.None, 0);

            //辉光敷料
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color halo = ((Lit ? WispFlame : WispGhost) with { A = 0 }) * (0.5f * vis);
            Main.EntitySpriteDraw(glow, drawPos, null, halo, 0f, glow.Size() / 2f, 0.62f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust ember = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(1.2f, 1.2f) - Vector2.UnitY * 0.6f, 120, default, 0.9f);
                ember.noGravity = true;
            }
        }
    }
}
