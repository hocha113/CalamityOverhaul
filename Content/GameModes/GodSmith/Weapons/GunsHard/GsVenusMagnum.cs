using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// 维纳斯万能枪重铸：藤蔓串刺节奏。<br/>
    /// [万能精准]：打出暴击后下一发穿透 +1，且瞄准线点亮 1 秒指示串刺窗口。<br/>
    /// [花瓣三重]：一次扳机 3 粒 ±4 度花瓣扇、各 70% 伤，仍只耗 1 发弹药。<br/>
    /// 高速弹转换身份两档保留
    /// </summary>
    internal class GsVenusMagnum : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.VenusMagnum;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to switch bloom\n" +
            "Magnum Focus: after a crit the next round pierces one extra foe, the aim line lights up for the window\n" +
            "Petal Triplet fires three petals in a fan for one bullet";

        /// <summary>串刺弹私有 flag</summary>
        private const int FlagPierce = 1;

        /// <summary>藤蔓翠绿</summary>
        private static readonly Color VineGreen = new(122, 228, 108);

        /// <summary>暴击已就绪的下一发穿透；只在 owner 路径读写（命中回调即 owner 端）</summary>
        private bool pierceReady;

        /// <summary>瞄准线点亮倒计时；只在 myPlayer 路径读写</summary>
        private int aimGlowTimer;

        /// <summary>本次射击为串刺弹的世界帧（打标窗口消费）</summary>
        private uint pierceShotTick = uint.MaxValue;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeFocus", EnName = "Magnum Focus",
                Converge = 0.5f,
                AimLine = GsAimLineKind.Line,
            },
            new GsFireMode {
                Key = "ModeTriplet", EnName = "Petal Triplet",
                DamageMul = 0.70f,
            },
        ];

        //==================== 精准档：暴击点亮串刺 ====================

        protected override void GsGunModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            if (mp.ModeIndex == 0 && pierceReady) {
                pierceReady = false;
                pierceShotTick = Main.GameUpdateCount;
            }
        }

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            if (mp.ModeIndex != 1) {
                return null;
            }
            //花瓣三重：3 粒 ±4 度扇（damage 已按 0.7 摊薄），共耗 1 弹
            for (int i = -1; i <= 1; i++) {
                Vector2 petalVel = velocity.RotatedBy(MathHelper.ToRadians(4f) * i);
                Projectile.NewProjectile(source, position, petalVel, type, damage, knockback, player.whoAmI);
            }
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(position + velocity.SafeNormalize(Vector2.UnitX) * 18f,
                        velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.4) * Main.rand.NextFloat(1.5f, 3f),
                        VineGreen, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(VineGreen, Main.rand.Next(12, 20));
                }
            }
            return false;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            GsGunsHardPlayer mp = Main.player[proj.owner].GetModPlayer<GsGunsHardPlayer>();
            bool piercing = pierceShotTick == Main.GameUpdateCount;
            router.MarkData = PackMark(mp.ModeIndex, piercing ? FlagPierce : 0);
            //穿透 +1：命中 owner 端裁决；>0 守卫防 -1 无限穿写坏
            if (piercing && proj.penetrate > 0) {
                proj.penetrate++;
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //串刺弹飞行相：翠绿花藤曳光（各端可见）
            if (MarkFlagOf(router.MarkData) != FlagPierce || VaultUtils.isServer) {
                return;
            }
            if (proj.timeLeft % 3 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.04f, VineGreen,
                    Main.rand.NextFloat(0.4f, 0.6f))?.Configure(VineGreen, Main.rand.Next(10, 16));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //只在攻击方端执行：暴击点亮下一发串刺（精准档专属）
            if (MarkModeOf(router.MarkData) != 0 || !hit.Crit || target.friendly) {
                return;
            }
            pierceReady = true;
            aimGlowTimer = 60;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.7f, Pitch = 0.5f }, target.Center);
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, VineGreen, 0.13f)?.Configure(8, 0.7f);
            }
        }

        protected override void GsGunHoldLocal(Item item, Player player, GsGunsHardPlayer mp) {
            if (aimGlowTimer > 0) {
                aimGlowTimer--;
            }
        }

        //==================== 瞄准线：暴击后点亮 1 秒 ====================

        public override bool AimLineVisible(Item item, Player player, GsGunsHardPlayer mp, GsFireMode mode)
            => mode.AimLine == GsAimLineKind.Line && aimGlowTimer > 0;

        public override Color AimLineColor(Item item, Player player, GsGunsHardPlayer mp) => VineGreen;

        internal override void GsGunHeldReset(Player player) {
            pierceReady = false;
            aimGlowTimer = 0;
            pierceShotTick = uint.MaxValue;
        }
    }
}
