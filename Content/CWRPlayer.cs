using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.LegendWeapon;
using CalamityOverhaul.Content.NPCs.Modifys.Crabulons;
using CalamityOverhaul.Content.Projectiles.Others;
using CalamityOverhaul.Content.RangedModify;
using CalamityOverhaul.Content.RangedModify.Core;
using CalamityOverhaul.OtherMods.ImproveGame;
using CalamityOverhaul.OtherMods.SubWorld;
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
    public class CWRPlayer : ModPlayer, ILocalizedModType
    {
        public string LocalizationCategory => "UI.CWRPlayer";

        public static LocalizedText Error_HookFailure { get; private set; }
        public static LocalizedText OnEnterWorld_ImproveGameWarning { get; private set; }
        public static LocalizedText HellfireExplosionDeathReason { get; private set; }
        public static LocalizedText SoulfireExplosionDeathReason { get; private set; }

        public override void SetStaticDefaults() {
            Error_HookFailure = this.GetLocalization(nameof(Error_HookFailure), () =>
                """
                个钩子失效了，为了游戏正常，请关闭游戏并重新进入，
                如果这仍旧没有恢复正常，请带上导出的日志寻找模组开发者
                """);
            OnEnterWorld_ImproveGameWarning = this.GetLocalization(nameof(OnEnterWorld_ImproveGameWarning), () =>
                """
                检测到您安装了[更好的体验]，但您的[更好的体验]版本过旧
                为了保证游戏正常运行，请保证[更好的体验]版本不低于1.7.1.7
                """);
            HellfireExplosionDeathReason = this.GetLocalization(nameof(HellfireExplosionDeathReason),
                () => "{0}在地狱的烈火中化为灰烬");
            SoulfireExplosionDeathReason = this.GetLocalization(nameof(SoulfireExplosionDeathReason),
                () => "{0}的灵魂在火焰中升华");
        }

        #region Data
        /// <summary>是否拥有大修宝典</summary>
        public bool HasOverhaulTheBibleBook;
        /// <summary>摄像头位置额外矫正值</summary>
        public Vector2 OffsetScreenPos;
        /// <summary>屏幕振动强度</summary>
        public float ScreenShakeValue;
        /// <summary>火力发电活跃帧数</summary>
        public int ThermalGenerationActiveTime;
        /// <summary>是否开启超级合成台</summary>
        public bool SupertableUIStartBool;
        /// <summary>是否坐在大排档塑料椅</summary>
        public bool InFoodStallChair;
        /// <summary>是否手持鬼妖</summary>
        public bool HeldMurasamaBool;
        /// <summary>是否正在进行终结技</summary>
        public bool EndSkillEffectStartBool;
        /// <summary>同 <see cref="Player.mouseInterface"/> 的 UI 鼠标占用</summary>
        public bool UIMouseInterface => Player.mouseInterface;
        /// <summary>是否了解风力</summary>
        public bool UnderstandWindGriven;
        /// <summary>是否了解风力 MK2</summary>
        public bool UnderstandWindGrivenMK2;
        /// <summary>是否使用电动火箭</summary>
        public bool RideElectricMinRocket;
        /// <summary>卸乘电动火箭恢复周期(帧)</summary>
        public int RideElectricMinRocketRecoverStateTime;
        /// <summary>手持状态</summary>
        public int HeldStyle;
        /// <summary>升龙技充能</summary>
        public int RisingDragonCharged;
        /// <summary>Tram 归属</summary>
        public int TramTPContrType = -1;
        /// <summary>Compressor 归属</summary>
        public int CompressorContrType = -1;
        /// <summary>欧米茄指示箭头计数器</summary>
        public int InspectOmigaTime;
        /// <summary>站平台帧数，&gt;0 时无重力</summary>
        public int ReceivingPlatformTime;
        /// <summary>禁暗影克隆体剩余帧数，每帧减一</summary>
        public int DontHasSemberDarkMasterCloneTime;
        /// <summary>实时绘制位置矫正</summary>
        internal Vector2 SpecialDrawPositionOffset;
        /// <summary>玩家位置变化量</summary>
        public Vector2 PlayerPositionChange;
        /// <summary>上一帧玩家位置变化量</summary>
        public Vector2 oldPlayerPositionChange;
        /// <summary>是否有地狱炎爆 debuff</summary>
        public bool HellfireExplosion;
        /// <summary>是否有灵魂火 debuff</summary>
        public bool SoulfireExplosion;
        /// <summary>毁灭者之主</summary>
        public bool DestroyerOwner;
        /// <summary>是否穿戴英雄无冕</summary>
        public bool IsUnsunghero;
        /// <summary>是否穿戴正义显现</summary>
        public bool IsJusticeUnveiled;
        /// <summary>正义显现触发机会次数</summary>
        public int JusticeUnveiledCharges;
        /// <summary>正义显现触发冷却(帧)</summary>
        public int JusticeUnveiledCooldown;
        /// <summary>待下帧应用的冲刺速度，非 null 时生效</summary>
        public Vector2? PendingDashVelocity { get; set; } = null;
        /// <summary>翻滚旋转速度倍率</summary>
        public float PendingDashRotSpeedMode = 0.015f;
        /// <summary>减速剩余帧数</summary>
        public float DecelerationCounter { get; set; }
        /// <summary>冲刺中是否旋转</summary>
        public bool IsRotatingDuringDash { get; set; }
        /// <summary>冲刺旋转方向，1 顺时针 -1 逆时针</summary>
        public float RotationDirection { get; set; } = 1f;
        /// <summary>冲刺冷却剩余帧数</summary>
        public float DashCooldownCounter { get; set; }
        /// <summary>旋转复位剩余帧数</summary>
        public float RotationResetCounter { get; set; }
        /// <summary>旋转复位持续帧数</summary>
        public float RotationResetDuration { get; set; } = 15;
        /// <summary>自定义冷却剩余帧数</summary>
        public int CustomCooldownCounter;
        /// <summary>掠袭者：冲刺后强化射击就绪</summary>
        public bool RaiderGunDashReady;
        /// <summary>掠袭者共享冲刺冷却(帧)，每帧减一</summary>
        public int RaiderGunDashCooldown;
        /// <summary>禁使用物品剩余帧数，每帧减一</summary>
        public int DontUseItemTime;
        /// <summary>抬棺人下一发弩箭伤害倍率，默认 1</summary>
        public float PallbearerNextArrowDamageMult = 1;

        [Obsolete("已经过时，目前保留仅用作外部联动兼容")]
        public float PressureIncrease;
        #endregion

        public CWRPlayer CloneCWRPlayer(CWRPlayer cwr) {
            cwr.HasOverhaulTheBibleBook = HasOverhaulTheBibleBook;
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
            cwr.ReceivingPlatformTime = ReceivingPlatformTime;
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
            cwr.RaiderGunDashReady = RaiderGunDashReady;
            cwr.RaiderGunDashCooldown = RaiderGunDashCooldown;
            cwr.DontUseItemTime = DontUseItemTime;
            cwr.PallbearerNextArrowDamageMult = PallbearerNextArrowDamageMult;
            return cwr;
        }

        public override ModPlayer Clone(Player newEntity) => CloneCWRPlayer((CWRPlayer)base.Clone(newEntity));

        public override void Initialize() {
            TramTPContrType = 0;
            ReceivingPlatformTime = 0;
            DontUseItemTime = 0;
            ThermalGenerationActiveTime = 0;
            PallbearerNextArrowDamageMult = 1;
            Reset();
        }

        public override void ResetEffects() => Reset();

        private void Reset() {
            OffsetScreenPos = Vector2.Zero;
            HeldStyle = -1;
            IsUnsunghero = false;
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
            if (Main.zenithWorld) {
                if (Player.GetItem().type == ModContent.ItemType<WeaverGrievances>()) {
                    WeaverGrievances.SpwanInOwnerDust(Player);
                }
            }
        }

        private void Information() {
            if (SubWorldRef.AnyActiveSubWorld()) {
                return; //子世界不弹兼容性提示
            }

            if (!VaultHook.CheckHookStatus(out int num)) {
                string hookDownText1 = $"{num} " + Error_HookFailure.Value;
                VaultUtils.Text(hookDownText1, Color.Red);
            }

            if (!ImproveRef.Suitableversion_improveGame && CWRMod.Instance.improveGame != null) {
                string improvGameText = OnEnterWorld_ImproveGameWarning.Value;
                SpwanTextProj.New(Player, () => VaultUtils.Text(improvGameText, Color.Red), 210);
                CWRMod.Instance.Logger.Info(improvGameText);
            }
        }

        public override void OnEnterWorld() {
            Information();

            //进世界 RAM 回满(LoadData 已读基础值)

            SpearOfLonginus.ZenithWorldAsset();

            LegendData.ResetInventory(Player);

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

            if (RaiderGunDashCooldown > 0) {
                RaiderGunDashCooldown--;
            }

            if (ReceivingPlatformTime > 0) {
                Player.gravity = 0;
                if (Player.velocity.Y > 0) {
                    Player.velocity.Y = 0;
                }
                ReceivingPlatformTime--;
            }
        }

        public override void PostUpdate() {

            if (DontUseItemTime > 0) {
                DontUseItemTime--;
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
            SpecialDrawPositionOffset.Y -= 2 * player.gravDir;

            if (RideElectricMinRocket) {
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
        /// <summary>设置屏幕震动强度</summary>
        public void GetScreenShake(float mode) {
            if (!CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
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
                NetworkText networkText = HellfireExplosionDeathReason.ToNetworkText(Player.name);
                damageSource = PlayerDeathReason.ByCustomReason(networkText);
            }
            if (SoulfireExplosion) {
                NetworkText networkText = SoulfireExplosionDeathReason.ToNetworkText(Player.name);
                damageSource = PlayerDeathReason.ByCustomReason(networkText);
            }
            if (Player.TryGetOverride<CrabulonPlayer>(out var crabulonPlayer)) {
                //死亡时立即下马，骑手端自动广播
                crabulonPlayer.MountCrabulon?.CloseMount();
                crabulonPlayer.IsMount = false;
                ModifyCrabulon.mountPlayerHeldProj = -1;
                crabulonPlayer.MountCrabulon = null;
                CrabulonPlayer.CloseDuringDash(Player);
            }
            return true;
        }

        /// <summary>取玩家当前隐藏的 held 弹幕实例</summary>
        internal bool TryGetHeldProjInds<T>(out T result) where T : class {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Player.whoAmI || !p.hide) {
                    continue;
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
        /// <summary>手持武器弹幕是否处于展示态</summary>
        internal bool HeldWeaponInDisplay() {
            return TryGetHeldProjInds(out BaseHeldGun heldGun) && heldGun.OnHandheldDisplayBool;
        }
    }
}
