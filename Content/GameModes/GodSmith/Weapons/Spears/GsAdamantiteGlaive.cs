using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 精金刃重铸：精金破势。<br/>
    /// 材质：猩红精金重刃。签名行为：①刺线最后四分之一是破甲甜点——尖端命中
    /// 大幅削甲、顿帧加深、金属重音升级反馈，杆身命中只是普通一刺
    /// ②重装节奏，全族最沉的一杆。<br/>
    /// 与 Ruler 的丈量赏罚（按距离乘除伤害）不同：本枪不动伤害数字，
    /// 赏的是破甲与顿帧的重装质感，罚只是回到普通一刺
    /// </summary>
    internal class GsAdamantiteGlaive : GsSpearScheme
    {
        public override int TargetItemID => ItemID.AdamantiteGlaive;

        protected override string GsDescFallback =>
            "Reforged: the last quarter of the blade is a sweet spot;\nlanding the very tip shatters armor and staggers, the shaft is just a poke";
        protected override int HeldProjType => ModContent.ProjectileType<GsAdamantiteGlaiveHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;//破甲收益吃机制端预算，综合 DPS 落在原版 108%~120%（会玩甜点更高）
    }

    /// <summary>
    /// 精金刃手持突刺：target.Center 与 TipPos 距离落进刺线最后 25% 段 = 甜点命中，
    /// ArmorPenetration +26、顿帧 +2、金属重音 + 小震屏升级反馈
    /// </summary>
    internal class GsAdamantiteGlaiveHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.AdamantiteGlaive;

        //重装节奏：本组最沉的一杆
        protected override float WindupFrames => 6f;
        protected override float ThrustFrames => 7f;
        protected override float DwellFrames => 4f;
        protected override float RecoverFrames => 10f;
        protected override float RestHoldout => 11f;
        protected override float PullbackDist => 18f;
        protected override float StabReach => 70f;
        protected override float BladeLength => 96f;
        protected override float CollisionWidth => 32f;
        protected override float TipGreedRadius => 28f;
        protected override float ThrustEasePower => 3.3f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.055f;
        protected override int HitboxSize => 54;
        protected override int HitstopFrames => 2 + (sweetPending ? 2 : 0);
        protected override float ThrustPitch => -0.32f;

        /// <summary>本次命中是否落在甜点段（ModifyHitExtra 先于 OnHitNPC 写入，顿帧属性随之读到）</summary>
        private bool sweetPending;

        /// <summary>甜点判定：target.Center 距刺尖不超过刺线全长的 25%</summary>
        private bool InSweetSpot(NPC target)
            => Vector2.Distance(target.Center, TipPos) <= (holdout + BladeLength) * 0.25f;

        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            sweetPending = InSweetSpot(target);
            if (sweetPending) {
                //破甲大幅：精金位阶的重装穿透（不动伤害数字，与 Ruler 的丈量乘除区分）
                modifiers.ArmorPenetration += 26f;
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            if (VaultUtils.isServer) {
                return;
            }
            //命中金属音：甜点更沉更响
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = sweetPending ? 0.5f : 0.3f, Pitch = sweetPending ? -0.35f : 0.1f, MaxInstances = 3 }, target.Center);
            if (!sweetPending || !firstOnTarget) {
                return;
            }
            //甜点升级反馈：金属重音 + 小震屏
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.55f, Pitch = -0.2f }, target.Center);
            if (CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    target.Center, stabUnit, 4f, 5f, 8, 480f, FullName));
            }
        }
    }
}
