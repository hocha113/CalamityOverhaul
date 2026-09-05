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
    /// 夜明重铸（P13 左键 rider）。材质身份：拂晓极光（帷幕垂落的曦光矛）。<br/>
    /// ①左键 rider：每第 5 次命中自敌顶垂落一支极光光矛②施法有举杖响应。
    /// 光矛 0.5×/5 折算 ≈ +7%，底伤加成保持 5%
    /// </summary>
    internal class GsNightglow : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.FairyQueenMagicItem;

        protected override string GsDescFallback =>
            "Reforged: every 5th bolt hit drops an aurora lance from above the target";
        public override int ChargePerHit => 3;

        protected override float PassiveDamageBonus => 0.05f;

        /// <summary>原版曦光弹类型</summary>
        private static int GlowBoltType => ContentSamples.ItemsByType[ItemID.FairyQueenMagicItem].shoot;

        /// <summary>曦光命中计数（owner 端命中钩子消费，本机契约）</summary>
        private int glowHits;

        //==================== 动画法：举杖迎光 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //举杖迎光：杖头抬 4px 微后仰（确定性输入，各端一致）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation += new Vector2(-player.direction * 2f, -4f) * progress;
            player.itemRotation -= player.direction * 0.1f * progress;
        }

        //==================== 左键 rider：垂落光矛 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积曦光
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != GlowBoltType || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            //每第 5 次命中：敌顶垂落一支极光光矛（Misc 源不承签）
            glowHits++;
            if (glowHits % 5 != 0) {
                return;
            }
            Player owner = Main.player[proj.owner];
            Projectile.NewProjectile(owner.GetSource_Misc("GsCataclysmRider"),
                target.Center + new Vector2(0f, -240f), new Vector2(0f, 2.6f),
                ModContent.ProjectileType<GsAuroraLanceProj>(),
                Math.Max(1, (int)(proj.damage * 0.5f)), proj.knockBack * 0.6f, proj.owner);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.4f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
            }
        }
    }
}
