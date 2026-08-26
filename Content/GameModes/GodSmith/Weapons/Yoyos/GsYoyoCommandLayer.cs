using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Yoyos
{
    /// <summary>
    /// 每悠悠球一份的指令状态包（<see cref="GodSmithProjRouter.LocalState"/> 承载，
    /// 各端各自持有、弹幕亡即弃）。锚点/路径点/输入机是 owner 权威本地量；
    /// 跨端一致的量只有指令模式（MarkData）与热度层（MarkData2）
    /// </summary>
    internal class GsYoyoState
    {
        //——owner 权威指令态——
        /// <summary>当前指令模式（owner 本地权威，镜像进 MarkData 过线）</summary>
        public int Mode;
        /// <summary>驻场锚点（owner 专有，远端靠位置同步呈现）</summary>
        public Vector2 AnchorPoint;
        /// <summary>驻场/环绕自旋相位角</summary>
        public float SpinPhase;
        /// <summary>路径编程节点（泰拉悠悠球，owner 专有）</summary>
        public readonly List<Vector2> PathNodes = [];
        /// <summary>贝塞尔巡回参数 0~1</summary>
        public float PathT;
        /// <summary>巡回往返方向</summary>
        public int PathDir = 1;
        /// <summary>折返已持续帧数（超时保险）</summary>
        public int LashTimer;

        //——热度（owner 权威，层数镜像进 MarkData2）——
        public int HeatLayers;
        public int HeatTarget = -1;
        public int HeatTargetType;

        //——输入机（owner 端专用）——
        public bool PrevRight;
        public int HoldFrames;
        /// <summary>单击确认倒计时（等双击窗）</summary>
        public int ClickWait;
        /// <summary>本次按住已触发环绕，松开前不再响应</summary>
        public bool OrbitLatch;
        /// <summary>双击第二击的松开沿不再计单击</summary>
        public bool SuppressRelease;

        //——通用计时与视觉——
        /// <summary>MaxUpdates 去重门（每帧逻辑只跑一次）</summary>
        public uint LastFrame;
        /// <summary>指令激活期周期 netUpdate 计时</summary>
        public int NetSyncTimer;
        /// <summary>各端上帧看到的模式（切换演出检测）</summary>
        public int SeenMode = -1;
        /// <summary>切换闪光倒计时</summary>
        public int SwitchFlash;
        /// <summary>折返/巡回残影环形缓冲（惰性建）</summary>
        public Vector2[] TrailBuf;
        public int TrailHead;
        public int TrailLen;

        //——个性寄存器（各球自用，语义见各参数行）——
        public int SigCount;
        public int SigTimer;
        public int SigTarget = -1;
        public int SigTarget2 = -1;
        public Vector2 SigPoint;
    }

    /// <summary>
    /// 悠悠球指令执行层：输入边沿检测（owner）、PostAI 速度覆写（owner）、
    /// 切换演出与辉光绘制（各端）。原版 aiStyle 99 每帧先跑，这里只做后置覆写，
    /// 回收（ai[0] &lt; 0）或松开左键当帧立即停手交还原版
    /// </summary>
    internal static class GsYoyoCommandLayer
    {
        /// <summary>环绕辉光青色</summary>
        private static readonly Color OrbitGlow = new(110, 210, 235);
        /// <summary>折返辉光白炽色</summary>
        private static readonly Color LashGlow = new(255, 245, 225);
        /// <summary>路径预览淡金</summary>
        private static readonly Color PathGold = new(255, 224, 150);

        //==================== PostAI 主入口 ====================

        internal static void PostAI(GsYoyoScheme scheme, Projectile proj, GodSmithProjRouter router) {
            Player owner = Main.player[proj.owner];
            if (!owner.active) {
                return;
            }
            GsYoyoState st = router.GetOrCreateState<GsYoyoState>();
            bool isOwner = proj.IsOwnedByLocalPlayer();
            bool newFrame = st.LastFrame != Main.GameUpdateCount;
            if (newFrame) {
                st.LastFrame = Main.GameUpdateCount;
            }

            //——owner 权威段：输入、覆写、同步——
            if (isOwner) {
                //原版回收态（ai[0] < 0）或松手当帧立即停覆写，回收/收线全交原版
                bool recalled = proj.ai[0] < 0f || !owner.channel || owner.dead || owner.CCed;
                if (recalled) {
                    if (st.Mode != GsYoyoMode.Follow) {
                        SetMode(scheme, proj, router, st, GsYoyoMode.Follow);
                    }
                }
                else {
                    if (newFrame) {
                        ReadInput(scheme, proj, router, st, owner);
                        if (st.Mode == GsYoyoMode.Orbit) {
                            DrainLife(scheme, proj);
                        }
                    }
                    Steer(scheme, proj, router, st, owner);
                }
                //指令激活期周期性推位置同步，远端轨迹不漂
                if (newFrame && st.Mode != GsYoyoMode.Follow && ++st.NetSyncTimer >= 12) {
                    st.NetSyncTimer = 0;
                    proj.netUpdate = true;
                }
            }

            if (!newFrame) {
                return;
            }

            //——各端表现与权威 tick 段（服务器也跑，个性钩子体内自守端别）——
            int effMode = isOwner ? st.Mode : (int)router.MarkData;
            if (st.SeenMode != effMode) {
                ModeSwitchFx(proj, st, effMode);
                st.SeenMode = effMode;
            }
            if (st.SwitchFlash > 0) {
                st.SwitchFlash--;
            }
            scheme.OnGlobalTick(proj, router, st, effMode);
            switch (effMode) {
                case GsYoyoMode.Anchor:
                    st.SpinPhase += scheme.AnchorSpin;
                    scheme.OnAnchorTick(proj, router, st);
                    break;
                case GsYoyoMode.Orbit:
                    st.SpinPhase += scheme.OrbitSpin;
                    scheme.OnOrbitTick(proj, router, st);
                    break;
                case GsYoyoMode.Lash:
                    st.LashTimer++;
                    scheme.OnLashTick(proj, router, st);
                    break;
                case GsYoyoMode.Path:
                    st.SpinPhase += scheme.AnchorSpin;
                    scheme.OnPathTick(proj, router, st);
                    break;
            }
            //残影缓冲只在高速指令期记录
            if (effMode == GsYoyoMode.Lash || effMode == GsYoyoMode.Path) {
                PushTrail(st, proj.Center);
            }
            else {
                st.TrailLen = 0;
            }
            if (effMode != GsYoyoMode.Follow && !VaultUtils.isServer) {
                Lighting.AddLight(proj.Center, scheme.GlowColor.ToVector3() * 0.22f);
            }
        }

        //==================== 输入：owner 侧原始右键边沿检测 ====================

        /// <summary>
        /// 右键语法：单击 = 驻场/路径点（Tier2 起有 14f 双击窗延迟），
        /// 双击 = 折返（T2+），按住 24f = 环绕开关（T3+）。UI 悬停时忽略
        /// </summary>
        private static void ReadInput(GsYoyoScheme scheme, Projectile proj, GodSmithProjRouter router, GsYoyoState st, Player owner) {
            bool uiBlock = Main.playerInventory || owner.mouseInterface || Main.mapFullscreen || Main.ingameOptionsWindow;
            bool right = Main.mouseRight && !uiBlock;
            bool tier2 = scheme.Tier >= 2;
            bool tier3 = scheme.Tier >= 3;

            if (right) {
                if (!st.PrevRight) {
                    //按下沿：双击窗内的第二击即折返
                    if (tier2 && st.ClickWait > 0) {
                        st.ClickWait = 0;
                        st.SuppressRelease = true;
                        SetMode(scheme, proj, router, st, GsYoyoMode.Lash);
                    }
                    st.HoldFrames = 0;
                }
                else {
                    st.HoldFrames++;
                }
                //长按环绕开关（本次按住只触发一次）
                if (tier3 && !st.OrbitLatch && st.HoldFrames >= 24) {
                    st.OrbitLatch = true;
                    SetMode(scheme, proj, router, st,
                        st.Mode == GsYoyoMode.Orbit ? GsYoyoMode.Follow : GsYoyoMode.Orbit);
                }
            }
            else {
                if (st.PrevRight) {
                    //松开沿
                    if (st.OrbitLatch) {
                        st.OrbitLatch = false;
                    }
                    else if (st.SuppressRelease) {
                        st.SuppressRelease = false;
                    }
                    else if (st.HoldFrames < 24) {
                        if (tier2) {
                            st.ClickWait = 14;   //开双击窗，超时按单击结算
                        }
                        else {
                            SingleClick(scheme, proj, router, st, owner);
                        }
                    }
                }
                if (st.ClickWait > 0 && --st.ClickWait == 0) {
                    SingleClick(scheme, proj, router, st, owner);
                }
            }
            st.PrevRight = right;
        }

        /// <summary>单击语义：普通球 = 驻场开关；泰拉 = 添加路径点，满员再点即清程</summary>
        private static void SingleClick(GsYoyoScheme scheme, Projectile proj, GodSmithProjRouter router, GsYoyoState st, Player owner) {
            if (scheme.PathPoints > 0) {
                if (st.PathNodes.Count >= scheme.PathPoints) {
                    st.PathNodes.Clear();
                    SetMode(scheme, proj, router, st, GsYoyoMode.Follow);
                    return;
                }
                st.PathNodes.Add(ClampToRange(proj, owner, Main.MouseWorld));
                if (st.Mode != GsYoyoMode.Path) {
                    SetMode(scheme, proj, router, st, GsYoyoMode.Path);
                }
                else if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item64 with { Volume = 0.4f, Pitch = 0.6f }, Main.MouseWorld);
                }
                return;
            }
            if (st.Mode == GsYoyoMode.Anchor) {
                SetMode(scheme, proj, router, st, GsYoyoMode.Follow);
            }
            else {
                st.AnchorPoint = ClampToRange(proj, owner, Main.MouseWorld);
                SetMode(scheme, proj, router, st, GsYoyoMode.Anchor);
            }
        }

        /// <summary>
        /// 模式转移（仅 owner 端调用）：镜像 MarkData/MarkData2 并 netUpdate 过线，
        /// 相位按当前几何初始化防瞬跳
        /// </summary>
        internal static void SetMode(GsYoyoScheme scheme, Projectile proj, GodSmithProjRouter router, GsYoyoState st, int mode) {
            if (st.Mode == mode) {
                return;
            }
            //identity 奇偶错相：悠悠球袋双球同拍执行时自动对置，不叠成一团
            float twinOffset = proj.identity % 2 * MathHelper.Pi;
            if (mode == GsYoyoMode.Anchor) {
                st.SpinPhase = (proj.Center - st.AnchorPoint).ToRotation() + twinOffset;
            }
            else if (mode == GsYoyoMode.Orbit) {
                st.SpinPhase = (proj.Center - Main.player[proj.owner].Center).ToRotation() + twinOffset;
            }
            else if (mode == GsYoyoMode.Lash) {
                st.LashTimer = 0;
                scheme.OnLashBeginOwner(proj, router, st);
            }
            else if (mode == GsYoyoMode.Path) {
                st.PathT = proj.identity % 2 * 0.5f;
                st.PathDir = 1;
            }
            st.Mode = mode;
            router.MarkData = mode;
            router.MarkData2 = st.HeatLayers;
            proj.netUpdate = true;
        }

        /// <summary>锚点/路径点收进原版最大放线距离的 95%，原版距离经济保留</summary>
        private static Vector2 ClampToRange(Projectile proj, Player owner, Vector2 point) {
            float maxR = ProjectileID.Sets.YoyosMaximumRange[proj.type] * 0.95f;
            if (maxR < 100f) {
                maxR = 100f;
            }
            Vector2 d = point - owner.Center;
            if (d.LengthSquared() > maxR * maxR) {
                point = owner.Center + d.SafeNormalize(Vector2.UnitX) * maxR;
            }
            return point;
        }

        //==================== 执行：速度覆写（owner，每 update） ====================

        private static void Steer(GsYoyoScheme scheme, Projectile proj, GodSmithProjRouter router, GsYoyoState st, Player owner) {
            float top = ProjectileID.Sets.YoyosTopSpeed[proj.type];
            if (top < 8f) {
                top = 8f;
            }
            switch (st.Mode) {
                case GsYoyoMode.Anchor: {
                    Vector2 target = st.AnchorPoint + st.SpinPhase.ToRotationVector2() * scheme.AnchorRadius;
                    MoveToward(proj, target, top + 6f);
                    break;
                }
                case GsYoyoMode.Orbit: {
                    float r = OrbitRadius(scheme, proj);
                    Vector2 target = owner.Center + st.SpinPhase.ToRotationVector2() * r;
                    //轨道点线速度 ωr，跟随上限给足余量防滞后
                    MoveToward(proj, target, scheme.OrbitSpin * r + 8f);
                    break;
                }
                case GsYoyoMode.Lash: {
                    Vector2 to = owner.Center - proj.Center;
                    float dist = to.Length();
                    if (dist < 44f || st.LashTimer > 90) {
                        SetMode(scheme, proj, router, st, GsYoyoMode.Follow);
                        break;
                    }
                    float speed = MathF.Min(top * scheme.LashSpeedMul, 40f);
                    proj.velocity = to * (speed / MathF.Max(dist, 1f));
                    break;
                }
                case GsYoyoMode.Path: {
                    if (st.PathNodes.Count == 0) {
                        SetMode(scheme, proj, router, st, GsYoyoMode.Follow);
                        break;
                    }
                    Vector2 target = PathTarget(scheme, st, top);
                    MoveToward(proj, target, top + 8f);
                    break;
                }
            }
        }

        /// <summary>环绕轨道半径 = 原版最大放线距离 × 比例（运行时读表，天平跟原版走）</summary>
        internal static float OrbitRadius(GsYoyoScheme scheme, Projectile proj) {
            float range = ProjectileID.Sets.YoyosMaximumRange[proj.type];
            if (range < 120f) {
                range = 120f;
            }
            return MathF.Max(range * scheme.OrbitRadiusRatio, 80f);
        }

        /// <summary>速度覆写：直接指向目标点并按上限截断（原版 tile 碰撞照常工作，不穿墙）</summary>
        private static void MoveToward(Projectile proj, Vector2 target, float maxSpeed) {
            Vector2 to = target - proj.Center;
            float d = to.Length();
            proj.velocity = d <= maxSpeed ? to : to * (maxSpeed / d);
        }

        /// <summary>巡回目标点：1 点驻场语义 / 2 点直线往返 / 3 点二次贝塞尔往返</summary>
        private static Vector2 PathTarget(GsYoyoScheme scheme, GsYoyoState st, float top) {
            List<Vector2> n = st.PathNodes;
            if (n.Count == 1) {
                return n[0] + st.SpinPhase.ToRotationVector2() * scheme.AnchorRadius;
            }
            float len = n.Count == 2
                ? Vector2.Distance(n[0], n[1])
                : Vector2.Distance(n[0], n[1]) + Vector2.Distance(n[1], n[2]);
            float speed = MathF.Max(top * 0.85f, 9f);
            st.PathT += st.PathDir * speed / MathF.Max(len, 60f);
            if (st.PathT >= 1f) {
                st.PathT = 1f;
                st.PathDir = -1;
            }
            else if (st.PathT <= 0f) {
                st.PathT = 0f;
                st.PathDir = 1;
            }
            return SamplePath(n, st.PathT);
        }

        internal static Vector2 SamplePath(List<Vector2> n, float t) {
            if (n.Count < 2) {
                return n.Count == 1 ? n[0] : Vector2.Zero;
            }
            if (n.Count == 2) {
                return Vector2.Lerp(n[0], n[1], t);
            }
            float u = 1f - t;
            return n[0] * (u * u) + n[1] * (2f * u * t) + n[2] * (t * t);
        }

        /// <summary>环绕期时限流速加倍走表（∞ 时限球无压力，驻场经济=原版飞行时限）</summary>
        private static void DrainLife(GsYoyoScheme scheme, Projectile proj) {
            float lifeMul = ProjectileID.Sets.YoyosLifeTimeMultiplier[proj.type];
            if (lifeMul > 0f) {
                proj.localAI[0] += scheme.OrbitTimeDrain - 1f;
            }
        }

        private static void PushTrail(GsYoyoState st, Vector2 pos) {
            st.TrailBuf ??= new Vector2[10];
            st.TrailBuf[st.TrailHead] = pos;
            st.TrailHead = (st.TrailHead + 1) % st.TrailBuf.Length;
            if (st.TrailLen < st.TrailBuf.Length) {
                st.TrailLen++;
            }
        }

        //==================== 命中：热度与倍率 ====================

        internal static void OnHit(GsYoyoScheme scheme, Projectile proj, NPC target, NPC.HitInfo hit, GodSmithProjRouter router) {
            GsYoyoState st = router.GetOrCreateState<GsYoyoState>();
            int mode = st.Mode;   //命中钩子只在攻击方端执行，st.Mode 即权威
            if (mode == GsYoyoMode.Anchor || mode == GsYoyoMode.Path) {
                //驻场热度：同目标连击叠层，换目标清零重叠
                if (st.HeatTarget == target.whoAmI && st.HeatTargetType == target.type) {
                    if (st.HeatLayers < scheme.HeatCapLayers) {
                        st.HeatLayers++;
                    }
                }
                else {
                    st.HeatTarget = target.whoAmI;
                    st.HeatTargetType = target.type;
                    st.HeatLayers = 1;
                }
                if ((int)router.MarkData2 != st.HeatLayers) {
                    router.MarkData2 = st.HeatLayers;
                    proj.netUpdate = true;
                }
            }
            scheme.OnCommandHit(proj, target, in hit, st, mode, router);
        }

        internal static void ModifyHit(GsYoyoScheme scheme, Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            GsYoyoState st = router.GetOrCreateState<GsYoyoState>();
            int mode = proj.IsOwnedByLocalPlayer() ? st.Mode : (int)router.MarkData;
            if ((mode == GsYoyoMode.Anchor || mode == GsYoyoMode.Path) && st.HeatLayers > 0
                && target.whoAmI == st.HeatTarget && target.type == st.HeatTargetType) {
                modifiers.FinalDamage *= 1f + st.HeatLayers * scheme.HeatPerHit;
            }
            if (mode == GsYoyoMode.Lash) {
                modifiers.FinalDamage *= scheme.LashDamageMul;
            }
            scheme.ModifyCommandHit(proj, target, ref modifiers, st, mode);
        }

        //==================== 演出与绘制 ====================

        /// <summary>切换演出：各端在 MarkData 变化帧自行播放（跨端里程碑）</summary>
        private static void ModeSwitchFx(Projectile proj, GsYoyoState st, int mode) {
            if (st.SeenMode < 0) {
                return;   //首帧初始化不演
            }
            st.SwitchFlash = 12;
            if (VaultUtils.isServer) {
                return;
            }
            SoundStyle sound = mode switch {
                GsYoyoMode.Anchor => SoundID.Item8 with { Volume = 0.55f, Pitch = 0.30f },
                GsYoyoMode.Lash => SoundID.Item60 with { Volume = 0.5f, Pitch = 0.2f },
                GsYoyoMode.Orbit => SoundID.Item24 with { Volume = 0.5f, Pitch = 0.4f },
                GsYoyoMode.Path => SoundID.Item64 with { Volume = 0.45f, Pitch = 0.5f },
                _ => SoundID.MenuTick with { Volume = 0.6f },
            };
            SoundEngine.PlaySound(sound, proj.Center);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center, Main.rand.NextVector2Circular(1.5f, 1.5f),
                    Color.White, 0.4f)?.Configure(Color.White, 14, 0.2f);
            }
        }

        internal static void PostDraw(GsYoyoScheme scheme, Projectile proj, GodSmithProjRouter router) {
            GsYoyoState st = router.GetOrCreateState<GsYoyoState>();
            bool isOwner = proj.IsOwnedByLocalPlayer();
            int effMode = isOwner ? st.Mode : (int)router.MarkData;
            int heat = isOwner ? st.HeatLayers : (int)router.MarkData2;
            float heatRatio = scheme.HeatCapLayers > 0
                ? MathHelper.Clamp(heat / (float)scheme.HeatCapLayers, 0f, 1f) : 0f;

            //残影链（折返/巡回，identity 定相不掷随机）
            if (st.TrailLen > 1 && (effMode == GsYoyoMode.Lash || effMode == GsYoyoMode.Path)) {
                Main.instance.LoadProjectile(proj.type);
                Texture2D tex = TextureAssets.Projectile[proj.type].Value;
                for (int i = 1; i < st.TrailLen; i++) {
                    int idx = (st.TrailHead - 1 - i + st.TrailBuf.Length * 2) % st.TrailBuf.Length;
                    float fade = 1f - i / (float)st.TrailLen;
                    Color c = scheme.GlowColor * (fade * 0.38f);
                    Main.EntitySpriteDraw(tex, st.TrailBuf[idx] - Main.screenPosition, null, c,
                        proj.rotation - i * 0.5f, tex.Size() / 2f,
                        proj.scale * (0.9f - i * 0.05f), SpriteEffects.None, 0);
                }
            }

            //模式辉光（SoftGlow 黑底贴图，A=0 加色）
            if (effMode != GsYoyoMode.Follow) {
                Texture2D glowTex = CWRAsset.SoftGlow.Value;
                Color glow = effMode switch {
                    GsYoyoMode.Anchor => scheme.GlowColor,
                    GsYoyoMode.Orbit => OrbitGlow,
                    GsYoyoMode.Lash => LashGlow,
                    _ => PathGold,
                };
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + proj.identity * 0.83f);
                Color c = glow * (0.42f * pulse);
                c.A = 0;
                Main.EntitySpriteDraw(glowTex, proj.Center - Main.screenPosition, null, c, 0f,
                    glowTex.Size() / 2f, 0.62f, SpriteEffects.None, 0);
            }

            //热度辉光：亮度与尺寸随层数分级
            if (heatRatio > 0f) {
                Texture2D glowTex = CWRAsset.SoftGlow.Value;
                Color c = scheme.HeatColor * (0.15f + 0.5f * heatRatio);
                c.A = 0;
                Main.EntitySpriteDraw(glowTex, proj.Center - Main.screenPosition, null, c, 0f,
                    glowTex.Size() / 2f, 0.45f + 0.3f * heatRatio, SpriteEffects.None, 0);
            }

            //切换闪光
            if (st.SwitchFlash > 0) {
                Texture2D star = CWRAsset.StarGlow01.Value;
                float k = st.SwitchFlash / 12f;
                Color c = Color.White * (0.7f * k);
                c.A = 0;
                Main.EntitySpriteDraw(star, proj.Center - Main.screenPosition, null, c,
                    proj.identity * 0.7f + (1f - k) * 1.5f, star.Size() / 2f,
                    0.5f + (1f - k) * 0.35f, SpriteEffects.None, 0);
            }

            //驻场锚点标记（锚点是 owner 本地量，只有 owner 画）
            if (isOwner && effMode == GsYoyoMode.Anchor) {
                Texture2D star = CWRAsset.StarGlow01.Value;
                float pulse = 0.55f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f);
                Color c = scheme.GlowColor * pulse;
                c.A = 0;
                Main.EntitySpriteDraw(star, st.AnchorPoint - Main.screenPosition, null, c,
                    Main.GlobalTimeWrappedHourly * 1.2f, star.Size() / 2f, 0.3f, SpriteEffects.None, 0);
            }

            //路径编程预览线（owner 专有）
            if (isOwner && scheme.PathPoints > 0 && st.PathNodes.Count > 0) {
                DrawPathPreview(st);
            }

            scheme.OnCommandDraw(proj, router, st, effMode, heatRatio);
        }

        /// <summary>路径预览：节点星 + 贝塞尔采样短线段（MaskLaserLine 淡金）</summary>
        private static void DrawPathPreview(GsYoyoState st) {
            Texture2D star = CWRAsset.StarGlow01.Value;
            for (int i = 0; i < st.PathNodes.Count; i++) {
                Color c = PathGold * 0.7f;
                c.A = 0;
                Main.EntitySpriteDraw(star, st.PathNodes[i] - Main.screenPosition, null, c,
                    i * 1.1f + Main.GlobalTimeWrappedHourly, star.Size() / 2f, 0.26f, SpriteEffects.None, 0);
            }
            if (st.PathNodes.Count < 2) {
                return;
            }
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Vector2 prev = SamplePath(st.PathNodes, 0f);
            const int steps = 20;
            for (int i = 1; i <= steps; i++) {
                Vector2 cur = SamplePath(st.PathNodes, i / (float)steps);
                Vector2 mid = (prev + cur) / 2f;
                float rot = (cur - prev).ToRotation();
                float lenScale = Vector2.Distance(prev, cur) / line.Width;
                Color c = PathGold * 0.30f;
                c.A = 0;
                Main.EntitySpriteDraw(line, mid - Main.screenPosition, null, c, rot,
                    line.Size() / 2f, new Vector2(lenScale, 0.16f), SpriteEffects.None, 0);
                prev = cur;
            }
        }
    }
}
