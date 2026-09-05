using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【淬蓝妖钢】材质：淬蓝的妖钢太刀，杀气藏在停顿里。签名：①居合五连（快快快蓄爆）：
    /// 前三拍闪现斩，刀身一两帧瞬移到终角，斩后残心几何冻结
    /// ②第四拍收鞘长蓄：刀贴身横持 ③第五拍拔刀全弧瞬斩：顿帧与残心全族最长
    /// </summary>
    internal class GsMuramasa : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Muramasa;

        protected override int HeldProjID => ModContent.ProjectileType<GsMuramasaHeld>();

        protected override int ComboBeats => 5;

        protected override string GsDescFallback =>
            "Reforged: a five-count iaijutsu string; three flash cuts, one long sheathed charge, then a full-arc draw that erupts out of stillness";
        internal static readonly Color SteelBright = new(200, 228, 255); //冷钢蓝白刃缘
        internal static readonly Color SteelMain = new(92, 140, 212);    //淬蓝钢身
        internal static readonly Color SteelHot = new(238, 248, 255);    //拔刀白

        //预算：原版 24 伤/18 帧 = 1.33 伤帧。周期 = 3×13f(快拍×0.95) + 20f(蓄拍无伤) + 19f(爆拍×1.6) = 78f，
        //每周期 24×1.08×(0.95×3+1.6) ≈ 115.3 → 约 1.48 伤帧 ≈ 111%；
        //含出手重询延迟按 ~88f 摊约 98%，整体落在包络中下段
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 村正手持：五拍居合。拍 0~2 闪现斩（藏行程露停顿：举相只贴身归位不做大后摆，
    /// 斩切 2 帧瞬移全弧，收势前 40% 残心冻结）；
    /// 拍 3 收鞘长蓄（几何独立：刀贴身横持，全程无伤）；
    /// 拍 4 拔刀爆发（全族最大弧、顿帧 3、残心冻结 55%）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsMuramasaHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Muramasa;
        protected override Color EdgeBright => GsMuramasa.SteelBright;
        protected override Color BodyMain => GsMuramasa.SteelMain;
        protected override Color HotAccent => GsMuramasa.SteelHot;

        protected override int BeatCount => 5;
        protected override float BaseReach => 124f;
        protected override float CollisionWidth => 34f;
        //瞬斩两帧都在伤害窗内（p=0.5 与 p=1.0）
        protected override float DamageWindowEnd => 1f;

        /// <summary>拍 3 收鞘长蓄</summary>
        private bool IsCharge => ComboStage == 3;
        /// <summary>收鞘持刀角（背身斜下，OnStageInit 缓存）</summary>
        private float sheathAngle;

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
                    }
                    else {
                        float s = (q - freeze) / (1f - freeze);
                        mainAngle = ArcEnd + (swingDir * 0.06f * EaseOutQuad(s));
                        mainReach = FullReach * MathHelper.Lerp(1.02f, 0.8f, s * s);
                    }
                    break;
                }
            }

            mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
        }

        /// <summary>收鞘几何：刀贴身横持，全程静止无伤，只有呼吸感</summary>
        private void SheathTransform(int phase) {
            slashProgress = 0f;
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

        //==================== 音效 ====================

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

        /// <summary>命中：低沉居合音</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.42f, Pitch = -0.58f }, target.Center);
            }
        }
    }
}
