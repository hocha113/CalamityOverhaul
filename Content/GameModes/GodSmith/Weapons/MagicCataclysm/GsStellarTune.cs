using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 星籁重铸（P13 左键 rider）。材质身份：星辉琴弦（星海终章的前奏音）。<br/>
    /// ①左键 rider：「攀音阶」，连续命中音高逐级爬升（90 帧内续接），攀满 8 音奏一记
    /// 和弦星爆②施法有拨弦摇摆响应。和弦爆 0.5×/8 ≈ +6%，计入包络
    /// </summary>
    internal class GsStellarTune : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.SparkleGuitar;

        protected override string GsDescFallback =>
            "Reforged: consecutive hits climb a scale; the 8th note lands as a star chord burst";
        public override int ChargePerHit => 2;

        protected override float PassiveDamageBonus => 0.08f;

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

        //==================== 左键 rider：攀音阶 ====================

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
