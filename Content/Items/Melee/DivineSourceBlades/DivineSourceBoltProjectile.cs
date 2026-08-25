using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DivineSourceBlades
{
    /// <summary>
    /// 金源科技光矢，挥砍时按拍数递增发射。
    /// ai[0] 充能标记(0/1)，充能弹更快、追踪更强、带金色
    /// </summary>
    internal class DivineSourceBoltProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int HomingDelay = 10;

        private bool Empowered => Projectile.ai[0] > 0.5f;
        private float MaxSpeed => Empowered ? 22f : 18f;
        private float TurnRate => Empowered ? 0.085f : 0.035f;
        private float SeekRange => Empowered ? 900f : 620f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
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
                        Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                        -Projectile.velocity * 0.06f,
                        gold ? DivineSourceBladeFX.AuricGold : DivineSourceBladeFX.CyanBright,
                        Main.rand.NextFloat(0.42f, 0.7f))
                        .Configure(gold ? DivineSourceBladeFX.AuricAmber : DivineSourceBladeFX.AzureBlue,
                            Main.rand.Next(12, 18));
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
                .Configure(0.03f, 0.24f, 10);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D shot = DivineSourceBladeFX.LightShot;
            Texture2D glow = DivineSourceBladeFX.SoftGlow;
            if (shot == null || glow == null) {
                return false;
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();
            float bodyLen = MathHelper.Clamp(speed * 3.4f, 34f, 74f);
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
                    shot.Size() * 0.5f, new Vector2(bodyLen * (0.5f + 0.4f * t) / shot.Width, 9f * t / shot.Height),
                    SpriteEffects.None, 0);
            }

            //底辉
            Color haloCol = halo * 0.5f;
            haloCol.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, haloCol, 0f,
                glow.Size() * 0.5f, 0.5f, SpriteEffects.None, 0);

            //主体与白热芯
            Color bodyCol = body * 0.95f;
            bodyCol.A = 0;
            Main.EntitySpriteDraw(shot, drawPos, null, bodyCol, Projectile.rotation,
                shot.Size() * 0.5f, new Vector2(bodyLen / shot.Width, 15f / shot.Height), SpriteEffects.None, 0);
            Color coreCol = core * 0.95f;
            coreCol.A = 0;
            Main.EntitySpriteDraw(shot, drawPos, null, coreCol, Projectile.rotation,
                shot.Size() * 0.5f, new Vector2(bodyLen * 0.62f / shot.Width, 6f / shot.Height), SpriteEffects.None, 0);
            return false;
        }
    }
}
