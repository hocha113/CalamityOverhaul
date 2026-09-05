using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 钴蓝薙刀重铸：薙斩变式。<br/>
    /// 材质：淬火钴钢刀刃。签名行为：①两拍交替——奇拍直线突刺，偶拍走基类横扫时间线，
    /// 一记横扫弧斩（角度扫掠 + 弧线采样判定，几何与突刺完全不同）
    /// ②扫斩命中钴钢脆响
    /// </summary>
    internal class GsCobaltNaginata : GsSpearScheme
    {
        public override int TargetItemID => ItemID.CobaltNaginata;

        protected override string GsDescFallback =>
            "Reforged: alternating polework, a straight thrust then a wide cobalt sweep;\nthe sweep carves an arc where the thrust line cannot reach";
        protected override int HeldProjType => ModContent.ProjectileType<GsCobaltNaginataHeld>();

        protected override int ComboBeats => 2;

        /// <summary>扫拍的扫向交替符号：第 1、3、5…次横扫上下轮换</summary>
        protected override float SpawnAi1(Item item, Player player)
            => (comboCounter - 1) / 2 % 2 == 0 ? 1f : -1f;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;//横扫覆盖面即机制收益，底伤小补，综合 DPS 落在原版 105%~115%
    }

    /// <summary>
    /// 钴蓝薙刀手持。ai[0]=拍号 0 直刺 / 1 横扫，ai[1]=扫向符号。<br/>
    /// 直刺拍走基类持距相位；横扫拍走基类横扫第二时间线（GsSweepBeatModule），
    /// 本类只留钴钢音效：扫掠首帧哨音与命中脆响
    /// </summary>
    internal class GsCobaltNaginataHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.CobaltNaginata;

        //直刺拍手感：硬模最轻灵的一把
        protected override float WindupFrames => 4f;
        protected override float ThrustFrames => 5f;
        protected override float DwellFrames => 3f;
        protected override float RecoverFrames => 8f;
        protected override float RestHoldout => 10f;
        protected override float PullbackDist => 14f;
        protected override float StabReach => 58f;
        protected override float BladeLength => 88f;
        protected override float CollisionWidth => 28f;
        protected override float TipGreedRadius => 26f;
        protected override float ThrustEasePower => 2.6f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.035f;
        protected override int HitboxSize => 48;
        protected override int HitstopFrames => 2;
        protected override float ThrustPitch => -0.12f;

        private bool IsSweep => ComboStage == 1;

        /// <summary>偶拍走基类横扫时间线（扫掠参数用基类默认值 = 本刀原值）</summary>
        protected override bool SweepBeatActive => IsSweep;

        private bool sweepSoundPlayed;

        protected override void OnInit() {
            //横扫是重几何拍：伤害小抬
            if (IsSweep) {
                Projectile.damage = (int)(Projectile.damage * 1.12f);
            }
        }

        /// <summary>横扫音效：扫出首帧沉哨音</summary>
        protected override void OnSweepTick(int sweepPhase) {
            if (sweepPhase != 1 || sweepSoundPlayed) {
                return;
            }
            sweepSoundPlayed = true;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = -0.22f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.3f, Pitch = 0.1f }, Owner.Center);
            }
        }

        /// <summary>命中反馈：钴钢脆响，扫拍音更沉</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            if (VaultUtils.isServer) {
                return;
            }
            bool sweep = IsSweep && sweepBeat != null;
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.4f, Pitch = sweep ? 0.15f : 0.45f, MaxInstances = 3 }, target.Center);
        }
    }
}
