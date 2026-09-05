using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.UIs.RadialWheels;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 教学卡的临时键：某一步要用的键位没绑定时，卡在讲这一步期间可以用该键的默认位代替，
    /// 玩家先把操作做出来看演示，之后再去设置里绑。与键位路径进同一受理点，不另写一套逻辑。
    /// 异化键（鬼雨/鬼梦两步）游戏逻辑自带原生中键兜底，这里不管
    /// </summary>
    internal partial class KikasaHudLead
    {
        //临时键的按住态，转盘步需要松开沿
        private static bool fallbackHeld;

        /// <summary>本步对应的键位；Rain/Dream 共用异化键</summary>
        private static ModKeybind ActionKeyFor(Phase phase) => phase switch {
            Phase.Domain or Phase.Close => CWRKeySystem.Legend_Domain,
            Phase.Sink => CWRKeySystem.Kikasa_Sink,
            Phase.Panorama => CWRKeySystem.Legend_UIControl,
            Phase.Wheel => CWRKeySystem.RadialWheel_Key,
            Phase.Restart => CWRKeySystem.Legend_Restart,
            _ => CWRKeySystem.Kikasa_DomainMutate,
        };

        /// <summary>键未绑定时本步的临时键：取各键的注册默认位</summary>
        private static Keys FallbackKeyFor(Phase phase) => phase switch {
            Phase.Domain or Phase.Close => Keys.Q,
            Phase.Sink => Keys.I,
            Phase.Panorama => Keys.M,
            Phase.Wheel => Keys.B,
            Phase.Restart => Keys.H,
            _ => Keys.None,
        };

        /// <summary>本步键位未绑定，且有临时键可顶上</summary>
        private static bool FallbackActive(Phase phase, out Keys key) {
            key = FallbackKeyFor(phase);
            if (key == Keys.None) {
                return false;
            }
            ModKeybind bind = ActionKeyFor(phase);
            return bind != null && CWRKeySystem.IsKeybindUnbound(bind);
        }

        /// <summary>临时键此刻能不能吃：打字（聊天/牌子/箱名/任何文本框）、改键、暂停、时停、全屏地图、演出锁输入期间一律不吃</summary>
        private static bool FallbackInputAllowed() {
            Player player = Main.LocalPlayer;
            return !Main.gamePaused && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest
                && !PlayerInput.WritingText && !Main.inFancyUI && !Main.ingameOptionsWindow
                && !Main.mapFullscreen && !Main.blockInput && !HackTime.Active
                && player?.active == true && !player.dead;
        }

        /// <summary>
        /// 轮询停摆时（会话不可用、换步、收起）把按住态放掉：转盘步按住的临时键要补一个松开沿，
        /// 否则盘会卡在开着等一个永远不来的松手
        /// </summary>
        private static void ReleaseFallbackHold() {
            if (fallbackHeld && currentPhase == Phase.Wheel && !Main.gameMenu) {
                RadialWheelHub.HandleKeyEdge(pressed: false, released: true);
            }
            fallbackHeld = false;
        }

        /// <summary>
        /// 每帧轮询临时键。按下沿走该步键位的单一受理点；转盘步是按住型，松开沿也要送到，
        /// 输入途中变得不可受理（开聊天等）时视作松开，盘不会被卡在开着
        /// </summary>
        private static void PollFallbackKey() {
            if (!FallbackActive(currentPhase, out Keys key) || !FallbackInputAllowed()) {
                ReleaseFallbackHold();
                return;
            }

            bool down = Main.keyState.IsKeyDown(key);
            bool pressed = down && !fallbackHeld && Main.oldKeyState.IsKeyUp(key);
            bool released = !down && fallbackHeld;
            fallbackHeld = down;

            Player player = Main.LocalPlayer;
            switch (currentPhase) {
                case Phase.Domain:
                case Phase.Close:
                    //不持伞、或域正翻转/在梦里收不了，都回一声拒绝，临时键绝不无声
                    if (pressed && (!HoldingUmbrella(player)
                        || !KikasaDomain.TryToggle(player, out _))) {
                        RefuseTick();
                    }
                    break;
                case Phase.Sink:
                    if (pressed) {
                        player.GetModPlayer<KikasaVaultPlayer>().HandleSinkPress();
                    }
                    break;
                case Phase.Panorama:
                    if (pressed) {
                        player.GetModPlayer<KikasaVaultPlayer>().HandlePanoramaPress();
                    }
                    break;
                case Phase.Wheel:
                    if (pressed || released) {
                        RadialWheelHub.HandleKeyEdge(pressed, released);
                    }
                    break;
                case Phase.Restart:
                    if (pressed && !player.GetModPlayer<KikasaResetPlayer>().HandleRestartPress()) {
                        RefuseTick();
                    }
                    break;
            }
        }

        private static bool HoldingUmbrella(Player player) {
            Item item = player.GetItem();
            return item != null && item.Alives()
                && item.type == ModContent.ItemType<KikasaItem>();
        }

        /// <summary>拒绝低音：临时键按了但此刻做不了，也绝不无声</summary>
        private static void RefuseTick()
            => SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.55f, Volume = 0.4f });
    }
}
