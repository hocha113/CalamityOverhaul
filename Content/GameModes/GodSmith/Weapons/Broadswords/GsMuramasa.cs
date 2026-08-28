using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【淬蓝妖钢】材质：淬蓝的妖钢太刀，杀气藏在停顿里。签名：①居合五连（快快快蓄爆）：
    /// 前三拍闪现斩，刀身一两帧瞬移到终角、行程全由涂抹带承弧，斩后残心几何冻结
    /// ②第四拍收鞘长蓄：刀贴身横持，蓝白电丝沿刃渐聚 ③第五拍拔刀全弧瞬斩：
    /// 细白刃线结构性亮起两三帧，顿帧与残心全族最长
    /// </summary>
    internal class GsMuramasa : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Muramasa;

        protected override int HeldProjID => ModContent.ProjectileType<GsMuramasaHeld>();

        protected override int ComboBeats => 5;

        protected override string GsDescFallback =>
            "Reforged: a five-count iaijutsu string; three flash cuts, " +
            "one long sheathed charge, then a full-arc draw that erupts out of stillness";

        //淬蓝妖钢色板
        internal static readonly Color SteelBright = new(200, 228, 255); //冷钢蓝白刃缘
        internal static readonly Color SteelMain = new(92, 140, 212);    //淬蓝钢身
        internal static readonly Color SteelHot = new(238, 248, 255);    //拔刀白（只上刃线与刃尖）
        internal static readonly Color SteelDeep = new(10, 16, 34);      //近黑蓝钢影

        //预算：原版 24 伤/18 帧 = 1.33 伤帧。周期 = 3×13f(快拍×0.95) + 20f(蓄拍无伤) + 19f(爆拍×1.6) = 78f，
        //每周期 24×1.08×(0.95×3+1.6) ≈ 115.3 → 约 1.48 伤帧 ≈ 111%；
        //含出手重询延迟按 ~88f 摊约 98%，整体落在包络中下段
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 村正手持：五拍居合。拍 0~2 闪现斩（藏行程露停顿：举相只贴身归位不做大后摆，
    /// 斩切 2 帧瞬移全弧、当帧刀身近隐、涂抹带承弧，收势前 40% 残心冻结）；
    /// 拍 3 收鞘长蓄（几何独立：刀贴身横持，电丝渐聚，全程无伤）；
    /// 拍 4 拔刀爆发（全族最大弧、白刃线 2~3 帧、顿帧 3、残心冻结 55%）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsMuramasaHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Muramasa;
        protected override Color EdgeBright => GsMuramasa.SteelBright;
        protected override Color BodyMain => GsMuramasa.SteelMain;
        protected override Color HotAccent => GsMuramasa.SteelHot;
        protected override Color DeepShadow => GsMuramasa.SteelDeep;

        protected override int BeatCount => 5;
        protected override float BaseReach => 124f;
        protected override float CollisionWidth => 34f;
        //瞬斩两帧都在伤害窗内（p=0.5 与 p=1.0）
        protected override float DamageWindowEnd => 1f;

        /// <summary>拍 3 收鞘长蓄</summary>
        private bool IsCharge => ComboStage == 3;
        /// <summary>收鞘持刀角（背身斜下，OnStageInit 缓存）</summary>
        private float sheathAngle;
        /// <summary>蓄势进度 0~1（举相+滞相）</summary>
        private float ChargeProgress => MathHelper.Clamp(timer / (float)(raiseDur + holdDur), 0f, 1f);

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //快拍三连：拍拍弧距与触及递增，音高递升
            0 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 2, Recover = 6,
                RaiseBack = 1.5f, Follow = 0.8f, ReachScale = 1f, LeanAmp = 0.03f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.12f,
            },
            1 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 2, Recover = 6,
                RaiseBack = 1.75f, Follow = 0.95f, ReachScale = 1.05f, LeanAmp = 0.035f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.18f,
            },
            2 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 2, Recover = 6,
                RaiseBack = 1.95f, Follow = 1.1f, ReachScale = 1.1f, LeanAmp = 0.04f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.24f,
            },
            //收鞘长蓄：Hold 拉满，无伤纯蓄
            3 => new GsBroadBeat {
                Raise = 5, Hold = 9, Slash = 1, Recover = 5,
                RaiseBack = 1.2f, Follow = 0.6f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 0, LungeSpeed = 0f, SwingPitch = 0.3f,
            },
            //拔刀爆发：全族最大弧距
            _ => new GsBroadBeat {
                Raise = 3, Hold = 1, Slash = 2, Recover = 13,
                RaiseBack = 2.7f, Follow = 1.4f, ReachScale = 1.22f, LeanAmp = 0.075f,
                DamageMult = 1.6f, Hitstop = 3, LungeSpeed = 2.8f, SwingPitch = -0.2f,
            },
        };

        protected override void OnStageInit() {
            sheathAngle = new Vector2(-facingDir, 0.34f).ToRotation();
        }

        //==================== 居合几何：藏行程露停顿 ====================

        protected override void UpdateBladeTransform(int phase) {
            if (IsCharge) {
                SheathTransform(phase);
                mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
                return;
            }
            float heldAngle = ArcStart - (swingDir * 0.05f);

            switch (phase) {
                case PhaseRaise: {
                    //藏行程：举相只做贴身小幅归位，不做大后摆预告
                    float p = timer / (float)raiseDur;
                    float eased = 1f - MathF.Pow(1f - p, 3f);
                    mainAngle = MathHelper.Lerp(heldAngle + (swingDir * 0.3f), heldAngle, eased);
                    mainReach = FullReach * MathHelper.Lerp(0.55f, 0.9f, eased);
                    slashProgress = 0f;
                    break;
                }
                case PhaseHold: {
                    //静止谷：完全定格一帧，为爆发让出画框
                    mainAngle = heldAngle;
                    mainReach = FullReach * 0.92f;
                    slashProgress = 0f;
                    break;
                }
                case PhaseSlash: {
                    //闪现斩：首帧直接跳到 93% 终角，次帧收尾，扫角采样保证全弧命中
                    float p = (timer - raiseDur - holdDur) / (float)slashDur;
                    slashProgress = p;
                    mainAngle = MathHelper.Lerp(heldAngle, ArcEnd, p >= 0.99f ? 1f : 0.93f);
                    mainReach = FullReach * 1.02f;
                    break;
                }
                default: {
                    //残心：前段几何完全冻结，之后才缓缓收刀
                    float q = (timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    float freeze = IsFinisher ? 0.55f : 0.4f;
                    slashProgress = 1f;
                    if (q < freeze) {
                        mainAngle = ArcEnd;
                        mainReach = FullReach * 1.02f;
                        fanFade = 1f;
                    }
                    else {
                        float s = (q - freeze) / (1f - freeze);
                        mainAngle = ArcEnd + (swingDir * 0.06f * EaseOutQuad(s));
                        mainReach = FullReach * MathHelper.Lerp(1.02f, 0.8f, s * s);
                        fanFade = MathHelper.Clamp(1f - (s * 1.15f), 0f, 1f);
                    }
                    break;
                }
            }

            mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
        }

        /// <summary>收鞘几何：刀贴身横持，全程静止无伤，只有呼吸感</summary>
        private void SheathTransform(int phase) {
            slashProgress = 0f;
            fanFade = 0f;
            if (phase == PhaseRaise) {
                float p = timer / (float)raiseDur;
                float eased = SmoothStep01(p);
                mainAngle = baseAngle + (MathHelper.WrapAngle(sheathAngle - baseAngle) * eased);
                mainReach = FullReach * MathHelper.Lerp(0.85f, 0.42f, eased);
                return;
            }
            //滞帧起全程定在鞘位，只留极轻的呼吸
            mainAngle = sheathAngle;
            mainReach = FullReach * (0.42f + (phase == PhaseHold ? 0.01f * MathF.Sin(timer * 0.8f) : 0f));
        }

        //==================== 演出层 ====================

        /// <summary>斩切当帧刀身近隐，行程交给涂抹带；残心瞬间实体化回满</summary>
        protected override float BladeAlpha => !IsCharge && CurrentPhase == PhaseSlash ? 0.25f : 1f;

        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsMuramasa.SteelMain, 0.15f);
        protected override Color GlowColor => GsMuramasa.SteelBright;
        protected override int GhostCount => IsFinisher ? 3 : 2;
        //残影角间距拉大成曝光表台阶
        protected override float GhostSpacing => 0.42f;

        protected override void HandlePhaseEvents(int phase) {
            //收鞘起手：一记轻的入鞘滑音
            if (IsCharge && timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.3f, Pitch = 0.5f }, Owner.Center);
            }
            base.HandlePhaseEvents(phase);
        }

        protected override void PlaySwingSound() {
            if (IsCharge) {
                //蓄满卡簧声，蓄拍不出挥砍音
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.35f }, Owner.Center);
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.55f, Pitch = -0.35f }, Owner.Center);
            }
        }

        protected override void OnSlashBegin() {
            if (!IsFinisher) {
                return;
            }
            SetFlash(6);
            if (VaultUtils.isServer) {
                return;
            }
            //拔刀瞬间沿全弧外抛一圈蓝白电丝
            for (int i = 0; i < 10; i++) {
                float ang = MathHelper.Lerp(ArcStart, ArcEnd, i / 9f);
                Vector2 at = Hand + (ang.ToRotationVector2() * (FullReach * Main.rand.NextFloat(0.5f, 0.9f)));
                PRTLoader.NewParticle<PRT_Line>(at, ang.ToRotationVector2() * Main.rand.NextFloat(3f, 7f),
                    GsMuramasa.SteelBright, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        protected override void HandleParticles(int phase) {
            if (IsCharge) {
                if (phase is PhaseRaise or PhaseHold) {
                    //蓝白电丝渐聚：沿刃短线越蓄越密 + 微光向刀身收拢
                    float charge = ChargeProgress;
                    if (Main.rand.NextFloat() < 0.35f + (charge * 0.5f)) {
                        Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat());
                        Vector2 vel = (mainAngle.ToRotationVector2() * Main.rand.NextFloat(-1.2f, 1.2f))
                            + Main.rand.NextVector2Circular(0.4f, 0.4f);
                        PRTLoader.NewParticle<PRT_Line>(at, vel,
                            Main.rand.NextBool() ? GsMuramasa.SteelBright : GsMuramasa.SteelHot,
                            Main.rand.NextFloat(0.18f, 0.3f))?.Configure(false, Main.rand.Next(6, 11));
                    }
                    if (Main.rand.NextBool(3)) {
                        Vector2 hand = Hand;
                        Vector2 at = hand + (Main.rand.NextVector2Unit() * Main.rand.NextFloat(30f, 54f));
                        PRTLoader.NewParticle<PRT_Light>(at, (hand - at) * 0.15f, GsMuramasa.SteelMain,
                            Main.rand.NextFloat(0.05f, 0.09f))?.Configure(8, 0.5f);
                    }
                }
                return;
            }
            base.HandleParticles(phase);
            //残心期刃面偶尔渗出一粒冷钢微光
            if (phase == PhaseRecover && fanFade > 0.6f && Main.rand.NextBool(4)) {
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.55f, 1f));
                PRTLoader.NewParticle<PRT_Spark>(at, mainAngle.ToRotationVector2() * Main.rand.NextFloat(0.4f, 1f),
                    GsMuramasa.SteelBright, Main.rand.NextFloat(0.2f, 0.32f))?.Configure(false, Main.rand.Next(8, 13));
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //低沉居合音 + 蓝钢火花沿挥切切线迸出
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.42f, Pitch = -0.58f }, target.Center);
            Vector2 tangent = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            int lines = IsFinisher ? 5 : 2;
            for (int i = 0; i < lines; i++) {
                PRTLoader.NewParticle<PRT_Line>(target.Center, tangent.RotatedByRandom(0.5) * Main.rand.NextFloat(4f, 9f),
                    Main.rand.NextBool(3) ? GsMuramasa.SteelHot : GsMuramasa.SteelBright,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(12, 18));
            }
        }

        /// <summary>瞬斩涂抹带三层加厚：闪现斩靠它承弧，刀身当帧是隐的</summary>
        protected override void DrawSmearArc(SpriteBatch sb) {
            if (IsCharge || slashProgress <= 0.02f || fanFade <= 0.02f) {
                return;
            }
            Texture2D wave = CWRAsset.SemiCircularSmear?.Value;
            if (wave == null) {
                return;
            }
            float widen = IsFinisher ? 1.25f : 1f;
            float alpha = fanFade * (0.42f + slashProgress * 0.34f);
            Vector2 arcCenter = Hand + (mainAngle.ToRotationVector2() * mainReach * 0.55f) - Main.screenPosition;
            float rot = mainAngle + (swingDir * 0.35f);
            float sizeMul = mainReach / 118f;

            Color outer = GsMuramasa.SteelBright * alpha;
            outer.A = 0;
            sb.Draw(wave, arcCenter, null, outer, rot, wave.Size() / 2f,
                new Vector2(0.5f, 0.24f * widen) * sizeMul, SpriteEffects.None, 0f);
            Color mid = GsMuramasa.SteelMain * (alpha * 0.75f);
            mid.A = 0;
            sb.Draw(wave, arcCenter, null, mid, rot, wave.Size() / 2f,
                new Vector2(0.46f, 0.12f * widen) * sizeMul, SpriteEffects.None, 0f);
            //薄亮芯层：刃路本身
            Color core = GsMuramasa.SteelHot * (alpha * 0.9f);
            core.A = 0;
            sb.Draw(wave, arcCenter, null, core, rot, wave.Size() / 2f,
                new Vector2(0.5f, 0.05f * widen) * sizeMul, SpriteEffects.None, 0f);
        }

        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (IsCharge) {
                DrawChargeGlint(sb);
                return;
            }
            if (!IsFinisher) {
                return;
            }
            //结构性白：拔刀斩切与其后两帧，细白刃线只描刃与刃尖，不做全屏曝光
            int intoRecover = timer - raiseDur - holdDur - slashDur;
            if (CurrentPhase < PhaseSlash || intoRecover > 2) {
                return;
            }
            float a = intoRecover <= 0 ? 0.9f : 0.9f - (intoRecover * 0.3f);
            Texture2D px = TextureAssets.MagicPixel.Value;
            Color line = GsMuramasa.SteelHot * a;
            line.A = 0;
            sb.Draw(px, Hand - Main.screenPosition, new Rectangle(0, 0, 1, 1), line, mainAngle,
                new Vector2(0f, 0.5f), new Vector2(mainReach * 1.02f, 2.2f), SpriteEffects.None, 0f);
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Color tip = GsMuramasa.SteelHot * (a * 0.8f);
                tip.A = 0;
                sb.Draw(glow, mainTip - Main.screenPosition, null, tip, 0f, glow.Size() / 2f,
                    0.24f, SpriteEffects.None, 0f);
            }
        }

        /// <summary>收鞘蓄势：鞘位刃线随蓄势变亮，刀尖凝一粒白星（确定性明灭）</summary>
        private void DrawChargeGlint(SpriteBatch sb) {
            float charge = ChargeProgress;
            if (charge <= 0.05f) {
                return;
            }
            Texture2D px = TextureAssets.MagicPixel.Value;
            Color line = GsMuramasa.SteelBright * (0.15f + charge * 0.45f);
            line.A = 0;
            sb.Draw(px, Hand - Main.screenPosition, new Rectangle(0, 0, 1, 1), line, mainAngle,
                new Vector2(0f, 0.5f), new Vector2(mainReach, 1.6f), SpriteEffects.None, 0f);
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float flicker = 0.8f + 0.2f * MathF.Sin((timer * 0.9f) + (DrawRand01(7) * MathHelper.TwoPi));
            Color star = GsMuramasa.SteelHot * (charge * 0.7f * flicker);
            star.A = 0;
            sb.Draw(glow, mainTip - Main.screenPosition, null, star, 0f, glow.Size() / 2f,
                0.08f + (0.14f * charge), SpriteEffects.None, 0f);
        }
    }
}
