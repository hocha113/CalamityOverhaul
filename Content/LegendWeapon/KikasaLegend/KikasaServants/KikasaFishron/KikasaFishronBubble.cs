using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaFishron
{
    /// <summary>
    /// 鬼奴猪龙鱼绕目标吐下的悬滞血气泡雷：出口短漂后停驻原地缓慢浮沉，
    /// 薄壁血膜带表面张力抖动与湿反光，触碰敌人或超时都"噗"地炸成一小蓬血雾。
    /// 尾段膜色转暗、抖动加剧作濒爆预告。全程无中途裁决，
    /// 漂移抖动走 identity 确定性相位，各端自演一致
    /// </summary>
    internal class KikasaFishronBubble : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int FloatLife = 280;
        /// <summary>濒爆预告窗</summary>
        private const int ShiverFrames = 42;

        /// <summary>吐泡序号相位：一环九泡错开呼吸</summary>
        private float PhaseOffset => Projectile.ai[0];

        private int Elapsed => FloatLife - Projectile.timeLeft;

        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color FoamPale => KikasaDomain.CoolTint(new(214, 118, 106), new(170, 185, 190));

        private float Seed => Projectile.identity * 0.7391f % 5.19f + PhaseOffset;

        /// <summary>出生 6 帧吹胀成形</summary>
        private float Inflate => MathHelper.Clamp(Elapsed / 6f, 0f, 1f);

        /// <summary>濒爆度 0~1</summary>
        private float ShiverT => MathHelper.Clamp(1f - Projectile.timeLeft / (float)ShiverFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //本地免疫走"每弹一命"，一颗爆开不占用全局免疫帧挡住邻泡
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            //悬滞近五秒，迟入场的客户端也要收到它
            Projectile.netImportant = true;
            Projectile.timeLeft = FloatLife;
        }

        /// <summary>吹胀成形前不伤人：雷是看得见的那颗</summary>
        public override bool? CanDamage() => Elapsed > 5 ? null : false;

        public override bool? CanCutTiles() => false;

        public override void AI() {
            int t = Elapsed;

            //出口短漂 → 悬滞
            if (t > 10) {
                Projectile.velocity *= 0.93f;
            }
            //悬滞态的确定性浮沉与横漂（各端一致，不掷 Main.rand）
            float bob = MathF.Sin(t * 0.045f + Seed) * 0.055f;
            float drift = MathF.Sin(t * 0.021f + Seed * 2.3f) * 0.03f;
            Projectile.velocity.X += drift * (1f + ShiverT * 1.5f);
            Projectile.velocity.Y += bob - 0.006f;

            //钻进实体墙就闷掉，别在石头里悬着——只认水线以上的真地形：
            //湖线以下的墙体被湖面演出盖住，泡在"湖里"生成时不许被隐形地形闷爆
            if (t > 8 && t % 5 == 0
                && Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                Player owner = Main.player[Projectile.owner];
                bool underLake = owner?.active == true
                    && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                    && domain.AnyActive && domain.RiseT > 0.5f
                    && Projectile.Center.Y >= domain.LakeWorldY - 2f;
                if (!underLake) {
                    Projectile.Kill();
                    return;
                }
            }

            //濒爆滴答：膜面抖动加剧 + 两声细响
            if (Projectile.timeLeft == ShiverFrames || Projectile.timeLeft == ShiverFrames / 2) {
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = 0.55f, MaxInstances = 3 }, Projectile.Center);
            }

            float glow = 0.3f * Inflate;
            Lighting.AddLight(Projectile.Center, 0.35f * glow, 0.09f * glow, 0.08f * glow);
        }

        //==================== 谢幕：噗 ====================

        public override void OnKill(int timeLeft) {
            //penetrate=1：命中与超时共用这记"噗"，各端都跑，队友也听得见
            SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.45f, Pitch = 0.15f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            //小蓬血雾 + 环破血珠（雾色也走冷化家族）
            Color mist = KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.15f), mist * 0.8f, Main.rand.NextFloat(0.5f, 0.75f))
                ?.Configure(Main.rand.Next(40, 64));
            for (int i = 0; i < 7; i++) {
                float ang = MathHelper.TwoPi * i / 7f + Main.rand.NextFloat(-0.3f, 0.3f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + ang.ToRotationVector2() * 6f,
                    ang.ToRotationVector2() * Main.rand.NextFloat(1.4f, 3.2f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(16, 28));
            }
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, BloodDeep, 0.05f)
                ?.Configure(new Vector2(0.9f, 1f), 0f, 0.16f, 8);
        }

        //==================== 绘制：薄壁血膜 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            Texture2D fill = CWRAsset.Extra_98?.Value;
            if (ring == null || fill == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            float inflate = Inflate;
            if (inflate <= 0.02f) {
                return false;
            }
            int t = Elapsed;
            float shiver = ShiverT;

            //表面张力抖动：宽窄反相呼吸，濒爆时抖得发慌
            float wobAmp = 0.07f + shiver * 0.12f;
            float wob = MathF.Sin(t * (0.31f + shiver * 0.25f) + Seed * 4f) * wobAmp;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.85f);
            float r = 15f * inflate;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //膜色随濒爆转暗凝
            Color shell = Color.Lerp(BloodMain, BloodDark, 0.25f + shiver * 0.45f);
            Color inner = BloodDeep * (0.18f + shiver * 0.10f);

            //内腔薄雾
            sb.Draw(fill, pos, null, inner * inflate, 0f, fill.Size() * 0.5f,
                new Vector2(r * 1.5f / (fill.Width * 0.5f)) * jiggle, SpriteEffects.None, 0f);
            //薄壁：环贴图本身就是空心的，膜才读作膜
            sb.Draw(ring, pos, null, shell * (0.85f * inflate), t * 0.01f + Seed, ring.Size() * 0.5f,
                new Vector2(r * 2f / ring.Width, r * 2f / ring.Height) * jiggle, SpriteEffects.None, 0f);
            //湿反光点：左上高光 + 渊青次要点缀
            Color glint = FoamPale with { A = 0 };
            sb.Draw(fill, pos + new Vector2(-r * 0.36f, -r * 0.4f), null, glint * (0.55f * inflate), 0f,
                fill.Size() * 0.5f, new Vector2(r * 0.34f / (fill.Width * 0.5f)), SpriteEffects.None, 0f);
            sb.Draw(fill, pos + new Vector2(r * 0.3f, r * 0.34f), null,
                (KikasaFishronServant.AbyssSheen with { A = 0 }) * (0.22f * inflate), 0f,
                fill.Size() * 0.5f, new Vector2(r * 0.2f / (fill.Width * 0.5f)), SpriteEffects.None, 0f);

            return false;
        }
    }
}
