using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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
    /// 镖枪重铸（L1 弹口层）：双档射击模式，镖种特效（诅咒/灵液/水晶镖）完整保留。<br/>
    /// [双针点射] 两连快镖压缩节拍（第 2 支 70% 伤），随后强制间歇，每发真实耗镖；<br/>
    /// [静息狙杀] 站定 0.8 秒蓄成伏击，下一镖 +60% 伤且镖速翻倍，移动即清
    /// </summary>
    internal class GsDartPistol : GodSmithScheme
    {
        public override int TargetItemID => ItemID.DartPistol;

        public override string GsFamily => "GunsSpecial";

        protected override string GsDescFallback =>
            "Reforged: two firing modes. Twin Sting fires quick two-round bursts (second dart 70%); Still Fang rewards standing still 0.8s with a +60% double-speed ambush dart"
            + "\nRight click to switch modes. Dart ammo effects are fully preserved";

        /// <summary>模式名（[0]=双针点射 [1]=静息狙杀）</summary>
        internal static LocalizedText[] ModeNames;

        /// <summary>静息蓄满所需帧数（0.8 秒）</summary>
        private const int StillNeed = 48;
        /// <summary>双针打完后的强制间歇</summary>
        private const int BurstGap = 12;

        //以下瞬时字段全部只在本地玩家路径消费（方案单例的 owner 契约）
        private int mode;
        private int switchCd;
        private int burstStep;
        private int burstGapTimer;
        private int stillTimer;
        private uint lastShotTick;
        private bool pendingStealth;

        public override void GsSetStaticDefaults() {
            ModeNames = [
                this.GetLocalization("Mode0", () => "Twin Sting"),
                this.GetLocalization("Mode1", () => "Still Fang"),
            ];
        }

        public override bool? GsAltFunctionUse(Item item, Player player) => true;

        public override bool? GsCanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                if (player.whoAmI == Main.myPlayer && switchCd <= 0) {
                    switchCd = 12;
                    mode = mode == 0 ? 1 : 0;
                    burstStep = 0;
                    stillTimer = 0;
                    GsGunPose.ModeSwitchFeedback(player, ModeNames[mode].Value);
                }
                return false;
            }
            //双针档：两发打完后的节拍器强制间歇
            if (mode == 0 && player.whoAmI == Main.myPlayer && burstGapTimer > 0) {
                return false;
            }
            return null;
        }

        public override float GsUseSpeedMultiplier(Item item, Player player) {
            //只对本地玩家的双针档加速；远端动画差异由弹幕生成自然呈现
            if (player.whoAmI == Main.myPlayer && mode == 0) {
                return 1.75f;
            }
            return 1f;
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (switchCd > 0) {
                switchCd--;
            }
            if (burstGapTimer > 0) {
                burstGapTimer--;
            }
            //断手回拍：第二针迟迟不来就取消半截连发
            if (mode == 0 && burstStep == 1 && Main.GameUpdateCount - lastShotTick > 30) {
                burstStep = 0;
            }
            if (mode == 1) {
                bool still = player.velocity.LengthSquared() < 0.5f;
                int prev = stillTimer;
                stillTimer = still ? Math.Min(stillTimer + 1, StillNeed) : 0;
                //蓄满瞬间的伏击反馈：轻响 + 枪口毒芒（个人读数）
                if (prev < StillNeed && stillTimer >= StillNeed && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = 0.5f }, player.Center);
                    Vector2 muzzle = player.Center + GsAimUnit(player) * 30f;
                    PRTLoader.NewParticle<PRT_Sparkle>(muzzle, Vector2.Zero,
                        new Color(150, 240, 110), 0.7f)?.Configure(new Color(190, 255, 150), 14, 0.05f);
                }
            }
        }

        public override void GsModifyShootStats(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            if (mode == 0) {
                if (burstStep == 1) {
                    damage = (int)(damage * 0.7f);
                }
            }
            else if (stillTimer >= StillNeed) {
                damage = (int)(damage * 1.6f);
                velocity *= 2f;
                pendingStealth = true;
            }
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            lastShotTick = Main.GameUpdateCount;
            if (mode == 0) {
                burstStep++;
                if (burstStep >= 2) {
                    burstStep = 0;
                    burstGapTimer = BurstGap;
                }
            }
            else if (pendingStealth && !VaultUtils.isServer) {
                //伏击蛇袭出手音
                SoundEngine.PlaySound(SoundID.Item63 with { Volume = 0.6f, Pitch = 0.2f }, position);
            }
            if (mode == 1) {
                stillTimer = 0;
            }
            return null;//原版镖直通，交给路由打标
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            router.MarkData = pendingStealth ? 1f : 0f;
            pendingStealth = false;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (router.MarkData < 0.5f || VaultUtils.isServer) {
                return;
            }
            //伏击镖的高速毒芒曳光
            Lighting.AddLight(proj.Center, new Vector3(0.25f, 0.4f, 0.15f));
            if (proj.timeLeft % 2 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(
                    proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.05f, new Color(150, 240, 110),
                    Main.rand.NextFloat(0.25f, 0.4f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (router.MarkData < 0.5f || VaultUtils.isServer) {
                return;
            }
            //伏击命中的毒液迸溅（个人反馈层）
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_AcidSplash>(target.Center,
                    (-proj.velocity).SafeNormalize(Vector2.UnitY).RotatedByRandom(0.8) * Main.rand.NextFloat(2f, 5f),
                    new Color(150, 240, 110), Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(14, 24));
            }
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;//基线补偿，综合 DPS 落在原版 108%~112%
    }
}
