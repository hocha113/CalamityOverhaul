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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaSkeletron
{
    /// <summary>
    /// 骷髅王鬼奴口吐的追踪血颅：幽蓝偏冷的小颅，出膛先抛一口弧线再咬住猎物，
    /// 航向叠加正弦扭摆走蛇形尾迹；头骨贴图缩小重染 + 眼窝鬼火 + 旧位残影串尾。
    /// ai[0]=追踪目标（owner 端定，spawn 包自带），ai[1]=蛇摆相位符号。
    /// 命中/超时冷色迸溅，落回血湖被湖收走不迸溅；鬼物穿行地形不受阻
    /// </summary>
    internal class KikasaSkeletronBloodSkull : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>出膛后多少帧开始咬住目标：先把上抛弧线画完</summary>
        private const int HomingDelay = 10;

        private ref float Life => ref Projectile.localAI[0];
        private int TargetIndex => (int)Projectile.ai[0];
        private float SwaySign => Projectile.ai[1] >= 0f ? 1f : -1f;

        //被湖收走：谢幕换涟漪，不走迸溅
        private bool lakeSwallowed;

        //==================== 冷端色板（幽蓝鬼火为主，异化时褪成潮灰）====================

        internal static Color SkullBone => KikasaDomain.CoolTint(new(196, 216, 230), new(184, 198, 204));
        internal static Color SkullGlow => KikasaDomain.CoolTint(new(118, 188, 232), new(152, 196, 206));
        internal static Color SkullDeep => KikasaDomain.CoolTint(new(46, 80, 108), new(46, 60, 66));

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 200;
            //鬼火颅穿地飞：湖下真地形被湖面演出盖住，撞上去像凭空截停；
            //追踪弹穿行地形也贴合鬼物读感，谢幕统一走 OnKill 迸溅
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            if (Life > HomingDelay) {
                NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[TargetIndex] : null;
                if (target?.active == true && target.CanBeChasedBy(Projectile)) {
                    //咬合：转率随追猎时间爬升，速度复利到上限
                    float turn = MathHelper.Lerp(0.035f, 0.1f, MathHelper.Clamp((Life - HomingDelay) / 30f, 0f, 1f));
                    float wantAngle = (target.Center - Projectile.Center).ToRotation();
                    float angle = Projectile.velocity.ToRotation().AngleTowards(wantAngle, turn);
                    float speed = MathF.Min(Projectile.velocity.Length() * 1.012f, 14.5f);
                    Projectile.velocity = angle.ToRotationVector2() * speed;
                }
                else {
                    //没猎物就滑翔泄劲，寿终由 timeLeft 收
                    Projectile.velocity *= 0.995f;
                }
            }
            else {
                //上抛段微重力，弧线读得出"吐"的抛物感
                Projectile.velocity.Y += 0.12f;
            }

            //蛇形摆尾：航向逐帧叠加正弦扭摆，路径本身就是尾迹
            Projectile.velocity = Projectile.velocity.RotatedBy(
                MathF.Sin(Life * 0.38f + Seed * 2f) * 0.05f * SwaySign);
            //齿端领飞
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            //沿途滴落冷血珠，蛇尾滴痕
            if (!Main.dedServ && Life % 3 == 0) {
                Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + back * Main.rand.NextFloat(4f, 12f),
                    Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    Main.rand.NextBool(3) ? SkullDeep : SkullGlow,
                    Main.rand.NextFloat(0.28f, 0.5f))?.Configure(Main.rand.Next(14, 24), 0.22f);
            }

            float glow = 0.5f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.10f * glow, 0.26f * glow, 0.38f * glow);

            //落回血湖：湖收回自己的东西，不迸溅
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && Projectile.Center.Y >= domain.LakeWorldY + 4f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == domain) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.7f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 3);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            ColdBurst(Projectile.Center, Projectile.velocity);
        }

        /// <summary>冷色迸溅：半球冷血珠 + 骨响碎裂 + 冷环，颅骨在此散架</summary>
        private static void ColdBurst(Vector2 pos, Vector2 impactVel) {
            if (Main.dedServ) {
                return;
            }
            Vector2 normal = -impactVel.SafeNormalize(Vector2.UnitY);
            float ke = MathHelper.Clamp(impactVel.Length() / 16f, 0.4f, 1f);
            for (int i = 0; i < 9; i++) {
                Vector2 vel = normal.RotatedByRandom(1.2f) * Main.rand.NextFloat(1.8f, 6.4f) * (0.5f + ke);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos + Main.rand.NextVector2Circular(6f, 6f),
                    vel, Main.rand.NextBool(3) ? SkullDeep : SkullBone,
                    Main.rand.NextFloat(0.32f, 0.6f))?.Configure(Main.rand.Next(18, 30));
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, SkullGlow, 0.06f)
                ?.Configure(new Vector2(0.8f, 1f), normal.ToRotation(), 0.2f + 0.12f * ke, 8);

            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.5f, Pitch = 0.25f, MaxInstances = 3 }, pos);
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.1f, MaxInstances = 3 }, pos);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.SkeletronHead);
            Texture2D tex = TextureAssets.Npc[NPCID.SkeletronHead]?.Value;
            if (tex == null) {
                return false;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.SkeletronHead];
            Rectangle frame = new(0, 0, tex.Width, frameH);
            Vector2 origin = frame.Size() * 0.5f;
            float fade = VisualFade;
            const float scale = 0.3f;
            SpriteBatch sb = Main.spriteBatch;

            //旧位残影串尾：越旧越小越淡，蛇身节节读出来
            for (int k = Projectile.oldPos.Length - 1; k >= 1; k -= 2) {
                Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                if (oldCenter == Projectile.Size * 0.5f) {
                    continue;
                }
                float fall = 1f - k / (float)Projectile.oldPos.Length;
                sb.Draw(tex, oldCenter - Main.screenPosition, frame,
                    (SkullGlow with { A = 0 }) * (0.22f * fall * fade), Projectile.oldRot[k],
                    origin, scale * (0.85f - k * 0.02f), SpriteEffects.None, 0f);
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            //暗缘压边给体积
            sb.Draw(tex, pos, frame, SkullDeep * (0.8f * fade), Projectile.rotation,
                origin, scale * 1.13f, SpriteEffects.None, 0f);
            //冷骨主体
            sb.Draw(tex, pos, frame, SkullBone * (0.95f * fade), Projectile.rotation,
                origin, scale, SpriteEffects.None, 0f);

            //眼窝鬼火：两点冷光 + 呼吸闪烁
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                float flick = 0.7f + 0.3f * MathF.Sin(Life * 0.5f + Seed * 4f);
                Vector2 gOrigin = glowTex.Size() * 0.5f;
                Color fire = (SkullGlow with { A = 0 }) * (0.85f * fade * flick);
                for (int side = -1; side <= 1; side += 2) {
                    Vector2 eye = Projectile.Center
                        + new Vector2(side * 14f, -10f).RotatedBy(Projectile.rotation) * scale;
                    sb.Draw(glowTex, eye - Main.screenPosition, null, fire, 0f,
                        gOrigin, new Vector2(9f * 2f / glowTex.Width), SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
