using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers;
using CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses;
using CalamityOverhaul.Content.Items.Melee.WeaverGrievanceses;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.LegendWeapon;
using CalamityOverhaul.Content.NPCs.Modifys.Crabulons;
using CalamityOverhaul.Content.Projectiles;
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
        public string LocalizationCategory => "UI";

        public static LocalizedText HellfireExplosionDeathReason { get; private set; }
        public static LocalizedText SoulfireExplosionDeathReason { get; private set; }

        public override void SetStaticDefaults() {
            HellfireExplosionDeathReason = this.GetLocalization(nameof(HellfireExplosionDeathReason),
                () => "{0}在地狱的烈火中化为灰烬");
            SoulfireExplosionDeathReason = this.GetLocalization(nameof(SoulfireExplosionDeathReason),
                () => "{0}的灵魂在火焰中升华");
        }

        #region Data
        /// <summary>屏幕振动强度</summary>
        public float ScreenShakeValue;
        /// <summary>火力发电活跃帧数</summary>
        public int ThermalGenerationActiveTime;
        /// <summary>了解风力</summary>
        public bool UnderstandWindGriven;
        /// <summary>了解风力 MK2</summary>
        public bool UnderstandWindGrivenMK2;
        /// <summary>电动火箭骑乘中</summary>
        public bool RideElectricMinRocket;
        /// <summary>卸乘电动火箭恢复周期(帧)</summary>
        public int RideElectricMinRocketRecoverStateTime;
        /// <summary>实时绘制位置矫正</summary>
        internal Vector2 SpecialDrawPositionOffset;
        /// <summary>玩家位置变化量</summary>
        public Vector2 PlayerPositionChange;
        /// <summary>上一帧玩家位置变化量</summary>
        private Vector2 oldPlayerPositionChange;
        /// <summary>地狱炎爆</summary>
        public bool HellfireExplosion;
        /// <summary>灵魂火</summary>
        public bool SoulfireExplosion;
        /// <summary>毁灭者之主</summary>
        public bool DestroyerOwner;
        /// <summary>穿戴正义显现</summary>
        public bool IsJusticeUnveiled;
        /// <summary>正义显现触发机会次数</summary>
        public int JusticeUnveiledCharges;
        /// <summary>正义显现触发冷却(帧)</summary>
        public int JusticeUnveiledCooldown;
        /// <summary>待下帧冲刺速度，非 null 生效</summary>
        public Vector2? PendingDashVelocity { get; set; } = null;
        /// <summary>翻滚旋转速度倍率</summary>
        public float PendingDashRotSpeedMode = 0.015f;
        /// <summary>冲刺中旋转</summary>
        public bool IsRotatingDuringDash { get; set; }
        /// <summary>冲刺旋转方向，1 顺时针 -1 逆时针</summary>
        public float RotationDirection { get; set; } = 1f;
        /// <summary>冲刺冷却剩余帧数</summary>
        public float DashCooldownCounter { get; set; }
        /// <summary>旋转复位剩余帧数</summary>
        public float RotationResetCounter { get; set; }
        /// <summary>旋转复位持续帧数</summary>
        private const float RotationResetDuration = 15f;
        /// <summary>自定义冷却剩余帧数</summary>
        public int CustomCooldownCounter;
        /// <summary>掠袭者冲刺后强化射击就绪</summary>
        public bool RaiderGunDashReady;
        /// <summary>掠袭者共享冲刺冷却(帧)</summary>
        public int RaiderGunDashCooldown;
        /// <summary>弹射平台摔落免伤剩余帧数,期间每帧重置摔落起点</summary>
        public int LauncherGraceTime;
        /// <summary>残酷遗物双击位移技按方向记录的消费帧戳，下标同原版 doubleTapCardinalTimer(0下1上2右3左)</summary>
        private readonly int[] relicDoubleTapFrame = new int[4];
        #endregion

        public CWRPlayer CloneCWRPlayer(CWRPlayer cwr) {
            cwr.ScreenShakeValue = ScreenShakeValue;
            cwr.ThermalGenerationActiveTime = ThermalGenerationActiveTime;
            cwr.UnderstandWindGriven = UnderstandWindGriven;
            cwr.UnderstandWindGrivenMK2 = UnderstandWindGrivenMK2;
            cwr.RideElectricMinRocket = RideElectricMinRocket;
            cwr.RideElectricMinRocketRecoverStateTime = RideElectricMinRocketRecoverStateTime;
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
            cwr.IsRotatingDuringDash = IsRotatingDuringDash;
            cwr.RotationDirection = RotationDirection;
            cwr.DashCooldownCounter = DashCooldownCounter;
            cwr.RotationResetCounter = RotationResetCounter;
            cwr.CustomCooldownCounter = CustomCooldownCounter;
            cwr.RaiderGunDashReady = RaiderGunDashReady;
            cwr.RaiderGunDashCooldown = RaiderGunDashCooldown;
            cwr.LauncherGraceTime = LauncherGraceTime;
            return cwr;
        }

        public override ModPlayer Clone(Player newEntity) => CloneCWRPlayer((CWRPlayer)base.Clone(newEntity));

        public override void Initialize() {
            ThermalGenerationActiveTime = 0;
            Reset();
        }

        public override void ResetEffects() => Reset();

        private void Reset() {
            HellfireExplosion = false;
            IsJusticeUnveiled = false;
            DestroyerOwner = false;
            RideElectricMinRocket = false;
        }

        /// <summary>残酷遗物双击位移技的按方向消费闩，同帧同方向仅首个调用者获得执行权</summary>
        public bool TryConsumeRelicDoubleTap(int dir) {
            if (relicDoubleTapFrame[dir] == (int)Main.GameUpdateCount) {
                return false;
            }
            relicDoubleTapFrame[dir] = (int)Main.GameUpdateCount;
            return true;
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

        public override void PostUpdateMiscEffects() {
            if (Main.zenithWorld) {
                if (Player.GetItem().type == ModContent.ItemType<WeaverGrievances>()) {
                    WeaverGrievances.SpwanInOwnerDust(Player);
                }
            }
        }

        public override void OnEnterWorld() {
            SpearOfLonginus.ZenithWorldAsset();

            LegendData.ResetInventory(Player);

            oldPlayerPositionChange = Player.position;
            PlayerPositionChange = Vector2.Zero;
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

            if (DashCooldownCounter > 0) {
                DashCooldownCounter--;
            }

            if (CustomCooldownCounter > 0) {
                CustomCooldownCounter--;
            }

            if (RaiderGunDashCooldown > 0) {
                RaiderGunDashCooldown--;
            }
        }

        public override void PostUpdate() {
            if (ThermalGenerationActiveTime > 0) {
                ThermalGenerationActiveTime--;
            }

            //弹射免伤期内摔落起点始终跟脚,落地时结算不出高度差
            if (LauncherGraceTime > 0) {
                LauncherGraceTime--;
                Player.fallStart = (int)(Player.position.Y / 16f);
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
            if (!player.frozen && !item.IsAir && !player.dead && item.type > ItemID.None) {
                if (player.gravDir < 0) {
                    offsetRot = MathHelper.Pi;
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
            if (ScreenShakeValue > 0f) {
                Main.screenPosition += Main.rand.NextVector2Circular(ScreenShakeValue, ScreenShakeValue);
                ScreenShakeValue = MathHelper.Clamp(ScreenShakeValue - 0.185f, 0f, 20f);
            }
        }
        /// <summary>屏幕震动强度</summary>
        public void GetScreenShake(float mode) {
            if (!CWRClientConfig.Instance.ScreenVibration) {
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
                //死时下马，骑手端自广播
                crabulonPlayer.MountCrabulon?.CloseMount();
                crabulonPlayer.IsMount = false;
                ModifyCrabulon.mountPlayerHeldProj = -1;
                crabulonPlayer.MountCrabulon = null;
                CrabulonPlayer.CloseDuringDash(Player);
            }
            return true;
        }

        /// <summary>隐藏 held 弹幕实例</summary>
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
        /// <summary>手持武器弹幕展示态</summary>
        internal bool HeldWeaponInDisplay() {
            return TryGetHeldProjInds(out BaseHeldGun heldGun) && heldGun.OnHandheldDisplayBool;
        }
    }
}
