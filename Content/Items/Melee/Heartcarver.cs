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
    /// <summary>
    /// 刻心者短剑：这把刀很钝，不是用来割肉的。<br/>
    /// 手持时以 ~72bpm 心跳节拍运转（<see cref="HeartcarverPlayer"/>），
    /// 攻击落在两次心跳之间的间隙窗口构成「剜心击」；
    /// 终结斩以剜心击命中会把目标的心脏整个剜出来（<see cref="HeartcarverExcisedHeart"/>）
    /// </summary>
    internal class Heartcarver : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "Heartcarver";

        /// <summary>剜心机制提示文本</summary>
        public static LocalizedText CarveTip { get; private set; }

        /// 四段连击计数(三连刺+终结斩)
        private int comboCounter;
        /// 连击重置计时，过久回第一段
        private int comboResetTimer;

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
            CarveTip = this.GetLocalization(nameof(CarveTip), () => @"手持时，刀会带着你的心一起跳
攻击落在两次心跳之间的间隙，即构成剜心击
以剜心击完成终结斩，能把心脏完整地剜出来");
        }

        public override void SetDefaults() {
            Item.width = 52;
            Item.height = 52;
            Item.damage = 1666;
            Item.DamageType = DamageClass.Generic;
            Item.useAnimation = 14;
            Item.useTime = 14;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Rapier;
            Item.knockBack = 5.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<HeartcarverHeld>();
            Item.shootSpeed = 2.4f;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 30, 0, 0);
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
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
            TooltipLine tipLine = new(Mod, "CarveTip", CarveTip.Value) {
                OverrideColor = HeartcarverPalette.Heat(0.4f)
            };
            tooltips.Add(tipLine);

            if (HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestReward, d => d.SupCalDoGQuestReward)) {
                TooltipLine line = new(Mod, "Story", SupCalDisplayText.Story2.Value);
                line.OverrideColor = Color.OrangeRed;
                tooltips.Add(line);
            }
        }

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                if (player.CountProjectilesOfID<HeartcarverDash>() > 0
                    || player.GetModPlayer<HeartcarverPlayer>().DashCooldown > 0) {
                    return false;
                }
            }
            return true;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void HoldItem(Player player) {
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                comboCounter = 0;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity.SafeNormalize(Vector2.Zero),
                    ModContent.ProjectileType<HeartcarverDash>(), (int)(damage * 1.5f), knockback * 2f, player.whoAmI);
                comboCounter = 0;//冲刺重置连击
                return false;
            }

            int combo = comboCounter % 4;
            comboCounter++;
            comboResetTimer = 60;
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, combo);
            return false;
        }
    }

    /// <summary>
    /// 刻心者手持弹幕：三连刺+终结斩。<br/>
    /// 刺击开始的一帧对照心跳窗口判定剜心击（ai[1]，拥有者判定后同步）；
    /// 剜心击强制暴击并附加伤害，终结斩剜心击直接剜出心脏
    /// </summary>
    internal class HeartcarverHeld : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "Heartcarver";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<Heartcarver>();

        /// 连击索引 0~2刺 3终结斩
        private ref float ComboIndex => ref Projectile.ai[0];
        /// 剜心击标记：拥有者在刺出瞬间判定，>0.5 为剜心击
        private ref float CarveFlag => ref Projectile.ai[1];

        private bool IsFinisher => ComboIndex >= 3f;
        private bool IsCarve => CarveFlag > 0.5f;

        //阶段时长(逻辑帧，攻速缩放)
        private float WindupTime => IsFinisher ? 8f : 3f;
        private float StabTime => IsFinisher ? 6f : 5f;
        private float RecoverTime => IsFinisher ? 8f : 6f;
        private float TotalTime => WindupTime + StabTime + RecoverTime;
        //刺击顶点的突出距离，逐刺递增
        private float StabReach => IsFinisher ? 52f : 32f + ComboIndex * 2f;
        //刀刃判定长度
        private const float BladeLength = 46f;

        private float elapsed;
        private float speedMul = 1f;
        private Vector2 stabUnit;
        /// 当前持出距离
        private float holdout;
        private bool strikeStarted;
        /// 剜心击命中顿帧：冻结 elapsed 数帧，把力钉在目标里
        private int hitstopFrames;
        //刺线快照：刺出瞬间世界锚定，不随收刀回缩
        private Vector2 lanceOrigin;
        private Vector2 lanceDir;
        private readonly HashSet<int> hitNPCs = [];

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Generic;
            Projectile.width = Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 60;
            Projectile.ArmorPenetration = 32767;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => elapsed >= WindupTime && elapsed <= WindupTime + StabTime + 1f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            Vector2 tip = hand + stabUnit * (holdout + BladeLength) * Projectile.scale;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , hand, tip, 26f, ref collisionPoint);
        }

        public override void Initialize() {
            stabUnit = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            Owner.direction = Math.Sign(stabUnit.X) == 0 ? Owner.direction : Math.Sign(stabUnit.X);

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }

            if (IsFinisher) {
                Projectile.damage = (int)(Projectile.damage * 1.25f);
                Projectile.scale = 1.15f;
            }
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<Heartcarver>()) {
                Projectile.Kill();
                return;
            }

            //剜心击顿帧：整套动作冻结，只维持姿态
            if (hitstopFrames > 0) {
                hitstopFrames--;
                SetDirection();
                UpdatePlayerPose();
                return;
            }

            if (elapsed >= TotalTime) {
                Projectile.Kill();
                return;
            }

            //右键冲刺，避免被左键硬控
            if (DownRight && Owner.CountProjectilesOfID<HeartcarverDash>() == 0
                && Owner.GetModPlayer<HeartcarverPlayer>().DashCooldown <= 0 && Projectile.IsOwnedByLocalPlayer()) {
                ShootState shootState = Owner.GetShootState();
                Projectile.NewProjectile(shootState.Source, Owner.Center, stabUnit,
                    ModContent.ProjectileType<HeartcarverDash>(), (int)(shootState.WeaponDamage * 1.5f)
                    , shootState.WeaponKnockback * 2f, Owner.whoAmI);
                Projectile.Kill();
                return;
            }

            float stabEnd = WindupTime + StabTime;

            if (elapsed < WindupTime) {
                //回拉蓄力：终结斩用高次幂后拉，绷到最后一刻
                float t = elapsed / WindupTime;
                holdout = IsFinisher
                    ? MathHelper.Lerp(10f, -16f, MathF.Pow(t, 3f))
                    : MathHelper.Lerp(10f, -8f, MathF.Sin(t * MathHelper.PiOver2));

                //终结斩蓄力：血线向持握点收束；72% 处硬切静默，给爆发留一口气
                if (IsFinisher && !VaultUtils.isServer && elapsed < WindupTime * 0.72f && elapsed % 2f < speedMul) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(70f, 130f);
                    Vector2 spawnPos = Owner.Center + angle.ToRotationVector2() * dist;
                    Vector2 vel = (Owner.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(6f, 11f);
                    PRTLoader.NewParticle<PRT_Line>(spawnPos, vel,
                        HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 2f))
                        ?.Configure(false, Main.rand.Next(8, 14));
                }
            }
            else if (elapsed < stabEnd) {
                //迅捷突刺：高次幂 ease-out，头几帧吃掉几乎全部行程
                float t = (elapsed - WindupTime) / StabTime;
                float eased = 1f - MathF.Pow(1f - t, IsFinisher ? 8f : 4.5f);
                holdout = MathHelper.Lerp(IsFinisher ? -16f : -8f, StabReach, eased);

                if (!strikeStarted) {
                    strikeStarted = true;
                    OnStrikeStart();
                }
            }
            else {
                //收刀
                float t = (elapsed - stabEnd) / RecoverTime;
                holdout = MathHelper.Lerp(StabReach, 12f, t * t * (3f - 2f * t));
            }

            SetDirection();
            UpdatePlayerPose();
            elapsed += speedMul;
        }

        /// <summary>刺出的一帧：对照心跳窗口判定剜心击，锚定刺线快照，铺刺击音</summary>
        private void OnStrikeStart() {
            if (Projectile.IsOwnedByLocalPlayer() && Owner.GetModPlayer<HeartcarverPlayer>().JudgeCarve()) {
                CarveFlag = 1f;
                Projectile.netUpdate = true;
            }

            lanceOrigin = Owner.GetPlayerStabilityCenter();
            lanceDir = stabUnit;

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with {
                Pitch = ComboIndex * 0.15f,
                Volume = 0.9f + ComboIndex * 0.1f
            }, Projectile.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.35f, Volume = 0.6f }, Projectile.Center);
            }
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (stabUnit * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, stabUnit.ToRotation() - MathHelper.PiOver2);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + stabUnit * (holdout + BladeLength * 0.5f);
            Projectile.rotation = stabUnit.ToRotation();
            Projectile.timeLeft = 60;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = stabUnit.X > 0 ? 1 : -1;
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.425f;
            }
            //剜心击：落在心跳间隙的刀必定命中要害
            if (IsCarve) {
                modifiers.SetCrit();
                modifiers.FinalDamage *= 1.25f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //转发物品命中钩子，维持装备与饰品的近战联动
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            target.AddBuff(BuffID.Bleeding, 180 + (int)ComboIndex * 60);

            if (IsCarve && Projectile.numHits <= 1) {
                OnCarveHit(target);
            }
            else if (IsFinisher && Projectile.numHits <= 1 && !VaultUtils.isServer) {
                //普通终结斩命中反馈
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.8f, Pitch = 0.4f }, target.Center);
                SpawnBloodBurst(target.Center, 8, 6f);
            }
        }

        /// <summary>剜心击命中：顿帧+后座+定向震屏+动脉放射；终结斩则把心脏剜出来</summary>
        private void OnCarveHit(NPC target) {
            hitstopFrames = IsFinisher ? 4 : 2;

            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.GetModPlayer<HeartcarverPlayer>().NotifyCarveStrike(target.whoAmI);
                //施力者后座：力作用是相互的
                Owner.velocity -= stabUnit * (IsFinisher ? 3f : 1.2f);
            }

            bool extracted = TryExtractHeart(target);

            if (VaultUtils.isServer) {
                return;
            }

            //剜心击命中音：湿的、卡进拍子里的
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.9f, Pitch = -0.25f }, target.Center);
            SoundEngine.PlaySound(SoundID.DrumTamaSnare with { Volume = 0.5f, Pitch = -0.5f }, target.Center);

            if (CWRServerConfig.Instance.ScreenVibration) {
                PunchCameraModifier modifier = new(target.Center, stabUnit,
                    extracted ? 8f : 5f, extracted ? 7f : 5f, extracted ? 12 : 8, 800f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }

            //动脉裂纹放射：命中点的血线爆发
            int lineCount = extracted ? 12 : 7;
            for (int i = 0; i < lineCount; i++) {
                float ang = MathHelper.TwoPi * i / lineCount + Main.rand.NextFloat(-0.2f, 0.2f);
                PRTLoader.NewParticle<PRT_Line>(target.Center, ang.ToRotationVector2() * Main.rand.NextFloat(6f, 13f),
                    HeartcarverPalette.Heat(Main.rand.NextFloat(0.4f)), Main.rand.NextFloat(1.4f, 2.4f))
                    ?.Configure(false, Main.rand.Next(10, 18));
            }
            SpawnBloodBurst(target.Center, extracted ? 18 : 10, extracted ? 11f : 8f);
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(target.Center, Vector2.Zero,
                HeartcarverPalette.Arterial, 1f)?.Configure(0.08f, extracted ? 0.9f : 0.5f, 16);
        }

        /// <summary>终结斩剜心击：从目标身上剜出心脏实体；剜出的一帧配红黑 impact frame</summary>
        private bool TryExtractHeart(NPC target) {
            if (!IsFinisher || target.friendly || target.dontTakeDamage) {
                return false;
            }
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<HeartcarverExcisedHeart>()] >= 2) {
                return false;
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                //心脏携带干净的武器面板伤害（不吃终结 1.25x 乘区），脉冲/血刃再按各自系数取
                int cleanDamage = Owner.GetWeaponDamage(Item);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center,
                    new Vector2(-stabUnit.X * 2f, -7f),
                    ModContent.ProjectileType<HeartcarverExcisedHeart>(), cleanDamage, 2f, Owner.whoAmI);
            }

            if (!VaultUtils.isServer) {
                //剜出的一帧：嘴的第一声尖叫 + 红黑高对比 impact frame（限频）
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.75f, Volume = 0.55f }, target.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.5f, Volume = 0.8f }, target.Center);
                if (Owner.whoAmI == Main.myPlayer) {
                    HeartcarverImpactRender.Trigger(0.9f);
                }
            }
            return true;
        }

        /// <summary>液态血珠爆发（数量∝动能）</summary>
        private static void SpawnBloodBurst(Vector2 pos, int count, float speed) {
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(speed * 0.4f, speed);
                vel.Y -= 1.2f;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel,
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(1f, 1.9f))
                    ?.Configure(Main.rand.Next(22, 38));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            //贴图刀尖指向右上，刺击时沿刺击方向旋转
            float rot = Projectile.rotation + MathHelper.PiOver4;
            SpriteEffects effect = SpriteEffects.None;
            if (Owner.direction < 0) {
                rot += MathHelper.PiOver2;
                effect = SpriteEffects.FlipHorizontally;
            }

            //刀身本体
            Vector2 drawPos = hand + stabUnit * holdout - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, rot, origin, Projectile.scale, effect, 0);

            //心跳脉动辉光：刀身随 lub-dub 呼吸的动脉红（武器本地的节拍广播，替代屏幕红晕）
            HeartcarverPlayer hcPlayer = Owner.GetModPlayer<HeartcarverPlayer>();
            float beatGlow = hcPlayer.BeatEnvelope * 0.38f + (hcPlayer.FrenzyTimer > 0 ? 0.10f : 0f);
            if (beatGlow > 0.03f) {
                Color pulse = HeartcarverPalette.Arterial with { A = 0 } * beatGlow;
                Main.EntitySpriteDraw(tex, drawPos, null, pulse, rot, origin, Projectile.scale * 1.07f, effect, 0);
            }
            //间隙窗口开启：刀刃泛白热一瞬（瞬时小面积粉白）
            if (hcPlayer.WindowFlash > 0.05f) {
                Color windowHot = HeartcarverPalette.Myocard with { A = 0 } * (hcPlayer.WindowFlash * 0.4f);
                Main.EntitySpriteDraw(tex, drawPos, null, windowHot, rot, origin, Projectile.scale * 1.02f, effect, 0);
            }

            //剜心击/终结斩的辉光层：动脉红打底，剜心击再覆一层心肌粉白
            if (IsFinisher || IsCarve) {
                Color glow = HeartcarverPalette.Arterial with { A = 0 } * 0.5f;
                Main.EntitySpriteDraw(tex, drawPos, null, glow, rot, origin, Projectile.scale * 1.08f, effect, 0);
            }
            if (IsCarve) {
                Color hot = HeartcarverPalette.Myocard with { A = 0 } * 0.35f;
                Main.EntitySpriteDraw(tex, drawPos, null, hot, rot, origin, Projectile.scale * 1.03f, effect, 0);
            }
            return false;
        }

        /// <summary>针状白热刺线：刺出瞬间世界锚定的静态 quad 光束，替代 sprite 残影</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (!strikeStarted || elapsed < WindupTime) {
                return;
            }
            Effect effect = HeartcarverAssets.HeartcarverLance?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            float lanceT = MathHelper.Clamp((elapsed - WindupTime) / (TotalTime - WindupTime), 0f, 1f);
            float reach = (StabReach + BladeLength + (IsFinisher ? 48f : 30f)) * Projectile.scale;
            Vector2 origin = lanceOrigin - lanceDir * 8f;
            Vector2 tip = lanceOrigin + lanceDir * reach;
            Vector2 perp = lanceDir.RotatedBy(MathHelper.PiOver2);
            float halfW = IsFinisher ? 30f : 20f;

            //uv.x: 0=手部端 → 1=针尖端；uv.y: 0~1 横截面
            var quad = new VertexPositionColorTexture[4];
            quad[0] = new VertexPositionColorTexture((origin + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            quad[1] = new VertexPositionColorTexture((origin - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));
            quad[2] = new VertexPositionColorTexture((tip + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            quad[3] = new VertexPositionColorTexture((tip - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uLife"]?.SetValue(lanceT);
            effect.Parameters["uCarve"]?.SetValue(IsCarve ? 1f : 0f);
            effect.Parameters["uSeed"]?.SetValue(ComboIndex * 0.37f + Projectile.whoAmI * 0.11f % 1f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// <summary>
    /// 刻心者冲刺突击：counter-motion 反向后拉 → 一帧满速贝塞尔弧线前冲 → 回刺 → 收势。<br/>
    /// 拖尾为 shader 条带图元；每次命中独立对照心跳窗口判定剜心击
    /// </summary>
    internal class HeartcarverDash : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "Heartcarver";

        //冲刺：反向后拉+前冲+回刺+收势
        private const int PreludeDuration = 4;
        private const int ForwardDuration = 16;
        private const int ReturnDuration = 10;
        private const int SettleDuration = 8;
        private const int TotalDuration = PreludeDuration + ForwardDuration + ReturnDuration + SettleDuration;
        private const float DashSpeed = 38f;
        /// <summary>反向后拉的最大距离</summary>
        private const float ReelBack = 26f;

        private Vector2 dashDirection;
        private Vector2 dashStartPos;
        private Vector2 dashPeakPos;
        private Vector2 dashPerpendicularDir;
        private Vector2 lastCenter;
        private int hitCount;
        private int dashTimer;
        private float arcIntensity;
        private bool launched;
        private bool actionEnded;
        private bool lastHitWasCarve;

        //条带拖尾缓存：0 = 最新
        private const int TrailMax = 34;
        private readonly Vector2[] trailPos = new Vector2[TrailMax];
        private readonly Vector2[] trailPerp = new Vector2[TrailMax];
        private int trailCount;

        private bool InPrelude => dashTimer < PreludeDuration;
        private bool InForward => dashTimer >= PreludeDuration && dashTimer < PreludeDuration + ForwardDuration;
        private bool InReturn => dashTimer >= PreludeDuration + ForwardDuration
            && dashTimer < PreludeDuration + ForwardDuration + ReturnDuration;
        private bool InSettle => dashTimer >= PreludeDuration + ForwardDuration + ReturnDuration;

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Generic;
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalDuration + 10;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
            Projectile.ArmorPenetration = 32767;
        }

        public override bool? CanDamage() => (InForward || InReturn) ? null : false;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (Projectile.localAI[0] == 0f) {
                InitializePrelude();
                Projectile.localAI[0] = 1f;
            }

            lastCenter = Owner.Center;

            if (InPrelude) {
                UpdatePreludeMotion();
            }
            else if (InForward || InReturn) {
                if (!launched) {
                    launched = true;
                    LaunchDash();
                }

                if (InForward) {
                    UpdateForwardMovement();
                    if (dashTimer == PreludeDuration + ForwardDuration - 1) {
                        BeginReturn();
                    }
                }
                else {
                    UpdateReturnMovement();
                }

                PushTrailSample();
                UpdateVisualEffects();
                Owner.GivePlayerImmuneState(36);
            }
            else if (!actionEnded) {
                //回刺落点：动作结束，收势期玩家恢复行动，条带自然渐隐
                EndDashAction();
            }

            Projectile.Center = Owner.Center;
            if (!InSettle) {
                Owner.direction = Projectile.direction = Math.Sign(dashDirection.X) == 0 ? 1 : Math.Sign(dashDirection.X);
                Owner.heldProj = Projectile.whoAmI;
                Owner.itemTime = 2;
                Owner.itemAnimation = 2;
            }

            dashTimer++;
            if (dashTimer >= TotalDuration) {
                Projectile.Kill();
            }
        }

        private void InitializePrelude() {
            dashDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            dashStartPos = Owner.Center;
            dashPerpendicularDir = new Vector2(-dashDirection.Y, dashDirection.X);
            arcIntensity = Main.rand.NextBool() ? 1f : -1f;
            Projectile.rotation = dashDirection.ToRotation()
                + (Projectile.direction > 0 ? MathHelper.PiOver4 : -MathHelper.Pi - MathHelper.PiOver4);

            if (VaultUtils.isServer) {
                return;
            }
            //吸气：后拉阶段的短促蓄势音
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.8f }, Owner.Center);
        }

        /// <summary>counter-motion：pow(t,8) 后拉——静止…静止…猛地向后一吸</summary>
        private void UpdatePreludeMotion() {
            float t = (dashTimer + 1) / (float)PreludeDuration;
            float reel = MathF.Pow(t, 8f) * ReelBack;
            Owner.velocity = Vector2.Zero;
            Owner.Center = dashStartPos - dashDirection * reel;

            //收束血线：能量向持刀者汇聚
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 2; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 spawnPos = Owner.Center + ang.ToRotationVector2() * Main.rand.NextFloat(60f, 120f);
                    PRTLoader.NewParticle<PRT_Line>(spawnPos,
                        (Owner.Center - spawnPos) * 0.16f,
                        HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(1f, 1.7f))
                        ?.Configure(false, Main.rand.Next(6, 10));
                }
            }
        }

        /// <summary>发射帧：速度一帧设满、震屏、爆发粒子全部压在同一帧</summary>
        private void LaunchDash() {
            dashStartPos = Owner.Center;

            if (CWRServerConfig.Instance.ScreenVibration) {
                PunchCameraModifier modifier = new(Owner.Center, dashDirection, 6f, 7f, 10, 600f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.8f, Pitch = -0.2f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = 0.1f }, Owner.Center);
            SpawnDashBurst();
        }

        private void UpdateForwardMovement() {
            float t = (dashTimer - PreludeDuration + 1) / (float)ForwardDuration;
            //一帧满速：高次幂 ease-out，首帧即吃掉大半行程
            float easedT = 1f - MathF.Pow(1f - t, 4.2f);

            //贝塞尔控制点：起点 → 弧形偏移控制点 → 终点
            Vector2 endPos = dashStartPos + dashDirection * DashSpeed * ForwardDuration * 0.6f;
            Vector2 controlPoint = dashStartPos + dashDirection * DashSpeed * ForwardDuration * 0.3f
                + dashPerpendicularDir * 80f * arcIntensity;

            //二阶贝塞尔曲线
            Vector2 targetPos = Vector2.Lerp(
                Vector2.Lerp(dashStartPos, controlPoint, easedT),
                Vector2.Lerp(controlPoint, endPos, easedT),
                easedT
            );

            Projectile.velocity = targetPos - Owner.Center;
            Owner.Center = targetPos;

            //匕首始终朝向运动方向
            if (Projectile.velocity.LengthSquared() > 0.01f) {
                float targetRot = Projectile.velocity.ToRotation();
                Projectile.rotation = targetRot + (Projectile.direction > 0 ? MathHelper.PiOver4 : -MathHelper.Pi - MathHelper.PiOver4);
            }
        }

        private void BeginReturn() {
            dashPeakPos = Owner.Center;

            if (VaultUtils.isServer) {
                return;
            }
            //回刺瞬间的强力音效
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.8f, Pitch = 0.5f }, Owner.Center);
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Owner.Center,
                    angle.ToRotationVector2() * Main.rand.NextFloat(5f, 10f),
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 2f))
                    ?.Configure(Main.rand.Next(18, 28));
            }
        }

        private void UpdateReturnMovement() {
            float t = (dashTimer - PreludeDuration - ForwardDuration + 1) / (float)ReturnDuration;
            float easedT = t * t * (3f - 2f * t);

            Owner.Center = Vector2.Lerp(dashPeakPos, dashStartPos, easedT);
            Projectile.velocity = Owner.Center - lastCenter;

            Vector2 returnDir = (dashStartPos - dashPeakPos).SafeNormalize(Vector2.UnitX);
            Projectile.rotation = returnDir.ToRotation() + (Projectile.direction > 0 ? MathHelper.PiOver4 : -MathHelper.Pi - MathHelper.PiOver4);
        }

        /// <summary>把当前位置推入条带缓存（0 为最新）</summary>
        private void PushTrailSample() {
            Vector2 move = Owner.Center - lastCenter;
            Vector2 perp = move.LengthSquared() > 0.01f
                ? move.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2)
                : (trailCount > 0 ? trailPerp[0] : dashPerpendicularDir);

            for (int i = Math.Min(trailCount, TrailMax - 1); i > 0; i--) {
                trailPos[i] = trailPos[i - 1];
                trailPerp[i] = trailPerp[i - 1];
            }
            trailPos[0] = Owner.Center;
            trailPerp[0] = perp;
            if (trailCount < TrailMax) {
                trailCount++;
            }
        }

        private void UpdateVisualEffects() {
            if (VaultUtils.isServer || InSettle) {
                return;
            }
            //高速运动的液滴尾迹
            if (dashTimer % 2 == 0) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Owner.Center + Main.rand.NextVector2Circular(18f, 18f),
                    -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 5f),
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(1f, 1.6f))
                    ?.Configure(Main.rand.Next(14, 24));
            }
            if (dashTimer % 3 == 0) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(Owner.Center + Main.rand.NextVector2Circular(15f, 15f),
                    -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 5f),
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 2f))
                    ?.Configure(false, Main.rand.Next(8, 15));
            }

            Lighting.AddLight(Owner.Center, HeartcarverPalette.Arterial.ToVector3() * 1.2f);
        }

        /// <summary>发射帧爆发：定向血珠锥 + 脉冲环 + 反向气流线</summary>
        private void SpawnDashBurst() {
            for (int i = 0; i < 16; i++) {
                float spread = Main.rand.NextFloat(-0.7f, 0.7f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Owner.Center,
                    dashDirection.RotatedBy(spread) * Main.rand.NextFloat(7f, 15f),
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(1.3f, 2.2f))
                    ?.Configure(Main.rand.Next(18, 30));
            }
            for (int i = 0; i < 8; i++) {
                float spread = Main.rand.NextFloat(-0.5f, 0.5f);
                PRTLoader.NewParticle<PRT_Line>(Owner.Center,
                    (-dashDirection).RotatedBy(spread) * Main.rand.NextFloat(4f, 9f),
                    HeartcarverPalette.ArterialDeep, Main.rand.NextFloat(1.2f, 2f))
                    ?.Configure(false, Main.rand.Next(8, 14));
            }
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(Owner.Center, Vector2.Zero,
                HeartcarverPalette.Arterial, 1f)?.Configure(0.1f, 0.7f, 14);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.425f;
            }
            //冲刺的每次命中独立对照心跳窗口
            lastHitWasCarve = Projectile.IsOwnedByLocalPlayer()
                && Owner.GetModPlayer<HeartcarverPlayer>().JudgeCarve();
            if (lastHitWasCarve) {
                modifiers.SetCrit();
                modifiers.FinalDamage *= 1.25f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Bleeding, 300);
            hitCount++;

            if (lastHitWasCarve && Projectile.IsOwnedByLocalPlayer()) {
                Owner.GetModPlayer<HeartcarverPlayer>().NotifyCarveStrike(target.whoAmI);
            }

            if (VaultUtils.isServer) {
                return;
            }

            SoundEngine.PlaySound(SoundID.NPCHit18 with {
                Volume = 0.7f,
                Pitch = 0.3f + (InReturn ? 0.2f : 0f) + (lastHitWasCarve ? -0.4f : 0f)
            }, target.Center);
            if (lastHitWasCarve) {
                SoundEngine.PlaySound(SoundID.DrumTamaSnare with { Volume = 0.45f, Pitch = -0.5f }, target.Center);
            }

            //血液飞溅(冲刺方向)
            Vector2 hitDir = (target.Center - Owner.Center).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 12; i++) {
                float spread = Main.rand.NextFloat(-0.9f, 0.9f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(target.Center,
                    hitDir.RotatedBy(spread) * Main.rand.NextFloat(4f, 11f),
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 2.2f))
                    ?.Configure(Main.rand.Next(18, 30));
            }

            //回刺命中时额外屏幕震动
            if (InReturn && CWRServerConfig.Instance.ScreenVibration) {
                PunchCameraModifier modifier = new(target.Center, hitDir, 4f, 5f, 8, 500f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }

            if (hitCount % 3 == 0) {
                SpawnDashBurst();
            }
        }

        /// <summary>动作收尾：速度阻尼、冷却记账（专属 ModPlayer，替代旧的隐形冷却弹幕）、收束粒子</summary>
        private void EndDashAction() {
            actionEnded = true;
            Owner.velocity *= 0.3f;
            Owner.GetModPlayer<HeartcarverPlayer>().DashCooldown = 120;

            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 14; i++) {
                float angle = MathHelper.TwoPi * i / 14f;
                float dist = Main.rand.NextFloat(40f, 80f);
                Vector2 startPos = Owner.Center + angle.ToRotationVector2() * dist;
                PRTLoader.NewParticle<PRT_SparkAlpha>(startPos,
                    (Owner.Center - startPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 8f),
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(1f, 1.7f))
                    ?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override void OnKill(int timeLeft) {
            //提前死亡（如玩家倒地）也要保证冷却入账
            if (!actionEnded) {
                EndDashAction();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //收势期只留条带渐隐，不再绘制刀身
            if (InSettle) {
                return false;
            }
            Texture2D texture = TextureValue;
            Vector2 drawPos = Owner.GetPlayerStabilityCenter() - Main.screenPosition;
            Rectangle sourceRect = texture.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;
            SpriteEffects spriteEffects = Projectile.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //后拉阶段：刀随身体后收，压低姿态
            Vector2 bladeOffset = InPrelude
                ? -dashDirection * 6f
                : Vector2.Zero;

            //主体
            Main.spriteBatch.Draw(
                texture, drawPos + bladeOffset, sourceRect, lightColor,
                Projectile.rotation, origin, Projectile.scale, spriteEffects, 0
            );

            //动脉红辉光：随速度增强，静止时收敛
            float speedGate = MathHelper.Clamp(Projectile.velocity.Length() / 30f, 0f, 1f);
            if (speedGate > 0.05f) {
                Color glowColor = HeartcarverPalette.Arterial with { A = 0 } * (0.55f * speedGate);
                Main.spriteBatch.Draw(
                    texture, drawPos + bladeOffset, sourceRect, glowColor,
                    Projectile.rotation, origin, Projectile.scale * 1.08f, spriteEffects, 0
                );
            }

            return false;
        }

        /// <summary>血刃条带拖尾：宽度沿尾递减的 TriangleStrip，替代 sprite 残影</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailCount < 3) {
                return;
            }
            Effect effect = HeartcarverAssets.HeartcarverRibbon?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            //收势期整体渐隐
            float fade = InSettle
                ? 1f - (dashTimer - (TotalDuration - SettleDuration)) / (float)SettleDuration
                : 1f;
            if (fade <= 0.02f) {
                return;
            }

            var bars = new VertexPositionColorTexture[trailCount * 2];
            for (int i = 0; i < trailCount; i++) {
                float along = i / (float)(trailCount - 1);
                float halfW = MathHelper.Lerp(24f, 4f, along);
                //uv.x: 0=最新头部 → 1=尾端；uv.y: 0~1 横截面
                bars[i * 2] = new VertexPositionColorTexture((trailPos[i] + trailPerp[i] * halfW).ToVector3()
                    , Color.White, new Vector2(along, 0f));
                bars[i * 2 + 1] = new VertexPositionColorTexture((trailPos[i] - trailPerp[i] * halfW).ToVector3()
                    , Color.White, new Vector2(along, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(fade);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// <summary>
    /// 环绕血刃：由吸收的心脏凝成（因果链：剜出→吸收→成刃）。<br/>
    /// 平时贴着拥有者心跳同步搏动环绕；拥有者打出剜心击时，全部血刃循声扑向伤口
    /// </summary>
    internal class HeartcarverDagger : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "Heartcarver";

        private enum DaggerState
        {
            /// <summary>自吸收点凝成，飞入轨道</summary>
            Coalesce,
            /// <summary>心跳同步环绕</summary>
            Orbit,
            /// <summary>循剜心声扑向目标</summary>
            Strike,
            /// <summary>化血消散</summary>
            Dissolve
        }

        private DaggerState State {
            get => (DaggerState)Projectile.ai[1];
            set => Projectile.ai[1] = (float)value;
        }

        private ref float DaggerIndex => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[2];

        //环绕椭圆轨道（倾斜伪3D）
        private const float OrbitRadiusX = 96f;
        private const float OrbitRadiusY = 52f;
        private float orbitAngle;
        private float orbitSpeed;
        private float orbitTilt;
        /// <summary>心跳踢速：lub 拍瞬间加速，随后衰减</summary>
        private float beatKick;

        private const int CoalesceDuration = 14;
        private const float StrikeSpeed = 30f;

        private float glowIntensity;
        private float daggerScale = 1f;
        private float dissolveFade = 1f;
        /// <summary>拥有者端：已消费的剜心击信号戳</summary>
        private int seenSignalStamp = -1;
        /// <summary>拥有者端：齐射错拍倒计时</summary>
        private int strikeArmCountdown = -1;
        private int targetWho = -1;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Generic;
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 600;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.ArmorPenetration = 32767;
            Projectile.CWR().HitAttribute.WormResistance = 0.4f;
        }

        public override bool? CanDamage() => State == DaggerState.Strike ? null : false;

        public override void Initialize() {
            //吞掉生成之前遗留的信号，血刃只响应成刃之后的剜心击
            seenSignalStamp = Owner.GetModPlayer<HeartcarverPlayer>().CarveSignalStamp;
            orbitTilt = MathHelper.PiOver4 + DaggerIndex * 0.4f;
            orbitAngle = MathHelper.TwoPi * DaggerIndex / 3f;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            HeartcarverPlayer hcPlayer = Owner.GetModPlayer<HeartcarverPlayer>();

            //收刀不持械：血刃失去心跳，化血消散
            if (State != DaggerState.Dissolve && State != DaggerState.Strike && !hcPlayer.HoldingHeartcarver) {
                EnterDissolve();
            }

            StateTimer++;

            switch (State) {
                case DaggerState.Coalesce:
                    CoalescePhase();
                    break;
                case DaggerState.Orbit:
                    OrbitPhase(hcPlayer);
                    break;
                case DaggerState.Strike:
                    StrikePhase();
                    break;
                case DaggerState.Dissolve:
                    DissolvePhase();
                    break;
            }

            float lightIntensity = glowIntensity * 0.8f * dissolveFade;
            Lighting.AddLight(Projectile.Center, HeartcarverPalette.Arterial.ToVector3() * lightIntensity);
        }

        //椭圆轨道(倾斜伪3D)
        private Vector2 GetEllipseOrbitPos(Vector2 center, float angle, float radiusX, float radiusY, float tilt) {
            float x = MathF.Cos(angle) * radiusX;
            float y = MathF.Sin(angle) * radiusY;
            float tiltedY = y * MathF.Cos(tilt);
            return center + new Vector2(x, tiltedY).RotatedBy(tilt * 0.3f);
        }

        //基于椭圆位置计算伪深度缩放
        private float GetDepthScale(float angle, float tilt) {
            float depth = MathF.Sin(angle) * MathF.Sin(tilt);
            return MathHelper.Lerp(0.7f, 1.3f, (depth + 1f) * 0.5f);
        }

        /// <summary>凝成：从吸收点弹性飞入轨道槽位</summary>
        private void CoalescePhase() {
            float progress = MathHelper.Clamp(StateTimer / CoalesceDuration, 0f, 1f);
            float ease = 1f - MathF.Pow(1f - progress, 3f);

            Vector2 slotPos = GetEllipseOrbitPos(Owner.Center, orbitAngle, OrbitRadiusX, OrbitRadiusY, orbitTilt);
            Projectile.Center = Vector2.Lerp(Projectile.Center, slotPos, ease * 0.5f);
            Projectile.rotation = (slotPos - Projectile.Center).SafeNormalize(Vector2.UnitX).ToRotation() + MathHelper.PiOver4;

            glowIntensity = MathHelper.Lerp(0.2f, 0.6f, progress);
            daggerScale = MathHelper.Lerp(0.4f, 1f, ease);

            //凝血尾迹
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                    Main.rand.NextVector2Circular(1.5f, 1.5f),
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.4f))
                    ?.Configure(Main.rand.Next(14, 22));
            }

            if (StateTimer >= CoalesceDuration) {
                State = DaggerState.Orbit;
                StateTimer = 0;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.4f, Pitch = 0.35f + DaggerIndex * 0.1f }, Projectile.Center);
                }
            }
        }

        /// <summary>环绕：轨道速度与辉光同步拥有者的心跳；监听剜心击信号错拍齐射</summary>
        private void OrbitPhase(HeartcarverPlayer hcPlayer) {
            Projectile.timeLeft = 600;

            //心跳踢速：lub 拍瞬间提速，随后指数回落——血刃与心同频
            if (hcPlayer.BeatPhase == HeartcarverPlayer.LubPhase) {
                beatKick = 1f;
            }
            beatKick *= 0.93f;

            orbitSpeed = 0.055f + beatKick * 0.11f;
            orbitAngle += orbitSpeed;

            float breathe = beatKick * 9f;
            Vector2 targetPos = GetEllipseOrbitPos(Owner.Center, orbitAngle,
                OrbitRadiusX + breathe, OrbitRadiusY + breathe * 0.5f, orbitTilt);
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.35f);

            //伪3D深度缩放 + 心跳胀缩
            daggerScale = GetDepthScale(orbitAngle, orbitTilt) * (1f + beatKick * 0.12f);
            glowIntensity = 0.4f + hcPlayer.BeatEnvelope * 0.5f;

            Vector2 tangent = new Vector2(-MathF.Sin(orbitAngle), MathF.Cos(orbitAngle));
            Projectile.rotation = tangent.ToRotation() + MathHelper.PiOver4;

            //搏动瞬间的血珠
            if (!VaultUtils.isServer && beatKick > 0.9f) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                    tangent * 2f, HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.3f))
                    ?.Configure(Main.rand.Next(12, 20));
            }

            //拥有者端：感知新的剜心击信号，按刃序错拍出击
            if (Projectile.IsOwnedByLocalPlayer()) {
                if (seenSignalStamp != hcPlayer.CarveSignalStamp) {
                    seenSignalStamp = hcPlayer.CarveSignalStamp;
                    targetWho = hcPlayer.CarveSignalNpc;
                    strikeArmCountdown = 2 + (int)DaggerIndex * 4;
                }
                if (strikeArmCountdown > 0 && --strikeArmCountdown == 0) {
                    LaunchStrike();
                }
            }
        }

        /// <summary>循声出击：预判目标落点，一帧满速射出（拥有者端触发后同步）</summary>
        private void LaunchStrike() {
            Vector2 launchDir;
            if (targetWho >= 0 && targetWho < Main.maxNPCs && Main.npc[targetWho].active) {
                NPC target = Main.npc[targetWho];
                float travelTime = Vector2.Distance(Projectile.Center, target.Center) / StrikeSpeed;
                launchDir = (target.Center + target.velocity * travelTime * 0.5f - Projectile.Center).SafeNormalize(Vector2.UnitX);
            }
            else {
                launchDir = (InMousePos - Projectile.Center).SafeNormalize(Vector2.UnitX);
            }

            Projectile.velocity = launchDir * StrikeSpeed;
            State = DaggerState.Strike;
            StateTimer = 0;
            Projectile.netUpdate = true;

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.7f, Pitch = 0.4f }, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Line>(Projectile.Center,
                        launchDir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(4f, 9f),
                        HeartcarverPalette.Heat(Main.rand.NextFloat(0.4f)), Main.rand.NextFloat(1.2f, 2f))
                        ?.Configure(false, Main.rand.Next(8, 14));
                }
            }
        }

        private void StrikePhase() {
            //强追踪伤口
            NPC target = null;
            if (targetWho >= 0 && targetWho < Main.maxNPCs && Main.npc[targetWho].active) {
                target = Main.npc[targetWho];
            }
            target ??= Projectile.Center.FindClosestNPC(900);

            if (target != null && Projectile.numHits == 0) {
                Projectile.SmoothHomingBehavior(target.Center, 1.04f, 0.14f);
            }

            float speed = Projectile.velocity.Length();
            if (speed < StrikeSpeed * 0.75f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * StrikeSpeed * 0.75f;
            }

            glowIntensity = 0.95f;
            daggerScale = 1.1f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -Projectile.velocity * Main.rand.NextFloat(0.06f, 0.15f),
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.3f))
                    ?.Configure(Main.rand.Next(12, 20));
            }

            if (StateTimer > 80) {
                EnterDissolve();
            }
        }

        private void EnterDissolve() {
            State = DaggerState.Dissolve;
            StateTimer = 0;
        }

        /// <summary>失去心跳支撑：凝血重新化开</summary>
        private void DissolvePhase() {
            dissolveFade = 1f - MathHelper.Clamp(StateTimer / 18f, 0f, 1f);
            Projectile.velocity *= 0.9f;
            daggerScale *= 0.97f;

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.6f)),
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(0.9f, 1.5f))
                    ?.Configure(Main.rand.Next(16, 26));
            }

            if (StateTimer >= 18) {
                Projectile.Kill();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Bleeding, 240);

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);

            Vector2 hitDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(target.Center,
                    hitDir.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(3f, 9f),
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(1f, 1.8f))
                    ?.Configure(Main.rand.Next(18, 28));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(target.Center, Main.rand.NextVector2Circular(7f, 7f),
                    HeartcarverPalette.Heat(Main.rand.NextFloat(0.3f)), Main.rand.NextFloat(1f, 1.7f))
                    ?.Configure(false, Main.rand.Next(10, 16));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                    Main.rand.NextVector2Circular(4f, 4f),
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(0.9f, 1.6f))
                    ?.Configure(Main.rand.Next(16, 26));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D daggerTex = TextureValue;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = daggerTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;

            SpriteEffects spriteEffects = Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float rot = Projectile.rotation + (Projectile.velocity.X > 0 ? 0 : MathHelper.PiOver2);

            float alpha = dissolveFade;
            float drawScale = Projectile.scale * daggerScale;

            //出击残影
            if (State == DaggerState.Strike) {
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float prog = 1f - i / (float)Projectile.oldPos.Length;
                    Color afterColor = HeartcarverPalette.Heat(prog * 0.5f) with { A = 0 } * (prog * 0.5f * alpha);
                    Vector2 afterPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    float afterRot = (Projectile.oldRot.Length > i ? Projectile.oldRot[i] : Projectile.rotation)
                        + (Projectile.velocity.X > 0 ? 0 : MathHelper.PiOver2);
                    sb.Draw(daggerTex, afterPos, sourceRect, afterColor, afterRot, origin,
                        drawScale * MathHelper.Lerp(0.8f, 1f, prog), spriteEffects, 0);
                }
            }

            //心跳同步光晕
            if (glowIntensity > 0.1f) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Color glowColor = HeartcarverPalette.Arterial with { A = 0 } * (glowIntensity * 0.55f * alpha);
                sb.Draw(glow, drawPos, null, glowColor, 0f, glow.Size() / 2f,
                    drawScale * (0.7f + glowIntensity * 0.5f), spriteEffects, 0f);
            }

            //主体
            sb.Draw(daggerTex, drawPos, sourceRect, lightColor * alpha, rot, origin, drawScale, spriteEffects, 0);

            //出击白热层
            if (State == DaggerState.Strike) {
                Color hot = HeartcarverPalette.Myocard with { A = 0 } * (0.4f * alpha);
                sb.Draw(daggerTex, drawPos, sourceRect, hot, rot, origin, drawScale * 1.05f, spriteEffects, 0);
            }

            return false;
        }
    }
}
