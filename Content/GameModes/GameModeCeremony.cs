using CalamityOverhaul.Content.GameModes.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>
    /// 模式切换演出：聊天栏留档 + 音效 + 屏幕中央大字的状态机。
    /// 各端在收到权威状态变更时本地调用（<see cref="GameModeSystem.NetHandle"/> 保证全端各放一次），
    /// 文案按各端语言本地化；专用服务器无演出。
    /// 标签自身的开关动画不在这里，由标签 UI 对旗标做真值差分点火
    /// </summary>
    internal static class GameModeCeremony
    {
        /// <summary>屏幕大字演出的持续帧数</summary>
        internal const int LineDuration = 200;

        /// <summary>当前大字所属模式</summary>
        internal static GameModeKind LineKind { get; private set; }

        /// <summary>当前大字是开启词还是关闭词</summary>
        internal static bool LineEnabled { get; private set; }

        /// <summary>大字剩余帧数，&gt;0 时演出在场</summary>
        internal static int LineTimer { get; private set; }

        /// <summary>大字演出在场</summary>
        internal static bool LineActive => LineTimer > 0;

        /// <summary>大字演出进度 0..1</summary>
        internal static float LineProgress => 1f - LineTimer / (float)LineDuration;

        internal static void Play(GameModeKind kind, bool enabled) {
            if (Main.dedServ) {
                return;
            }

            VaultUtils.Text(GameModeText.ToggleLine(kind, enabled).Value, GameModeTheme.Accent(kind));
            PlaySound(kind, enabled);

            LineKind = kind;
            LineEnabled = enabled;
            LineTimer = LineDuration;
        }

        private static void PlaySound(GameModeKind kind, bool enabled) {
            var at = Main.LocalPlayer.Center;
            if (kind == GameModeKind.Brutal) {
                //开=兽吼，关=沉降的低吼
                SoundEngine.PlaySound(enabled
                    ? SoundID.Roar
                    : SoundID.Roar with { Volume = 0.65f, Pitch = -0.6f }, at);
            }
            else {
                //修罗一族用更重的低频吼
                SoundEngine.PlaySound(enabled
                    ? SoundID.ForceRoarPitched with { Volume = 0.9f }
                    : SoundID.ForceRoarPitched with { Volume = 0.55f, Pitch = -0.5f }, at);
            }
        }

        /// <summary>推进大字计时；由标签 UI 每帧驱动</summary>
        internal static void UpdateLine() {
            if (LineTimer > 0) {
                LineTimer--;
            }
        }

        /// <summary>跨世界收尾，防止旧演出漂进新世界</summary>
        internal static void Reset() => LineTimer = 0;
    }
}
