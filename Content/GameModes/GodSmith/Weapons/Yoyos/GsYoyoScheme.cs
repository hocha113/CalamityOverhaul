using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Yoyos
{
    /// <summary>
    /// 每悠悠球一份的环绕状态（<see cref="GodSmithProjRouter.LocalState"/> 承载，各端各持，弹幕亡即弃）。
    /// 开关与环心是 owner 权威本地量；跨端只镜像开关本身（MarkData），远端靠位置同步看轨迹
    /// </summary>
    internal class GsYoyoOrbitState
    {
        /// <summary>环绕开关（owner 权威）</summary>
        public bool Orbiting;
        /// <summary>环绕相位角</summary>
        public float Phase;
        /// <summary>环心（逐帧跟光标，已按放线距离夹紧）</summary>
        public Vector2 Center;
        /// <summary>上帧右键原始状态（边沿检测）；首帧只记录不触发，出球时已按住的右键不算一次点击</summary>
        public bool PrevRight;
        public bool InputInit;
        /// <summary>环绕期周期 netUpdate 计时</summary>
        public int NetSyncTimer;
        /// <summary>MaxUpdates 去重门（每帧逻辑只跑一次）</summary>
        public uint LastFrame;
        /// <summary>各端上帧看到的开关（切换音效用）</summary>
        public bool SeenOrbiting;
        public bool SeenInit;
    }

    /// <summary>
    /// 悠悠球族方案基类。原版 aiStyle 99 全权（线绳/飞行时限/回收/悠悠球袋/配重球生态全部无损），
    /// 本族只加一条右键指令：球在外时点右键，悠悠球改为围着光标位置转圈巡逻，
    /// 环心逐帧跟随光标；再点一次或收球即回原版跟随。<br/>
    /// 指令输入 = owner 侧右键原始边沿（channel 占用左键，AltFunctionUse 在 itemAnimation 归零前不可达）。
    /// 跨端契约：开关写 MarkData 随 netUpdate 过线，环心/相位是 owner 本地量，远端靠弹幕位置同步呈现
    /// </summary>
    internal abstract class GsYoyoScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "Yoyos";

        /// <summary>本方案悠悠球弹幕 type（加载期从原版 item.shoot 读取，不硬编码）</summary>
        internal int YoyoProjType { get; private set; } = -1;

        /// <summary>基础伤害倍率（有效 DPS 口径的静态部分）</summary>
        internal virtual float DamageMul => 1.05f;

        /// <summary>环绕半径 px</summary>
        internal virtual float OrbitRadius => 40f;

        /// <summary>环绕期球体辉光色</summary>
        internal virtual Color GlowColor => new(255, 214, 120);

        /// <summary>全族共用简述（en 默认值；正典 zh 写在族 loc 文件，键仍按各方案类名落位）</summary>
        protected override string GsDescFallback =>
            "Reforged: while the yoyo is out, right click to make it circle around your cursor;" +
            "\nright click again or let go to return to normal";

        public override void GsSetStaticDefaults() {
            YoyoProjType = new Item(TargetItemID).shoot;
            if (YoyoProjType <= ProjectileID.None) {
                CWRMod.Instance.Logger.Error($"[GodSmith] 悠悠球方案 {FullName} 读取 item.shoot 失败，通道未注册");
                return;
            }
            GsRegisterProjChannel(YoyoProjType);
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            damage *= DamageMul;
        }

        //==================== 环绕指令 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //承签传染的子弹幕（配重球等）不接管
            if (proj.type != YoyoProjType) {
                return;
            }
            Player owner = Main.player[proj.owner];
            if (!owner.active) {
                return;
            }
            GsYoyoOrbitState st = router.GetOrCreateState<GsYoyoOrbitState>();
            bool isOwner = proj.IsOwnedByLocalPlayer();
            bool newFrame = st.LastFrame != Main.GameUpdateCount;
            if (newFrame) {
                st.LastFrame = Main.GameUpdateCount;
            }

            if (isOwner) {
                //原版回收态（ai[0] < 0）或松手当帧立即停覆写，收线全交原版
                bool recalled = proj.ai[0] < 0f || !owner.channel || owner.dead || owner.CCed;
                if (recalled) {
                    if (st.Orbiting) {
                        SetOrbiting(proj, router, st, false);
                    }
                }
                else {
                    if (newFrame) {
                        ReadInput(proj, router, st, owner);
                    }
                    if (st.Orbiting) {
                        Steer(proj, st, owner, newFrame);
                    }
                }
                //环绕期周期性推位置同步，远端轨迹不漂
                if (st.Orbiting && newFrame && ++st.NetSyncTimer >= 10) {
                    st.NetSyncTimer = 0;
                    proj.netUpdate = true;
                }
            }

            if (!newFrame) {
                return;
            }
            bool effOrbit = isOwner ? st.Orbiting : router.MarkData > 0.5f;
            if (st.SeenInit && st.SeenOrbiting != effOrbit && !VaultUtils.isServer) {
                SoundStyle sound = effOrbit
                    ? SoundID.Item8 with { Volume = 0.45f, Pitch = 0.35f }
                    : SoundID.Item8 with { Volume = 0.3f, Pitch = -0.25f };
                SoundEngine.PlaySound(sound, proj.Center);
            }
            st.SeenInit = true;
            st.SeenOrbiting = effOrbit;
            if (effOrbit && !VaultUtils.isServer) {
                Lighting.AddLight(proj.Center, GlowColor.ToVector3() * 0.2f);
            }
        }

        /// <summary>owner 侧右键按下沿切换环绕；UI 悬停/背包/地图打开时忽略；出球首帧只登记不触发</summary>
        private void ReadInput(Projectile proj, GodSmithProjRouter router, GsYoyoOrbitState st, Player owner) {
            bool right = Main.mouseRight;
            if (!st.InputInit) {
                st.InputInit = true;
                st.PrevRight = right;
                return;
            }
            bool uiBlock = Main.playerInventory || owner.mouseInterface || Main.mapFullscreen || Main.ingameOptionsWindow;
            if (right && !st.PrevRight && !uiBlock) {
                SetOrbiting(proj, router, st, !st.Orbiting);
            }
            st.PrevRight = right;
        }

        /// <summary>开关转移（仅 owner 端）：开启时从球当前方位切入环，identity 奇偶错相让悠悠球袋双球对置；镜像 MarkData 过线</summary>
        private void SetOrbiting(Projectile proj, GodSmithProjRouter router, GsYoyoOrbitState st, bool on) {
            st.Orbiting = on;
            if (on) {
                Player owner = Main.player[proj.owner];
                st.Center = ClampCenter(proj, owner, Main.MouseWorld);
                st.Phase = (proj.Center - st.Center).ToRotation() + proj.identity % 2 * MathHelper.Pi;
                st.NetSyncTimer = 0;
            }
            router.MarkData = on ? 1f : 0f;
            proj.netUpdate = true;
        }

        /// <summary>速度覆写：环心逐帧跟光标，球沿环匀角速前进；角速按原版顶速折算，上限给足余量防滞后</summary>
        private void Steer(Projectile proj, GsYoyoOrbitState st, Player owner, bool newFrame) {
            st.Center = ClampCenter(proj, owner, Main.MouseWorld);
            float top = ProjectileID.Sets.YoyosTopSpeed[proj.type];
            if (top < 8f) {
                top = 8f;
            }
            float r = OrbitRadius;
            float spin = MathHelper.Clamp(top * 0.9f / r, 0.12f, 0.30f);
            if (newFrame) {
                st.Phase += spin;
            }
            Vector2 target = st.Center + st.Phase.ToRotationVector2() * r;
            Vector2 to = target - proj.Center;
            float dist = to.Length();
            float maxSpeed = MathF.Max(top, spin * r) + 6f;
            proj.velocity = dist <= maxSpeed ? to : to * (maxSpeed / dist);
        }

        /// <summary>环心收进原版最大放线距离之内（扣掉环半径），线不会被拉断</summary>
        private Vector2 ClampCenter(Projectile proj, Player owner, Vector2 point) {
            float maxR = ProjectileID.Sets.YoyosMaximumRange[proj.type] * 0.92f - OrbitRadius;
            if (maxR < 80f) {
                maxR = 80f;
            }
            Vector2 d = point - owner.Center;
            if (d.LengthSquared() > maxR * maxR) {
                point = owner.Center + d.SafeNormalize(Vector2.UnitX) * maxR;
            }
            return point;
        }

        //==================== 绘制：环绕态读数 ====================

        public override void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) {
            if (proj.type != YoyoProjType) {
                return;
            }
            GsYoyoOrbitState st = router.GetOrCreateState<GsYoyoOrbitState>();
            bool isOwner = proj.IsOwnedByLocalPlayer();
            bool orbiting = isOwner ? st.Orbiting : router.MarkData > 0.5f;
            if (!orbiting) {
                return;
            }

            //球体辉光（SoftGlow 黑底贴图，预乘批里 A=0 即加色）
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + proj.identity * 0.83f);
                Color c = GlowColor * (0.4f * pulse);
                c.A = 0;
                Main.EntitySpriteDraw(glowTex, proj.Center - Main.screenPosition, null, c, 0f,
                    glowTex.Size() / 2f, 0.6f, SpriteEffects.None, 0);
            }

            //环心标记只有 owner 画（环心是本地量）
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (isOwner && star != null) {
                float pulse = 0.55f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f);
                Color c = GlowColor * pulse;
                c.A = 0;
                Main.EntitySpriteDraw(star, st.Center - Main.screenPosition, null, c,
                    Main.GlobalTimeWrappedHourly * 1.2f, star.Size() / 2f, 0.3f, SpriteEffects.None, 0);
            }
        }
    }
}
