using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
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
    /// 荒漠猎行者
    /// <para>一把链锁式远程武器，发射的并非弹药本身，而是一枚被锁链牵引的钩状枪头</para>
    /// <para>左键发射枪头，命中或飞至最远距离后会自动回收</para>
    /// <para>右键强制将正在外飞的枪头立刻拽回</para>
    /// </summary>
    internal class DuneStalker : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "DuneStalker";

        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 62;
            Item.height = 34;
            Item.damage = 18;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.knockBack = 5.5f;
            Item.shootSpeed = 16f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.autoReuse = true;
            Item.UseSound = SoundID.Item40 with { Pitch = -0.15f };
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(0, 1, 50, 0);
            Item.shoot = ModContent.ProjectileType<DuneStalkerHeld>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.AntlionMandible, 8)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 8)
                .AddIngredient(ItemID.Chain, 12)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    /// <summary>
    /// 荒漠猎行者的手持弹幕，负责跟随玩家瞄准、播放开火动画、生成枪头与处理收回指令
    /// </summary>
    internal class DuneStalkerHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Ranged + "DuneStalker";
        public override LocalizedText DisplayName
            => ItemLoader.GetItem(ModContent.ItemType<DuneStalker>()).DisplayName;

        /// <summary>开火冷却（射击间隔结束才能再次开火）</summary>
        private ref float FireCooldown => ref Projectile.ai[0];
        /// <summary>开火反冲衰减，用于轻微的枪体抖动表现</summary>
        private ref float RecoilOffset => ref Projectile.ai[1];
        /// <summary>当前是否有任何一个枪头存活</summary>
        private bool HeadActive => Owner.ownedProjectileCounts[ModContent.ProjectileType<DuneStalkerHeadProj>()] > 0;

        public override void SetDefaults() {
            Projectile.width = 62;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
            Projectile.hide = false;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        public override bool PreUpdate() {
            //没有持有目标物品时立即销毁
            if (Item == null || Item.IsAir || Item.type != ModContent.ItemType<DuneStalker>()) {
                Projectile.Kill();
                return false;
            }
            if (!Owner.active || Owner.dead || Owner.CCed) {
                Projectile.Kill();
                return false;
            }
            if (!Owner.channel && !HeadActive) {
                Projectile.Kill();
                return false;
            }
            return true;
        }

        public override void AI() {
            //保持手持状态
            SetHeld();
            Projectile.timeLeft = 2;

            //跟随玩家朝鼠标方向旋转
            UpdateHoldPose();

            //处理玩家手臂姿态
            UpdateOwnerArms();

            //开火冷却递减
            if (FireCooldown > 0) {
                FireCooldown--;
                Owner.itemTime = 2;
                Owner.itemAnimation = 2;
            }

            //反冲偏移衰减（仅用于绘制）
            if (RecoilOffset > 0) {
                RecoilOffset *= 0.82f;
                if (RecoilOffset < 0.05f) {
                    RecoilOffset = 0;
                }
            }

            //左键：尝试发射枪头
            if (DownLeft && !HeadActive && FireCooldown <= 0) {
                FireHead();
            }
        }

        /// <summary>
        /// 更新枪体位置和旋转，使其紧贴玩家中心、指向鼠标
        /// </summary>
        private void UpdateHoldPose() {
            if (!HeadActive)
                Projectile.rotation = ToMouseA;
            Vector2 aimDir = Projectile.rotation.ToRotationVector2();
            if (aimDir == Vector2.Zero) {
                aimDir = new Vector2(Owner.direction, 0);
            }

            //先确定玩家朝向，避免后续的角度计算使用过期方向
            Owner.ChangeDir(aimDir.X >= 0 ? 1 : -1);

            //开火时增加一点反冲的位移
            float recoil = RecoilOffset;
            Vector2 holdCenter = Owner.GetPlayerStabilityCenter()
                + aimDir * (20 - recoil)
                + new Vector2(0, 2 * Owner.gravDir);

            Projectile.Center = holdCenter;

            //itemRotation 期望的是"相对玩家朝向"的角度，所以需要乘上 direction 进行镜像
            Owner.itemRotation = MathHelper.WrapAngle(Projectile.rotation * Owner.direction);
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
        }

        /// <summary>
        /// 设置玩家持枪的双手姿势
        /// <para>注意：<see cref="Player.SetCompositeArmFront"/> 接收的旋转角是世界空间下的，
        /// 0 表示手臂垂直向下，-PI/2 指向右，+PI/2 指向左；因此公式与玩家朝向无关，
        /// 只需要按重力方向翻转 PI/2 的偏移</para>
        /// </summary>
        private void UpdateOwnerArms() {
            float armRot = Projectile.rotation - MathHelper.PiOver2 * SafeGravDir;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRot);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, armRot);
        }

        /// <summary>
        /// 获取枪口在世界中的位置（用于发射、绘制链条起点）
        /// </summary>
        public Vector2 GetMuzzlePos() {
            float dirSign = DirSign;
            Vector2 forward = Projectile.rotation.ToRotationVector2();
            Vector2 normal = new Vector2(-forward.Y, forward.X) * dirSign;
            return Projectile.Center + forward * 26f + normal * -4f;
        }

        /// <summary>
        /// 发射钩状枪头
        /// </summary>
        private void FireHead() {
            //消耗弹药，借弹药数据加成伤害与速度
            bool hasAmmo = Owner.PickAmmo(Item, out int _, out float speed, out int damage, out float knockback, out int _, false);
            if (!hasAmmo) {
                return;
            }

            FireCooldown = Item.useTime;
            RecoilOffset = 6f;
            Owner.itemTime = Item.useTime;
            Owner.itemAnimation = Item.useTime;

            SoundEngine.PlaySound(Item.UseSound, Projectile.Center);

            //发射口的烟尘特效
            if (!Main.dedServ) {
                Vector2 muzzle = GetMuzzlePos();
                Vector2 fwd = Projectile.rotation.ToRotationVector2();
                for (int i = 0; i < 14; i++) {
                    Dust d = Dust.NewDustPerfect(muzzle, DustID.Sand,
                        fwd.RotatedByRandom(0.35f) * Main.rand.NextFloat(2f, 6f), 80, default, 1.1f);
                    d.noGravity = true;
                }
                for (int i = 0; i < 4; i++) {
                    Dust smoke = Dust.NewDustPerfect(muzzle, DustID.Smoke,
                        fwd.RotatedByRandom(0.2f) * Main.rand.NextFloat(1f, 3f), 120, default, 1.4f);
                    smoke.noGravity = true;
                }
            }

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            Vector2 shootPos = GetMuzzlePos();
            Vector2 shootVel = Projectile.rotation.ToRotationVector2() * speed;

            int headDamage = Owner.GetWeaponDamage(Item);
            //如果弹药本身提供了更高的伤害基准则采纳
            if (damage > headDamage) {
                headDamage = damage;
            }

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                shootPos,
                shootVel,
                ModContent.ProjectileType<DuneStalkerHeadProj>(),
                headDamage,
                knockback,
                Owner.whoAmI,
                ai0: Projectile.whoAmI
            );

            NetUpdate();
        }

        /// <summary>
        /// 让所有外飞的枪头立刻进入收回阶段
        /// </summary>
        private void RecallHeads() {
            int headType = ModContent.ProjectileType<DuneStalkerHeadProj>();
            bool anyRecalled = false;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != headType || proj.owner != Owner.whoAmI) {
                    continue;
                }
                if (proj.ModProjectile is DuneStalkerHeadProj head && head.ForceRecall()) {
                    anyRecalled = true;
                }
            }
            if (anyRecalled) {
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.2f, Volume = 0.5f }, Projectile.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //左右镜像时使用 FlipVertically 以匹配旋转后正确朝向
            SpriteEffects fx = DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, Projectile.rotation, origin, Projectile.scale, fx, 0);
            return false;
        }
    }

    /// <summary>
    /// 荒漠猎行者发射的链锁枪头，沿瞄准方向飞出后自动回收，途中持续命中敌人
    /// </summary>
    internal class DuneStalkerHeadProj : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Ranged + "DuneStalkerHead";

        [VaultLoaden(CWRConstant.Item_Ranged + "DuneStalkerHead")]
        internal static Asset<Texture2D> HeadTex = null;
        [VaultLoaden(CWRConstant.Item_Ranged + "DuneStalkerChain")]
        internal static Asset<Texture2D> ChainTex = null;

        /// <summary>当前阶段：0 = 飞出，1 = 回收</summary>
        private ref float State => ref Projectile.ai[0];
        /// <summary>整体计时器</summary>
        private ref float Timer => ref Projectile.ai[1];
        /// <summary>关联的手持弹幕 whoAmI（用于定位枪口绘制锚点）</summary>
        private ref float HeldOwnerWhoAmI => ref Projectile.ai[2];

        /// <summary>飞出阶段允许达到的最大距离</summary>
        private const float MaxLaunchDistance = 540f;
        /// <summary>飞出阶段的最大持续时间，避免被卡住</summary>
        private const int MaxLaunchTime = 45;
        /// <summary>回收阶段的基础速度</summary>
        private const float ReturnSpeed = 22f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.netImportant = true;
        }

        private Player Owner => Main.player[Projectile.owner];
        private bool IsLaunching => State == 0f;
        private bool IsReturning => State == 1f;

        /// <summary>
        /// 外部强制收回接口，返回是否成功切换到回收阶段
        /// </summary>
        public bool ForceRecall() {
            if (IsReturning) {
                return false;
            }
            State = 1f;
            Timer = 0f;
            Projectile.tileCollide = false;
            Projectile.netUpdate = true;
            return true;
        }

        public override void AI() {
            Timer++;

            //玩家失活直接销毁
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (Projectile.rotation == 0)
            Projectile.rotation = Projectile.velocity.ToRotation();

            Projectile.position += Owner.velocity / 2;

            Vector2 anchor = GetAnchorPosition();

            if (IsLaunching) {
                LaunchingAI(anchor);
            }
            else {
                ReturningAI(anchor);
            }

            //轻微的沙尘拖尾
            SpawnTrailDust();

            //持续向枪头方向施加轻微光照
            Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.42f, 0.22f) * 0.5f);
        }

        /// <summary>
        /// 飞出阶段：保持初始速度，达到最大距离或最大时间时转入收回
        /// </summary>
        private void LaunchingAI(Vector2 anchor) {
            //略微的空气阻力，避免飞得过远难以收回
            Projectile.velocity *= 0.992f;

            float distance = Vector2.Distance(Projectile.Center, anchor);
            if (distance >= MaxLaunchDistance || Timer >= MaxLaunchTime) {
                EnterReturning();
            }
        }

        /// <summary>
        /// 回收阶段：朝玩家枪口加速直线返回，距离足够近时销毁
        /// </summary>
        private void ReturningAI(Vector2 anchor) {
            Vector2 toAnchor = anchor - Projectile.Center;
            float distance = toAnchor.Length();
            if (distance < 26f) {
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.5f, Pitch = 0.1f }, anchor);
                }
                Projectile.Kill();
                return;
            }

            Vector2 desiredVel = toAnchor.SafeNormalize(Vector2.UnitX) * ReturnSpeed;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.35f);
        }

        /// <summary>
        /// 切换到回收阶段，并播放反馈音效
        /// </summary>
        private void EnterReturning() {
            if (IsReturning) {
                return;
            }
            State = 1f;
            Timer = 0f;
            Projectile.tileCollide = false;
            Projectile.netUpdate = true;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.4f, Volume = 0.6f }, Projectile.Center);
            }
        }

        /// <summary>
        /// 通过 <see cref="HeldOwnerWhoAmI"/> 查找绑定的手持弹幕枪口；若不存在则退化为玩家中心
        /// </summary>
        private Vector2 GetAnchorPosition() {
            int heldWhoAmI = (int)HeldOwnerWhoAmI;
            if (heldWhoAmI >= 0 && heldWhoAmI < Main.maxProjectiles) {
                Projectile held = Main.projectile[heldWhoAmI];
                if (held.Alives() && held.type == ModContent.ProjectileType<DuneStalkerHeld>()
                    && held.owner == Projectile.owner
                    && held.ModProjectile is DuneStalkerHeld stalker) {
                    return stalker.GetMuzzlePos();
                }
            }
            return Owner.GetPlayerStabilityCenter();
        }

        private void SpawnTrailDust() {
            if (Main.dedServ || Main.rand.NextBool(2)) {
                return;
            }
            Vector2 backward = -Projectile.velocity.SafeNormalize(Vector2.Zero);
            Dust dust = Dust.NewDustPerfect(Projectile.Center + backward * 6f, DustID.Sand,
                backward * Main.rand.NextFloat(0.5f, 1.6f) + Main.rand.NextVector2Circular(0.6f, 0.6f),
                80, default, 0.95f);
            dust.noGravity = true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (IsReturning) {
                return false;
            }
            //撞墙时进入回收阶段并制造一点反馈
            if (!Main.dedServ) {
                Collision.HitTiles(Projectile.position, oldVelocity, Projectile.width, Projectile.height);
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
                for (int i = 0; i < 10; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center,
                        DustID.Sand, -oldVelocity.RotatedByRandom(0.4f) * Main.rand.NextFloat(0.3f, 1.2f), 80, default, 1.2f);
                    d.noGravity = true;
                }
            }
            EnterReturning();
            return false;
        }

        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac) {
            width = height = 20;
            return base.TileCollideStyle(ref width, ref height, ref fallThrough, ref hitboxCenterFrac);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中之后并不立即收回，允许飞行途中持续切割；但接触到玩家手会被收回
            if (!Main.dedServ) {
                for (int i = 0; i < 8; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center,
                        DustID.Sand, Main.rand.NextVector2Circular(4f, 4f), 80, default, 1.3f);
                    d.noGravity = true;
                }
            }
            //首次命中后若仍在飞出阶段，将剩余飞行时间缩短，加快回收节奏
            if (IsLaunching && Timer < MaxLaunchTime - 12) {
                Timer = MaxLaunchTime - 12;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawChain();
            DrawHead(lightColor);
            return false;
        }

        /// <summary>
        /// 绘制玩家枪口到枪头之间的锁链
        /// </summary>
        private void DrawChain() {
            Texture2D chainTex = ChainTex.Value;
            Vector2 start = GetAnchorPosition();
            Vector2 end = Projectile.Center;

            Vector2 diff = end - start;
            float distance = diff.Length();
            if (distance < 1f) {
                return;
            }

            float rotation = diff.ToRotation();
            int segLength = chainTex.Height - 2;
            if (segLength <= 0) {
                return;
            }

            int segCount = (int)Math.Ceiling(distance / segLength);
            Vector2 unit = diff / distance;
            Vector2 origin = chainTex.Size() / 2;

            for (int i = 0; i < segCount; i++) {
                Vector2 segWorld = start + unit * segLength * i;
                Color segColor = Lighting.GetColor(segWorld.ToTileCoordinates());
                Main.EntitySpriteDraw(chainTex, segWorld - Main.screenPosition, null, segColor,
                    rotation, origin, 1f, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制枪头本体（含轻微残影）
        /// </summary>
        private void DrawHead(Color lightColor) {
            Texture2D tex = HeadTex?.Value ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            //残影
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size / 2f;
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = lightColor * fade * 0.4f;
                Main.EntitySpriteDraw(tex, oldCenter - Main.screenPosition, null, trailColor,
                    Projectile.rotation, origin, Projectile.scale * (0.9f + 0.1f * fade), SpriteEffects.None, 0);
            }
            //主体
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
        }
    }
}
