using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Sandveil.Projectiles
{
    /// <summary>
    /// 腐化沙鲨跃咬尾迹漏下的诅咒沙珠：慢速下坠的小弹。
    /// 出膛淡入期无判定（公平阀，伤害窗=可见窗）；
    /// 弹体走暗沙真 alpha 外壳+亮芯的双层配方（镜像 VileLanceProj.DrawGlob，
    /// 色板参考 DuneStorm 沙色偏诅咒绿），同材质拖尾 ≥0.5× 弹体横轴
    /// </summary>
    internal class SandveilCursedBeadProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>淡入帧数，判定开启与可见同门</summary>
        private const int FadeInFrames = 10;
        /// <summary>慢速下坠：每帧重力与终端速度</summary>
        private const float Gravity = 0.06f;
        private const float MaxFallSpeed = 5.5f;

        /// <summary>暗壳（真 alpha 实底）与亮芯（A=0 加色）：沙色底偏诅咒绿</summary>
        private static readonly Color ShellDeep = new(104, 106, 50);
        private static readonly Color CoreBright = new(176, 224, 104, 0);

        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.alpha = 255;
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void AI() {
            Age++;
            Projectile.alpha = (int)MathHelper.Lerp(220f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));

            Projectile.velocity.X *= 0.985f;
            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }
            Projectile.rotation += 0.08f;

            if (!Main.dedServ && Main.rand.NextBool(6)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.CursedTorch,
                    -Projectile.velocity * 0.2f, 150, default, 0.8f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.1f, 0.16f, 0.03f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() * 0.5f;
            float opacity = 1f - Projectile.alpha / 255f;

            //同材质拖尾（横轴 ≥0.5 倍体宽）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                DrawGlob(tex, origin, oldDrawPos, t * 0.35f * opacity, 0.55f * t);
            }
            DrawGlob(tex, origin, Projectile.Center - Main.screenPosition, opacity, 1f);
            return false;
        }

        /// <summary>双层实体：暗壳全 alpha ×1.18 打底遮挡，亮芯 A=0 缩在里面</summary>
        private void DrawGlob(Texture2D tex, Vector2 origin, Vector2 drawPos, float alpha, float scaleMul) {
            Vector2 scale = new Vector2(0.3f, 0.3f) * scaleMul;
            Main.EntitySpriteDraw(tex, drawPos, null, ShellDeep * (0.92f * alpha),
                Projectile.rotation, origin, scale * 1.18f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, CoreBright * (0.8f * alpha),
                Projectile.rotation, origin, scale * 0.72f, SpriteEffects.None, 0);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.CursedTorch,
                    Main.rand.NextVector2Circular(1.6f, 1.6f), 120, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.NPCHit9 with { Volume = 0.3f, Pitch = 0.3f, MaxInstances = 4 }, Projectile.Center);
        }
    }
}
