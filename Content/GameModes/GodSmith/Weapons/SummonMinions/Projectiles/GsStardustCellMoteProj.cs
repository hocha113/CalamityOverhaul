using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 分裂胞子：星尘细胞有丝分裂甩出的原生质小体。出生 6 帧演分裂
    /// （两瓣胞体从一点撕开），随后软寻的附近敌人，命中重挂细胞侵蚀并炸开星尘；
    /// 无的可寻时游过 70 帧自行散回星尘。材质：星尘原生质（膜体半透、核心炽蓝）
    /// </summary>
    internal class GsStardustCellMoteProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private static readonly Color CellCyan = new(94, 202, 238);
        private static readonly Color CellPale = new(196, 240, 252);
        private static readonly Color NucleusBlue = new(58, 118, 236);

        private const int LifeFrames = 70;
        private const int SplitFrames = 6;
        private const int FadeFrames = 12;

        private ref float Life => ref Projectile.localAI[0];

        private float Seed => Projectile.identity * 0.8447f % MathHelper.TwoPi;

        private bool Fading => Projectile.timeLeft <= FadeFrames;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = LifeFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.35f, Pitch = 0.4f },
                    Projectile.Center);
            }
            //分裂期只随初速漂移，随后软寻的
            if (Life > SplitFrames && !Fading) {
                NPC prey = FindPrey(560f);
                if (prey != null) {
                    Vector2 want = (prey.Center - Projectile.Center)
                        .SafeNormalize(Vector2.UnitY) * 9f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.09f);
                }
            }
            if (Fading) {
                Projectile.velocity *= 0.9f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, CellCyan.ToVector3() * 0.2f);
            //原生质拖尾：低频星尘光点
            if (Life % 4f == 0f && !Fading) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center,
                    -Projectile.velocity * 0.06f, CellCyan, 0.1f)?.Configure(12, 0.7f);
            }
            //散回星尘：末段膜体化光
            if (Fading && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.4f)),
                    CellPale, Main.rand.NextFloat(0.07f, 0.12f))?.Configure(14, 0.6f);
            }
        }

        /// <summary>最近可追猎敌人（各端本地同判，寻的量随 velocity 过线容差可接受）</summary>
        private NPC FindPrey(float radius) {
            NPC best = null;
            float bestDist = radius;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = npc.Center.Distance(Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //重挂细胞侵蚀（骑原版 buff 同步）
            target.AddBuff(BuffID.StardustMinionBleed, 300);
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.4f, Pitch = -0.1f },
                Projectile.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.8f, 4f),
                    i % 2 == 0 ? CellCyan : CellPale,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(false, Main.rand.Next(10, 16));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (soft == null || glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float fade = Fading ? Projectile.timeLeft / (float)FadeFrames : 1f;

            if (Life <= SplitFrames) {
                //有丝分裂：两瓣胞体自一点对向撕开，中间拉出原生质丝
                float t = Life / SplitFrames;
                float apart = MathHelper.Lerp(1f, 9f, t);
                Vector2 axis = (Seed + 0.6f).ToRotationVector2();
                for (int s = -1; s <= 1; s += 2) {
                    Main.EntitySpriteDraw(soft, pos + axis * apart * s, null,
                        CellPale * 0.55f, 0f, soft.Size() / 2f,
                        new Vector2(11f / soft.Width, 10f / soft.Height), SpriteEffects.None, 0);
                }
                Main.EntitySpriteDraw(soft, pos, null, CellCyan * (0.5f * (1f - t)),
                    axis.ToRotation(), soft.Size() / 2f,
                    new Vector2(apart * 2.2f / soft.Width, 3f / soft.Height), SpriteEffects.None, 0);
                return false;
            }

            //膜体：伪足波动（三瓣错相鼓包）+ 半透膜 + 炽蓝核
            float wobA = 1f + 0.14f * (float)Math.Sin(Life * 0.31f + Seed);
            float wobB = 1f + 0.14f * (float)Math.Sin(Life * 0.31f + Seed + 2.1f);
            Main.EntitySpriteDraw(soft, pos, null, CellPale * (0.45f * fade),
                Projectile.rotation, soft.Size() / 2f,
                new Vector2(15f * wobA / soft.Width, 12f * wobB / soft.Height),
                SpriteEffects.None, 0);
            for (int i = 0; i < 3; i++) {
                float ang = Seed + Life * 0.06f + i / 3f * MathHelper.TwoPi;
                float bump = 5f + 1.6f * (float)Math.Sin(Life * 0.24f + i * 1.9f);
                Main.EntitySpriteDraw(soft, pos + ang.ToRotationVector2() * bump, null,
                    CellCyan * (0.3f * fade), ang, soft.Size() / 2f,
                    new Vector2(6f / soft.Width, 5f / soft.Height), SpriteEffects.None, 0);
            }
            //核心（加色炽蓝，呼吸）
            float pulse = 0.85f + 0.15f * (float)Math.Sin(Life * 0.4f + Seed);
            Main.EntitySpriteDraw(glow, pos, null, (NucleusBlue with { A = 0 }) * (0.8f * fade),
                0f, glow.Size() / 2f, 0.16f * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, (CellCyan with { A = 0 }) * (0.35f * fade),
                0f, glow.Size() / 2f, 0.3f * pulse, SpriteEffects.None, 0);
            return false;
        }
    }
}
