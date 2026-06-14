using InnoVault.GameSystem;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Graphics;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.LandingScens
{
    /// <summary>
    /// 机械世界空降仓着陆演出：玩家覆写 在着陆阶段隐藏玩家绘制，锁定玩家操控，直到玩家从空降仓弹出
    /// </summary>
    internal class MachineWorldLandingPlayer : PlayerOverride
    {
        /// <summary>
        /// 着陆演出是否处于激活状态（玩家尚未弹出）
        /// </summary>
        public bool LandingActive;

        /// <summary>

        /// 弹出动画是否正在播放

        /// </summary>
        public bool EjectAnimating;

        /// <summary>

        /// 弹出动画计时器

        /// </summary>
        public int EjectTimer;

        /// <summary>

        /// 玩家是否在本帧按下了左键（在清除controlUseItem之前捕获） 供MachineWorldLandingActor读取

        /// </summary>
        public bool ClickedThisFrame;

        /// <summary>

        /// 脱离物块阶段每帧上升的像素速度

        /// </summary>
        private const float EscapeSpeed = 8f;

        /// <summary>

        /// 脱离物块阶段的最大持续帧数（安全上限，避免无限循环）

        /// </summary>
        private const int MaxEscapeFrames = 300;

        /// <summary>

        /// 脱出后的弹射恢复帧数

        /// </summary>
        private const int LaunchDuration = 30;

        /// <summary>

        /// 是否已经脱离物块进入空中

        /// </summary>
        private bool hasReachedOpenAir;

        /// <summary>

        /// 弹出水平偏移

        /// </summary>
        private float ejectHorizontalOffset;

        public override void PostUpdate() {
            if (!MachineWorld.Active) {
                if (LandingActive || EjectAnimating) {
                    LandingActive = false;
                    EjectAnimating = false;
                    EjectTimer = 0;
                    hasReachedOpenAir = false;
                }
                return;
            }

            if (LandingActive && !EjectAnimating) {
                ClickedThisFrame = Player.controlUseItem;
                Player.velocity = Vector2.Zero;
                Player.fallStart = (int)(Player.position.Y / 16f);
                Player.controlLeft = false;
                Player.controlRight = false;
                Player.controlUp = false;
                Player.controlDown = false;
                Player.controlJump = false;
                Player.controlUseItem = false;
            }
            else {
                ClickedThisFrame = false;
            }

            if (EjectAnimating) {
                EjectTimer++;

                if (!hasReachedOpenAir) {
                    //阶段1：脱离物块：持续向上移动直到玩家位于空中
                    Player.position.Y -= EscapeSpeed;
                    Player.velocity = Vector2.Zero;
                    Player.fallStart = (int)(Player.position.Y / 16f);
                    //禁止物理引擎干扰
                    Player.controlLeft = false;
                    Player.controlRight = false;
                    Player.controlUp = false;
                    Player.controlDown = false;
                    Player.controlJump = false;
                    Player.controlUseItem = false;

                    //检测玩家当前位置是否已经脱离实心物块
                    bool inSolid = Collision.SolidCollision(Player.position, Player.width, Player.height);
                    if (!inSolid || EjectTimer >= MaxEscapeFrames) {
                        hasReachedOpenAir = true;
                        EjectTimer = 0;
                        //给予向上的弹射速度
                        Player.velocity = new Vector2(ejectHorizontalOffset, -10f);
                        Player.fallStart = (int)(Player.position.Y / 16f);
                    }
                }
                else {
                    //阶段2：弹射恢复：让物理引擎逐渐接管
                    float progress = (float)EjectTimer / LaunchDuration;
                    if (progress < 0.5f) {
                        Player.fallStart = (int)(Player.position.Y / 16f);
                    }

                    if (EjectTimer >= LaunchDuration) {
                        EjectAnimating = false;
                        LandingActive = false;
                        EjectTimer = 0;
                        hasReachedOpenAir = false;
                    }
                }
            }
        }

        /// <summary>

        /// 触发弹出动画

        /// </summary>
        public void TriggerEject(Vector2 podCenter) {
            if (!LandingActive || EjectAnimating) return;

            EjectAnimating = true;
            EjectTimer = 0;
            hasReachedOpenAir = false;
            //将玩家移动到空降仓中心位置
            Player.position = podCenter - Player.Size / 2f;
            ejectHorizontalOffset = Main.rand.NextFloat(-3f, 3f);
        }

        /// <summary>

        /// 隐藏玩家绘制：在着陆阶段玩家不可见

        /// </summary>
        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            if (LandingActive && !EjectAnimating) {
                players = players.Where(p => p.whoAmI != Player.whoAmI);
            }
            return true;
        }
    }
}
