using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 钴蓝薙刀重铸：薙斩变式。<br/>
    /// 材质：淬火钴钢刀刃。签名行为：①两拍交替——奇拍直线突刺，偶拍走基类横扫时间线，
    /// 一记横扫弧斩（角度扫掠 + 弧线采样判定，几何与突刺完全不同）
    /// ②扫拍拖出钴蓝弧光涂抹与刀身残影 ③扫斩命中钴钢脆响、火花沿切线甩出
    /// </summary>
    internal class GsCobaltNaginata : GsSpearScheme
    {
        public override int TargetItemID => ItemID.CobaltNaginata;

        protected override string GsDescFallback =>
            "Reforged: alternating polework, a straight thrust then a wide cobalt sweep;" +
            "\nthe sweep carves an arc where the thrust line cannot reach";

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
    /// 本类只留钴钢演出：扫掠音效火花、弧光涂抹、刀身自绘与命中反馈
    /// </summary>
    internal class GsCobaltNaginataHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.CobaltNaginata;

        //淬火钴钢色板
        internal static readonly Color CobaltEdge = new(168, 214, 255);   //刃缘亮蓝
        internal static readonly Color CobaltMain = new(74, 128, 236);    //钴身
        internal static readonly Color CobaltDeep = new(30, 52, 120);     //深钴影
        internal static readonly Color CobaltFlash = new(220, 240, 255);  //淬火白闪

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

        protected override Color EdgeColor => CobaltEdge;
        protected override Color CoreColor => CobaltMain;
        protected override Color ShaftColor => CobaltDeep with { A = 235 };

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

        /// <summary>横扫演出：扫出首帧沉哨音，扫掠期沿切线甩钴蓝火花</summary>
        protected override void OnSweepTick(int sweepPhase) {
            if (sweepPhase != 1) {
                return;
            }
            //扫出首帧一记沉哨音
            if (!sweepSoundPlayed) {
                sweepSoundPlayed = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = -0.22f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.3f, Pitch = 0.1f }, Owner.Center);
                }
                return;
            }
            if (VaultUtils.isServer) {
                return;
            }
            //扫掠期沿切线甩钴蓝火花
            Vector2 sweepVel = (sweepBeat.mainAngle + sweepBeat.swingDir * MathHelper.PiOver2).ToRotationVector2();
            Vector2 at = Hand + sweepBeat.mainAngle.ToRotationVector2() * Main.rand.NextFloat(0.6f, 1f) * sweepBeat.mainReach;
            Color c = Main.rand.NextBool(3) ? CobaltFlash : CobaltEdge;
            PRTLoader.NewParticle<PRT_Spark>(at, sweepVel * Main.rand.NextFloat(3.5f, 7f), c,
                Main.rand.NextFloat(0.32f, 0.55f))?.Configure(true, Main.rand.Next(11, 18));
        }

        /// <summary>命中反馈：钴钢脆响 + 火花沿刃向甩出，扫拍音更沉、火花更宽</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            bool sweep = IsSweep && sweepBeat != null;
            Vector2 dir = sweep
                ? (sweepBeat.mainAngle + sweepBeat.swingDir * MathHelper.PiOver2).ToRotationVector2()
                : stabUnit;
            Vector2 pos = sweep
                ? Vector2.Lerp(Hand + sweepBeat.mainAngle.ToRotationVector2() * sweepBeat.mainReach, target.Center, 0.5f)
                : Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.4f, Pitch = sweep ? 0.15f : 0.45f, MaxInstances = 3 }, target.Center);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, CobaltFlash, sweep ? 0.22f : 0.16f)?.Configure(9, 0.75f);
            int sparks = sweep ? 8 : 5;
            float spread = sweep ? 0.85f : 0.5f;
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = dir.RotatedByRandom(spread) * Main.rand.NextFloat(3.5f, 8f);
                Color c = Main.rand.NextBool(3) ? CobaltFlash : CobaltEdge;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        //==================== 横扫拍自绘：弧光涂抹 + 残影 + 刀身 ====================

        protected override void DrawSweepSet(SpriteBatch sb, Color lightColor) {
            if (sweepBeat == null) {
                return;
            }
            DrawSweepSmear(sb);
            DrawSweepBlade(sb, lightColor);
        }

        /// <summary>钴蓝弧光：双层弧形涂抹沿扫角走（加色 A=0），扫掠亮收势蚀散</summary>
        private void DrawSweepSmear(SpriteBatch sb) {
            if (sweepBeat.slashProgress <= 0.02f || sweepBeat.fade <= 0.02f) {
                return;
            }
            Texture2D wave = CWRAsset.SemiCircularSmear?.Value;
            if (wave == null) {
                return;
            }
            float alpha = sweepBeat.fade * (0.28f + sweepBeat.slashProgress * 0.38f);
            Vector2 arcCenter = Hand + sweepBeat.mainAngle.ToRotationVector2() * (sweepBeat.mainReach * 0.55f) - Main.screenPosition;
            float rot = sweepBeat.mainAngle + sweepBeat.swingDir * 0.35f;
            Color c1 = CobaltEdge with { A = 0 } * alpha;
            sb.Draw(wave, arcCenter, null, c1, rot, wave.Size() / 2f,
                new Vector2(0.46f, 0.22f) * (sweepBeat.mainReach / 118f), SpriteEffects.None, 0f);
            Color c2 = CobaltMain with { A = 0 } * (alpha * 0.7f);
            sb.Draw(wave, arcCenter, null, c2, rot, wave.Size() / 2f,
                new Vector2(0.42f, 0.10f) * (sweepBeat.mainReach / 118f), SpriteEffects.None, 0f);
        }

        /// <summary>扫拍刀身：姿态残影 + 暗影垫底 + 本体 + 扫掠期淬火辉光（朝向含倒重力竖翻）</summary>
        private void DrawSweepBlade(SpriteBatch sb, Color lightColor) {
            Main.instance.LoadItem(TargetItemType);
            Texture2D tex = TextureAssets.Item[TargetItemType].Value;
            Vector2 origin = tex.Size() / 2f;
            float scale = BladeLength / MathF.Max(tex.Size().Length() * BladeTexFill, 1f);
            GetBladeOrientation(out float rotOffset, out SpriteEffects effect);
            Vector2 hand = Hand;

            //扫掠期姿态残影，最近的最亮
            if (sweepBeat.Phase == 1 && sweepBeat.slashProgress > 0.10f) {
                for (int g = 3; g >= 1; g--) {
                    float ghostAngle = sweepBeat.mainAngle - sweepBeat.swingDir * 0.20f * g;
                    float ghostAlpha = g switch { 1 => 0.32f, 2 => 0.17f, _ => 0.08f };
                    Color ghost = CobaltEdge with { A = 0 } * ghostAlpha;
                    Vector2 gPos = hand + ghostAngle.ToRotationVector2() * (sweepBeat.mainReach * 0.52f) - Main.screenPosition;
                    sb.Draw(tex, gPos, null, ghost, ghostAngle + rotOffset, origin, scale, effect, 0f);
                }
            }

            Vector2 drawPos = hand + sweepBeat.mainAngle.ToRotationVector2() * (sweepBeat.mainReach * 0.52f) - Main.screenPosition;

            //深钴暗影垫底
            Color shadow = new Color(14, 14, 20, 190) * 0.45f;
            sb.Draw(tex, drawPos + new Vector2(facingDir, 2f), null, shadow, sweepBeat.mainAngle + rotOffset, origin, scale * 1.02f, effect, 0f);
            sb.Draw(tex, drawPos, null, lightColor, sweepBeat.mainAngle + rotOffset, origin, scale, effect, 0f);

            //扫掠期淬火白辉光
            float glow = sweepBeat.Phase == 1 ? 0.35f * sweepBeat.fade : 0.12f * sweepBeat.fade;
            if (glow > 0.02f) {
                Color gc = CobaltFlash with { A = 0 } * glow;
                sb.Draw(tex, drawPos, null, gc, sweepBeat.mainAngle + rotOffset, origin, scale * 1.045f, effect, 0f);
            }
        }
    }
}
