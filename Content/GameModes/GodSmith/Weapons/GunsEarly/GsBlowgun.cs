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
    /// 吹箭筒「长息渐强」：漆木长吹筒·骨哨扣。<br/>
    /// ①长息：一口气连吹 5 镖，逐镖渐强（伤害与镖速递增，哨音随之上行）；
    /// ②第五镖「尽息」：重镖 + 命中炸开大毒雾；
    /// ③回气逐口可打断（吹几镖吸几口），站定回气快 25%；完美回气首镖即带第三镖的势。<br/>
    /// 吹嘴后坐：渐强段推得越来越沉。<br/>
    /// 账目：射速原版；渐强均值 ×1.18、尽息毒雾摊 +6%，伤害行 ×0.92 → 约 112%
    /// （待游戏内标定）
    /// </summary>
    internal class GsBlowgun : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Blowgun;

        protected override string GsDescFallback =>
            "Reforged: one long breath drives 5 darts, each flying harder and faster than the last.\n" +
            "The fifth dart empties the lungs: a heavy bolt that bursts into a broad toxic cloud on impact.\n" +
            "Breathe back one dart per gulp, faster while standing still; a sweet-spot breath starts you at third-dart strength";

        public override int MagSize => 5;
        public override int ReloadTicks => 48;
        public override GsReloadStyle Style => GsReloadStyle.Breath;
        protected override bool EjectsShell => false;

        private static readonly Color VenomGreen = new(130, 210, 110);
        private static readonly Color CrescendoGold = new(214, 226, 130);

        /// <summary>渐强段后坐随口气加深</summary>
        protected override float GetRecoil(bool lastRound) => lastRound ? 2f : 0.8f;

        /// <summary>站定回气 +25%</summary>
        protected override float ReloadRate(Player player)
            => player.velocity.LengthSquared() < 0.2f ? 1.25f : 1f;

        /// <summary>伤害行 ×0.92：渐强均值回缩，账目见类注释</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 0.92f;

        /// <summary>渐强序号（0..4，射前视角）：完美整匣从第 3 口起势</summary>
        private int BreathIndex(GsGunsEarlyPlayer mp)
            => Math.Clamp(MagSize - mp.magLeft + (mp.perfectMag ? 2 : 0), 0, 4);

        /// <summary>渐强序号（射后视角）：Fire* 时余弹已被共享层扣 1，故减一还原</summary>
        private int FiredIndex(GsGunsEarlyPlayer mp)
            => Math.Clamp(MagSize - mp.magLeft - 1 + (mp.perfectMag ? 2 : 0), 0, 4);

        /// <summary>完美奖励改整匣：本匣渐强从第 3 口起势</summary>
        protected override void OnPerfectReload(Item item, Player player, GsGunsEarlyPlayer mp) => mp.perfectMag = true;

        protected override void ModifyShot(Item item, Player player, GsGunsEarlyPlayer mp, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback, bool lastRound) {
            int i = BreathIndex(mp);
            damage = (int)(damage * (1f + 0.09f * i));
            velocity *= 1f + 0.06f * i;
            if (lastRound) {
                damage = (int)(damage * 1.15f);     //尽息重镖追加
            }
        }

        protected override bool? FireNormalRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            pendingMark = FiredIndex(mp) + 1f;      //1..5 档：渐强曳尾随档变密
            BreathPuff(mp, position, velocity, false);
            return null;
        }

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            pendingMark = 6f;                       //尽息档
            BreathPuff(mp, position, velocity, true);
            return null;
        }

        /// <summary>吹息音画：哨音随口气上行，尽息一声长叹</summary>
        private void BreathPuff(GsGunsEarlyPlayer mp, Vector2 position, Vector2 velocity, bool last) {
            if (VaultUtils.isServer) {
                return;
            }
            int i = FiredIndex(mp);
            SoundEngine.PlaySound(SoundID.Item63 with {
                Volume = 0.45f + i * 0.06f,
                Pitch = -0.25f + i * 0.13f
            }, position);
            Vector2 aim = velocity.SafeNormalize(Vector2.UnitX);
            int puffs = last ? 3 : 1;
            for (int k = 0; k < puffs; k++) {
                PRTLoader.NewParticle<PRT_Smoke>(position + aim * 6f,
                    aim.RotatedByRandom(0.2) * Main.rand.NextFloat(1f, 1.8f),
                    new Color(176, 188, 168), Main.rand.NextFloat(0.03f, 0.05f))
                    ?.Configure(Main.rand.Next(10, 16), 0.3f);
            }
        }

        //==================== 尽息毒雾（owner 端权威） ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (router.MarkData >= 2f) {
                target.AddBuff(BuffID.Poisoned, 60 + (int)router.MarkData * 30);
            }
            if (proj.owner != Main.myPlayer || router.MarkData < 6f) {
                return;
            }
            //尽息镖：大毒雾云（径 90，滞留）
            Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsGunsEarlyBurstProj>(),
                Math.Max(1, (int)(proj.damage * 0.75f)), 0f, proj.owner, 90f, 3f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.6f, Pitch = -0.25f }, target.Center);
                PRTLoader.NewParticle<PRT_DWave>(target.Center, Vector2.Zero,
                    VenomGreen * 0.7f, 0.15f)?.Configure(Vector2.One, 0f, 1.5f, 12);
            }
        }

        //==================== 回气音画 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.4f, Pitch = 0.2f }, player.Center);
            }
        }

        protected override void OnRoundLoaded(Item item, Player player, GsGunsEarlyPlayer mp, int roundIndex) {
            if (!VaultUtils.isServer) {
                //一口一镖归膛
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.05f + 0.1f * roundIndex }, player.Center);
            }
        }

        //==================== 后坐姿态：渐强推沉 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0 || !IsLocal(player)) {
                //渐强档位是本地节拍层，远端画基础推量即可
                BasePush(player, 1f);
                return;
            }
            BasePush(player, 1f + BreathIndex(State(player)) * 0.25f);
        }

        private static void BasePush(Player player, float depth) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation -= new Vector2(player.direction, 0f) * (0.8f * depth * progress);
            player.itemRotation += player.direction * 0.03f * progress;
        }

        //==================== 渐强曳尾表现 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (router.MarkData < 1f || VaultUtils.isServer) {
                return;
            }
            bool spent = router.MarkData >= 6f;
            int tier = spent ? 5 : (int)router.MarkData;
            //渐强曳尾：档位越高越密、越金
            int interval = Math.Max(2, 6 - tier);
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.04f,
                    Color.Lerp(VenomGreen, CrescendoGold, tier / 5f),
                    Main.rand.NextFloat(0.22f, 0.4f))
                    ?.Configure(spent ? CrescendoGold : VenomGreen, Main.rand.Next(8, 14), 0.12f, 0.65f);
            }
            if (spent) {
                Lighting.AddLight(proj.Center, 0.16f, 0.2f, 0.06f);
            }
        }
    }
}
