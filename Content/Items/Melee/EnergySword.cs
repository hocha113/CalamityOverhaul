using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class EnergySword : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "EnergySword";

        /// <summary>三段连击计数，决定下一次挥砍的招式</summary>
        private int comboCounter;
        /// <summary>连击重置计时器，过久未挥砍则回到第一段</summary>
        private int comboResetTimer;

        public override void SetDefaults() {
            Item.height = 44;
            Item.width = 44;
            Item.damage = 12;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 20;
            Item.scale = 1;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 2.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(0, 0, 75, 0);
            Item.rare = ItemRarityID.Green;
            Item.shoot = ModContent.ProjectileType<EnergySwordHeld>();
            Item.shootSpeed = 12f;
            Item.SetItemUsesCharge(true);
            Item.SetItemMaxCharge(40);
            //noMelee 武器需要手动允许近战词缀
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override bool CanUseItem(Player player) {
            float charge = Item.GetItemCharge() - 0.12f;
            if (charge < 0) {
                charge = 0;
            }
            Item.SetItemCharge(charge);
            return player.ownedProjectileCounts[Item.shoot] == 0;
        }

        public override void HoldItem(Player player) {
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                comboCounter = 0;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int combo = comboCounter % 3;
            float swingDir = comboCounter % 2 == 0 ? 1f : -1f;
            comboCounter++;
            comboResetTimer = 75;
            Projectile.NewProjectile(source, player.Center, velocity, type
                , damage, knockback, player.whoAmI, combo, swingDir);
            return false;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(CWRID.Item_DubiousPlating, 5).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 4).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 2).
                AddRecipeGroup(CWRCrafted.GoldBarGroup, 2).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.TinBarGroup, 2).
                AddRecipeGroup(CWRCrafted.GoldBarGroup, 2).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    /// <summary>
    /// 能量剑手持弹幕
    /// <br/>三段连击: 正手斩 → 反手斩 → 能量重斩，挥砍中段若充能足够则射出能量光束
    /// <br/>刀光由 EnergySlashTrail.fx 渲染，亮度随充能变化
    /// </summary>
    internal class EnergySwordHeld : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "EnergySword";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<EnergySword>();

        /// <summary>连击索引: 0=正手斩 1=反手斩 2=能量重斩</summary>
        private ref float ComboIndex => ref Projectile.ai[0];
        /// <summary>挥砍方向符号 ±1</summary>
        private ref float SwingDirAi => ref Projectile.ai[1];

        private bool IsFinisher => ComboIndex >= 2f;

        //阶段时长（逻辑帧，受攻速缩放）
        private float WindupTime => IsFinisher ? 6f : 4f;
        private float SlashTime => IsFinisher ? 12f : 9f;
        private float RecoverTime => 6f;
        private float TotalTime => WindupTime + SlashTime + RecoverTime;
        //挥砍弧度
        private float SwingArc => IsFinisher ? 4.4f : 3.1f;
        //刀尖距离持握点的长度
        private float BladeReach => IsFinisher ? 98f : 86f;

        private static readonly Color EnergyRed = new(255, 84, 60);
        private static readonly Color EnergyHot = new(255, 206, 160);

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

        //刀光轨迹缓存，每逻辑帧细分采样
        private const int TrailMax = 48;
        private const int TrailSubdiv = 4;
        private readonly float[] trailRot = new float[TrailMax];
        private int trailCount;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 60;
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
            Vector2 tip = hand + currentRotation.ToRotationVector2() * BladeReach * Projectile.scale;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , hand, tip, 30f, ref collisionPoint);
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
                Projectile.damage = (int)(Projectile.damage * 1.25f);
                Projectile.scale = 1.1f;
            }
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<EnergySword>()) {
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
                //蓄力回拉
                float t = elapsed / WindupTime;
                currentRotation = startAngle - swingSign * 0.2f * MathF.Sin(t * MathHelper.PiOver2);
                trailFade = 0f;
            }
            else if (elapsed < slashEnd) {
                //ease-out 斩击
                float t = (elapsed - WindupTime) / SlashTime;
                float eased = 1f - MathF.Pow(1f - t, IsFinisher ? 4f : 3.2f);
                currentRotation = MathHelper.Lerp(startAngle, endAngle, eased);
                trailFade = 1f;
                PushTrailSamples();

                if (!slashSoundPlayed) {
                    slashSoundPlayed = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item1 with {
                            Pitch = 0.15f + ComboIndex * 0.12f
                        }, Owner.Center);
                        if (IsFinisher && HasCharge) {
                            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.5f, Pitch = 0.4f }, Owner.Center);
                        }
                    }
                }

                if (!beamFired && t >= 0.32f) {
                    beamFired = true;
                    FireBeam();
                }

                //刀刃能量粒子
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 along = Owner.GetPlayerStabilityCenter()
                        + currentRotation.ToRotationVector2() * Main.rand.NextFloat(BladeReach * 0.5f, BladeReach);
                    Vector2 tangent = currentRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
                    PRTLoader.NewParticle<PRT_Spark>(along, tangent * Main.rand.NextFloat(2f, 5f)
                        , Color.Lerp(EnergyRed, EnergyHot, Main.rand.NextFloat()), Main.rand.NextFloat(0.5f, 0.9f)).Configure(false, 8);
                }
            }
            else {
                //收势
                float t = (elapsed - slashEnd) / RecoverTime;
                currentRotation = endAngle;
                trailFade = 1f - t;
                PushTrailSamples();
            }

            UpdatePlayerPose();
            Lighting.AddLight(Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.7f
                , EnergyRed.ToVector3() * (0.25f + ChargeRatio * 0.4f));
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

        /// <summary>充能比例0~1，光效强度</summary>
        private float ChargeRatio => MathHelper.Clamp(Item.GetItemCharge() / 40f, 0f, 1f);

        private bool HasCharge => Item.GetItemCharge() >= 0.2f;

        private void FireBeam() {
            if (!Projectile.IsOwnedByLocalPlayer() || !HasCharge) {
                return;
            }

            Vector2 spawnPos = Owner.GetPlayerStabilityCenter() + UnitToMouseV * BladeReach * 0.6f;
            int proj = Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), spawnPos
                , UnitToMouseV * Item.shootSpeed, ProjectileID.MiniRetinaLaser
                , Projectile.damage, Projectile.knockBack, Owner.whoAmI);
            Main.projectile[proj].DamageType = DamageClass.Melee;
            Main.projectile[proj].penetrate = 6;
            Main.projectile[proj].usesLocalNPCImmunity = true;
            Main.projectile[proj].localNPCHitCooldown = -1;
            Main.projectile[proj].netUpdate = true;

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.45f, Pitch = 0.35f }, spawnPos);
            }
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.5f;
            Projectile.timeLeft = 60;
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

            if (!VaultUtils.isServer) {
                for (int i = 0; i < (IsFinisher ? 6 : 3); i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, Main.rand.NextVector2Circular(5f, 5f)
                        , Color.Lerp(EnergyRed, EnergyHot, Main.rand.NextFloat()), Main.rand.NextFloat(0.7f, 1.2f)).Configure(false, 10);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float dist = BladeReach * 0.5f * Projectile.scale;

            SpriteEffects effect = lockedDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            //贴图刀尖指向右上(-PiOver4)，垂直翻转后指向右下(+PiOver4)
            float rotOffset = lockedDirection == -1 ? -MathHelper.PiOver4 : MathHelper.PiOver4;

            //挥砍残影
            if (CanDamage() == true) {
                for (int i = 1; i <= 3; i++) {
                    float rot = MathHelper.Lerp(currentRotation, lastRotation, i / 3f);
                    Vector2 pos = hand + rot.ToRotationVector2() * dist - Main.screenPosition;
                    Color trailColor = EnergyRed * (0.3f * (1f - i / 4f) * (0.4f + ChargeRatio * 0.6f));
                    trailColor.A = 0;
                    Main.EntitySpriteDraw(tex, pos, null, trailColor, rot + rotOffset, origin
                        , Projectile.scale, effect, 0);
                }
            }

            //刀身本体
            Vector2 drawPos = hand + currentRotation.ToRotationVector2() * dist - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, currentRotation + rotOffset, origin
                , Projectile.scale, effect, 0);

            //能量辉光层，亮度随充能变化
            Color glow = EnergyRed * (0.25f + ChargeRatio * 0.45f);
            glow.A = 0;
            Main.EntitySpriteDraw(tex, drawPos, null, glow, currentRotation + rotOffset, origin
                , Projectile.scale * 1.06f, effect, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailCount < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.EnergySlashTrail?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 center = Owner.GetPlayerStabilityCenter();
            float outer = (BladeReach + 8f) * Projectile.scale;
            float inner = BladeReach * 0.35f * Projectile.scale;
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
            effect.Parameters["uHeat"]?.SetValue(MathHelper.Clamp(ChargeRatio * 0.85f + (IsFinisher ? 0.3f : 0f), 0f, 1f));
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }
}
