using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
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
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 云端漫步：蓄力高举大斧，松开后斧头向前飞出，沿途喷洒云雾，然后自动飞回玩家手中
    /// </summary>
    internal class Cloudwalking : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "Cloudwalking";

        public override void SetDefaults() {
            Item.width = 60;
            Item.height = 60;
            Item.damage = 40;
            Item.DamageType = DamageClass.Melee;
            //使用动画由手持弹幕全权处理，这里只是触发入口
            Item.useAnimation = 12;
            Item.useTime = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTurn = false;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.knockBack = 10f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shootSpeed = 1f;
            Item.value = Item.buyPrice(0, 80, 0, 0);
            Item.rare = ItemRarityID.Cyan;
            Item.shoot = ModContent.ProjectileType<CloudwalkingHeld>();
            Item.crit = 5;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 3;

        //场上只允许一个实例
        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    /// <summary>
    /// 手持弹幕：蓄力举斧 → 投出（拖云雾） → 飞回玩家 → 收手销毁
    /// </summary>
    internal class CloudwalkingHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "Cloudwalking";

        public const int PhaseCharging = 0;
        public const int PhaseFlyingOut = 1;
        public const int PhaseReturning = 2;
        public const int PhaseRecovering = 3;

        private const int MaxChargeTime = 60;
        private const int RecoverTime = 12;
        private const int MaxFlyTime = 85;
        private const float LiftAnglePlayerRel = -MathHelper.Pi * 0.62f;
        private const float TextureBladeAngle = -MathHelper.PiOver4;
        private const float HoldDistance = 56f;
        private const float BaseThrowSpeed = 20f;
        private const float ExtraThrowSpeed = 14f;
        private const float MaxFlyDistance = 700f;

        private int lockedDirection = 1;
        private float chargeFrames;
        private float currentRotation;
        private float shakePhase;
        private int chargeParticleClock;
        private Vector2 axePivot;
        //投出时记录起点，用于判断返回时机（仅 owner 客户端使用）
        private Vector2 flyOrigin;

        public int Phase {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        private ref float PhaseTimer => ref Projectile.ai[1];

        public float ChargeRatio => MathHelper.Clamp(chargeFrames / MaxChargeTime, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.hide = true;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //if (target.IsWormBody()) {
            //    modifiers.FinalDamage *= 0.6f;
            //}
        }

        //飞行阶段由 velocity 驱动，蓄力/收手阶段手动固定位置
        public override bool ShouldUpdatePosition() => Phase == PhaseFlyingOut || Phase == PhaseReturning;

        public override bool? CanDamage() => Phase == PhaseFlyingOut || Phase == PhaseReturning;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase != PhaseFlyingOut && Phase != PhaseReturning) {
                return false;
            }
            //以斧头中心延伸出轴线做线段碰撞
            Vector2 axisDir = currentRotation.ToRotationVector2();
            Vector2 tip = axePivot + axisDir * 52f;
            Vector2 root = axePivot - axisDir * 28f;
            float collDist = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , root, tip, 28f, ref collDist);
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<Cloudwalking>()) {
                Projectile.Kill();
                return;
            }

            //首帧初始化：锁定方向和起手音效
            if (Projectile.localAI[0] == 0) {
                Projectile.localAI[0] = 1;
                lockedDirection = Math.Sign(ToMouse.X);
                if (lockedDirection == 0) lockedDirection = Owner.direction;
                Owner.direction = lockedDirection;
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = 0.15f }, Owner.Center);
            }

            switch (Phase) {
                case PhaseCharging:
                    UpdateCharging();
                    break;
                case PhaseFlyingOut:
                    UpdateFlyingOut();
                    break;
                case PhaseReturning:
                    UpdateReturning();
                    break;
                case PhaseRecovering:
                    UpdateRecovering();
                    break;
            }

            UpdatePlayerPose();

            float lightStrength = Phase == PhaseCharging ? (0.3f + ChargeRatio * 0.7f) : 0.8f;
            Lighting.AddLight(axePivot, 0.18f * lightStrength, 0.48f * lightStrength, 0.9f * lightStrength);

            PhaseTimer++;
        }

        private void UpdateCharging() {
            bool reachedCap = chargeFrames >= MaxChargeTime;
            bool released = !DownLeft && chargeFrames >= 1;

            float liftAngle = MirrorAngle(LiftAnglePlayerRel);
            shakePhase += 0.32f + ChargeRatio * 0.45f;
            float tremor = (float)Math.Sin(shakePhase) * ChargeRatio * 0.04f;
            currentRotation = liftAngle + tremor;
            axePivot = GetHandPos() + currentRotation.ToRotationVector2() * (HoldDistance * (1f + ChargeRatio * 0.07f));

            if (chargeFrames > 0 && chargeFrames % 20 == 0) {
                SoundEngine.PlaySound(SoundID.Item8 with {
                    Volume = 0.18f + ChargeRatio * 0.28f,
                    Pitch = -0.3f + ChargeRatio * 0.55f
                }, axePivot);
            }

            SpawnChargingParticles();

            //满蓄闪光
            if (Math.Abs(chargeFrames - MaxChargeTime + 1) < 0.5f) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.55f, Pitch = 0.25f }, axePivot);
                for (int i = 0; i < 22; i++) {
                    Vector2 v = (MathHelper.TwoPi * i / 22f).ToRotationVector2() * Main.rand.NextFloat(2f, 4.5f);
                    Dust d = Dust.NewDustPerfect(axePivot, DustID.WhiteTorch, v, 80, Color.LightCyan, 1.3f);
                    d.noGravity = true;
                    d.fadeIn = 1.1f;
                }
            }

            chargeFrames++;
            if (released || reachedCap) {
                EnterFlyPhase();
            }
        }

        private void EnterFlyPhase() {
            Phase = PhaseFlyingOut;
            PhaseTimer = 0;
            flyOrigin = axePivot;
            Projectile.Center = axePivot;
            Projectile.velocity = UnitToMouseV * (BaseThrowSpeed + ChargeRatio * ExtraThrowSpeed);
            Projectile.netUpdate = true;

            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.9f, Pitch = 0.25f - ChargeRatio * 0.15f }, Owner.Center);
        }

        private void UpdateFlyingOut() {
            axePivot = Projectile.Center;
            currentRotation += 0.22f * lockedDirection;
            //轻微空气阻力
            Projectile.velocity *= 0.986f;

            SpawnFlyingParticles(1.0f);

            //仅 owner 判断返回时机，避免 flyOrigin 未同步到其他客户端
            if (Projectile.IsOwnedByLocalPlayer()) {
                float maxDist = MathHelper.Lerp(380f, MaxFlyDistance, ChargeRatio);
                bool tooFar = Vector2.DistanceSquared(Projectile.Center, flyOrigin) >= maxDist * maxDist;
                bool timeout = PhaseTimer >= MaxFlyTime;
                if (tooFar || timeout) {
                    Phase = PhaseReturning;
                    PhaseTimer = 0;
                    Projectile.netUpdate = true;
                }
            }
        }

        private void UpdateReturning() {
            axePivot = Projectile.Center;
            currentRotation += 0.22f * lockedDirection;

            Vector2 toPlayer = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            float t = MathHelper.Clamp(PhaseTimer / 55f, 0f, 1f);
            float returnSpeed = MathHelper.Lerp(16f, 40f, t * t);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toPlayer * returnSpeed, 0.24f);

            SpawnFlyingParticles(0.55f);

            if (Projectile.IsOwnedByLocalPlayer() && Projectile.Distance(Owner.Center) < 80f) {
                Phase = PhaseRecovering;
                PhaseTimer = 0;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.55f, Pitch = 0.35f }, Owner.Center);
            }
        }

        private void UpdateRecovering() {
            //接住斧头后短暂停在手边再消失
            currentRotation = MirrorAngle(LiftAnglePlayerRel);
            axePivot = GetHandPos() + currentRotation.ToRotationVector2() * HoldDistance;

            if (PhaseTimer >= RecoverTime) {
                Projectile.Kill();
            }
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;

            if (Phase == PhaseFlyingOut || Phase == PhaseReturning) {
                axePivot = Projectile.Center;
                //飞行中手臂朝向斧头所在方向
                Vector2 toAxe = axePivot - Owner.GetPlayerStabilityCenter();
                float toAxeAngle = toAxe.ToRotation();
                Owner.itemRotation = (float)Math.Atan2(toAxe.Y * lockedDirection
                    , Math.Abs(toAxe.X)) * lockedDirection;
                if (CWRServerConfig.Instance.WeaponHandheldDisplay) {
                    float armAngle = toAxeAngle - MathHelper.PiOver2;
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters, armAngle);
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters
                        , armAngle + 0.22f * lockedDirection);
                }
            }
            else {
                //蓄力/收手阶段双臂举起跟随斧头
                Owner.itemRotation = currentRotation;
                if (CWRServerConfig.Instance.WeaponHandheldDisplay) {
                    float armAngle = currentRotation - MathHelper.PiOver2;
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armAngle);
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full
                        , armAngle + 0.15f * lockedDirection);
                }
                Projectile.Center = axePivot;
            }

            Projectile.timeLeft = 2;
        }

        private void SpawnChargingParticles() {
            chargeParticleClock++;
            if (Main.netMode == NetmodeID.Server) return;

            Vector2 bladePos = axePivot + currentRotation.ToRotationVector2() * 35f;

            //云雾粒子：密度随充能增加
            int chance = Math.Max(1, 5 - (int)(ChargeRatio * 4));
            if (Main.rand.NextBool(chance)) {
                Vector2 jitter = Main.rand.NextVector2Circular(22f, 22f);
                Vector2 vel = new Vector2(0, -Main.rand.NextFloat(0.4f, 1.8f)) + jitter * -0.03f;
                Color smokeColor = Color.Lerp(new Color(180, 225, 255), Color.White, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Smoke>(bladePos + jitter, vel, smokeColor, Main.rand.NextFloat(0.38f, 0.75f)).Configure(Main.rand.Next(28, 52), 0.7f, Main.rand.NextFloat(-0.03f, 0.03f));
            }

            //汇聚光尘：从四周向斧刃飞来
            if (ChargeRatio > 0.3f && Main.rand.NextBool(2)) {
                float r = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(48f, 115f) * (0.5f + ChargeRatio * 0.5f);
                Vector2 spawn = bladePos + r.ToRotationVector2() * radius;
                Vector2 vel = (bladePos - spawn).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2.8f, 5.2f);
                Dust d = Dust.NewDustPerfect(spawn, DustID.WhiteTorch, vel, 80, default, 0.85f + ChargeRatio * 0.55f);
                d.noGravity = true;
                d.fadeIn = 0.5f;
            }

            //满蓄后旋转云环
            if (ChargeRatio > 0.65f && chargeParticleClock % 4 == 0) {
                float angle = chargeParticleClock * 0.28f;
                Vector2 orbitPos = bladePos + angle.ToRotationVector2() * (26f + ChargeRatio * 18f);
                PRTLoader.NewParticle<PRT_Smoke>(orbitPos, Main.rand.NextVector2Circular(0.8f, 0.8f), new Color(140, 210, 255), Main.rand.NextFloat(0.28f, 0.52f)).Configure(Main.rand.Next(18, 32), 0.8f);
            }
        }

        private void SpawnFlyingParticles(float density) {
            if (Main.netMode == NetmodeID.Server) return;
            if (!Main.rand.NextBool((int)Math.Max(1, 1f / density))) return;

            Vector2 axisDir = currentRotation.ToRotationVector2();
            Vector2 bladeTip = axePivot + axisDir * 50f;
            Vector2 bladeRoot = axePivot - axisDir * 26f;

            //沿斧头轴线留下云雾拖尾
            for (int i = 0; i < 2; i++) {
                float t = Main.rand.NextFloat();
                Vector2 pos = Vector2.Lerp(bladeRoot, bladeTip, t) + Main.rand.NextVector2Circular(9f, 9f);
                //速度：略微逆飞行方向，形成自然拖尾
                Vector2 vel = Projectile.velocity * Main.rand.NextFloat(-0.08f, -0.03f)
                    + Main.rand.NextVector2Circular(1.2f, 1.2f);
                Color smokeColor = Color.Lerp(Color.White, new Color(155, 215, 255), Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Smoke>(pos, vel, smokeColor, Main.rand.NextFloat(0.6f, 1.05f)).Configure(Main.rand.Next(38, 68), 0.72f, Main.rand.NextFloat(-0.02f, 0.02f));
            }

            //偶发白色星光
            if (Main.rand.NextBool(3)) {
                Vector2 pos = bladeTip + Main.rand.NextVector2Circular(14f, 14f);
                Vector2 vel = Main.rand.NextVector2Circular(2.2f, 2.2f);
                Dust d = Dust.NewDustPerfect(pos, DustID.WhiteTorch, vel, 80, default, 0.75f + ChargeRatio * 0.4f);
                d.noGravity = true;
                d.fadeIn = 1.0f;
            }
        }

        private Vector2 GetHandPos() {
            Vector2 pivot = Owner.GetPlayerStabilityCenter();
            pivot.Y -= 6f * Owner.gravDir;
            return pivot;
        }

        private float MirrorAngle(float rightFacingAngle) {
            return lockedDirection > 0 ? rightFacingAngle : MathHelper.Pi - rightFacingAngle;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!CWRServerConfig.Instance.WeaponHandheldDisplay) {
                return false;
            }

            Texture2D tex = TextureAssets.Item[ModContent.ItemType<Cloudwalking>()].Value;
            Vector2 origin = tex.Size() / 2f;
            float drawRot = currentRotation - TextureBladeAngle;
            SpriteEffects effect = SpriteEffects.None;

            //蓄力/收手阶段需要朝左镜像，飞行阶段自旋不需要
            if (Phase == PhaseCharging || Phase == PhaseRecovering) {
                if (Owner.direction == -1) {
                    effect = SpriteEffects.FlipVertically;
                    drawRot -= MathHelper.PiOver2;
                }
            }

            Vector2 drawPos = axePivot - Main.screenPosition;
            float scale = 1.0f + (Phase == PhaseCharging ? ChargeRatio * 0.14f : 0f);

            //蓄力时蓝色外光晕
            if (Phase == PhaseCharging && ChargeRatio > 0.05f) {
                Texture2D glow = CWRUtils.GetT2DValue(CWRConstant.Masking + "SoftGlow");
                if (glow != null) {
                    Color glowColor = Color.Lerp(new Color(60, 140, 255, 0), new Color(200, 235, 255, 0), ChargeRatio);
                    float pulse = 0.85f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 7f) * 0.15f;
                    Main.spriteBatch.Draw(glow, drawPos, null, glowColor * ChargeRatio * 0.88f * pulse
                        , 0f, glow.Size() / 2f, (0.65f + ChargeRatio * 1.0f) * pulse, SpriteEffects.None, 0);
                }
            }

            //飞行阶段旋转残影拖尾
            if (Phase == PhaseFlyingOut || Phase == PhaseReturning) {
                int trail = 5;
                for (int i = 0; i < trail; i++) {
                    float trailAngle = currentRotation - 0.20f * lockedDirection * (trail - i);
                    float trailDrawRot = trailAngle - TextureBladeAngle;
                    float alpha = 0.42f * (1f - i / (float)trail);
                    Color trailColor = Color.Lerp(new Color(130, 205, 255, 0), Color.Transparent, i / (float)trail) * alpha;
                    Main.spriteBatch.Draw(tex, drawPos, null, trailColor, trailDrawRot, origin, scale, SpriteEffects.None, 0);
                }
            }

            //主体斧头
            Main.spriteBatch.Draw(tex, drawPos, null, lightColor, drawRot, origin, scale, effect, 0);

            //满蓄高光叠加
            if (Phase == PhaseCharging && ChargeRatio > 0.7f) {
                Color hot = Color.Lerp(new Color(150, 215, 255, 0), new Color(255, 255, 255, 0)
                    , (ChargeRatio - 0.7f) / 0.3f);
                Main.spriteBatch.Draw(tex, drawPos, null, hot, drawRot, origin, scale * 1.02f, effect, 0);
            }

            return false;
        }
    }

    internal class CloudwalkingSkyLootSystem : ModSystem
    {
        private const string SaveKey = "CloudwalkingInjected";
        private static bool injected;

        public override void OnWorldUnload() => injected = false;

        public override void LoadWorldData(TagCompound tag) {
            injected = tag != null && tag.TryGet(SaveKey, out bool v) && v;
        }

        public override void SaveWorldData(TagCompound tag) {
            tag[SaveKey] = injected;
        }

        public override void PostWorldGen() => Inject();

        public override void PostUpdateWorld() {
            if (injected || Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            Inject();
        }

        private static void Inject() {
            List<Chest> skyChests = new List<Chest>();
            for (int i = 0; i < Main.maxChests; i++) {
                Chest chest = Main.chest[i];
                if (chest != null && IsSkywareChest(chest)) {
                    skyChests.Add(chest);
                }
            }

            if (skyChests.Count == 0) {
                injected = true;
                return;
            }

            //按距世界水平中心的距离升序排序，靠前的更居中
            int centerX = Main.maxTilesX / 2;
            skyChests.Sort((a, b) =>
                Math.Abs(a.x - centerX).CompareTo(Math.Abs(b.x - centerX)));

            //从靠中间的一半里随机挑一个，保证倾向居中的同时保留随机性
            int pickRange = Math.Max(1, (skyChests.Count + 1) / 2);
            Chest target = skyChests[WorldGen.genRand.Next(pickRange)];

            PlaceInChest(target, ModContent.ItemType<Cloudwalking>());
            injected = true;
        }

        private static bool IsSkywareChest(Chest chest) {
            if (chest.x < 0 || chest.x >= Main.maxTilesX || chest.y < 0 || chest.y >= Main.maxTilesY) {
                return false;
            }
            Tile tile = Framing.GetTileSafely(chest.x, chest.y);
            //TileID.Containers 下 TileFrameX / 36 为箱子样式编号，13 = 空岛天蓝箱
            return tile.HasTile && tile.TileType == TileID.Containers && tile.TileFrameX / 36 == 13;
        }

        private static void PlaceInChest(Chest chest, int itemType) {
            //优先替换星怒
            for (int i = 0; i < chest.item.Length; i++) {
                if (chest.item[i] == null || chest.item[i].type == ItemID.Starfury) {
                    if (chest.item[i] == null) chest.item[i] = new Item();
                    chest.item[i].SetDefaults(itemType);
                    chest.item[i].stack = 1;
                    return;
                }
            }
            //次选第一个空格
            for (int i = 0; i < chest.item.Length; i++) {
                if (chest.item[i] == null || chest.item[i].type == ItemID.None) {
                    if (chest.item[i] == null) chest.item[i] = new Item();
                    chest.item[i].SetDefaults(itemType);
                    chest.item[i].stack = 1;
                    return;
                }
            }
        }
    }
}
