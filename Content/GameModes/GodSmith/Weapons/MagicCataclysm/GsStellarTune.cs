using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 星籁重铸（P13 左键 rider）。材质身份：星辉琴弦（星海终章的前奏音）。<br/>
    /// ①左键 rider：「攀音阶」，连续命中音高逐级爬升（90 帧内续接），攀满 8 音奏一记
    /// 和弦星爆，呼应大招的踩拍星和弦②星弹曳音符与星辉③满量右键「星海终章」照旧
    /// ④施法有拨弦摇摆响应。和弦爆 0.5×/8 ≈ +6%，计入包络
    /// </summary>
    internal class GsStellarTune : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.SparkleGuitar;

        protected override string GsDescFallback =>
            "Reforged: hits build Melody; at full charge, right click to open the Stellar Finale\n" +
            "Star chords play themselves at your foes on the beat while you dance a little faster\n" +
            "Consecutive hits climb a scale; the 8th note lands as a star chord burst";

        public override int ChargePerHit => 2;

        public override int CataclysmManaCost => 45;

        protected override float PassiveDamageBonus => 0.08f;

        protected override int DirectorType => ModContent.ProjectileType<GsStellarFinaleDirector>();

        protected override Color AccentColor => new(255, 160, 220);

        protected override bool AnchorAtCursor => false;

        protected override SoundStyle TriggerSound => SoundID.Item26;

        /// <summary>原版星弦弹类型</summary>
        private static int ChordType => ContentSamples.ItemsByType[ItemID.SparkleGuitar].shoot;

        /// <summary>攀音阶：连击计数与续窗（owner 端命中钩子消费，本机契约）</summary>
        private int scaleStep;

        private uint scaleTick;

        //==================== 动画法：拨弦摇摆 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //拨弦：琴身随使用进度小幅摇摆，读作扫弦（确定性输入，各端一致）
            float progress = 1f - player.itemAnimation / (float)player.itemAnimationMax;
            float sway = MathF.Sin(progress * MathHelper.TwoPi) * 0.09f;
            player.itemRotation += player.direction * sway;
            player.itemLocation.Y += MathF.Sin(progress * MathHelper.Pi) * 1.5f;
        }

        //==================== 左键 rider：音符曳尾 + 攀音阶 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ChordType || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, GsStellarFinaleDirector.StarPink.ToVector3() * 0.24f);
            //音符自弹道飘离
            if (proj.timeLeft % 9 == 0) {
                PRTLoader.NewParticle<PRT_Note>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.04f + new Vector2(0f, -Main.rand.NextFloat(0.4f, 0.9f)),
                    Main.rand.NextBool() ? GsStellarFinaleDirector.StarPink : GsStellarFinaleDirector.StarBlue,
                    Main.rand.NextFloat(0.7f, 0.95f))?.Configure(Main.rand.Next(22, 34));
            }
            if (proj.timeLeft % 5 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.05f, GsStellarFinaleDirector.StarBlue,
                    Main.rand.NextFloat(0.22f, 0.34f))?.Configure(GsStellarFinaleDirector.StarPink, 16, 0.06f, 0.8f);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (proj.type != ChordType) {
                return null;
            }
            //粉蓝双声部重影：两色交替错帧（identity 定相）
            bool alt = (proj.identity & 1) == 0;
            GsCataclysmRiderLib.DrawSpeedGhost(proj,
                alt ? GsStellarFinaleDirector.StarPink : GsStellarFinaleDirector.StarBlue, 0.4f);
            return null;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积乐章
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != ChordType || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            //攀音阶：90 帧内续接，音高逐级爬升；第 8 音落成和弦星爆
            if (Main.GameUpdateCount - scaleTick > 90) {
                scaleStep = 0;
            }
            scaleTick = Main.GameUpdateCount;
            scaleStep++;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item26 with {
                    Volume = 0.3f,
                    Pitch = -0.25f + 0.09f * Math.Min(scaleStep, 8),
                    MaxInstances = 4,
                }, target.Center);
                PRTLoader.NewParticle<PRT_Note>(target.Center + Main.rand.NextVector2Circular(10f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.8f, 1.4f)),
                    GsStellarFinaleDirector.StarPink, 0.9f)?.Configure(Main.rand.Next(24, 36));
            }
            if (scaleStep < 8) {
                return;
            }
            scaleStep = 0;
            //终止式：和弦星爆（真弹幕跨端可见；ai2 传音阶步进定音高）
            Player owner = Main.player[proj.owner];
            Projectile.NewProjectile(owner.GetSource_Misc("GsCataclysmRider"), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsCataclysmRiderBurstProj>(),
                Math.Max(1, (int)(proj.damage * 0.5f)), 3f, proj.owner,
                90f, GsCataclysmRiderBurstProj.ThemeStar, 8f);
        }
    }
}
