using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard.Specials
{
    /// <summary>
    /// 毒鳔枪重铸（L2 弹幕增强）：免弹药保留，原版毒泡不换载体。<br/>
    /// [毒泡流] 原版速射；[脓疱] 用时约三倍憋一颗大脓疱（×2.4 伤），
    /// 命中裂成 6 枚孢子泡（各 25%，子弹幕承签防递归）并留 90px 毒雾云 2 秒
    /// </summary>
    internal class GsToxikarp : GodSmithScheme
    {
        public override int TargetItemID => ItemID.Toxikarp;

        public override string GsFamily => "GunsSpecial";

        protected override string GsDescFallback =>
            "Reforged: two spit modes. Bubble Stream is the classic rapid fire; Pustule charges one slow bloated bubble (x2.4) that bursts into 6 spore bubbles and a lingering toxic cloud"
            + "\nRight click to switch modes. No ammo, as always";

        //毒液色板
        internal static readonly Color ToxinBright = new(196, 255, 120);
        internal static readonly Color ToxinMain = new(120, 200, 70);
        internal static readonly Color ToxinDeep = new(60, 110, 46);

        /// <summary>模式名（[0]=毒泡流 [1]=脓疱）</summary>
        internal static LocalizedText[] ModeNames;

        /// <summary>MarkData 语义：0 原版泡 / 1 脓疱大泡 / 2 孢子泡</summary>
        private const float MarkPustule = 1f;
        private const float MarkSpore = 2f;

        //以下瞬时字段只在本地玩家路径消费（方案单例的 owner 契约）
        private int mode;
        private int switchCd;
        private float pendingMark;

        public override void GsSetStaticDefaults() {
            ModeNames = [
                this.GetLocalization("Mode0", () => "Bubble Stream"),
                this.GetLocalization("Mode1", () => "Pustule"),
            ];
        }

        public override bool? GsAltFunctionUse(Item item, Player player) => true;

        public override bool? GsCanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                if (player.whoAmI == Main.myPlayer && switchCd <= 0) {
                    switchCd = 12;
                    mode = mode == 0 ? 1 : 0;
                    GsGunPose.ModeSwitchFeedback(player, ModeNames[mode].Value);
                }
                return false;
            }
            return null;
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (switchCd > 0) {
                switchCd--;
            }
        }

        public override float GsUseSpeedMultiplier(Item item, Player player) {
            if (player.whoAmI == Main.myPlayer && mode == 1) {
                return 0.34f;//脓疱档用时约三倍
            }
            return 1f;
        }

        public override void GsModifyShootStats(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            if (mode == 1) {
                damage = (int)(damage * 2.4f);
                velocity *= 0.8f;
                pendingMark = MarkPustule;
            }
            else {
                pendingMark = 0f;
            }
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (mode == 1 && !VaultUtils.isServer) {
                //憋出脓疱的浊响
                SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.7f, Pitch = -0.5f }, position);
            }
            return null;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            router.MarkData = pendingMark;
            if (pendingMark == MarkPustule) {
                //owner 端权威扩碰撞箱；远端 hitbox 维持原状不参与判定
                proj.Resize(34, 34);
            }
        }

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            //脓疱裂出的子泡降为孢子标记，孢子不再二次裂解（防递归）
            if (proj.type == ProjectileID.ToxicBubble && parentRouter.MarkData == MarkPustule) {
                router.MarkData = MarkSpore;
                proj.timeLeft = Math.Min(proj.timeLeft, 50);
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.ToxicBubble) {
                return;
            }
            if (router.MarkData == MarkPustule) {
                //大泡鼓胀与浮沉，读作一颗随时要破的脓疱
                proj.scale = MathF.Min(proj.scale + 0.04f, 1.9f);
                proj.velocity.Y += MathF.Sin(Main.GameUpdateCount * 0.19f + proj.identity * 0.9f) * 0.05f;
                Lighting.AddLight(proj.Center, ToxinMain.ToVector3() * 0.35f);
                if (!VaultUtils.isServer && proj.timeLeft % 8 == 0) {
                    PRTLoader.NewParticle<PRT_ToxicBubble>(proj.Center + Main.rand.NextVector2Circular(10f, 10f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f), ToxinMain,
                        Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 26));
                }
            }
            else if (router.MarkData == MarkSpore) {
                proj.scale = 0.72f;
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.ToxicBubble || router.MarkData != MarkPustule) {
                return;
            }
            //破裂声与毒雾（演出各端，生成只 owner）
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.8f, Pitch = -0.2f }, proj.Center);
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_ToxicMist>(proj.Center + Main.rand.NextVector2Circular(14f, 14f),
                        Main.rand.NextVector2Circular(1.2f, 1.2f), ToxinDeep,
                        Main.rand.NextFloat(0.7f, 1.1f))?.Configure(Main.rand.Next(30, 50));
                }
            }
            if (proj.owner != Main.myPlayer) {
                return;
            }
            //6 枚孢子泡放射（同型弹幕，承签自动降为孢子标记）
            int sporeDamage = Math.Max(1, (int)(proj.damage * 0.25f));
            for (int i = 0; i < 6; i++) {
                Vector2 vel = (MathHelper.TwoPi * i / 6f + Main.rand.NextFloat(-0.2f, 0.2f)).ToRotationVector2()
                    * Main.rand.NextFloat(3.5f, 5.5f);
                Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, vel,
                    ProjectileID.ToxicBubble, sporeDamage, 0.5f, proj.owner);
            }
            //滞留毒雾云
            Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, Vector2.Zero,
                ModContent.ProjectileType<GsToxinCloudProj>(),
                Math.Max(1, (int)(proj.damage * 0.25f)), 0f, proj.owner);
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;//基线补偿，综合 DPS 落在原版 108%~112%
    }

    /// <summary>
    /// 脓疱毒雾云：90px 滞留 2 秒的踩踏毒场，缓慢鼓动，命中挂中毒
    /// </summary>
    internal class GsToxinCloudProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float Seed => Projectile.identity * 0.433f % 1f;

        public override void SetDefaults() {
            Projectile.width = 150;
            Projectile.height = 110;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, GsToxikarp.ToxinDeep.ToVector3() * 0.3f);
            if (!VaultUtils.isServer && Main.GameUpdateCount % 8 == Projectile.identity % 8) {
                PRTLoader.NewParticle<PRT_ToxicMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(60f, 40f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.6f),
                    GsToxikarp.ToxinDeep, Main.rand.NextFloat(0.8f, 1.2f))?.Configure(Main.rand.Next(26, 40));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Poisoned, 240);

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog == null) {
                return false;
            }
            float fade = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f)
                * MathHelper.Clamp((120 - Projectile.timeLeft) / 12f, 0f, 1f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            //双层慢旋毒雾（Fog 真 alpha），相位反向防贴纸感
            float rot = Main.GlobalTimeWrappedHourly * 0.4f + Seed * MathHelper.TwoPi;
            Main.EntitySpriteDraw(fog, drawPos, null, GsToxikarp.ToxinDeep * (0.55f * fade), rot,
                fog.Size() / 2f, 0.9f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(fog, drawPos + new Vector2(10f, -6f), null,
                GsToxikarp.ToxinMain * (0.3f * fade), -rot * 0.7f,
                fog.Size() / 2f, 0.65f, SpriteEffects.FlipHorizontally, 0);
            return false;
        }
    }
}
