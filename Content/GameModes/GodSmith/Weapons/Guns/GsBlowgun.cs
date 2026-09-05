using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 吹箭筒「长息渐强」：漆木长吹筒·骨哨扣。<br/>
    /// ①长息：一口气连吹 5 镖，逐镖渐强（伤害与镖速递增，哨音随之上行）；
    /// ②第五镖「尽息」：重镖 + 命中炸开大毒雾；
    /// ③回气逐口可打断（吹几镖吸几口），站定回气快 25%。<br/>
    /// 吹嘴后坐：渐强段推得越来越沉。<br/>
    /// 账目：射速原版；渐强均值 ×1.18、尽息毒雾摊 +6%，伤害行 ×0.92 → 约 112%
    /// （待游戏内标定）
    /// </summary>
    internal class GsBlowgun : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Blowgun;

        protected override string GsDescFallback =>
            "Reforged: one long breath drives 5 darts, each flying harder and faster than the last.\nThe fifth dart empties the lungs: a heavy bolt that bursts into a broad toxic cloud on impact.\nBreathe back one dart per gulp, faster while standing still; a sweet-spot breath starts you at third-dart strength";
        public override int MagSize => 5;
        public override int ReloadTicks => 48;
        public override GsReloadStyle Style => GsReloadStyle.Breath;

        /// <summary>渐强段后坐随口气加深</summary>
        protected override float GetRecoil(bool lastRound) => lastRound ? 2f : 0.8f;

        /// <summary>站定回气 +25%</summary>
        protected override float ReloadRate(Player player)
            => player.velocity.LengthSquared() < 0.2f ? 1.25f : 1f;

        /// <summary>伤害行 ×0.92：渐强均值回缩，账目见类注释</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 0.92f;

        /// <summary>渐强序号（0..4，射前视角）</summary>
        private int BreathIndex(GsGunsEarlyPlayer mp)
            => Math.Clamp(MagSize - mp.magLeft, 0, 4);

        /// <summary>渐强序号（射后视角）：Fire* 时余弹已被共享层扣 1，故减一还原</summary>
        private int FiredIndex(GsGunsEarlyPlayer mp)
            => Math.Clamp(MagSize - mp.magLeft - 1, 0, 4);

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
            pendingMark = FiredIndex(mp) + 1f;      //1..5 档：渐强档位（毒时长随档）
            BreathPuff(mp, position);
            return null;
        }

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            pendingMark = 6f;                       //尽息档
            BreathPuff(mp, position);
            return null;
        }

        /// <summary>吹息音效：哨音随口气上行</summary>
        private void BreathPuff(GsGunsEarlyPlayer mp, Vector2 position) {
            if (VaultUtils.isServer) {
                return;
            }
            int i = FiredIndex(mp);
            SoundEngine.PlaySound(SoundID.Item63 with {
                Volume = 0.45f + i * 0.06f,
                Pitch = -0.25f + i * 0.13f
            }, position);
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

        //==================== 后坐姿态：渐强推沉（差分；负踢=管口下压的设计语义） ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            //渐强档位是本地节拍层，远端画基础推量即可（深度只走位移轴，踢幅不变）
            float depth = IsLocal(player) ? 1f + BreathIndex(State(player)) * 0.25f : 1f;
            GunKickStyle(player, 0.8f * depth, -0.03f);
        }
    }
}
