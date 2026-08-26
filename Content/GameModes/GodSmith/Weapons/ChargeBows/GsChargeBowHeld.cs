using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
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

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.ChargeBows
{
    /// <summary>
    /// 弓·前困难族共享手持弹幕：21 把弓共用，按持有弓的 <see cref="GsChargeBowScheme"/> 取参。<br/>
    /// 相位机：搭箭 Nock → 蓄力 Draw（跟准星，档位随持弓时长爬升）→ 释放 Loose → 自杀。
    /// 松开左键即释放；过满窗口耗尽自动失稳（回落 T2 数值发射并附加疲劳帧）。<br/>
    /// 网络：owner 权威推进，ai[0] = 相位×10+档位（变更即 netUpdate），远端只按 ai[0] 与同步的
    /// DownLeft/ToMouse 画拉弓姿态；释放箭只在 owner 端生成；蓄力中死亡/切物品由 owner 端自杀、不发射不耗弹。
    /// ai[1] = 展示用弹药物品 ID（owner 低频更新，远端画搭箭贴图用）
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
        /// <summary>展示用弹药物品 ID</summary>
        private ref float AmmoShowType => ref Projectile.ai[1];

        private int Phase => (int)PackedState / 10;
        private int Tier => (int)PackedState % 10;

        /// <summary>相内计时（各端自走，纯表现量）</summary>
        private int timer;
        /// <summary>蓄力帧计数（各端自走；owner 端为权威档位依据）</summary>
        private int drawFrames;
        /// <summary>释放相时长（owner 端权威，含 T0 补齐与失稳疲劳）</summary>
        private int looseDur = GsChargeBowScheme.LooseFrames;
        /// <summary>音画状态侦测：上次观察到的档位/相位（各端本地）</summary>
        private int seenTier;
        private int seenPhase;
        /// <summary>档位达成弓身脉闪</summary>
        private int flashTimer;
        /// <summary>T3 星芒计时</summary>
        private int starTimer;

        private int boundBowType;
        private GsChargeBowScheme scheme;
        //阈值缓存（各端按同步的 Item 各自折算，结果一致）
        private int nockDur = GsChargeBowScheme.NockFrames;
        private int t1F, t2F, t3F, overF;

        private Vector2 BowCenter => Owner.GetPlayerStabilityCenter() + ToMouseA.ToRotationVector2() * 13f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 6;
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
            t1F = cs.Tier1Frames(item, Owner);
            t2F = cs.Tier2Frames(item, Owner);
            t3F = cs.Tier3Frames(item, Owner);
            overF = cs.OverloadFrames(item, Owner);
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
            UpdatePose();
            UpdateArms();
            UpdateStateMachine();
            WatchTierCues();
            EmitDrawParticles();
        }

        //==================== 状态机 ====================

        private void UpdateStateMachine() {
            bool owner = Projectile.IsOwnedByLocalPlayer();
            switch (Phase) {
                case PhaseNock: {
                    timer++;
                    if (owner) {
                        UpdateAmmoPreview();
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
                    UpdateAmmoPreview();
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

        /// <summary>owner 端低频更新展示弹药（远端画搭箭贴图用）</summary>
        private void UpdateAmmoPreview() {
            ShootState state = Owner.GetShootState();
            int show = state.HasAmmo ? state.UseAmmoItemType : 0;
            if ((int)AmmoShowType != show) {
                AmmoShowType = show;
                NetUpdate();
            }
        }

        //==================== 释放（owner 端权威） ====================

        /// <summary>释放：恰好一次 PickAmmo（原版消耗判定与弹药节约照常），按档位换型/打标生成主箭与衍生</summary>
        private void OwnerRelease(int shotTier, bool fatigued) {
            int drawSpent = drawFrames;
            bool fired = false;
            if (Owner.PickAmmo(Item, out int shootType, out float speed, out int damage, out float knockback,
                out int usedAmmoItemId, false)) {
                int finalType = scheme.TransformShootType(shootType, shotTier);
                int finalDamage = Math.Max(1, (int)(damage * scheme.TierDamageMul(Item, shotTier)));
                Vector2 muzzle = BowCenter + UnitToMouseV * 6f;
                Vector2 velocity = UnitToMouseV * speed * GsChargeBowScheme.TierSpeedMul(shotTier);
                EntitySource_ItemUse_WithAmmo source = new(Owner, Item, usedAmmoItemId, "GsChargeBow");
                if (!scheme.CustomLoose(shotTier, finalType)) {
                    scheme.StampNext(shotTier, GsChargeBowScheme.KindMain);
                    Projectile.NewProjectile(source, muzzle, velocity, finalType, finalDamage, knockback, Owner.whoAmI);
                }
                scheme.OnLoose(Owner, Item, source, muzzle, velocity, finalType, finalDamage, knockback, shotTier);
                scheme.ClearStamp();
                fired = true;
            }

            //T0 轻放回拍补齐：快速连点的循环时长向原版 useTime 对齐（0.85 伤 × 原版射速 = 保底不奖励）
            looseDur = GsChargeBowScheme.LooseFrames;
            if (shotTier <= 0) {
                looseDur = Math.Max(GsChargeBowScheme.LooseFrames, (int)(t1F * 0.92f) - nockDur - drawSpent);
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

        //==================== 音画侦测（各端统一：观察 ai[0] 变化处发声） ====================

        private void WatchTierCues() {
            int tier = Tier;
            int phase = Phase;

            if (tier != seenTier && phase == PhaseDraw && tier > seenTier) {
                if (!VaultUtils.isServer) {
                    if (tier == 2) {
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.25f }, Projectile.Center);
                        flashTimer = 6;
                    }
                    else if (tier == 3) {
                        SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = -0.1f }, Projectile.Center);
                        flashTimer = 8;
                        starTimer = 5;
                        if (Owner.whoAmI == Main.myPlayer) {
                            Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center,
                                UnitToMouseV, 2f, 6f, 8, 900f, "GsChargeBow"));
                        }
                    }
                }
            }

            if (phase == PhaseLoose && seenPhase != PhaseLoose && !VaultUtils.isServer) {
                //失稳推断：上一观测档为 T3、进 Loose 时档回落 2，读作疲弦闷响
                bool destabilized = seenTier >= 3 && tier == 2;
                float pitch = destabilized ? -0.3f : 0.05f + 0.08f * tier;
                SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.9f, Pitch = pitch }, Projectile.Center);
                if (tier >= 2 && !destabilized) {
                    //满蓄出手的口部闪光
                    PRTLoader.NewParticle<PRT_Light>(BowCenter + UnitToMouseV * 8f, UnitToMouseV * 2f,
                        scheme.TrailHot, 0.14f + 0.04f * tier)?.Configure(8, 0.8f);
                }
            }

            seenTier = tier;
            seenPhase = phase;
            if (flashTimer > 0) {
                flashTimer--;
            }
            if (starTimer > 0) {
                starTimer--;
            }
        }

        /// <summary>蓄力期弦上火星：档位越高越密（客户端）</summary>
        private void EmitDrawParticles() {
            if (VaultUtils.isServer || Phase != PhaseDraw || Tier < 1) {
                return;
            }
            if (!Main.rand.NextBool(Tier >= 3 ? 2 : Tier >= 2 ? 3 : 6)) {
                return;
            }
            Vector2 nock = GetNockWorldPos();
            PRTLoader.NewParticle<PRT_Spark>(nock + Main.rand.NextVector2Circular(3f, 3f),
                Main.rand.NextVector2Circular(0.5f, 0.5f) - UnitToMouseV * 0.3f,
                Tier >= 3 ? scheme.TrailHot : scheme.TrailMain, Main.rand.NextFloat(0.18f, 0.3f))
                ?.Configure(false, Main.rand.Next(8, 14));
            Lighting.AddLight(nock, scheme.TrailMain.ToVector3() * (0.12f * Tier));
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

        /// <summary>搭箭点：弓中心沿瞄准反向随拉弓进度与档位后移，读作弦被拉满</summary>
        private Vector2 GetNockWorldPos() {
            float tierBack = Tier switch { 3 => 9f, 2 => 7f, 1 => 4f, _ => 0f };
            float back = 2f + DrawProgress() * 8f + tierBack;
            return BowCenter - ToMouseA.ToRotationVector2() * back;
        }

        //==================== 绘制 ====================

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
            //释放回弹：弓身前送一记
            if (Phase == PhaseLoose) {
                drawCenter += ToMouseA.ToRotationVector2() * MathHelper.Clamp(3f - timer, 0f, 3f);
            }
            //失稳前 12 帧弓身高频抖动（各端 drawFrames 接近一致，读秒警告一致出现）
            if (Phase == PhaseDraw && Tier >= 3 && overF - drawFrames < 12) {
                drawCenter += new Vector2(MathF.Sin(drawFrames * 1.7f + Projectile.identity), MathF.Cos(drawFrames * 2.1f)) * 1.2f;
            }

            SpriteEffects effect = DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Vector2 screenPos = drawCenter - Main.screenPosition;

            //档位辉光垫底（SoftGlow 加色 A=0）
            int tier = Tier;
            if (tier >= 2 || flashTimer > 0) {
                Texture2D glowTex = CWRAsset.SoftGlow.Value;
                float flash = flashTimer / 8f;
                Color glow = (tier >= 3 ? scheme.TrailHot : scheme.TrailMain) * (0.25f + 0.1f * tier + flash * 0.4f);
                glow.A = 0;
                Main.EntitySpriteDraw(glowTex, screenPos, null, glow, 0f, glowTex.Size() / 2f,
                    0.55f + 0.1f * tier + flash * 0.2f, SpriteEffects.None);
            }

            //弓弦：两端锚点连到搭箭点的两段直线（原版贴图静态弦上叠动态弦）
            if (Phase == PhaseDraw) {
                Vector2 perp = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();
                float halfString = bowTex.Height * 0.36f;
                Vector2 top = drawCenter + perp * halfString;
                Vector2 bottom = drawCenter - perp * halfString;
                Vector2 nock = GetNockWorldPos() + (drawCenter - Projectile.Center);
                Color stringColor = Color.Lerp(lightColor, Color.White, 0.3f) * 0.8f;
                DrawLine(top, nock, stringColor, 2f);
                DrawLine(nock, bottom, stringColor, 2f);
                DrawNockedArrow(nock, lightColor);
            }

            //弓体
            Main.EntitySpriteDraw(bowTex, screenPos, null, lightColor, Projectile.rotation,
                bowTex.Size() / 2f, 1f, effect);

            //过满档弓身加色重影
            if (tier >= 3) {
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity * 0.53f);
                Color hot = scheme.TrailHot * (0.35f * pulse);
                hot.A = 0;
                Main.EntitySpriteDraw(bowTex, screenPos, null, hot, Projectile.rotation,
                    bowTex.Size() / 2f, 1.06f, effect);
            }

            //T3 达成星芒（StarTexture_White 加色，identity 定相）
            if (starTimer > 0) {
                Texture2D star = CWRAsset.StarTexture_White.Value;
                float p = starTimer / 5f;
                Color starColor = scheme.TrailHot * (0.8f * p);
                starColor.A = 0;
                Main.EntitySpriteDraw(star, screenPos, null, starColor, Projectile.identity * 0.7f,
                    star.Size() / 2f, 0.16f + 0.1f * (1f - p), SpriteEffects.None);
            }

            return false;
        }

        private static void DrawLine(Vector2 start, Vector2 end, Color color, float thickness) {
            Vector2 toEnd = end - start;
            float length = toEnd.Length();
            if (length < 1f) {
                return;
            }
            Main.EntitySpriteDraw(VaultAsset.placeholder2.Value, start - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                color, toEnd.ToRotation(), new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0);
        }

        /// <summary>搭在弦上的箭矢：按展示弹药画，随档位后移；无限弹药映射到实体箭贴图</summary>
        private void DrawNockedArrow(Vector2 nock, Color lightColor) {
            int ammoItemType = (int)AmmoShowType;
            if (ammoItemType <= ItemID.None || ammoItemType >= TextureAssets.Item.Length) {
                return;
            }
            Main.instance.LoadItem(ammoItemType);
            Texture2D arrowTex = TextureAssets.Item[ammoItemType].Value;
            Item ammoItem = new(ammoItemType);
            if (!ammoItem.consumable) {
                int showType = ItemID.WoodenArrow;
                if (VaultUtils.ProjectileToSafeAmmoMap.TryGetValue(ammoItem.shoot, out int mapped)) {
                    showType = mapped;
                }
                Main.instance.LoadItem(showType);
                arrowTex = TextureAssets.Item[showType].Value;
            }
            Main.EntitySpriteDraw(arrowTex, nock - Main.screenPosition, null, lightColor,
                Projectile.rotation + MathHelper.PiOver2, new Vector2(arrowTex.Width / 2f, arrowTex.Height),
                1f, SpriteEffects.FlipVertically);
        }
    }
}
