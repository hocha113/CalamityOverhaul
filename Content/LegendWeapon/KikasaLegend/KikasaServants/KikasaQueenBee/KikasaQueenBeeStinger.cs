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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaQueenBee
{
    /// <summary>
    /// 鬼奴蜂后的血色螫针：四相演出预算——升空拖珠、顶点悬滞翻转、
    /// 坠落拉丝、落点收尾（贴壁血渍 / 落水涟漪 / 命中化珠）。
    /// 弹道纯确定性（重力 + 出生速度），顶点行为由本地速度符号裁决，各端一致
    /// </summary>
    internal class KikasaQueenBeeStinger : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>升空段重力（缓，读出高抛）</summary>
        private const float RiseGravity = 0.42f;

        /// <summary>坠落段重力（急，雨要有分量）</summary>
        private const float FallGravity = 0.5f;

        /// <summary>顶点悬滞帧数：重力反转的呼吸口</summary>
        private const int ApexFrames = 12;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>顶点相进度：0=未到顶，1..ApexFrames=悬滞中，>ApexFrames=坠落</summary>
        private int apexTimer;
        private float apexBaseRot;
        private bool apexLatched;
        //贴壁演出已放 / 被湖收走：OnKill 分路
        private bool burstDone;
        private bool lakeSwallowed;

        //==================== 血色板（随观看域鬼雨异化冷化）====================

        private static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color GlintShine => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));

        /// <summary>连续量抖动的确定性相位（9.1：弹道不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.31f;

        /// <summary>出生 3 帧淡入</summary>
        private float VisualFade => MathHelper.Clamp(Life / 3f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            //不吃引擎地形碰撞：湖下真地形被湖面演出盖住，撞上去像凭空截停；
            //贴壁血渍改走 AI 内手动检测（只认水线以上的真地形）
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //独立命中冷却：针雨不写全局无敌帧，与蜂群、耙扫互不抢结算
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        /// <summary>是否撞上"看得见"的真地形：湖线以下的墙体被湖面演出盖住，不算贴壁</summary>
        private bool TouchingVisibleTile() {
            if (!Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                return false;
            }
            Player owner = Main.player[Projectile.owner];
            return owner?.active != true
                || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                || !domain.AnyActive || domain.RiseT <= 0.5f
                || Projectile.Center.Y < domain.LakeWorldY - 2f;
        }

        public override void AI() {
            Life++;

            //贴壁收尾（机制身份保留）：手动地形检测替代 tileCollide
            if (Life > 3 && TouchingVisibleTile()) {
                WallStick();
                return;
            }

            if (!apexLatched) {
                if (Projectile.velocity.Y < -0.5f) {
                    //升空：缓重力慢慢吃掉冲量，针尾拖珠
                    Projectile.velocity.Y += RiseGravity;
                    Projectile.velocity.X *= 0.995f;
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                    if (!Main.dedServ && Life % 4 == 1) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            Projectile.Center - Projectile.velocity * 0.5f,
                            Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                            BloodMain * 0.45f, Main.rand.NextFloat(0.22f, 0.4f))
                            ?.Configure(Main.rand.Next(12, 20), 0.3f);
                    }
                    return;
                }
                //顶点闩：升力耗尽的那一帧进悬滞
                apexLatched = true;
                apexBaseRot = Projectile.rotation;
            }

            if (apexTimer < ApexFrames) {
                //顶点悬滞：动量近乎冻结，针身缓缓翻转向下——重力反转的可读拍
                apexTimer++;
                Projectile.velocity *= 0.8f;
                float flip = SmoothStep01(apexTimer / (float)ApexFrames);
                //翻转方向跟着水平残速走，两侧的针各自向内翻
                float sign = Projectile.velocity.X >= 0f ? 1f : -1f;
                Projectile.rotation = apexBaseRot + MathHelper.Pi * flip * sign;
                //悬滞中点的一粒冷光——雨落前最后的安静
                if (!Main.dedServ && apexTimer == ApexFrames / 2) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, Vector2.Zero,
                        GlintShine, 0.7f)?.Configure(false, 10);
                }
                return;
            }

            //坠落：急重力拉丝；朝向从悬滞翻转角平滑并入速度方向，不许跳变
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + FallGravity, 17f);
            Projectile.velocity.X *= 0.99f;
            Projectile.rotation = Projectile.rotation.AngleLerp(
                Projectile.velocity.ToRotation() + MathHelper.PiOver2, 0.3f);
            if (!Main.dedServ && Life % 5 == 2) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center - Projectile.velocity * 0.6f,
                    Projectile.velocity * 0.08f,
                    BloodDeep * 0.4f, Main.rand.NextFloat(0.2f, 0.34f))
                    ?.Configure(Main.rand.Next(10, 16), 0.24f);
            }

            float glow = 0.3f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.4f * glow, 0.1f * glow, 0.09f * glow);

            //落水收尾：涟漪一圈，湖把针收走
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && Projectile.Center.Y >= domain.LakeWorldY + 4f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == domain) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.6f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 2);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.25f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
            }
        }

        //==================== 命中与谢幕 ====================

        /// <summary>贴壁：小迸溅 + 血渍，渍会挂壁滴淌</summary>
        private void WallStick() {
            burstDone = true;
            if (!Main.dedServ) {
                Vector2 normal = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(Projectile.Center,
                        normal.RotatedByRandom(0.8f) * Main.rand.NextFloat(1.5f, 4f),
                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(16, 28));
                }
                PRTLoader.NewParticle<PRT_KikasaBloodSmear>(Projectile.Center + normal * 2f,
                    Vector2.Zero, BloodMain, Main.rand.NextFloat(0.5f, 0.7f))
                    ?.Configure(Main.rand.Next(70, 100));
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.3f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            //命中 NPC / 超时化珠（贴壁与落湖各有自己的收尾）
            if (Main.dedServ || lakeSwallowed || burstDone) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(1.4f, 1.4f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(12, 22));
            }
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();
            //坠落段拉丝：越快越细长
            float stretch = MathHelper.Clamp(speed * 0.05f, 0.1f, 1.1f);

            //坠落拖影：旧位残针一线排开
            if (apexTimer >= ApexFrames && speed > 7f) {
                for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                    Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                    if (oldCenter == Projectile.Size * 0.5f) {
                        continue;
                    }
                    float fall = 1f - k / (float)Projectile.oldPos.Length;
                    sb.Draw(tex, oldCenter - Main.screenPosition, null,
                        BloodDark * (0.3f * fall * fade), Projectile.rotation, origin,
                        new Vector2(0.07f, 0.3f * fall), SpriteEffects.None, 0f);
                }
            }

            //针体：暗缘→血红本体→亮芯，窄长的三层渐细
            sb.Draw(tex, pos, null, BloodDark * (0.9f * fade), Projectile.rotation, origin,
                new Vector2(0.15f, 0.44f + stretch * 0.4f), SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, BloodMain * fade, Projectile.rotation, origin,
                new Vector2(0.11f, 0.36f + stretch * 0.36f), SpriteEffects.None, 0f);
            Color core = GlintShine with { A = 0 };
            sb.Draw(tex, pos, null, core * (0.55f * fade), Projectile.rotation, origin,
                new Vector2(0.05f, 0.22f + stretch * 0.24f), SpriteEffects.None, 0f);

            //顶点悬滞的微光呼吸
            if (apexLatched && apexTimer < ApexFrames) {
                float pulse = MathF.Sin(apexTimer / (float)ApexFrames * MathHelper.Pi);
                sb.Draw(tex, pos, null, core * (0.4f * pulse * fade), Projectile.rotation + Seed, origin,
                    new Vector2(0.2f, 0.2f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
