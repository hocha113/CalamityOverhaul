using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
using CalamityOverhaul.Content.RemakeItems;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutOverride : CWRItemOverride
    {
        #region Data
        /// <summary>
        /// 目标ID
        /// </summary>
        public static int ID => GetCalItemID("HalibutCannon");
        /// <summary>
        /// 目标ID
        /// </summary>
        public override int TargetID => ID;
        /// <summary>
        /// 每个时期阶段对应的伤害，这个成员一般不需要直接访问，而是使用<see cref="GetOnDamage"/>
        /// </summary>
        private static Dictionary<int, int> DamageDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 每个时期阶段对应的额外暴击振幅的字典，这个成员一般不需要直接访问，而是使用<see cref="GetOnCrit"/>
        /// </summary>
        private static Dictionary<int, int> SetLevelCritDictionary = new Dictionary<int, int>();
        /// <summary>
        /// 获取开局的伤害
        /// </summary>
        public static int GetStartDamage => DamageDictionary[0];
        #endregion
        /// <summary>
        /// 获取时期对应的伤害
        /// </summary>
        public static int GetOnDamage(Item item) => DamageDictionary[HalibutData.GetLevel(item)];
        /// <summary>
        /// 计算伤害比例
        /// </summary>
        public static float GetSengsDamage(Item item) => GetOnDamage(item) / (float)GetStartDamage;
        /// <summary>
        /// 根据<see cref="GetOnDamage"/>获取一个与<see cref="RangedDamageClass"/>相关的乘算伤害
        /// </summary>
        public static int ActualRangedDamage(Item item) => (int)(Main.LocalPlayer.GetTotalDamage<RangedDamageClass>().ApplyTo(GetOnDamage(item)));
        /// <summary>
        /// 获取时期对应的额外暴击
        /// </summary>
        public static int GetOnCrit(Item item) => SetLevelCritDictionary[HalibutData.GetLevel(item)];

        public static void LoadWeaponData() {
            DamageDictionary = new Dictionary<int, int>(){
                {0, 4 },
                {1, 5 },
                {2, 6 },
                {3, 6 },
                {4, 11 },
                {5, 15 },
                {6, 20 },
                {7, 27 },
                {8, 35 },
                {9, 48 },
                {10, 65 },
                {11, 90 },
                {12, 125 },
                {13, 190 },
                {14, 320 }
            };
            SetLevelCritDictionary = new Dictionary<int, int>(){
                {0, 0 },
                {1, 1 },
                {2, 1 },
                {3, 2 },
                {4, 2 },
                {5, 5 },
                {6, 6 },
                {7, 7 },
                {8, 8 },
                {9, 9 },
                {10, 10 },
                {11, 11 },
                {12, 13 },
                {13, 15 },
                {14, 20 }
            };
        }
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? On_ModifyWeaponCrit(Item item, Player player, ref float crit) {
            crit += GetOnCrit(item);
            return false;
        }
        public override bool On_ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            CWRUtils.ModifyLegendWeaponDamageFunc(item, GetOnDamage(item), GetStartDamage, ref damage);
            return false;
        }
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => HalibutText.SetTooltip(item, ref tooltips);

        public override void SetStaticDefaults() => LoadWeaponData();

        public static void SetDefaultsFunc(Item Item) {
            LoadWeaponData();
            Item.damage = GetStartDamage;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 118;
            Item.height = 56;
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 1f;
            Item.UseSound = SoundID.Item38 with { Volume = 0.6f };
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Bullet;
            Item.CWR().LegendData = new HalibutData();
        }

        public override bool? CanUseItem(Item item, Player player) {
            item.UseSound = SoundID.Item38 with { Volume = 0.6f };
            if (FishSkill.IDToInstance.TryGetValue(player.GetOverride<HalibutPlayer>().SkillID, out var fishSkill)) {
                bool? result = fishSkill.CanUseItem(item, player);
                if (result.HasValue) {
                    return result.Value;
                }
            }
            if (player.CountProjectilesOfID<SuperpositionProj>() > 0) {
                return false;
            }
            return true;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            if (FishSkill.IDToInstance.TryGetValue(player.GetOverride<HalibutPlayer>().SkillID, out var fishSkill)) {
                bool? result = fishSkill.AltFunctionUse(item, player);
                if (result.HasValue) {
                    return result.Value;
                }
            }
            return false;
        }

        public override bool? UseItem(Item item, Player player) {
            if (FishSkill.IDToInstance.TryGetValue(player.GetOverride<HalibutPlayer>().SkillID, out var fishSkill)) {
                bool? result = fishSkill.UseItem(item, player);
                if (result.HasValue) {
                    return result.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// 计算武器持握的旋转角度，基于玩家手臂朝向
        /// </summary>
        private static float CalculateWeaponRotation(Player player) {
            float armRotation = player.compositeFrontArm.rotation;
            float rotationOffset = MathHelper.PiOver2 * player.gravDir;
            return armRotation + rotationOffset;
        }

        /// <summary>
        /// 获取武器绘制的位置偏移量
        /// </summary>
        private static Vector2 GetWeaponPositionOffset(float rotation, float distanceFromPlayer = 7f) {
            return rotation.ToRotationVector2() * distanceFromPlayer;
        }

        /// <summary>
        /// 设置武器相对于精灵的原点偏移
        /// </summary>
        private static Vector2 GetItemSpriteOrigin(int offsetX = -52, int offsetY = 4) {
            return new Vector2(offsetX, offsetY);
        }

        /// <summary>
        /// 应用清爽的持握样式
        /// </summary>
        private static void ApplyHoldingStyle(Player player, float rotation, Vector2 position, Vector2 itemSize, Vector2 originOffset) {
            originOffset.X *= player.direction;
            originOffset.Y *= player.gravDir;

            player.itemRotation = rotation;

            if (player.direction < 0) {
                player.itemRotation += MathHelper.Pi;
            }

            Vector2 consistentCenterAnchor = player.itemRotation.ToRotationVector2() * (itemSize.X / -2f - 10f) * player.direction;
            Vector2 consistentAnchor = consistentCenterAnchor - originOffset.RotatedBy(player.itemRotation);
            Vector2 offsetAgain = itemSize * -0.5f;

            Vector2 finalPosition = position + offsetAgain + consistentAnchor;
            int frame = player.bodyFrame.Y / player.bodyFrame.Height;
            if ((frame > 6 && frame < 10) || (frame > 13 && frame < 17)) {
                finalPosition -= Vector2.UnitY * 2f;
            }

            player.itemLocation = finalPosition + new Vector2(itemSize.X * 0.5f, 0);
        }

        /// <summary>
        /// 获取同步的鼠标位置
        /// </summary>
        private static Vector2 GetSyncedMousePosition(Player player) {
            if (player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return halibutPlayer.MouseWorld;
            }
            return Main.MouseWorld;
        }

        /// <summary>
        /// 计算动画进度（0到1）
        /// </summary>
        private static float GetAnimationProgress(Player player) {
            return 1f - (player.itemTime / (float)player.itemTimeMax);
        }

        /// <summary>
        /// 计算手臂摆动旋转的额外偏移量
        /// </summary>
        private static float CalculateArmSwingOffset(float progress, int playerDirection) {
            if (progress >= 0.4f) return 0f;

            float swingPhase = (0.4f - progress) / 0.4f;
            float swingPower = (float)Math.Pow(swingPhase, 2);
            return -0.16f * swingPower * playerDirection;
        }

        /// <summary>
        /// 根据鼠标位置计算基础旋转角度
        /// </summary>
        private static float CalculateBaseRotation(Player player, Vector2 mouseWorldPos) {
            Vector2 toMouse = player.Center - mouseWorldPos;
            float angleToMouse = toMouse.ToRotation();
            float gravityAdjustedAngle = angleToMouse * player.gravDir;
            return gravityAdjustedAngle + MathHelper.PiOver2;
        }

        public static bool DontModifyHeldStyle() {
            if (CWRMod.Instance.terrariaOverhaul != null) {
                return true;//与TO持握样式冲突
            }
            return false;
        }

        public override void UseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (DontModifyHeldStyle()) {
                return;
            }

            //同步获取鼠标位置
            Vector2 syncedMousePos = GetSyncedMousePosition(player);
            //根据鼠标位置更新玩家朝向
            Vector2 playerToMouse = player.To(syncedMousePos);
            player.ChangeDir(Math.Sign(playerToMouse.X));

            //计算武器的旋转角度和位置
            float weaponRotation = CalculateWeaponRotation(player);
            Vector2 positionOffset = GetWeaponPositionOffset(weaponRotation);
            Vector2 weaponDrawPosition = player.MountedCenter + positionOffset;

            //设置武器尺寸和原点
            Vector2 weaponDimensions = new Vector2(item.width, item.height);
            Vector2 spriteOrigin = GetItemSpriteOrigin();

            //应用持握样式
            ApplyHoldingStyle(player, weaponRotation, weaponDrawPosition, weaponDimensions, spriteOrigin);
        }

        public override void UseItemFrame(Item item, Player player) {
            if (DontModifyHeldStyle()) {
                return;
            }

            //同步获取鼠标位置
            Vector2 syncedMousePos = GetSyncedMousePosition(player);

            //根据鼠标位置更新玩家朝向
            Vector2 playerToMouse = player.To(syncedMousePos);
            player.ChangeDir(Math.Sign(playerToMouse.X));

            //计算动画的当前进度
            float animProgress = GetAnimationProgress(player);

            //基于鼠标位置计算旋转
            float baseRotation = CalculateBaseRotation(player, syncedMousePos);

            //添加挥动动画偏移
            float swingOffset = CalculateArmSwingOffset(animProgress, player.direction);
            float finalRotation = baseRotation + swingOffset;

            //设置复合手臂姿势
            SetCompositeArmWithRotation(player, finalRotation);
        }

        /// <summary>
        /// 设置玩家的复合手臂姿势
        /// </summary>
        private static void SetCompositeArmWithRotation(Player player, float rotation) {
            player.SetCompositeArmFront(
                enabled: true,
                stretch: Player.CompositeArmStretchAmount.Full,
                rotation: rotation
            );
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            bool isBullet = false;
            if (type == ProjectileID.Bullet) {
                isBullet = true;
                type = ModContent.ProjectileType<OceanCurrent>();
            }

            var hp = player.GetOverride<HalibutPlayer>();
            //记录克隆需要的射击事件
            if (hp.CloneFishActive) {
                hp.RegisterShoot(type, velocity, damage, knockback, item.type);
            }

            if (FishSkill.IDToInstance.TryGetValue(player.GetOverride<HalibutPlayer>().SkillID, out var fishSkill)) {
                bool? result = fishSkill.ShootAlt(item, player, source, position, velocity, type, damage, knockback);
                if (result.HasValue) {
                    return result.Value;
                }
            }

            if (player.altFunctionUse == 2) {
                return false;//右键不触发普通攻击
            }

            if (fishSkill != null) {
                bool? result = fishSkill.Shoot(item, player, source, position, velocity, type, damage, knockback);
                if (result.HasValue) {
                    return result.Value;
                }
            }

            int bulletAmt = Main.rand.Next((int)(1 + HalibutData.GetLevel() * 0.45f), (int)(1 + HalibutData.GetLevel() * 0.85f));
            if (isBullet) {
                damage = (int)(damage * (1.2f + (bulletAmt - 1) * (1f - 0.3 / Main.LocalPlayer.GetDamage<RangedDamageClass>().Additive)) * (1f + HalibutData.GetLevel() / 26f));
                if (damage < 12) {
                    damage = 12;
                }
                bulletAmt = 1;
            }

            for (int index = 0; index < bulletAmt; ++index) {
                float SpeedX = velocity.X + Main.rand.Next(-10, 11) * 0.05f;
                float SpeedY = velocity.Y + Main.rand.Next(-10, 11) * 0.05f;
                if (isBullet) {
                    SpeedX *= 1.4f;
                    SpeedY *= 1.4f;
                }
                Projectile.NewProjectile(source, position.X, position.Y, SpeedX, SpeedY, type, damage, knockback, player.whoAmI);
            }

            return false;
        }
    }
}
