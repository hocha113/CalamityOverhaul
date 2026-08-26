using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Stonewake.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Stonewake
{
    /// <summary>
    /// 石醒双厅的逐玩家状态：脉冲/光柱的服务端调度冷却（决策私产，不同步），
    /// 以及被凝视之柱扫过后的石纹视觉挂饰（纯本地演出，绝不做真石化/禁锢）
    /// </summary>
    internal class StonewakePlayer : ModPlayer
    {
        /// <summary>石纹挂饰时长（帧）</summary>
        private const int VeilFrames = 90;

        /// <summary>共振脉冲冷却（服务端决策私产，客户端不得用它驱动画面）</summary>
        internal int PulseCooldown;
        /// <summary>凝视之柱冷却（服务端决策私产）</summary>
        internal int PillarCooldown;
        /// <summary>石纹挂饰剩余帧（本地视觉）</summary>
        internal int StoneVeil;

        public override void Initialize() {
            PulseCooldown = 0;
            PillarCooldown = 0;
            StoneVeil = 0;
        }

        public override void PostUpdateMiscEffects() {
            if (StoneVeil > 0) {
                StoneVeil--;
            }
        }

        public override void UpdateDead() => StoneVeil = 0;

        public override void OnHitByProjectile(Projectile proj, Player.HurtInfo hurtInfo) {
            if (proj.type != ModContent.ProjectileType<StonewakeGazePillarProj>()) {
                return;
            }
            StoneVeil = VeilFrames;
            if (Main.dedServ) {
                return;
            }
            //石纹上身的一拍：石屑迸落+石质闷响
            SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 3 }, Player.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_MarbleChip>(Player.Center + Main.rand.NextVector2Circular(10f, 16f),
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1.5f, 3.5f)),
                    StonewakeFX.MarbleGold, Main.rand.NextFloat(0.35f, 0.6f)).Configure(Main.rand.Next(18, 28));
            }
        }

        public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright) {
            if (StoneVeil <= 0) {
                return;
            }
            //入纹快、褪纹慢：前 12 帧压满，尾段线性回暖
            float k = StoneVeil > VeilFrames - 12 ? (VeilFrames - StoneVeil) / 12f
                : MathHelper.Clamp(StoneVeil / (float)(VeilFrames - 12), 0f, 1f);
            //向石灰色收拢：绿蓝压得更狠一点，读作大理石的冷白
            r *= 1f - 0.30f * k;
            g *= 1f - 0.32f * k;
            b *= 1f - 0.38f * k;

            //挂饰期间偶发石尘剥落（只在主影绘制时掉，避免残影分身重复掉屑）
            if (drawInfo.shadow == 0f && !Main.gamePaused && Main.rand.NextBool(9)) {
                Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height,
                    DustID.Stone, 0f, 0.4f, 120, default, 0.8f);
                dust.velocity *= 0.3f;
                dust.noGravity = false;
            }
        }
    }
}
