using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 幽灵法杖重铸（A 档）。材质身份：渡魂青焰（幽灵套的苍青冥火）。<br/>
    /// ①迷魂缕缕缠体、幽芒残影飞行；②命中积攒「收魂」，每渡满四分之一计量分出一缕副魂扑向近旁另一敌；
    /// ③集满右键「魂渊潮汐」：光标处开魂涡，三涌潮汐扫荡，潮头渡魂归疗施法者；④施法有举杖响应与幽焰起手
    /// </summary>
    internal class GsSpectreStaff : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.SpectreStaff;

        protected override string GsDescFallback =>
            "Reforged: hits build Soulharvest; when full, right click to raise the Soul Tide at your cursor" +
            "\nEvery quarter of the gauge banked splits off an echo soul that hunts another nearby foe" +
            "\nThree tide crests sweep the ring; each crest hit ferries 1 life back to you, up to 6 a second";

        protected override float PassiveDamageBonus => 0.08f;
        protected override int DirectorType => ModContent.ProjectileType<GsSpectreStaffTideDirector>();
        public override int ChargePerHit => 4;
        protected override Color AccentColor => SpectreCyan;
        protected override SoundStyle TriggerSound => SoundID.Item72;

        internal static readonly Color SpectreCyan = new(130, 235, 215);
        internal static readonly Color SpectreDeep = new(36, 96, 104);

        /// <summary>原版迷魂弹类型</summary>
        internal static int SoulType => ContentSamples.ItemsByType[ItemID.SpectreStaff].shoot;

        //==================== 动画法：举杖 + 幽焰起手 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //施法举杖：杖头抬升 5px 再缓落（确定性输入，各端一致）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation += new Vector2(-player.direction * 2f, -5f) * progress;
            player.itemRotation -= player.direction * 0.12f * progress;
        }

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //起手幽焰：杖尖苍青冥火腾起
            Vector2 tip = player.MountedCenter + new Vector2(player.direction * 16f, -14f);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_SoulFire>(tip + Main.rand.NextVector2Circular(5f, 5f),
                    new Vector2(0f, -Main.rand.NextFloat(0.8f, 1.6f)), SpectreCyan, Main.rand.NextFloat(0.32f, 0.5f));
            }
            Lighting.AddLight(tip, SpectreCyan.ToVector3() * 0.35f);
        }

        //==================== 左键 rider：幽芒飞行 + 渡魂副魂 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != SoulType || VaultUtils.isServer) {
                return;
            }
            //迷魂缕：苍青魂缕缀行（副魂 MarkData=1 更稀），禁裸贴图平移
            int interval = router.MarkData >= 1f ? 5 : 3;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_SoulLight>(proj.Center - proj.velocity * 0.5f,
                    -proj.velocity * 0.08f + Main.rand.NextVector2Circular(0.3f, 0.3f),
                    SpectreCyan, Main.rand.NextFloat(0.24f, 0.4f));
            }
            Lighting.AddLight(proj.Center, SpectreCyan.ToVector3() * 0.3f);
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (proj.type != SoulType) {
                return null;
            }
            //幽芒残影：本体之下垫双层呼吸辉体（A=0 加色），identity 定相
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 pos = proj.Center - Main.screenPosition;
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + proj.identity * 0.83f);
            Main.EntitySpriteDraw(glow, pos, null, SpectreDeep with { A = 0 } * (0.55f * pulse), 0f,
                glow.Size() / 2f, 0.4f * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, SpectreCyan with { A = 0 } * (0.5f * pulse), 0f,
                glow.Size() / 2f, 0.22f, SpriteEffects.None, 0);
            return null;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (!VaultUtils.isServer && proj.type == SoulType) {
                //命中反馈：魂火迸绽
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_SoulFire>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        Main.rand.NextVector2Circular(1.5f, 1.5f) - new Vector2(0f, 1.2f),
                        SpectreCyan, Main.rand.NextFloat(0.35f, 0.6f));
                }
            }
            if (proj.type != SoulType || !proj.IsOwnedByLocalPlayer()) {
                base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
                return;
            }
            //计量里程碑检测：跨过四分之一刻度即渡出副魂（副魂命中不再触发，防自喂）
            GsCataclysmPlayer state = Main.player[proj.owner].GetModPlayer<GsCataclysmPlayer>();
            int before = state.BoundItemType == TargetItemID ? state.Charge : 0;
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (router.MarkData >= 1f || state.Charge <= before || state.Charge >= ChargeMax) {
                return;
            }
            if (state.Charge / 25 > before / 25) {
                SpawnEchoSoul(proj, target);
            }
        }

        /// <summary>渡出副魂：从命中处扑向近旁另一敌（MarkData=1 防自喂标）</summary>
        private void SpawnEchoSoul(Projectile proj, NPC target) {
            NPC next = FindAnotherEnemy(target);
            Vector2 vel = next != null
                ? (next.Center - target.Center).SafeNormalize(-Vector2.UnitY) * 5f
                : -Vector2.UnitY * 4f;
            int damage = Math.Max(1, (int)(proj.damage * 0.35f));
            //出生前挂私有形态：经打标继承窗口写 MarkData（先于生成包）
            pendingEcho = true;
            Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, vel,
                SoulType, damage, proj.knockBack * 0.4f, proj.owner);
            pendingEcho = false;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.4f, Pitch = 0.55f, MaxInstances = 3 }, target.Center);
            }
        }

        private bool pendingEcho;

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            if (pendingEcho && proj.owner == Main.myPlayer) {
                router.MarkData = 1f;
                proj.scale *= 0.72f;
                proj.netUpdate = true;
            }
        }

        /// <summary>找 target 之外最近的可追击敌怪</summary>
        private static NPC FindAnotherEnemy(NPC target) {
            NPC best = null;
            float bestDist = 480f * 480f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.whoAmI == target.whoAmI) {
                    continue;
                }
                float d = Vector2.DistanceSquared(npc.Center, target.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }
    }
}
