using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.UI;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>
    /// 模式切换演出：聊天栏留档 + 音效 + 屏幕中央大字的状态机。
    /// 各端在收到权威状态变更时本地调用（<see cref="GameModeSystem.NetHandle"/> 保证全端各放一次），
    /// 文案按各端语言本地化；专用服务器无演出。
    /// 台词/颜色/音效按表现脸取（天顶世界的修罗走毁灭变体）。
    /// 标签自身的开关动画不在这里，由标签 UI 对旗标做真值差分点火
    /// </summary>
    internal static class GameModeCeremony
    {
        /// <summary>屏幕大字演出的持续帧数</summary>
        internal const int LineDuration = 200;

        /// <summary>当前大字所属表现脸</summary>
        internal static GameModeFace LineFace { get; private set; }

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

            GameModeFace face = GameModeSystem.FaceOf(kind);
            VaultUtils.Text(GameModeText.ToggleLine(face, enabled).Value, GameModeTheme.Accent(face));
            PlaySound(face, enabled);

            if (enabled) {
                //开启向按档位递增的一记轻震（ScreenVibration 配置门在 GetScreenShake 内部）
                float shake = face switch {
                    GameModeFace.Brutal => 6f,
                    GameModeFace.Asura => 8f,
                    _ => 10f,
                };
                Main.LocalPlayer.CWR()?.GetScreenShake(shake);
            }

            LineFace = face;
            LineEnabled = enabled;
            LineTimer = LineDuration;
        }

        /// <summary>CrueltyOpen 分档变调：残酷原样，修罗降二成，毁灭降四成；关闭再降调减量</summary>
        private static void PlaySound(GameModeFace face, bool enabled) {
            var at = Main.LocalPlayer.Center;
            float pitch = face switch {
                GameModeFace.Brutal => 0f,
                GameModeFace.Asura => -0.2f,
                _ => -0.4f,
            };
            if (enabled) {
                SoundEngine.PlaySound(CWRSound.CrueltyOpen with { Pitch = pitch }, at);
            }
            else {
                SoundEngine.PlaySound(CWRSound.CrueltyOpen with { Pitch = pitch - 0.35f, Volume = 0.65f }, at);
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
