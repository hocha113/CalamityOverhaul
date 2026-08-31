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
    /// 镖步枪重铸（L1 弹口层 + 蓄力时序闸）：镖种特效完整保留。<br/>
    /// [贯穿蓄射] 按住蓄力（瞄准线渐显），松开放镖：满蓄 60 tick 时镖速 ×2、穿透 +2、伤害 +50%，
    /// 未满按比例给部分加成。原版 use 流程被压掉，发射由松开触发、手动走 PickAmmo（1 镖）；<br/>
    /// [三镖扇] 一次 use 打出 ±6° 三镖各 60% 伤（共耗 1 镖），节拍放缓到 1.25 倍
    /// </summary>
    internal class GsDartRifle : GodSmithScheme
    {
        public override int TargetItemID => ItemID.DartRifle;

        public override string GsFamily => "GunsSpecial";

        protected override string GsDescFallback =>
            "Reforged: two firing modes. Pierce Draw charges while held (aim line fades in) and looses on release, a full 1s draw gives x2 dart speed, +2 pierce and +50% damage; Triple Fan spits 3 darts at 60% each for a single dart"
            + "\nRight click to switch modes. Dart ammo effects are fully preserved";

        /// <summary>模式名（[0]=贯穿蓄射 [1]=三镖扇）</summary>
        internal static LocalizedText[] ModeNames;

        /// <summary>满蓄所需帧数</summary>
        private const int ChargeFull = 60;

        //以下瞬时字段全部只在本地玩家路径消费（方案单例的 owner 契约）
        private int mode;
        private int switchCd;
        private int charge;
        private uint lastHoldTick;
        private float pendingChargeRatio;

        public override void GsSetStaticDefaults() {
            ModeNames = [
                this.GetLocalization("Mode0", () => "Pierce Draw"),
                this.GetLocalization("Mode1", () => "Triple Fan"),
            ];
        }

        public override bool? GsAltFunctionUse(Item item, Player player) => true;

        public override bool? GsCanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                if (player.whoAmI == Main.myPlayer && switchCd <= 0) {
                    switchCd = 12;
                    mode = mode == 0 ? 1 : 0;
                    charge = 0;
                    GsGunPose.ModeSwitchFeedback(player, ModeNames[mode].Value);
                }
                return false;
            }
            //蓄射档压掉原版 use：发射改由「松开」在 GsHoldItem 里触发
            if (mode == 0) {
                return false;
            }
            return null;
        }

        public override float GsUseSpeedMultiplier(Item item, Player player) {
            //三镖扇多弹头，节拍放缓找平总账；远端动画差异可接受
            if (player.whoAmI == Main.myPlayer && mode == 1) {
                return 0.8f;
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
            //断持重置：换走武器再切回来，残留的蓄力作废
            if (Main.GameUpdateCount - lastHoldTick > 2) {
                charge = 0;
            }
            lastHoldTick = Main.GameUpdateCount;

            if (mode != 0) {
                return;
            }
            //蓄射档压掉 use 流，itemAnimation 恒 0 时原版不绘制枪体：持枪姿态件常驻补位
            //（蓄力与静息全程枪在手；蓄力比映射后倾属演出升级，冻结待后续波）
            GsGunHoldPoseProj.Ensure(player, TargetItemID, 0f);
            if (player.controlUseItem && !player.mouseInterface) {
                int prev = charge;
                charge = Math.Min(charge + 1, ChargeFull);
                if (prev < ChargeFull && charge >= ChargeFull && !VaultUtils.isServer) {
                    //满蓄咔哒
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = 0.6f }, player.Center);
                }
                if (charge > 8) {
                    DrawChargeAimLine(player, charge / (float)ChargeFull);
                }
            }
            else if (charge > 0) {
                int released = charge;
                charge = 0;
                if (released >= 4) {
                    FireChargedDart(item, player, released);
                }
            }
        }

        /// <summary>蓄力瞄准线：owner 本地粒子沿瞄准向铺点，随蓄力比渐远渐亮（个人读数）</summary>
        private static void DrawChargeAimLine(Player player, float ratio) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 aim = GsAimUnit(player);
            Vector2 muzzle = player.Center + aim * 26f;
            float reach = MathHelper.Lerp(90f, 620f, ratio);
            for (int i = 0; i < 2; i++) {
                float d = Main.rand.NextFloat(0.05f, 1f);
                PRTLoader.NewParticle<PRT_Spark>(muzzle + aim * (reach * d),
                    aim * 0.2f, new Color(170, 235, 120) * ((0.4f + 0.6f * ratio) * (1f - d * 0.6f)),
                    0.2f)?.Configure(false, 6);
            }
        }

        /// <summary>松开放镖：手动走 PickAmmo 原版弹药链（1 镖），按蓄力比给加成</summary>
        private void FireChargedDart(Item item, Player player, int released) {
            if (!player.PickAmmo(item, out int projType, out float speed, out int damage,
                out float knockback, out int usedAmmoID, false)) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item16 with { Volume = 0.4f, Pitch = -0.4f }, player.Center);
                }
                return;
            }
            float ratio = MathHelper.Clamp(released / (float)ChargeFull, 0f, 1f);
            pendingChargeRatio = ratio;
            Vector2 aim = GsAimUnit(player);
            Vector2 vel = aim * speed * (1f + ratio);
            int dmg = (int)(damage * (1f + 0.5f * ratio));
            Projectile.NewProjectile(player.GetSource_ItemUse_WithPotentialAmmo(item, usedAmmoID),
                player.Center + aim * 20f, vel, projType, dmg, knockback, player.whoAmI);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item98 with {
                    Volume = 0.55f + 0.25f * ratio,
                    Pitch = -0.1f + 0.35f * ratio
                }, player.Center);
            }
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (mode != 1) {
                return null;
            }
            //三镖扇：一次 use 共耗 1 镖，多弹头伤害摊薄
            pendingChargeRatio = 0f;
            int fanDamage = Math.Max(1, (int)(damage * 0.6f));
            for (int i = -1; i <= 1; i++) {
                Vector2 vel = velocity.RotatedBy(i * MathHelper.ToRadians(6f));
                Projectile.NewProjectile(source, position, vel, type, fanDamage, knockback, player.whoAmI);
            }
            return false;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            router.MarkData = pendingChargeRatio;
            //满蓄贯穿 +2，带 >0 守卫防 -1 无限穿被写坏
            if (pendingChargeRatio >= 0.99f && proj.penetrate > 0) {
                proj.penetrate += 2;
            }
            pendingChargeRatio = 0f;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            float ratio = router.MarkData;
            if (ratio < 0.35f || VaultUtils.isServer) {
                return;
            }
            //蓄力镖的贯穿光尾，强度随蓄力比
            Lighting.AddLight(proj.Center, new Vector3(0.3f, 0.42f, 0.16f) * ratio);
            int interval = ratio >= 0.99f ? 1 : 3;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_Spark>(
                    proj.Center - proj.velocity * 0.25f,
                    -proj.velocity * 0.04f, new Color(190, 250, 130),
                    Main.rand.NextFloat(0.25f, 0.45f) * ratio)?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (router.MarkData < 0.99f || VaultUtils.isServer) {
                return;
            }
            //满蓄贯穿命中的酸液穿刺迸溅（个人反馈层）
            Vector2 through = proj.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_AcidSplash>(target.Center + through * 8f,
                    through.RotatedByRandom(0.35) * Main.rand.NextFloat(3f, 6.5f),
                    new Color(190, 250, 130), Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(14, 22));
            }
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;//基线补偿，综合 DPS 落在原版 108%~112%
    }
}
