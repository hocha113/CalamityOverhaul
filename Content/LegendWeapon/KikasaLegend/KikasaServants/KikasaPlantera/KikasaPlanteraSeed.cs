using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaPlantera
{
    /// <summary>
    /// 鬼奴世纪之花的血色种子：机关枪弹丸。出膛后短程增速（活着的弹道，
    /// 不做匀速平移），远程微坠；飞行沿途撕落细血珠，速度拉伸绘形；
    /// 命中/超时半球小迸溅，落空坠回血湖时被湖收走；鬼物穿行地形不受阻
    /// </summary>
    internal class KikasaPlanteraSeed : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Life => ref Projectile.ai[0];

        //被湖收走：谢幕换涟漪
        private bool lakeSwallowed;

        private static Color BloodMain => KikasaPlanteraServant.BloodMain;
        private static Color BloodDeep => KikasaPlanteraServant.BloodDeep;
        private static Color FoamGlow => KikasaPlanteraServant.FoamGlow;

        /// <summary>出生 3 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 3f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            //鬼物弹丸穿地飞：湖下真地形被湖面演出盖住，撞上去像凭空截停；
            //谢幕统一走 OnKill 迸溅，不再依赖撞地
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            //出膛短程复利增速到极速，随后远程微坠——种子是"打"出去的
            float speed = Projectile.velocity.Length();
            if (speed < 23f) {
                Projectile.velocity *= 1.028f;
            }
            if (Life > 30f) {
                Projectile.velocity.Y += 0.055f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行撕落细血珠
            if (!Main.dedServ && (int)Life % 2 == 0) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center - Projectile.velocity * 0.4f,
                    Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * 0.55f,
                    Main.rand.NextFloat(0.22f, 0.38f))?.Configure(Main.rand.Next(8, 14), 0.1f);
            }

            float glow = 0.35f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.45f * glow, 0.12f * glow, 0.1f * glow);

            //落空坠回血湖：湖收回自己的血，不迸溅
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && Projectile.Center.Y >= domain.LakeWorldY + 4f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == domain) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.55f);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.25f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            //命中 NPC / 超时坠灭共用（penetrate=1，Kill 各端都跑，队友也看得见）
            ImpactBurst(Projectile.Center, Projectile.velocity);
        }

        /// <summary>种子炸开：半球血珠 + 一圈扩散环 + 血尘底噪</summary>
        private static void ImpactBurst(Vector2 pos, Vector2 impactVel) {
            if (Main.dedServ) {
                return;
            }
            Vector2 normal = -impactVel.SafeNormalize(Vector2.UnitY);
            float mainAngle = normal.ToRotation();
            for (int i = 0; i < 6; i++) {
                float spreadAngle = Main.rand.NextFloat(-MathHelper.PiOver2, MathHelper.PiOver2);
                Vector2 vel = (mainAngle + spreadAngle).ToRotationVector2()
                    * Main.rand.NextFloat(1.6f, 5.5f) * (1f - MathF.Abs(spreadAngle) * 0.3f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos + Main.rand.NextVector2Circular(4f, 4f),
                    vel, Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(16, 26));
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, BloodDeep, 0.06f)
                ?.Configure(new Vector2(0.7f, 1f), mainAngle, 0.17f, 8);
            for (int i = 0; i < 2; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                    normal.RotatedByRandom(0.8f) * Main.rand.NextFloat(1f, 3f), 100, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.3f, Pitch = 0.1f, MaxInstances = 3 }, pos);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.SeedPlantera);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.SeedPlantera]?.Value;
            if (tex == null) {
                return false;
            }
            //原版种子双帧
            int frameH = tex.Height / 2;
            Rectangle frame = new(0, frameH * ((int)Life / 3 % 2), tex.Width, frameH);
            Vector2 origin = frame.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float fade = VisualFade;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0f, 0.7f);
            Vector2 scale = new(1.25f * (1f - stretch * 0.25f), 1.25f * (1f + stretch));

            //暗血压边给体积，血红主体，亮芯湿反光
            SpriteBatch sb = Main.spriteBatch;
            Color rim = Color.Lerp(Color.White, BloodDeep, 0.85f) * (0.9f * fade);
            Color body = Color.Lerp(Color.White, BloodMain, 0.7f) * fade;
            sb.Draw(tex, pos, frame, rim, Projectile.rotation, origin, scale * 1.18f, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, frame, body, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
            Color core = FoamGlow with { A = 0 };
            sb.Draw(tex, pos, frame, core * (0.45f * fade), Projectile.rotation, origin, scale * 0.6f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
