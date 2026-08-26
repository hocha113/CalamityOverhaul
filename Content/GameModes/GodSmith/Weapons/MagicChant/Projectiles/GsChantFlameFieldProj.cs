using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant.Projectiles
{
    /// <summary>
    /// 烈火之花「盛焰花田」：火球满层强化后原地化开的贴地火田。<br/>
    /// 判定 = 本体矩形（宽 180 高 50，与可见火苗带同源），idStatic 免疫 15t 兑现 0.25s 一跳；
    /// 伤害在生成时按 0.3 倍烘焙。阶段全部是 timeLeft 的确定函数，远端从快照自算
    /// </summary>
    internal class GsChantFlameFieldProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicChant";

        /// <summary>总寿命 2.5s</summary>
        private const int LifeTicks = 150;
        /// <summary>起势帧数</summary>
        private const int RiseTicks = 10;
        /// <summary>收尾帧数</summary>
        private const int FadeTicks = 18;

        private static readonly Color FlameDeep = new(255, 96, 24);
        private static readonly Color FlameHot = new(255, 196, 96);

        /// <summary>起势/收尾包络（timeLeft 确定函数）</summary>
        private float Envelope {
            get {
                float rise = MathHelper.Clamp((LifeTicks - Projectile.timeLeft) / (float)RiseTicks, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / (float)FadeTicks, 0f, 1f);
                return Math.Min(rise, fade);
            }
        }

        public override void SetDefaults() {
            Projectile.width = 180;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTicks;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 15;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Envelope > 0.55f ? null : false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.7f, Pitch = -0.15f }, Projectile.Center);
                }
            }
            float env = Envelope;
            Lighting.AddLight(Projectile.Center, FlameDeep.ToVector3() * (0.8f * env));

            if (VaultUtils.isServer) {
                return;
            }
            //燃焰身份：火苗自地面窜起、火星上飘，持续期预算 ≤4/帧
            float halfW = Projectile.width * 0.5f;
            Vector2 ground = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_HellFire>(
                    ground + new Vector2(Main.rand.NextFloat(-halfW, halfW), -Main.rand.NextFloat(0f, 8f)),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(1.2f, 2.6f)),
                    Color.White, Main.rand.NextFloat(0.55f, 0.95f) * Math.Max(0.4f, env));
            }
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_LavaFire>(
                    ground + new Vector2(Main.rand.NextFloat(-halfW, halfW), -4f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.8f, 1.8f),
                    Color.White, Main.rand.NextFloat(0.4f, 0.7f) * env);
            }
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_Spark>(
                    ground + new Vector2(Main.rand.NextFloat(-halfW, halfW), -6f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(2f, 4f)),
                    FlameHot, Main.rand.NextFloat(0.22f, 0.4f))?.Configure(true, Main.rand.Next(16, 26));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //火田灼身余痕：命中处一小撮火星（owner 端个人反馈，预算内）
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    -Vector2.UnitY.RotatedByRandom(0.7) * Main.rand.NextFloat(1.5f, 3.5f),
                    FlameDeep, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            //余痕相：熄灭时焦土余烬比火田活得久
            if (VaultUtils.isServer) {
                return;
            }
            float halfW = Projectile.width * 0.5f;
            Vector2 ground = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    ground + new Vector2(Main.rand.NextFloat(-halfW, halfW), -Main.rand.NextFloat(0f, 10f)),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.5f, 1.5f)),
                    Main.rand.NextBool() ? FlameDeep : new Color(148, 92, 44),
                    Main.rand.NextFloat(0.25f, 0.42f))?.Configure(true, Main.rand.Next(20, 34));
            }
        }
    }
}
