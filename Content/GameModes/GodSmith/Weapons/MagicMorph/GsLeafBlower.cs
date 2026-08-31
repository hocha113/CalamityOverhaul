using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 吹叶机重铸：风暴模式（模式切换型，走 GsMorphPlayer.OpenMode 通用接口）。
    /// 材质身份：丛林风暴（叶刃卷成的绿旋风）。<br/>
    /// ①A 形态 rider：叶片旋转尾迹（叶绿光屑）；<br/>
    /// ②B 形态（蓄力 45t）「风暴模式」：开启 6 秒风暴窗，窗内叶片弹速 +25%、
    /// 穿透 +1、螺旋尾迹增密，枪口有叶旋读数；③施法有持续风压抖动
    /// </summary>
    internal class GsLeafBlower : GsMorphScheme
    {
        public override int TargetItemID => ItemID.LeafBlower;

        protected override string GsDescFallback =>
            "Reforged: leaf blades spin with a razor-green wake" +
            "\nHold right click to charge Storm Mode: for six seconds every leaf flies faster, cuts deeper and spirals harder";

        protected override int ChargeTicksB => 45;
        protected override float ChargeManaMult => 1.7f;
        protected override Color ChargeColor => LeafMain;
        protected override float BaseDamageMult => 1.05f;

        internal static readonly Color LeafBright = new(190, 255, 140);
        internal static readonly Color LeafMain = new(110, 205, 70);
        internal static readonly Color LeafDeep = new(40, 100, 34);

        /// <summary>风暴模式窗时长</summary>
        private const int StormTicks = 360;

        /// <summary>形态：风暴窗内出生的叶片（跨端按此标加密螺旋尾迹）</summary>
        private const int KindLeafStorm = 20;

        /// <summary>原版叶片弹类型</summary>
        private static int LeafType => ContentSamples.ItemsByType[ItemID.LeafBlower].shoot;

        private static bool StormOn(Player player)
            => player.GetModPlayer<GsMorphPlayer>().ModeActive(ItemID.LeafBlower);

        //==================== 动画法：风压抖动 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //持续风压：itemAnimation 定相正弦抖动（绝对抖动剖面 ±0.028，差分施加防积分失真；want 取负=原 += 语义）
            int a = player.itemAnimation;
            float jitter = MathF.Sin(a * 1.9f) * 1.4f;
            player.itemLocation += new Vector2(0f, jitter);
            GsMagicKickMath.ApplyKickDiff(player,
                -jitter * 0.02f,
                -MathF.Sin((a + 1) * 1.9f) * 1.4f * 0.02f);
        }

        //==================== B 形态：开风暴窗 ====================

        protected override void FireMorphB(Item item, Player player) {
            player.GetModPlayer<GsMorphPlayer>().OpenMode(item.type, StormTicks);
            SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.9f, Pitch = -0.25f }, player.Center);
            if (VaultUtils.isServer) {
                return;
            }
            //开窗演出：风环爆 + 绕身叶旋
            PRTLoader.NewParticle<PRT_ProcRing>(player.MountedCenter, Vector2.Zero, LeafMain, 1f)
                ?.Configure(64f, 9f, 16);
            for (int i = 0; i < 10; i++) {
                float ang = MathHelper.TwoPi * i / 10f;
                PRTLoader.NewParticle<PRT_Spark>(player.MountedCenter + ang.ToRotationVector2() * 20f,
                    (ang + MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(3f, 5f),
                    i % 2 == 0 ? LeafMain : LeafBright, Main.rand.NextFloat(0.26f, 0.4f))
                    ?.Configure(true, Main.rand.Next(14, 22));
            }
        }

        //==================== 风暴窗：弹速/穿透/打标 ====================

        public override void GsModifyShootStats(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            //风暴窗内弹速 +25%（射击链只在 owner 端执行，模式态天然本地）
            if (player.whoAmI == Main.myPlayer && StormOn(player)) {
                velocity *= 1.25f;
            }
        }

        protected override void GsMorphOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            //风暴窗出生的叶片：穿透 +1，打风暴标（远端按标加密尾迹）
            if (proj.owner == Main.myPlayer && proj.type == LeafType && StormOn(Main.player[proj.owner])) {
                router.MarkData = KindLeafStorm;
                if (proj.penetrate > 0) {
                    proj.penetrate++;
                }
                proj.netUpdate = true;
            }
        }

        protected override void GsMorphHoldItem(Item item, Player player) {
            //风暴窗读数：枪口叶旋（模式态只在 owner 端存在，个人读数合法）
            if (player.whoAmI != Main.myPlayer || VaultUtils.isServer || !StormOn(player)) {
                return;
            }
            if (Main.GameUpdateCount % 4 == 0) {
                Vector2 tip = player.MountedCenter + GsAimUnit(player) * 30f;
                float ang = Main.GameUpdateCount * 0.35f;
                PRTLoader.NewParticle<PRT_Spark>(tip + ang.ToRotationVector2() * 9f,
                    (ang + MathHelper.PiOver2).ToRotationVector2() * 1.6f,
                    LeafBright, Main.rand.NextFloat(0.18f, 0.28f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        //==================== 飞行相：叶旋尾迹 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != LeafType || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, LeafMain.ToVector3() * 0.12f);
            bool storm = router.MarkData == KindLeafStorm;
            //叶旋尾迹：绕弹道螺旋的叶绿光屑，风暴叶双螺旋增密
            int interval = storm ? 3 : 6;
            if (proj.timeLeft % interval == 0) {
                float phase = proj.timeLeft * 0.5f + proj.identity * 1.3f;
                Vector2 side = proj.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2)
                    * MathF.Sin(phase) * 8f;
                PRTLoader.NewParticle<PRT_Light>(proj.Center + side - proj.velocity * 0.3f,
                    -proj.velocity * 0.05f, storm ? LeafBright : LeafMain,
                    Main.rand.NextFloat(0.05f, 0.08f))?.Configure(Main.rand.Next(10, 16), 0.65f);
                if (storm) {
                    PRTLoader.NewParticle<PRT_Light>(proj.Center - side - proj.velocity * 0.3f,
                        -proj.velocity * 0.05f, LeafMain,
                        Main.rand.NextFloat(0.05f, 0.08f))?.Configure(Main.rand.Next(10, 16), 0.65f);
                }
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != LeafType || VaultUtils.isServer) {
                return;
            }
            //命中反馈：叶屑迸散（风暴叶更多）
            int count = router.MarkData == KindLeafStorm ? 4 : 2;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Main.rand.NextVector2Circular(2f, 2f), i % 2 == 0 ? LeafMain : LeafBright,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }
    }
}
