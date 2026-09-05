using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DivineSourceBlades
{
    /// <summary>
    /// 金源科技光矢与光枪，随挥砍沿刀路分批离手。
    /// ai[0] 档位(0 常态光矢 / 1 充能光枪，更大更重、可贯穿、金色)，
    /// ai[1] 蛇行幅度(有符号，弧度/帧，0 直飞)，ai[2] 绽放帧数(先减速外散再追踪，0 走默认延迟)
    /// </summary>
    internal class DivineSourceBoltProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 150;
        private const int HomingDelay = 8;
        /// <summary>常态判定箱边长，各档按 SizeMul 首帧 Resize</summary>
        private const int BaseHitbox = 20;
        /// <summary>蛇行角频率，周期约 28 帧；发射端用它反推对偶弹的初始航向偏置</summary>
        public const float WeaveFreq = 0.22f;
        /// <summary>蛇行在该帧数内衰减归零，末段直扑目标</summary>
        private const float WeaveDecayFrames = 55f;
        /// <summary>绽放期每帧速度衰减</summary>
        private const float BloomDrag = 0.9f;

        private bool IsLance => Projectile.ai[0] > 0.5f;
        private float Weave => Projectile.ai[1];
        private int BloomFrames => (int)Projectile.ai[2];

        /// <summary>整体尺寸，判定与各绘制层同源</summary>
        private float SizeMul => IsLance ? 2.6f : 1.25f;
        private float MaxSpeed => IsLance ? 27f : 22f;
        private float Accel => IsLance ? 0.34f : 0.3f;
        private float TurnRate => IsLance ? 0.075f : 0.13f;
        private float SeekRange => IsLance ? 1300f : 1100f;

        private int Age => Lifetime - Projectile.timeLeft;
        private int HomingStart => BloomFrames > 0 ? BloomFrames : HomingDelay;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = BaseHitbox;
            Projectile.height = BaseHitbox;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            //首帧按档位定型，ai 在 SetDefaults 时还没写入；放在 AI 里各端都跑，出膛特效旁观者也看得到
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                InitTier();
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            int age = Age;
            float speed = Projectile.velocity.Length();
            if (age < BloomFrames) {
                //绽放期减速外散，等追踪把整圈一齐拽回
                Projectile.velocity *= BloomDrag;
            }
            else if (speed < MaxSpeed) {
                //复利加定量续力，越飞越快，慢启动的绽放弹也追得上
                speed = Math.Min(MaxSpeed, (speed * 1.024f) + Accel);
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * speed;
            }

            //蛇行: 航向按正弦摆动，幅度随时间衰减；对偶弹反号便绕瞄准线交织成螺旋
            if (Weave != 0f && age < WeaveDecayFrames) {
                float amp = Weave * (1f - (age / WeaveDecayFrames));
                Projectile.velocity = Projectile.velocity.RotatedBy(amp * MathF.Sin(age * WeaveFreq));
            }

            //限转率朝最近目标弯，光枪更重、转得慢但看得远
            if (age >= HomingStart) {
                NPC target = FindTarget();
                if (target != null) {
                    float aim = (target.Center - Projectile.Center).ToRotation();
                    float next = Projectile.velocity.ToRotation().AngleTowards(aim, TurnRate);
                    Projectile.velocity = next.ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            if (VaultUtils.isServer) {
                return;
            }

            float sizeMul = SizeMul;
            bool lance = IsLance;
            Lighting.AddLight(Projectile.Center,
                lance ? new Vector3(0.6f, 0.52f, 0.3f) : new Vector3(0.16f, 0.36f, 0.6f));

            //沿途甩数据屑，速度越快甩得越勤
            int shedGap = speed > 15f ? 3 : 5;
            if (Projectile.timeLeft % shedGap == 0) {
                bool gold = lance && Main.rand.NextBool(3);
                PRTLoader.NewParticle<PRT_CyberSquare>(
                    Projectile.Center + (Main.rand.NextVector2Circular(5f, 5f) * sizeMul),
                    -Projectile.velocity * 0.06f,
                    gold ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright,
                    Main.rand.NextFloat(0.55f, 0.9f) * sizeMul)
                    .Configure(gold ? DivineSourceBladeFX.AuricAmber : DivineSourceBladeFX.AzureBlue,
                        Main.rand.Next(12, 18));
            }
            //光枪两粒金屑绕枪身螺旋伴飞，半径随枪体放大
            if (lance && Projectile.timeLeft % 4 == 0) {
                float orbit = Projectile.timeLeft * 0.55f;
                for (int s = 0; s < 2; s++) {
                    Vector2 at = Projectile.Center
                        + ((Projectile.rotation + MathHelper.PiOver2).ToRotationVector2()
                        * MathF.Sin(orbit + (s * MathHelper.Pi)) * 15f * sizeMul);
                    PRTLoader.NewParticle<PRT_CyberSquare>(at, Projectile.velocity * 0.85f,
                        DivineSourceBladeFX.AuricGold, Main.rand.NextFloat(0.4f, 0.6f) * sizeMul)
                        .Configure(DivineSourceBladeFX.AuricAmber, Main.rand.Next(8, 13));
                }
            }
        }

        private void InitTier() {
            int size = (int)(BaseHitbox * SizeMul);
            Projectile.Resize(size, size);
            if (IsLance) {
                //光枪贯穿三名敌人，同一目标只吃一次
                Projectile.penetrate = 3;
                Projectile.usesLocalNPCImmunity = true;
                Projectile.localNPCHitCooldown = -1;
            }

            if (VaultUtils.isServer) {
                return;
            }
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            if (IsLance) {
                //出膛金环炸开，沿枪身方向甩一撮金三角
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                    DivineSourceBladeFX.AuricGold, 0f).Configure(0.04f, 0.55f, 12);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_DivineTechTriangle>(Projectile.Center,
                        forward.RotatedByRandom(0.5) * Main.rand.NextFloat(3f, 7f),
                        DivineSourceBladeFX.AuricGold, Main.rand.NextFloat(0.07f, 0.12f))
                        .Configure(DivineSourceBladeFX.AuricAmber, Main.rand.Next(14, 22));
                }
                return;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center,
                    forward.RotatedByRandom(0.7) * Main.rand.NextFloat(1.5f, 3.5f),
                    DivineSourceBladeFX.CyanBright, Main.rand.NextFloat(0.4f, 0.7f))
                    .Configure(DivineSourceBladeFX.AzureBlue, Main.rand.Next(10, 16));
            }
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = SeekRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中喂充能，光枪一口更大
            Main.player[Projectile.owner].GetModPlayer<DivineSourcePlayer>().AddCharge(IsLance ? 0.03f : 0.013f);
            if (!IsLance) {
                return;
            }

            //贯穿逐段衰减
            Projectile.damage = (int)(Projectile.damage * 0.8f);
            if (Projectile.owner == Main.myPlayer) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<DivineSourceHitFXProjectile>(), 0, 0f, Projectile.owner,
                    ai0: 0.95f, ai1: 1f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            bool lance = IsLance;
            //余痕比弹体活得久
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            int shards = lance ? 7 : 4;
            for (int i = 0; i < shards; i++) {
                bool gold = lance && Main.rand.NextBool(2);
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center,
                    dir.RotatedByRandom(1.1f) * Main.rand.NextFloat(1.5f, 4f),
                    gold ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright,
                    Main.rand.NextFloat(0.5f, 0.85f))
                    .Configure(gold ? DivineSourceBladeFX.AuricAmber : DivineSourceBladeFX.AzureBlue,
                        Main.rand.Next(14, 22));
            }
            int tris = lance ? 4 : 2;
            for (int i = 0; i < tris; i++) {
                PRTLoader.NewParticle<PRT_DivineTechTriangle>(Projectile.Center,
                    dir.RotatedByRandom(0.8f) * Main.rand.NextFloat(2f, 5f),
                    lance ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright,
                    Main.rand.NextFloat(0.06f, 0.11f))
                    .Configure(DivineSourceBladeFX.AzureBlue, Main.rand.Next(16, 24));
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                lance ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright, 0f)
                .Configure(lance ? 0.06f : 0.03f, lance ? 0.7f : 0.3f, lance ? 16 : 10);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D shot = DivineSourceBladeFX.LightShot;
            Texture2D glow = DivineSourceBladeFX.SoftGlow;
            if (shot == null || glow == null) {
                return false;
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();
            float sizeMul = SizeMul;
            bool lance = IsLance;
            float bodyLen = MathHelper.Clamp(speed * 5.1f, 51f, 111f) * sizeMul;
            Color core = lance ? DivineSourceBladeFX.AuricCream : DivineSourceBladeFX.TechWhite;
            Color body = lance
                ? DivineSourceBladeFX.Blend(DivineSourceBladeFX.CyanBright, DivineSourceBladeFX.AuricGold, 0.55f)
                : DivineSourceBladeFX.CyanBright;
            Color halo = lance
                ? DivineSourceBladeFX.Blend(DivineSourceBladeFX.AzureBlue, DivineSourceBladeFX.AuricAmber, 0.4f)
                : DivineSourceBladeFX.AzureBlue;

            //拖尾残段，速度拉伸的旧位置段带
            for (int i = 8; i >= 2; i -= 2) {
                if (i >= Projectile.oldPos.Length) {
                    continue;
                }
                Vector2 oldPos = Projectile.oldPos[i];
                if (oldPos == Vector2.Zero) {
                    continue;
                }
                Vector2 oldCenter = oldPos + (Projectile.Size * 0.5f) - Main.screenPosition;
                float t = 1f - (i / 10f);
                Color ghost = halo * (0.32f * t);
                ghost.A = 0;
                Main.EntitySpriteDraw(shot, oldCenter, null, ghost, Projectile.rotation,
                    shot.Size() * 0.5f,
                    new Vector2(bodyLen * (0.5f + (0.4f * t)) / shot.Width, 13f * t * sizeMul / shot.Height),
                    SpriteEffects.None, 0);
            }

            //底辉，光枪按枪身比例收一点免得糊成球
            Color haloCol = halo * 0.5f;
            haloCol.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, haloCol, 0f,
                glow.Size() * 0.5f, (lance ? 0.6f : 0.75f) * sizeMul, SpriteEffects.None, 0);

            //主体与白热芯
            Color bodyCol = body * 0.95f;
            bodyCol.A = 0;
            Main.EntitySpriteDraw(shot, drawPos, null, bodyCol, Projectile.rotation,
                shot.Size() * 0.5f, new Vector2(bodyLen / shot.Width, 22f * sizeMul / shot.Height),
                SpriteEffects.None, 0);
            Color coreCol = core * 0.95f;
            coreCol.A = 0;
            Main.EntitySpriteDraw(shot, drawPos, null, coreCol, Projectile.rotation,
                shot.Size() * 0.5f, new Vector2(bodyLen * 0.62f / shot.Width, 9f * sizeMul / shot.Height),
                SpriteEffects.None, 0);

            if (!lance) {
                return false;
            }

            //枪脊: 一道更长更细的奶金亮线前后探出枪身，读作矛而非胖弹
            Color spine = DivineSourceBladeFX.AuricCream * 0.7f;
            spine.A = 0;
            Main.EntitySpriteDraw(shot, drawPos, null, spine, Projectile.rotation,
                shot.Size() * 0.5f, new Vector2(bodyLen * 1.35f / shot.Width, 5f / shot.Height),
                SpriteEffects.None, 0);

            //枪头双层反向旋转星芒
            Texture2D star = DivineSourceBladeFX.BlankStar;
            if (star != null) {
                float time = (float)Main.timeForVisualEffects * 0.05f;
                Vector2 headPos = drawPos + (Projectile.velocity.SafeNormalize(Vector2.UnitX) * (bodyLen * 0.32f));
                Color starCol = DivineSourceBladeFX.AuricGold * 0.6f;
                starCol.A = 0;
                Main.EntitySpriteDraw(star, headPos, null, starCol, time * 2.2f,
                    star.Size() * 0.5f, 0.1f * sizeMul, SpriteEffects.None, 0);
                Color starCore = DivineSourceBladeFX.AuricCream * 0.45f;
                starCore.A = 0;
                Main.EntitySpriteDraw(star, headPos, null, starCore, (-time * 1.5f) + MathHelper.PiOver4,
                    star.Size() * 0.5f, 0.06f * sizeMul, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
