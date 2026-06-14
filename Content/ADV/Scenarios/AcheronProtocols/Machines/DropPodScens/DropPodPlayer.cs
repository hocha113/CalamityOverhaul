using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Graphics;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.DropPodScens
{
    /// <summary>空降仓演出玩家覆写：在空降仓子世界中隐藏玩家绘制，管理坠落状态， 并生成 <see cref="DropPodActor"/> 实体来</summary>
    internal class DropPodPlayer : PlayerOverride
    {
        /// <summary>
        /// 坠落演出是否激活
        /// </summary>
        public bool DropPodActive;
        /// <summary>
        /// 坠落累计计时器
        /// </summary>
        public int DropTimer;
        /// <summary>
        /// 空降仓水平偏移量（屏幕像素），由玩家方向键控制
        /// </summary>
        public float HorizontalOffset;
        /// <summary>
        /// 空降仓倾斜角度，随水平移动产生
        /// </summary>
        public float TiltAngle;

        private const float MoveSpeed = 4.5f;
        private const float MaxOffset = 680f;
        private const float TiltMax = 0.12f;
        private const float Deceleration = 0.92f;
        private const float TiltLerp = 0.1f;

        private float moveVelocity;

        /// <summary>上一帧 2 键状态</summary>
        private bool prevD2Pressed;

        public override void PostUpdate() {
            if (!DropPodWorld.Active) {
                if (DropPodActive) {
                    DropPodActive = false;
                    DropTimer = 0;
                    HorizontalOffset = 0f;
                    TiltAngle = 0f;
                    moveVelocity = 0f;
                }
                return;
            }

            DropPodActive = true;
            DropTimer++;

            //读取左右方向键输入
            bool moveLeft = Player.controlLeft;
            bool moveRight = Player.controlRight;

            if (moveLeft && !moveRight) {
                moveVelocity -= MoveSpeed * 0.3f;
            }
            else if (moveRight && !moveLeft) {
                moveVelocity += MoveSpeed * 0.3f;
            }
            else {
                moveVelocity *= Deceleration;
            }

            moveVelocity = MathHelper.Clamp(moveVelocity, -MoveSpeed, MoveSpeed);
            HorizontalOffset += moveVelocity;
            HorizontalOffset = MathHelper.Clamp(HorizontalOffset, -MaxOffset, MaxOffset);

            //根据移动速度计算倾斜角度
            float targetTilt = moveVelocity / MoveSpeed * TiltMax;
            TiltAngle = MathHelper.Lerp(TiltAngle, targetTilt, TiltLerp);

            //── 调试：按 2 键触发坠入环节 ─────────────────────────────────────────
            bool d2 = Keyboard.GetState().IsKeyDown(Keys.D2);
            if (d2 && !prevD2Pressed) {
                DropPodActor.TriggerLandingPhase();
            }
            prevD2Pressed = d2;
            //──────────────────────────────────────────────────────────────────────

            //锁定玩家位置在世界中央，无重力
            Player.position = new Vector2(
                DropPodWorld.Instance.Width * 16 / 2f - Player.width / 2f,
                DropPodWorld.Instance.Height * 16 / 2f - Player.height / 2f);
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
        }

        /// <summary>

        /// 隐藏玩家绘制：参考HalibutPlayer和CrabulonPlayer的模式

        /// </summary>
        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            if (!DropPodActive) return true;

            //移除本玩家的绘制
            players = players.Where(p => p.whoAmI != Player.whoAmI);
            return true;
        }
    }
}
