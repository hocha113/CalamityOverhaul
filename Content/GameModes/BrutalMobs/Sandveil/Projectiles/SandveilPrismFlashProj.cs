using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Sandveil.Projectiles
{
    /// <summary>
    /// 神圣沙鲨跃出帧释放的棱光小闪。方向由生成端一次性给定的固定对角 4 向
    /// （非追踪保证：本体从不读取玩家位置，只按初速直线缓减飞行）。
    /// 出膛淡入期无判定（公平阀，伤害窗=可见窗）。
    /// //豁免声明：棱光属光，弹体允许纯加色（A=0）绘制，无遮挡像素层（镜像闪电豁免口径）
    /// </summary>
    internal class SandveilPrismFlashProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>淡入帧数，判定开启与可见同门</summary>
        private const int FadeInFrames = 6;
        /// <summary>每帧缓减系数（棱光渐散）</summary>
        private const float Drag = 0.985f;

        /// <summary>棱光双色：粉白芯与淡虹缘（均 A=0 加色）</summary>
        private static readonly Color PrismCore = new(255, 244, 252, 0);
        private static readonly Color PrismEdge = new(255, 176, 232, 0);

        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
            Projectile.alpha = 255;
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void AI() {
            Age++;
            Projectile.alpha = (int)MathHelper.Lerp(220f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));
            Projectile.velocity *= Drag;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ && Main.rand.NextBool(5)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PinkTorch,
                    -Projectile.velocity * 0.15f, 160, default, 0.7f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.22f, 0.14f, 0.2f);
        }

        public override bool PreDraw(ref Color lightColor) {
            //豁免声明：棱光属光——纯加色绘制，随寿命收束（可见度与判定同门）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 origin = glow.Size() / 2f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float opacity = (1f - Projectile.alpha / 255f) * MathHelper.Clamp(Projectile.timeLeft / 16f, 0f, 1f);
            if (opacity <= 0.01f) {
                return false;
            }
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.12f, 0.4f, 1f);

            //速度拉伸的棱光条 + 小亮芯（读作运动的光，不是静态贴纸）
            Main.EntitySpriteDraw(glow, pos, null, PrismEdge * (0.6f * opacity), Projectile.rotation,
                origin, new Vector2(0.62f * stretch + 0.2f, 0.13f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, PrismCore * (0.85f * opacity), Projectile.rotation,
                origin, new Vector2(0.3f * stretch + 0.12f, 0.09f), SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PinkTorch,
                    Main.rand.NextVector2Circular(1.8f, 1.8f), 140, default, Main.rand.NextFloat(0.7f, 1.1f));
                dust.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item25 with { Volume = 0.3f, Pitch = 0.5f, MaxInstances = 4 }, Projectile.Center);
        }
    }
}
