using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Launchers
{
    /// <summary>
    /// 弹射平台TP:检测站进台面的玩家并按设定方向/力度抛出。<br/>
    /// 检测与扣电各端同跑(漂移靠周期锚定纠偏),速度只由玩家拥有端写入(玩家运动归属契约)
    /// </summary>
    internal class PlayerLauncherTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<PlayerLauncherTile>();
        public override int TargetItem => ModContent.ItemType<PlayerLauncher>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 500;

        #region 常量

        //基础弹射能耗,最终成本再叠加力度
        internal const float BaseConsumeUE = 8;
        //同一玩家两次弹射的最短间隔(帧)
        private const int LaunchCooldownTicks = 24;
        //弹射后的摔落免伤时长(帧)
        private const int GraceTime = 240;

        #endregion

        #region 字段

        //弹射参数
        internal bool Enabled = true;
        internal float LaunchDirection = -90f;
        internal float LaunchPower = 14f;

        //状态
        internal float GlowIntensity;
        private int textIdleTime;
        //各玩家的弹射冷却;每端独立检测同一批玩家,故无需同步
        private readonly int[] launchCooldowns = new int[Main.maxPlayers];

        #endregion

        #region 属性

        internal float LaunchCost => BaseConsumeUE + LaunchPower;
        internal Vector2 LaunchVelocity => MathHelper.ToRadians(LaunchDirection).ToRotationVector2() * LaunchPower;
        public Vector2 IndicatorPosition => CenterInWorld + new Vector2(0, -8);

        #endregion

        #region 数据同步与存档

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(Enabled);
            data.Write(LaunchDirection);
            data.Write(LaunchPower);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            Enabled = reader.ReadBoolean();
            LaunchDirection = reader.ReadSingle();
            LaunchPower = reader.ReadSingle();
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["_Enabled"] = Enabled;
            tag["_LaunchDirection"] = LaunchDirection;
            tag["_LaunchPower"] = LaunchPower;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (tag.TryGet("_Enabled", out bool enabled)) {
                Enabled = enabled;
            }
            if (tag.TryGet("_LaunchDirection", out float direction)) {
                LaunchDirection = direction;
            }
            if (tag.TryGet("_LaunchPower", out float power)) {
                LaunchPower = power;
            }
        }

        #endregion

        #region 更新逻辑

        public override void UpdateMachine() {
            if (textIdleTime > 0) {
                textIdleTime--;
            }
            for (int i = 0; i < launchCooldowns.Length; i++) {
                if (launchCooldowns[i] > 0) {
                    launchCooldowns[i]--;
                }
            }

            bool charged = MachineData.UEvalue >= LaunchCost;
            bool ready = Enabled && charged;
            GlowIntensity = ready
                ? Math.Min(1f, GlowIntensity + 0.05f)
                : Math.Max(0f, GlowIntensity - 0.05f);

            if (!Enabled) {
                return;
            }

            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.gravDir < 0) {
                    continue;
                }
                if (launchCooldowns[player.whoAmI] > 0) {
                    continue;
                }
                if (!IsStandingOnPad(player)) {
                    continue;
                }

                if (!charged) {
                    if (textIdleTime <= 0) {
                        //并行阶段CombatText生成延迟到主线程执行(串行阶段立即执行)
                        Defer(() => CombatText.NewText(HitBox, new Color(140, 200, 255), PlayerLauncher.NoEnergyText.Value));
                        textIdleTime = 180;
                    }
                    continue;
                }

                launchCooldowns[player.whoAmI] = LaunchCooldownTicks;
                MachineData.UEvalue -= LaunchCost;
                charged = MachineData.UEvalue >= LaunchCost;
                PerformLaunch(player.whoAmI);
            }
        }

        /// <summary>站姿判定:脚底与台座同层且没在上升途中</summary>
        private bool IsStandingOnPad(Player player) {
            if (player.velocity.Y < 0f) {
                return false;
            }
            if (!player.Hitbox.Intersects(HitBox)) {
                return false;
            }
            return Math.Abs(player.Bottom.Y - HitBox.Bottom) < 10f;
        }

        private void PerformLaunch(int playerWhoAmI) {
            Vector2 velocity = LaunchVelocity;
            //速度写入归拥有端;音效与粒子每端都放,近处旁观者同样看到弹射
            Defer(() => {
                Player target = Main.player[playerWhoAmI];
                if (!target.active || target.dead) {
                    return;
                }

                if (playerWhoAmI == Main.myPlayer) {
                    target.velocity = velocity;
                    target.fallStart = (int)(target.position.Y / 16f);
                    target.CWR().LauncherGraceTime = GraceTime;
                }

                if (VaultUtils.isServer) {
                    return;
                }
                SoundEngine.PlaySound(SoundID.Item56 with { Pitch = 0.4f, Volume = 0.6f }, CenterInWorld);
                for (int i = 0; i < 14; i++) {
                    Vector2 dustVel = velocity.SafeNormalize(-Vector2.UnitY)
                        .RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(2f, 6f);
                    Dust dust = Dust.NewDustDirect(HitBox.TopLeft(), HitBox.Width, 8,
                        DustID.Electric, dustVel.X, dustVel.Y, 100, default, 0.9f);
                    dust.noGravity = true;
                }
            });
        }

        #endregion

        #region 交互与绘制

        public void RightClickByTile() {
            var ui = UIHandleLoader.GetUIHandleOfType<PlayerLauncherUI>();
            ui?.Interactive(this);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();

            if (GlowIntensity > 0.01f || PlayerLauncherUI.Instance?.Station == this) {
                DrawDirectionIndicator(spriteBatch);
            }
        }

        private void DrawDirectionIndicator(SpriteBatch spriteBatch) {
            var arrowAsset = Throwers.Thrower.InputArrow;
            if (arrowAsset == null) {
                return;
            }

            float radians = MathHelper.ToRadians(LaunchDirection);
            Vector2 drawPos = IndicatorPosition - Main.screenPosition + radians.ToRotationVector2() * 24f;
            Color arrowColor = PlayerLauncher.Tint * MathHelper.Clamp(0.35f + GlowIntensity * 0.65f, 0f, 1f);

            spriteBatch.Draw(
                arrowAsset.Value,
                drawPos,
                null,
                arrowColor,
                radians,
                arrowAsset.Value.Size() / 2f,
                0.8f,
                SpriteEffects.None,
                0f
            );
        }

        #endregion
    }
}
