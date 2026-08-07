using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Helen;
using CalamityOverhaul.Content.Scenarios.SupCal.SupCalDisplayTexts;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>鬼砍刀 shader（域内加载，不动 EffectLoader）</summary>
    internal class OniMacheteAssets
    {
        /// <summary>挥砍刀光弧带</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect OniMacheteSlash { get; private set; }
        /// <summary>鬼手硫火火鞘</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect OniMacheteFlame { get; private set; }
        /// <summary>熔金裂纹（地面 decal / NPC 覆盖双 technique）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect OniMacheteCrack { get; private set; }
        /// <summary>鬼手之火彗尾</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect OniMacheteComet { get; private set; }
        /// <summary>扼颈全屏暗角</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect OniMacheteGrip { get; private set; }
    }

    /// <summary>
    /// 鬼砍刀。挥砍/命中积硫火压制，鬼手出击消耗；耗尽躁动（更凶、会扼颈），再挥命中压服
    /// </summary>
    [VaultLoaden(CWRConstant.Item_Melee)]
    internal class OniMachete : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "OniMachete";
        public static Texture2D OniArm = null;
        public static Texture2D OniHand = null;

        public static LocalizedText SuppressTip { get; private set; }

        public override void SetStaticDefaults() {
            SuppressTip = this.GetLocalization(nameof(SuppressTip)
                , () => "挥砍与命中积累硫火压制，六只鬼手的每次出击都在消耗它\n压制熄灭时鬼手躁动：它们打得更凶，也随时会回头掐住你的脖子");
        }

        public override void SetDefaults() {
            Item.width = Item.height = 45;
            Item.damage = 2666;
            Item.DamageType = DamageClass.Generic;
            Item.useTime = Item.useAnimation = 20;//真实节奏由 BladeActive 把关（普通 22/重斩 30 帧）
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useTurn = true;
            Item.knockBack = 7f;
            Item.rare = ItemRarityID.Red;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<OniMacheteHeld>();
            Item.shootSpeed = 12f;
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            if (InWorldBossPhase.Level11) {
                damage *= 1.25f;
            }
            if (InWorldBossPhase.Level12) {
                damage *= 1.25f;
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            TooltipLine mechanic = new(Mod, "OniSuppress", SuppressTip.Value) {
                OverrideColor = new Color(255, 180, 70)
            };
            tooltips.Add(mechanic);
            if (HalibutStorySync.ReadSupCal(d => d.SupCalYharonQuestReward, d => d.SupCalYharonQuestReward)) {
                TooltipLine line = new(Mod, "Story", SupCalDisplayText.Story3.Value);
                line.OverrideColor = Color.OrangeRed;
                tooltips.Add(line);
            }
        }

        public override bool CanUseItem(Player player) {
            //余光期旧挥不挡下一刀（只画刀光、不控角色）
            int type = ModContent.ProjectileType<OniMacheteHeld>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type
                    && proj.ModProjectile is OniMacheteHeld held && held.BladeActive) {
                    return false;
                }
            }
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            OniMachetePlayer mp = player.GetModPlayer<OniMachetePlayer>();
            int beat = mp.StepCombo();
            float swingDir = mp.NextSwingFlip();
            Projectile.NewProjectile(source, player.Center, velocity, type
                , damage, knockback, player.whoAmI, beat, swingDir);
            return false;
        }

        /// <summary>削甲（弹幕管线统一调用）</summary>
        internal static void ApplyGoldRend(NPC target, ref NPC.HitModifiers modifiers) {
            target.defense = Math.Max(0, target.defense - 10);
            if (modifiers.SuperArmor || target.defense > 999) {
                return;
            }
            modifiers.DefenseEffectiveness *= 0f;
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)
            => ApplyGoldRend(target, ref modifiers);

        public override void HoldItem(Player player) {
            //只在owner端补手
            if (player.whoAmI != Main.myPlayer || player.dead) {
                return;
            }

            int handType = ModContent.ProjectileType<OniHandMinion>();
            if (player.ownedProjectileCounts[handType] >= OniHandMinion.HandCount) {
                return;
            }

            //按缺失编队位补，每帧至多一只
            Span<bool> taken = stackalloc bool[OniHandMinion.HandCount];
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == handType) {
                    int idx = (int)proj.ai[0];
                    if (idx >= 0 && idx < OniHandMinion.HandCount) {
                        taken[idx] = true;
                    }
                }
            }
            for (int i = 0; i < OniHandMinion.HandCount; i++) {
                if (taken[i]) {
                    continue;
                }
                int damage = (int)(player.GetWeaponDamage(Item, true) * OniHandMinion.HandDamageFactor);
                Projectile.NewProjectile(player.FromObjectGetParent(), player.Center, Vector2.Zero
                    , handType, damage, Item.knockBack * 2f, player.whoAmI, i);
                break;
            }
        }
    }

    /// <summary>鬼砍刀玩家态，硫火压制 / 躁动 / 连击 / 扼颈暗角（实例数据，不落 static）</summary>
    internal class OniMachetePlayer : ModPlayer
    {
        public const float SuppressionMax = 100f;
        /// <summary>躁动→忠仆回归阈值（迟滞）</summary>
        public const float RecoverThreshold = 25f;
        /// <summary>压制不足警告线（火鞘变薄断续）</summary>
        public const float LowLine = 30f;
        //鬼手在场被动流失约 3.6/秒
        private const float PassiveDrain = 0.06f;

        /// <summary>硫火压制 0..100，挥砍/命中积、出击耗</summary>
        public float Suppression = SuppressionMax;
        /// <summary>压制耗尽后的躁动</summary>
        public bool Restless;
        /// <summary>扼颈冷却（帧）</summary>
        public int GripCooldown;
        /// <summary>扼颈暗角包络 0..1（本地视觉）</summary>
        public float GripVignette;

        private int comboIndex;
        private int comboResetTimer;
        private bool swingFlip;
        private bool lowWarned;

        public bool HoldingMachete => Player.HeldItem != null
            && Player.HeldItem.type == ModContent.ItemType<OniMachete>();

        public int StepCombo() {
            int beat = comboIndex % OniMacheteHeld.BeatCount;
            comboIndex++;
            comboResetTimer = 45;
            return beat;
        }

        public float NextSwingFlip() {
            swingFlip = !swingFlip;
            return swingFlip ? 1f : -1f;
        }

        public void AddSuppression(float amount)
            => Suppression = MathHelper.Clamp(Suppression + amount, 0f, SuppressionMax);

        /// <summary>鬼手出击消耗（躁动期不扣）</summary>
        public void ConsumeSuppression(float amount) {
            if (!Restless) {
                Suppression = Math.Max(0f, Suppression - amount);
            }
        }

        public void PushGripVignette(float strength)
            => GripVignette = MathHelper.Clamp(MathF.Max(GripVignette, strength), 0f, 1f);

        public override void PostUpdateMiscEffects() {
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                comboIndex = 0;
            }
            if (GripCooldown > 0) {
                GripCooldown--;
            }
            GripVignette = Math.Max(0f, GripVignette - 0.03f);

            bool handsOut = Player.ownedProjectileCounts[ModContent.ProjectileType<OniHandMinion>()] > 0;
            if (handsOut && HoldingMachete) {
                Suppression = Math.Max(0f, Suppression - PassiveDrain);

                if (!Restless && Suppression <= 0f) {
                    EnterRestless();
                }
                if (Restless && Suppression >= RecoverThreshold) {
                    Restless = false;
                    lowWarned = false;
                    if (Player.whoAmI == Main.myPlayer) {
                        SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.6f, Pitch = -0.2f }, Player.Center);
                    }
                }
                if (!Restless && Suppression < LowLine && !lowWarned) {
                    lowWarned = true;
                    if (Player.whoAmI == Main.myPlayer) {
                        //压制走低预警音
                        SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.8f, Pitch = -0.4f }, Player.Center);
                    }
                }
                if (lowWarned && Suppression >= LowLine + 15f) {
                    lowWarned = false;
                }
            }
            else {
                //收刀或手散尽，压制回升
                Suppression = Math.Min(SuppressionMax, Suppression + 0.5f);
                if (Restless && Suppression >= RecoverThreshold) {
                    Restless = false;
                }
            }
        }

        private void EnterRestless() {
            Restless = true;
            //首次躁动缓冲，扼颈不即刻扑脸
            GripCooldown = Math.Max(GripCooldown, 150);
            if (Player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.7f, Pitch = -0.45f }, Player.Center);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.8f, Pitch = -0.7f }, Player.Center);
            }
        }
    }

    /// <summary>扼颈，移速迟滞，不掉伤不禁跳</summary>
    internal class OniNeckGripDebuff : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private LocalizedText displayNameCache;
        private LocalizedText descriptionCache;
        public override LocalizedText DisplayName
            => displayNameCache ??= this.GetLocalization(nameof(DisplayName), () => "鬼手扼颈");
        public override LocalizedText Description
            => descriptionCache ??= this.GetLocalization(nameof(Description), () => "一只鬼手正掐着你的脖子，行动变得沉重");

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            //逐帧刷新短时长，不显示倒计时
            Main.buffNoTimeDisplay[Type] = true;
            //加载期触碰，抢在 tML 惰性注册前落默认值
            _ = DisplayName;
            _ = Description;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.moveSpeed *= 0.62f;
            player.runAcceleration *= 0.72f;
        }
    }

    /// <summary>刀光条带时间轴（Def / 几何 / 双层 quad），调色硫磺橙红+熔金</summary>
    internal static class OniSlashStrip
    {
        /// <summary>子刀光定义（出生冻结）</summary>
        public struct Def
        {
            public int Life;          //总寿命（帧）
            public int ErodeStart;    //侵蚀起点
            public int ErodeFrames;
            public float Rot;         //quad 基准角（含滚转）
            public float Span;        //弧跨度（弧度，<2π）
            public float Thick;       //shader 厚度
            public float HalfX;       //quad 半尺寸
            public float HalfY;       //<HalfX 伪3D压扁
            public float Flip;        //±1 挥动镜像
            public float Opacity;
            public float FrontGlow;
            public float OffsetAlongAim;
            public float Seed;
            public float TailErode;   //起笔端蒸发上限
            public float FlashPower;  //全形白闪强度
            public float GoldVein;    //熔金脉络权重（重斩加强）
        }

        public static float EaseOutBack(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }

        public static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        public static float Erode(in Def d, int lt)
            => SmoothStep01((lt - d.ErodeStart) / (float)d.ErodeFrames);

        public static float Opacity(in Def d, int lt)
            => d.Opacity * (1f - MathHelper.Clamp((lt - (d.Life - 6)) / 6f, 0f, 1f));

        /// <summary>出生爆发缩放，62% 起步 easeOutBack 过冲后外扩</summary>
        public static float BirthScale(in Def d, int lt, int sweepFrames) {
            float burstT = MathHelper.Clamp(lt / (sweepFrames + 2f), 0f, 1f);
            float lifeT = MathHelper.Clamp(lt / (float)d.Life, 0f, 1f);
            return MathHelper.Lerp(0.62f, 1f, EaseOutBack(burstT)) + 0.06f * lifeT;
        }

        /// <summary>全形白闪，张开瞬间过曝 1~2 帧速落</summary>
        public static float Flash(in Def d, int lt, int sweepFrames) {
            float ft = lt - sweepFrames;
            float flash = ft < 0f ? 0f : ft <= 1f ? 1f : MathF.Pow(0.52f, ft - 1f);
            return flash < 0.02f ? 0f : flash * d.FlashPower;
        }

        /// <summary>刀光中线上一点，uc=0..1 沿刃</summary>
        public static Vector2 PointAt(in Def d, Vector2 center, float uc) {
            Vector2 ax = d.Rot.ToRotationVector2();
            Vector2 ay = ax.RotatedBy(MathHelper.PiOver2);
            float env = MathF.Sin(MathF.Pow(uc, 1.75f) * MathF.PI);
            float w = d.Thick * MathF.Pow(MathF.Max(env, 0.0001f), 0.72f);
            float rFrac = 0.90f - w * 0.5f;
            float phi = d.Flip * (uc - 0.5f) * d.Span;
            return center + ax * MathF.Cos(phi) * rFrac * d.HalfX + ay * MathF.Sin(phi) * rFrac * d.HalfY;
        }

        /// <summary>设备态 + 帧级公共 uniform，false=资产未就绪</summary>
        public static bool BeginDraw(GraphicsDevice device, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = OniMacheteAssets.OniMacheteSlash;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            prevBlend = device.BlendState;
            prevRaster = device.RasterizerState;
            prevDepth = device.DepthStencilState;
            if (fx == null || noise == null) {
                return false;
            }

            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            return true;
        }

        public static void EndDraw(GraphicsDevice device
            , BlendState prevBlend, RasterizerState prevRaster, DepthStencilState prevDepth) {
            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>双层，主体色带→白金核心薄条</summary>
        public static void DrawTwoLayers(GraphicsDevice device, Effect fx, in Def d
            , Vector2 center, int lt, int sweepFrames, float sweep) {
            DrawLayer(device, fx, in d, center, lt, sweepFrames, sweep
                , opacityMul: 1f, thickMul: 1f, frontMul: 1f, goldVein: d.GoldVein);
            DrawLayer(device, fx, in d, center, lt, sweepFrames, sweep
                , opacityMul: 0.90f, thickMul: 0.45f, frontMul: 1.3f, goldVein: 0f);
        }

        private static void DrawLayer(GraphicsDevice device, Effect fx, in Def d
            , Vector2 center, int lt, int sweepFrames, float sweep
            , float opacityMul, float thickMul, float frontMul, float goldVein) {
            if (lt < 0 || lt >= d.Life) {
                return;
            }
            float opacity = Opacity(in d, lt) * opacityMul;
            if (opacity <= 0.012f) {
                return;
            }

            float scale = BirthScale(in d, lt, sweepFrames);
            //惯性收势
            float followT = MathHelper.Clamp((lt - sweepFrames) / 13f, 0f, 1f);
            float rotOff = d.Flip * 0.12f * (1f - (1f - followT) * (1f - followT));
            //厚度呼吸，薄入→冲击最厚→消散变薄
            float lifeT = MathHelper.Clamp(lt / (float)d.Life, 0f, 1f);
            float thickIn = VaultUtils.EaseOutCubic(MathHelper.Clamp(lt / (sweepFrames + 2f), 0f, 1f));
            float thickBreath = MathHelper.Lerp(0.70f, 1.10f, thickIn)
                * (1f - 0.40f * SmoothStep01((lifeT - 0.45f) / 0.55f));
            float tailErode = d.TailErode * SmoothStep01((lt - sweepFrames) / (d.Life * 0.7f));

            Vector2 axisX = (d.Rot + rotOff).ToRotationVector2();
            Vector2 axisY = axisX.RotatedBy(MathHelper.PiOver2);
            float hx = d.HalfX * scale;
            float hy = d.HalfY * scale;

            fx.Parameters["uSweep"]?.SetValue(sweep);
            fx.Parameters["uErode"]?.SetValue(Erode(in d, lt));
            fx.Parameters["uTailErode"]?.SetValue(tailErode);
            fx.Parameters["uFlash"]?.SetValue(Flash(in d, lt, sweepFrames));
            fx.Parameters["uOpacity"]?.SetValue(opacity);
            fx.Parameters["uFlip"]?.SetValue(d.Flip);
            fx.Parameters["uSeed"]?.SetValue(d.Seed);
            fx.Parameters["uArcSpan"]?.SetValue(d.Span > 0f ? d.Span : 1f);
            fx.Parameters["uThick"]?.SetValue(d.Thick * thickBreath * thickMul);
            fx.Parameters["uFrontGlow"]?.SetValue(d.FrontGlow * frontMul
                * (lt <= sweepFrames + 1 ? 1f : MathF.Max(0f, 1f - (lt - sweepFrames - 1) / 5f)));
            fx.Parameters["uGoldVein"]?.SetValue(goldVein);

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((center - axisX * hx - axisY * hy).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((center + axisX * hx - axisY * hy).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((center - axisX * hx + axisY * hy).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((center + axisX * hx + axisY * hy).ToVector3(), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }
    }

    /// <summary>
    /// 鬼砍刀手持（每挥一发）。三段=纵劈→反撩→重斩；前摇≥40% 反拉+pow 迟滞后吸，打击 poly(9/12) ease-out。<br/>
    /// 刀光扫掠锁刀身；挥砍/命中喂压制。ai[0]=拍位 0..2，ai[1]=挥向 ±1
    /// </summary>
    internal class OniMacheteHeld : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "OniMachete";
        public override LocalizedText DisplayName
            => ItemLoader.GetItem(ModContent.ItemType<OniMachete>()).DisplayName;

        public const int BeatCount = 3;

        private ref float BeatAi => ref Projectile.ai[0];
        private ref float SwingDirAi => ref Projectile.ai[1];

        private int Beat => (int)BeatAi;
        private bool IsFinisher => Beat >= BeatCount - 1;

        //==== 节拍时长（逻辑帧，受攻速缩放；前摇仍占 ~40%）====
        private float WindupTime => IsFinisher ? 12f : 9f;
        /// <summary>重斩蓄势顶点滞帧</summary>
        private float HoldTime => IsFinisher ? 3f : 0f;
        private float StrikeTime => 5f;
        private float RecoverTime => IsFinisher ? 10f : 8f;
        private float TotalTime => WindupTime + HoldTime + StrikeTime + RecoverTime;
        private float SwingArc => IsFinisher ? 4.6f : 3.4f;
        private float BladeReach => (IsFinisher ? 196f : 175f) * Projectile.scale;
        /// <summary>蓄势收束硬切点，之后停喷粒子</summary>
        private const float ChargeSilenceAt = 0.72f;

        private float elapsed;
        private float speedMul = 1f;
        private int lockedDirection = 1;
        private int swingSign = 1;
        private float startAngle;
        private float endAngle;
        private float currentRotation;
        private float lastRotation;
        private float strikeEased;      //本帧打击段 ease（刀身+刀光扫掠）
        private bool strikeStarted;
        private bool windupSoundPlayed;
        private bool slashBorn;
        private int slashBirth = -1;    //刀光出生帧（elapsed 取整）
        private OniSlashStrip.Def slashDef;
        private int impactHoldFrames;
        private float recoilPulse;

        /// <summary>刀身仍在挥（未入余光），物品不可再挥</summary>
        internal bool BladeActive => elapsed < TotalTime;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 54;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 120;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void Initialize() {
            swingSign = Math.Sign(SwingDirAi);
            if (swingSign == 0) {
                swingSign = 1;
            }

            lockedDirection = Math.Sign(ToMouse.X);
            if (lockedDirection == 0) {
                lockedDirection = Owner.direction;
            }
            Owner.direction = lockedDirection;

            //重斩恒走过顶下劈
            if (IsFinisher) {
                swingSign = lockedDirection;
            }

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }

            float baseAngle = Projectile.velocity.ToRotation();
            startAngle = baseAngle - swingSign * SwingArc * 0.5f;
            endAngle = baseAngle + swingSign * SwingArc * 0.5f;
            currentRotation = lastRotation = startAngle;

            if (IsFinisher) {
                Projectile.damage = (int)(Projectile.damage * 1.35f);
                Projectile.scale *= 1.12f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with { Volume = 0.55f, Pitch = -0.55f, MaxInstances = 3 }, Owner.Center);
                }
            }

            //起手喂一点压制
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.GetModPlayer<OniMachetePlayer>().AddSuppression(3f);
            }

            BuildSlashDef(baseAngle);
        }

        /// <summary>三拍刀光参数，纵劈竖长 / 反撩更立 / 重斩扁弧+熔金</summary>
        private void BuildSlashDef(float aim) {
            float s = Projectile.scale;
            slashDef = Beat switch {
                0 => new OniSlashStrip.Def {
                    Life = 24, ErodeStart = 7, ErodeFrames = 13,
                    Rot = aim + swingSign * 0.14f, Span = 3.15f, Thick = 0.32f,
                    HalfX = 156f * s, HalfY = 212f * s, Flip = swingSign,
                    Opacity = 0.92f, FrontGlow = 2.2f, OffsetAlongAim = 30f * s,
                    TailErode = 0.50f, FlashPower = 0.55f, GoldVein = 0.25f,
                },
                1 => new OniSlashStrip.Def {
                    Life = 24, ErodeStart = 7, ErodeFrames = 13,
                    Rot = aim - swingSign * 0.10f, Span = 3.25f, Thick = 0.34f,
                    HalfX = 175f * s, HalfY = 234f * s, Flip = swingSign,
                    Opacity = 0.95f, FrontGlow = 2.4f, OffsetAlongAim = 42f * s,
                    TailErode = 0.45f, FlashPower = 0.60f, GoldVein = 0.35f,
                },
                _ => new OniSlashStrip.Def {
                    Life = 32, ErodeStart = 9, ErodeFrames = 17,
                    Rot = aim + swingSign * 0.24f, Span = 3.45f, Thick = 0.44f,
                    HalfX = 280f * s, HalfY = 180f * s, Flip = swingSign,
                    Opacity = 1f, FrontGlow = 2.8f, OffsetAlongAim = -26f * s,
                    TailErode = 0.34f, FlashPower = 0.92f, GoldVein = 1f,
                },
            };
            slashDef.Seed = (Projectile.whoAmI * 0.191f + Beat * 0.37f) % 1f;
        }

        private Vector2 SlashCenter => Owner.GetPlayerStabilityCenter()
            + Projectile.velocity.ToRotation().ToRotationVector2() * slashDef.OffsetAlongAim;

        public override bool? CanDamage() {
            if (!BladeActive) {
                return false;
            }
            float strikeStart = WindupTime + HoldTime;
            return elapsed >= strikeStart && elapsed <= strikeStart + StrikeTime + 1f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            //三层判定，刀身线段+刀光弧折线+内侧辐条
            Rectangle greedy = targetHitbox;
            greedy.Inflate(10, 10);
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            Vector2 tip = hand + currentRotation.ToRotationVector2() * BladeReach;
            float cp = 0f;
            if (Collision.CheckAABBvLineCollision(greedy.TopLeft(), greedy.Size(), hand, tip, 44f, ref cp)) {
                return true;
            }

            Vector2 center = SlashCenter;
            float sweepU = MathHelper.Clamp(strikeEased * 1.05f, 0f, 1f);
            float thickWorld = MathF.Max(30f, slashDef.Thick * slashDef.HalfX);
            const int samples = 11;
            Vector2 prev = Vector2.Zero;
            bool hasPrev = false;
            for (int k = 0; k < samples; k++) {
                float uc = 0.05f + 0.90f * (k / (float)(samples - 1));
                if (uc > sweepU) {
                    break;
                }
                Vector2 mid = OniSlashStrip.PointAt(in slashDef, center, uc);
                if (hasPrev && Collision.CheckAABBvLineCollision(greedy.TopLeft(), greedy.Size(), prev, mid, thickWorld, ref cp)) {
                    return true;
                }
                if (k % 3 == 0 && Collision.CheckAABBvLineCollision(greedy.TopLeft(), greedy.Size(), hand, mid, 34f, ref cp)) {
                    return true;
                }
                prev = mid;
                hasPrev = true;
            }
            return false;
        }

        public override void CutTiles() {
            if (CanDamage() != true) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            Vector2 tip = hand + currentRotation.ToRotationVector2() * BladeReach;
            Utils.PlotTileLine(hand, tip, 36f, DelegateMethods.CutTiles);
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<OniMachete>()) {
                Projectile.Kill();
                return;
            }
            if (!BladeActive) {
                //余光期，交还控制，等刀光侵蚀完
                if (!slashBorn || (int)elapsed - slashBirth >= slashDef.Life) {
                    Projectile.Kill();
                    return;
                }
                Projectile.timeLeft = 60;
                elapsed += speedMul;
                return;
            }

            lastRotation = currentRotation;
            float holdEnd = WindupTime + HoldTime;
            float strikeEnd = holdEnd + StrikeTime;

            if (elapsed < WindupTime) {
                WindupMotion();
            }
            else if (elapsed < holdEnd) {
                //滞帧，刀停在最深蓄势位
                strikeEased = 0f;
            }
            else if (elapsed < strikeEnd) {
                StrikeMotion(holdEnd);
            }
            else {
                RecoverMotion(strikeEnd);
            }

            //命中视觉停驻
            if (impactHoldFrames > 0) {
                impactHoldFrames--;
                currentRotation = lastRotation;
            }
            recoilPulse *= 0.75f;

            UpdatePlayerPose();

            Vector2 edgeLight = Owner.GetPlayerStabilityCenter()
                + currentRotation.ToRotationVector2() * BladeReach * 0.7f;
            Lighting.AddLight(edgeLight, 0.85f, 0.42f, 0.10f);

            elapsed += speedMul;
        }

        /// <summary>前摇，easeOut 拉开 + 末端 pow(6) 迟滞后吸</summary>
        private void WindupMotion() {
            float t = elapsed / WindupTime;
            float pull = 0.40f * VaultUtils.EaseOutCubic(t) + 0.22f * MathF.Pow(t, 6f);
            currentRotation = startAngle - swingSign * pull;
            strikeEased = 0f;

            //收束熔金屑，72% 硬切
            if (!VaultUtils.isServer && t < ChargeSilenceAt && Main.rand.NextBool(IsFinisher ? 1 : 2)) {
                Vector2 tip = Owner.GetPlayerStabilityCenter()
                    + currentRotation.ToRotationVector2() * BladeReach * 0.85f;
                Vector2 spawn = tip + Main.rand.NextVector2Unit() * Main.rand.NextFloat(60f, 150f);
                PRTLoader.NewParticle<PRT_OniMacheteGold>(spawn, (tip - spawn) * 0.10f
                    , default, Main.rand.NextFloat(0.30f, 0.55f))
                    ?.Configure(Main.rand.Next(10, 16), gravity: false, cooling: 1.4f);
            }
            //重斩蓄势低鸣
            if (IsFinisher && !VaultUtils.isServer && !windupSoundPlayed && t >= 0.5f) {
                windupSoundPlayed = true;
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.45f, Pitch = -0.5f }, Owner.Center);
            }
        }

        /// <summary>打击段，poly(9/12) ease-out，刀光扫掠锁刀身</summary>
        private void StrikeMotion(float strikeStart) {
            float t = (elapsed - strikeStart) / StrikeTime;
            strikeEased = 1f - MathF.Pow(1f - MathHelper.Clamp(t, 0f, 1f), IsFinisher ? 12f : 9f);
            currentRotation = MathHelper.Lerp(startAngle, endAngle, strikeEased);

            if (!strikeStarted) {
                strikeStarted = true;
                slashBorn = true;
                slashBirth = (int)elapsed;
                if (!VaultUtils.isServer) {
                    //分层音，重挥底鸣+硫火撕风
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.75f, Pitch = IsFinisher ? -0.55f : -0.25f + Beat * 0.12f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.55f, Pitch = 0.15f }, Owner.Center);
                    if (IsFinisher) {
                        SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 0.7f, Pitch = -0.35f }, Owner.Center);
                    }
                }
            }

            //刀光前缘迸屑，喷量∝扫掠增量
            if (!VaultUtils.isServer) {
                Vector2 center = SlashCenter;
                float edgeU = MathHelper.Clamp(strikeEased * 1.02f, 0.06f, 0.94f);
                Vector2 pos = OniSlashStrip.PointAt(in slashDef, center, edgeU);
                Vector2 tangent = (OniSlashStrip.PointAt(in slashDef, center, MathHelper.Clamp(edgeU + 0.04f, 0f, 1f)) - pos)
                    .SafeNormalize(currentRotation.ToRotationVector2());
                int count = t < 0.45f ? (IsFinisher ? 4 : 3) : 1;
                for (int i = 0; i < count; i++) {
                    PRTLoader.NewParticle<PRT_OniMacheteGold>(pos + Main.rand.NextVector2Circular(8f, 8f)
                        , tangent * Main.rand.NextFloat(5f, 12f) + Main.rand.NextVector2Circular(1.5f, 1.5f)
                        , default, Main.rand.NextFloat(0.35f, 0.7f))
                        ?.Configure(Main.rand.Next(14, 24), gravity: true, cooling: 1.1f);
                }
            }
        }

        /// <summary>收势，短过冲回稳</summary>
        private void RecoverMotion(float strikeEnd) {
            float t = MathHelper.Clamp((elapsed - strikeEnd) / RecoverTime, 0f, 1f);
            float overshoot = MathF.Sin(MathHelper.Clamp(t / 0.42f, 0f, 1f) * MathF.PI) * 0.13f;
            currentRotation = endAngle + swingSign * overshoot;
            strikeEased = 1f;
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;
            Owner.SetCompositeArmFront(true
                , elapsed < WindupTime ? Player.CompositeArmStretchAmount.ThreeQuarters : Player.CompositeArmStretchAmount.Full
                , currentRotation - MathHelper.PiOver2);
            Projectile.Center = Owner.GetPlayerStabilityCenter()
                + currentRotation.ToRotationVector2() * BladeReach * 0.55f;
            Projectile.timeLeft = 120;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => OniMachete.ApplyGoldRend(target, ref modifiers);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中喂压制（owner 端）
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.GetModPlayer<OniMachetePlayer>().AddSuppression(IsFinisher ? 12f : 8f);
            }

            //熔金裂纹挂目标
            target.GetGlobalNPC<OniMacheteGlobalNPC>().AddCrack(IsFinisher ? 0.85f : 0.55f);

            //施力者 1 帧停驻 + 回坐脉冲
            impactHoldFrames = 1;
            recoilPulse = 1f;

            if (IsFinisher && CWRServerConfig.Instance.ScreenVibration) {
                var modifier = new PunchCameraModifier(target.Center
                    , currentRotation.ToRotationVector2(), 5f, 6f, 10, 900f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.8f, Pitch = -0.35f }, target.Center);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.55f, Pitch = 0.35f }, target.Center);

            //粒子量∝拍位
            Vector2 aimDir = currentRotation.ToRotationVector2();
            int golds = IsFinisher ? 14 : 7;
            for (int i = 0; i < golds; i++) {
                PRTLoader.NewParticle<PRT_OniMacheteGold>(target.Center
                    , aimDir.RotatedByRandom(0.7) * Main.rand.NextFloat(4f, IsFinisher ? 15f : 10f)
                    , default, Main.rand.NextFloat(0.45f, 0.85f))
                    ?.Configure(Main.rand.Next(18, 30), gravity: true);
            }
            for (int i = 0; i < (IsFinisher ? 5 : 2); i++) {
                var flame = PRTLoader.NewParticle<PRT_HellFlame>(target.Center + Main.rand.NextVector2Circular(16f, 16f)
                    , aimDir.RotatedByRandom(0.9) * Main.rand.NextFloat(2f, 6f)
                    , Color.White, Main.rand.NextFloat(0.5f, 0.9f));
                if (flame != null) {
                    flame.ai[0] = 1;
                }
            }
            if (IsFinisher) {
                PRTLoader.NewParticle<PRT_DWave>(target.Center, Vector2.Zero, new Color(255, 170, 60), 0.5f)
                    ?.Configure(new Vector2(1f, 0.65f), currentRotation, 1.5f, 20);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //余光期不画刀身
            if (!BladeActive) {
                return false;
            }
            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float dist = BladeReach * 0.52f;
            float drawScale = Projectile.scale * 1.52f * (1f + recoilPulse * 0.04f);

            //朝左翻一次、反向挥再翻一次(XOR);刃口始终朝挥动前缘,终结拍 swingSign==facing 不额外翻
            bool edgeFlip = swingSign * lockedDirection < 0;
            bool flipY = lockedDirection < 0 != edgeFlip;
            SpriteEffects effect = flipY ? SpriteEffects.FlipVertically : SpriteEffects.None;
            //贴图刀尖右上(-PiOver4),翻转后右下(+PiOver4)
            float rotOffset = flipY ? -MathHelper.PiOver4 : MathHelper.PiOver4;

            //打击段残影
            float strikeStart = WindupTime + HoldTime;
            if (elapsed >= strikeStart && elapsed <= strikeStart + StrikeTime + 1f) {
                for (int i = 1; i <= 2; i++) {
                    float rot = MathHelper.Lerp(currentRotation, lastRotation, i / 3f);
                    Vector2 pos = hand + rot.ToRotationVector2() * dist - Main.screenPosition;
                    Color trailColor = new Color(255, 150, 50, 0) * (0.35f * (1f - i / 3f));
                    Main.EntitySpriteDraw(tex, pos, null, trailColor, rot + rotOffset, origin
                        , drawScale, effect, 0);
                }
            }

            //蓄势末段熔金压边辉光（滞帧最亮）
            float windT = MathHelper.Clamp(elapsed / WindupTime, 0f, 1f);
            if (elapsed < strikeStart && windT > 0.55f) {
                float heat = (windT - 0.55f) / 0.45f;
                Vector2 pos0 = hand + currentRotation.ToRotationVector2() * dist - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos0, null, new Color(255, 170, 60, 0) * (heat * 0.5f)
                    , currentRotation + rotOffset, origin, drawScale * 1.04f, effect, 0);
            }

            //刀身本体
            Vector2 drawPos = hand + currentRotation.ToRotationVector2() * dist - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, currentRotation + rotOffset, origin
                , drawScale, effect, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !slashBorn) {
                return;
            }
            int lt = (int)elapsed - slashBirth;
            if (lt < 0 || lt >= slashDef.Life) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OniSlashStrip.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }
            OniSlashStrip.DrawTwoLayers(device, fx, in slashDef, SlashCenter, lt
                , (int)StrikeTime, strikeEased);
            OniSlashStrip.EndDraw(device, pb, pr, pd);
        }
    }
}
