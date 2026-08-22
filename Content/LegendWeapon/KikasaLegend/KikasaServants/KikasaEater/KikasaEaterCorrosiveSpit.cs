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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEater
{
    /// <summary>
    /// 鬼奴世界吞噬怪的腐蚀血痰：一口沸着蚀泡的粘稠血团，飞行中不断有
    /// 小泡从团面炸开、掉出速度拉伸的碎珠。命中 NPC / 贴壁 / 砸上血湖面
    /// 都爆成滞留腐蚀血雾（<see cref="KikasaEaterCorrosionMist"/>）
    /// 与克眼血痰被湖收走的语义刻意相反：蚀液碰上湖水是沸炸不是回收。
    /// 弹体只在 owner 端生成，spawn 参数自带全部初值；血雾只由 owner 端补生
    /// </summary>
    internal class KikasaEaterCorrosiveSpit : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>出膛后多少帧开始吃重力：痰是抛出去的，不是射出去的</summary>
        private const int GravityDelay = 6;

        private ref float Life => ref Projectile.ai[0];

        //贴壁/命中已放过爆雾，OnKill 不再补
        private bool burstDone;
        //砸上湖面：血雾贴水生成
        private bool lakeBurst;

        private static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));

        /// <summary>连续量抖动的确定性相位（绘制路径不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            //不吃引擎地形碰撞：湖下真地形被湖面演出盖住，撞上去像凭空截停；
            //贴壁爆雾改走 AI 内手动检测（只认水线以上的真地形）
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            //抛物线：短暂平直后被重量拽下去，粘性阻力缓慢泄劲
            if (Life > GravityDelay) {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.26f, 16f);
            }
            Projectile.velocity *= 0.995f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //蚀泡沸腾：团面上小泡炸开，横向溅碎珠，偶尔一粒蚀紫
            if (!Main.dedServ && Life % 3 == 0) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Vector2 side = dir.RotatedBy(MathHelper.PiOver2);
                Vector2 pos = Projectile.Center - dir * Main.rand.NextFloat(2f, 10f)
                    + side * Main.rand.NextFloat(-6f, 6f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos,
                    Projectile.velocity * Main.rand.NextFloat(0.15f, 0.4f)
                        + side * Main.rand.NextFloat(-1.6f, 1.6f)
                        - dir * Main.rand.NextFloat(0f, 1f),
                    Main.rand.NextBool(4) ? KikasaEaterServant.CorrodePurple : BloodMain,
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(14, 24));
            }

            float glow = 0.4f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.42f * glow, 0.10f * glow, 0.16f * glow);

            //砸上血湖面：蚀液把湖面炸沸，血雾贴水滞留，不被湖收走
            Player owner = Main.player[Projectile.owner];
            bool lakeAlive = owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f;
            KikasaDomainPlayer kdp = lakeAlive ? owner.GetModPlayer<KikasaDomainPlayer>() : null;
            if (lakeAlive
                && Projectile.velocity.Y > 0f
                && Projectile.Center.Y >= kdp.LakeWorldY - 2f) {
                lakeBurst = true;
                if (!Main.dedServ && KikasaDomain.Viewed == kdp) {
                    Vector2 hit = new(Projectile.Center.X, kdp.LakeWorldY);
                    KikasaDomainDeco.RippleAt(hit, 1.1f);
                    KikasaDomainDeco.SplashAt(hit, 6);
                }
                Projectile.Kill();
                return;
            }

            //贴壁爆雾（机制身份保留）：手动地形检测替代 tileCollide
            //只认水线以上的真地形，湖线以下的墙体被湖面盖着，交给上面的落湖沸炸
            if (Life > 3
                && (!lakeAlive || Projectile.Center.Y < kdp.LakeWorldY - 2f)
                && Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                burstDone = true;
                SplashBurst(Projectile.Center, Projectile.velocity, onTile: true);
                SpawnMist(Projectile.Center);
                Projectile.Kill();
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (lakeBurst) {
                //贴水爆雾：雾坐在水面上滚
                Vector2 surface = Projectile.Center;
                if (Main.player[Projectile.owner].TryGetModPlayer(out KikasaDomainPlayer domain)) {
                    surface.Y = domain.LakeWorldY - 24f;
                }
                if (!Main.dedServ) {
                    SplashBurst(surface, Projectile.velocity, onTile: false);
                }
                SpawnMist(surface);
                return;
            }
            if (!burstDone) {
                //命中 NPC / 超时坠灭共用（penetrate=1，Kill 各端都跑）
                if (!Main.dedServ) {
                    SplashBurst(Projectile.Center, Projectile.velocity, onTile: false);
                }
                SpawnMist(Projectile.Center);
            }
        }

        /// <summary>滞留腐蚀血雾只由 owner 端补生，spawn 参数自带全部初值</summary>
        private void SpawnMist(Vector2 pos) {
            if (Main.myPlayer != Projectile.owner) {
                return;
            }
            int damage = (int)(Projectile.damage * 0.38f);
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<KikasaEaterCorrosionMist>(), Math.Max(damage, 1), 0f,
                Projectile.owner, lakeBurst ? 1f : 0f);
        }

        /// <summary>爆浆：半球血珠扇 + 蚀紫飞沫 + 扩散环；贴壁再留渍斑</summary>
        private static void SplashBurst(Vector2 pos, Vector2 impactVel, bool onTile) {
            if (Main.dedServ) {
                return;
            }
            Vector2 normal = -impactVel.SafeNormalize(Vector2.UnitY);
            float ke = MathHelper.Clamp(impactVel.Length() / 18f, 0.3f, 1f);
            float mainAngle = normal.ToRotation();

            int count = (int)(6 + 4 * ke);
            for (int i = 0; i < count; i++) {
                float spread = Main.rand.NextFloat(-MathHelper.PiOver2, MathHelper.PiOver2);
                float speedRatio = 1f - MathF.Abs(spread) / MathHelper.PiOver2;
                Vector2 vel = (mainAngle + spread).ToRotationVector2()
                    * Main.rand.NextFloat(2f, 7f) * (0.35f + 0.65f * speedRatio) * (0.5f + ke);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos + Main.rand.NextVector2Circular(5f, 5f),
                    vel, Main.rand.NextBool(4) ? KikasaEaterServant.CorrodePurple
                        : Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.4f, 0.75f))?.Configure(Main.rand.Next(18, 32));
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, BloodDeep, 0.08f)
                ?.Configure(new Vector2(0.7f, 1f), mainAngle, 0.22f + 0.16f * ke, 9);
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                    normal.RotatedByRandom(0.9f) * Main.rand.NextFloat(1.2f, 3.2f), 100, default, Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = Main.rand.NextBool();
            }
            if (onTile) {
                PRTLoader.NewParticle<PRT_KikasaBloodSmear>(pos + normal * 2f, Vector2.Zero, BloodMain,
                    Main.rand.NextFloat(0.8f, 1.05f))?.Configure(Main.rand.Next(80, 120));
            }

            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.4f, Pitch = -0.15f, MaxInstances = 3 }, pos);
            SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.35f, Pitch = 0.1f, MaxInstances = 3 }, pos);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            if (fade <= 0.01f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = tex.Size() * 0.5f;

            //短拖影：速度拉伸的旧位残团，尾端收小，痰有分量地飞
            Vector2[] oldPos = Projectile.oldPos;
            if (oldPos != null) {
                for (int k = oldPos.Length - 1; k >= 2; k -= 2) {
                    if (oldPos[k] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 gp = oldPos[k] + Projectile.Size * 0.5f - Main.screenPosition;
                    float fall = 1f - k / (float)oldPos.Length;
                    sb.Draw(tex, gp, null, BloodDeep * (0.3f * fall * fade), Projectile.rotation, origin,
                        new Vector2(0.22f, 0.3f) * (0.9f - k * 0.05f), SpriteEffects.None, 0f);
                }
            }

            //液团三层：暗血压边→血红主体→蚀紫沸芯；表面张力抖动 + 速度拉伸
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.35f - Main.screenPosition;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.034f, 0.15f, 0.85f);
            float wob = MathF.Sin(Life * 0.6f + Seed * 6f) * 0.13f;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.8f);

            sb.Draw(tex, pos, null, BloodDark * (0.85f * fade), Projectile.rotation, origin,
                new Vector2(0.46f, 0.5f + stretch * 0.8f) * jiggle, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, BloodMain * fade, Projectile.rotation, origin,
                new Vector2(0.36f, 0.42f + stretch * 0.7f) * jiggle, SpriteEffects.None, 0f);
            //沸芯：蚀紫小团在团心打转，读作里头有东西在烧
            float churn = MathF.Sin(Life * 0.9f + Seed * 3f);
            Vector2 churnOff = new(churn * 3f, MathF.Cos(Life * 0.7f + Seed) * 2.5f);
            sb.Draw(tex, pos + churnOff, null,
                (KikasaEaterServant.CorrodePurple with { A = 0 }) * (0.5f * fade),
                Projectile.rotation, origin,
                new Vector2(0.15f, 0.2f + stretch * 0.25f) * jiggle, SpriteEffects.None, 0f);
            //湿面反光
            Color glint = BloodBright with { A = 0 };
            sb.Draw(tex, pos, null, glint * (0.5f * fade), Projectile.rotation, origin,
                new Vector2(0.12f, 0.2f + stretch * 0.28f) * jiggle, SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>
    /// 滞留腐蚀血雾：血痰爆开后赖着不走的蚀骨雾团，胀开→滞留啃咬→散尽。
    /// ai[0]=1 表示贴湖面模式（雾坐在水线上滚、持续把水面煮沸）。
    /// 命中走圆形范围 + 低频跳伤；粒子帧内限量，音效稀疏门控
    /// </summary>
    internal class KikasaEaterCorrosionMist : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private const int GrowFrames = 16;
        private const int TotalLife = 110;
        private const int FadeFrames = 18;
        private const float MaxRadius = 104f;

        private ref float SurfaceMode => ref Projectile.ai[0];
        private ref float Life => ref Projectile.localAI[0];

        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        private float Seed => Projectile.identity * 0.7391f % 5.13f;

        /// <summary>当前雾团半径：胀开→滞留→散尽</summary>
        private float Radius {
            get {
                float grow = MathHelper.Clamp(Life / GrowFrames, 0f, 1f);
                grow = 1f - (1f - grow) * (1f - grow);
                float fade = MathHelper.Clamp((TotalLife - Life) / (float)FadeFrames, 0f, 1f);
                return MaxRadius * grow * (0.4f + 0.6f * fade);
            }
        }

        private float Opacity => MathHelper.Clamp(Life / GrowFrames, 0f, 1f)
            * MathHelper.Clamp((TotalLife - Life) / (float)FadeFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 26;
            Projectile.timeLeft = TotalLife + 6;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            Player owner = Main.player[Projectile.owner];
            bool onSurface = SurfaceMode == 1f && owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f;
            KikasaDomainPlayer kdp = onSurface ? owner.GetModPlayer<KikasaDomainPlayer>() : null;
            if (onSurface) {
                //贴水滚：雾底压着水线走
                Projectile.Center = new Vector2(Projectile.Center.X, kdp.LakeWorldY - 24f);
            }

            if (Life >= TotalLife) {
                Projectile.Kill();
                return;
            }

            Lighting.AddLight(Projectile.Center, 0.20f * Opacity, 0.05f * Opacity, 0.09f * Opacity);

            if (Main.dedServ) {
                return;
            }

            //雾体由 PRT 滚出来：帧内限量，胀开期更密
            bool growing = Life < GrowFrames * 2;
            if ((int)Life % (growing ? 3 : 6) == 0) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.7f, Radius * 0.55f);
                PRTLoader.NewParticle<PRT_GhostRainMist>(pos,
                    new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -Main.rand.NextFloat(0.15f, 0.45f)),
                    Color.Lerp(MistBlood, KikasaEaterServant.CorrodePurple, Main.rand.NextFloat(0.15f, 0.4f))
                        * (0.7f * Opacity),
                    Main.rand.NextFloat(0.5f, 0.85f))?.Configure(Main.rand.Next(40, 70));
            }
            //蚀泡上浮炸裂
            if ((int)Life % 4 == 1) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.8f, Radius * 0.6f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.6f, 1.5f)),
                    (Main.rand.NextBool(3) ? KikasaEaterServant.CorrodePurple : BloodDeep) * 0.6f,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(14, 24), -0.02f);
            }
            //贴水模式持续把水面煮沸
            if (onSurface && KikasaDomain.Viewed == kdp && (int)Life % 9 == 4) {
                Vector2 hit = new(Projectile.Center.X + Main.rand.NextFloat(-Radius, Radius) * 0.7f, kdp.LakeWorldY);
                KikasaDomainDeco.RippleAt(hit, Main.rand.NextFloat(0.25f, 0.5f));
            }
            //稀疏的蚀咬气泡声
            if ((int)Life % 30 == 8 && Main.rand.NextBool(2)) {
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.3f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
            }
        }

        /// <summary>雾成形后才咬人，散尽前松口</summary>
        public override bool? CanDamage() => Radius > MaxRadius * 0.42f ? null : false;

        /// <summary>圆形范围命中：目标矩形到雾心最近点距离判定</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 center = Projectile.Center;
            float nearX = MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right);
            float nearY = MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom);
            return Vector2.DistanceSquared(center, new Vector2(nearX, nearY)) <= Radius * Radius;
        }

        public override bool PreDraw(ref Color lightColor) {
            //雾团主体：三层错相滚动的暗雾盘 + 极低的蚀紫内芯，PRT 负责边缘呼吸
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float r = Radius / 96f;
            float t = Main.GlobalTimeWrappedHourly;

            for (int i = 0; i < 3; i++) {
                float ang = t * (0.25f + i * 0.11f) * (i % 2 == 0 ? 1f : -1f) + Seed + i * 2.1f;
                Vector2 off = new(MathF.Sin(ang) * 10f, MathF.Cos(ang * 0.8f) * 7f);
                float layerScale = r * (0.9f + i * 0.28f);
                Color c = Color.Lerp(MistBlood, BloodDeep, i * 0.3f) * (Opacity * (0.5f - i * 0.12f));
                sb.Draw(tex, pos + off, null, c, ang * 0.3f, origin,
                    new Vector2(layerScale * 1.25f, layerScale), SpriteEffects.None, 0f);
            }
            Color core = (KikasaEaterServant.CorrodePurple with { A = 0 }) * (0.22f * Opacity);
            sb.Draw(tex, pos, null, core, -t * 0.2f + Seed, origin,
                new Vector2(r * 0.55f, r * 0.45f), SpriteEffects.None, 0f);
            Color rim = (BloodMain with { A = 0 }) * (0.10f * Opacity);
            sb.Draw(tex, pos, null, rim, t * 0.14f + Seed, origin,
                new Vector2(r * 1.35f, r * 1.05f), SpriteEffects.None, 0f);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //蚀咬：目标身上冒蚀紫小泡
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(16f, 16f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.5f, 1.8f)),
                    Main.rand.NextBool(2) ? KikasaEaterServant.CorrodePurple : BloodDeep,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20), -0.01f);
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.3f, Pitch = -0.6f, MaxInstances = 2 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //散尽余韵：最后一口雾
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.2f), MistBlood * 0.55f, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(40, 60));
        }
    }
}
