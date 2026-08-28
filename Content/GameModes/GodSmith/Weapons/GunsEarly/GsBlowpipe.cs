using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsEarly
{
    /// <summary>
    /// 吹管「毒引」：青竹短管·浸毒棉芯。<br/>
    /// ①毒引：命中同一目标叠一缕毒引，叠满 3 缕炸开小毒雾（滞留毒云）；
    /// ②回气装填：一口 3 镖，吸气可被打断（吸几口吹几口），站定不动回气快 25%；
    /// ③完美回气：下一镖「淬毒镖」，命中立即引爆毒引。<br/>
    /// 吹嘴后坐：出镖轻推 + 吹气烟圈。<br/>
    /// 账目：射速原版；毒雾 3 中一循环均摊 +15%，伤害行 ×1.3（原版公认弱）→ 约 130%
    /// （对齐弱势武器上限，待游戏内标定）
    /// </summary>
    internal class GsBlowpipe : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Blowpipe;

        protected override string GsDescFallback =>
            "Reforged: each hit on the same target threads in a venom fuse; three fuses burst into a lingering toxic cloud.\n" +
            "Reloading is catching your breath, one dart per gulp; stand still to breathe 25% faster.\n" +
            "A sweet-spot breath envenoms the next dart to detonate the fuses on contact";

        public override int MagSize => 3;
        public override int ReloadTicks => 40;
        public override GsReloadStyle Style => GsReloadStyle.Breath;
        protected override bool EjectsShell => false;
        protected override float GetRecoil(bool lastRound) => 0.6f;

        private static readonly Color VenomGreen = new(130, 210, 110);
        private static readonly Color VenomDeep = new(70, 130, 66);

        /// <summary>站定回气 +25%（吹管的呼吸法）</summary>
        protected override float ReloadRate(Player player)
            => player.velocity.LengthSquared() < 0.2f ? 1.25f : 1f;

        /// <summary>伤害行 ×1.3：原版吹管公认弱，账目见类注释</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 1.3f;

        protected override bool? FireNormalRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => Spit(mp, position, velocity, false);

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => Spit(mp, position, velocity, true);

        /// <summary>出镖：淬毒镖打 2 档标；吹嘴烟圈（个人视觉）</summary>
        private bool? Spit(GsGunsEarlyPlayer mp, Vector2 position, Vector2 velocity, bool last) {
            pendingMark = mp.perfectNextShot ? 2f : 1f;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item63 with { Volume = 0.5f, Pitch = last ? -0.2f : 0.15f }, position);
                Vector2 aim = velocity.SafeNormalize(Vector2.UnitX);
                PRTLoader.NewParticle<PRT_Smoke>(position + aim * 6f, aim * 1.2f,
                    new Color(180, 190, 170), Main.rand.NextFloat(0.03f, 0.05f))
                    ?.Configure(Main.rand.Next(10, 16), 0.3f);
            }
            return null;
        }

        //==================== 毒引（owner 端权威） ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.owner != Main.myPlayer || router.MarkData < 1f) {
                return;
            }
            Player player = Main.player[proj.owner];
            GsBlowpipePlayer bp = player.GetModPlayer<GsBlowpipePlayer>();

            if (bp.venomTarget != target.whoAmI) {
                bp.venomTarget = target.whoAmI;
                bp.venomStacks = 0;
            }
            bp.venomStacks++;
            bp.venomWindow = 240;
            target.AddBuff(BuffID.Poisoned, 120);

            bool detonate = bp.venomStacks >= 3 || router.MarkData >= 2f;
            if (detonate) {
                bp.venomStacks = 0;
                //毒引引爆：族内共享毒雾云（滞留 2 秒）
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsGunsEarlyBurstProj>(),
                    Math.Max(1, (int)(proj.damage * 0.7f)), 0f, proj.owner, 60f, 3f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.5f, Pitch = -0.1f }, target.Center);
                }
            }
            else if (!VaultUtils.isServer) {
                //叠引反馈：毒泡上浮
                PRTLoader.NewParticle<PRT_ToxicBubble>(target.Top,
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1f),
                    VenomGreen, Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(24, 36));
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.35f, Pitch = 0.3f + bp.venomStacks * 0.1f }, target.Center);
            }
        }

        //==================== 回气音画 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                //长吸一口气
                SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.35f, Pitch = 0.4f }, player.Center);
            }
        }

        protected override void OnRoundLoaded(Item item, Player player, GsGunsEarlyPlayer mp, int roundIndex) {
            if (!VaultUtils.isServer) {
                //每口气归膛：轻哨音上行
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.1f + 0.12f * roundIndex }, player.Center);
            }
        }

        //==================== 后坐姿态：吹嘴轻推 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation -= new Vector2(player.direction, 0f) * (0.8f * progress);
            player.itemRotation += player.direction * 0.03f * progress;
        }

        //==================== 毒镖表现 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (router.MarkData < 1f || VaultUtils.isServer) {
                return;
            }
            bool envenomed = router.MarkData >= 2f;
            if (envenomed) {
                Lighting.AddLight(proj.Center, 0.08f, 0.22f, 0.06f);
            }
            int interval = envenomed ? 3 : 5;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.04f, envenomed ? VenomGreen : VenomDeep,
                    Main.rand.NextFloat(0.22f, 0.36f))
                    ?.Configure(VenomGreen, Main.rand.Next(8, 13), 0.1f, 0.6f);
            }
        }
    }

    /// <summary>
    /// 吹管专属本地态：毒引叠层。只在 owner 命中路径读写，不同步
    /// </summary>
    internal class GsBlowpipePlayer : ModPlayer
    {
        public int venomTarget = -1;    //毒引目标
        public int venomStacks;         //毒引层数
        public int venomWindow;         //失效窗口

        public override void PostUpdate() {
            if (venomWindow > 0 && --venomWindow == 0) {
                venomStacks = 0;
                venomTarget = -1;
            }
        }

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) {
            venomStacks = 0;
            venomTarget = -1;
            venomWindow = 0;
        }
    }
}
