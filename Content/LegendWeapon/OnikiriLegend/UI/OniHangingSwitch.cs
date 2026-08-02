using InnoVault.UIHandles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 吊挂切换器状态壳:两屏互通的门是"对面那件器物的悬挂微缩"
    /// (点鬼簿挂太刀,改铭台挂卷轴),本类管绳物理/悬停/偶发回声/点击预演,
    /// 画法各屏渲染器自带
    /// </summary>
    internal sealed class OniHangingSwitch(SoundStyle clickSound)
    {
        /// <summary>绳长(锚到物件顶挂点;物件放大后略加长,仍贴梁)</summary>
        public const float RopeLen = 62f;
        private const float CeremonyFrames = 12f;
        private const float EchoFrames = 70f;

        private readonly OniRope rope = new(6, RopeLen);
        private readonly SoundStyle clickSound = clickSound;
        private float hoverEase;
        private bool wasHovered;
        private int windCooldown = 320;
        private int echoCooldown = 460;
        private float echoRun = -1f;
        //点击预演计帧,-1 闲;到 CeremonyFrames 触发切换(一次)
        private float ceremony = -1f;
        private bool fired;

        /// <summary>本帧悬停</summary>
        public bool Hovering { get; private set; }
        /// <summary>悬停缓动 0~1</summary>
        public float HoverEase => hoverEase;
        /// <summary>物件摆角(整绳方向弹给挂物,小角截断)</summary>
        public float Rot { get; private set; }
        /// <summary>绳末=物件顶挂点</summary>
        public Vector2 End => rope.End;
        /// <summary>本帧命中区</summary>
        public Rectangle HitBox { get; private set; }
        /// <summary>预演进度 0~1:半拔白光/弹开一截等小演出的驱动量</summary>
        public float Ceremony01 => ceremony < 0f ? 0f : MathHelper.Clamp(ceremony / CeremonyFrames, 0f, 1f);
        /// <summary>偶发回声 0~1:金光巡鞘/鬼火漏缝这类"对面在喘气"的脉冲</summary>
        public float Echo01 => echoRun < 0f ? 0f : MathHelper.Clamp(echoRun / EchoFrames, 0f, 1f);

        /// <summary>开屏复位:预演/悬停清零,绳保留(重开不甩)</summary>
        public void Reset() {
            hoverEase = 0f;
            Hovering = false;
            wasHovered = false;
            ceremony = -1f;
            fired = false;
            HitBox = Rectangle.Empty;
        }

        /// <summary>绘制挂绳(物件本体由调用方画在 End 之下)</summary>
        public void DrawRope(Microsoft.Xna.Framework.Graphics.SpriteBatch sb, float alpha) {
            rope.Draw(sb, OnikiriUITheme.Deep * 0.9f, OnikiriUITheme.Deep * 0.62f, 1.8f, alpha);
        }

        /// <summary>
        /// 推进一帧;objSize=物件命中尺寸(顶挂点之下);echoBoost=对面有事(回声更频);
        /// 返回 true 的那一帧执行切换
        /// </summary>
        public bool Update(Vector2 anchor, Vector2 mouse, bool interactive, float time,
            Vector2 objSize, KeyPressState leftPress, bool echoBoost = false) {
            //绳:悬停视为被手扶住,风息阻尼加重
            float windAmp = Hovering ? 0.015f : 0.055f;
            rope.Update(anchor, null, time, windAmp, endWeight: 0.55f, damping: Hovering ? 0.80f : 0.85f);
            if (!Hovering && --windCooldown <= 0) {
                windCooldown = Main.rand.Next(260, 620);
                rope.Nudge(Main.rand.NextFloat(0.5f, 1.1f) * (Main.rand.NextBool() ? 1f : -1f), Main.rand.NextFloat(0.3f));
            }
            //摆角:整绳方向压系数,物件只承大势
            float targetRot = (rope.End - anchor).SafeNormalize(Vector2.UnitY).ToRotation() - MathHelper.PiOver2;
            Rot = MathHelper.Clamp(targetRot * 0.7f, -0.26f, 0.26f);

            //偶发回声
            if (echoRun >= 0f) {
                echoRun += 1f;
                if (echoRun > EchoFrames) {
                    echoRun = -1f;
                    echoCooldown = echoBoost ? Main.rand.Next(220, 420) : Main.rand.Next(460, 900);
                }
            }
            else if (--echoCooldown <= 0) {
                echoRun = 0f;
            }

            //命中:物件挂在绳末之下的轴对齐外包(摆角小,不做 OBB)
            Rectangle box = new((int)(End.X - objSize.X * 0.5f - 5f), (int)End.Y - 4,
                (int)objSize.X + 10, (int)objSize.Y + 10);
            HitBox = box;
            bool hoverNow = interactive && ceremony < 0f && box.Contains(mouse.ToPoint());
            if (hoverNow && !wasHovered) {
                rope.Nudge(Main.rand.NextFloat(0.5f, 1.0f) * (Main.rand.NextBool() ? 1f : -1f));
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.25f, Volume = 0.3f });
            }
            Hovering = hoverNow;
            wasHovered = hoverNow;
            hoverEase += ((hoverNow ? 1f : 0f) - hoverEase) * 0.2f;

            //点击预演→到帧触发切换
            if (hoverNow && leftPress == KeyPressState.Pressed) {
                ceremony = 0f;
                fired = false;
                rope.Nudge(0f, Main.rand.NextFloat(0.6f, 1.1f));
                SoundEngine.PlaySound(clickSound);
            }
            if (ceremony >= 0f) {
                ceremony += 1f;
                if (!fired && ceremony >= CeremonyFrames) {
                    fired = true;
                    return true;
                }
                //切换后残留一小段供收尾淡出,再归闲
                if (ceremony > CeremonyFrames + 40f) {
                    ceremony = -1f;
                    fired = false;
                }
            }
            return false;
        }
    }
}
