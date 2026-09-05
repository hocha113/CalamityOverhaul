using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows
{
    /// <summary>
    /// 弓·蓄力族共享手持弹幕：21 把弓共用，按持有弓的 <see cref="GsChargeBowScheme"/> 取参。<br/>
    /// 相位机：搭箭 Nock → 蓄力 Draw（跟准星，档位随持弓时长爬升）→ 释放 Loose → 自杀。
    /// 松开左键即释放；过满窗口耗尽自动失稳（回落 T2 数值发射并附加疲劳帧）。<br/>
    /// 力度表现全走姿态与镜头（不产生绘制层）：档位越高弓身前送越深、过满全程弦上微颤并在失稳前加剧、
    /// 释放弓身按档位前弹、满蓄以上 owner 端震屏。<br/>
    /// 网络：owner 权威推进，ai[0] = 相位×10+档位（变更即 netUpdate），远端只按 ai[0] 与同步的
    /// DownLeft/ToMouse 画拉弓姿态；释放箭只在 owner 端生成；蓄力中死亡/切物品由 owner 端自杀、不发射不耗弹
    /// </summary>
    internal class GsChargeBowHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //held 本身不参与命中，不注册新键，显示名指向原版键
        public override LocalizedText DisplayName => Language.GetText("ItemName.WoodenBow");

        private const int PhaseNock = 0;
        private const int PhaseDraw = 1;
        private const int PhaseLoose = 2;

        /// <summary>相位×10 + 档位（owner 写，各端读）</summary>
        private ref float PackedState => ref Projectile.ai[0];

        private int Phase => (int)PackedState / 10;
        private int Tier => (int)PackedState % 10;

        /// <summary>相内计时（各端自走，纯表现量）</summary>
        private int timer;
        /// <summary>蓄力帧计数（各端自走；owner 端为权威档位依据）</summary>
        private int drawFrames;
        /// <summary>释放相时长（owner 端权威，含 T0 补齐与失稳疲劳）</summary>
        private int looseDur = GsChargeBowScheme.LooseFrames;
        /// <summary>音效状态侦测：上次观察到的档位/相位（各端本地）</summary>
        private int seenTier;
        private int seenPhase;
        /// <summary>档位弓身前送量（T1/T2/T3 弓向前顶体现深拉，每帧 0.5px 趋近；各端按同步档位自走）</summary>
        private float tierLead;
        /// <summary>释放时的档位（各端在进 Loose 相时记下，喂释放前弹幅度）</summary>
        private int looseTier;

        private int boundBowType;
        private GsChargeBowScheme scheme;
        //阈值缓存（各端按同步的 Item 各自折算，结果一致）
        private int nockDur = GsChargeBowScheme.NockFrames;
        private int baseF, t1F, t2F, t3F, overF;
        //持弓距离（TryBind 时按弦锚库折算）
        private float holdDist = 13f;

        private Vector2 BowCenter => Owner.GetPlayerStabilityCenter() + ToMouseA.ToRotationVector2() * (holdDist + tierLead);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 6;
            //heldProj 只走玩家第 27 层内联绘制（PlayerDrawLayers 无 hide 检查），
            //不设 hide 会在 Main.DrawProjectiles 再画一遍成加色层 2× 亮度
            Projectile.hide = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        public override void Initialize() {
            TryBind();
        }

        /// <summary>绑定手持弓与方案，折算蓄力阈值（各端用同步的 Item 独立算，结果一致）</summary>
        private bool TryBind() {
            Item item = Item;
            if (item == null || item.IsAir) {
                return false;
            }
            if (!GodSmithScheme.TryGetScheme(item.type, out GodSmithScheme raw) || raw is not GsChargeBowScheme cs) {
                return false;
            }
            scheme = cs;
            boundBowType = item.type;
            float speed = Owner.GetWeaponAttackSpeed(item);
            if (speed <= 0f) {
                speed = 1f;
            }
            nockDur = Math.Max(1, (int)MathF.Round(GsChargeBowScheme.NockFrames / speed));
            baseF = cs.BaseFrames(item, Owner);
            t1F = cs.Tier1Frames(item, Owner);
            t2F = cs.Tier2Frames(item, Owner);
            t3F = cs.Tier3Frames(item, Owner);
            overF = cs.OverloadFrames(item, Owner);
            holdDist = GsBowStringLib.HoldDistance(item.type);
            return true;
        }

        public override void AI() {
            Projectile.timeLeft = 6;//丢包/掉线兜底自清

            if (scheme == null && !TryBind()) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.Kill();
                }
                return;
            }

            //蓄力中切物品/死亡：owner 端自杀，不发射不耗弹
            if (Projectile.IsOwnedByLocalPlayer()) {
                if (Owner.dead || !Owner.active || Owner.CCed || Item.type != boundBowType) {
                    Projectile.Kill();
                    return;
                }
            }

            SetHeld();
            UpdateTierLead();
            UpdatePose();
            UpdateArms();
            UpdateStateMachine();
            WatchTierCues();
        }

        /// <summary>档位前送量趋近目标（每帧 0.5px，T2→T3 的 2.5px 差 5 帧到位，消单帧瞬跳）；深拉读作弓被顶出去</summary>
        private void UpdateTierLead() {
            float target = Phase == PhaseDraw ? Tier switch { 3 => 5.5f, 2 => 3f, 1 => 1f, _ => 0f } : 0f;
            tierLead += MathHelper.Clamp(target - tierLead, -0.5f, 0.5f);
        }

        //==================== 状态机 ====================

        private void UpdateStateMachine() {
            bool owner = Projectile.IsOwnedByLocalPlayer();
            switch (Phase) {
                case PhaseNock: {
                    timer++;
                    if (owner) {
                        if (timer >= nockDur) {
                            SetState(PhaseDraw, 0);
                            timer = 0;
                        }
                        //搭箭期就松手：按 T0 轻放走
                        if (!DownLeft) {
                            OwnerRelease(0, false);
                        }
                    }
                    break;
                }
                case PhaseDraw: {
                    drawFrames++;
                    if (!owner) {
                        break;
                    }
                    int wantTier = drawFrames >= t3F ? 3 : drawFrames >= t2F ? 2 : drawFrames >= t1F ? 1 : 0;
                    if (wantTier != Tier) {
                        SetState(PhaseDraw, wantTier);
                    }
                    if (drawFrames >= overF) {
                        //失稳：过满窗口耗尽，回落 T2 数值发射并附加疲劳
                        OwnerRelease(2, true);
                    }
                    else if (!DownLeft) {
                        OwnerRelease(Tier, false);
                    }
                    break;
                }
                default: {
                    timer++;
                    if (owner && timer >= looseDur) {
                        Projectile.Kill();
                    }
                    break;
                }
            }
        }

        /// <summary>owner 写包态并同步</summary>
        private void SetState(int phase, int tier) {
            PackedState = phase * 10 + tier;
            NetUpdate();
        }

        //==================== 释放（owner 端权威） ====================

        /// <summary>释放：恰好一次 PickAmmo（原版消耗判定与弹药节约照常），按档位换型/打标生成主箭与衍生；击退随档位加码</summary>
        private void OwnerRelease(int shotTier, bool fatigued) {
            int drawSpent = drawFrames;
            bool fired = false;
            if (Owner.PickAmmo(Item, out int shootType, out float speed, out int damage, out float knockback,
                out int usedAmmoItemId, false)) {
                int finalType = scheme.TransformShootType(shootType, shotTier);
                int finalDamage = Math.Max(1, (int)(damage * scheme.TierDamageMul(Item, shotTier)));
                float finalKnockback = knockback * GsChargeBowScheme.TierKnockbackMul(shotTier);
                Vector2 muzzle = BowCenter + UnitToMouseV * 6f;
                Vector2 velocity = UnitToMouseV * speed * GsChargeBowScheme.TierSpeedMul(shotTier);
                EntitySource_ItemUse_WithAmmo source = new(Owner, Item, usedAmmoItemId, "GsChargeBow");
                if (!scheme.CustomLoose(shotTier, finalType)) {
                    scheme.StampNext(shotTier, GsChargeBowScheme.KindMain);
                    Projectile.NewProjectile(source, muzzle, velocity, finalType, finalDamage, finalKnockback, Owner.whoAmI);
                }
                scheme.OnLoose(Owner, Item, source, muzzle, velocity, finalType, finalDamage, finalKnockback, shotTier);
                scheme.ClearStamp();
                fired = true;
            }

            //T0 轻放回拍补齐：快速连点的循环时长向原版一发周期 U 对齐（0.85 伤 × 原版射速 = 保底不奖励；
            //阈值提速后 T1 已远短于 U，故对齐基准取 baseF 而非 t1F）
            looseDur = GsChargeBowScheme.LooseFrames;
            if (shotTier <= 0) {
                looseDur = Math.Max(GsChargeBowScheme.LooseFrames, (int)(baseF * 0.92f) - nockDur - drawSpent);
            }
            if (fatigued) {
                looseDur += GsChargeBowScheme.FatigueFrames;
            }
            if (!fired) {
                looseDur = GsChargeBowScheme.LooseFrames;
            }

            timer = 0;
            SetState(PhaseLoose, shotTier);
        }

        //==================== 音效侦测（各端统一：观察 ai[0] 变化处发声） ====================

        private void WatchTierCues() {
            int tier = Tier;
            int phase = Phase;

            if (tier != seenTier && phase == PhaseDraw && tier > seenTier) {
                if (!VaultUtils.isServer) {
                    if (tier == 2) {
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.25f }, Projectile.Center);
                    }
                    else if (tier == 3) {
                        SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = -0.1f }, Projectile.Center);
                        if (Owner.whoAmI == Main.myPlayer && CWRClientConfig.Instance.ScreenVibration) {
                            Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center,
                                UnitToMouseV, 2f, 6f, 8, 900f, "GsChargeBow"));
                        }
                    }
                }
            }

            if (phase == PhaseLoose && seenPhase != PhaseLoose) {
                looseTier = tier;
                if (!VaultUtils.isServer) {
                    //失稳推断：上一观测档为 T3、进 Loose 时档回落 2，读作疲弦闷响
                    bool destabilized = seenTier >= 3 && tier == 2;
                    float pitch = destabilized ? -0.3f : 0.05f + 0.08f * tier;
                    SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.9f + 0.05f * tier, Pitch = pitch }, Projectile.Center);
                    //满蓄以上出手的镜头后坐：过满一记重推，失稳只剩闷震
                    if (tier >= 2 && Owner.whoAmI == Main.myPlayer && CWRClientConfig.Instance.ScreenVibration) {
                        float strength = destabilized ? 1.5f : tier >= 3 ? 4f : 2.5f;
                        Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center,
                            -UnitToMouseV, strength, 7f, 9, 900f, "GsChargeBowLoose"));
                    }
                }
            }

            seenTier = tier;
            seenPhase = phase;
        }

        //==================== 姿态 ====================

        private void UpdatePose() {
            Projectile.rotation = ToMouseA;
            Owner.ChangeDir(ToMouse.X >= 0 ? 1 : -1);
            Projectile.Center = BowCenter;
        }

        /// <summary>后手持弓瞄准，前手随拉弓进度收拢（镜像 BarrenBow 姿态契约）</summary>
        private void UpdateArms() {
            float holdArmRot = Projectile.rotation - MathHelper.PiOver2 * SafeGravDir;
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, holdArmRot);

            float pull = DrawProgress();
            Player.CompositeArmStretchAmount stretch = Player.CompositeArmStretchAmount.Full;
            if (pull > 0.25f) {
                stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
            }
            if (pull > 0.5f) {
                stretch = Player.CompositeArmStretchAmount.Quarter;
            }
            if (pull > 0.75f) {
                stretch = Player.CompositeArmStretchAmount.None;
            }
            Owner.SetCompositeArmFront(true, stretch, holdArmRot);

            Owner.itemRotation = MathHelper.WrapAngle(Projectile.rotation * Owner.direction);
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
        }

        /// <summary>拉弓进度 0~1（T2 阈值即满弦，之后维持）</summary>
        private float DrawProgress() {
            if (Phase == PhaseLoose) {
                return 0f;
            }
            if (Phase == PhaseNock) {
                return 0.05f;
            }
            return MathHelper.Clamp(drawFrames / (float)Math.Max(1, t2F), 0.08f, 1f);
        }

        //==================== 绘制 ====================

        /// <summary>弓的物品贴图本体一笔（不画弦、不画搭箭、无辉光）；释放前弹与过满颤动只改位移</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (scheme == null) {
                return false;
            }
            int bowType = boundBowType;
            if (bowType <= 0 || bowType >= TextureAssets.Item.Length) {
                return false;
            }
            Main.instance.LoadItem(bowType);
            Texture2D bowTex = TextureAssets.Item[bowType].Value;

            Vector2 drawCenter = Projectile.Center;
            //释放前弹：弓身按释放档位向前甩一记再回坐（T0 3px → T3 9px），过冲后两帧内收回
            if (Phase == PhaseLoose) {
                float snap = 3f + 2f * looseTier;
                drawCenter += ToMouseA.ToRotationVector2() * MathF.Max(0f, snap - timer * (snap / 3f));
            }
            //过满全程弦上微颤（0.5px），失稳前 14 帧加剧到 1.6px（各端 drawFrames 接近一致，读秒警告一致出现）
            if (Phase == PhaseDraw && Tier >= 3) {
                float urgency = MathHelper.Clamp((14f - (overF - drawFrames)) / 14f, 0f, 1f);
                float amp = 0.5f + 1.1f * urgency;
                drawCenter += new Vector2(MathF.Sin(drawFrames * 1.7f + Projectile.identity), MathF.Cos(drawFrames * 2.1f)) * amp;
            }

            SpriteEffects effect = DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Main.EntitySpriteDraw(bowTex, drawCenter - Main.screenPosition, null, lightColor, Projectile.rotation,
                bowTex.Size() / 2f, 1f, effect);
            return false;
        }
    }
}
