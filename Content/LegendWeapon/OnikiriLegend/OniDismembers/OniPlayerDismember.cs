using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs;
using InnoVault.GameSystem;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using OFR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers
{
    /// <summary>单个玩家的反噬肢解状态</summary>
    internal class PlayerDismemberEntry
    {
        public int PlayerIndex;
        public int Timer;
        /// <summary>定身锚点（触发帧的 player.Center）</summary>
        public Vector2 AnchorCenter;
        public float CutAngle;
        /// <summary>切线过身体的局部点（快照中心局部像素）</summary>
        public Vector2 CutPointLocal;
        /// <summary>切线单位法线，两半沿 ±法线分离</summary>
        public Vector2 CutNormal;
        /// <summary>快照 RT 尺寸，0=捕获降级（仅锁定无视觉）</summary>
        public int SnapWidth;
        public int SnapHeight;
        public bool Captured;
        public float DriftMax;
        //捕获帧的姿态参数、快照定格在反噬落下的那一瞬

        public int Direction;
        public float GravDir;
        public Rectangle BodyFrame;
        public Rectangle LegFrame;
        //单刀两片（退化时整身单片不滑）

        public readonly List<Vector2[]> Pieces = [];
        public readonly List<sbyte> PieceSides = [];
        public readonly List<float> PieceSpins = [];
        public readonly List<float> PieceJitters = [];
    }

    /// <summary>玩家肢解. 自伤+镜头</summary>
    internal class OniPlayerDismember : ICWRLoader
    {
        /// <summary>反噬必定伤害、最大生命比例，无视防御与闪避</summary>
        public const float SelfHurtFraction = 0.25f;
        /// <summary>伤口亮起 → 裂开的滞拍帧数</summary>
        public const int HoldFrames = 8;
        /// <summary>两半滑开帧数</summary>
        public const int DriftFrames = 16;
        /// <summary>裂着悬停的帧数</summary>
        public const int RestFrames = 8;
        /// <summary>回拢帧数</summary>
        public const int KnitFrames = 18;
        /// <summary>弥合收尾帧数（伤口线熄灭）</summary>
        public const int SealFrames = 6;
        /// <summary>总时长 = 锁定时长</summary>
        public const int TotalFrames = HoldFrames + DriftFrames + RestFrames + KnitFrames + SealFrames;
        //快照 RT 边长、兜住身体+翅膀/披风等装备层

        private const int SnapSize = 176;

        /// <summary>所有活跃反噬状态</summary>
        internal static readonly List<PlayerDismemberEntry> Entries = [];
        /// <summary>快照 RT 注册表（playerIndex → RT）</summary>
        internal static readonly Dictionary<int, RenderTarget2D> SnapRTs = [];

        void ICWRLoader.UnLoadData() {
            Entries.Clear();
            DisposeAllSnapshots();
        }

        /// <summary>该玩家是否处于反噬僵直（锁操控、隐本体、禁再肢解）</summary>
        public static bool IsLocked(Player player)
            => player != null && GetEntry(player.whoAmI) != null;

        /// <summary>反噬自伤结算中（同帧），铭刻的承伤增减/守护挂点据此放行这刀固定契约</summary>
        internal static bool SelfHurtResolving { get; private set; }

        /// <summary>落下反噬、必定伤害先落（owner 端结算，原版受伤包同步），玩家当帧定格</summary>
        public static void Trigger(Player player, float cutAngle) {
            if (Main.dedServ || player == null || !player.active || player.dead) {
                return;
            }

            //最终化 HurtInfo 直接结算固定伤害，不经过防御、减伤与闪避；

            //刀无善恶，残血强行肢解即自尽

            if (player.whoAmI == Main.myPlayer) {
                int selfDamage = Math.Max((int)(player.statLifeMax2 * SelfHurtFraction), 1);
                player.immune = false;
                player.immuneTime = 0;
                PlayerDeathReason deathReason = PlayerDeathReason.ByCustomReason(
                    OniPlayerDismemberSystem.SelfHurtDeathReason.ToNetworkText(player.name));
                Player.HurtInfo hurtInfo = new() {
                    DamageSource = deathReason,
                    SourceDamage = selfDamage,
                    Damage = selfDamage,
                    HitDirection = 0,
                    Knockback = 0f,
                    Dodgeable = false,
                    PvP = false,
                    CooldownCounter = -1,
                };
                SelfHurtResolving = true;
                try {
                    player.Hurt(hurtInfo);
                } finally {
                    SelfHurtResolving = false;
                }
                if (player.dead) {
                    return; //死亡流程接管，僵直与弥合都不再有意义

                }
            }

            Entries.RemoveAll(e => e.PlayerIndex == player.whoAmI);

            Vector2 dir = cutAngle.ToRotationVector2();
            PlayerDismemberEntry entry = new() {
                PlayerIndex = player.whoAmI,
                AnchorCenter = player.Center,
                CutAngle = cutAngle,
                //落刀点带一点身位内的随机偏移，避免每次都从正中剖开

                CutPointLocal = new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-12f, 12f)),
                CutNormal = new Vector2(-dir.Y, dir.X),
                SnapWidth = SnapSize,
                SnapHeight = SnapSize,
                DriftMax = Main.rand.NextFloat(9f, 13f),
                Direction = player.direction,
                GravDir = player.gravDir,
                BodyFrame = player.bodyFrame,
                LegFrame = player.legFrame,
            };
            BuildPieces(entry);
            Entries.Add(entry);

            //位移类挂点立即斩断、反噬期间人钉在原地

            if (player.whoAmI == Main.myPlayer) {
                if (player.mount?.Active == true) {
                    player.mount.Dismount(player);
                }
                player.RemoveAllGrapplingHooks();
            }
            player.velocity = Vector2.Zero;

            //伤口亮起、与肢解切口同语汇的嘶声

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.55f, Volume = 0.45f }, player.Center);
        }

        internal static PlayerDismemberEntry GetEntry(int playerIndex) {
            for (int i = 0; i < Entries.Count; i++) {
                if (Entries[i].PlayerIndex == playerIndex) {
                    return Entries[i];
                }
            }
            return null;
        }

        /// <summary>整身 quad 沿切线裁成两片；贴角掠过等退化情况保留整身单片不滑</summary>
        private static void BuildPieces(PlayerDismemberEntry entry) {
            float hw = entry.SnapWidth * 0.5f;
            float hh = entry.SnapHeight * 0.5f;
            Vector2[] quad = [new(-hw, -hh), new(hw, -hh), new(hw, hh), new(-hw, hh)];

            List<Vector2> pos = OniDismember.ClipHalfPlane(quad, entry.CutPointLocal, entry.CutNormal, 1f);
            List<Vector2> neg = OniDismember.ClipHalfPlane(quad, entry.CutPointLocal, entry.CutNormal, -1f);

            if (pos.Count >= 3 && neg.Count >= 3
                && OniDismember.PolyArea(pos) >= 64f && OniDismember.PolyArea(neg) >= 64f) {
                AddPiece(entry, [.. pos], 1);
                AddPiece(entry, [.. neg], -1);
            }
            else {
                AddPiece(entry, quad, 0);
            }
        }

        private static void AddPiece(PlayerDismemberEntry entry, Vector2[] verts, sbyte side) {
            entry.Pieces.Add(verts);
            entry.PieceSides.Add(side);
            entry.PieceSpins.Add(side * Main.rand.NextFloat(0.014f, 0.038f));
            entry.PieceJitters.Add(Main.rand.NextFloat(MathHelper.TwoPi));
        }

        /// <summary>裂开 → 悬停 → 回拢的位移曲线 0..1..0</summary>
        internal static float DriftCurve(int timer) {
            int t = timer - HoldFrames;
            if (t < 0) {
                return 0f;
            }
            if (t < DriftFrames) {
                return OFR.EaseOutCubic(t / (float)DriftFrames);
            }
            t -= DriftFrames;
            if (t < RestFrames) {
                return 1f;
            }
            t -= RestFrames;
            //回拢、缓入缓出，落回时不撞

            return 1f - MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(t / (float)KnitFrames, 0f, 1f));
        }

        /// <summary>碎片本帧位移与旋转；悬停段叠加僵直微颤（绷着的张力）</summary>
        internal static void GetPieceMotion(PlayerDismemberEntry entry, int index, out Vector2 offset, out float rotation) {
            float curve = DriftCurve(entry.Timer);
            sbyte side = entry.PieceSides[index];
            offset = entry.CutNormal * (side * entry.DriftMax * curve);
            rotation = entry.PieceSpins[index] * curve;
            if (curve >= 0.999f) {
                float t = Main.GlobalTimeWrappedHourly;
                float phase = entry.PieceJitters[index];
                offset.X += MathF.Sin(t * 22.7f + phase) * 0.35f;
                offset.Y += MathF.Cos(t * 18.3f + phase * 1.7f) * 0.35f;
            }
        }

        /// <summary>逐帧、挂 PostUpdatePlayers</summary>
        internal static void UpdateAll() {
            for (int i = Entries.Count - 1; i >= 0; i--) {
                PlayerDismemberEntry entry = Entries[i];
                Player player = Main.player[entry.PlayerIndex];
                if (!player.active || player.dead) {
                    Entries.RemoveAt(i);
                    continue;
                }

                entry.Timer++;
                if (entry.Timer >= TotalFrames) {
                    Entries.RemoveAt(i);
                    SealBurst(entry);
                    continue;
                }

                //钉死、反噬期间击退/重力全部无效，人立在原地承受

                player.Center = entry.AnchorCenter;
                player.velocity = Vector2.Zero;

                if (entry.Timer == HoldFrames) {
                    SplitBurst(entry);
                }

                Lighting.AddLight(entry.AnchorCenter, new Vector3(0.85f, 0.16f, 0.10f)
                    * (0.5f + 0.5f * DriftCurve(entry.Timer)));
            }
        }

        /// <summary>裂开瞬间、沿切线迸出碎晶 + 闷响（比敌方断口收敛，这是自己的身体）</summary>
        private static void SplitBurst(PlayerDismemberEntry entry) {
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f, Volume = 0.5f }, entry.AnchorCenter);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.6f, Volume = 0.45f }, entry.AnchorCenter);

            Vector2 tangent = new(-entry.CutNormal.Y, entry.CutNormal.X);
            for (int k = 0; k < 8; k++) {
                Vector2 pos = entry.AnchorCenter + entry.CutPointLocal
                    + tangent * Main.rand.NextFloat(-1f, 1f) * 26f;
                Vector2 vel = entry.CutNormal * Main.rand.NextFloat(1.2f, 3.4f) * (Main.rand.NextBool() ? 1f : -1f)
                    + tangent * Main.rand.NextFloat(-0.8f, 0.8f);
                Color c = Main.rand.NextBool(3) ? new Color(255, 238, 215) : new Color(255, 96, 58);
                PRTLoader.NewParticle<PRT_OniShard>(pos, vel, c, Main.rand.NextFloat(0.28f, 0.5f))
                    ?.Configure(Main.rand.Next(16, 26), Main.rand.NextFloat(-0.2f, 0.2f)
                        , Main.rand.NextFloat(1.2f, 2f), affectedByGravity: true);
            }
        }

        /// <summary>弥合瞬间、伤口收拢的轻响 + 内吸碎晶，人回来了</summary>
        private static void SealBurst(PlayerDismemberEntry entry) {
            SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.15f, Volume = 0.5f }, entry.AnchorCenter);
            SoundEngine.PlaySound(SoundID.Item35 with { Pitch = 0.5f, Volume = 0.3f, MaxInstances = 2 }, entry.AnchorCenter);

            Vector2 tangent = new(-entry.CutNormal.Y, entry.CutNormal.X);
            for (int k = 0; k < 6; k++) {
                //从两侧向切线内吸、裂开的反演

                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 pos = entry.AnchorCenter + entry.CutPointLocal
                    + tangent * Main.rand.NextFloat(-1f, 1f) * 22f
                    + entry.CutNormal * side * Main.rand.NextFloat(10f, 22f);
                Vector2 vel = -entry.CutNormal * side * Main.rand.NextFloat(1.5f, 3f);
                PRTLoader.NewParticle<PRT_OniShard>(pos, vel, new Color(255, 224, 200)
                    , Main.rand.NextFloat(0.22f, 0.4f))
                    ?.Configure(Main.rand.Next(10, 18), Main.rand.NextFloat(-0.15f, 0.15f)
                        , Main.rand.NextFloat(1f, 1.6f), affectedByGravity: false);
            }
        }

        /// <summary>取或建玩家专属快照 RT（仅绘制线程调用）</summary>
        internal static RenderTarget2D EnsureSnapshotRT(GraphicsDevice gd, PlayerDismemberEntry entry) {
            if (SnapRTs.TryGetValue(entry.PlayerIndex, out RenderTarget2D rt) && rt != null && !rt.IsDisposed
                && rt.Width == entry.SnapWidth && rt.Height == entry.SnapHeight) {
                return rt;
            }
            rt?.Dispose();
            try {
                rt = new RenderTarget2D(gd, entry.SnapWidth, entry.SnapHeight, false
                    , SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            } catch {
                return null;
            }
            SnapRTs[entry.PlayerIndex] = rt;
            return rt;
        }

        internal static void DisposeAllSnapshots() {
            foreach (RenderTarget2D rt in SnapRTs.Values) {
                rt.SafeDispose();
            }
            SnapRTs.Clear();
        }
    }

    /// <summary>反噬状态逐帧维护与世界卸载清理</summary>
    internal sealed class OniPlayerDismemberSystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.OnikiriText";

        /// <summary>反噬致死的死亡原因，{0}=玩家名</summary>
        public static LocalizedText SelfHurtDeathReason { get; private set; }

        public override void SetStaticDefaults() {
            SelfHurtDeathReason = this.GetLocalization(nameof(SelfHurtDeathReason)
                , () => "{0}被自己的一刀斩作了两段");
        }

        public override void PostUpdatePlayers() {
            if (!Main.dedServ) {
                OniPlayerDismember.UpdateAll();
            }
        }

        public override void OnWorldUnload() => OniPlayerDismember.Entries.Clear();
    }

    /// <summary>反噬僵直的操控锁、只清输入不碰位置（钉死在管理器里做），本人客户端生效</summary>
    internal class OniPlayerDismemberLock : ModPlayer
    {
        public override void SetControls() {
            if (!OniPlayerDismember.IsLocked(Player)) {
                return;
            }
            Player.controlLeft = Player.controlRight = false;
            Player.controlUp = Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = Player.controlUseTile = false;
            Player.controlHook = Player.controlMount = false;
            Player.controlThrow = false;
            //反噬期间禁持物、裂成两半的人挥不了刀

            Player.noItems = true;
        }
    }

    /// <summary>快照捕获完成后隐藏被反噬玩家的本体绘制（碎片接管）</summary>
    internal class OniPlayerDismemberHideOverride : PlayerOverride
    {
        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            PlayerDismemberEntry entry = OniPlayerDismember.GetEntry(Player.whoAmI);
            if (entry == null || !entry.Captured || entry.SnapWidth <= 0) {
                return true;
            }
            int hide = Player.whoAmI;
            players = players.Where(p => p.whoAmI != hide);
            return true;
        }
    }

    /// <summary>反噬肢解渲染、快照捕获 + 碎片绘制。 捕获</summary>
    internal sealed class OniPlayerDismemberRender : RenderHandle
    {
        private static readonly List<VertexPositionColorTexture> vertexScratch = [];
        private static readonly List<int> pruneScratch = [];
        private static Vector4[] cutLineParams = [];
        private static Vector4[] cutGlowParams = [];

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu) {
                if (OniPlayerDismember.Entries.Count > 0) {
                    OniPlayerDismember.Entries.Clear();
                }
                if (OniPlayerDismember.SnapRTs.Count > 0) {
                    OniPlayerDismember.DisposeAllSnapshots();
                }
            }
        }

        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }

            PruneOrphanRTs();

            if (!AnyPendingCapture()) {
                return;
            }
            //低质量光照/RT 异常时放弃捕获、仅锁定无碎片视觉，本体照常绘制

            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                return;
            }
            if (screenSwap == null || screenSwap.IsDisposed
                || Main.screenTarget == null || Main.screenTarget.IsDisposed
                || !RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                return;
            }

            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //先保屏、screenTarget 一旦重绑定内容即被丢弃

            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            foreach (PlayerDismemberEntry entry in OniPlayerDismember.Entries) {
                if (entry.Captured || entry.SnapWidth <= 0) {
                    continue;
                }
                Player player = Main.player[entry.PlayerIndex];
                if (!player.active) {
                    continue;
                }
                CaptureSnapshot(spriteBatch, graphicsDevice, entry, player);
            }

            //还屏

            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }

        /// <summary>玩家外观（本色傀儡，无手持物）→ 专属 RT；锚点落 RT 中央</summary>
        private static void CaptureSnapshot(SpriteBatch sb, GraphicsDevice gd, PlayerDismemberEntry entry, Player player) {
            RenderTarget2D rt = OniPlayerDismember.EnsureSnapshotRT(gd, entry);
            if (rt == null) {
                entry.SnapWidth = 0;    //显存异常、降级为仅锁定

                return;
            }

            gd.SetRenderTarget(rt);
            gd.Clear(Color.Transparent);

            Vector2 realScreenPos = Main.screenPosition;
            //伪造屏幕原点、玩家绘制层全部以 world -

            Main.screenPosition = entry.AnchorCenter - new Vector2(rt.Width, rt.Height) * 0.5f;

            //Immediate 批立刻装载单位矩阵顶点变换

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            try {
                PlayerCloneRenderer.Prepare(player);
                PlayerCloneRenderer.DrawPreparedNatural(entry.AnchorCenter - player.Size * 0.5f
                    , entry.Direction, entry.GravDir, entry.BodyFrame, entry.LegFrame);
            } catch {
                //玩家绘制层钩子异常不拖垮捕获管线

            } finally {
                Main.screenPosition = realScreenPos;
                try {
                    sb.End();
                } catch {
                }
            }
            entry.Captured = true;
        }

        private static bool AnyPendingCapture() {
            foreach (PlayerDismemberEntry entry in OniPlayerDismember.Entries) {
                if (!entry.Captured && entry.SnapWidth > 0) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>释放已无对应状态的快照 RT</summary>
        private static void PruneOrphanRTs() {
            if (OniPlayerDismember.SnapRTs.Count == 0) {
                return;
            }
            pruneScratch.Clear();
            foreach (int index in OniPlayerDismember.SnapRTs.Keys) {
                if (OniPlayerDismember.GetEntry(index) == null) {
                    pruneScratch.Add(index);
                }
            }
            foreach (int index in pruneScratch) {
                OniPlayerDismember.SnapRTs[index]?.Dispose();
                OniPlayerDismember.SnapRTs.Remove(index);
            }
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu || OniPlayerDismember.Entries.Count == 0) {
                return;
            }
            Effect fx = EffectLoader.OniDismember?.Value;
            if (fx == null) {
                return;
            }

            foreach (PlayerDismemberEntry entry in OniPlayerDismember.Entries) {
                if (!entry.Captured || entry.SnapWidth <= 0) {
                    continue;
                }
                if (!OniPlayerDismember.SnapRTs.TryGetValue(entry.PlayerIndex, out RenderTarget2D rt)
                    || rt == null || rt.IsDisposed) {
                    continue;
                }
                DrawEntry(entry, rt, fx);
            }
        }

        private static void DrawEntry(PlayerDismemberEntry entry, RenderTarget2D rt, Effect fx) {
            GraphicsDevice gd = Main.instance.GraphicsDevice;

            BlendState prevBlend = gd.BlendState;
            RasterizerState prevRaster = gd.RasterizerState;
            DepthStencilState prevDepth = gd.DepthStencilState;
            gd.BlendState = BlendState.AlphaBlend;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.DepthStencilState = DepthStencilState.None;

            BuildVertices(entry);
            if (vertexScratch.Count >= 3 && EnsureCutParamBuffers(fx) > 0) {
                SetShaderParams(entry, rt, fx);
                VertexPositionColorTexture[] verts = [.. vertexScratch];
                foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                    pass.Apply();
                    gd.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, verts.Length / 3);
                }
            }

            gd.BlendState = prevBlend;
            gd.RasterizerState = prevRaster;
            gd.DepthStencilState = prevDepth;
        }

        private static int EnsureCutParamBuffers(Effect fx) {
            int lineCapacity = fx.Parameters["uCutLine"]?.Elements.Count ?? 0;
            int glowCapacity = fx.Parameters["uCutGlow"]?.Elements.Count ?? 0;
            int capacity = Math.Min(lineCapacity, glowCapacity);
            if (capacity <= 0) {
                return 0;
            }
            if (cutLineParams.Length != capacity) {
                cutLineParams = new Vector4[capacity];
                cutGlowParams = new Vector4[capacity];
            }
            return capacity;
        }

        private static void SetShaderParams(PlayerDismemberEntry entry, RenderTarget2D rt, Effect fx) {
            float curve = OniPlayerDismember.DriftCurve(entry.Timer);
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSnapSize"]?.SetValue(new Vector2(entry.SnapWidth, entry.SnapHeight));
            //反噬的冷灰比敌方浅、人还活着，只是被斩开了一瞬

            fx.Parameters["uDesat"]?.SetValue(0.22f * curve);
            fx.Parameters["uDim"]?.SetValue(1f - 0.10f * curve);
            fx.Parameters["uColHot"]?.SetValue(new Vector3(1.85f, 1.62f, 1.30f));
            fx.Parameters["uColBright"]?.SetValue(new Vector3(1.55f, 0.28f, 0.14f));
            fx.Parameters["uSnapTex"]?.SetValue(rt);

            cutLineParams[0] = new Vector4(entry.CutPointLocal.X, entry.CutPointLocal.Y
                , entry.CutNormal.X, entry.CutNormal.Y);
            cutGlowParams[0] = new Vector4(GlowStrength(entry), GlowHalfWidth(entry), 0f, 0f);
            fx.Parameters["uCutLine"]?.SetValue(cutLineParams);
            fx.Parameters["uCutGlow"]?.SetValue(cutGlowParams);
            fx.Parameters["uCutCount"]?.SetValue(1);
            fx.Parameters["uDrawBase"]?.SetValue(1f);
        }

        /// <summary>伤口辉光、亮起闪 → 滞拍呼吸 → 裂开灼热 → 回拢升温 → 弥合过曝熄灭</summary>
        private static float GlowStrength(PlayerDismemberEntry entry) {
            int t = entry.Timer;
            if (t <= 2) {
                return 1.35f;
            }
            if (t < OniPlayerDismember.HoldFrames) {
                float breath = 0.5f + 0.5f * MathF.Sin(t * 0.6f - MathHelper.PiOver2);
                return 0.55f + 0.30f * breath;
            }
            int sealStart = OniPlayerDismember.TotalFrames - OniPlayerDismember.SealFrames;
            if (t >= sealStart) {
                //弥合、过曝一闪后熄灭

                float f = (t - sealStart) / (float)OniPlayerDismember.SealFrames;
                return MathHelper.Lerp(1.4f, 0f, f);
            }
            //裂开与回拢期、稳定灼热，回拢后段升温预示弥合

            float curve = OniPlayerDismember.DriftCurve(t);
            float knitHeat = 1f - curve;    //越合越烫

            return 0.85f + 0.35f * knitHeat + 0.05f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5.3f);
        }

        private static float GlowHalfWidth(PlayerDismemberEntry entry) {
            int t = entry.Timer;
            if (t < OniPlayerDismember.HoldFrames) {
                return 6f - 2.5f * t / OniPlayerDismember.HoldFrames;
            }
            return 3.4f;
        }

        /// <summary>两片 → 三角扇顶点（世界坐标）</summary>
        private static void BuildVertices(PlayerDismemberEntry entry) {
            vertexScratch.Clear();
            Vector2 snapHalf = new(entry.SnapWidth * 0.5f, entry.SnapHeight * 0.5f);
            int maxVerts = 0;
            for (int i = 0; i < entry.Pieces.Count; i++) {
                if (entry.Pieces[i].Length > maxVerts) {
                    maxVerts = entry.Pieces[i].Length;
                }
            }
            //一次 stackalloc，循环内复用（CA2014：循环内每次 stackalloc 到方法返回才释放）
            Span<Vector2> world = stackalloc Vector2[maxVerts];

            for (int i = 0; i < entry.Pieces.Count; i++) {
                Vector2[] piece = entry.Pieces[i];
                OniPlayerDismember.GetPieceMotion(entry, i, out Vector2 offset, out float rotation);

                //绕片质心旋转

                Vector2 centroid = Vector2.Zero;
                foreach (Vector2 v in piece) {
                    centroid += v;
                }
                centroid /= piece.Length;
                float sin = MathF.Sin(rotation);
                float cos = MathF.Cos(rotation);

                for (int k = 0; k < piece.Length; k++) {
                    Vector2 rel = piece[k] - centroid;
                    Vector2 spun = new(rel.X * cos - rel.Y * sin, rel.X * sin + rel.Y * cos);
                    world[k] = entry.AnchorCenter + centroid + spun + offset;
                }

                for (int k = 1; k < piece.Length - 1; k++) {
                    AppendVertex(world[0], piece[0], snapHalf, entry);
                    AppendVertex(world[k], piece[k], snapHalf, entry);
                    AppendVertex(world[k + 1], piece[k + 1], snapHalf, entry);
                }
            }
        }

        private static void AppendVertex(Vector2 worldPos, Vector2 localPos, Vector2 snapHalf, PlayerDismemberEntry entry) {
            Vector2 uv = new((localPos.X + snapHalf.X) / entry.SnapWidth
                , (localPos.Y + snapHalf.Y) / entry.SnapHeight);
            vertexScratch.Add(new VertexPositionColorTexture(worldPos.ToVector3(), Color.White, uv));
        }
    }
}
