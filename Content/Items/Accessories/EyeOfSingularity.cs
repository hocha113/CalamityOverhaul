using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Ranged.NeutronBows;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class EyeOfSingularity : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "EyeOfSingularity";
        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 6));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(180, 22, 15, 0);
            Item.rare = CWRID.Rarity_Turquoise;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_EyeOfSingularity;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.moveSpeed += 0.25f;
            player.magicQuiver = true;
            player.GetDamage<RangedDamageClass>() += 1f;
            player.GetCritChance<RangedDamageClass>() += 100f;
            player.GetAttackSpeed<RangedDamageClass>() += 1f;
            player.aggro -= 1200;

            EyeOfSingularityPlayer singPlayer = player.GetModPlayer<EyeOfSingularityPlayer>();
            singPlayer.Alive = true;
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) {
            return incomingItem.type != ModContent.ItemType<ElementMuzzleBrake>()
                && incomingItem.type != ModContent.ItemType<PrecisionMuzzleBrake>()
                && incomingItem.type != ModContent.ItemType<SimpleMuzzleBrake>();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.InsertHotkeyBinding(CWRKeySystem.Accessory_Skills, "[KEY]", CWRKeySystem.Notbound.Value + $"[{CWRKeySystem.Accessory_Skills.DisplayName}]");
        }
    }

    /// 奇点视界微型黑洞，命中吸附绞杀
    public class SingularityBlackHole : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 200;
            Projectile.timeLeft = 90;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public bool CanDrawCustom() => false;

        public override void AI() {
            if (Projectile.ai[2] % 12 == 0) {
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 5; j++) {
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vr * (0.1f + j * 0.18f), Color.MediumPurple, 0.8f * Projectile.localAI[2]).Configure(false, 25);
                    }
                }
            }

            Projectile.ai[2]++;
            Projectile.ai[0] += 0.12f;

            if (Projectile.timeLeft > 75) {
                Projectile.localAI[0] += 0.05f;
                Projectile.ai[1] += 0.08f;
            }
            if (Projectile.timeLeft <= 15) {
                Projectile.localAI[0] -= 0.08f;
                Projectile.ai[1] -= 0.05f;
            }

            Projectile.localAI[1] += 0.07f;
            if (Projectile.localAI[2] < 1f) {
                Projectile.localAI[2] += 0.03f;
            }
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);

            //吸附周围敌人
            float pullRadius = 400f;
            float pullStrength = 6f;
            foreach (var npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < pullRadius && dist > 16f) {
                    Vector2 dir = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    float factor = 1f - (dist / pullRadius);
                    npc.velocity += dir * pullStrength * factor;
                }
            }

            //吸附敌方弹幕
            foreach (var proj in Main.ActiveProjectiles) {
                if (!proj.hostile || proj.damage <= 0) {
                    continue;
                }
                float dist = Vector2.Distance(proj.Center, Projectile.Center);
                if (dist < pullRadius * 0.6f && dist > 8f) {
                    Vector2 dir = (Projectile.Center - proj.Center).SafeNormalize(Vector2.Zero);
                    proj.velocity += dir * 3f * (1f - dist / (pullRadius * 0.6f));
                    if (dist < 32f) {
                        proj.Kill();
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.4f, 0.1f, 0.8f));
        }

        public override bool ShouldUpdatePosition() => false;
        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            Color warpColor = new Color(55, 15, 55) * Projectile.ai[1];
            for (int i = 0; i < 4; i++) {
                Main.spriteBatch.Draw(warpTex, Projectile.Center - Main.screenPosition, null, warpColor
                    , Projectile.ai[0] + i * 1.57f, warpTex.Size() / 2
                    , Projectile.localAI[0] * 0.5f * Projectile.localAI[2], SpriteEffects.None, 0f);
            }
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }

    /// 伽马射线暴，事件视界射击追加追踪弹
    public class GammaRayBurst : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.penetrate = 1;
            Projectile.extraUpdates = 3;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 30;
            }
            if (Projectile.alpha < 0) {
                Projectile.alpha = 0;
            }

            Projectile.ai[0]++;

            if (Projectile.ai[0] > 20) {
                NPC target = Projectile.Center.FindClosestNPC(1200);
                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1, 0.12f);
                }
            }

            if (!VaultUtils.isServer && Projectile.ai[0] % 2 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, Projectile.velocity * 0.1f, Color.Cyan, Main.rand.NextFloat(0.8f, 1.5f)).Configure(false, 12);
                if (Main.rand.NextBool(5)) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, Projectile.velocity.RotatedByRandom(0.5f) * -0.3f, Color.DeepSkyBlue, Main.rand.NextFloat(0.3f, 0.6f)).Configure(false, 15);
                }
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0f, 0.5f, 0.8f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<VoidErosion>(), 600);
            target.immune[Projectile.owner] = 0;
        }

        public override Color? GetAlpha(Color lightColor) => new Color(0, 180, 255, Projectile.alpha);
        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// 超新星爆发，致死范围伤害
    public class SupernovaExplosion : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        private int Time;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 600;
            Projectile.timeLeft = 60;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public bool CanDrawCustom() => true;

        public override void AI() {
            Time++;

            if (Time == 1) {
                SoundEngine.PlaySound(SoundID.DD2_BetsySummon with { Pitch = -0.5f, Volume = 2f }, Projectile.Center);
                if (CWRServerConfig.Instance.LensEasing) {
                    Main.SetCameraLerp(0.15f, 60);
                }
            }

            //膨胀效果
            if (Time < 20) {
                Projectile.localAI[0] += 0.08f;
                Projectile.ai[1] += 0.06f;
            }
            else {
                Projectile.localAI[0] -= 0.02f;
                Projectile.ai[1] -= 0.02f;
            }
            Projectile.ai[0] += 0.1f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);
            Projectile.localAI[1] += 0.05f;

            //大量粒子特效
            if (Time % 3 == 0) {
                for (int i = 0; i < 8; i++) {
                    float rot = MathHelper.TwoPi / 8f * i + Time * 0.05f;
                    Vector2 vr = rot.ToRotationVector2();
                    for (int j = 0; j < 6; j++) {
                        Color color = Main.rand.NextBool() ? Color.OrangeRed : Color.Cyan;
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vr * (0.5f + j * 0.6f), color, Main.rand.NextFloat(0.8f, 1.6f)).Configure(false, 30);
                    }
                }
            }

            //放射状NeutronLaser
            if (Time == 5 && Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 16; i++) {
                    float rot = MathHelper.TwoPi / 16f * i;
                    Vector2 vel = rot.ToRotationVector2() * 18f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel
                        , ModContent.ProjectileType<NeutronLaser>(), Projectile.damage / 2, 0, Projectile.owner);
                }
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1.5f, 0.8f, 0.3f) * Math.Min(Time / 10f, 1f));
        }

        public override bool ShouldUpdatePosition() => false;
        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            Color warpColor = new Color(60, 30, 15) * Projectile.ai[1];
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 drawOrig = warpTex.Size() / 2;
            for (int i = 0; i < 6; i++) {
                Main.spriteBatch.Draw(warpTex, drawPos, null, warpColor
                    , Projectile.ai[0] + i * 1.05f, drawOrig
                    , Projectile.localAI[0] * (1f + i * 0.08f), SpriteEffects.None, 0f);
            }
        }

        public void DrawCustom(SpriteBatch spriteBatch) {
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float progress = Time / 60f;
            float scale = CWRUtils.EaseOutCubic(Math.Min(progress * 2f, 1f)) * 4f;
            float alpha = 1f - CWRUtils.EaseInQuad(progress);
            Color coreColor = Color.Lerp(Color.White, Color.OrangeRed, progress) * alpha;
            coreColor.A = 0;
            spriteBatch.Draw(glowTex, drawPos, null, coreColor, 0
                , glowTex.Size() / 2, scale, SpriteEffects.None, 0f);
            Color outerColor = Color.Lerp(Color.Cyan, Color.MediumPurple, progress) * alpha * 0.5f;
            outerColor.A = 0;
            spriteBatch.Draw(glowTex, drawPos, null, outerColor, 0
                , glowTex.Size() / 2, scale * 1.5f, SpriteEffects.None, 0f);
        }
    }

    /// 量子迁跃，瞬移至光标并清沿途敌弹
    public class QuantumLeapProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void Initialize() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            Vector2 startPos = Owner.Center;
            Vector2 targetPos = Main.MouseWorld;
            Vector2 direction = (targetPos - startPos);
            float distance = direction.Length();
            direction = direction.SafeNormalize(Vector2.Zero);

            //消除沿途敌方弹幕
            float clearRadius = 80f;
            foreach (var proj in Main.ActiveProjectiles) {
                if (!proj.hostile || proj.damage <= 0) {
                    continue;
                }
                //检查弹幕是否在起点到终点的线段附近
                Vector2 ap = proj.Center - startPos;
                Vector2 ab = targetPos - startPos;
                float t = Math.Clamp(Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab), 0f, 1f);
                Vector2 closest = startPos + ab * t;
                float projDist = Vector2.Distance(proj.Center, closest);
                if (projDist < clearRadius) {
                    proj.Kill();
                }
            }

            //传送玩家
            Owner.Teleport(targetPos, -1);
            Owner.velocity = Vector2.Zero;

            SoundEngine.PlaySound(CWRSound.Pecharge with { Pitch = 0.5f }, targetPos);

            //起点特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 30; i++) {
                    float rot = MathHelper.TwoPi / 30f * i;
                    Vector2 vr = rot.ToRotationVector2();
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(startPos, vr * Main.rand.NextFloat(1f, 3f), Color.DeepSkyBlue, Main.rand.NextFloat(0.5f, 1f)).Configure(false, 25);
                }
                //终点特效
                for (int i = 0; i < 30; i++) {
                    float rot = MathHelper.TwoPi / 30f * i;
                    Vector2 vr = rot.ToRotationVector2();
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(targetPos, vr * Main.rand.NextFloat(1f, 3f), Color.MediumPurple, Main.rand.NextFloat(0.5f, 1f)).Configure(false, 25);
                }
                //沿途粒子连线
                int steps = (int)(distance / 16f);
                for (int i = 0; i < steps; i++) {
                    Vector2 pos = Vector2.Lerp(startPos, targetPos, i / (float)steps);
                    PRTLoader.NewParticle<PRT_Spark>(pos, Main.rand.NextVector2Circular(1, 1), Color.Cyan, Main.rand.NextFloat(0.3f, 0.6f)).Configure(false, 15);
                }
            }
        }

        public override void AI() {
            Projectile.Center = Owner.Center;
            if (Projectile.timeLeft <= 1) {
                Projectile.Kill();
            }
        }

        public override bool? CanDamage() => false;
        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// 奇点视界玩家机制
    internal class EyeOfSingularityPlayer : ModPlayer
    {
        public bool Alive;
        /// 事件视界(静止隐身)
        public bool EventHorizonActive;
        /// 事件视界透明度(平滑过渡)
        private float eventHorizonOpacity;
        /// 坍缩协议激活
        public bool CollapseProtocolActive;
        /// 坍缩协议剩余(帧)
        public int CollapseProtocolTimer;
        /// 坍缩协议冷却(帧)
        public int CollapseProtocolCooldown;
        /// 量子迁跃冷却
        public int QuantumLeapCooldown;
        /// 超新星复活冷却
        public int SupernovaCooldown;
        /// 静止计时
        private int stationaryTimer;
        /// 上帧位置
        private Vector2 lastPosition;
        /// 伽马射线暴冷却(帧)
        public int GammaRayBurstCooldown;

        public override void Initialize() {
            Alive = false;
            EventHorizonActive = false;
            CollapseProtocolActive = false;
            eventHorizonOpacity = 1f;
        }

        public override void ResetEffects() {
            Alive = false;

            if (CollapseProtocolCooldown > 0) {
                CollapseProtocolCooldown--;
            }
            if (CollapseProtocolTimer > 0) {
                CollapseProtocolTimer--;
                if (CollapseProtocolTimer <= 0) {
                    CollapseProtocolActive = false;
                }
            }
            if (QuantumLeapCooldown > 0) {
                QuantumLeapCooldown--;
            }
            if (GammaRayBurstCooldown > 0) {
                GammaRayBurstCooldown--;
            }
            if (SupernovaCooldown > 0) {
                SupernovaCooldown--;
            }
        }

        public override void PostUpdateMiscEffects() {
            if (!Alive) {
                EventHorizonActive = false;
                //饰品卸下时平滑恢复透明度
                if (eventHorizonOpacity < 1f) {
                    eventHorizonOpacity = MathHelper.Lerp(eventHorizonOpacity, 1f, 0.15f);
                    if (eventHorizonOpacity > 0.98f) {
                        eventHorizonOpacity = 1f;
                    }
                    Player.opacityForAnimation = eventHorizonOpacity;
                }
                stationaryTimer = 0;
                return;
            }

            //事件视界：静止检测
            float moveDist = Vector2.Distance(Player.Center, lastPosition);
            if (moveDist < 1f) {
                stationaryTimer++;
            }
            else {
                stationaryTimer = 0;
                if (EventHorizonActive) {
                    EventHorizonActive = false;
                }
            }

            //60帧静止进事件视界
            if (stationaryTimer > 60 && !EventHorizonActive) {
                EventHorizonActive = true;
                //微弱音效提示
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.8f, Volume = 0.4f }, Player.Center);
                }
            }

            //透明度平滑过渡
            if (EventHorizonActive) {
                eventHorizonOpacity = MathHelper.Lerp(eventHorizonOpacity, 0.15f, 0.08f);
                Player.npcTypeNoAggro[0] = true;
                Player.aggro -= 9999;
            }
            else {
                //恢复移动淡入
                eventHorizonOpacity = MathHelper.Lerp(eventHorizonOpacity, 1f, 0.15f);
                if (eventHorizonOpacity > 0.98f) {
                    eventHorizonOpacity = 1f;
                }
            }

            Player.opacityForAnimation = eventHorizonOpacity;

            lastPosition = Player.Center;
        }

        /// 远程命中产黑洞(仅玩家直射弹，排除衍生)
        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Alive) {
                return;
            }

            //排除衍生弹防级联
            int gammaType = ModContent.ProjectileType<GammaRayBurst>();
            int blackHoleType = ModContent.ProjectileType<SingularityBlackHole>();
            int supernovaType = ModContent.ProjectileType<SupernovaExplosion>();
            int neutronType = ModContent.ProjectileType<NeutronLaser>();
            if (proj.type == gammaType || proj.type == blackHoleType
                || proj.type == supernovaType || proj.type == neutronType) {
                return;
            }

            if (hit.DamageType.CountsAsClass<RangedDamageClass>()) {
                target.AddBuff(ModContent.BuffType<VoidErosion>(), 600);

                //坍缩协议无视无敌
                if (CollapseProtocolActive) {
                    target.immune[Player.whoAmI] = 0;
                }

                //命中概率产黑洞(限同屏数)
                if (Main.rand.NextBool(5) && Player.whoAmI == Main.myPlayer
                    && Player.ownedProjectileCounts[blackHoleType] < 3) {
                    Projectile.NewProjectile(Player.FromObjectGetParent(), target.Center, Vector2.Zero
                        , blackHoleType, hit.SourceDamage * 2, 0, Player.whoAmI);
                }

                //事件视界射击追加伽马射线(带冷却)
                if (EventHorizonActive && GammaRayBurstCooldown <= 0
                    && Player.ownedProjectileCounts[gammaType] < 6
                    && Player.whoAmI == Main.myPlayer) {
                    GammaRayBurstCooldown = 30;
                    for (int i = 0; i < 3; i++) {
                        Vector2 vel = (target.Center - Player.Center).SafeNormalize(Vector2.UnitX).RotatedByRandom(0.5f) * 12f;
                        Projectile.NewProjectile(Player.FromObjectGetParent(), Player.Center, vel
                            , gammaType, hit.SourceDamage, 0, Player.whoAmI);
                    }
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (!Alive) {
                return;
            }

            //远程暴击伤害倍率提升至500%（默认200%加上额外300%）
            if (modifiers.DamageType.CountsAsClass<RangedDamageClass>()) {
                modifiers.CritDamage += 3f;
            }

            //坍缩协议激活时强制暴击
            if (CollapseProtocolActive && modifiers.DamageType.CountsAsClass<RangedDamageClass>()) {
                modifiers.SetCrit();
            }
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Alive) {
                return;
            }

            //坍缩协议激活时无视无敌帧
            if (CollapseProtocolActive && hit.DamageType.CountsAsClass<RangedDamageClass>()) {
                target.immune[Player.whoAmI] = 0;
            }
        }

        public override bool CanConsumeAmmo(Item weapon, Item ammo) {
            if (Alive) {
                return false;
            }
            return base.CanConsumeAmmo(weapon, ammo);
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            if (!Alive) {
                return;
            }

            //事件视界激活时免疫接触伤害
            if (EventHorizonActive) {
                modifiers.FinalDamage *= 0f;
            }
        }

        public override void PreUpdateMovement() {
            if (!Alive) {
                return;
            }

            //量子迁跃，按下专属按键瞬移至光标位置
            if (CWRKeySystem.Accessory_Skills.JustPressed && QuantumLeapCooldown <= 0 && Player.whoAmI == Main.myPlayer) {
                Player.dashType = 0;
                Player.SetPlayerDashID(string.Empty);
                Projectile.NewProjectile(Player.FromObjectGetParent(), Player.Center, Vector2.Zero
                    , ModContent.ProjectileType<QuantumLeapProj>(), 0, 0, Player.whoAmI);
                QuantumLeapCooldown = 40;
                Player.GivePlayerImmuneState(15);
                //传送后重置事件视界，因为位置发生了突变
                stationaryTimer = 0;
                EventHorizonActive = false;
            }

            //坍缩协议，手持远程武器时右键激活
            if (Player.controlUseTile && Player.releaseUseItem
                && !CollapseProtocolActive && CollapseProtocolCooldown <= 0
                && Player.statMana > 0 && Player.whoAmI == Main.myPlayer
                && Player.HeldItem.DamageType.CountsAsClass<RangedDamageClass>()) {
                //消耗所有魔力
                Player.statMana = 0;
                Player.manaRegenDelay = 120;

                CollapseProtocolActive = true;
                CollapseProtocolTimer = 600;
                CollapseProtocolCooldown = 3600;

                SoundEngine.PlaySound(CWRSound.Pecharge with { Pitch = -0.3f, Volume = 1.5f }, Player.Center);

                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 50; i++) {
                        float rot = MathHelper.TwoPi / 50f * i;
                        Vector2 vr = rot.ToRotationVector2();
                        PRTLoader.NewParticle<PRT_Spark>(Player.Center, vr * Main.rand.NextFloat(2f, 5f), Color.BlueViolet, Main.rand.NextFloat(1f, 2f)).Configure(false, 30);
                    }
                }
            }
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp
            , ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            if (Alive && SupernovaCooldown <= 0) {
                SupernovaCooldown = 3600;

                //超新星爆发
                if (Player.whoAmI == Main.myPlayer) {
                    Projectile.NewProjectile(Player.FromObjectGetParent(), Player.Center, Vector2.Zero
                        , ModContent.ProjectileType<SupernovaExplosion>(), 99999, 0, Player.whoAmI);
                }

                //给予无敌帧并满血恢复
                Player.GivePlayerImmuneState(180);
                Player.Heal(Player.statLifeMax2);

                SoundEngine.PlaySound(SoundID.DD2_BetsySummon with { Pitch = -0.3f, Volume = 2.5f }, Player.Center);

                //传送到安全位置
                Vector2 safePos = Player.Center + new Vector2(0, -300);
                Player.Teleport(safePos, -1);

                playSound = false;
                genDust = false;
                return false;
            }
            return base.PreKill(damage, hitDirection, pvp, ref playSound, ref genDust, ref damageSource);
        }

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) {
            //玩家真正死亡时重置所有冷却和状态
            CollapseProtocolActive = false;
            CollapseProtocolTimer = 0;
            CollapseProtocolCooldown = 0;
            QuantumLeapCooldown = 0;
            SupernovaCooldown = 0;
            GammaRayBurstCooldown = 0;
            EventHorizonActive = false;
            eventHorizonOpacity = 1f;
            stationaryTimer = 0;
        }
    }
}
