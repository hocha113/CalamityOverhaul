using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class NeutronGlaive : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "NeutronGlaive";

        /// <summary>三段连击计数，决定下一次挥砍的招式</summary>
        private int comboCounter;
        /// <summary>连击重置计时器，过久未挥砍则回到第一段</summary>
        private int comboResetTimer;

        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 16));
        }

        public override void SetDefaults() {
            Item.height = 154;
            Item.width = 154;
            Item.damage = 855;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 13;
            Item.scale = 1;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 7.5f;
            Item.UseSound = SoundID.Item60;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(13, 53, 75, 0);
            Item.rare = ItemRarityID.Red;
            Item.crit = 8;
            Item.shoot = ModContent.ProjectileType<NeutronGlaiveHeld>();
            Item.shootSpeed = 18f;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_NeutronGlaive;
            //noMelee 武器需要手动允许近战词缀
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override bool CanUseItem(Player player) {
            Item.UseSound = SoundID.Item60;
            if (player.altFunctionUse == 2) {
                Item.UseSound = SoundID.AbigailAttack;
            }
            return player.ownedProjectileCounts[ModContent.ProjectileType<NeutronGlaiveHeldAlt>()] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<NeutronGlaiveHeld>()] == 0;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override void HoldItem(Player player) {
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                comboCounter = 0;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<NeutronGlaiveHeldAlt>(), damage, knockback, player.whoAmI);
                comboCounter = 0;//中子洪流重置连击
                return false;
            }

            int combo = comboCounter % 3;
            float swingDir = comboCounter % 2 == 0 ? 1f : -1f;
            comboCounter++;
            comboResetTimer = 60;
            Projectile.NewProjectile(source, player.Center, velocity, type
                , damage, knockback, player.whoAmI, combo, swingDir);
            return false;
        }
    }

    /// <summary>
    /// 中子偃月刀手持弹幕
    /// <br/>三段连击: 横扫 → 反手回扫 → 回环重劈，挥砍中段射出中子光束，首次命中引发中子爆轰
    /// <br/>刀光由 NeutronSlashTrail.fx 渲染，回环重劈时拖出星河
    /// </summary>
    internal class NeutronGlaiveHeld : BaseHeldProj, IWarpDrawable, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "NeutronGlaive";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<NeutronGlaive>();

        private const int FrameCount = 16;

        /// <summary>连击索引: 0=横扫 1=反手回扫 2=回环重劈</summary>
        private ref float ComboIndex => ref Projectile.ai[0];
        /// <summary>挥砍方向符号 ±1</summary>
        private ref float SwingDirAi => ref Projectile.ai[1];

        private bool IsFinisher => ComboIndex >= 2f;

        //阶段时长（逻辑帧，受攻速缩放）
        private float WindupTime => IsFinisher ? 10f : 7f;
        private float SlashTime => IsFinisher ? 18f : 13f;
        private float RecoverTime => 8f;
        private float TotalTime => WindupTime + SlashTime + RecoverTime;
        //挥砍弧度：终结技近乎一整圈的回环
        private float SwingArc => IsFinisher ? 5.9f : 3.6f;
        //刀尖距离持握点的长度
        private float BladeReach => IsFinisher ? 180f : 165f;

        private static readonly Color NeutronViolet = new(138, 80, 255);
        private static readonly Color NeutronBlue = new(120, 180, 255);

        private float elapsed;
        private float speedMul = 1f;
        private int lockedDirection = 1;
        private int swingSign = 1;
        private float startAngle;
        private float endAngle;
        private float currentRotation;
        private float lastRotation;
        private bool slashSoundPlayed;
        private bool beamFired;
        private float trailFade;
        private readonly HashSet<int> hitNPCs = [];

        //刀光轨迹缓存：每逻辑帧细分采样以保证弧光平滑
        private const int TrailMax = 64;
        private const int TrailSubdiv = 4;
        private readonly float[] trailRot = new float[TrailMax];
        private int trailCount;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 66;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 90;
            Projectile.scale = 1.45f;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => elapsed >= WindupTime && elapsed <= WindupTime + SlashTime + 1f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            Vector2 tip = hand + currentRotation.ToRotationVector2() * BladeReach;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , hand, tip, 52f, ref collisionPoint);
        }

        public override void Initialize() {
            swingSign = Math.Sign(SwingDirAi);
            if (swingSign == 0) {
                swingSign = 1;
            }

            lockedDirection = Math.Sign(Projectile.velocity.X);
            if (lockedDirection == 0) {
                lockedDirection = Owner.direction;
            }
            Owner.direction = lockedDirection;

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
                Projectile.scale = 1.55f;
            }
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<NeutronGlaive>()) {
                Projectile.Kill();
                return;
            }
            if (elapsed >= TotalTime) {
                Projectile.Kill();
                return;
            }

            lastRotation = currentRotation;
            float slashEnd = WindupTime + SlashTime;

            if (elapsed < WindupTime) {
                //长柄武器的大幅度蓄力回拉
                float t = elapsed / WindupTime;
                currentRotation = startAngle - swingSign * 0.3f * MathF.Sin(t * MathHelper.PiOver2);
                trailFade = 0f;
            }
            else if (elapsed < slashEnd) {
                //ease-out 重斩
                float t = (elapsed - WindupTime) / SlashTime;
                float eased = 1f - MathF.Pow(1f - t, IsFinisher ? 4.4f : 3.6f);
                currentRotation = MathHelper.Lerp(startAngle, endAngle, eased);
                trailFade = 1f;
                PushTrailSamples();

                if (!slashSoundPlayed) {
                    slashSoundPlayed = true;
                    if (!VaultUtils.isServer && IsFinisher) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.85f, Pitch = -0.3f }, Owner.Center);
                    }
                }

                if (!beamFired && t >= 0.3f) {
                    beamFired = true;
                    FireBeam();
                }

                //刃锋星屑
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 along = Owner.GetPlayerStabilityCenter()
                        + currentRotation.ToRotationVector2() * Main.rand.NextFloat(BladeReach * 0.55f, BladeReach);
                    Vector2 tangent = currentRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(along, tangent * Main.rand.NextFloat(1.5f, 4f)
                        , Color.Lerp(NeutronViolet, NeutronBlue, Main.rand.NextFloat()), Main.rand.NextFloat(0.25f, 0.4f)).Configure(false, 14);
                }
            }
            else {
                //收势：刀停住，弧光收缩渐隐
                float t = (elapsed - slashEnd) / RecoverTime;
                currentRotation = endAngle;
                trailFade = 1f - t;
                PushTrailSamples();
            }

            UpdatePlayerPose();
            VaultUtils.ClockFrame(ref Projectile.frame, 5, FrameCount - 1);
            Lighting.AddLight(Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.7f
                , NeutronViolet.ToVector3() * 0.7f);
            elapsed += speedMul;
        }

        private void PushTrailSamples() {
            for (int s = TrailSubdiv - 1; s >= 0; s--) {
                float rot = MathHelper.Lerp(currentRotation, lastRotation, s / (float)TrailSubdiv);
                for (int i = Math.Min(trailCount, TrailMax - 1); i > 0; i--) {
                    trailRot[i] = trailRot[i - 1];
                }
                trailRot[0] = rot;
                if (trailCount < TrailMax) {
                    trailCount++;
                }
            }
        }

        private void FireBeam() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 spawnPos = Owner.GetPlayerStabilityCenter() + UnitToMouseV * BladeReach * 0.5f;
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), spawnPos, UnitToMouseV * Item.shootSpeed
                , ModContent.ProjectileType<NeutronGlaiveBeam>(), Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.5f;
            Projectile.timeLeft = 90;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = currentRotation.ToRotationVector2().X > 0 ? 1 : -1;
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.425f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //转发物品命中钩子，维持装备与饰品的近战联动
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            if (Projectile.numHits == 0 && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), target.Center, Vector2.Zero
                    , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage / 2, 0, Owner.whoAmI);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Projectile.numHits == 0 && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), target.Center, Vector2.Zero
                    , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage / 2, 0, Owner.whoAmI);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //本体在 DrawCustom 的扭曲豁免层绘制，这里只画挥砍残影
            if (CanDamage() != true) {
                return false;
            }
            Texture2D tex = TextureValue;
            Rectangle rect = tex.GetRectangle(Projectile.frame, FrameCount);
            Vector2 origin = rect.Size() / 2f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float dist = BladeReach * 0.5f;
            SpriteEffects effect = lockedDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            float rotOffset = lockedDirection == -1 ? -MathHelper.PiOver4 : MathHelper.PiOver4;

            for (int i = 1; i <= 3; i++) {
                float rot = MathHelper.Lerp(currentRotation, lastRotation, i / 4f);
                Vector2 pos = hand + rot.ToRotationVector2() * dist - Main.screenPosition;
                Color trailColor = NeutronViolet * (0.32f * (1f - i / 4f));
                trailColor.A = 0;
                Main.EntitySpriteDraw(tex, pos, rect, trailColor, rot + rotOffset, origin
                    , Projectile.scale, effect, 0);
            }
            return false;
        }

        bool IWarpDrawable.CanDrawCustom() => true;

        bool IWarpDrawable.DontUseBlueshiftEffect() => true;

        void IWarpDrawable.Warp() {
            //挥砍期间沿刃锋路径绘制热扭曲
            if (CanDamage() != true) {
                return;
            }
            Texture2D warpTex = CWRUtils.GetT2DValue(CWRConstant.Masking + "DiffusionCircle");
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            Color warpColor = new Color(45, 45, 45) * 0.5f;
            for (int i = 0; i < 4; i++) {
                float rot = MathHelper.Lerp(currentRotation, lastRotation, i / 4f);
                Vector2 pos = hand + rot.ToRotationVector2() * BladeReach * 0.75f - Main.screenPosition;
                Main.spriteBatch.Draw(warpTex, pos, null, warpColor, rot
                    , warpTex.Size() / 2, 0.32f, SpriteEffects.None, 0f);
            }
        }

        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) {
            Texture2D tex = TextureValue;
            Rectangle rect = tex.GetRectangle(Projectile.frame, FrameCount);
            Vector2 origin = rect.Size() / 2f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float dist = BladeReach * 0.5f;
            SpriteEffects effect = lockedDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            float rotOffset = lockedDirection == -1 ? -MathHelper.PiOver4 : MathHelper.PiOver4;

            Vector2 drawPos = hand + currentRotation.ToRotationVector2() * dist - Main.screenPosition;
            spriteBatch.Draw(tex, drawPos, rect, Color.White, currentRotation + rotOffset, origin
                , Projectile.scale, effect, 0);

            //终结回环的能量辉光层
            if (IsFinisher && CanDamage() == true) {
                Color glow = NeutronBlue * 0.4f;
                glow.A = 0;
                spriteBatch.Draw(tex, drawPos, rect, glow, currentRotation + rotOffset, origin
                    , Projectile.scale * 1.04f, effect, 0);
            }
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailCount < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.NeutronSlashTrail?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 center = Owner.GetPlayerStabilityCenter();
            float outer = BladeReach + 14f;
            float inner = BladeReach * 0.28f;
            for (int i = 0; i < trailCount; i++) {
                float factor = 1f - i / (float)trailCount;
                Vector2 dir = trailRot[i].ToRotationVector2();
                bars[i * 2] = new VertexPositionColorTexture((center + dir * outer).ToVector3()
                    , Color.White, new Vector2(factor, 0f));
                bars[i * 2 + 1] = new VertexPositionColorTexture((center + dir * inner).ToVector3()
                    , Color.White, new Vector2(factor, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(trailFade);
            effect.Parameters["uHeat"]?.SetValue(IsFinisher ? 1f : 0.4f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    internal class NeutronGlaiveBeam : ModProjectile, IWarpDrawable, ICWRLoader
    {
        public override string Texture => CWRConstant.Projectile_Melee + "NeutronGlaiveBeam";

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 120;
            Projectile.MaxUpdates = 3;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Color.White.ToVector3() * 0.3f);

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            Projectile.ai[0] += 0.05f;
            if (Projectile.ai[0] > 0.3f) {
                Projectile.ai[0] = 0.3f;
            }
            if (Projectile.timeLeft > 15) {
                Projectile.localAI[0] += 0.15f;
                if (Projectile.localAI[0] > 0.3f) {
                    Projectile.localAI[0] = 0.3f;
                }
                Projectile.ai[1] += 0.2f;
                if (Projectile.ai[1] > 0.3f) {
                    Projectile.ai[1] = 0.3f;
                }
            }
            else {
                Projectile.localAI[0] -= 0.03f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;

            float rot = Main.rand.NextFloat(6.282f);
            for (int i = 0; i < 2; i++) {
                Vector2 dir = rot.ToRotationVector2();
                Vector2 vel = dir.RotatedBy(1.57f) * Main.rand.NextFloat(1.3f, 2.5f) + Projectile.velocity;
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.Next(3, 10)
                    , DustID.Granite, vel, Scale: Main.rand.NextFloat(1.4f, 1.6f));
                dust.noGravity = true;

                rot = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            if (++Projectile.localAI[2] > 2) {
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 3; j++) {
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vr * (0.1f + i * 0.14f), Color.BlueViolet, Main.rand.NextFloat(0.2f, 0.3f)).Configure(false, 17);
                    }
                }
                Projectile.localAI[2] = 0;
            }
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(300, SoundID.Item14 with { Pitch = 0.45f });
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 randpos = VaultUtils.RandVr(64);
            Projectile.Center += randpos;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero
                , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage, 0);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity * -0.6f;
            for (int j = 0; j < 73; j++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + oldVelocity, oldVelocity.RotatedByRandom(0.3f) * -Main.rand.NextFloat(0.3f, 1.1f), Color.LightBlue, Main.rand.NextFloat(0.5f, 0.7f)).Configure(false, 7);
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) => false;
        bool IWarpDrawable.CanDrawCustom() => true;
        void IWarpDrawable.Warp() {
            float scale = System.Math.Max(Projectile.localAI[0], 0.01f);
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: 200f * scale,
                screenHeight: 200f * scale,
                intensity: Projectile.ai[1] * 0.65f,
                progress: Projectile.ai[1],
                rotation: Projectile.ai[0],
                technique: "GravitationalLens",
                radius: 0.4f
            );
        }

        public void DrawCustom(SpriteBatch spriteBatch) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle rectangle = mainValue.GetRectangle();
            Vector2 orig = rectangle.Size() / 2;
            float rot = Projectile.rotation;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 offsetPos = Projectile.oldPos[k].To(Projectile.position);
                Vector2 drawPos2 = drawPos - offsetPos;
                Color color = Projectile.GetAlpha(Color.Pink) * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(mainValue, drawPos2, rectangle, color, rot, orig, Projectile.scale, SpriteEffects.None, 0);
            }

            VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, mainValue, Projectile.timeLeft, drawPos, rectangle, Color.Blue, rot, orig, Projectile.scale, 0);
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, rectangle
                , Color.White, Projectile.rotation, orig, Projectile.scale, SpriteEffects.None, 0);
        }
    }

    internal class NeutronGlaiveHeldAlt : BaseHeldProj, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Melee + "NeutronGlaive";
        private static Asset<Texture2D> bar1;
        private static Asset<Texture2D> bar2;
        private static Asset<Texture2D> bar3;
        private static Asset<Texture2D> bar4;
        private bool canatcck;
        private bool canatcck2 = true;
        private bool canatcck3 = true;
        private int uiframe;
        private const int maxatcck = 80;
        void ICWRLoader.SetupData() {
            if (Main.dedServ) {
                return;
            }
            bar1 = CWRUtils.GetT2DAsset(CWRConstant.UI + "NeutronsBar");
            bar2 = CWRUtils.GetT2DAsset(CWRConstant.UI + "NeutronsBar2");
            bar3 = CWRUtils.GetT2DAsset(CWRConstant.UI + "NeutronsBarTop");
            bar4 = CWRUtils.GetT2DAsset(CWRConstant.UI + "NeutronsBarTop2");
        }
        void ICWRLoader.UnLoadData() {
            bar1 = null;
            bar2 = null;
            bar3 = null;
            bar4 = null;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 112;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 4;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.hide = true;
        }

        public override void AI() {
            if (Owner.dead || !Owner.active || canatcck || !DownRight) {
                canatcck = true;
                if (Projectile.ai[0] >= maxatcck) {
                    Projectile.Kill();
                }
                else {
                    canatcck2 = false;
                    Projectile.scale = 1.25f;

                    if (++Projectile.ai[1] > 5) {
                        SoundEngine.PlaySound(SoundID.Item4, Projectile.Center);
                        Vector2 pos = Projectile.Center + Projectile.velocity.UnitVector() * Main.rand.Next(-52, 112);
                        int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), pos
                        , Projectile.velocity.RotatedByRandom(0.2f), ModContent.ProjectileType<NeutronsOrb>(), Projectile.damage, 0);
                        Main.projectile[proj].SetAllProjectilesHome(true);
                        for (int i = 0; i < 4; i++) {
                            float rot1 = MathHelper.PiOver2 * i;
                            Vector2 vr = rot1.ToRotationVector2();
                            for (int j = 0; j < 13; j++) {
                                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, vr * (0.1f + j * 0.14f), Color.BlueViolet, Main.rand.NextFloat(0.5f, 0.7f)).Configure(false, 17);
                            }
                        }
                        Projectile.ai[1] = 0;
                    }

                    Projectile.ai[0]--;
                    if (Projectile.ai[0] <= 0) {
                        Projectile.Kill();
                    }
                }
            }
            if (canatcck2) {
                Projectile.velocity = ToMouse.UnitVector() * 18;
            }
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.velocity.UnitVector() * 40 * Projectile.scale;
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!canatcck && Projectile.ai[0] <= maxatcck) {
                Projectile.ai[0]++;
            }
            if (Projectile.ai[0] >= maxatcck) {
                if (canatcck3) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f }, Projectile.Center);
                    canatcck3 = false;
                }
                Projectile.scale = 1.5f;
            }
            SetHeld();
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 15);
            if (canatcck2) {
                VaultUtils.ClockFrame(ref uiframe, 5, 6);
            }
            float rot = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rot * -DirSign);
            Owner.direction = Math.Sign(Projectile.velocity.X);
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer() && canatcck2) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + Projectile.velocity.UnitVector() * 255
                    , Vector2.Zero, ModContent.ProjectileType<EXNeutronExplode>(), Projectile.damage * 10, 0);
            }
        }

        public static void DrawBar(Player Owner, float sengs, int uiframe) {
            sengs = MathHelper.Clamp(sengs, 0, maxatcck);
            if (!(sengs <= 0f)) {
                Texture2D barBG = bar3.Value;
                Texture2D barFG = bar1.Value;
                if (sengs >= maxatcck) {
                    barBG = bar4.Value;
                    barFG = bar2.Value;
                }
                float barScale = 1.2f;
                Vector2 drawPos = Owner.GetPlayerStabilityCenter() + new Vector2(0, 90) - Main.screenPosition;
                Rectangle frameCrop = new Rectangle(0, 0, (int)(sengs / maxatcck * barFG.Width), barFG.Height);
                Color color = Color.White;
                Main.spriteBatch.Draw(barBG, drawPos, barBG.GetRectangle(uiframe, 7), color, 0f, VaultUtils.GetOrig(barBG, 7), barScale, 0, 0f);
                Main.spriteBatch.Draw(barFG, drawPos + new Vector2(2, 4), frameCrop, color, 0f, VaultUtils.GetOrig(barFG, 1), barScale, 0, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawBar(Owner, Projectile.ai[0], uiframe);
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, value.GetRectangle(Projectile.frame, 16)
                , Color.White, Projectile.rotation + MathHelper.PiOver4 * Owner.direction, VaultUtils.GetOrig(value, 16) + new Vector2(0, 5 * Owner.direction)
                , Projectile.scale, Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0);
            return false;
        }
    }

    internal class NeutronsOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.timeLeft = 120;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, Projectile.velocity, Color.BlueViolet, Main.rand.NextFloat(0.2f, 0.3f)).Configure(false, 17);
        }
    }

    internal class NeutronExplode : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 200;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 4;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
        }

        public bool CanDrawCustom() => false;

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 133; j++) {
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vr * (0.1f + i * 0.24f), Color.BlueViolet, Main.rand.NextFloat(1.2f, 2.3f)).Configure(false, 7);
                    }
                }
                Projectile.ai[2]++;
            }
            Projectile.ai[0] += 0.25f;
            if (Projectile.timeLeft > 15) {
                Projectile.localAI[0] += 0.25f;
                Projectile.ai[1] += 0.2f;
            }
            else {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);

            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            float scale = Math.Max(Projectile.localAI[0], 0.01f);
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: 400f * scale,
                screenHeight: 400f * scale,
                intensity: Projectile.ai[1] * 0.85f,
                progress: Projectile.ai[1],
                rotation: Projectile.ai[0],
                technique: "GravitationalVortex"
            );
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }

    internal class EXNeutronExplode : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 2000;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 4;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.DamageType = EndlessDamageClass.Instance;
        }

        public bool CanDrawCustom() => false;

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                SoundEngine.PlaySound(CWRSound.Pecharge with { Pitch = -0.1f, Volume = 0.8f }, Projectile.Center);
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 133; j++) {
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vr * (0.1f + j * 0.34f), Color.BlueViolet, Main.rand.NextFloat(2.2f, 2.3f)).Configure(false, 7);
                    }
                }
            }
            if (Projectile.ai[2] % 6 == 0) {
                float randvalue = Main.rand.NextFloat(MathHelper.TwoPi);
                float randvalue2 = Main.rand.NextFloat(0.3f, 1.6f);
                for (int z = 0; z < 4; z++) {
                    Vector2 rand = (MathHelper.PiOver2 * z + randvalue).ToRotationVector2() * 130 * randvalue2;
                    for (int i = 0; i < 4; i++) {
                        float rot1 = MathHelper.PiOver2 * i;
                        Vector2 vr = rot1.ToRotationVector2();
                        for (int j = 0; j < 33; j++) {
                            PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + rand, vr * 0.24f, Color.CadetBlue, Main.rand.NextFloat(0.9f, 1.3f)).Configure(false, 13);
                        }
                    }
                }
            }
            Projectile.ai[0] += 0.25f;
            if (Projectile.timeLeft > 15) {
                Projectile.localAI[0] += 0.25f;
                Projectile.ai[1] += 0.2f;
            }
            else {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);
            Projectile.ai[2]++;
            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            float scale = Math.Max(Projectile.localAI[0], 0.01f);
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: 1200f * scale,
                screenHeight: 1200f * scale,
                intensity: Projectile.ai[1] * 1.0f,
                progress: Projectile.ai[1],
                rotation: Projectile.ai[0],
                technique: "GravitationalVortex",
                radius: 0.48f
            );
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }
}
