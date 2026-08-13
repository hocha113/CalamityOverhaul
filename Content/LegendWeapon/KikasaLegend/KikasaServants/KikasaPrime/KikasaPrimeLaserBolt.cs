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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaPrime
{
    /// <summary>
    /// 机械骷髅王鬼奴的镭射短脉冲：一粒细而快的血光曳光弹——
    /// 不是持续光束，是双发点射里那一"哒"。速度拉伸的三层血芯 + 短尾迹，
    /// 命中小迸溅、落进血湖被湖收走。只在 owner 端生成
    /// </summary>
    internal class KikasaPrimeLaserBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Life => ref Projectile.localAI[0];

        private bool lakeSwallowed;

        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color CoreGlow => KikasaDomain.CoolTint(new(255, 150, 120), new(190, 214, 218));

        private float VisualFade => MathHelper.Clamp(Life / 3f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 7;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            //血光钉穿地飞：湖下真地形被湖面演出盖住，撞上去像凭空截停；
            //谢幕统一走 OnKill 小迸溅，不再依赖撞地
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //细快：双倍步进补出激光的干脆
            Projectile.extraUpdates = 1;
            Projectile.timeLeft = 120;
        }

        public override void AI() {
            Life++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行余滴：偶尔从弹体上甩出一粒失稳血珠
            if (!Main.dedServ && (int)Life % 6 == 2) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center - Projectile.velocity * 0.3f,
                    Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    BloodDeep * 0.5f, Main.rand.NextFloat(0.22f, 0.4f))?.Configure(Main.rand.Next(10, 18));
            }

            float glow = 0.45f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.5f * glow, 0.13f * glow, 0.1f * glow);

            //落进血湖：湖收回自己的血，不迸溅
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && Projectile.Center.Y >= domain.LakeWorldY + 4f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == domain) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.6f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 3);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = 0f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            //命中/超时共用的小迸溅（Kill 各端都跑，队友也看得见）
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            Vector2 normal = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.3f, Pitch = 0.25f, MaxInstances = 3 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, BloodDeep, 0.05f)
                ?.Configure(new Vector2(0.6f, 1f), normal.ToRotation(), 0.16f, 7);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(3f, 3f),
                    normal.RotatedByRandom(0.8f) * Main.rand.NextFloat(1.5f, 4.5f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(14, 24));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    normal.RotatedByRandom(0.5f) * Main.rand.NextFloat(3f, 6f),
                    Color.Lerp(CoreGlow, Color.White, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = tex.Size() * 0.5f;

            //短尾迹：旧位淡血线，快弹的速度读数
            Vector2[] oldPos = Projectile.oldPos;
            for (int i = oldPos.Length - 1; i >= 1; i--) {
                if (oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float k = 1f - i / (float)oldPos.Length;
                sb.Draw(tex, oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null,
                    BloodDeep * (0.22f * k * fade), Projectile.rotation, origin,
                    new Vector2(0.1f, 0.42f) * k, SpriteEffects.None, 0f);
            }

            //弹体：速度拉伸的三层细芯，亮芯走预乘加色
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.4f - Main.screenPosition;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.045f, 0.5f, 1.5f);
            sb.Draw(tex, pos, null, BloodDark * (0.8f * fade), Projectile.rotation, origin,
                new Vector2(0.16f, 0.3f + stretch * 0.5f), SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, BloodMain * fade, Projectile.rotation, origin,
                new Vector2(0.11f, 0.24f + stretch * 0.44f), SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, (CoreGlow with { A = 0 }) * (0.85f * fade), Projectile.rotation, origin,
                new Vector2(0.05f, 0.16f + stretch * 0.3f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
