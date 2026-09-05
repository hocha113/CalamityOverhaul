using CalamityOverhaul.Common;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 【短剑首把·垂直切片】罗马短剑重铸：军团操典三段刺。<br/>
    /// 材质：军团青铜刃。签名行为：①三拍连刺——高线快刺、低线快刺、盾步重刺交替几何
    /// ②节奏训练——踩住断拍窗连刺，刺速逐层加快（最多三层）
    /// ③盾步重刺前压半步，命中金属重音 + 小震屏
    /// </summary>
    internal class GsGladius : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.Gladius;

        protected override string GsDescFallback =>
            "Reforged: legion drill in three beats, two quick thrusts then a shield-step heavy;\nkeep the rhythm and each thrust comes faster, up to three tempo stacks";
        protected override int HeldProjType => ModContent.ProjectileType<GsGladiusHeld>();

        protected override int ComboBeats => 3;

        /// <summary>节奏层数 0~3，只在 myPlayer 路径消费</summary>
        private int tempoStacks;

        protected override void SpawnHeld(Item item, Player player, int beat) {
            //断拍窗内接上一刺 = 踩住节奏，层数 +1；首刺/断拍从零起
            tempoStacks = comboCounter > 1 ? Math.Min(3, tempoStacks + 1) : 0;
            base.SpawnHeld(item, player, beat);
        }

        protected override float SpawnAi1(Item item, Player player) => tempoStacks;

        protected override void OnComboReset() => tempoStacks = 0;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;//签名机制吃掉大半预算，底伤只补零头，综合 DPS 落在原版 105%~118%
    }

    /// <summary>
    /// 罗马短剑手持突刺。ai[0]=拍号 0 高线快刺 / 1 低线快刺 / 2 盾步重刺，ai[1]=节奏层数。<br/>
    /// 节奏层数直接加攻速缩放（+8%/层）
    /// </summary>
    internal class GsGladiusHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.Gladius;

        private bool IsFinisher => ComboStage >= 2;
        private int TempoStacks => Math.Clamp((int)WeaponParam, 0, 3);

        //盾步重刺整体更沉更深
        protected override float WindupFrames => IsFinisher ? 6f : 3f;
        protected override float ThrustFrames => IsFinisher ? 5f : 4f;
        protected override float DwellFrames => IsFinisher ? 3f : 2f;
        protected override float RecoverFrames => IsFinisher ? 9f : 6f;
        protected override float PullbackDist => IsFinisher ? 16f : 9f;
        protected override float StabReach => IsFinisher ? 46f : 30f;
        protected override float BladeLength => 44f;
        protected override float ThrustEasePower => IsFinisher ? 3f : 2.55f;
        protected override int HitstopFrames => IsFinisher ? 3 : 1;
        protected override float LeanAmp => IsFinisher ? 0.06f : 0.028f;
        protected override float ThrustPitch => IsFinisher ? -0.10f : 0.22f;

        /// <summary>高低线交替：0 拍走高线，1 拍走低线，终结拍直取中线</summary>
        protected override void ModifyStabDirection(ref Vector2 unit) {
            float tilt = ComboStage switch { 0 => -0.065f, 1 => 0.065f, _ => 0f };
            unit = unit.RotatedBy(tilt * facingDir);
        }

        protected override void OnInit() {
            //节奏训练：层数直接催快时间线
            speedMul *= 1f + TempoStacks * 0.08f;
            if (IsFinisher) {
                Projectile.damage = (int)(Projectile.damage * 1.30f);
            }
        }

        protected override void OnThrustBurst() {
            //盾步前压：爆发首帧沿出手向踏半步（owner 端权威，位置随原版同步）
            if (IsFinisher && Owner.whoAmI == Main.myPlayer && !Owner.mount.Active) {
                Owner.velocity.X += facingDir * 2.6f;
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.75f, Pitch = ThrustPitch + TempoStacks * 0.05f }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.45f, Pitch = -0.30f }, Owner.Center);
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            if (!IsFinisher || !firstOnTarget) {
                return;
            }
            //盾步重刺命中的升级反馈：金属重音 + 小震屏
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.6f, Pitch = 0.15f }, target.Center);
                if (CWRClientConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        target.Center, stabUnit, 3.5f, 5f, 7, 480f, FullName));
                }
            }
        }
    }
}
