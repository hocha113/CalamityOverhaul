using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【冷钢太刀】材质：淬钢青的东洋冷锻刃。签名：①太刀语言——斩切相仅两帧，
    /// 刀身隐入挥影、行程全由涂抹带承弧 ②「残心」：每斩收势前段几何完全冻结，
    /// 刀停在终角纹丝不动 ③第三拍「居合」：刀贴鞘长蓄、寒光聚拢，一闪出鞘白闪爆发
    /// </summary>
    internal class GsKatana : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Katana;

        protected override int HeldProjID => ModContent.ProjectileType<GsKatanaHeld>();

        protected override string GsDescFallback =>
            "Reforged: the blade vanishes into its own swing arc and reappears frozen at the endpoint; " +
            "the third strike is a sheathed iai draw that erupts in a single white flash";

        //钢青冷色板
        internal static readonly Color SteelBright = new(214, 234, 248); //霜白刃缘
        internal static readonly Color SteelMain = new(126, 156, 184);   //钢青身
        internal static readonly Color SteelHot = new(166, 220, 255);    //寒光青白
        internal static readonly Color SteelDeep = new(14, 20, 30);      //近黑钢影

        //底伤 +5%：两记快斩 0.95x + 居合 1.5x（但居合滞帧 7 帧拉长节奏），
        //按 max(useTime, 弹幕总帧) 摊算综合 DPS 约为原版 103%~112%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 冷钢太刀手持：三拍。0 横一文字 / 1 返し斬（镜像更快）/ 2 居合（贴鞘长蓄、
    /// 白闪一闪）。整替几何：斩切两帧瞬移到终角（藏行程），收势前 40% 完全冻结（残心）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsKatanaHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Katana;
        protected override Color EdgeBright => GsKatana.SteelBright;
        protected override Color BodyMain => GsKatana.SteelMain;
        protected override Color HotAccent => GsKatana.SteelHot;
        protected override Color DeepShadow => GsKatana.SteelDeep;

        /// <summary>斩切仅两帧，攻速快时可能压到 1 帧（p 直达 1.0），窗放宽到全程</summary>
        protected override float DamageWindowEnd => 1.01f;

        /// <summary>残心冻结占收势的前几成</summary>
        private const float FreezeRatio = 0.4f;

        protected override GsBroadBeat GetBeat(int stage) {
            return stage switch {
                //横一文字：慢起手、静滞、两帧闪斩、长残心
                0 => new GsBroadBeat {
                    Raise = 7, Hold = 3, Slash = 2, Recover = 10,
                    RaiseBack = 1.6f, Follow = 0.9f, ReachScale = 1f, LeanAmp = 0.04f,
                    DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.1f,
                },
                //返し斬：镜像回斩，节奏更催
                1 => new GsBroadBeat {
                    Raise = 5, Hold = 2, Slash = 2, Recover = 9,
                    RaiseBack = 1.4f, Follow = 1.05f, ReachScale = 1.02f, LeanAmp = 0.04f,
                    DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.18f,
                },
                //居合：刀贴鞘长蓄（Hold 7），一闪出鞘，残心最长
                _ => new GsBroadBeat {
                    Raise = 4, Hold = 7, Slash = 2, Recover = 14,
                    RaiseBack = 2.4f, Follow = 1.15f, ReachScale = 1.22f, LeanAmp = 0.085f,
                    DamageMult = 1.5f, Hitstop = 2, LungeSpeed = 2.8f, SwingPitch = -0.15f,
                },
            };
        }

        //==================== 太刀几何：藏行程、露停顿 ====================

        /// <summary>
        /// 整替相位几何：举相收刀入架（居合拍贴鞘更低）、滞帧完全静止、
        /// 斩切两帧瞬移到终角、收势前 40% 残心冻结后才回收
        /// </summary>
        protected override void UpdateBladeTransform(int phase) {
            float arcStart = ArcStart;
            float heldAngle = arcStart - (swingDir * 0.05f);
            //居合拍刀贴鞘：架位更低更贴身
            float parkReach = IsFinisher ? 0.34f : 0.55f;

            switch (phase) {
                case PhaseRaise: {
                    float p = timer / (float)raiseDur;
                    float eased = 1f - MathF.Pow(1f - p, 3f);
                    float liftFrom = arcStart + (swingDir * raiseBack * 0.5f);
                    mainAngle = MathHelper.Lerp(liftFrom, arcStart, eased);
                    mainReach = FullReach * MathHelper.Lerp(0.5f, parkReach + 0.12f, eased);
                    slashProgress = 0f;
                    break;
                }
                case PhaseHold: {
                    //静滞：几何锁死在架位，只允许贴鞘微沉（收-爆-停里的「收」）
                    float p = (timer - raiseDur) / (float)holdDur;
                    mainAngle = heldAngle;
                    mainReach = FullReach * MathHelper.Lerp(parkReach + 0.12f, parkReach, EaseOutQuad(p));
                    slashProgress = 0f;
                    break;
                }
                case PhaseSlash: {
                    //两帧闪斩：首帧直达 96% 终角，次帧落定——刀不走过程，行程交给涂抹带
                    float p = (timer - raiseDur - holdDur) / (float)slashDur;
                    slashProgress = p;
                    mainAngle = p < 0.75f
                        ? MathHelper.Lerp(heldAngle, ArcEnd, 0.96f)
                        : ArcEnd;
                    mainReach = FullReach;
                    break;
                }
                default: {
                    float q = (timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    slashProgress = 1f;
                    if (q <= FreezeRatio) {
                        //残心：几何纹丝不动，只让挥影自然蚀散
                        mainAngle = ArcEnd;
                        mainReach = FullReach;
                        fanFade = MathHelper.Clamp(1f - (q / FreezeRatio) * 0.55f, 0f, 1f);
                    }
                    else {
                        float r = (q - FreezeRatio) / (1f - FreezeRatio);
                        float settle = EaseOutQuad(r);
                        mainAngle = ArcEnd - (swingDir * 0.07f * settle);
                        mainReach = FullReach * MathHelper.Lerp(1f, 0.74f, r * r);
                        fanFade = MathHelper.Clamp(0.45f * (1f - r), 0f, 1f);
                    }
                    break;
                }
            }

            mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
        }

        //==================== 演出：刀隐入影，弧由涂抹承 ====================

        /// <summary>斩切当帧刀身压暗隐入挥影；其余相全亮</summary>
        protected override float BladeAlpha => CurrentPhase == PhaseSlash ? 0.22f : 1f;

        /// <summary>行程由残影链承弧：拍数多、间距大，铺满整段挥弧</summary>
        protected override int GhostCount => IsFinisher ? 4 : 3;
        protected override float GhostSpacing => IsFinisher ? 0.5f : 0.38f;

        /// <summary>涂抹带外层提到霜白，弧比刀亮（太刀的swoosh是主角）</summary>
        protected override Color SmearOuterColor => Color.Lerp(GsKatana.SteelBright, Color.White, 0.45f);

        protected override bool GlowAlways => IsFinisher;
        protected override Color GlowColor => GsKatana.SteelHot;

        /// <summary>base 之上补一层更窄更亮的芯弧，涂抹 alpha 整体抬高</summary>
        protected override void DrawSmearArc(SpriteBatch sb) {
            base.DrawSmearArc(sb);
            if (slashProgress <= 0.02f || fanFade <= 0.02f) {
                return;
            }
            Texture2D wave = CWRAsset.SemiCircularSmear?.Value;
            if (wave == null) {
                return;
            }
            float alpha = fanFade * (0.28f + slashProgress * 0.3f);
            Vector2 arcCenter = Hand + (mainAngle.ToRotationVector2() * mainReach * 0.55f) - Main.screenPosition;
            float rot = mainAngle + (swingDir * 0.35f);
            Color core = Color.Lerp(GsKatana.SteelHot, Color.White, 0.6f) * alpha;
            core.A = 0;
            sb.Draw(wave, arcCenter, null, core, rot, wave.Size() / 2f
                , new Vector2(0.4f, 0.06f) * (mainReach / 118f), SpriteEffects.None, 0f);
        }

        protected override void HandlePhaseEvents(int phase) {
            //居合起手：一记贴鞘的低鸣
            if (IsFinisher && timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.35f, Pitch = 0.5f }, Owner.Center);
            }
            base.HandlePhaseEvents(phase);
        }

        /// <summary>出鞘一闪：白闪拉满（顿帧走 beat.Hitstop=2）</summary>
        protected override void OnSlashBegin() {
            if (IsFinisher) {
                SetFlash(8);
            }
        }

        /// <summary>太刀音色：高频薄刃声，居合补一记利落的出鞘斩响</summary>
        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.75f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = 0.15f }, Owner.Center);
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            //居合蓄势：寒光自四周向刀鞘聚拢
            if (IsFinisher && phase is PhaseRaise or PhaseHold) {
                Vector2 hilt = Vector2.Lerp(Hand, mainTip, 0.3f);
                Vector2 at = hilt + Main.rand.NextVector2Unit() * Main.rand.NextFloat(30f, 62f);
                PRTLoader.NewParticle<PRT_Light>(at, (hilt - at) * 0.15f, GsKatana.SteelHot,
                    Main.rand.NextFloat(0.05f, 0.1f))?.Configure(8, 0.5f);
            }
        }
    }
}
