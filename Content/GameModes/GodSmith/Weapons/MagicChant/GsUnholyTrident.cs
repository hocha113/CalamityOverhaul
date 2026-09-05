using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 邪恶三叉戟重铸：渎圣狱火。材质身份：恶魔仪仗的暗红三叉（渎圣狱火缠绕的仪仗戟）。<br/>
    /// ①「三叉正拍」：正拍戟弹裂成三叉（主叉 + 两侧 0.4 倍微扇）；<br/>
    /// ②满层强化「狱渊叉阵」：光标下方地底升起四道火叉波（自下而上 0.8 倍，错帧涌出）；<br/>
    /// ③命中挂狱火；④施法有掷矛后坐
    /// </summary>
    internal class GsUnholyTrident : GsChantScheme
    {
        public override int TargetItemID => ItemID.UnholyTrident;

        protected override string GsDescFallback =>
            "Reforged: on-beat casts split into a trident of three prongs that sear with hellfire\nAt full resonance the next cast raises four waves of infernal tridents from beneath your cursor";
        protected override float BaseDamageMult => 1.02f;

        /// <summary>私有形态：侧叉微扇</summary>
        private const float FormSideProng = 10f;
        /// <summary>私有形态：狱渊叉波（地底上涌）</summary>
        private const float FormPitWave = 11f;

        private static int TridentType => ProjectileID.UnholyTridentFriendly;

        //==================== 动画法：掷矛后坐 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //掷矛后坐：出手瞬间戟身后坐 4px 并上踢，随动画进度回坐（绝对剖面 0.12·p，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation -= new Vector2(player.direction, 0f) * (4f * progress);
            GsMagicKickMath.ApplyKickDiff(player, 0.12f * progress, 0.12f * ((player.itemAnimation + 1) / n));
        }

        //==================== 三叉正拍 / 狱渊叉阵 ====================

        protected override bool? ChantShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            if (chant.CurrentBeat == ChantBeat.OnBeat) {
                SpawnSideProngs(player, source, position, velocity, damage, knockback);
            }
            return null;
        }

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //强化咏唱：三叉照发，另在光标下方唤起狱渊叉阵；主戟带强化标照常刺出
            SpawnSideProngs(player, source, position, velocity, damage, knockback);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.85f, Pitch = -0.35f }, Main.MouseWorld);
            int waveDamage = Math.Max(1, (int)(damage * 0.8f));
            for (int i = 0; i < 4; i++) {
                //错帧涌出：起点越深到面越晚，四道波自下而上错开
                Vector2 pos = Main.MouseWorld + new Vector2(-66f + 44f * i, 70f + 28f * i);
                QueueForm(player, FormPitWave);
                int idx = Projectile.NewProjectile(source, pos, new Vector2(0f, -12f),
                    TridentType, waveDamage, knockback * 0.7f, player.whoAmI);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Projectile wave = Main.projectile[idx];
                    wave.tileCollide = false;
                    wave.timeLeft = 60;
                    wave.netUpdate = true;
                }
            }
            return null;
        }

        /// <summary>两侧 0.4 倍微扇侧叉（owner 端射击链内调用）</summary>
        private static void SpawnSideProngs(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int damage, float knockback) {
            int prongDamage = Math.Max(1, (int)(damage * 0.4f));
            for (int i = 0; i < 2; i++) {
                Vector2 vel = velocity.RotatedBy(i == 0 ? MathHelper.ToRadians(9f) : MathHelper.ToRadians(-9f));
                QueueForm(player, FormSideProng);
                int idx = Projectile.NewProjectile(source, position, vel,
                    TridentType, prongDamage, knockback * 0.5f, player.whoAmI);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].scale *= 0.75f;
                    Main.projectile[idx].netUpdate = true;
                }
            }
        }

        //==================== 命中 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != TridentType) {
                return;
            }
            bool wave = router.MarkData == FormPitWave;
            if (!VaultUtils.isServer && wave) {
                //叉波命中更重的音效反馈
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 3 }, target.Center);
            }
            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            //狱火（OnFire3，TML 源已证 BuffID=323）：叉波灼得更久
            target.AddBuff(BuffID.OnFire3, wave ? 240 : router.MarkData == FormSideProng ? 120 : 150);
        }
    }
}
