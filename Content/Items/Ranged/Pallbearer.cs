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
    /// 抬棺人：弩身木料取自埋地百年的漆黑棺材。<br/>
    /// 左键装填蓄力射出「棺钉」——近乎瞬发的重弩栓，贯穿数个目标，每个被贯穿者
    /// 身上留下一道血色烙印；右键把弩整具掷出，命中者沾上「收殓标记」；
    /// 同一目标积累三道烙印即触发「落棺」：焦黑棺影瞬现、合盖一击处决
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
            Item.channel = true; //允许持续按住
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
            //单 HeldProj：弩在手上或棺还没飞回来都不允许再次使用
            return player.ownedProjectileCounts[Item.shoot] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<PallbearerBoomerang>()] == 0;
        }

        public override bool CanConsumeAmmo(Item ammo, Player player) {
            if (player.ownedProjectileCounts[Item.shoot] == 0) {
                return false; //是0说明正在发射手持弹幕本身，不消耗弹药；棺钉在 PickAmmo 时消耗
            }
            return player.altFunctionUse != 2; //右键掷棺不消耗弹药
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //生成手持弹幕而不是直接射出箭矢
            Projectile.NewProjectile(source, position, velocity, Item.shoot, damage, knockback, player.whoAmI);
            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity
            , ref int type, ref int damage, ref float knockback) {
            //修正射击起始位置到玩家中心
            position = player.GetPlayerStabilityCenter();
        }
    }

    /// <summary>
    /// 抬棺人弩手持弹幕：装填 → 蓄力 → 射钉状态机。<br/>
    /// 蓄力语法：血色火星向弩口坍缩、密度∝sqrt(charge)、72% 处硬切静默（尖叫前的吸气）；
    /// 发射帧 = 弩身大后座（12px 指数收回）+ 玩家小位移后坐 + 血橙暖色瞬时爆闪 + 定向震屏
    /// </summary>
    internal class PallbearerHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Ranged + "PallbearerHeld";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<Pallbearer>()).DisplayName;

        private enum CrossbowState
        {
            Idle,       //待机
            Loading,    //装填棺钉
            Charged,    //满弦蓄力
            Firing,     //发射收势
            Winding     //掷棺蓄势：反向后拉，力量长在出手前
        }

        private CrossbowState State {
            get => (CrossbowState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float StateTimer => ref Projectile.ai[1];
        private ref float ChargeLevel => ref Projectile.localAI[0]; //蓄力等级 0-1

        private float armRotation;

        //==== 发射反馈（客户端表现量，不参与同步）====
        private float recoilPunch;      //后座位移 px，指数收回
        private float stringSnap;       //弦回弹：1 拉满 → 发射瞬间 -0.22 过冲 → 归零
        private bool chargeCue;         //72% 静默切入提示音只播一次
        private float windupPull;       //掷棺蓄势后拉位移 px（迟滞后吸曲线）

        //==== 常量 ====
        private const int LoadDuration = 28;        //装填时长
        private const int MaxChargeDuration = 60;   //最大蓄力时长
        private const int FireDuration = 12;        //射击收势时长
        private const float ChargeSilence = 0.72f;  //蓄力硬切静默点
        private const int WindupFrames = 7;         //掷棺蓄势帧数：短到不打断节奏，长到看得见
        private const float WindupPullDist = 26f;   //蓄势后拉幅度 px

        private float bowstringPullback; //弓弦拉动进度 0-1

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 4; //4帧：0待机 1装填过渡 2满弦 3击发回弹
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 32;
            Projectile.friendly = false; //手持本体不参与碰撞伤害
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

            recoilPunch *= 0.74f; //后座指数收回
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
                //棺木咯吱：装填从撬开棺板般的木鸣开始
                SoundEngine.PlaySound(SoundID.DoorOpen with { Pitch = -0.35f, Volume = 0.5f, MaxInstances = 3 }, Owner.Center);
            }

            if (DownRight) {
                //先蓄势再出手：力量长在出手前的反向动作里
                State = CrossbowState.Winding;
                StateTimer = 0;
                windupPull = 0f;
                SoundEngine.PlaySound(SoundID.DoorOpen with { Pitch = -0.55f, Volume = 0.45f, MaxInstances = 2 }, Owner.Center);
            }
        }

        /// <summary>
        /// 掷棺蓄势：pow(t,3) 迟滞后吸——前几帧几乎不动，最后两帧猛地拽向身后，随即出手。
        /// 一旦进入即不可取消（挥出重物没有半途收力的余地）
        /// </summary>
        private void HandleWinding() {
            float t = MathHelper.Clamp(StateTimer / (float)WindupFrames, 0f, 1f);
            windupPull = MathF.Pow(t, 3f) * WindupPullDist;

            //拽到最深的一帧甩出
            if (StateTimer >= WindupFrames) {
                windupPull = 0f;
                ThrowCrossbow();
            }
        }

        private void HandleLoading() {
            float loadProgress = StateTimer / LoadDuration;
            Projectile.frame = loadProgress < 0.5f ? 0 : 1;
            bowstringPullback = MathHelper.SmoothStep(0f, 1f, loadProgress);

            //土腥味：棺木上散落的陈年尘土被弦带起
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
                //弦紧绷
                SoundEngine.PlaySound(CWRSound.Bow_String with { Pitch = -0.1f, Volume = 0.7f, MaxInstances = 3 }, Owner.Center);
            }

            if (!DownLeft) { //取消装填
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
                //==== 蓄力语法：血色火星向弩口坍缩，密度∝sqrt(charge) ====
                if (!Main.dedServ && Main.rand.NextFloat() < 0.18f + 0.6f * MathF.Sqrt(ChargeLevel)) {
                    SpawnConvergeSpark();
                }
                //弦与木料的受力呻吟，音高随蓄力爬升
                if (StateTimer % 15 == 0) {
                    SoundEngine.PlaySound(SoundID.DoorOpen with {
                        Volume = 0.22f,
                        Pitch = -0.1f + ChargeLevel * 0.5f,
                        MaxInstances = 2
                    }, Owner.Center);
                }
            }

            //72% 硬切静默：一声弦定，此后无声无粒子——击发前的吸气
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
            //弦回弹可见：发射瞬间过冲到 -0.22，随后弹性归零
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

        /// <summary>蓄力坍缩火星：从 60~130px 外被拽向弩口的血色火星，顺速度拉丝、始终指心</summary>
        private void SpawnConvergeSpark() {
            Vector2 muzzle = MuzzlePos();
            Vector2 pos = muzzle + Main.rand.NextVector2Unit() * Main.rand.NextFloat(60f, 130f);
            Vector2 vel = (muzzle - pos) * 0.115f; //比例拽力：火星坍缩进弩口
            Color col = Color.Lerp(PallbearerVFX.BloodDeep, PallbearerVFX.Blood, ChargeLevel);
            PRTLoader.NewParticle<PRT_Spark>(pos, vel, col, 0.45f + 0.4f * ChargeLevel)
                ?.Configure(false, 15);
        }

        /// <summary>发射棺钉：单帧执行 —— 生成(owner)、大后座、玩家后坐、震屏、分层音效、暖色爆闪</summary>
        private void FireNail() {
            float charge = ChargeLevel;
            Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            Vector2 muzzle = MuzzlePos();

            //==== owner 端：弹药转换与弹幕生成 ====
            bool fired = true;
            if (Projectile.IsOwnedByLocalPlayer()) {
                fired = Owner.PickAmmo(Owner.GetItem(), out int _, out float speed, out int damage
                    , out float knockback, out int _);
                if (fired) {
                    float mult = 1f + 0.5f * charge;
                    //每 update 速度，extraUpdates=4 → 实际弹速 ×5，近乎瞬发的重弩栓
                    Vector2 shootVelocity = aim * MathHelper.Clamp(speed * (1f + 0.3f * charge), 12f, 26f);
                    Projectile.NewProjectile(
                        Owner.GetSource_ItemUse(Owner.GetItem()),
                        muzzle, shootVelocity,
                        ModContent.ProjectileType<PallbearerArrow>(),
                        (int)(damage * mult),
                        knockback * (1f + 0.5f * charge),
                        Owner.whoAmI,
                        charge);
                    //玩家小位移后坐：打桩机顶肩
                    if (!Owner.mount.Active) {
                        Owner.velocity -= aim * (1.1f + 1.7f * charge);
                    }
                }
            }

            if (!fired) {
                //空匣：干瘪的一声锁响
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.4f, Volume = 0.4f }, Owner.Center);
                return;
            }

            //==== 各端表现（单帧打击链）====
            recoilPunch = 12f;      //弩身大后座 punch，指数收回
            stringSnap = -0.22f;    //弦过冲

            SoundEngine.PlaySound(CWRSound.Gun_Crossbow_Shoot with {
                Volume = 1f,
                Pitch = -0.15f + charge * 0.12f,
                MaxInstances = 3
            }, muzzle);
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.7f, Volume = 0.5f, MaxInstances = 3 }, muzzle);
            if (charge > 0.7f) {
                //满弦重栓：弩炮级的第三层低音
                SoundEngine.PlaySound(SoundID.DD2_BallistaTowerShot with { Pitch = -0.3f, Volume = 0.55f, MaxInstances = 2 }, muzzle);
            }

            PallbearerVFX.Punch(muzzle, aim, 5f + 3f * charge, 9f, 12, 750f);

            if (!Main.dedServ) {
                //枪口血橙暖色爆闪：小面积、一瞬即灭
                PRTLoader.NewParticle<PRT_Light>(muzzle, aim * 2f, PallbearerVFX.Ember, 0.36f + 0.2f * charge)
                    ?.Configure(9, 1f, 1.5f, 2.4f);
                //枪口破空痕：沿射向拉长的粗短光痕，几帧即灭——击发那一帧的"烟斗"
                PRTLoader.NewParticle<PRT_PallbearerTracer>(muzzle, Vector2.Zero, default, 1f)
                    ?.Configure(muzzle - aim * 14f, muzzle + aim * (95f + 55f * charge), 20f + 10f * charge, 6);
                //粒子量∝动能：血色锐线锥
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

        /// <summary>掷棺出手帧：蓄势拽到最深后的爆发——快出手、飞行中还在持续加速</summary>
        private void ThrowCrossbow() {
            Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(
                    Owner.GetSource_ItemUse(Owner.GetItem()),
                    Projectile.Center,
                    aim * PallbearerBoomerang.LaunchSpeed, //出手即快，此后复利加速持续压榨
                    ModContent.ProjectileType<PallbearerBoomerang>(),
                    (int)(Projectile.damage * 0.85f),
                    Projectile.knockBack * 1.5f,
                    Owner.whoAmI
                );
                if (!Owner.mount.Active) {
                    Owner.velocity -= aim * 1.8f; //甩出的反作用
                }
            }

            //沉重的木器破空 + 棺盖颤响 + 出手震屏 + 手边一道破空痕
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
                    //满弦震颤：进入静默段后振幅衰减到近乎凝滞（吸气）
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
                    //蓄势：整条手臂随棺弩拽向身后
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
            Projectile.velocity = aimDir; //稳定的方向向量

            float holdDistance = 20f + ((State == CrossbowState.Loading || State == CrossbowState.Charged) ? bowstringPullback * 8f : 0f);
            holdDistance -= recoilPunch;  //发射后座
            holdDistance -= windupPull;   //掷棺蓄势：棺弩拽向身后
            if (State == CrossbowState.Firing) {
                holdDistance -= stringSnap * 8f; //弦过冲：击发瞬间弩身随弦轻微前送再弹回
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

            //装上膛的棺钉：满弦时可见地压在钉槽里，随弦后拉
            if (State == CrossbowState.Charged || (State == CrossbowState.Loading && bowstringPullback > 0.55f)) {
                Texture2D nailTex = TextureAssets.Projectile[ModContent.ProjectileType<PallbearerArrow>()].Value;
                Vector2 nailPos = drawPos + Projectile.velocity * (14f - bowstringPullback * 7f);
                Main.EntitySpriteDraw(nailTex, nailPos, null, lightColor, Projectile.rotation + MathHelper.PiOver2
                    , nailTex.Size() / 2f, 0.85f, fx, 0);
            }

            Main.EntitySpriteDraw(texture, drawPos, frame, lightColor, Projectile.rotation, origin, Projectile.scale, fx, 0);

            //蓄力驻留辉光：暗红自缝隙透出；静默段亮度封顶稳定驻留（视觉宣告就绪，无声）
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
    /// 掷棺回旋：一帧设满高速甩出，近直线贯穿，硬刹折返高速吸附回手。<br/>
    /// 命中者挂「收殓标记」——棺椁的气息沾了身，这口棺材就知道该收殓谁
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

        //==== 运动学参数 ====
        //恒速直线没有动力学：加速感 = 出手快且飞行中仍在复利压榨；
        //力量感 = 出手前的反向蓄势 + 顶点硬刹的过冲 + 全速砸进手里的收势
        /// <summary>出手初速：已经很快，但只是起点</summary>
        public const float LaunchSpeed = 26f;
        private const float FlightAccelMul = 1.055f;   //飞行段复利加速/帧：越飞越快，呼啸越来越尖
        private const float FlightMaxSpeed = 74f;      //飞行段速度上限
        private const int MaxFlightFrames = 24;        //直线段最长帧数
        private const float MaxDistance = 950f;        //最大射程
        private const float BrakeMul = 0.62f;          //硬刹每帧衰减：撞上空气墙
        private const int HoverFrames = 8;             //顶点悬滞：猛拽前的半拍屏息
        private const int ReturnCreepFrames = 4;       //回程前几帧的缓慢蠕动：链条绷紧的张力
        private const float ReturnAccelMul = 1.13f;    //回程复利加速/帧：被链条猛拽回来
        private const float ReturnMaxSpeed = 78f;      //回程峰值，全速砸进手里不减速

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
                    //复利加速：出手已快，仍每帧 ×1.055 持续压榨——棺弩越飞越快地撕开空气
                    float speed = Projectile.velocity.Length();
                    if (speed < FlightMaxSpeed) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX)
                            * MathF.Min(speed * FlightAccelMul, FlightMaxSpeed);
                    }
                    if (Time >= MaxFlightFrames || toPlayer.Length() > MaxDistance) {
                        State = BoomerangState.Braking;
                        Time = 0f;
                        //撞上空气墙的破空爆响
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 0.6f, MaxInstances = 2 }, Projectile.Center);
                    }
                    break;
                }
                case BoomerangState.Braking: {
                    //硬刹 ×0.62/f：全速一头钉停。刹死后残余一丝反向过冲（质量惯性）
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
                    //顶点屏息：反向过冲衰减成轻微浮沉，转速掉下来——猛拽前的半拍死寂
                    Projectile.velocity *= 0.82f;
                    Projectile.velocity.Y += MathF.Sin(Time * 0.55f) * 0.14f;
                    if (Time >= HoverFrames) {
                        State = BoomerangState.Returning;
                        Time = 0f;
                        ReturnSpeed = 10f;
                        //链条绷紧的一声闷响
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.3f, Volume = 0.7f }, Projectile.Center);
                        SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.6f, Volume = 0.4f }, Projectile.Center);
                    }
                    break;
                }
                default: {
                    //回程两段：前几帧缓慢蠕动（链条张力），随后复利猛拽，全速砸进手里不减速
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
                        //catch：全速砸进手里——震屏、双层闷响、余烬迸出、玩家被顶得一晃
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

            //旋转随速度；回程被链条拽着转得更疯
            float velLen = Projectile.velocity.Length();
            float targetSpin = 0.1f + velLen / 80f * (State == BoomerangState.Returning ? 1.35f : 1f);
            SpinSpeed = MathHelper.Lerp(SpinSpeed, targetSpin, 0.3f);
            Projectile.rotation += SpinSpeed * Math.Sign(Projectile.velocity.X == 0 ? Owner.direction : Projectile.velocity.X);

            //呼啸随速度爬升：加速感听得见
            if (Time % 7 == 0 && velLen > 14f) {
                float speedT = MathHelper.Clamp((velLen - 14f) / 60f, 0f, 1f);
                SoundEngine.PlaySound(SoundID.Item32 with {
                    Volume = 0.28f + 0.24f * speedT,
                    Pitch = -0.3f + speedT * 0.9f,
                    MaxInstances = 2
                }, Projectile.Center);
            }

            //余烬甩尾：甩出率随速度升档——越快甩得越凶
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
            //==== 收殓标记：棺椁的气息沾染客人（buff 走原版包同步，各端一致）====
            target.AddBuff(ModContent.BuffType<PallbearerShrouded>(), PallbearerShrouded.MarkDuration);
            target.CWR().TimeFrozenTick = 3; //轻顿帧

            //咬肉感：命中瞬间轻微减速，直线段下一帧立刻回满速
            if (State == BoomerangState.Throwing) {
                Projectile.velocity *= 0.55f;
            }

            //重物撞击 + 标记认领的轻钟
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.55f, Volume = 0.85f, MaxInstances = 3 }, target.Center);
            SoundEngine.PlaySound(SoundID.Item35 with { Pitch = -0.3f, Volume = 0.35f, MaxInstances = 2 }, target.Center);
            PallbearerVFX.Punch(target.Center, Projectile.velocity, 4.5f, 8f, 10, 650f);

            //血爆 + 焦黑木屑
            PallbearerVFX.BloodBurst(target.Center, Projectile.velocity, 0.8f);
            PallbearerVFX.Splinters(target.Center, -Projectile.velocity.SafeNormalize(Vector2.UnitX), 3, 4.5f);
        }

        //==== 绘制：双层条带 + 旋转拖影 + 殓棺锁链 ====

        private float SpeedT => MathHelper.Clamp(Projectile.velocity.Length() / ReturnMaxSpeed, 0f, 1f);

        public float GetOuterWidth(float completionRatio) {
            float speedFade = MathHelper.Clamp(Projectile.velocity.Length() / 46f, 0.15f, 1f);
            return (1f - completionRatio) * 38f * speedFade; //completion 0 = 最新端（头）最宽
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
            //宽软外层铺氛围，窄亮内芯给锐度：同一 fx 两次装配
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

            //殓棺锁链：悬滞时垂坠松弛，回程猛拽绷直，亮环奔向持弩人——"被棺链拽回来"
            if (State == BoomerangState.Hover || State == BoomerangState.Returning) {
                float taut = State == BoomerangState.Hover ? 0.3f : 0.45f + 0.55f * speedFactor;
                PallbearerVFX.DrawChain(sb, Projectile.Center, Owner.GetPlayerStabilityCenter()
                    , taut, 0.55f, Main.GlobalTimeWrappedHourly * 1.7f);
            }

            //位移拖影：旧位置的暗红残像，速度越快越实
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

            //旋转拖影：按转速回溯旋转角的残像糊成磨盘——自旋的可视化
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

            //速度门控的深红底晕：慢时无光，快时血光拖行
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(soft, drawPos, null, PallbearerVFX.BloodDeep with { A = 0 } * (0.5f * speedFactor)
                , 0f, soft.Size() / 2f, 1.3f + speedFactor * 0.5f, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(texture, drawPos, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 棺钉：近乎瞬发的重弩栓。贯穿至多 4 个目标，每个被贯穿者吃到独立的
    /// 血爆 + 低频闷击 + 顿帧，并留下一道「血色烙印」；同一目标累满三道烙印触发「落棺」。<br/>
    /// ai[0]=蓄力 0-1；ai[2]=模式 0飞行/1钉地
    /// </summary>
    internal class PallbearerArrow : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Ranged + "PallbearerArrow";

        private ref float ChargeLevel => ref Projectile.ai[0];
        private ref float Mode => ref Projectile.ai[2];

        private const float ModeFlight = 0f;
        private const float ModeStuckTile = 1f;

        /// <summary>收殓标记下吃钉的伤害倍率</summary>
        public const float MarkedDamageMult = 1.75f;

        private Trail trail;
        //弹道光痕锚点：各端本地在首帧记录出膛位置，终点事件时铺一道驻留光痕
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
            Projectile.penetrate = 4;                   //贯穿 4 目标：每一发都是打桩
            Projectile.timeLeft = 200;
            Projectile.arrow = true;                    //弹药转换而来，吃箭袋类加成
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 4;                //5 倍 tick：近乎瞬发的重弩栓（时长常量按 5 拍折算）
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;        //每枚钉对每个目标只命中一次
            Projectile.ArmorPenetration = 32767;        //面对钉子，甲壳毫无意义
        }

        public override bool? CanDamage() => Mode == ModeFlight ? null : false;

        public override void AI() {
            if (!birthSet) {
                birthSet = true;
                birthPos = Projectile.Center;
            }
            if (Mode == ModeFlight) {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                //飞行余烬：低频，血色一线
                if (!Main.dedServ && Main.rand.NextBool(22)) {
                    PRTLoader.NewParticle<PRT_PallbearerEmber>(Projectile.Center - Projectile.velocity * 0.6f
                        , -Projectile.velocity * 0.04f, PallbearerVFX.BloodDeep
                        , Main.rand.NextFloat(0.3f, 0.5f))?.Configure(12);
                }
            }
            else {
                //钉进地里：不再更新位置，静静钉着直到消隐
                Projectile.velocity = Vector2.Zero;
            }

            Lighting.AddLight(Projectile.Center, PallbearerVFX.Blood.ToVector3() * (0.2f + 0.25f * ChargeLevel));
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //高 DR 目标削减其减伤：毁灭毫无意义，钉入才有意义
            float dr = CWRRef.GetNPCDR(target);
            if (dr > 0f && dr <= 0.9f) {
                modifiers.FinalDamage *= (1f - dr * 0.5f) / (1f - dr);
            }
            //收殓标记：棺材认得它，钉得更深
            if (target.HasBuff(ModContent.BuffType<PallbearerShrouded>())) {
                modifiers.FinalDamage *= MarkedDamageMult;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool marked = target.HasBuff(ModContent.BuffType<PallbearerShrouded>());

            //==== 每个被贯穿目标的独立打击链：顿帧 → 闷击 → 血爆 → 定向震屏 ====
            target.CWR().TimeFrozenTick = 4; //3~5 帧 hit-stop
            PallbearerVFX.NailThunk(Projectile.Center, marked);
            PallbearerVFX.Punch(Projectile.Center, Projectile.velocity, marked ? 4.5f : 3.4f, 8f, 8, 620f);
            PallbearerVFX.BloodBurst(Projectile.Center, Projectile.velocity
                , MathHelper.Clamp(Projectile.velocity.Length() / 26f, 0.4f, 1f), marked);

            //咬肉：穿过一个身体，轻微掉速
            Projectile.velocity *= 0.9f;

            //==== 血色烙印（owner 端计数，落棺触发）====
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            PallbearerNPC brands = target.GetGlobalNPC<PallbearerNPC>();
            int count = brands.AddBrand(target, Projectile.Center, Projectile.velocity.ToRotation());
            if (count < PallbearerNPC.MaxBrands) {
                return;
            }
            //三道烙印：落棺
            brands.ClearBrands();
            IEntitySource source = Main.player[Projectile.owner].HeldItem?.type == ModContent.ItemType<Pallbearer>()
                ? Main.player[Projectile.owner].GetSource_ItemUse(Main.player[Projectile.owner].HeldItem)
                : Projectile.GetSource_FromThis();
            Projectile.NewProjectile(source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<PallbearerCoffinSeal>()
                , (int)(Projectile.damage * 3f), 12f, Projectile.owner, target.whoAmI);
        }

        /// <summary>终点事件铺弹道光痕：出膛点→终点的三层驻留光束，弹体死后仍挂在空中缓缓熄灭</summary>
        private void SpawnTracer() {
            if (Main.dedServ || tracerSpawned || !birthSet) {
                return;
            }
            tracerSpawned = true;
            if (Vector2.DistanceSquared(birthPos, Projectile.Center) < 40f * 40f) {
                return; //贴脸命中不铺痕
            }
            PRTLoader.NewParticle<PRT_PallbearerTracer>(Projectile.Center, Vector2.Zero, default, 1f)
                ?.Configure(birthPos, Projectile.Center, 9f + 8f * ChargeLevel, 13);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //钉进地面：楔子插在土里短暂存留——把东西钉死在地上的家伙
            SpawnTracer();
            Mode = ModeStuckTile;
            Projectile.velocity = Vector2.Zero;
            Projectile.timeLeft = 300; //5 拍折算 ≈ 1s
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
            //飞行中死亡（穿透耗尽/超时）：此刻铺光痕；钉地模式早已铺过
            if (Mode == ModeFlight) {
                SpawnTracer();
            }
            //消隐：一点余烬与尘土
            PallbearerVFX.EmberBurst(Projectile.Center, 2, 1.6f, 0.6f);
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke
                    , Main.rand.NextVector2Circular(1.4f, 1.4f), 180, PallbearerVFX.CharDark, 1f);
                d.noGravity = true;
            }
        }

        //==== 绘制 ====

        public float GetWidthFunc(float completionRatio) =>
            (1f - completionRatio) * (14f + 8f * ChargeLevel); //completion 0 = 钉头端最宽

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
                //高速拉伸残影：把渲染帧之间 5 次 update 的空档糊上，弹体读作一道钉光而非跳帧的贴图
                Color ghost = PallbearerVFX.Blood with { A = 0 };
                Main.EntitySpriteDraw(texture, drawPos - Projectile.velocity * 1.4f - Main.rand.NextVector2Circular(0.5f, 0.5f)
                    , null, ghost * 0.38f, Projectile.rotation, origin
                    , new Vector2(0.86f, 1.5f) * Projectile.scale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(texture, drawPos - Projectile.velocity * 2.8f, null, ghost * 0.16f
                    , Projectile.rotation, origin, new Vector2(0.78f, 1.8f) * Projectile.scale, SpriteEffects.None, 0);

                //本体沿速度方向轻微拉伸：运动方向性
                Main.EntitySpriteDraw(texture, drawPos, null, bodyColor, Projectile.rotation, origin
                    , new Vector2(0.92f, 1.18f) * Projectile.scale, SpriteEffects.None, 0);
                return false;
            }

            //钉地模式：静止原样
            Main.EntitySpriteDraw(texture, drawPos, null, bodyColor,
                Projectile.rotation, origin, Projectile.scale * 0.92f, SpriteEffects.None, 0);
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Mode != ModeFlight) {
                return false;
            }
            //沿飞行方向的线段碰撞：钉是长条楔子
            Vector2 lineDirection = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Vector2 start = Projectile.Center - lineDirection * 16f;
            Vector2 end = Projectile.Center + lineDirection * 16f;
            float value = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 10f, ref value);
        }
    }

    /// <summary>
    /// 落棺封殓：同一目标累满三道血色烙印触发的残暴处决。焦黑棺影瞬现于目标身后，
    /// 血色锁链一拍收束、死寂半拍，棺盖猛然合拢一击结算大伤害。<br/>
    /// ai[0]=封殓目标 whoAmI。绘制走 behindNPCs 缓存：棺影浮在目标「身后」
    /// </summary>
    internal class PallbearerCoffinSeal : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<Pallbearer>()).DisplayName;

        private ref float BoundTarget => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        //==== 时间轴（60fps，总长 46 帧 ≈ 旧版 56%）====
        private const int RiseEnd = 12;       //棺影瞬现 & 锁链收束
        private const int SlamFrame = 16;     //12~16 死寂收缩 → 16 合盖
        private const int DissolveStart = 20;
        private const int TotalFrames = 46;

        /// <summary>封殓承伤 debuff 时长</summary>
        public const int SealedDuration = 300;
        /// <summary>落棺对被标记者的额外倍率</summary>
        public const float MarkedSealMult = 1.5f;

        private float coffinW = 120f;
        private float coffinH = 200f;
        private float seed;
        private bool slamDone;      //本端表现只放一次
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
            Projectile.localNPCHitCooldown = -1; //一具棺只合一次盖
            Projectile.hide = true; //只经 behindNPCs 缓存绘制（棺影在目标身后），不走常规弹幕层
        }

        /// <summary>棺影锚点：目标中心略上方（棺影比人高半头）</summary>
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
                //棺影按客人的身量定制
                if (target != null) {
                    coffinW = MathHelper.Clamp(target.width * 1.6f + 46f, 96f, 260f);
                    coffinH = MathHelper.Clamp(target.height * 1.7f + 96f, 170f, 430f);
                }
                seed = (Projectile.identity % 97) * 0.113f;
                //第一声钟：棺影破土
                PallbearerVFX.BellToll(Projectile.Center, 0.3f, 0.85f);
                SoundEngine.PlaySound(SoundID.DoorOpen with { Pitch = -0.55f, Volume = 0.7f }, Projectile.Center);
                PallbearerVFX.Punch(Projectile.Center, Vector2.UnitY, 4f, 9f, 8, 800f);
            }

            //锚定目标；目标消失则直接进入消散段
            if (target != null) {
                Projectile.Center = AnchorFor(target);
            }
            else if (Timer < DissolveStart) {
                Timer = DissolveStart;
            }

            //死寂段：链绷紧，世界屏息
            if (Timer >= RiseEnd && Timer < SlamFrame && target != null) {
                target.CWR().TimeFrozenTick = 2;
            }

            //==== 合盖帧 ====
            if (Timer == SlamFrame && !slamDone) {
                slamDone = true;
                DoSlamPresentation(target);
            }

            //浮现期：棺缝喷薄血色余烬；消散期：棺影碎作余烬散去
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

            //链影淡出：合盖前后链随之隐去
            if (Timer > RiseEnd) {
                chainFade = MathF.Max(0f, chainFade - 0.18f);
            }

            Lighting.AddLight(Projectile.Center, PallbearerVFX.Blood.ToVector3() * (Timer < SlamFrame ? 0.45f : 0.7f * GlowEnvelope()));
            Timer++;
        }

        private float GlowEnvelope() {
            return 1f - MathHelper.Clamp((Timer - DissolveStart) / (float)(TotalFrames - DissolveStart), 0f, 1f);
        }

        /// <summary>合盖单帧：重钟与闷响双层 + 强定向震屏 + 12 帧顿帧 + 血红冲击环（伤害由碰撞窗自动结算）</summary>
        private void DoSlamPresentation(NPC target) {
            Vector2 pos = Projectile.Center;
            PallbearerVFX.BellToll(pos, 1f, 1f);
            SoundEngine.PlaySound(SoundID.DoorClosed with { Pitch = -0.6f, Volume = 1f }, pos);
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.85f, Volume = 0.8f }, pos);
            PallbearerVFX.Punch(pos, Vector2.UnitY, 10f, 12f, 16, 950f);

            if (target != null) {
                target.CWR().TimeFrozenTick = 12; //合盖重顿帧
            }

            if (!Main.dedServ) {
                //血红冲击环 + 血橙瞬时爆闪 + 余烬迸发 + 焦黑碎屑
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

        //==== 伤害结算：只在合盖窗内、只对封殓目标 ====

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
            //封殓承伤（buff 走原版同步）
            target.AddBuff(ModContent.BuffType<PallbearerSealed>(), SealedDuration);
            //收殓标记被这口棺材兑现消耗——想再强化就再掷一次棺
            if (target.HasBuff(ModContent.BuffType<PallbearerShrouded>())) {
                target.RequestBuffRemoval(ModContent.BuffType<PallbearerShrouded>());
            }
        }

        //==== 绘制：棺影 quad（shader）+ 血色锁链，全部提交到 NPC 身后 ====

        public override bool PreDraw(ref Color lightColor) {
            float reveal = PallbearerVFX.EaseOutCubic(Timer / 9f);
            //瞬现落定曲线：快速过冲落定；死寂段收缩；合盖帧弹回
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
                //shader 未就绪的降级：暗影棺形剪影 + 血色轮廓
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

        /// <summary>血色锁链：三处烙印锚点 → 棺沿锚环，一拍收束绷紧后随合盖隐去</summary>
        private void DrawChains() {
            if (chainFade <= 0.01f || Timer < 2f) {
                return;
            }
            NPC target = Target;
            if (target == null) {
                return;
            }
            //收束进度：2~10 帧内从垂坠猛拉到绷直
            float taut = MathHelper.Clamp((Timer - 2f) / 8f, 0f, 1f);
            float t = Main.GlobalTimeWrappedHourly * 2.2f;
            for (int i = 0; i < 3; i++) {
                //锚点：identity 播种的伪随机体表点，各端一致
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
            //棺影浮现在目标身后
            behindNPCs.Add(index);
        }
    }

    /// <summary>收殓标记：被掷棺沾了棺椁气息。吃钉伤害 ×1.75，落棺伤害 ×1.5，且演出全面强化</summary>
    internal class PallbearerShrouded : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        /// <summary>标记存留时长（10s）</summary>
        public const int MarkDuration = 600;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            //棺椁气息缠身：暗红余烬明灭
            if (!Main.dedServ && Main.rand.NextBool(12)) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.55f, npc.height * 0.55f);
                PRTLoader.NewParticle<PRT_PallbearerEmber>(pos, new Vector2(0f, Main.rand.NextFloat(-0.7f, 0.3f))
                    , PallbearerVFX.BloodDeep, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(18, 0.02f);
            }
        }
    }

    /// <summary>封殓：落棺合盖后残留的棺椁压迫，承受伤害提高</summary>
    internal class PallbearerSealed : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        /// <summary>封殓承伤倍率</summary>
        public const float DamageTakenMult = 1.10f;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }
    }

    /// <summary>
    /// 抬棺人烙印簿：每个 NPC 一份实例。棺钉每贯穿一次留下一道血色烙印刻痕
    /// （小面积锐利烧灼，随时间冷却消退），每道烙印 +5% 承伤；封殓 debuff 期间再 +10%。<br/>
    /// 烙印数据在武器持有者端生成与结算（伤害由命中方客户端计算，无同步需求）
    /// </summary>
    internal class PallbearerNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>触发落棺所需烙印数</summary>
        public const int MaxBrands = 3;
        /// <summary>每道烙印的承伤加成</summary>
        public const float DamageTakenPerBrand = 0.05f;
        /// <summary>烙印冷却消退时长（自最后一道起）</summary>
        public const int BrandDecayTime = 420;

        /// <summary>当前烙印数 0..3</summary>
        public int BrandCount { get; private set; }
        private int brandDecay;
        //延迟分配：InstancePerEntity 会给每个 NPC 建实例，只有真被烙印的目标才付数组开销
        private Vector2[] brandOffsets;
        private float[] brandRots;
        private float[] brandAges;

        /// <summary>刻下一道烙印：位置取命中点贴体表，走向沿钉入方向。返回当前烙印数</summary>
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

        /// <summary>清空烙印（落棺兑现）</summary>
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
                //刚烙下的几帧：更大更亮（烧灼瞬间），随后定格为锐利刻痕
                float flare = MathF.Max(0f, 1f - brandAges[i] / 9f);
                float s = 1f + flare * 0.55f;
                float pulse = 0.82f + 0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + i * 2.1f);
                //焦黑灼底（正常混合压暗皮肉）
                spriteBatch.Draw(streak, pos, null, PallbearerVFX.CharDark * 0.72f, rot, origin
                    , new Vector2(0.3f, 0.8f) * s, SpriteEffects.None, 0f);
                //血色刻痕芯（加色）
                Color core = PallbearerVFX.Blood with { A = 0 };
                spriteBatch.Draw(streak, pos, null, core * ((0.75f + flare * 0.6f) * pulse), rot, origin
                    , new Vector2(0.15f, 0.62f) * s, SpriteEffects.None, 0f);
                spriteBatch.Draw(streak, pos, null, core * (0.5f * pulse), rot, origin
                    , new Vector2(0.08f, 0.45f) * s, SpriteEffects.None, 0f);
            }
        }
    }
}
