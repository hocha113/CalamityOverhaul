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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaGolem
{
    /// <summary>
    /// 石首浮屠的眼窝血珠：从眼窝里喷出的一小口滚烫积血，点缀性小弹（非激光）。
    /// 微重力抛线、带一缕余烬，命中溅小口血火，落空坠湖被收走
    /// </summary>
    internal class KikasaGolemEyeSpit : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>出膛后多少帧开始吃重力</summary>
        private const int GravityDelay = 10;

        private ref float Life => ref Projectile.ai[0];

        private bool lakeSwallowed;

        private static Color EmberTint => KikasaDomain.CoolTint(new(255, 140, 66), new(160, 182, 186));

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 3 帧淡入</summary>
        private float VisualFade => MathHelper.Clamp(Life / 3f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            //鬼物小弹穿地飞：湖下真地形被湖面演出盖住，撞上去像凭空截停；
            //谢幕统一走 OnKill 迸溅，不再依赖撞地
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            if (Life > GravityDelay) {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.12f, 12f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //一缕余烬跟在珠后
            if (!Main.dedServ && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center - Projectile.velocity * 0.5f,
                    -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    Color.Lerp(KikasaEyeBloodShot.BloodMain, EmberTint, Main.rand.NextFloat(0.6f)),
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(false, Main.rand.Next(8, 14));
            }

            float glow = 0.3f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.4f * glow, 0.16f * glow, 0.07f * glow);

            //落空坠湖：湖收回自己的血
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && Projectile.velocity.Y > 0f
                && Projectile.Center.Y >= domain.LakeWorldY + 2f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == domain) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.4f);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            //小口血火迸开（penetrate=1，Kill 各端都跑）
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.3f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    -Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.9f) * Main.rand.NextFloat(1.2f, 3.4f),
                    Main.rand.NextBool(3) ? KikasaEyeBloodShot.BloodDeep : KikasaEyeBloodShot.BloodMain,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 22));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    -Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.7f) * Main.rand.NextFloat(1.5f, 3.5f),
                    EmberTint, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || VisualFade <= 0.01f) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.04f, 0.1f, 0.8f);
            float wob = MathF.Sin(Life * 0.6f + Seed * 5f) * 0.1f;
            SpriteBatch sb = Main.spriteBatch;

            //暗血压边→血珠主体→烬色小芯
            sb.Draw(tex, pos, null, KikasaEyeBloodShot.BloodDark * (0.85f * fade), Projectile.rotation, origin,
                new Vector2(0.24f, 0.3f + stretch * 0.5f) * (1f + wob), SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, KikasaEyeBloodShot.BloodMain * fade, Projectile.rotation, origin,
                new Vector2(0.18f, 0.24f + stretch * 0.45f) * (1f + wob), SpriteEffects.None, 0f);
            Color core = EmberTint with { A = 0 };
            sb.Draw(tex, pos, null, core * (0.6f * fade), Projectile.rotation, origin,
                new Vector2(0.08f, 0.13f + stretch * 0.2f), SpriteEffects.None, 0f);

            return false;
        }
    }
}
