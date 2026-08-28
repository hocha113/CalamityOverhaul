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
    /// 蝙蝠权杖重铸（P13 左键 rider）。材质身份：暮紫夜革（月下蝠群的翼膜暗光）。<br/>
    /// ①左键 rider：蝠影拍翅曳暮紫翼残影，每第 8 次蝙蝠咬中分出一只幻影蝠扑向近旁另一敌，
    /// 呼应大招的成波扑咬②满量右键「万蝠临渊」照旧③施法有权杖上挑响应。
    /// 幻影蝠 0.4×/8 ≈ +5%，计入包络
    /// </summary>
    internal class GsBatScepter : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.BatScepter;

        protected override string GsDescFallback =>
            "Reforged: hits build Nightfall; at full charge, right click to call the bat deluge\n" +
            "A phantom moon rises, waves of bats dive at your foes, then circle you as a guard ring\n" +
            "Every 8th bat bite splits off a phantom bat that dives at another nearby foe";

        public override int ChargePerHit => 3;

        public override int CataclysmManaCost => 45;

        protected override float PassiveDamageBonus => 0.08f;

        protected override int DirectorType => ModContent.ProjectileType<GsBatSwarmDirector>();

        protected override Color AccentColor => new(150, 110, 200);

        protected override bool AnchorAtCursor => false;

        protected override SoundStyle TriggerSound => SoundID.Item32;

        /// <summary>原版蝙蝠弹类型</summary>
        private static int BatType => ContentSamples.ItemsByType[ItemID.BatScepter].shoot;

        /// <summary>蝙蝠咬中计数（owner 端命中钩子消费，本机契约）</summary>
        private int biteCounter;

        /// <summary>幻影蝠出生窗旗标（打标继承窗口写角色）</summary>
        private bool pendingPhantom;

        //==================== 动画法：权杖上挑 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //挥杖唤蝠：杖头上挑 3px 带一记后旋（确定性输入，各端一致）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation += new Vector2(-player.direction * 2f, -3f) * progress;
            player.itemRotation -= player.direction * 0.14f * progress;
        }

        //==================== 左键 rider：翼残影 + 幻影蝠 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != BatType || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, GsBatSwarmDirector.DuskViolet.ToVector3() * 0.22f);
            //拍翅暮尘：幻影蝠（MarkData=1）更稀
            int interval = router.MarkData >= 1f ? 7 : 4;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.06f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    GsBatSwarmDirector.DuskViolet, Main.rand.NextFloat(0.07f, 0.11f))?.Configure(10, 0.8f);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (proj.type != BatType) {
                return null;
            }
            //拍翅双残影：随翅频摆动的暮紫重影（identity 定相，零随机）
            Main.instance.LoadProjectile(proj.type);
            var tex = TextureAssets.Projectile[proj.type].Value;
            int frameHeight = tex.Height / Main.projFrames[proj.type];
            Rectangle frame = new(0, frameHeight * proj.frame, tex.Width, frameHeight);
            float flap = MathF.Sin(Main.GlobalTimeWrappedHourly * 16f + proj.identity * 0.83f);
            Color ghost = GsBatSwarmDirector.DuskViolet with { A = 0 };
            Vector2 perp = proj.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < 2; i++) {
                Vector2 off = perp * flap * (3f + i * 3f) * (i == 0 ? 1f : -1f) - proj.velocity * (0.4f * (i + 1));
                Main.EntitySpriteDraw(tex, proj.Center + off - Main.screenPosition, frame,
                    ghost * (0.34f / (i + 1)), proj.rotation, frame.Size() * 0.5f, proj.scale,
                    proj.spriteDirection == -1 ? Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipHorizontally
                        : Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            }
            return null;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积夜幕（计量是攻击方本地量）
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != BatType) {
                return;
            }
            //咬中反馈：暮紫夜尘迸散
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Light>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        Main.rand.NextVector2Circular(1.4f, 1.4f),
                        GsBatSwarmDirector.DuskViolet, Main.rand.NextFloat(0.09f, 0.14f))?.Configure(12, 0.85f);
                }
            }
            //幻影蝠不再分裂（防自喂）；每第 8 咬渡出一只扑向近旁另一敌
            if (router.MarkData >= 1f || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            biteCounter++;
            if (biteCounter % 8 != 0) {
                return;
            }
            NPC next = GsCataclysmRiderLib.FindAnotherEnemy(target, target.Center, 460f);
            Vector2 vel = next != null
                ? (next.Center - target.Center).SafeNormalize(-Vector2.UnitY) * 7f
                : -Vector2.UnitY * 5f;
            int damage = Math.Max(1, (int)(proj.damage * 0.4f));
            pendingPhantom = true;
            Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, vel,
                BatType, damage, proj.knockBack * 0.5f, proj.owner);
            pendingPhantom = false;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath4 with { Volume = 0.3f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
            }
        }

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            //幻影蝠：出生窗打角色标（先于生成包），缩体 0.8
            if (pendingPhantom && proj.owner == Main.myPlayer) {
                router.MarkData = 1f;
                proj.scale *= 0.8f;
                proj.netUpdate = true;
            }
        }
    }
}
