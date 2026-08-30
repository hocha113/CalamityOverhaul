using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 泡间电弧：两个爆点之间的闪电链接（雷泡链爆/泡球连拍共用）。
    /// 静态两点——生成时 Center=中点、velocity=半长向量（仅存数据，不位移），
    /// 生成包原子携带两端点，各端一致。12f 预警细弧无伤 → 18f 全功率线段判定 → 10f 消散
    /// </summary>
    internal class SeaShrimpBubbleArc : SeaShrimpModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        [VaultLoaden(CWRConstant.Masking + "ThunderTrail")]
        private static Asset<Texture2D> ThunderTex = null;

        private const int WarmupFrames = 12;
        private const int FullFrames = 18;
        private const int FadeFrames = 10;
        private const int TotalFrames = WarmupFrames + FullFrames + FadeFrames;
        private const int ArcPointCount = 14;

        /// <summary>本地帧龄：逐端计数，迟入端不重播预警</summary>
        private int Age => (int)Projectile.localAI[0];

        private Vector2 EndA => Projectile.Center - Projectile.velocity;
        private Vector2 EndB => Projectile.Center + Projectile.velocity;

        private ThunderTrail mainTrail;
        private ThunderTrail coreTrail;
        /// <summary>0~1 当前功率（预警→全功率→消散）</summary>
        private float power;

        /// <summary>深渊生物电弧色</summary>
        internal static Color ArcColor => new(120, 226, 250);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = TotalFrames + 8;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.localAI[0]++;
            int age = Age;
            if (age >= TotalFrames) {
                Projectile.Kill();
                return;
            }

            //功率包络：预警细弧→急升全功率→消散
            if (age < WarmupFrames) {
                power = age / (float)WarmupFrames * 0.25f;
            }
            else if (age >= WarmupFrames + FullFrames) {
                power = MathHelper.Lerp(1f, 0f, (age - WarmupFrames - FullFrames) / (float)FadeFrames);
            }
            else {
                float t = MathHelper.Clamp((age - WarmupFrames) / 6f, 0f, 1f);
                power = MathHelper.Lerp(0.25f, 1f, 1f - (1f - t) * (1f - t) * (1f - t));
            }

            if (age == 1 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 4 }, Projectile.Center);
            }
            if (age == WarmupFrames && !Main.dedServ) {
                //全功率帧：接通爆鸣
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.55f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
            }

            if (Main.dedServ) {
                return;
            }

            BuildArcPath(EndA, EndB);

            //沿线蓝辉光照
            for (int i = 0; i < 4; i++) {
                Lighting.AddLight(Vector2.Lerp(EndA, EndB, i / 3f), ArcColor.ToVector3() * 0.4f * power);
            }
        }

        /// <summary>两爆点间采样并扰动电弧路径（两端固定，中段正弦摆）</summary>
        private void BuildArcPath(Vector2 start, Vector2 end) {
            Vector2[] points = new Vector2[ArcPointCount];
            Vector2 dir = end - start;
            Vector2 perp = dir.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            float waveSeed = Main.GlobalTimeWrappedHourly * 10f + Projectile.identity * 0.7f;

            for (int i = 0; i < ArcPointCount; i++) {
                float t = i / (float)(ArcPointCount - 1);
                float envelope = MathF.Sin(t * MathHelper.Pi);
                float wave = MathF.Sin(waveSeed + t * 12f) * 13f * envelope * power;
                points[i] = start + dir * t + perp * wave;
            }

            if (mainTrail == null) {
                mainTrail = new ThunderTrail(ThunderTex, GetMainWidth, GetMainColor, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 3,
                };
                mainTrail.SetRange((0, 9));
                mainTrail.SetExpandWidth(5);

                coreTrail = new ThunderTrail(ThunderTex, GetCoreWidth, GetCoreColor, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 2,
                };
                coreTrail.SetRange((0, 4));
                coreTrail.SetExpandWidth(3);
            }

            mainTrail.BasePositions = points;
            coreTrail.BasePositions = points;
            if (Age % 3 == 0) {
                mainTrail.RandomThunder();
                coreTrail.RandomThunder();
            }
        }

        private float GetMainWidth(float factor) => (14f + 8f * MathF.Sin(factor * MathHelper.Pi)) * power;
        private float GetCoreWidth(float factor) => (6f + 3f * MathF.Sin(factor * MathHelper.Pi)) * power;
        private Color GetMainColor(float factor) => ArcColor;
        private Color GetCoreColor(float factor) => Color.White;
        private float GetArcAlpha(float factor) => power;

        /// <summary>伤害窗=功率过半（预警细弧无伤；消散段随亮度跌破一半即无害，伤害窗=弧仍亮的窗）</summary>
        public override bool? CanDamage() => power >= 0.5f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                EndA, EndB, 28f * power, ref p);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Electrified, 60);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (power <= 0.02f) {
                return false;
            }
            mainTrail?.DrawThunder(Main.instance.GraphicsDevice);
            coreTrail?.DrawThunder(Main.instance.GraphicsDevice);

            //两端点辉光：电弧咬在爆点上的读数
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            Color glowColor = ArcColor with { A = 0 };
            float pulse = 1f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 24f + Projectile.identity);
            Main.EntitySpriteDraw(glow, EndA - Main.screenPosition, null, glowColor * (0.7f * power),
                0f, glow.Size() / 2f, 0.5f * power * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, EndB - Main.screenPosition, null, glowColor * (0.7f * power),
                0f, glow.Size() / 2f, 0.5f * power * pulse, SpriteEffects.None, 0);
            return false;
        }
    }
}
