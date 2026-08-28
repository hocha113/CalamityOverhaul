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
    /// 夜明重铸（P13 左键 rider）。材质身份：拂晓极光（帷幕垂落的曦光矛）。<br/>
    /// ①左键 rider：曦光弹沿翠紫极光渐变曳虹，每第 5 次命中自敌顶垂落一支极光光矛，
    /// 呼应大招帷幕的光矛雨②满量右键「极光帷幕」照旧③施法有举杖响应。
    /// 光矛 0.5×/5 折算 ≈ +7%，底伤加成保持 5%
    /// </summary>
    internal class GsNightglow : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.FairyQueenMagicItem;

        protected override string GsDescFallback =>
            "Reforged: hits build Dawnlight; at full charge, right click to unfurl an Aurora Curtain over your cursor\n" +
            "The curtain sears foes within and rains light lances below\n" +
            "Every 5th bolt hit drops an aurora lance from above the target";

        public override int ChargePerHit => 3;

        public override int CataclysmManaCost => 50;

        protected override float PassiveDamageBonus => 0.05f;

        protected override int DirectorType => ModContent.ProjectileType<GsAuroraCurtainDirector>();

        protected override Color AccentColor => new(140, 230, 210);

        protected override SoundStyle TriggerSound => SoundID.Item84;

        /// <summary>原版曦光弹类型</summary>
        private static int GlowBoltType => ContentSamples.ItemsByType[ItemID.FairyQueenMagicItem].shoot;

        /// <summary>曦光命中计数（owner 端命中钩子消费，本机契约）</summary>
        private int glowHits;

        protected override void ModifyTriggerParams(Item item, Player player, ref Vector2 anchor, ref float ai1, ref float ai2) {
            //帷幕挂在光标上空，光矛自帘心落向帘下
            anchor.Y -= 200f;
        }

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

        //==================== 左键 rider：极光渐变曳虹 + 垂落光矛 ====================

        /// <summary>沿翠→紫极光带取色（identity 定相 + 缓慢时间漂移，绘制路径零随机）</summary>
        private static Color AuroraHue(int identity, float shift = 0f) {
            float t = 0.5f + 0.5f * MathF.Sin(identity * 0.73f + Main.GlobalTimeWrappedHourly * 0.9f + shift);
            return Color.Lerp(GsAuroraLanceProj.AuroraGreen, GsAuroraLanceProj.AuroraViolet, t);
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != GlowBoltType || VaultUtils.isServer) {
                return;
            }
            Color hue = AuroraHue(proj.identity);
            Lighting.AddLight(proj.Center, hue.ToVector3() * 0.28f);
            if (proj.timeLeft % 5 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.05f, hue, Main.rand.NextFloat(0.24f, 0.38f))?.Configure(hue, 18, 0.05f, 0.8f);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (proj.type != GlowBoltType) {
                return null;
            }
            //极光曳虹：重影色沿极光带缓慢流动
            GsCataclysmRiderLib.DrawSpeedGhost(proj, AuroraHue(proj.identity), 0.42f);
            return null;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积曦光
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != GlowBoltType) {
                return;
            }
            //命中反馈：曦光斑
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        Main.rand.NextVector2Circular(1.2f, 1.2f) - new Vector2(0f, 0.5f),
                        AuroraHue(proj.identity, i * 1.7f), Main.rand.NextFloat(0.26f, 0.42f))
                        ?.Configure(AuroraHue(proj.identity, i * 1.7f + 0.5f), 20, 0.05f, 0.85f);
                }
            }
            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            //每第 5 次命中：敌顶垂落一支极光光矛（复用帷幕光矛，ai0=矛体色相；Misc 源不承签）
            glowHits++;
            if (glowHits % 5 != 0) {
                return;
            }
            Player owner = Main.player[proj.owner];
            float hueNorm = proj.identity % 7 / 3.5f - 1f;
            Projectile.NewProjectile(owner.GetSource_Misc("GsCataclysmRider"),
                target.Center + new Vector2(0f, -240f), new Vector2(0f, 2.6f),
                ModContent.ProjectileType<GsAuroraLanceProj>(),
                Math.Max(1, (int)(proj.damage * 0.5f)), proj.knockBack * 0.6f, proj.owner, hueNorm);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.4f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
            }
        }
    }
}
