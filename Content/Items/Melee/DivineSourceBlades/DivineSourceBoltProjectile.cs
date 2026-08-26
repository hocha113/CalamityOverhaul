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
    /// 金源科技光矢，挥砍时按拍数递增发射。
    /// ai[0] 充能标记(0/1)，充能弹更大、更快、追踪更强、带金色
    /// </summary>
    internal class DivineSourceBoltProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int HomingDelay = 8;
        /// <summary>常规判定箱边长，充能态按 SizeMul 首帧 Resize</summary>
        private const int BaseHitbox = 20;

        private bool Empowered => Projectile.ai[0] > 0.5f;
        /// <summary>充能态整体放大一档，判定与各绘制层同源</summary>
        private float SizeMul => Empowered ? 1.5f : 1f;
        private float MaxSpeed => Empowered ? 22f : 18f;
        private float TurnRate => Empowered ? 0.13f : 0.06f;
        private float SeekRange => Empowered ? 1100f : 800f;

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
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            //首帧按充能标记撑大判定箱(Resize 保持中心)，ai[0] 在 SetDefaults 时还没写入
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Empowered) {
                    int size = (int)(BaseHitbox * SizeMul);
                    Projectile.Resize(size, size);
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            //复利续力，飞行期速度持续攀升
            float speed = Projectile.velocity.Length();
            if (speed < MaxSpeed) {
                Projectile.velocity *= 1.024f;
            }

            //轻微追踪，限转率朝最近目标弯
            if (Projectile.timeLeft < 150 - HomingDelay) {
                NPC target = FindTarget();
                if (target != null) {
                    float aim = (target.Center - Projectile.Center).ToRotation();
                    float next = Projectile.velocity.ToRotation().AngleTowards(aim, TurnRate);
                    Projectile.velocity = next.ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            if (!VaultUtils.isServer) {
                Lighting.AddLight(Projectile.Center,
                    (Empowered ? new Vector3(0.5f, 0.45f, 0.28f) : new Vector3(0.16f, 0.36f, 0.6f)));
                //沿途甩数据屑，速度越快甩得越勤
                int shedGap = speed > 15f ? 3 : 5;
                if (Projectile.timeLeft % shedGap == 0) {
                    bool gold = Empowered && Main.rand.NextBool(3);
                    PRTLoader.NewParticle<PRT_CyberSquare>(
                        Projectile.Center + Main.rand.NextVector2Circular(5f, 5f) * SizeMul,
                        -Projectile.velocity * 0.06f,
                        gold ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright,
                        Main.rand.NextFloat(0.55f, 0.9f) * SizeMul)
                        .Configure(gold ? DivineSourceBladeFX.AuricAmber : DivineSourceBladeFX.AzureBlue,
                            Main.rand.Next(12, 18));
                }
                //充能期两粒金屑绕弹体螺旋伴飞，半径随弹体放大
                if (Empowered && Projectile.timeLeft % 4 == 0) {
                    float orbit = Projectile.timeLeft * 0.55f;
                    for (int s = 0; s < 2; s++) {
                        Vector2 at = Projectile.Center
                            + (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2()
                            * MathF.Sin(orbit + s * MathHelper.Pi) * 15f * SizeMul;
                        PRTLoader.NewParticle<PRT_CyberSquare>(at, Projectile.velocity * 0.85f,
                            DivineSourceBladeFX.AuricGold, Main.rand.NextFloat(0.4f, 0.6f) * SizeMul)
                            .Configure(DivineSourceBladeFX.AuricAmber, Main.rand.Next(8, 13));
                    }
                }
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
            //光矢命中喂一小口充能
            Main.player[Projectile.owner].GetModPlayer<DivineSourcePlayer>().AddCharge(0.008f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //余痕比弹体活得久
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 4; i++) {
                bool gold = Empowered && Main.rand.NextBool(2);
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center,
                    dir.RotatedByRandom(1.1f) * Main.rand.NextFloat(1.5f, 4f),
                    gold ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright,
                    Main.rand.NextFloat(0.5f, 0.85f))
                    .Configure(gold ? DivineSourceBladeFX.AuricAmber : DivineSourceBladeFX.AzureBlue,
                        Main.rand.Next(14, 22));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_DivineTechTriangle>(Projectile.Center,
                    dir.RotatedByRandom(0.8f) * Main.rand.NextFloat(2f, 5f),
                    Empowered ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright,
                    Main.rand.NextFloat(0.06f, 0.11f))
                    .Configure(DivineSourceBladeFX.AzureBlue, Main.rand.Next(16, 24));
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                Empowered ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright, 0f)
                .Configure(Empowered ? 0.05f : 0.03f, Empowered ? 0.42f : 0.28f, Empowered ? 14 : 10);
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
            float bodyLen = MathHelper.Clamp(speed * 5.1f, 51f, 111f) * sizeMul;
            Color core = Empowered ? DivineSourceBladeFX.AuricCream : DivineSourceBladeFX.TechWhite;
            Color body = Empowered
                ? DivineSourceBladeFX.Blend(DivineSourceBladeFX.CyanBright, DivineSourceBladeFX.AuricGold, 0.55f)
                : DivineSourceBladeFX.CyanBright;
            Color halo = Empowered
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
                Vector2 oldCenter = oldPos + Projectile.Size * 0.5f - Main.screenPosition;
                float t = 1f - i / 10f;
                Color ghost = halo * (0.32f * t);
                ghost.A = 0;
                Main.EntitySpriteDraw(shot, oldCenter, null, ghost, Projectile.rotation,
                    shot.Size() * 0.5f,
                    new Vector2(bodyLen * (0.5f + 0.4f * t) / shot.Width, 13f * t * sizeMul / shot.Height),
                    SpriteEffects.None, 0);
            }

            //底辉
            Color haloCol = halo * 0.5f;
            haloCol.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, haloCol, 0f,
                glow.Size() * 0.5f, 0.75f * sizeMul, SpriteEffects.None, 0);

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

            //充能期弹头挂一枚旋转星芒
            if (Empowered) {
                Texture2D star = DivineSourceBladeFX.BlankStar;
                if (star != null) {
                    float time = (float)Main.timeForVisualEffects * 0.05f;
                    Vector2 headPos = drawPos + Projectile.velocity.SafeNormalize(Vector2.UnitX) * (bodyLen * 0.32f);
                    Color starCol = DivineSourceBladeFX.AuricGold * 0.6f;
                    starCol.A = 0;
                    Main.EntitySpriteDraw(star, headPos, null, starCol, time * 2.2f,
                        star.Size() * 0.5f, 0.1f * sizeMul, SpriteEffects.None, 0);
                    Color starCore = DivineSourceBladeFX.AuricCream * 0.45f;
                    starCore.A = 0;
                    Main.EntitySpriteDraw(star, headPos, null, starCore, -time * 1.5f,
                        star.Size() * 0.5f, 0.06f * sizeMul, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
