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
    /// 狱炎柱：小恶魔聚火令的结算体。目标脚下竖起 120 高火柱，
    /// 三相 = 喷发 8 帧（伤害窗）/ 舔舐 24 帧（伤害窗，每目标至多两段）/ 熄灭余烬 8 帧（无伤害）。
    /// 柱身两端收口（喷发从地面窜起、熄灭向上散烬），不做恒亮贴条
    /// </summary>
    internal class GsImpPyreProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        private static readonly Color FireHot = new(255, 224, 150);
        private static readonly Color FireMain = new(255, 132, 44);
        private static readonly Color FireDeep = new(150, 52, 20);

        private const int EruptFrames = 8;
        private const int BlazeFrames = 24;
        private const int FadeFrames = 8;
        private const int TotalFrames = EruptFrames + BlazeFrames + FadeFrames;
        private const float PillarHeight = 120f;
        private const float PillarWidth = 44f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool Fading => Elapsed >= EruptFrames + BlazeFrames;

        /// <summary>喷发进度（0~1，柱身从地面窜起）</summary>
        private float RiseT => MathHelper.Clamp(Elapsed / (float)EruptFrames, 0f, 1f);

        private float Seed => Projectile.identity * 0.8117f % MathHelper.TwoPi;

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 130;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //舔舐期每目标至多两段
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (VaultUtils.isServer) {
                return;
            }
            float height = PillarHeight * RiseT;
            Lighting.AddLight(Projectile.Bottom - new Vector2(0f, height * 0.5f),
                FireMain.ToVector3() * (Fading ? 0.2f : 0.5f));
            //喷发帧：火舌窜起（音效随首帧走，AI 各端都跑，远端也可闻）
            if (Elapsed == 1) {
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.55f, Pitch = 0.1f },
                    Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_HellFire>(
                        Projectile.Bottom + new Vector2(Main.rand.NextFloat(-16f, 16f), 0f),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f),
                            -Main.rand.NextFloat(4f, 9f)),
                        FireMain, Main.rand.NextFloat(0.5f, 0.9f));
                }
            }
            //舔舐相：持续火舌与上升余烬（每帧 ≤2）
            if (!Fading && Elapsed > 1) {
                if (Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_HellFlame>(
                        Projectile.Bottom + new Vector2(Main.rand.NextFloat(-14f, 14f),
                            -Main.rand.NextFloat(0f, height * 0.8f)),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(1.5f, 3f)),
                        FireMain, Main.rand.NextFloat(0.4f, 0.7f));
                }
            }
            //熄灭相：散烬上飘
            else if (Fading && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Bottom + new Vector2(Main.rand.NextFloat(-12f, 12f),
                        -Main.rand.NextFloat(20f, height)),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1f, 2.4f)),
                    Main.rand.NextBool() ? FireMain : FireDeep,
                    Main.rand.NextFloat(0.25f, 0.4f))?.Configure(false, Main.rand.Next(14, 24));
            }
        }

        /// <summary>熄灭相不再伤害</summary>
        public override bool? CanDamage() => Fading ? false : null;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float height = PillarHeight * RiseT;
            Rectangle pillar = new((int)(Projectile.Bottom.X - PillarWidth / 2f),
                (int)(Projectile.Bottom.Y - height), (int)PillarWidth, (int)height);
            return pillar.Intersects(targetHitbox);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 240);

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (soft == null || glow == null) {
                return false;
            }
            float fade = Fading
                ? MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f) : 1f;
            float height = PillarHeight * RiseT * (Fading ? 0.6f + 0.4f * fade : 1f);
            Vector2 basePos = Projectile.Bottom - Main.screenPosition;
            Vector2 mid = basePos - new Vector2(0f, height * 0.5f);
            //柱身摇曳（identity 定相）
            float sway = 0.05f * (float)Math.Sin(Elapsed * 0.35f + Seed);
            float widthPulse = 1f + 0.12f * (float)Math.Sin(Elapsed * 0.6f + Seed * 1.3f);

            //焦深底柱（真 alpha 暗层）
            Main.EntitySpriteDraw(soft, mid, null, FireDeep * (0.7f * fade), sway,
                soft.Size() / 2f,
                new Vector2(PillarWidth * 1.1f / soft.Width * widthPulse, height * 1.05f / soft.Height),
                SpriteEffects.None, 0);
            //主焰柱
            Main.EntitySpriteDraw(soft, mid, null, FireMain * (0.8f * fade), sway * 1.4f,
                soft.Size() / 2f,
                new Vector2(PillarWidth * 0.72f / soft.Width * widthPulse, height * 0.96f / soft.Height),
                SpriteEffects.None, 0);
            //灼芯（加色）
            Main.EntitySpriteDraw(soft, mid, null, (FireHot with { A = 0 }) * (0.55f * fade),
                sway * 1.8f, soft.Size() / 2f,
                new Vector2(PillarWidth * 0.34f / soft.Width, height * 0.8f / soft.Height),
                SpriteEffects.None, 0);
            //足底熔光
            Main.EntitySpriteDraw(glow, basePos, null, (FireMain with { A = 0 }) * (0.5f * fade),
                0f, glow.Size() / 2f, new Vector2(0.9f, 0.35f) * widthPulse, SpriteEffects.None, 0);
            return false;
        }
    }
}
