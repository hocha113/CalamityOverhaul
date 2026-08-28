using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【僵尸臂】材质：还没僵透的腐烂手臂。签名：①乱抡：每一拍的时长与挥弧
    /// 按连段序号伪随机摇摆 ±20%，抡起来歪歪扭扭没个准头 ②命中 12% 概率甩目标
    /// 一脸腐液上缓速 ③挥动与命中都掉腐屑
    /// </summary>
    internal class GsZombieArm : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.ZombieArm;

        protected override int HeldProjID => ModContent.ProjectileType<GsZombieArmHeld>();

        protected override string GsDescFallback =>
            "Reforged: flails with the sloppy rhythm of the undead, no two swings alike; " +
            "rotten splatter may briefly confuse whatever it hits";

        //腐臂色板
        internal static readonly Color RotBright = new(182, 202, 158); //腐皮灰绿
        internal static readonly Color RotMain = new(112, 132, 96);    //烂肉暗绿
        internal static readonly Color RotHot = new(154, 224, 110);    //腐液荧绿
        internal static readonly Color RotDeep = new(32, 38, 26);      //尸斑暗色

        //公认弱势趣味武器，包络放宽到 130%：底伤 +20%，
        //终结拍 1.25x + 12% 缓速（控制收益小），综合 DPS 约为原版 122%~128%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.2f;

        /// <summary>照抄基类实现，多传 ai[2]=连段序号做乱抡种子（随生成包过线，各端一致）</summary>
        public override bool? GsCanUseItem(Item item, Player player) {
            if (player.ownedProjectileCounts[HeldProjID] > 0) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                int beat = comboCounter % ComboBeats;
                float swingSign = comboCounter % 2 == 0 ? 1f : -1f;
                ModifyLocalSwing(item, player, ref beat, ref swingSign);
                comboCounter++;
                comboResetTimer = ComboResetFrames;
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    HeldProjID, player.GetWeaponDamage(item), item.knockBack, player.whoAmI,
                    beat, swingSign, comboCounter);
            }
            return false;
        }
    }

    /// <summary>
    /// 僵尸臂手持：三拍乱抡。OnStageInit 用 ai[2] 做种子把各相时长与
    /// RaiseBack/Follow 扰动 ±20%（同一 ai[2] 各端算出同一套拍子）。
    /// ai[0]=拍号 ai[1]=交替符号 ai[2]=乱抡种子
    /// </summary>
    internal class GsZombieArmHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.ZombieArm;
        protected override Color EdgeBright => GsZombieArm.RotBright;
        protected override Color BodyMain => GsZombieArm.RotMain;
        protected override Color HotAccent => GsZombieArm.RotHot;
        protected override Color DeepShadow => GsZombieArm.RotDeep;

        /// <summary>迷乱时长 0.7s（Slow 对 NPC 无效，Confused 是真实生效的原版路径）</summary>
        private const int ConfuseFrames = 42;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //终结重抡：连僵尸都知道最后要用点劲
                return new GsBroadBeat {
                    Raise = 8, Hold = 2, Slash = 5, Recover = 11,
                    RaiseBack = 2.1f, Follow = 1.3f, ReachScale = 1.1f, LeanAmp = 0.08f,
                    DamageMult = 1.25f, Hitstop = 2, LungeSpeed = 2.4f, SwingPitch = -0.35f,
                };
            }
            GsBroadBeat b = GsBroadBeat.Standard;
            b.SwingPitch = -0.2f;//闷沉的烂肉抡动声
            return b;
        }

        /// <summary>乱抡种子伪随机 0~1（ai[2]+salt 播种，同一拍各端一致）</summary>
        private float SwayRand01(int salt) {
            uint h = (uint)((int)Projectile.ai[2] * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>乱抡：各相时长与挥弧扰动 ±20%；总时长跟着重算</summary>
        protected override void OnStageInit() {
            base.OnStageInit();
            float Sway(int salt) => 0.8f + 0.4f * SwayRand01(salt);
            raiseDur = Math.Max(1, (int)MathF.Round(raiseDur * Sway(1)));
            holdDur = Math.Max(1, (int)MathF.Round(holdDur * Sway(2)));
            slashDur = Math.Max(1, (int)MathF.Round(slashDur * Sway(3)));
            recoverDur = Math.Max(2, (int)MathF.Round(recoverDur * Sway(4)));
            raiseBack *= Sway(5);
            follow *= Sway(6);
            totalDur = raiseDur + holdDur + slashDur + recoverDur;
        }

        /// <summary>12% 概率甩一脸腐液使其迷乱（owner 端命中路径掷点，AddBuff 自带同步）</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.rand.NextFloat() < 0.12f) {
                target.AddBuff(BuffID.Confused, ConfuseFrames);
                if (!VaultUtils.isServer) {
                    //腐液糊上去的反馈
                    PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                        GsZombieArm.RotHot, 0.24f)?.Configure(12, 0.8f);
                    for (int i = 0; i < 6; i++) {
                        Dust d = Dust.NewDustPerfect(target.Center, DustID.Corruption,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f), 80, default,
                            Main.rand.NextFloat(1f, 1.5f));
                        d.noGravity = true;
                    }
                }
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            //抡动时掉腐屑，烂手臂就是会掉渣
            if (phase == PhaseSlash && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.4f, 1f)),
                    DustID.Corruption, Vector2.Zero, 100, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.velocity = (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2() * 1.2f;
                d.noGravity = false;
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //腐屑迸溅
            int bits = IsFinisher ? 7 : 4;
            for (int i = 0; i < bits; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Corruption,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 90, default,
                    Main.rand.NextFloat(0.8f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
        }
    }
}
