using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 凶兆坍缩：鸦群啄出的不祥征兆在猎物身上聚拢成形。跟随目标，
    /// 三相 = 收羽 14 帧（六道影羽螺旋向心收束，无伤害）/ 爆鸣 6 帧
    /// （紫黑凶兆炸裂，伤害窗 + 暗影焰）/ 落羽 16 帧（余羽飘坠，无伤害）。
    /// ai[0] = 目标索引，ai[1] = 目标类型校验（随生成包过线）。
    /// 材质：夜鸦影羽 + 暗影焰紫，暗芯用真 alpha 压层
    /// </summary>
    internal class GsRavenOmenProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private static readonly Color OmenViolet = new(174, 96, 255);
        private static readonly Color ShadowInk = new(38, 22, 58);
        private static readonly Color FeatherGray = new(120, 108, 140);

        private const int GatherFrames = 14;
        private const int BurstFrames = 6;
        private const int DriftFrames = 16;
        private const int TotalFrames = GatherFrames + BurstFrames + DriftFrames;
        private const float BurstRadius = 70f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool InBurst => Elapsed >= GatherFrames && Elapsed < GatherFrames + BurstFrames;

        private bool Drifting => Elapsed >= GatherFrames + BurstFrames;

        private float Seed => Projectile.identity * 0.6659f % MathHelper.TwoPi;

        private NPC BoundTarget {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[idx];
                return npc.active && npc.type == (int)Projectile.ai[1] ? npc : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = TotalFrames;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            //收羽期跟住目标，爆鸣起锚定原地（死鸟不追尸）
            NPC target = BoundTarget;
            if (Elapsed < GatherFrames) {
                if (target == null) {
                    Projectile.Kill();
                    return;
                }
                Projectile.Center = target.Center;
            }
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center,
                OmenViolet.ToVector3() * (InBurst ? 0.6f : 0.25f));
            if (Elapsed == 1) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f, Pitch = -0.2f },
                    Projectile.Center);
            }
            //爆鸣首帧：凶兆炸裂 + 影羽四散
            if (Elapsed == GatherFrames) {
                SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.6f, Pitch = -0.4f },
                    Projectile.Center);
                for (int i = 0; i < 9; i++) {
                    float ang = Seed + i / 9f * MathHelper.TwoPi;
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        ang.ToRotationVector2() * Main.rand.NextFloat(2f, 5f),
                        i % 3 == 0 ? FeatherGray : OmenViolet,
                        Main.rand.NextFloat(0.26f, 0.44f))?.Configure(true, Main.rand.Next(14, 24));
                }
            }
            //落羽相：残羽缓坠
            if (Drifting && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 24f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.6f, 1.3f)),
                    FeatherGray, Main.rand.NextFloat(0.18f, 0.3f))?.Configure(true, Main.rand.Next(16, 26));
            }
        }

        /// <summary>只有爆鸣窗结算伤害</summary>
        public override bool? CanDamage() => InBurst ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => Utils.CenteredRectangle(Projectile.Center, new Vector2(BurstRadius * 2f))
                .Intersects(targetHitbox);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.ShadowFlame, 180);

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (soft == null || glow == null || flare == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;

            if (Elapsed < GatherFrames) {
                //收羽：六道影羽沿螺旋向心收束（半径与转角同步缩小）
                float t = Elapsed / (float)GatherFrames;
                for (int i = 0; i < 6; i++) {
                    float ang = Seed + i / 6f * MathHelper.TwoPi + t * 2.6f;
                    float dist = MathHelper.Lerp(96f, 10f, t * t);
                    Vector2 featherPos = pos + ang.ToRotationVector2() * dist;
                    //羽尖朝向切线方向，像绕圈滑入
                    float featherRot = ang + MathHelper.PiOver2 + 0.4f;
                    Main.EntitySpriteDraw(soft, featherPos, null,
                        Color.Lerp(FeatherGray, OmenViolet, t) * (0.35f + 0.5f * t), featherRot,
                        soft.Size() / 2f, new Vector2(20f / soft.Width, 3.4f / soft.Height),
                        SpriteEffects.None, 0);
                }
                //心口凝影（真 alpha 暗核渐显）
                Main.EntitySpriteDraw(soft, pos, null, ShadowInk * (0.55f * t), Seed,
                    soft.Size() / 2f, new Vector2(30f * t / soft.Width, 30f * t / soft.Height),
                    SpriteEffects.None, 0);
                return false;
            }

            float fade = Drifting
                ? MathHelper.Clamp(Projectile.timeLeft / (float)DriftFrames, 0f, 1f) : 1f;
            float burstT = InBurst ? (Elapsed - GatherFrames) / (float)BurstFrames : 1f;
            float ringR = MathHelper.Lerp(0.25f, 1.35f, burstT);
            //暗芯（真 alpha 压暗）+ 紫焰环（加色）+ 凶星闪
            Main.EntitySpriteDraw(soft, pos, null, ShadowInk * (0.7f * fade), Seed + burstT,
                soft.Size() / 2f, new Vector2(52f / soft.Width, 52f / soft.Height) * ringR,
                SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, (OmenViolet with { A = 0 }) * (0.65f * fade),
                0f, glow.Size() / 2f, 1.5f * ringR, SpriteEffects.None, 0);
            if (InBurst) {
                Main.EntitySpriteDraw(flare, pos, null,
                    (OmenViolet with { A = 0 }) * (0.9f * (1f - burstT * 0.5f)),
                    Seed - burstT * 0.6f, flare.Size() / 2f, 0.4f * ringR, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
