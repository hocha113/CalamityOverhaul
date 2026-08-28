using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·钉头锤】钉头锤重铸：铸铁钉锤。签名行为：①满转速命中砸裂护甲，数秒半防
    /// ②破甲一击带低沉铛声与裂甲火花锥 ③击退侧重，铸铁分量压得实
    /// </summary>
    internal class GsMace : GsFlailScheme
    {
        public override int TargetItemID => ItemID.Mace;

        protected override int FlailProjType => ModContent.ProjectileType<GsMaceHead>();

        protected override string GsDescFallback =>
            "Reforged: a fully charged strike cracks the target's armor, halving its defense for a few seconds" +
            "\nHeavier knockback backs up every blow";

        //全游戏底子最弱的连枷之一（木箱开出的白板锤），重铸补一成五底伤，
        //破甲收益吃满转门槛，综合 DPS 仍落在弱势武器 135% 包络内
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.15f;

        //击退侧重：铸铁分量真实可感
        public override void GsModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
            => knockback *= 1.25f;
    }

    /// <summary>
    /// 钉头锤锤头。族默认链体参数（HeadSize 30）；满转命中挂破甲并升级命中反馈，
    /// 未满转不破甲
    /// </summary>
    internal class GsMaceHead : GsFlailHeadProj
    {
        /// <summary>火星橙</summary>
        internal static readonly Color SparkOrange = new(255, 152, 62);
        /// <summary>铸铁灰</summary>
        internal static readonly Color CastIron = new(148, 148, 156);

        public override int SourceItemID => ItemID.Mace;
        public override int VanillaProjID => ProjectileID.Mace;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain41;
        public override Color GlowColor => SparkOrange;

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            //破甲重击：满转掷出的一击砸裂护甲（半防 4 秒），未满转不给
            if (LaunchCharge >= 0.99f && State == StateLaunch) {
                target.AddBuff(BuffID.BrokenArmor, 240);
            }
        }

        protected override void SpawnHitBurst(NPC target, NPC.HitInfo hit, float charge) {
            base.SpawnHitBurst(target, hit, charge);
            //破甲一击的升级反馈：低沉铛声 + 裂甲火花锥
            if (charge < 0.99f || State != StateLaunch) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit42 with { Volume = 0.8f, Pitch = -0.5f }, target.Center);
            Vector2 coneDir = Owner.MountedCenter.To(target.Center).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 9; i++) {
                Vector2 vel = coneDir.RotatedByRandom(0.55) * Main.rand.NextFloat(4f, 9f);
                Color c = Main.rand.NextBool() ? SparkOrange : CastIron;
                PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, c,
                    Main.rand.NextFloat(0.4f, 0.65f))?.Configure(true, Main.rand.Next(12, 20));
            }
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, SparkOrange, 0.20f)
                ?.Configure(10, 0.85f);
        }
    }
}
