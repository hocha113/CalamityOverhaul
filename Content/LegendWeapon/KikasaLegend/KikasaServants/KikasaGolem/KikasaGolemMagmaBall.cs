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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaGolem
{
    /// <summary>
    /// 石首浮屠的岩浆血火球：一团熔壳裹血的炽石，低平抛出、被重力按到湖面上，
    /// 在水面弹跳一次（打水漂——鬼奴独占的弹道语义）再扑向目标：
    /// 第一跳压低角度、激起一线蒸汽白雾，跳后短暂寻的；命中爆出岩浆血溅，
    /// 第二次触水则被湖收走。ai0=跳跃状态(0可跳/1已跳或不跳)，ai1=目标序号，
    /// 弹跳判据读领域水线、各端同规则推进，owner 在弹跳帧盖章纠偏
    /// </summary>
    internal class KikasaGolemMagmaBall : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>出膛后多少帧开始吃重力：低平段先飞一小截</summary>
        private const int GravityDelay = 6;

        /// <summary>跳后寻的窗口（帧）</summary>
        private const int HomingFrames = 34;

        /// <summary>跳跃状态：0=水漂未用，1=已跳/不跳直坠</summary>
        private ref float SkipState => ref Projectile.ai[0];

        /// <summary>目标 NPC 序号（-1 无目标），跳后寻的用；各端同读同规则</summary>
        private ref float TargetIndex => ref Projectile.ai[1];

        private int life;
        private int homingLeft;
        //跳跃闩：远端可能靠同步包得知已跳，也要补演出
        private bool bounceSeen;
        private bool burstDone;
        private bool lakeSwallowed;

        //岩浆血色板：橙热做点缀层，主体仍在血系冷化家族里
        private static Color MagmaDark => KikasaDomain.CoolTint(new(70, 24, 16), new(48, 58, 62));
        private static Color MagmaDeep => KikasaDomain.CoolTint(new(180, 60, 26), new(96, 116, 120));
        private static Color MagmaMain => KikasaDomain.CoolTint(new(255, 120, 44), new(150, 176, 180));
        private static Color MagmaHot => KikasaDomain.CoolTint(new(255, 206, 140), new(196, 210, 210));
        private static Color SteamPale => KikasaDomain.CoolTint(new(216, 196, 190), new(188, 198, 202));

        /// <summary>连续量抖动的确定性相位（绘制路径不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            //不吃引擎地形碰撞：湖下真地形被湖面演出盖住，撞上去像凭空截停；
            //贴壁爆溅改走 AI 内手动检测（只认水线以上的真地形）
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>是否撞上"看得见"的真地形：湖线以下的墙体被湖面演出盖住，交给水漂/吞没判据</summary>
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
            life++;

            //低平抛物线：先直一小截，随后被自身重量按下去
            if (life > GravityDelay) {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.26f, 17f);
            }
            Projectile.velocity *= 0.998f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            //跳后寻的：小转率贴向目标，规则各端一致
            if (homingLeft > 0) {
                homingLeft--;
                int idx = (int)TargetIndex;
                if (idx >= 0 && idx < Main.maxNPCs) {
                    NPC npc = Main.npc[idx];
                    if (npc?.active == true && npc.CanBeChasedBy(Projectile)) {
                        float speed = Projectile.velocity.Length();
                        float want = (npc.Center - Projectile.Center).ToRotation();
                        float cur = Projectile.velocity.ToRotation().AngleTowards(want, 0.05f);
                        Projectile.velocity = cur.ToRotationVector2() * speed;
                    }
                }
            }

            //已跳闩：远端可能靠同步包得知已跳，补上寻的窗；
            //不跳型（出膛即 ai0=1）也走这里，从出生就带同一段寻的
            if ((int)SkipState == 1 && !bounceSeen) {
                bounceSeen = true;
                if (homingLeft <= 0) {
                    homingLeft = HomingFrames;
                }
            }

            //飞行余烬：熔壳一路掉渣冒烟
            if (!Main.dedServ) {
                if (life % 2 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        Projectile.Center - Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(4f, 4f),
                        -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                        Color.Lerp(MagmaMain, MagmaHot, Main.rand.NextFloat(0.5f)),
                        Main.rand.NextFloat(0.5f, 0.95f))?.Configure(true, Main.rand.Next(10, 18));
                }
                if (life % 5 == 1) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center - Projectile.velocity * 0.3f,
                        Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.7f, 0.7f),
                        Main.rand.NextBool(3) ? MagmaDeep : KikasaEyeBloodShot.BloodDeep,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 22));
                }
                if (life % 6 == 3) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        Projectile.Center - Projectile.velocity * 0.6f,
                        new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.3f, 0.7f)),
                        MagmaDark * 0.6f, Main.rand.NextFloat(0.4f, 0.65f))
                        ?.Configure(Main.rand.Next(26, 44));
                }
            }

            float glow = 0.6f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.62f * glow, 0.3f * glow, 0.1f * glow);

            //贴壁爆溅（机制身份保留）：手动地形检测替代 tileCollide
            if (life > 3 && TouchingVisibleTile()) {
                burstDone = true;
                MagmaBurst(Projectile.Center, Projectile.velocity, onTile: true);
                Projectile.Kill();
                return;
            }

            //水面判据：读 owner 领域的水线，各端同规则
            Player owner = Main.player[Projectile.owner];
            if (owner?.active != true
                || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                || !domain.AnyActive || domain.RiseT <= 0.5f) {
                return;
            }
            float lakeY = domain.LakeWorldY;
            if (Projectile.velocity.Y <= 0f || Projectile.Center.Y < lakeY - 2f) {
                return;
            }

            if ((int)SkipState == 0) {
                Bounce(domain, lakeY);
            }
            else {
                Swallow(domain, lakeY);
            }
        }

        /// <summary>打水漂：第一跳压低角度弹起，激起一线蒸汽白雾</summary>
        private void Bounce(KikasaDomainPlayer domain, float lakeY) {
            SkipState = 1;
            bounceSeen = true;
            homingLeft = HomingFrames;
            //压低弹角：往上弹得克制，水平劲头反而更足
            Projectile.velocity.Y = -MathHelper.Clamp(MathF.Abs(Projectile.velocity.Y) * 0.5f, 4.2f, 7f);
            Projectile.velocity.X *= 1.1f;
            Projectile.Center = new Vector2(Projectile.Center.X, lakeY - 4f);
            //owner 盖章：服务器没有领域水线，弹跳后的弹道靠这份包纠偏
            Projectile.netUpdate = Main.myPlayer == Projectile.owner;

            SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.7f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = 0.35f, MaxInstances = 3 }, Projectile.Center);

            if (Main.dedServ) {
                return;
            }
            Vector2 hit = new(Projectile.Center.X, lakeY);
            float dir = MathF.Sign(Projectile.velocity.X);
            if (KikasaDomain.Viewed == domain) {
                KikasaDomainDeco.RippleAt(hit, 1.0f);
                KikasaDomainDeco.SplashAt(hit, 5);
                Main.LocalPlayer?.CWR()?.GetScreenShake(1f);
            }
            //一线蒸汽白雾：沿前进方向铺一排贴水的热气
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(dir * (10f + i * 16f), -4f),
                    new Vector2(dir * 0.3f, -Main.rand.NextFloat(0.5f, 1.0f)),
                    SteamPale * (0.6f - i * 0.08f), Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(Main.rand.Next(26, 46));
            }
            //低平水花扇：贴着水皮向前撇
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(dir * Main.rand.NextFloat(0f, 14f), -2f),
                    new Vector2(dir * Main.rand.NextFloat(1.5f, 4f), -Main.rand.NextFloat(1f, 2.6f)),
                    SteamPale * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(Main.rand.Next(14, 26), 0f);
            }
        }

        /// <summary>第二次触水：湖把烧红的石头收走，一口白汽</summary>
        private void Swallow(KikasaDomainPlayer domain, float lakeY) {
            lakeSwallowed = true;
            SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
            if (!Main.dedServ) {
                Vector2 hit = new(Projectile.Center.X, lakeY);
                if (KikasaDomain.Viewed == domain) {
                    KikasaDomainDeco.RippleAt(hit, 0.7f);
                    KikasaDomainDeco.SplashAt(hit, 4);
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        hit + new Vector2(Main.rand.NextFloat(-10f, 10f), -4f),
                        new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.6f, 1.2f)),
                        SteamPale * 0.6f, Main.rand.NextFloat(0.5f, 0.8f))
                        ?.Configure(Main.rand.Next(30, 50));
                }
            }
            Projectile.Kill();
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中的额外燎痕（OnHit 只在 owner 端跑，主爆在 OnKill 各端可见）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    target.Center + Main.rand.NextVector2Circular(16f, 16f),
                    Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(2f, 2f),
                    MagmaMain, Main.rand.NextFloat(0.6f, 1f))?.Configure(true, Main.rand.Next(14, 22));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            if (!burstDone) {
                //命中 NPC / 超时坠灭共用（penetrate=1，Kill 各端都跑，队友也看得见）
                MagmaBurst(Projectile.Center, Projectile.velocity, onTile: false);
            }
        }

        /// <summary>岩浆血溅：半球溅射的熔珠与血团 + 扩散环 + 烟柱 + 余烬滞留</summary>
        private static void MagmaBurst(Vector2 pos, Vector2 impactVel, bool onTile) {
            if (Main.dedServ) {
                return;
            }
            Vector2 normal = -impactVel.SafeNormalize(Vector2.UnitY);
            float ke = MathHelper.Clamp(impactVel.Length() / 18f, 0.4f, 1f);
            float mainAngle = normal.ToRotation();

            //熔珠半球扇：贴法线的最快
            int count = (int)(8 + 6 * ke);
            for (int i = 0; i < count; i++) {
                float spread = Main.rand.NextFloat(-MathHelper.PiOver2, MathHelper.PiOver2);
                float speedRatio = 1f - MathF.Abs(spread) / MathHelper.PiOver2;
                Vector2 vel = (mainAngle + spread).ToRotationVector2()
                    * Main.rand.NextFloat(2.5f, 8f) * (0.4f + 0.6f * speedRatio) * (0.5f + ke);
                PRTLoader.NewParticle<PRT_Spark>(pos + Main.rand.NextVector2Circular(5f, 5f),
                    vel, Color.Lerp(MagmaMain, MagmaHot, Main.rand.NextFloat(0.6f)),
                    Main.rand.NextFloat(0.7f, 1.3f))?.Configure(true, Main.rand.Next(18, 30));
            }
            //血团裹在熔溅里：这是血湖借出去的血
            for (int i = 0; i < 6; i++) {
                Vector2 vel = normal.RotatedByRandom(0.8f) * Main.rand.NextFloat(1.8f, 5f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos, vel,
                    Main.rand.NextBool(3) ? KikasaEyeBloodShot.BloodDeep : KikasaEyeBloodShot.BloodMain,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(22, 38), 0.4f);
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, MagmaDeep, 0.09f)
                ?.Configure(new Vector2(0.7f, 1f), mainAngle, 0.26f + 0.16f * ke, 9);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    pos + Main.rand.NextVector2Circular(8f, 8f),
                    normal * 0.4f + new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.7f)),
                    MagmaDark * 0.7f, Main.rand.NextFloat(0.6f, 0.9f))
                    ?.Configure(Main.rand.Next(50, 80));
            }
            //原版火尘垫底
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Torch,
                    normal.RotatedByRandom(1f) * Main.rand.NextFloat(1.5f, 4f), 0, default, Main.rand.NextFloat(1.1f, 1.7f));
                d.noGravity = Main.rand.NextBool();
            }
            //贴壁留一摊会滴淌的岩浆血渍
            if (onTile) {
                PRTLoader.NewParticle<PRT_KikasaBloodSmear>(pos + normal * 2f, Vector2.Zero,
                    MagmaDeep, Main.rand.NextFloat(0.8f, 1.1f))?.Configure(Main.rand.Next(80, 120));
            }

            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Volume = 0.8f, Pitch = -0.1f, MaxInstances = 3 }, pos);
            SoundEngine.PlaySound(SoundID.NPCHit3 with { Volume = 0.4f, Pitch = -0.35f, MaxInstances = 3 }, pos);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || VisualFade <= 0.01f) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.3f - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float rotation = Projectile.rotation + MathHelper.PiOver2;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0.1f, 0.7f);

            //熔壳呼吸：内压顶着壳在鼓
            float wob = MathF.Sin(life * 0.5f + Seed * 6f) * 0.1f;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.8f);
            SpriteBatch sb = Main.spriteBatch;

            //熔壳暗缘
            sb.Draw(tex, pos, null, MagmaDark * (0.9f * fade), rotation, origin,
                new Vector2(0.56f, 0.6f + stretch * 0.8f) * jiggle, SpriteEffects.None, 0f);
            //岩浆主体
            sb.Draw(tex, pos, null, MagmaDeep * fade, rotation, origin,
                new Vector2(0.44f, 0.5f + stretch * 0.7f) * jiggle, SpriteEffects.None, 0f);
            //炽热内芯：加色小面积
            Color core = MagmaMain with { A = 0 };
            sb.Draw(tex, pos, null, core * (0.85f * fade), rotation, origin,
                new Vector2(0.26f, 0.34f + stretch * 0.4f) * jiggle, SpriteEffects.None, 0f);
            Color white = MagmaHot with { A = 0 };
            sb.Draw(tex, pos, null, white * (0.55f * fade), rotation, origin,
                new Vector2(0.12f, 0.2f + stretch * 0.2f) * jiggle, SpriteEffects.None, 0f);

            return false;
        }
    }
}
