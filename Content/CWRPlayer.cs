using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee.Extras;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Items.Rogue.Extras;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using CalamityOverhaul.Content.UIs.OverhaulTheBible;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content
{
    public class CWRPlayer : ModPlayer
    {
        #region Date
        /// <summary>
        /// 是否拥有大修宝典
        /// </summary>
        public bool HasOverhaulTheBibleBook;
        /// <summary>
        /// 圣物的装备等级，这个字段决定了玩家会拥有什么样的弹幕效果
        /// </summary>
        public int TheRelicLuxor = 0;
        /// <summary>
        /// 是否装备制动器
        /// </summary>
        public bool LoadMuzzleBrake;
        /// <summary>
        /// 装备的制动器等级
        /// </summary>
        public int LoadMuzzleBrakeLevel;
        /// <summary>
        /// 应力缩放
        /// </summary>
        public float PressureIncrease;
        /// <summary>
        /// 摄像头位置额外矫正值
        /// </summary>
        public Vector2 OffsetScreenPos;
        /// <summary>
        /// 设置屏幕振动
        /// </summary>
        public float ScreenShakeValue;
        /// <summary>
        /// 是否开启超级合成台
        /// </summary>
        public bool SupertableUIStartBool;
        /// <summary>
        /// 玩家是否坐在大排档塑料椅子之上
        /// </summary>
        public bool InFoodStallChair;
        /// <summary>
        /// 玩家是否手持鬼妖
        /// </summary>
        public bool HeldMurasamaBool;
        /// <summary>
        /// 玩家是否正在进行终结技
        /// </summary>
        public bool EndSkillEffectStartBool;
        /// <summary>
        /// 是否受伤
        /// </summary>
        public bool OnHit;
        /// <summary>
        /// 是否激活锈蚀勋章效果
        /// </summary>
        public bool RustyMedallion_Value;
        /// <summary>
        /// 该属性用于判断鼠标是否处于接口状态，这个和<see cref="Player.mouseInterface"/>作用相同
        /// </summary>
        public bool uiMouseInterface => Player.mouseInterface;
        /// <summary>
        /// 手持状态
        /// </summary>
        public int HeldStyle;
        /// <summary>
        /// 升龙技充能
        /// </summary>
        public int RisingDragonCharged;
        /// <summary>
        /// Tramg归属
        /// </summary>
        public int TETramContrType = -1;
        /// <summary>
        /// Compressor归属
        /// </summary>
        public int CompressorContrType = -1;
        /// <summary>
        /// 欧米茄指示箭头计数器
        /// </summary>
        public int InspectOmigaTime;
        /// <summary>
        /// 挥舞索引，一般被刀具所使用
        /// </summary>
        public int SwingIndex;
        /// <summary>
        /// 是否站在平台上，如果该值大于0，则会出现无重力的效果
        /// </summary>
        public int ReceivingPlatformTime;
        /// <summary>
        /// 值大于0时会停止大部分的游戏活动模拟冻结效果，这个值每帧会自动减1
        /// </summary>
        public int TimeFrozenTick;
        /// <summary>
        /// 如果该时间大于0，则玩家不能切换武器，这个值每帧会自动减1
        /// </summary>
        public int DontSwitchWeaponTime;
        /// <summary>
        /// 如果该时间大于0，则说明玩家正在换弹
        /// </summary>
        public int PlayerIsKreLoadTime;
        /// <summary>
        /// 不能拥有暗影克隆体的时间，这个值每帧会自动减1
        /// </summary>
        public int DontHasSemberDarkMasterCloneTime;
        /// <summary>
        /// 一个实时的绘制矫正值
        /// </summary>
        internal Vector2 SpecialDrawPositionOffset;
        /// <summary>
        /// 玩家位置变化量
        /// </summary>
        public Vector2 PlayerPositionChange;
        /// <summary>
        /// 上一帧的玩家位置变化量
        /// </summary>
        public Vector2 oldPlayerPositionChange;
        /// <summary>
        /// 是否有地狱炎爆debuff
        /// </summary>
        public bool HellfireExplosion;
        /// <summary>
        /// 是否有灵魂火debuff
        /// </summary>
        public bool SoulfireExplosion;
        /// <summary>
        /// 是否穿戴正义显现
        /// </summary>
        public bool IsJusticeUnveiled;
        /// <summary>
        /// 存储待应用的冲刺速度向量，当其不为null时将在下一个帧应用
        /// </summary>
        public Vector2? PendingDashVelocity { get; set; } = null;
        /// <summary>
        /// 用于记录减速过程的计数器，表示减速剩余的帧数
        /// </summary>
        public float DecelerationCounter { get; set; }
        /// <summary>
        /// 指示玩家在冲刺过程中是否进行旋转
        /// </summary>
        public bool IsRotatingDuringDash { get; set; }
        /// <summary>
        /// 冲刺时的旋转方向，1为顺时针，-1为逆时针
        /// </summary>
        public float RotationDirection { get; set; } = 1f;
        /// <summary>
        /// 冲刺冷却计数器，用于记录冷却剩余的帧数
        /// </summary>
        public float DashCooldownCounter { get; set; }
        /// <summary>
        /// 记录旋转复位过程的计数器，表示复位剩余的帧数
        /// </summary>
        public float RotationResetCounter { get; set; }
        /// <summary>
        /// 旋转复位过程的持续时间（以帧为单位）
        /// </summary>
        public float RotationResetDuration { get; set; } = 15;
        /// <summary>
        /// 自定义冷却计数器，用于记录额外冷却的剩余帧数
        /// </summary>
        public int CustomCooldownCounter;
        #endregion

        public override void Initialize() {
            SwingIndex = 0;
            TETramContrType = 0;
            ReceivingPlatformTime = 0;
            Reset();
        }

        public override void ResetEffects() => Reset();

        private void Reset() {
            OffsetScreenPos = Vector2.Zero;
            TheRelicLuxor = 0;
            LoadMuzzleBrakeLevel = 0;
            PressureIncrease = 1;
            HeldStyle = -1;
            OnHit = false;
            LoadMuzzleBrake = false;
            InFoodStallChair = false;
            HeldMurasamaBool = false;
            EndSkillEffectStartBool = false;
            RustyMedallion_Value = false;
            HasOverhaulTheBibleBook = false;
            HellfireExplosion = false;
            IsJusticeUnveiled = false;
        }

        /// <summary>
        /// 用于判断是否应该冻结时间
        /// </summary>
        /// <returns></returns>
        public static bool CanTimeFrozen() {
            if (Main.LocalPlayer != null && Main.LocalPlayer.active) {
                if (Main.LocalPlayer.CWR().TimeFrozenTick > 0) {
                    return true;
                }
            }
            return false;
        }

        public override void PostUpdateMiscEffects() {
            if (Main.zenithWorld) {//在天顶世界中，怨念编织者会有特殊的粒子效果
                if (Player.GetItem().type == ModContent.ItemType<WeaverGrievances>()) {
                    WeaverGrievances.SpwanInOwnerDust(Player);
                }
            }
        }

        public override void OnEnterWorld() {
            CWRHook.CheckHookStatus();

            //if (false) {
            //    string soubText = CWRLocText.GetTextValue("TemporaryVersion_Text");
            //    soubText.Replace("[V1]", CWRMod.Instance.Version.ToString());
            //    soubText.Replace("[V2]", "0.6");
            //    SpwanTextProj.New(Player, () => CWRUtils.Text(soubText, Color.IndianRed));
            //}

            if (!CWRMod.Suitableversion_improveGame && CWRMod.Instance.improveGame != null) {
                string improvGameText = CWRLocText.GetTextValue("OnEnterWorld_TextContent2");
                SpwanTextProj.New(Player, () => CWRUtils.Text(improvGameText, Color.Red), 210);
                CWRMod.Instance.Logger.Info(improvGameText);
            }

            if (CWRServerConfig.Instance.WeaponOverhaul && Player.name == "HoCha113") {
                string text = CWRMod.RItemIndsDict.Count + CWRLocText.GetTextValue("OnEnterWorld_TextContent");
                SpwanTextProj.New(Player, () => CWRUtils.Text(text, Color.GreenYellow), 240);
            }

            if (ModGanged.Has_MS_Config_recursionCraftingDepth(out _)) {
                SpwanTextProj.New(Player, () => CWRUtils.Text(CWRLocText.GetTextValue("MS_Config_Text"), Color.IndianRed));
            }

            CraftingSlotHighlighter.Instance.eyEBool = true;
            if (SupertableUI.Instance != null) {
                SupertableUI.Instance.Active = false;
            }
            if (RecipeUI.Instance != null) {
                RecipeUI.Instance.index = 0;
                RecipeUI.Instance.LoadPsreviewItems();
            }
            if (OverhaulTheBibleUI.Instance != null) {
                OverhaulTheBibleUI.Instance.Active = false;
            }

            SupertableUI.LoadenWorld();

            SpearOfLonginus.ZenithWorldAsset();

            //初始化位置信息
            oldPlayerPositionChange = Player.position;
            PlayerPositionChange = Vector2.Zero;
        }

        public override void SaveData(TagCompound tag) {
            OverhaulTheBibleUI.Instance?.SaveData(tag);
            SupertableUI.Instance?.SaveData(tag);
        }

        public override void LoadData(TagCompound tag) {
            OverhaulTheBibleUI.Instance?.LoadData(tag);
            SupertableUI.Instance?.LoadData(tag);
        }

        public override void OnHurt(Player.HurtInfo info) {
            OnHit = true;
            if (Main.myPlayer == Player.whoAmI) {
                Projectile.NewProjectile(Player.GetSource_FromThis(), Player.Center, Vector2.Zero, ModContent.ProjectileType<Hit>(), 0, 0, Player.whoAmI);
            }
        }

        public override void CatchFish(FishingAttempt attempt, ref int itemDrop
            , ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) {
            if (CWRServerConfig.Instance.WeaponOverhaul && !attempt.inHoney && !attempt.inLava && Main.rand.NextBool(500)) {
                itemDrop = ModContent.ItemType<HalibutCannon>();
            }
        }

        public override void PreUpdateMovement() {
            if (PendingDashVelocity.HasValue) {
                Player.velocity = PendingDashVelocity.Value;
                PendingDashVelocity = null;
                RotationResetCounter = 0;
            }

            if (IsRotatingDuringDash) {
                Player.fullRotation += Player.velocity.Length() * 0.015f * RotationDirection;
                Player.fullRotationOrigin = Player.Size / 2;
            }

            if (RotationResetCounter > 0) {
                RotationResetCounter--;
                float resetProgress = RotationResetCounter / RotationResetDuration;
                Player.fullRotation = MathHelper.Lerp(0, Player.fullRotation, resetProgress);
            }

            if (DecelerationCounter > 0) {
                Player.velocity *= 0.95f;
                DecelerationCounter--;
            }

            if (DashCooldownCounter > 0) {
                DashCooldownCounter--;
            }

            if (CustomCooldownCounter > 0) {
                CustomCooldownCounter--;
            }

            if (ReceivingPlatformTime > 0) {
                Player.gravity = 0;
                if (Player.velocity.Y > 0) {
                    Player.velocity.Y = 0;
                }
                ReceivingPlatformTime--;
            }
        }

        public void SetScope() {
            Item heldItem = Player.GetItem();
            if (heldItem.type != ItemID.None && heldItem.CWR().Scope) {
                Player.scope = false;
            }
        }

        public override void PostUpdate() {
            SetScope();

            if (DontSwitchWeaponTime > 0) {
                DontSwitchWeaponTime--;
            }
            if (PlayerIsKreLoadTime > 0) {
                PlayerIsKreLoadTime--;
            }
            if (DontHasSemberDarkMasterCloneTime > 0) {
                DontHasSemberDarkMasterCloneTime--;
            }
            if (InspectOmigaTime > 0) {
                InspectOmigaTime--;
            }
            if (TimeFrozenTick > 0) {
                TimeFrozenTick--;
            }

            PlayerPositionChange = oldPlayerPositionChange.To(Player.position);
            oldPlayerPositionChange = Player.position;
        }

        public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo) {
            if (drawInfo.shadow != 0f) {
                return;
            }

            Player player = drawInfo.drawPlayer;

            SpecialDrawPositionOffset = Main.OffsetsPlayerHeadgear[player.bodyFrame.Y / player.bodyFrame.Height] * player.Directions;
            SpecialDrawPositionOffset.Y -= 2 * player.gravDir;//乘以一个重力矫正，这是一个无视偏转的值，所以需要考虑重力方向

            Item item = player.ActiveItem();
            if (HeldStyle >= 0) {
                player.bodyFrame.Y = player.bodyFrame.Height * HeldStyle;
            }
            if (!player.frozen && !item.IsAir && !player.dead && item.type > ItemID.None) {
                CWRItems cwrItem = item.CWR();
                Texture2D value = null;
                Rectangle frame = new Rectangle(0, 0, 1, 1);
                Vector2 orig = Vector2.Zero;
                Vector2 offsetPos = Vector2.Zero;
                float size = 1;
                int frameindex = 0;

                if (cwrItem.IsBow) {
                    int maxframe = 4;
                    if (player.velocity.Y == 0f && player.velocity.X != 0) {
                        frameindex = (int)(Main.GameUpdateCount / 4 % maxframe);
                    }
                    value = CWRAsset.Quiver_back_Asset.Value;
                    frame = CWRUtils.GetRec(value, frameindex, maxframe);
                    if (HeldStyle >= 0) {
                        frame = CWRUtils.GetRec(value, 0, maxframe);
                    }
                    orig = frame.Size() / 2;
                }

                if (item.type == ModContent.ItemType<DarkFrostSolstice>()) {
                    value = CWRAsset.IceGod_back_Asset.Value;
                    frame = CWRUtils.GetRec(value);
                    orig = CWRUtils.GetOrig(value);
                    float sengs = Main.GameUpdateCount * 0.05f;
                    offsetPos = new Vector2(player.direction * 8, -25 + MathF.Sin(sengs) * 5);
                }

                if (value == null) {
                    return;
                }

                SpriteEffects spriteEffects = Player.direction == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Vector2 drawPos;
                drawPos.X = (int)(((int)player.position.X) - Main.screenPosition.X + (player.width / 2) - (9 * player.direction)) - 4f * player.direction + offsetPos.X;
                drawPos.Y = (int)(((int)player.position.Y) - Main.screenPosition.Y + (player.height / 2) + 2f * player.gravDir - 8f * player.gravDir) + offsetPos.Y + player.gfxOffY;
                drawPos.Y += SpecialDrawPositionOffset.Y;
                DrawData howDoIDrawThings = new DrawData(value, drawPos, frame, drawInfo.colorArmorBody, player.bodyRotation, orig, size, spriteEffects, 0) {
                    shader = 0
                };
                drawInfo.DrawDataCache.Add(howDoIDrawThings);
            }
        }

        public override void ModifyScreenPosition() {
            Main.screenPosition += OffsetScreenPos;
            if (ScreenShakeValue > 0f) {
                Main.screenPosition += Main.rand.NextVector2Circular(ScreenShakeValue, ScreenShakeValue);
                ScreenShakeValue = MathHelper.Clamp(ScreenShakeValue - 0.185f, 0f, 20f);
            }
        }
        /// <summary>
        /// 设置屏幕震荡模长
        /// </summary>
        /// <param name="mode"></param>
        public void SetScreenShake(float mode) {
            if (ScreenShakeValue < mode)
                ScreenShakeValue = mode;
        }

        public override void UpdateBadLifeRegen() {
            if (HellfireExplosion) {
                if (Player.lifeRegen > 0) {
                    Player.lifeRegen = 0;
                }
                Player.lifeRegenTime = 0;
                Player.lifeRegen -= 120;
            }
            if (SoulfireExplosion) {
                if (Player.lifeRegen > 0) {
                    Player.lifeRegen = 0;
                }
                Player.lifeRegenTime = 0;
                Player.lifeRegen -= 120;
            }
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource) {
            if (HellfireExplosion) {
                damageSource = PlayerDeathReason.ByCustomReason(Player.name + CWRLocText.GetTextValue("HellfireExplosion_DeadLang_Text"));
            }
            if (SoulfireExplosion) {
                damageSource = PlayerDeathReason.ByCustomReason(Player.name + CWRLocText.GetTextValue("SoulfireExplosion_DeadLang_Text"));
            }
            return true;
        }

        /// <summary>
        /// 尝试获取玩家当前持有的类型为 T 的投射物实例。
        /// </summary>
        /// <typeparam name="T">要获取的投射物实例的类型。</typeparam>
        /// <param name="result">方法返回时，如果找到且成功获取到类型为 T 的投射物实例，则包含该实例；否则为 null。</param>
        /// <returns>如果找到并成功获取到类型为 T 的投射物实例，则为 true；否则为 false。</returns>
        internal bool TryGetHeldProjInds<T>(out T result) where T : class {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                // 检查投射物是否处于激活状态，是否属于玩家所有，并且是否隐藏
                if (!p.active || p.owner != Player.whoAmI || !p.hide) {
                    continue; // 如果当前投射物不符合条件，则跳过并检查下一个投射物
                }
                if (p.ModProjectile as T != null) {
                    Player.heldProj = p.whoAmI;
                    T instance = p.ModProjectile as T;
                    if (instance != null) {
                        result = instance;
                        return true;
                    }
                }
            }
            result = null;
            return false;
        }
        /// <summary>
        /// 获取玩家所手持的BaseHeldRanged实例
        /// </summary>
        /// <param name="baseGun"></param>
        /// <returns>如果玩家没有手持BaseGun或者发生了其他非法情况，返回<see langword="false"/></returns>
        internal bool TryGetInds_BaseHeldRanged(out BaseHeldRanged baseranged) {
            return TryGetHeldProjInds(out baseranged);
        }
        /// <summary>
        /// 获取玩家所手持的<see cref="BaseGun"/>实例
        /// </summary>
        /// <param name="baseGun"></param>
        /// <returns>如果玩家没有手持<see cref="BaseGun"/>或者发生了其他非法情况，返回<see langword="false"/></returns>
        internal bool TryGetInds_BaseGun(out BaseGun baseGun) {
            return TryGetHeldProjInds(out baseGun);
        }
        /// <summary>
        /// 获取玩家所手持的<see cref="BaseFeederGun"/>实例
        /// </summary>
        /// <param name="baseGun"></param>
        /// <returns>如果玩家没有手持<see cref="BaseFeederGun"/>或者发生了其他非法情况，返回<see langword="false"/></returns>
        internal bool TryGetInds_BaseFeederGun(out BaseFeederGun baseFeederGun) {
            return TryGetHeldProjInds(out baseFeederGun);
        }
        /// <summary>
        /// 获取玩家所手持的<see cref="BaseBow"/>实例
        /// </summary>
        /// <param name="baseGun"></param>
        /// <returns>如果玩家没有手持<see cref="BaseBow"/>或者发生了其他非法情况，返回<see langword="false"/></returns>
        internal bool TryGetInds_BaseBow(out BaseBow baseBow) {
            return TryGetHeldProjInds(out baseBow);
        }
    }
}
