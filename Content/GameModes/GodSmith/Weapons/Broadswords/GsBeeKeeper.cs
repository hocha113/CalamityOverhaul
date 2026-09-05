using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【蜂蜡蜂刃】材质：蜂巢蜡髓浇铸的黄黑条纹蜂刃。签名「蜂群协奏」：
    /// ①每记命中自蜂蜡刃口放出 1~2 只护巢蜂 ②第四拍「蜂拥」，爆发时三蜂齐射
    /// ③命中糊上蜂蜡黏浆（Slimed）
    /// </summary>
    internal class GsBeeKeeper : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BeeKeeper;

        protected override int HeldProjID => ModContent.ProjectileType<GsBeeKeeperHeld>();

        protected override int ComboBeats => 4;

        protected override string GsDescFallback =>
            "Reforged: every strike releases hive bees from the waxen edge; the fourth swing gathers three bee shades around the blade and looses them all at once, and wounds drip sticky wax";
        internal static readonly Color WaxBright = new(255, 232, 160);  //蜡髓淡金
        internal static readonly Color WaxMain = new(224, 170, 62);     //蜂蜜琥珀
        internal static readonly Color WaxHot = new(255, 202, 70);      //蜜金强调

        //底乘 1.0：蜂收益就是主预算——每命中 1~2 蜂（期望 1.5）×前三拍 + 蜂拥 3 蜂
        //≈ 7.5 蜂/循环（原版命中期望 2 蜂×3 挥 = 6），单蜂约 1/3 底伤；
        //近战终结仅 1.15x，蜂蜡黏浆为纯演出层，综合 DPS 约为原版 105%~115%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1f;
    }

    /// <summary>
    /// 蜂蜡蜂刃手持：四拍蜂群连击。0/1/2 轻快短扫（蜂翅般的碎步节奏），
    /// 3 蜂拥终结（爆发三蜂齐射+小前压）。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBeeKeeperHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BeeKeeper;
        protected override Color EdgeBright => GsBeeKeeper.WaxBright;
        protected override Color BodyMain => GsBeeKeeper.WaxMain;
        protected override Color HotAccent => GsBeeKeeper.WaxHot;

        protected override int BeatCount => 4;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 3) {
                //蜂拥终结：举刀蜂影绕刃读拍，爆发齐射带小前压
                return new GsBroadBeat {
                    Raise = 8, Hold = 4, Slash = 4, Recover = 10,
                    RaiseBack = 2.05f, Follow = 1.2f, ReachScale = 1.1f, LeanAmp = 0.07f,
                    DamageMult = 1.15f, Hitstop = 2, LungeSpeed = 2.0f, SwingPitch = -0.2f,
                };
            }
            //蜂翅碎步：三记轻快短扫，音高错落如振翅
            return new GsBroadBeat {
                Raise = stage == 2 ? 5 : 4, Hold = 1, Slash = 3, Recover = stage == 2 ? 7 : 6,
                RaiseBack = 1.7f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = stage switch { 0 => 0.05f, 1 => 0.13f, _ => -0.04f },
            };
        }

        /// <summary>owner 端放一只原版护巢蜂（beeType/beeDamage/beeKB 走原版换算，保留强化蜂特性）</summary>
        private void ReleaseBee(Vector2 pos, Vector2 vel, int baseDamage) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            //beeType 会掷 Main.rand 并写 makeStrongBee，须与 beeDamage/beeKB 同端连读
            int type = Owner.beeType();
            int idx = SpawnOwnedProj(type, pos, vel, Owner.beeDamage(baseDamage / 3), Owner.beeKB(0f));
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].DamageType = DamageClass.Melee;//镜像原版蜂刀的近战蜂
                Main.projectile[idx].netUpdate = true;
            }
        }

        protected override void HandlePhaseEvents(int phase) {
            base.HandlePhaseEvents(phase);
            //蜂拥蓄力：环刃蜂鸣的嗡嗡声底（Item97 为原版蜂械射音，压低作振翅底噪）
            if (IsFinisher && phase == PhaseRaise && timer == 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.3f, Pitch = -0.35f }, Owner.Center);
            }
        }

        protected override void OnSlashBegin() {
            if (!IsFinisher) {
                return;
            }
            //蜂拥：三只真蜂沿出手向扇形齐射
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
            for (int i = 0; i < 3; i++) {
                Vector2 vel = (baseAngle + (i - 1) * 0.32f).ToRotationVector2() * 6.5f;
                ReleaseBee(Vector2.Lerp(Hand, mainTip, 0.6f), vel, baseDamage);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.6f, Pitch = 0.15f }, Owner.Center);
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            //蜂蜡黏浆：Slow 对 NPC 无效，改挂 Slimed（真实滴淌演出，原版 buff 自带同步）
            target.AddBuff(BuffID.Slimed, 40);
            //命中放蜂 1~2 只（owner 端掷数，生成包同步）
            if (Owner.whoAmI == Main.myPlayer) {
                int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
                int count = Main.rand.Next(1, 3);
                for (int i = 0; i < count; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 2.4f);
                    ReleaseBee(target.Center + Main.rand.NextVector2Circular(8f, 8f), vel, baseDamage);
                }
            }
        }
    }
}
