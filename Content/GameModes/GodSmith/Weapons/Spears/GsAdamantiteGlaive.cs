using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
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
    /// 大幅削甲、顿帧加深、金属崩火升级反馈，杆身命中只是普通一刺
    /// ②甜点段在刺出时以猩红尖光标出，玩家能看见该用哪一寸刃 ③重装节奏，全族最沉的一杆。<br/>
    /// 与 Ruler 的丈量赏罚（按距离乘除伤害）不同：本枪不动伤害数字，
    /// 赏的是破甲与顿帧的重装质感，罚只是回到普通一刺
    /// </summary>
    internal class GsAdamantiteGlaive : GsSpearScheme
    {
        public override int TargetItemID => ItemID.AdamantiteGlaive;

        protected override string GsDescFallback =>
            "Reforged: the last quarter of the blade is a sweet spot;" +
            "\nlanding the very tip shatters armor and staggers, the shaft is just a poke";

        protected override int HeldProjType => ModContent.ProjectileType<GsAdamantiteGlaiveHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;//破甲收益吃机制端预算，综合 DPS 落在原版 108%~120%（会玩甜点更高）
    }

    /// <summary>
    /// 精金刃手持突刺：target.Center 与 TipPos 距离落进刺线最后 25% 段 = 甜点命中，
    /// ArmorPenetration +26、顿帧 +2、金属崩火 + 小震屏升级反馈
    /// </summary>
    internal class GsAdamantiteGlaiveHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.AdamantiteGlaive;

        //猩红精金色板
        internal static readonly Color AdamRed = new(255, 92, 96);      //猩红亮
        internal static readonly Color AdamCrimson = new(206, 44, 60);  //精金红
        internal static readonly Color AdamEmber = new(255, 176, 120);  //崩火橙
        internal static readonly Color AdamDeep = new(96, 20, 34);      //深红影

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

        protected override Color EdgeColor => AdamRed;
        protected override Color CoreColor => AdamCrimson;
        protected override Color ShaftColor => AdamDeep with { A = 235 };

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
            if (!sweetPending || !firstOnTarget || VaultUtils.isServer) {
                return;
            }
            //甜点升级反馈：金属重音 + 小震屏
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.55f, Pitch = -0.2f }, target.Center);
            if (CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    target.Center, stabUnit, 4f, 5f, 8, 480f, FullName));
            }
        }

        /// <summary>命中反馈分流：甜点 = 猩红金属崩火喷泉，杆身 = 收敛的红火花</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            bool sweet = sweetPending;
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = sweet ? 0.5f : 0.3f, Pitch = sweet ? -0.35f : 0.1f, MaxInstances = 3 }, target.Center);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, sweet ? AdamEmber : AdamCrimson,
                sweet ? 0.26f : 0.15f)?.Configure(10, 0.8f);
            if (sweet) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(pos, Vector2.Zero, AdamRed, 0.05f)
                    ?.Configure(0.06f, 0.42f, 12);
            }
            int sparks = sweet ? 11 : 4;
            for (int i = 0; i < sparks; i++) {
                //崩火：甜点命中带重力的金属火屑弹跳四溅
                Vector2 vel = stabUnit.RotatedByRandom(sweet ? 0.9 : 0.45) * Main.rand.NextFloat(3.5f, sweet ? 10f : 7f);
                Color c = Main.rand.NextBool(3) ? AdamEmber : AdamRed;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.4f, sweet ? 0.75f : 0.55f))
                    ?.Configure(true, Main.rand.Next(14, sweet ? 26 : 20));
            }
        }

        /// <summary>甜点段可视化：刺出与驻相期，刺线最后 25% 段亮一枚猩红尖光（定值，无随机）</summary>
        protected override void DrawOverBlade(SpriteBatch sb) {
            int phase = CurrentPhase;
            if (phase is not PhaseThrust and not PhaseDwell || FanFade <= 0.05f) {
                return;
            }
            Texture2D streak = CWRAsset.LightShot?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (streak == null || glow == null) {
                return;
            }
            float sweetLen = (holdout + BladeLength) * 0.25f;
            Vector2 mid = TipPos - stabUnit * (sweetLen * 0.5f) - Main.screenPosition;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f);
            //甜点段猩红拉丝
            Color c1 = AdamRed with { A = 0 } * (0.55f * FanFade * pulse);
            sb.Draw(streak, mid, null, c1, stabUnit.ToRotation(), streak.Size() / 2f,
                new Vector2(sweetLen / streak.Size().X, 0.09f), SpriteEffects.None, 0f);
            //尖端崩火橙光点
            Color c2 = AdamEmber with { A = 0 } * (0.5f * FanFade * pulse);
            sb.Draw(glow, TipPos - Main.screenPosition, null, c2, 0f, glow.Size() / 2f, 0.2f * pulse, SpriteEffects.None, 0f);
        }
    }
}
