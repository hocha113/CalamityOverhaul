using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Stones;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Helen;
using CalamityOverhaul.Content.Scenarios.SupCal.SupCalDisplayTexts;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 左键射钉、右键掷棺、三烙印落棺
    /// </summary>
    internal class Pallbearer : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "Pallbearer";
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 80;
            Item.height = 32;
            Item.damage = 666;
            Item.DamageType = DamageClass.Generic;
            Item.useTime = 45;
            Item.useAnimation = 45;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 6.5f;
            Item.rare = ItemRarityID.Yellow;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<PallbearerHeld>();
            Item.shootSpeed = 15f;
            Item.useAmmo = AmmoID.Arrow;
            Item.channel = true;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            if (InWorldBossPhase.Level9) {
                damage *= 1.25f;
            }
            if (InWorldBossPhase.Level10) {
                damage *= 1.25f;
            }
            if (InWorldBossPhase.Level11) {
                damage *= 1.25f;
            }
            if (InWorldBossPhase.Level12) {
                damage *= 1.25f;
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            if (HalibutStorySync.ReadSupCal(d => d.SupCalQuestReward, d => d.SupCalQuestReward)) {
                TooltipLine line = new(Mod, "Story", SupCalDisplayText.Story1.Value);
                line.OverrideColor = Color.OrangeRed;
                tooltips.Add(line);
            }
        }

        public override bool CanUseItem(Player player) {
            Item.useStyle = ItemUseStyleID.Shoot;
            if (player.altFunctionUse == 2) {
                Item.useStyle = ItemUseStyleID.Swing;
            }
            //手上有Held或棺未回则不可用
            return player.ownedProjectileCounts[Item.shoot] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<PallbearerBoomerang>()] == 0;
        }

        public override bool CanConsumeAmmo(Item ammo, Player player) {
            if (player.ownedProjectileCounts[Item.shoot] == 0) {
                return false; //生成Held本身不耗弹，钉在PickAmmo耗
            }
            return player.altFunctionUse != 2; //右键掷棺不耗弹
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //只生成Held
            Projectile.NewProjectile(source, position, velocity, Item.shoot, damage, knockback, player.whoAmI);
            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity
            , ref int type, ref int damage, ref float knockback) {
            //枪口改玩家中心
            position = player.GetPlayerStabilityCenter();
        }
    }

    /// <summary>
    /// 装填→蓄力→射钉。ai0状态 ai1计时 localAI0蓄力0-1
    /// </summary>
    internal class PallbearerHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Ranged + "PallbearerHeld";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<Pallbearer>()).DisplayName;

        private enum CrossbowState
        {
            Idle,       //待机
            Loading,    //装填
            Charged,    //满弦
            Firing,     //收势
            Winding     //掷棺蓄势后拉
        }

        private CrossbowState State {
            get => (CrossbowState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float StateTimer => ref Projectile.ai[1];
        private ref float ChargeLevel => ref Projectile.localAI[0]; //蓄力0-1

        private float armRotation;

        //发射反馈（客户端，不同步）
        private float recoilPunch;      //后座px，指数收回
        private float stringSnap;       //弦回弹 1→-0.22过冲→0
        private bool chargeCue;         //72%静默提示音一次
        private float windupPull;       //蓄势后拉px

        //常量
        private const int LoadDuration = 28;        //装填帧
        private const int MaxChargeDuration = 60;   //蓄力上限帧
        private const int FireDuration = 12;        //收势帧
        private const float ChargeSilence = 0.72f;  //静默切入点
        private const int WindupFrames = 7;         //掷棺蓄势帧
        private const float WindupPullDist = 26f;   //后拉幅度px

        private float bowstringPullback; //拉弦0-1

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 4; //0待机 1装填 2满弦 3回弹
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 32;
            Projectile.friendly = false; //Held不碰伤
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
            Projectile.hide = false;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (DownLeft || DownRight || State == CrossbowState.Firing) {
                Projectile.timeLeft = 60;
            }
            SetHeld();

            switch (State) {
                case CrossbowState.Idle:
                    HandleIdle();
                    break;
                case CrossbowState.Loading:
                    HandleLoading();
                    break;
                case CrossbowState.Charged:
                    HandleCharged();
                    break;
                case CrossbowState.Firing:
                    HandleFiring();
                    break;
                case CrossbowState.Winding:
                    HandleWinding();
                    break;
            }

            recoilPunch *= 0.74f; //后座收回
            UpdateOwnerArms();
            UpdatePositionAndRotation();
            StateTimer++;
        }

        private void HandleIdle() {
            Projectile.frame = 0;
            bowstringPullback = 0f;
            ChargeLevel = 0f;
            chargeCue = false;

            if (DownLeft && Owner.HasAmmo(Owner.GetItem())) {
                State = CrossbowState.Loading;
                StateTimer = 0;
                //装填木鸣
                SoundEngine.PlaySound(SoundID.DoorOpen with { Pitch = -0.35f, Volume = 0.5f, MaxInstances = 3 }, Owner.Center);
            }

            if (DownRight) {
                //先蓄势再出手
                State = CrossbowState.Winding;
                StateTimer = 0;
                windupPull = 0f;
                SoundEngine.PlaySound(SoundID.DoorOpen with { Pitch = -0.55f, Volume = 0.45f, MaxInstances = 2 }, Owner.Center);
            }
        }

        /// <summary>掷棺蓄势，pow(t,3)迟滞后吸，不可取消</summary>
        private void HandleWinding() {
            float t = MathHelper.Clamp(StateTimer / (float)WindupFrames, 0f, 1f);
            windupPull = MathF.Pow(t, 3f) * WindupPullDist;

            //蓄满出手
            if (StateTimer >= WindupFrames) {
                windupPull = 0f;
                ThrowCrossbow();
            }
        }

        private void HandleLoading() {
            float loadProgress = StateTimer / LoadDuration;
            Projectile.frame = loadProgress < 0.5f ? 0 : 1;
            bowstringPullback = MathHelper.SmoothStep(0f, 1f, loadProgress);

            //装填尘土
            if (StateTimer % 9 == 0 && !Main.dedServ) {
                Vector2 dustPos = Projectile.Center + Projectile.velocity * Main.rand.NextFloat(-6f, 18f);
                PRTLoader.NewParticle<PRT_Smoke>(dustPos, Main.rand.NextVector2Circular(0.7f, 0.5f)
                    , PallbearerVFX.Charcoal, 0.14f)?.Configure(24, 0.3f, 0.01f);
            }

            if (StateTimer >= LoadDuration) {
                State = CrossbowState.Charged;
                StateTimer = 0;
                ChargeLevel = 0f;
                Projectile.frame = 2;
                //满弦音
                SoundEngine.PlaySound(CWRSound.Bow_String with { Pitch = -0.1f, Volume = 0.7f, MaxInstances = 3 }, Owner.Center);
            }

            if (!DownLeft) { //取消
                State = CrossbowState.Idle;
                StateTimer = 0;
            }
        }

        private void HandleCharged() {
            Projectile.frame = 2;
            bowstringPullback = 1f;
            ChargeLevel = MathHelper.Clamp(StateTimer / MaxChargeDuration, 0f, 1f);

            bool silent = ChargeLevel >= ChargeSilence;

            if (DownLeft && !silent) {
                //蓄力火星坍缩，密度∝sqrt(charge)
                if (!Main.dedServ && Main.rand.NextFloat() < 0.18f + 0.6f * MathF.Sqrt(ChargeLevel)) {
                    SpawnConvergeSpark();
                }
                //受力音，音高随蓄力
                if (StateTimer % 15 == 0) {
                    SoundEngine.PlaySound(SoundID.DoorOpen with {
                        Volume = 0.22f,
                        Pitch = -0.1f + ChargeLevel * 0.5f,
                        MaxInstances = 2
                    }, Owner.Center);
                }
            }

            //72%硬切静默
            if (silent && !chargeCue) {
                chargeCue = true;
                SoundEngine.PlaySound(CWRSound.Bow_String with { Pitch = 0.3f, Volume = 0.35f, MaxInstances = 2 }, Owner.Center);
            }

            if (!DownLeft || StateTimer >= MaxChargeDuration) {
                State = CrossbowState.Firing;
                StateTimer = 0;
                FireNail();
            }
        }

        private void HandleFiring() {
            Projectile.frame = 3;
            float fireProgress = StateTimer / FireDuration;
            //弦过冲回弹
            stringSnap = MathHelper.Lerp(stringSnap, 0f, 0.3f);
            bowstringPullback = stringSnap;

            if (fireProgress >= 1f) {
                State = CrossbowState.Idle;
                StateTimer = 0;
                ChargeLevel = 0f;
                chargeCue = false;
            }
        }

        private Vector2 MuzzlePos() => Projectile.Center + Projectile.velocity * 26f;

        /// <summary>蓄力坍缩火星，60~130px外拽向弩口</summary>
        private void SpawnConvergeSpark() {
            Vector2 muzzle = MuzzlePos();
            Vector2 pos = muzzle + Main.rand.NextVector2Unit() * Main.rand.NextFloat(60f, 130f);
            Vector2 vel = (muzzle - pos) * 0.115f; //比例拽力
            Color col = Color.Lerp(PallbearerVFX.BloodDeep, PallbearerVFX.Blood, ChargeLevel);
            PRTLoader.NewParticle<PRT_Spark>(pos, vel, col, 0.45f + 0.4f * ChargeLevel)
                ?.Configure(false, 15);
        }

        /// <summary>发射棺钉，owner生成+各端表现</summary>
        private void FireNail() {
            float charge = ChargeLevel;
            Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            Vector2 muzzle = MuzzlePos();

            //owner端生成
            bool fired = true;
            if (Projectile.IsOwnedByLocalPlayer()) {
                fired = Owner.PickAmmo(Owner.GetItem(), out int _, out float speed, out int damage
                    , out float knockback, out int _);
                if (fired) {
                    float mult = 1f + 0.5f * charge;
                    //extraUpdates=4，实际弹速×5
                    Vector2 shootVelocity = aim * MathHelper.Clamp(speed * (1f + 0.3f * charge), 12f, 26f);
                    Projectile.NewProjectile(
                        Owner.GetSource_ItemUse(Owner.GetItem()),
                        muzzle, shootVelocity,
                        ModContent.ProjectileType<PallbearerArrow>(),
                        (int)(damage * mult),
                        knockback * (1f + 0.5f * charge),
                        Owner.whoAmI,
                        charge);
                    //玩家后坐
                    if (!Owner.mount.Active) {
                        Owner.velocity -= aim * (1.1f + 1.7f * charge);
                    }
                }
            }

            if (!fired) {
                //空匣锁响
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.4f, Volume = 0.4f }, Owner.Center);
                return;
            }

            //各端打击链
            recoilPunch = 12f;      //弩身后座
            stringSnap = -0.22f;    //弦过冲

            SoundEngine.PlaySound(CWRSound.Gun_Crossbow_Shoot with {
                Volume = 1f,
                Pitch = -0.15f + charge * 0.12f,
                MaxInstances = 3
            }, muzzle);
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.7f, Volume = 0.5f, MaxInstances = 3 }, muzzle);
            if (charge > 0.7f) {
                //满弦第三层低音
                SoundEngine.PlaySound(SoundID.DD2_BallistaTowerShot with { Pitch = -0.3f, Volume = 0.55f, MaxInstances = 2 }, muzzle);
            }

            PallbearerVFX.Punch(muzzle, aim, 5f + 3f * charge, 9f, 12, 750f);

            if (!Main.dedServ) {
                //枪口暖色爆闪
                PRTLoader.NewParticle<PRT_Light>(muzzle, aim * 2f, PallbearerVFX.Ember, 0.36f + 0.2f * charge)
                    ?.Configure(9, 1f, 1.5f, 2.4f);
                //枪口破空痕
                PRTLoader.NewParticle<PRT_PallbearerTracer>(muzzle, Vector2.Zero, default, 1f)
                    ?.Configure(muzzle - aim * 14f, muzzle + aim * (95f + 55f * charge), 20f + 10f * charge, 6);
                //血色锐线锥∝动能
                int lineCount = (int)(8 + 9 * charge);
                for (int i = 0; i < lineCount; i++) {
                    Vector2 vel = aim.RotatedByRandom(0.28f) * Main.rand.NextFloat(4f, 11f + charge * 6f);
                    Color col = Color.Lerp(PallbearerVFX.Blood, PallbearerVFX.BloodDeep, Main.rand.NextFloat(0.55f));
                    PRTLoader.NewParticle<PRT_Line>(muzzle, vel, col, Main.rand.NextFloat(0.5f, 0.9f))
                        ?.Configure(false, 13);
                }
                PallbearerVFX.Splinters(muzzle, aim, 4, 6f);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(muzzle, aim.RotatedByRandom(0.5f) * Main.rand.NextFloat(1.2f, 3f)
                        , PallbearerVFX.CharDark, 0.2f)?.Configure(24, 0.4f, 0.02f);
                }
            }
        }

        /// <summary>掷棺出手</summary>
        private void ThrowCrossbow() {
            Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(
                    Owner.GetSource_ItemUse(Owner.GetItem()),
                    Projectile.Center,
                    aim * PallbearerBoomerang.LaunchSpeed, //初速，此后复利加速
                    ModContent.ProjectileType<PallbearerBoomerang>(),
                    (int)(Projectile.damage * 0.85f),
                    Projectile.knockBack * 1.5f,
                    Owner.whoAmI
                );
                if (!Owner.mount.Active) {
                    Owner.velocity -= aim * 1.8f; //反作用
                }
            }

            //出手音+震屏+破空痕
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.5f, Volume = 1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.8f, Volume = 0.45f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DoorClosed with { Pitch = -0.3f, Volume = 0.4f }, Projectile.Center);
            PallbearerVFX.Punch(Projectile.Center, aim, 5.5f, 9f, 10, 700f);
            if (!Main.dedServ) {
                PRTLoader.NewParticle<PRT_PallbearerTracer>(Projectile.Center, Vector2.Zero, default, 1f)
                    ?.Configure(Projectile.Center - aim * 10f, Projectile.Center + aim * 120f, 22f, 6);
            }
            Projectile.Kill();
        }

        private void UpdateOwnerArms() {
            int dir = Owner.direction;
            float targetArmRot = Projectile.rotation;
            if (dir < 0) {
                targetArmRot -= MathHelper.PiOver2;
            }
            else {
                targetArmRot -= MathHelper.ToRadians(60);
            }

            switch (State) {
                case CrossbowState.Loading:
                    armRotation = MathHelper.Lerp(armRotation, targetArmRot - 0.5f * dir, 0.15f);
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotation);
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter, targetArmRot);
                    break;
                case CrossbowState.Charged:
                    //满弦震颤，静默段近凝滞
                    float tremble = ChargeLevel >= ChargeSilence
                        ? 0.008f * (1f - (ChargeLevel - ChargeSilence) / (1f - ChargeSilence))
                        : 0.03f;
                    float vibration = MathF.Sin(StateTimer * 0.3f) * tremble;
                    armRotation = targetArmRot - 0.6f * dir + vibration;
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotation);
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, targetArmRot);
                    break;
                case CrossbowState.Firing:
                    armRotation = MathHelper.Lerp(armRotation, targetArmRot, 0.4f);
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotation);
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, targetArmRot);
                    break;
                case CrossbowState.Winding:
                    //蓄势后拉手臂
                    float windT = windupPull / WindupPullDist;
                    armRotation = MathHelper.Lerp(armRotation, targetArmRot - 1.1f * dir * windT, 0.5f);
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotation);
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);
                    break;
                default:
                    armRotation = MathHelper.Lerp(armRotation, targetArmRot, 0.2f);
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Quarter, armRotation);
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters, targetArmRot);
                    break;
            }
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
        }

        private void UpdatePositionAndRotation() {
            Vector2 ownerCenter = Owner.GetPlayerStabilityCenter();
            Vector2 aimDir = (InMousePos - ownerCenter).SafeNormalize(Vector2.UnitX * Owner.direction);
            Projectile.velocity = aimDir; //瞄准方向

            float holdDistance = 20f + ((State == CrossbowState.Loading || State == CrossbowState.Charged) ? bowstringPullback * 8f : 0f);
            holdDistance -= recoilPunch;  //后座
            holdDistance -= windupPull;   //蓄势后拉
            if (State == CrossbowState.Firing) {
                holdDistance -= stringSnap * 8f; //弦过冲带弩
            }
            Projectile.Center = ownerCenter + aimDir * holdDistance;
            Projectile.rotation = aimDir.ToRotation();

            Owner.ChangeDir(aimDir.X > 0 ? 1 : -1);
            Owner.itemRotation = Projectile.rotation * Owner.direction;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle frame = texture.Frame(1, Main.projFrames[Type], 0, Projectile.frame);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = frame.Size() / 2f;
            SpriteEffects fx = Owner.direction == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            //满弦钉槽预览
            if (State == CrossbowState.Charged || (State == CrossbowState.Loading && bowstringPullback > 0.55f)) {
                Texture2D nailTex = TextureAssets.Projectile[ModContent.ProjectileType<PallbearerArrow>()].Value;
                Vector2 nailPos = drawPos + Projectile.velocity * (14f - bowstringPullback * 7f);
                Main.EntitySpriteDraw(nailTex, nailPos, null, lightColor, Projectile.rotation + MathHelper.PiOver2
                    , nailTex.Size() / 2f, 0.85f, fx, 0);
            }

            Main.EntitySpriteDraw(texture, drawPos, frame, lightColor, Projectile.rotation, origin, Projectile.scale, fx, 0);

            //蓄力驻留辉光
            if (State == CrossbowState.Charged && ChargeLevel > 0.15f) {
                float glow = MathHelper.Clamp(ChargeLevel / ChargeSilence, 0f, 1f);
                Color glowColor = Color.Lerp(PallbearerVFX.BloodDeep, PallbearerVFX.Blood, glow) with { A = 0 };
                Main.EntitySpriteDraw(texture, drawPos, frame, glowColor * (0.34f * glow),
                        Projectile.rotation, origin, Projectile.scale * 1.02f, fx, 0);
                Texture2D soft = CWRAsset.SoftGlow.Value;
                Vector2 muzzle = MuzzlePos() - Main.screenPosition;
                Main.EntitySpriteDraw(soft, muzzle, null, glowColor * (0.42f * glow), 0f
                    , soft.Size() / 2f, 0.2f + 0.08f * glow, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 掷棺回旋，命中挂收殓标记
    /// </summary>
    internal class PallbearerBoomerang : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Ranged + "Pallbearer";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<Pallbearer>()).DisplayName;

        private enum BoomerangState { Throwing, Braking, Hover, Returning }
        private BoomerangState State {
            get => (BoomerangState)Projectile.ai[0];
            set {
                if (Projectile.ai[0] != (float)value) {
                    Projectile.ai[0] = (float)value;
                    Projectile.netUpdate = true;
                }
            }
        }
        private ref float Time => ref Projectile.ai[1];
        private ref float ReturnSpeed => ref Projectile.ai[2];
        private ref float SpinSpeed => ref Projectile.localAI[0];

        private Trail trailOuter;
        private Trail trailCore;

        //运动学
        /// <summary>出手初速</summary>
        public const float LaunchSpeed = 26f;
        private const float FlightAccelMul = 1.055f;   //飞行复利/帧
        private const float FlightMaxSpeed = 74f;      //飞行速度上限
        private const int MaxFlightFrames = 24;        //直线段最长帧
        private const float MaxDistance = 950f;        //最大射程
        private const float BrakeMul = 0.62f;          //硬刹衰减/帧
        private const int HoverFrames = 8;             //顶点悬滞帧
        private const int ReturnCreepFrames = 4;       //回程蠕动帧
        private const float ReturnAccelMul = 1.13f;    //回程复利/帧
        private const float ReturnMaxSpeed = 78f;      //回程峰值

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 22;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }

        public override bool? CanDamage() => State == BoomerangState.Hover ? false : Time > 0;

        public override void AI() {
            if (!Owner.active || Owner.dead) { Projectile.Kill(); return; }

            Time++;
            Vector2 playerCenter = Owner.GetPlayerStabilityCenter();
            Vector2 toPlayer = playerCenter - Projectile.Center;

            switch (State) {
                case BoomerangState.Throwing: {
                    //飞行复利加速
                    float speed = Projectile.velocity.Length();
                    if (speed < FlightMaxSpeed) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX)
                            * MathF.Min(speed * FlightAccelMul, FlightMaxSpeed);
                    }
                    if (Time >= MaxFlightFrames || toPlayer.Length() > MaxDistance) {
                        State = BoomerangState.Braking;
                        Time = 0f;
                        //硬刹破空音
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 0.6f, MaxInstances = 2 }, Projectile.Center);
                    }
                    break;
                }
                case BoomerangState.Braking: {
                    //硬刹，残余反向过冲
                    Projectile.velocity *= BrakeMul;
                    if (Projectile.velocity.Length() < 2.5f) {
                        Projectile.velocity = -Projectile.velocity.SafeNormalize(Vector2.Zero) * 1.6f;
                        State = BoomerangState.Hover;
                        Time = 0f;
                        if (!Main.dedServ) {
                            PallbearerVFX.EmberBurst(Projectile.Center, 4, 2.4f, 0.7f);
                        }
                    }
                    break;
                }
                case BoomerangState.Hover: {
                    //顶点悬滞
                    Projectile.velocity *= 0.82f;
                    Projectile.velocity.Y += MathF.Sin(Time * 0.55f) * 0.14f;
                    if (Time >= HoverFrames) {
                        State = BoomerangState.Returning;
                        Time = 0f;
                        ReturnSpeed = 10f;
                        //回程绷链音
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.3f, Volume = 0.7f }, Projectile.Center);
                        SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.6f, Volume = 0.4f }, Projectile.Center);
                    }
                    break;
                }
                default: {
                    //回程蠕动→复利猛拽
                    if (Time <= ReturnCreepFrames) {
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity
                            , toPlayer.SafeNormalize(Vector2.Zero) * ReturnSpeed, 0.3f);
                    }
                    else {
                        ReturnSpeed = MathF.Min(ReturnSpeed * ReturnAccelMul, ReturnMaxSpeed);
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity
                            , toPlayer.SafeNormalize(Vector2.Zero) * ReturnSpeed, 0.5f);
                    }

                    if (toPlayer.Length() < 70f) {
                        //接棺
                        Owner.GetModPlayer<CWRPlayer>().GetScreenShake(5.5f);
                        SoundEngine.PlaySound(SoundID.Grab with { Volume = 1f, Pitch = 0f }, Owner.Center);
                        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.55f, Pitch = -0.5f }, Owner.Center);
                        if (!Owner.mount.Active) {
                            Owner.velocity += Projectile.velocity.SafeNormalize(Vector2.Zero) * 1.5f;
                        }
                        if (!Main.dedServ) {
                            PallbearerVFX.EmberBurst(playerCenter, 5, 3f, 0.8f);
                            PRTLoader.NewParticle<PRT_PallbearerTracer>(playerCenter, Vector2.Zero, default, 1f)
                                ?.Configure(Projectile.Center - Projectile.velocity * 1.5f, playerCenter, 16f, 7);
                        }
                        Projectile.Kill();
                        return;
                    }
                    break;
                }
            }

            //转速随速度，回程更快
            float velLen = Projectile.velocity.Length();
            float targetSpin = 0.1f + velLen / 80f * (State == BoomerangState.Returning ? 1.35f : 1f);
            SpinSpeed = MathHelper.Lerp(SpinSpeed, targetSpin, 0.3f);
            Projectile.rotation += SpinSpeed * Math.Sign(Projectile.velocity.X == 0 ? Owner.direction : Projectile.velocity.X);

            //呼啸∝速度
            if (Time % 7 == 0 && velLen > 14f) {
                float speedT = MathHelper.Clamp((velLen - 14f) / 60f, 0f, 1f);
                SoundEngine.PlaySound(SoundID.Item32 with {
                    Volume = 0.28f + 0.24f * speedT,
                    Pitch = -0.3f + speedT * 0.9f,
                    MaxInstances = 2
                }, Projectile.Center);
            }

            //余烬甩尾∝速度
            if (!Main.dedServ && velLen > 16f) {
                int cadence = velLen > 55f ? 1 : velLen > 34f ? 2 : 4;
                if (Time % cadence == 0) {
                    float tipAngle = Projectile.rotation + (Main.rand.NextBool() ? 0f : MathHelper.Pi);
                    Vector2 tip = Projectile.Center + tipAngle.ToRotationVector2() * 34f;
                    PRTLoader.NewParticle<PRT_PallbearerEmber>(tip
                        , tipAngle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(1.5f, 3.5f)
                        , PallbearerVFX.BloodDeep, Main.rand.NextFloat(0.45f, 0.7f))?.Configure(14);
                }
            }

            Lighting.AddLight(Projectile.Center, PallbearerVFX.Blood.ToVector3() * 0.26f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //收殓标记（原版buff包同步）
            target.AddBuff(ModContent.BuffType<PallbearerShrouded>(), PallbearerShrouded.MarkDuration);
            target.CWR().TimeFrozenTick = 3; //轻顿帧

            //命中咬肉减速
            if (State == BoomerangState.Throwing) {
                Projectile.velocity *= 0.55f;
            }

            //撞击+标记轻钟
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.55f, Volume = 0.85f, MaxInstances = 3 }, target.Center);
            SoundEngine.PlaySound(SoundID.Item35 with { Pitch = -0.3f, Volume = 0.35f, MaxInstances = 2 }, target.Center);
            PallbearerVFX.Punch(target.Center, Projectile.velocity, 4.5f, 8f, 10, 650f);

            //血爆+木屑
            PallbearerVFX.BloodBurst(target.Center, Projectile.velocity, 0.8f);
            PallbearerVFX.Splinters(target.Center, -Projectile.velocity.SafeNormalize(Vector2.UnitX), 3, 4.5f);
        }

        //绘制

        private float SpeedT => MathHelper.Clamp(Projectile.velocity.Length() / ReturnMaxSpeed, 0f, 1f);

        public float GetOuterWidth(float completionRatio) {
            float speedFade = MathHelper.Clamp(Projectile.velocity.Length() / 46f, 0.15f, 1f);
            return (1f - completionRatio) * 38f * speedFade; //0=头端最宽
        }

        public Color GetOuterColor(Vector2 coord) =>
            Color.White * ((0.18f + 0.5f * SpeedT) * (1f - coord.X));

        public float GetCoreWidth(float completionRatio) {
            float speedFade = MathHelper.Clamp(Projectile.velocity.Length() / 46f, 0.15f, 1f);
            return (1f - completionRatio) * 13f * speedFade;
        }

        public Color GetCoreColor(Vector2 coord) =>
            Color.White * ((0.35f + 0.65f * SpeedT) * MathF.Pow(1f - coord.X, 1.5f));

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect fx = PallbearerAssets.PallbearerTrail;
            if (fx == null || !Projectile.active) {
                return;
            }
            //外宽内窄双层trail
            PallbearerVFX.ApplyTrail(fx, Projectile.whoAmI * 0.37f);
            GraniteMarbleVFX.DrawTrailFromOldPos(Projectile, ref trailOuter, GetOuterWidth, GetOuterColor, fx);
            PallbearerVFX.ApplyTrail(fx, Projectile.whoAmI * 0.37f + 0.5f);
            GraniteMarbleVFX.DrawTrailFromOldPos(Projectile, ref trailCore, GetCoreWidth, GetCoreColor, fx);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Item[ModContent.ItemType<Pallbearer>()].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;
            float speedFactor = SpeedT;
            SpriteBatch sb = Main.spriteBatch;

            //悬滞垂链，回程绷直
            if (State == BoomerangState.Hover || State == BoomerangState.Returning) {
                float taut = State == BoomerangState.Hover ? 0.3f : 0.45f + 0.55f * speedFactor;
                PallbearerVFX.DrawChain(sb, Projectile.Center, Owner.GetPlayerStabilityCenter()
                    , taut, 0.55f, Main.GlobalTimeWrappedHourly * 1.7f);
            }

            //位移拖影
            Color ghostBlood = PallbearerVFX.BloodDeep with { A = 0 };
            for (int i = 2; i <= 8; i += 3) {
                if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = (1f - i / 10f) * 0.32f * speedFactor;
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float ghostRot = i < Projectile.oldRot.Length ? Projectile.oldRot[i] : Projectile.rotation;
                Main.EntitySpriteDraw(texture, ghostPos, null, ghostBlood * fade
                    , ghostRot, origin, Projectile.scale * 0.96f, SpriteEffects.None, 0);
            }

            //旋转拖影
            float spinT = MathHelper.Clamp(SpinSpeed / 0.85f, 0f, 1f);
            for (int i = 1; i <= 4; i++) {
                float fade = (0.3f - i * 0.06f) * spinT;
                if (fade <= 0.01f) {
                    continue;
                }
                float ghostRot = Projectile.rotation - SpinSpeed * i * 2.2f * Math.Sign(Projectile.velocity.X == 0 ? 1 : Projectile.velocity.X);
                Main.EntitySpriteDraw(texture, drawPos, null, ghostBlood * fade
                    , ghostRot, origin, Projectile.scale, SpriteEffects.None, 0);
            }

            //速度门控底晕
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(soft, drawPos, null, PallbearerVFX.BloodDeep with { A = 0 } * (0.5f * speedFactor)
                , 0f, soft.Size() / 2f, 1.3f + speedFactor * 0.5f, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(texture, drawPos, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 棺钉。ai0蓄力0-1，ai2模式0飞行/1钉地
    /// </summary>
    internal class PallbearerArrow : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Ranged + "PallbearerArrow";

        private ref float ChargeLevel => ref Projectile.ai[0];
        private ref float Mode => ref Projectile.ai[2];

        private const float ModeFlight = 0f;
        private const float ModeStuckTile = 1f;

        /// <summary>标记下吃钉倍率</summary>
        public const float MarkedDamageMult = 1.75f;

        private Trail trail;
        //弹道光痕锚点（各端本地）
        private Vector2 birthPos;
        private bool birthSet;
        private bool tracerSpawned;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 24;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 4;                   //贯穿4
            Projectile.timeLeft = 200;
            Projectile.arrow = true;                    //吃箭袋加成
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 4;                //5倍tick，时长按5拍折算
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;        //每目标一次
            Projectile.ArmorPenetration = 32767;        //穿甲满
        }

        public override bool? CanDamage() => Mode == ModeFlight ? null : false;

        public override void AI() {
            if (!birthSet) {
                birthSet = true;
                birthPos = Projectile.Center;
            }
            if (Mode == ModeFlight) {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                //飞行余烬
                if (!Main.dedServ && Main.rand.NextBool(22)) {
                    PRTLoader.NewParticle<PRT_PallbearerEmber>(Projectile.Center - Projectile.velocity * 0.6f
                        , -Projectile.velocity * 0.04f, PallbearerVFX.BloodDeep
                        , Main.rand.NextFloat(0.3f, 0.5f))?.Configure(12);
                }
            }
            else {
                //钉地静止
                Projectile.velocity = Vector2.Zero;
            }

            Lighting.AddLight(Projectile.Center, PallbearerVFX.Blood.ToVector3() * (0.2f + 0.25f * ChargeLevel));
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //高DR目标削减伤
            float dr = CWRRef.GetNPCDR(target);
            if (dr > 0f && dr <= 0.9f) {
                modifiers.FinalDamage *= (1f - dr * 0.5f) / (1f - dr);
            }
            //收殓标记加伤
            if (target.HasBuff(ModContent.BuffType<PallbearerShrouded>())) {
                modifiers.FinalDamage *= MarkedDamageMult;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool marked = target.HasBuff(ModContent.BuffType<PallbearerShrouded>());

            //打击链
            target.CWR().TimeFrozenTick = 4; //hit-stop
            PallbearerVFX.NailThunk(Projectile.Center, marked);
            PallbearerVFX.Punch(Projectile.Center, Projectile.velocity, marked ? 4.5f : 3.4f, 8f, 8, 620f);
            PallbearerVFX.BloodBurst(Projectile.Center, Projectile.velocity
                , MathHelper.Clamp(Projectile.velocity.Length() / 26f, 0.4f, 1f), marked);

            //贯穿掉速
            Projectile.velocity *= 0.9f;

            //烙印（owner端计数）
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            PallbearerNPC brands = target.GetGlobalNPC<PallbearerNPC>();
            int count = brands.AddBrand(target, Projectile.Center, Projectile.velocity.ToRotation());
            if (count < PallbearerNPC.MaxBrands) {
                return;
            }
            //满烙印→落棺
            brands.ClearBrands();
            IEntitySource source = Main.player[Projectile.owner].HeldItem?.type == ModContent.ItemType<Pallbearer>()
                ? Main.player[Projectile.owner].GetSource_ItemUse(Main.player[Projectile.owner].HeldItem)
                : Projectile.GetSource_FromThis();
            Projectile.NewProjectile(source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<PallbearerCoffinSeal>()
                , (int)(Projectile.damage * 3f), 12f, Projectile.owner, target.whoAmI);
        }

        /// <summary>终点铺弹道光痕</summary>
        private void SpawnTracer() {
            if (Main.dedServ || tracerSpawned || !birthSet) {
                return;
            }
            tracerSpawned = true;
            if (Vector2.DistanceSquared(birthPos, Projectile.Center) < 40f * 40f) {
                return; //贴脸不铺痕
            }
            PRTLoader.NewParticle<PRT_PallbearerTracer>(Projectile.Center, Vector2.Zero, default, 1f)
                ?.Configure(birthPos, Projectile.Center, 9f + 8f * ChargeLevel, 13);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //钉地存留
            SpawnTracer();
            Mode = ModeStuckTile;
            Projectile.velocity = Vector2.Zero;
            Projectile.timeLeft = 300; //≈1s（5拍）
            Projectile.netUpdate = true;
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.5f, Volume = 0.6f, MaxInstances = 4 }, Projectile.Center);
            if (!Main.dedServ) {
                PallbearerVFX.Splinters(Projectile.Center, -oldVelocity.SafeNormalize(Vector2.UnitY), 4, 4f);
                PallbearerVFX.EmberBurst(Projectile.Center, 3, 2.2f, 0.7f);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //飞行死亡才铺光痕
            if (Mode == ModeFlight) {
                SpawnTracer();
            }
            //消隐余烬
            PallbearerVFX.EmberBurst(Projectile.Center, 2, 1.6f, 0.6f);
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke
                    , Main.rand.NextVector2Circular(1.4f, 1.4f), 180, PallbearerVFX.CharDark, 1f);
                d.noGravity = true;
            }
        }

        //绘制

        public float GetWidthFunc(float completionRatio) =>
            (1f - completionRatio) * (14f + 8f * ChargeLevel); //0=钉头最宽

        public Color GetColorFunc(Vector2 coord) => Color.White * (0.6f + 0.3f * ChargeLevel) * (1f - coord.X);

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Mode != ModeFlight) {
                return;
            }
            Effect fx = PallbearerAssets.PallbearerTrail;
            if (fx == null || !Projectile.active) {
                return;
            }
            PallbearerVFX.ApplyTrail(fx, Projectile.whoAmI * 0.61f);
            GraniteMarbleVFX.DrawTrailFromOldPos(Projectile, ref trail, GetWidthFunc, GetColorFunc, fx);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;
            Color bodyColor = Color.Lerp(lightColor, PallbearerVFX.BloodDeep, 0.22f) * Projectile.Opacity;

            if (Mode == ModeFlight) {
                //高速拉伸残影补帧
                Color ghost = PallbearerVFX.Blood with { A = 0 };
                Main.EntitySpriteDraw(texture, drawPos - Projectile.velocity * 1.4f - Main.rand.NextVector2Circular(0.5f, 0.5f)
                    , null, ghost * 0.38f, Projectile.rotation, origin
                    , new Vector2(0.86f, 1.5f) * Projectile.scale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(texture, drawPos - Projectile.velocity * 2.8f, null, ghost * 0.16f
                    , Projectile.rotation, origin, new Vector2(0.78f, 1.8f) * Projectile.scale, SpriteEffects.None, 0);

                //本体沿速拉伸
                Main.EntitySpriteDraw(texture, drawPos, null, bodyColor, Projectile.rotation, origin
                    , new Vector2(0.92f, 1.18f) * Projectile.scale, SpriteEffects.None, 0);
                return false;
            }

            //钉地静止
            Main.EntitySpriteDraw(texture, drawPos, null, bodyColor,
                Projectile.rotation, origin, Projectile.scale * 0.92f, SpriteEffects.None, 0);
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Mode != ModeFlight) {
                return false;
            }
            //线段碰撞
            Vector2 lineDirection = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Vector2 start = Projectile.Center - lineDirection * 16f;
            Vector2 end = Projectile.Center + lineDirection * 16f;
            float value = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 10f, ref value);
        }
    }

    /// <summary>
    /// 落棺。ai0目标whoAmI，behindNPCs绘制
    /// </summary>
    internal class PallbearerCoffinSeal : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<Pallbearer>()).DisplayName;

        private ref float BoundTarget => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        //时间轴 46帧
        private const int RiseEnd = 12;       //浮现/收链
        private const int SlamFrame = 16;     //合盖帧
        private const int DissolveStart = 20;
        private const int TotalFrames = 46;

        /// <summary>封殓debuff时长</summary>
        public const int SealedDuration = 300;
        /// <summary>标记下落棺倍率</summary>
        public const float MarkedSealMult = 1.5f;

        private float coffinW = 120f;
        private float coffinH = 200f;
        private float seed;
        private bool slamDone;      //本端合盖表现一次
        private float chainFade = 1f;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //合盖一次
            Projectile.hide = true; //仅behindNPCs绘制
        }

        /// <summary>棺影锚点，目标中心略上</summary>
        public static Vector2 AnchorFor(NPC target) => target.Center - new Vector2(0f, target.height * 0.12f);

        private NPC Target {
            get {
                int idx = (int)BoundTarget;
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[idx];
                return npc.active ? npc : null;
            }
        }

        public override void AI() {
            NPC target = Target;

            if (Timer == 0f) {
                //按目标体型定棺
                if (target != null) {
                    coffinW = MathHelper.Clamp(target.width * 1.6f + 46f, 96f, 260f);
                    coffinH = MathHelper.Clamp(target.height * 1.7f + 96f, 170f, 430f);
                }
                seed = (Projectile.identity % 97) * 0.113f;
                //破土钟
                PallbearerVFX.BellToll(Projectile.Center, 0.3f, 0.85f);
                SoundEngine.PlaySound(SoundID.DoorOpen with { Pitch = -0.55f, Volume = 0.7f }, Projectile.Center);
                PallbearerVFX.Punch(Projectile.Center, Vector2.UnitY, 4f, 9f, 8, 800f);
            }

            //锚目标，消失则进消散
            if (target != null) {
                Projectile.Center = AnchorFor(target);
            }
            else if (Timer < DissolveStart) {
                Timer = DissolveStart;
            }

            //死寂段顿帧
            if (Timer >= RiseEnd && Timer < SlamFrame && target != null) {
                target.CWR().TimeFrozenTick = 2;
            }

            //合盖帧
            if (Timer == SlamFrame && !slamDone) {
                slamDone = true;
                DoSlamPresentation(target);
            }

            //浮现/消散余烬
            if (!Main.dedServ) {
                if (Timer < RiseEnd && Main.rand.NextBool(2)) {
                    Vector2 rim = Projectile.Center + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * coffinW
                        , Main.rand.NextFloat(-0.5f, 0.5f) * coffinH);
                    PRTLoader.NewParticle<PRT_PallbearerEmber>(rim, Main.rand.NextVector2Circular(1.6f, 1.2f)
                        , PallbearerVFX.Blood, Main.rand.NextFloat(0.45f, 0.75f))?.Configure(16);
                }
                if (Timer >= DissolveStart && Main.rand.NextBool(2)) {
                    float dissolveT = (Timer - DissolveStart) / (float)(TotalFrames - DissolveStart);
                    Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * coffinW
                        , MathHelper.Lerp(coffinH * 0.5f, -coffinH * 0.5f, dissolveT) * Main.rand.NextFloat(0.7f, 1f));
                    PRTLoader.NewParticle<PRT_PallbearerEmber>(pos, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2.4f, -0.8f))
                        , Main.rand.NextBool(3) ? PallbearerVFX.BloodDeep : PallbearerVFX.Blood
                        , Main.rand.NextFloat(0.5f, 0.85f))?.Configure(22);
                }
            }

            //合盖后链淡出
            if (Timer > RiseEnd) {
                chainFade = MathF.Max(0f, chainFade - 0.18f);
            }

            Lighting.AddLight(Projectile.Center, PallbearerVFX.Blood.ToVector3() * (Timer < SlamFrame ? 0.45f : 0.7f * GlowEnvelope()));
            Timer++;
        }

        private float GlowEnvelope() {
            return 1f - MathHelper.Clamp((Timer - DissolveStart) / (float)(TotalFrames - DissolveStart), 0f, 1f);
        }

        /// <summary>合盖单帧表现，伤害走碰撞窗</summary>
        private void DoSlamPresentation(NPC target) {
            Vector2 pos = Projectile.Center;
            PallbearerVFX.BellToll(pos, 1f, 1f);
            SoundEngine.PlaySound(SoundID.DoorClosed with { Pitch = -0.6f, Volume = 1f }, pos);
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.85f, Volume = 0.8f }, pos);
            PallbearerVFX.Punch(pos, Vector2.UnitY, 10f, 12f, 16, 950f);

            if (target != null) {
                target.CWR().TimeFrozenTick = 12; //合盖顿帧
            }

            if (!Main.dedServ) {
                //冲击环+爆闪+余烬+碎屑
                PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, PallbearerVFX.Blood, 0.22f)
                    ?.Configure(new Vector2(1f, 0.74f), 0f, 1.5f, 16);
                PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, PallbearerVFX.BloodDeep, 0.14f)
                    ?.Configure(new Vector2(1f, 0.74f), 0f, 0.9f, 12);
                PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, PallbearerVFX.Ember, 0.8f)
                    ?.Configure(10, 1f, 0.6f, 1.5f);
                PallbearerVFX.EmberBurst(pos, 22, 6.5f, 1.1f);
                for (int i = 0; i < 9; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(5f, 11f);
                    PRTLoader.NewParticle<PRT_Line>(pos, vel, PallbearerVFX.Blood, Main.rand.NextFloat(0.5f, 0.85f))
                        ?.Configure(true, 18);
                }
                PallbearerVFX.Splinters(pos, -Vector2.UnitY, 8, 7.5f);
            }
        }

        //伤害，合盖窗×封殓目标

        public override bool? CanDamage() => Timer >= SlamFrame && Timer <= SlamFrame + 2 ? null : false;

        public override bool? CanHitNPC(NPC target) => target.whoAmI == (int)BoundTarget ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NPC target = Target;
            if (target == null) {
                return false;
            }
            Rectangle coffinRect = new((int)(Projectile.Center.X - coffinW * 0.5f), (int)(Projectile.Center.Y - coffinH * 0.5f)
                , (int)coffinW, (int)coffinH);
            return coffinRect.Intersects(targetHitbox);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.HasBuff(ModContent.BuffType<PallbearerShrouded>())) {
                modifiers.FinalDamage *= MarkedSealMult;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //封殓承伤buff
            target.AddBuff(ModContent.BuffType<PallbearerSealed>(), SealedDuration);
            //兑现后清收殓标记
            if (target.HasBuff(ModContent.BuffType<PallbearerShrouded>())) {
                target.RequestBuffRemoval(ModContent.BuffType<PallbearerShrouded>());
            }
        }

        //绘制（NPC身后）

        public override bool PreDraw(ref Color lightColor) {
            float reveal = PallbearerVFX.EaseOutCubic(Timer / 9f);
            //落定/死寂缩/合盖弹
            float scale = MathHelper.Lerp(0.8f, 1f, PallbearerVFX.EaseOutBack(MathHelper.Clamp(Timer / 10f, 0f, 1f)));
            if (Timer >= RiseEnd && Timer < SlamFrame) {
                scale *= MathHelper.Lerp(1f, 0.955f, (Timer - RiseEnd) / (float)(SlamFrame - RiseEnd));
            }
            else if (Timer >= SlamFrame) {
                scale *= 1f + 0.06f * MathF.Exp(-(Timer - SlamFrame) * 0.4f);
            }
            float erode = MathHelper.Clamp((Timer - DissolveStart) / (float)(TotalFrames - DissolveStart), 0f, 1f);
            float close = MathHelper.Clamp((Timer - RiseEnd) / (float)(SlamFrame - RiseEnd), 0f, 1f);
            float slamFlash = Timer >= SlamFrame ? MathF.Exp(-(Timer - SlamFrame) * 0.45f) : 0f;

            DrawCoffinQuad(reveal, erode, close, slamFlash, scale);
            DrawChains();
            return false;
        }

        private void DrawCoffinQuad(float reveal, float erode, float close, float slamFlash, float scale) {
            Effect fx = PallbearerAssets.PallbearerSeal;
            GraphicsDevice device = Main.instance.GraphicsDevice;
            Vector2 center = Projectile.Center;
            float hx = coffinW * 0.5f * scale;
            float hy = coffinH * 0.5f * scale;

            if (fx == null || CWRAsset.PerlinNoise?.Value == null) {
                //无shader降级剪影
                Texture2D soft = CWRAsset.SoftGlow?.Value;
                if (soft != null) {
                    Main.EntitySpriteDraw(soft, center - Main.screenPosition, null
                        , PallbearerVFX.CharDark * (0.85f * reveal * (1f - erode))
                        , 0f, soft.Size() / 2f, new Vector2(hx, hy) / 26f, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(soft, center - Main.screenPosition, null
                        , PallbearerVFX.Blood with { A = 0 } * (0.35f * reveal * (1f - erode))
                        , 0f, soft.Size() / 2f, new Vector2(hx, hy) / 22f, SpriteEffects.None, 0);
                }
                return;
            }

            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uReveal"]?.SetValue(reveal);
            fx.Parameters["uErode"]?.SetValue(erode);
            fx.Parameters["uClose"]?.SetValue(close);
            fx.Parameters["uSlam"]?.SetValue(slamFlash);
            fx.Parameters["uSizePx"]?.SetValue(new Vector2(hx * 2f, hy * 2f));
            fx.Parameters["uNoiseTex"]?.SetValue(CWRAsset.PerlinNoise.Value);
            fx.Parameters["uColBody"]?.SetValue(PallbearerVFX.Charcoal.ToVector3());
            fx.Parameters["uColBodyDark"]?.SetValue(PallbearerVFX.CharDark.ToVector3());
            fx.Parameters["uColBrand"]?.SetValue(PallbearerVFX.Blood.ToVector3());
            fx.Parameters["uColEmber"]?.SetValue(PallbearerVFX.BloodDeep.ToVector3());

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(center.X - hx, center.Y - hy, 0f), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(center.X + hx, center.Y - hy, 0f), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(new Vector3(center.X - hx, center.Y + hy, 0f), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(new Vector3(center.X + hx, center.Y + hy, 0f), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>烙印锚→棺沿，收束后随合盖隐</summary>
        private void DrawChains() {
            if (chainFade <= 0.01f || Timer < 2f) {
                return;
            }
            NPC target = Target;
            if (target == null) {
                return;
            }
            //收束2~10帧
            float taut = MathHelper.Clamp((Timer - 2f) / 8f, 0f, 1f);
            float t = Main.GlobalTimeWrappedHourly * 2.2f;
            for (int i = 0; i < 3; i++) {
                //identity播种体表点
                float hash = ((Projectile.identity * 7919 + i * 977) % 1000) / 1000f;
                float ang = hash * MathHelper.TwoPi;
                Vector2 bodyPoint = target.Center + new Vector2(MathF.Cos(ang) * target.width * 0.38f
                    , MathF.Sin(ang) * target.height * 0.38f);
                Vector2 rimAnchor = Projectile.Center + (i switch {
                    0 => new Vector2(-coffinW * 0.42f, -coffinH * 0.18f),
                    1 => new Vector2(coffinW * 0.42f, -coffinH * 0.18f),
                    _ => new Vector2(0f, -coffinH * 0.46f),
                });
                PallbearerVFX.DrawChain(Main.spriteBatch, bodyPoint, rimAnchor, taut, chainFade, t + i * 0.31f);
            }
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs
            , List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            //身后层
            behindNPCs.Add(index);
        }
    }

    /// <summary>收殓标记，吃钉×1.75 落棺×1.5</summary>
    internal class PallbearerShrouded : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        /// <summary>标记时长10s</summary>
        public const int MarkDuration = 600;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            //标记余烬
            if (!Main.dedServ && Main.rand.NextBool(12)) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.55f, npc.height * 0.55f);
                PRTLoader.NewParticle<PRT_PallbearerEmber>(pos, new Vector2(0f, Main.rand.NextFloat(-0.7f, 0.3f))
                    , PallbearerVFX.BloodDeep, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(18, 0.02f);
            }
        }
    }

    /// <summary>封殓承伤debuff</summary>
    internal class PallbearerSealed : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        /// <summary>承伤倍率</summary>
        public const float DamageTakenMult = 1.10f;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }
    }

    /// <summary>
    /// 烙印簿，InstancePerEntity。owner端计数，每道+5%承伤
    /// </summary>
    internal class PallbearerNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>落棺所需烙印数</summary>
        public const int MaxBrands = 3;
        /// <summary>每道承伤加成</summary>
        public const float DamageTakenPerBrand = 0.05f;
        /// <summary>烙印消退时长</summary>
        public const int BrandDecayTime = 420;

        /// <summary>烙印数0..3</summary>
        public int BrandCount { get; private set; }
        private int brandDecay;
        //延迟分配数组
        private Vector2[] brandOffsets;
        private float[] brandRots;
        private float[] brandAges;

        /// <summary>加烙印，返回当前数</summary>
        public int AddBrand(NPC npc, Vector2 hitPos, float hitRot) {
            brandOffsets ??= new Vector2[MaxBrands];
            brandRots ??= new float[MaxBrands];
            brandAges ??= new float[MaxBrands];
            if (BrandCount < MaxBrands) {
                Vector2 offset = hitPos - npc.Center;
                float maxR = MathF.Max(npc.width, npc.height) * 0.42f;
                if (offset.Length() > maxR) {
                    offset = offset.SafeNormalize(Main.rand.NextVector2Unit()) * maxR * Main.rand.NextFloat(0.35f, 0.85f);
                }
                brandOffsets[BrandCount] = offset;
                brandRots[BrandCount] = hitRot + Main.rand.NextFloat(-0.25f, 0.25f);
                brandAges[BrandCount] = 0f;
                BrandCount++;
            }
            brandDecay = BrandDecayTime;
            return BrandCount;
        }

        /// <summary>清空烙印</summary>
        public void ClearBrands() {
            BrandCount = 0;
            brandDecay = 0;
        }

        public override void PostAI(NPC npc) {
            if (BrandCount <= 0) {
                return;
            }
            for (int i = 0; i < BrandCount; i++) {
                brandAges[i]++;
            }
            if (--brandDecay <= 0) {
                BrandCount = 0;
            }
        }

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            if (BrandCount > 0) {
                modifiers.FinalDamage *= 1f + DamageTakenPerBrand * BrandCount;
            }
            if (npc.HasBuff(ModContent.BuffType<PallbearerSealed>())) {
                modifiers.FinalDamage *= PallbearerSealed.DamageTakenMult;
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (BrandCount <= 0 || npc.IsABestiaryIconDummy) {
                return;
            }
            Texture2D streak = CWRAsset.Extra_98?.Value;
            if (streak == null) {
                return;
            }
            Vector2 origin = streak.Size() * 0.5f;
            for (int i = 0; i < BrandCount; i++) {
                Vector2 pos = npc.Center + brandOffsets[i] - screenPos;
                float rot = brandRots[i] + MathHelper.PiOver2;
                //初烙闪光后定格
                float flare = MathF.Max(0f, 1f - brandAges[i] / 9f);
                float s = 1f + flare * 0.55f;
                float pulse = 0.82f + 0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + i * 2.1f);
                //焦黑底
                spriteBatch.Draw(streak, pos, null, PallbearerVFX.CharDark * 0.72f, rot, origin
                    , new Vector2(0.3f, 0.8f) * s, SpriteEffects.None, 0f);
                //血色芯
                Color core = PallbearerVFX.Blood with { A = 0 };
                spriteBatch.Draw(streak, pos, null, core * ((0.75f + flare * 0.6f) * pulse), rot, origin
                    , new Vector2(0.15f, 0.62f) * s, SpriteEffects.None, 0f);
                spriteBatch.Draw(streak, pos, null, core * (0.5f * pulse), rot, origin
                    , new Vector2(0.08f, 0.45f) * s, SpriteEffects.None, 0f);
            }
        }
    }
}
