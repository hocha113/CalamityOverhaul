using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 巨晶刺：预告即本体——蓄势期在原地画低亮度的晶簇鬼影（可见缺口=真实缺口），
    /// 而后拔地而起、驻留、收回。三刺簇轮廓（主刺全高+两侧矮刺错落，单贴图直拉会糊成针）。
    /// 伤害窗=可见柱体（拔起+驻留有伤，收回段无害）；柱底可悬空（落差阀由生成端裁决）。
    /// ai[0]=预告帧数，ai[1]=柱高
    /// </summary>
    internal class SeaShrimpCrystalSpike : SeaShrimpModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int OmenFrames => (int)Projectile.ai[0];
        private float SpikeHeight => Projectile.ai[1];

        private const int EruptFrames = 8;
        private const int HoldFrames = 26;
        private const int RetractFrames = 14;

        private int TotalLife => OmenFrames + EruptFrames + HoldFrames + RetractFrames;

        /// <summary>本地帧龄：localAI 逐端计数（timeLeft 不跨端，反推会在远端错位）</summary>
        private int Age => (int)Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 120;
        }

        /// <summary>可见柱高比例：预告 0，拔起急升，驻留 1，收回缓落</summary>
        private float Height01() {
            int age = Age;
            if (age < OmenFrames) {
                return 0f;
            }
            if (age < OmenFrames + EruptFrames) {
                float t = (age - OmenFrames) / (float)EruptFrames;
                return 1f - (1f - t) * (1f - t) * (1f - t);
            }
            if (age < OmenFrames + EruptFrames + HoldFrames) {
                return 1f;
            }
            float r = (age - OmenFrames - EruptFrames - HoldFrames) / (float)RetractFrames;
            return 1f - r;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            int age = Age;
            if (age >= TotalLife) {
                Projectile.Kill();
                return;
            }
            float h01 = Height01();
            if (h01 > 0.1f) {
                Lighting.AddLight(Projectile.Center - new Vector2(0f, SpikeHeight * h01 * 0.5f),
                    0.1f, 0.2f, 0.42f);
            }

            if (age < OmenFrames && !Main.dedServ) {
                //预告：地缝向心尘 + 蓝光渐起
                if (Main.GameUpdateCount % 3 == 0) {
                    Vector2 from = Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), -Main.rand.NextFloat(0f, 20f));
                    PRTLoader.NewParticle<PRT_Spark>(from,
                        new Vector2(-MathF.Sign(from.X - Projectile.Center.X) * 0.8f, -0.6f),
                        SeaShrimpRenderer.CrystalBlue * 0.8f,
                        Main.rand.NextFloat(0.3f, 0.55f))?.Configure(false, Main.rand.Next(8, 14));
                }
            }
            if (age == OmenFrames && !Main.dedServ) {
                //拔地帧：晶屑 + 湿沙水花锥（巨柱顶出，量随尺寸走）
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 4 }, Projectile.Center);
                EverdeepVFX.SplashBurst(Projectile.Center, Vector2.UnitY * 10f, 1.1f);
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_DefCrystalShard>(Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), 0f),
                        new Vector2(Main.rand.NextFloat(-2.2f, 2.2f), -Main.rand.NextFloat(2.5f, 6.5f)),
                        SeaShrimpRenderer.CrystalBlue, Main.rand.NextFloat(0.45f, 0.85f))?.Configure(Main.rand.Next(18, 32), Main.rand.NextFloat(-0.35f, 0.35f));
                }
            }
        }

        /// <summary>伤害窗=可见窗：拔起与驻留有伤，预告与收回无害</summary>
        public override bool? CanDamage() {
            int age = Age;
            return age >= OmenFrames && age < OmenFrames + EruptFrames + HoldFrames ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float h = SpikeHeight * Height01();
            if (h < 8f) {
                return false;
            }
            Rectangle column = new((int)(Projectile.Center.X - 34f), (int)(Projectile.Center.Y - h),
                68, (int)h);
            return column.Intersects(targetHitbox);
        }

        /// <summary>三刺簇布局：X 偏移 / 高度占比 / 横向拉伸（主刺居中全高，两侧矮刺错落）</summary>
        private static readonly (float OffX, float H, float W)[] Cluster = [
            (-30f, 0.58f, 2.1f),
            (32f, 0.72f, 2.4f),
            (0f, 1f, 3.1f),
        ];

        /// <summary>画一遍三刺簇（鬼影与实体共用同一轮廓：可见范围=判定范围）</summary>
        private static void DrawCluster(Texture2D tex, Vector2 basePos, Vector2 origin,
            float spikeHeight, float h01, System.Func<int, Color> colorOf, float extraScale = 1f) {
            for (int i = 0; i < Cluster.Length; i++) {
                (float offX, float hFrac, float w) = Cluster[i];
                float scaleY = spikeHeight * hFrac / tex.Height * h01;
                Main.spriteBatch.Draw(tex, basePos + new Vector2(offX, 0f), null, colorOf(i), 0f,
                    origin, new Vector2(w, scaleY) * extraScale, SpriteEffects.None, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //以女史莱姆蓝晶尖刺为本体素材（原版剪影与色板免费拿）
            Main.instance.LoadProjectile(ProjectileID.QueenSlimeMinionBlueSpike);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.QueenSlimeMinionBlueSpike]?.Value;
            if (tex == null) {
                return false;
            }

            int age = Age;
            float h01 = Height01();
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new(tex.Width * 0.5f, tex.Height);

            if (age < OmenFrames) {
                //鬼影预告：真实高度的低亮度剪影，可见范围=将来的判定范围
                float a = 0.16f + 0.22f * (age / (float)OmenFrames)
                    * (0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 18f + Projectile.identity));
                DrawCluster(tex, basePos, origin, SpikeHeight, 1f, _ => SeaShrimpRenderer.CrystalBlue * a);
                return false;
            }

            //暗缘剪影（亮背景下的轮廓保障）+ 主体 + 亮芯
            DrawCluster(tex, basePos + new Vector2(2f, 0f), origin, SpikeHeight, h01,
                _ => new Color(12, 20, 42) * 0.9f, 1.06f);
            Color lit = lightColor;
            DrawCluster(tex, basePos, origin, SpikeHeight, h01,
                i => i == Cluster.Length - 1 ? lit : lit.MultiplyRGB(new Color(180, 195, 225)));
            //亮芯只给主刺：晶簇的光从中央长出来
            float coreY = SpikeHeight / tex.Height * h01;
            Main.spriteBatch.Draw(tex, basePos, null,
                new Color(190, 225, 255, 120) * 0.6f, 0f, origin,
                new Vector2(1.4f, coreY * 0.98f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
