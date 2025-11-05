using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.LegendWeapon;
using CalamityOverhaul.Content.Projectiles.Others;
using CalamityOverhaul.Content.RangedModify;
using CalamityOverhaul.Content.RangedModify.Core;
using CalamityOverhaul.Content.RemakeItems;
using CalamityOverhaul.Content.UIs.OverhaulTheBible;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using CalamityOverhaul.OtherMods.HighFPSSupport;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content
{
    public class CWRPlayer : ModPlayer
    {
        #region Data
        /// <summary>
        /// 是否拥有大修宝典
        /// </summary>
        public bool HasOverhaulTheBibleBook;
        /// <summary>
        /// 装备的制动器等级，0则不装备
        /// </summary>
        public int LoadMuzzleBrakeLevel;
        /// <summary>
        /// 应力缩放
        /// </summary>
        public float PressureIncrease;
        /// <summary>
        /// 装弹时间缩放
        /// </summary>
        public float KreloadTimeIncrease;
        /// <summary>
        /// 摄像头位置额外矫正值
        /// </summary>
        public Vector2 OffsetScreenPos;
        /// <summary>
        /// 设置屏幕振动
        /// </summary>
        public float ScreenShakeValue;
        /// <summary>
        /// 火力发电活跃
        /// </summary>
        public int ThermalGenerationActiveTime;
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
        /// 该属性用于判断鼠标是否处于接口状态，这个和<see cref="Player.mouseInterface"/>作用相同
        /// </summary>
        public bool UIMouseInterface => Player.mouseInterface;
        /// <summary>
        /// 是否了解了风力
        /// </summary>
        public bool UnderstandWindGriven;
        /// <summary>
        /// 是否了解了风力MK2
        /// </summary>
        public bool UnderstandWindGrivenMK2;
        /// <summary>
        /// 是否使用了电动火箭
        /// </summary>
        public bool RideElectricMinRocket;
        /// <summary>
        /// 卸乘电动火箭的恢复周期
        /// </summary>
        public int RideElectricMinRocketRecoverStateTime;
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
        public int TramTPContrType = -1;
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
        /// 如果该时间大于0，则玩家不能切换武器，这个值每帧会自动减1
        /// </summary>
        public int DontSwitchWeaponTime;
        /// <summary>
        /// 如果该时间大于0，则说明玩家正在换弹
        /// </summary>
        public int PlayerIsKreLoadTime;
        /// <summary>
        /// 玩家装弹时间完成比例
        /// </summary>
        public float ReloadingRatio;
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
        /// 毁灭者之主
        /// </summary>
        public bool DestroyerOwner;
        /// <summary>
        /// 是否穿戴正义显现
        /// </summary>
        public bool IsJusticeUnveiled;
        /// <summary>
        /// 正义显现的触发机会次数
        /// </summary>
        public int JusticeUnveiledCharges;
        /// <summary>
        /// 正义显现的触发冷却
        /// </summary>
        public int JusticeUnveiledCooldown;
        /// <summary>
        /// 存储待应用的冲刺速度向量，当其不为null时将在下一个帧应用
        /// </summary>
        public Vector2? PendingDashVelocity { get; set; } = null;
        /// <summary>
        /// 翻滚时的旋转速度倍率
        /// </summary>
        public float PendingDashRotSpeedMode = 0.015f;
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
        /// <summary>
        /// 大于0时不能使用物品，该值每帧减一直至归零
        /// </summary>
        public int DontUseItemTime;
        /// <summary>
        /// 毁灭者的存在索引
        /// </summary>
        internal static int TheDestroyer = -1;
        /// <summary>
        /// 抬棺人下一发弩箭的伤害倍率，默认为1
        /// </summary>
        public float PallbearerNextArrowDamageMult = 1;
        #endregion

        public CWRPlayer CloneCWRPlayer(CWRPlayer cwr) {
            cwr.HasOverhaulTheBibleBook = HasOverhaulTheBibleBook;
            cwr.LoadMuzzleBrakeLevel = LoadMuzzleBrakeLevel;
            cwr.PressureIncrease = PressureIncrease;
            cwr.KreloadTimeIncrease = KreloadTimeIncrease;
            cwr.OffsetScreenPos = OffsetScreenPos;
            cwr.ScreenShakeValue = ScreenShakeValue;
            cwr.ThermalGenerationActiveTime = ThermalGenerationActiveTime;
            cwr.SupertableUIStartBool = SupertableUIStartBool;
            cwr.InFoodStallChair = InFoodStallChair;
            cwr.HeldMurasamaBool = HeldMurasamaBool;
            cwr.EndSkillEffectStartBool = EndSkillEffectStartBool;
            cwr.UnderstandWindGriven = UnderstandWindGriven;
            cwr.UnderstandWindGrivenMK2 = UnderstandWindGrivenMK2;
            cwr.RideElectricMinRocket = RideElectricMinRocket;
            cwr.RideElectricMinRocketRecoverStateTime = RideElectricMinRocketRecoverStateTime;
            cwr.HeldStyle = HeldStyle;
            cwr.RisingDragonCharged = RisingDragonCharged;
            cwr.TramTPContrType = TramTPContrType;
            cwr.CompressorContrType = CompressorContrType;
            cwr.InspectOmigaTime = InspectOmigaTime;
            cwr.SwingIndex = SwingIndex;
            cwr.ReceivingPlatformTime = ReceivingPlatformTime;
            cwr.DontSwitchWeaponTime = DontSwitchWeaponTime;
            cwr.PlayerIsKreLoadTime = PlayerIsKreLoadTime;
            cwr.ReloadingRatio = ReloadingRatio;
            cwr.DontHasSemberDarkMasterCloneTime = DontHasSemberDarkMasterCloneTime;
            cwr.SpecialDrawPositionOffset = SpecialDrawPositionOffset;
            cwr.PlayerPositionChange = PlayerPositionChange;
            cwr.oldPlayerPositionChange = oldPlayerPositionChange;
            cwr.HellfireExplosion = HellfireExplosion;
            cwr.SoulfireExplosion = SoulfireExplosion;
            cwr.DestroyerOwner = DestroyerOwner;
            cwr.IsJusticeUnveiled = IsJusticeUnveiled;
            cwr.JusticeUnveiledCharges = JusticeUnveiledCharges;
            cwr.JusticeUnveiledCooldown = JusticeUnveiledCooldown;
            cwr.PendingDashVelocity = PendingDashVelocity;
            cwr.PendingDashRotSpeedMode = PendingDashRotSpeedMode;
            cwr.DecelerationCounter = DecelerationCounter;
            cwr.IsRotatingDuringDash = IsRotatingDuringDash;
            cwr.RotationDirection = RotationDirection;
            cwr.DashCooldownCounter = DashCooldownCounter;
            cwr.RotationResetCounter = RotationResetCounter;
            cwr.RotationResetDuration = RotationResetDuration;
            cwr.CustomCooldownCounter = CustomCooldownCounter;
            cwr.DontUseItemTime = DontUseItemTime;
            cwr.PallbearerNextArrowDamageMult = PallbearerNextArrowDamageMult;
            return cwr;
        }

        public override ModPlayer Clone(Player newEntity) => CloneCWRPlayer((CWRPlayer)base.Clone(newEntity));

        public override void Initialize() {
            SwingIndex = 0;
            TramTPContrType = 0;
            ReceivingPlatformTime = 0;
            DontUseItemTime = 0;
            ThermalGenerationActiveTime = 0;
            PallbearerNextArrowDamageMult = 1; //初始化
            Reset();
        }

        public override void ResetEffects() => Reset();

        private void Reset() {
            OffsetScreenPos = Vector2.Zero;
            LoadMuzzleBrakeLevel = 0;
            PressureIncrease = 1;
            KreloadTimeIncrease = 1;
            HeldStyle = -1;
            ReloadingRatio = 0;
            TheDestroyer = -1;
            InFoodStallChair = false;
            HeldMurasamaBool = false;
            EndSkillEffectStartBool = false;
            HasOverhaulTheBibleBook = false;
            HellfireExplosion = false;
            IsJusticeUnveiled = false;
            DestroyerOwner = false;
            RideElectricMinRocket = false;
        }

        public override void SaveData(TagCompound tag) {
            try {
                tag["UnderstandWindGriven"] = UnderstandWindGriven;
                tag["UnderstandWindGrivenMK2"] = UnderstandWindGrivenMK2;
            } catch (Exception ex) { CWRMod.Instance.Logger.Error($"CWRPlayer.SaveData An Error Has Cccurred: {ex.Message}"); }
        }

        public override void LoadData(TagCompound tag) {
            try {
                if (!tag.TryGet("UnderstandWindGriven", out UnderstandWindGriven)) {
                    UnderstandWindGriven = false;
                }
                if (!tag.TryGet("UnderstandWindGrivenMK2", out UnderstandWindGrivenMK2)) {
                    UnderstandWindGrivenMK2 = false;
                }
            } catch (Exception ex) { CWRMod.Instance.Logger.Error($"CWRPlayer.LoadData An Error Has Cccurred: {ex.Message}"); }
        }

        public override bool CanUseItem(Item item) {
            if (DontUseItemTime > 0) {
                return false;
            }
            return base.CanUseItem(item);
        }

        public override void PostUpdateMiscEffects() {
            if (Main.zenithWorld) {//在天顶世界中，怨念编织者会有特殊的粒子效果
                if (Player.GetItem().type == ModContent.ItemType<WeaverGrievances>()) {
                    WeaverGrievances.SpwanInOwnerDust(Player);
                }
            }
        }

        /// <summary>
        /// 临时性版本提醒，在正式版本中不要调用这个函数
        /// </summary>
        private void DompTemporaryVersionText() {
            string soubText = CWRLocText.GetTextValue("TemporaryVersion_Text");
            soubText = soubText.Replace("[V1]", CWRMod.Instance.Version.ToString());
            soubText = soubText.Replace("[V2]", "0.6.8.2");
            SpwanTextProj.New(Player, () => VaultUtils.Text(soubText, Color.IndianRed));
        }

        public override void OnEnterWorld() {
            //DompTemporaryVersionText();

            if (!VaultHook.CheckHookStatus(out int num)) {
                string hookDownText1 = $"{num} " + CWRLocText.GetTextValue("Error_1");
                VaultUtils.Text(hookDownText1, Color.Red);
            }

            if (!ModGanged.Suitableversion_improveGame && CWRMod.Instance.improveGame != null) {
                string improvGameText = CWRLocText.GetTextValue("OnEnterWorld_TextContent2");
                SpwanTextProj.New(Player, () => VaultUtils.Text(improvGameText, Color.Red), 210);
                CWRMod.Instance.Logger.Info(improvGameText);
            }

            if (CWRServerConfig.Instance.WeaponOverhaul && Player.name == "HoCha113") {
                string text = CWRItemOverride.ByID.Count + CWRLocText.GetTextValue("OnEnterWorld_TextContent");
                SpwanTextProj.New(Player, () => VaultUtils.Text(text, Color.GreenYellow), 240);
            }

            //if (ModGanged.Has_MS_Config_recursionCraftingDepth(out _)) {
            //   SpwanTextProj.New(Player, () => VaultUtils.Text(CWRLocText.GetTextValue("MS_Config_Text"), Color.IndianRed));
            //}

            CraftingSlotHighlighter.Instance.eyEBool = true;

            if (SupertableUI.Instance != null) {
                SupertableUI.Instance.Active = false;
            }

            if (RecipeUI.Instance != null) {
                RecipeUI.Instance.index = 0;
                RecipeUI.Instance.LoadPreviewItems();
            }

            if (OverhaulTheBibleUI.Instance != null) {
                OverhaulTheBibleUI.Instance.Active = false;
            }

            SupertableUI.LoadenWorld();

            SpearOfLonginus.ZenithWorldAsset();

            HandlerCanOverride.ModifiIntercept_OnEnterWorld();

            HighFPSRef.DisableMotionInterpolation();

            LegendData.ResetInventory(Player);

            //初始化位置信息
            oldPlayerPositionChange = Player.position;
            PlayerPositionChange = Vector2.Zero;
        }

        public override void OnHurt(Player.HurtInfo info) {
            if (Main.myPlayer == Player.whoAmI) {
                Projectile.NewProjectile(Player.GetSource_FromThis(), Player.Center, Vector2.Zero
                    , ModContent.ProjectileType<Hit>(), 0, 0, Player.whoAmI);
            }
        }

        public override void PreUpdateMovement() {
            if (RideElectricMinRocketRecoverStateTime > 0) {
                RideElectricMinRocketRecoverStateTime--;
                Player.fullRotation = MathHelper.Lerp(Player.fullRotation, 0, 0.1f);
                if (RideElectricMinRocketRecoverStateTime == 0) {
                    Player.fullRotation = 0;
                }
            }

            if (PendingDashVelocity.HasValue) {
                Player.velocity = PendingDashVelocity.Value;
                PendingDashVelocity = null;
                RotationResetCounter = 0;
            }

            if (IsRotatingDuringDash) {
                Player.fullRotation += Player.velocity.Length() * PendingDashRotSpeedMode * RotationDirection;
                Player.fullRotationOrigin = Player.Size / 2;
                PendingDashRotSpeedMode = 0.015f;
            }

            if (RotationResetCounter > 0) {
                IsRotatingDuringDash = false;
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

            if (DontUseItemTime > 0) {
                DontUseItemTime--;
            }
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
            if (ThermalGenerationActiveTime > 0) {
                ThermalGenerationActiveTime--;
            }

            if (!IsJusticeUnveiled) {
                JusticeUnveiledCharges = 0;
            }

            if (JusticeUnveiledCooldown > 0) {
                JusticeUnveiledCooldown--;
            }

            PlayerPositionChange = oldPlayerPositionChange.To(Player.position);
            oldPlayerPositionChange = Player.position;
        }

        public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo) {
            if (drawInfo.shadow != 0f) {
                return;
            }

            Player player = drawInfo.drawPlayer;
            Texture2D value = null;
            Rectangle frame = new Rectangle(0, 0, 1, 1);
            Vector2 orig = Vector2.Zero;
            Vector2 offsetPos = Vector2.Zero;
            Vector2 drawPos;
            float size = 1;
            float offsetRot = 0;
            int frameindex = 0;
            SpriteEffects spriteEffects = Player.direction == player.gravDir ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            SpecialDrawPositionOffset = Main.OffsetsPlayerHeadgear[player.bodyFrame.Y / player.bodyFrame.Height] * player.Directions;
            SpecialDrawPositionOffset.Y -= 2 * player.gravDir;//乘以一个重力矫正，这是一个无视偏转的值，所以需要考虑重力方向

            if (RideElectricMinRocket) {//添加小火箭相关的绘制
                drawPos.X = (int)(((int)player.position.X) - Main.screenPosition.X + (player.width / 2) - (9 * player.direction)) - 4f * player.direction + offsetPos.X;
                drawPos.Y = (int)(((int)player.position.Y) - Main.screenPosition.Y + (player.height / 2) + 2f * player.gravDir - 8f * player.gravDir) + offsetPos.Y * player.gravDir;
                drawPos.Y += SpecialDrawPositionOffset.Y;
                value = TextureAssets.Projectile[ModContent.ProjectileType<ElectricMinRocketHeld>()].Value;
                frame = value.GetRectangle();
                orig = value.GetOrig();
                DrawData electricMinRocketDraw = new DrawData(value, drawPos, frame, drawInfo.colorArmorBody, player.bodyRotation + offsetRot, orig, size, spriteEffects, 0) {
                    shader = 0,
                };
                drawInfo.DrawDataCache.Add(electricMinRocketDraw);
            }

            Item item = player.GetItem();
            if (HeldStyle >= 0) {
                player.headFrame.Y = player.headFrame.Height * HeldStyle;
                player.bodyFrame.Y = player.bodyFrame.Height * HeldStyle;
            }
            if (!player.frozen && !item.IsAir && !player.dead && item.type > ItemID.None) {
                if (player.gravDir < 0) {
                    offsetRot = MathHelper.Pi;
                }

                if (GlobalBow.IsBow || GlobalBow.IsArrow) {
                    int maxframe = 4;
                    if (player.velocity.Y == 0f && player.velocity.X != 0) {
                        frameindex = (int)(Main.GameUpdateCount / 4 % maxframe);
                    }
                    value = CWRAsset.Quiver_back_Asset.Value;
                    frame = value.GetRectangle(frameindex, maxframe);
                    if (HeldStyle >= 0) {
                        frame = value.GetRectangle(0, maxframe);
                    }
                    orig = frame.Size() / 2;
                }

                if (item.type == DarkFrostSolstice.ID) {
                    value = CWRAsset.IceGod_back_Asset.Value;
                    frame = value.GetRectangle();
                    orig = value.GetOrig();
                    float sengs = Main.GameUpdateCount * 0.05f;
                    offsetPos = new Vector2(player.direction * 8, MathF.Sin(sengs) * 5 - 16);
                }

                if (value == null) {
                    return;
                }

                drawPos.X = (int)(((int)player.position.X) - Main.screenPosition.X + (player.width / 2) - (9 * player.direction)) - 4f * player.direction + offsetPos.X;
                drawPos.Y = (int)(((int)player.position.Y) - Main.screenPosition.Y + (player.height / 2) + 2f * player.gravDir - 8f * player.gravDir) + offsetPos.Y * player.gravDir;
                drawPos.Y += SpecialDrawPositionOffset.Y;
                DrawData howDoIDrawThings = new DrawData(value, drawPos, frame, drawInfo.colorArmorBody, player.bodyRotation + offsetRot, orig, size, spriteEffects, 0) {
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
        public void GetScreenShake(float mode) {
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
                NetworkText networkText = CWRLocText.Instance.HellfireExplosion_DeadLang_Text.ToNetworkText(Player.name);
                damageSource = PlayerDeathReason.ByCustomReason(networkText);
            }
            if (SoulfireExplosion) {
                NetworkText networkText = CWRLocText.Instance.SoulfireExplosion_DeadLang_Text.ToNetworkText(Player.name);
                damageSource = PlayerDeathReason.ByCustomReason(networkText);
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
                //检查投射物是否处于激活状态，是否属于玩家所有，并且是否隐藏
                if (!p.active || p.owner != Player.whoAmI || !p.hide) {
                    continue; //如果当前投射物不符合条件，则跳过并检查下一个投射物
                }
                if (p.ModProjectile as T != null) {
                    Player.heldProj = p.whoAmI;
                    if (p.ModProjectile is T instance) {
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
