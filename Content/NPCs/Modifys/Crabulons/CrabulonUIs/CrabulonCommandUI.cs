using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons.CrabulonUIs
{
    /// <summary>指令枚举</summary>
    internal enum CrabCommand
    {
        Crouch,
        Recall,
        Unsaddle,
        Release
    }

    /// <summary>指令环状态机，世界锚定；绘见CrabulonClusterRenderer；骑乘/装鞍仍走世界交互</summary>
    internal class CrabulonCommandController : ModPlayer
    {
        public static CrabulonCommandController Local => Main.LocalPlayer?.GetModPlayer<CrabulonCommandController>();

        public ModifyCrabulon Focus { get; private set; }
        public float ButtonAppear { get; private set; }
        public float ButtonHover { get; private set; }
        public float InfoAppear { get; private set; }
        public bool WheelOpen { get; private set; }
        public float WheelProgress { get; private set; }
        public int HoveredCommand { get; private set; } = -1;
        public bool ReleaseArmed { get; private set; }
        public float Time { get; private set; }

        public IReadOnlyList<CrabCommand> ActiveCommands => activeCommands;
        public float CommandHover(int i) => i >= 0 && i < cmdHover.Length ? cmdHover[i] : 0f;

        private readonly List<CrabCommand> activeCommands = [];
        private float[] cmdHover = [];
        private ModifyCrabulon wheelTarget;
        private int lastHover = -1;

        private readonly List<Spore> spores = [];
        public IReadOnlyList<Spore> Spores => spores;
        private float sporeTimer;

        //展开键出现距离
        private const float DetectRange = 360f;

        public override void PostUpdate() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            Time += 1f / 60f;

            ModifyCrabulon hovered = ScanCrabulons(out ModifyCrabulon nearest);
            UpdateFocus(hovered, nearest);
            HandleInput();
            UpdateSpores();
            UpdateProgress();
        }

        public override void UpdateDead() {
            if (Player.whoAmI == Main.myPlayer && WheelOpen) {
                CloseWheel(true);
            }
        }

        //扫本机已驯蟹，回悬停/最近
        private ModifyCrabulon ScanCrabulons(out ModifyCrabulon nearest) {
            ModifyCrabulon hovered = null;
            nearest = null;
            //近两帧无蟹盖戳（世上没有菌生蟹）：跳过全表扫描
            if (!ModifyCrabulon.PresenceStamp.ActiveWithin()) {
                return null;
            }
            float nearestDistSq = DetectRange * DetectRange;

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != CWRID.NPC_Crabulon || !npc.TryGetOverride<ModifyCrabulon>(out var m) || m == null) {
                    continue;
                }
                if (m.FeedValue <= 0f || m.Owner == null || m.Owner.whoAmI != Player.whoAmI) {
                    continue;
                }

                if (m.uiOldLife < 0) {
                    m.uiOldLife = m.npc.life;
                }
                if (m.npc.life < m.uiOldLife) {
                    m.uiDamageFlash = 1f;
                }
                m.uiOldLife = m.npc.life;
                if (m.uiDamageFlash > 0f) {
                    m.uiDamageFlash = MathF.Max(0f, m.uiDamageFlash - 1f / 75f);
                }

                if (m.Mount) {
                    continue;//骑乘走骑乘血条
                }
                if (m.hoverNPC) {
                    hovered = m;
                }
                float distSq = m.npc.DistanceSQ(Player.Center);
                if (distSq < nearestDistSq) {
                    nearestDistSq = distSq;
                    nearest = m;
                }
            }
            return hovered;
        }

        private void UpdateFocus(ModifyCrabulon hovered, ModifyCrabulon nearest) {
            //开环锁焦点
            ModifyCrabulon desired = WheelOpen ? wheelTarget : (hovered ?? nearest);
            if (desired != null && !desired.Mount) {
                Focus = desired;
            }

            bool active = Focus != null && Focus.npc.Alives() && !Focus.Mount && Focus.FeedValue > 0f;
            ButtonAppear = MathHelper.Lerp(ButtonAppear, active ? 1f : 0f, 0.15f);

            bool wantInfo = active && (Focus.hoverNPC || WheelOpen);
            InfoAppear = MathHelper.Lerp(InfoAppear, wantInfo ? 1f : 0f, 0.18f);

            if (!active && !WheelOpen && ButtonAppear < 0.01f && InfoAppear < 0.01f) {
                ButtonAppear = 0f;
                InfoAppear = 0f;
                Focus = null;
            }
        }

        private void HandleInput() {
            if (Main.mapFullscreen || Player.dead) {
                if (WheelOpen) {
                    CloseWheel(true);
                }
                ButtonHover = MathHelper.Lerp(ButtonHover, 0f, 0.2f);
                HoveredCommand = -1;
                return;
            }
            if (Focus == null) {
                if (WheelOpen) {
                    CloseWheel(true);
                }
                ButtonHover = MathHelper.Lerp(ButtonHover, 0f, 0.2f);
                HoveredCommand = -1;
                return;
            }

            //目标失效收起
            if (WheelOpen && (wheelTarget == null || !wheelTarget.npc.Alives() || wheelTarget.Mount
                || wheelTarget.FeedValue <= 0f
                || wheelTarget.npc.DistanceSQ(Player.Center) > DetectRange * 1.4f * (DetectRange * 1.4f))) {
                CloseWheel(true);
            }

            Vector2 buttonWorld = CrabulonClusterRenderer.ButtonWorld(Focus, Time, WheelProgress);
            bool overButton = ButtonAppear > 0.4f
                && (Main.MouseWorld - buttonWorld).Length() < CrabulonClusterRenderer.ButtonHitRadius;
            ButtonHover = MathHelper.Lerp(ButtonHover, overButton ? 1f : 0f, 0.2f);

            //环命中/悬停
            if (WheelOpen) {
                HoveredCommand = WorldHitTest(wheelTarget);
                if (HoveredCommand != lastHover) {
                    if (HoveredCommand >= 0) {
                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.35f, Pitch = 0.1f });
                    }
                    lastHover = HoveredCommand;
                }
                for (int i = 0; i < cmdHover.Length; i++) {
                    cmdHover[i] = MathHelper.Lerp(cmdHover[i], i == HoveredCommand ? 1f : 0f, 0.25f);
                }
            }
            else {
                HoveredCommand = -1;
            }

            //占鼠标仅键/环上
            if (overButton || (WheelOpen && (HoveredCommand >= 0 || WithinWheelBand(wheelTarget)))) {
                Player.mouseInterface = true;
            }

            //开环右键取消
            if (WheelOpen && Main.mouseRight && Main.mouseRightRelease) {
                Main.mouseRightRelease = false;
                CloseWheel(false);
                return;
            }

            if (!(Main.mouseLeft && Main.mouseLeftRelease)) {
                return;
            }

            //左键分发
            if (overButton) {
                if (WheelOpen) {
                    CloseWheel(false);
                }
                else {
                    OpenWheel(Focus);
                }
                Main.mouseLeftRelease = false;
                return;
            }
            if (!WheelOpen) {
                return;
            }
            if (HoveredCommand >= 0) {
                OnPetalClicked(HoveredCommand);
                Main.mouseLeftRelease = false;
                return;
            }
            //环内死区收起吃点击，环外收起放行
            float bodyR = CrabulonClusterRenderer.BodyRadius(wheelTarget.npc);
            if ((Main.MouseWorld - wheelTarget.npc.Center).Length() < bodyR + 12f) {
                CloseWheel(false);
                Main.mouseLeftRelease = false;
            }
            else {
                CloseWheel(true);
            }
        }

        private void OnPetalClicked(int index) {
            if (index < 0 || index >= activeCommands.Count || wheelTarget == null || !wheelTarget.npc.Alives()) {
                return;
            }
            CrabCommand cmd = activeCommands[index];

            if (cmd == CrabCommand.Release) {
                if (!ReleaseArmed) {
                    ReleaseArmed = true;//首击武装，再确认
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.45f, Pitch = -0.4f });
                }
                else {
                    DoRelease(wheelTarget);
                    CloseWheel(true);
                }
                return;
            }

            ReleaseArmed = false;
            switch (cmd) {
                case CrabCommand.Crouch:
                    SoundEngine.PlaySound(CWRSound.ButtonZero);
                    wheelTarget.Crouch = !wheelTarget.Crouch;
                    wheelTarget.SendNetWork();
                    break;//切换后保持开环
                case CrabCommand.Recall:
                    wheelTarget.Networking.SendRecallRequest();
                    SoundEngine.PlaySound(SoundID.Item6 with { Volume = 0.4f, Pitch = 0.3f });
                    break;
                case CrabCommand.Unsaddle:
                    DoUnsaddle(wheelTarget);
                    CloseWheel(true);//鞍具变则收起
                    break;
            }
        }

        private void OpenWheel(ModifyCrabulon target) {
            if (target == null || !target.npc.Alives() || target.Mount) {
                return;
            }
            wheelTarget = target;
            activeCommands.Clear();
            activeCommands.Add(CrabCommand.Crouch);
            activeCommands.Add(CrabCommand.Recall);
            if (target.SaddleItem.Alives()) {
                activeCommands.Add(CrabCommand.Unsaddle);
            }
            activeCommands.Add(CrabCommand.Release);
            cmdHover = new float[activeCommands.Count];
            HoveredCommand = -1;
            lastHover = -1;
            ReleaseArmed = false;
            WheelOpen = true;
            target.uiCommandOpen = true;//开环屏蔽右键上马
            SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.4f, Pitch = 0.25f }, target.npc.Center);
        }

        private void CloseWheel(bool silent) {
            if (WheelOpen && !silent) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.35f, Pitch = -0.1f });
            }
            if (wheelTarget != null) {
                wheelTarget.uiCommandOpen = false;
            }
            WheelOpen = false;
            HoveredCommand = -1;
            lastHover = -1;
            ReleaseArmed = false;
            wheelTarget = null;
        }

        private void DoUnsaddle(ModifyCrabulon m) {
            if (!m.SaddleItem.Alives()) {
                return;
            }
            if (m.Mount) {
                m.CloseMount();
            }
            VaultUtils.SpwanItem(m.npc.FromObjectGetParent(), m.npc.Top, new Vector2(32), m.SaddleItem);
            m.SaddleItem.TurnToAir();
            SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f });
            m.SendNetWork();
        }

        private void DoRelease(ModifyCrabulon m) {
            if (!m.npc.Alives()) {
                return;
            }
            string name = m.npc.GivenOrTypeName;
            m.ReleaseTame();
            m.SendNetWork();
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.5f, Pitch = -0.3f });
            CombatText.NewText(Player.Hitbox, new Color(120, 220, 150), string.Format(ModifyCrabulon.ReleasedText.Value, name));
        }

        private void UpdateProgress() {
            float target = WheelOpen ? 1f : 0f;
            WheelProgress = MathHelper.Lerp(WheelProgress, target, WheelOpen ? 0.24f : 0.3f);
            if (MathF.Abs(WheelProgress - target) < 0.004f) {
                WheelProgress = target;
            }
        }

        #region 孢子
        internal struct Spore
        {
            public Vector2 WorldPos;
            public Vector2 Vel;
            public float Life;
            public float MaxLife;
            public float Size;
            public Color Color;
        }

        private void UpdateSpores() {
            for (int i = spores.Count - 1; i >= 0; i--) {
                Spore s = spores[i];
                s.Life -= 1f / 60f;
                s.WorldPos += s.Vel;
                s.Vel *= 0.97f;
                s.Vel.Y -= 0.02f;
                spores[i] = s;
                if (s.Life <= 0f) {
                    spores.RemoveAt(i);
                }
            }
            if (Focus == null || !Focus.npc.Alives() || InfoAppear <= 0.3f) {
                return;
            }
            sporeTimer += 1f;
            if (sporeTimer > 6f && spores.Count < 22) {
                sporeTimer = 0f;
                float bodyR = CrabulonClusterRenderer.BodyRadius(Focus.npc);
                Vector2 pos = Focus.npc.Center
                    + new Vector2(Main.rand.NextFloat(-bodyR, bodyR), Main.rand.NextFloat(-bodyR * 0.5f, bodyR * 0.2f));
                spores.Add(new Spore {
                    WorldPos = pos,
                    Vel = new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-0.7f, -0.2f)),
                    Life = Main.rand.NextFloat(1.2f, 2.4f),
                    MaxLife = 2.4f,
                    Size = Main.rand.NextFloat(1.5f, 3f),
                    Color = Color.Lerp(CrabulonClusterRenderer.Cyan, CrabulonClusterRenderer.Green, Main.rand.NextFloat()),
                });
            }
        }
        #endregion

        //极坐标命中，-1死区/环外
        private int WorldHitTest(ModifyCrabulon m) {
            int count = activeCommands.Count;
            if (count <= 0 || m == null) {
                return -1;
            }
            Vector2 off = Main.MouseWorld - m.npc.Center;
            float dist = off.Length();
            float bodyR = CrabulonClusterRenderer.BodyRadius(m.npc);
            if (dist < bodyR + 12f || dist > bodyR + 72f) {
                return -1;
            }
            if (count == 1) {
                return 0;
            }
            float ang = WrapTwoPi(MathF.Atan2(off.Y, off.X) + MathHelper.PiOver2);
            float sectorSize = MathHelper.TwoPi / count;
            float shifted = WrapTwoPi(ang + sectorSize * 0.5f);
            return Math.Clamp((int)(shifted / sectorSize), 0, count - 1);
        }

        private bool WithinWheelBand(ModifyCrabulon m) {
            if (m == null) {
                return false;
            }
            float dist = (Main.MouseWorld - m.npc.Center).Length();
            float bodyR = CrabulonClusterRenderer.BodyRadius(m.npc);
            return dist >= bodyR && dist <= bodyR + 76f;
        }

        internal static float WrapTwoPi(float a) {
            while (a < 0f) {
                a += MathHelper.TwoPi;
            }
            while (a >= MathHelper.TwoPi) {
                a -= MathHelper.TwoPi;
            }
            return a;
        }

        //首扇区朝上顺时针
        public static void GetSectorAngles(int idx, int count, out float aStart, out float aEnd) {
            if (count <= 0) {
                aStart = aEnd = 0f;
                return;
            }
            float sectorSize = MathHelper.TwoPi / count;
            float mid = -MathHelper.PiOver2 + idx * sectorSize;
            float gap = count > 1 ? 0.14f : 0f;
            aStart = mid - sectorSize * 0.5f + gap * 0.5f;
            aEnd = mid + sectorSize * 0.5f - gap * 0.5f;
        }
    }

    /// <summary>世界锚定矢量绘，复用HalibutRenderer；ModifyCrabulon.PostDraw逐蟹调</summary>
    internal static class CrabulonClusterRenderer
    {
        internal static readonly Color Cyan = new(70, 220, 205);
        internal static readonly Color Green = new(130, 230, 130);
        internal static readonly Color Glow = new(165, 255, 235);
        internal static readonly Color Dark = new(6, 18, 16);
        internal static readonly Color Danger = new(255, 95, 95);
        internal static readonly Color Warm = new(255, 205, 120);
        internal static readonly Color TextCol = new(200, 245, 235);

        //展开键缩放=点击半径
        internal const float ButtonBaseScale = 2f;
        internal const float ButtonHitRadius = 5.2f * ButtonBaseScale + 6f;

        public static float BodyRadius(NPC npc) => MathHelper.Clamp(MathF.Max(npc.width, npc.height) * 0.5f, 40f, 90f);

        //展开键蟹背上，开环上抬
        public static Vector2 ButtonWorld(ModifyCrabulon m, float time, float wheelProgress = 0f)
            => m.npc.Top + new Vector2(0f, -16f - MathHelper.Clamp(wheelProgress, 0f, 1f) * 46f + MathF.Sin(time * 2f) * 1.6f);

        public static void DrawWorldCluster(SpriteBatch sb, ModifyCrabulon m) {
            if (Main.dedServ || m == null || !m.npc.Alives() || m.FeedValue <= 0f || m.Mount) {
                return;
            }
            CrabulonCommandController ctrl = CrabulonCommandController.Local;
            if (ctrl == null) {
                return;
            }

            bool focused = ctrl.Focus == m;
            float info = focused ? ctrl.InfoAppear : 0f;
            float wheel = focused ? ctrl.WheelProgress : 0f;
            float button = focused ? ctrl.ButtonAppear : 0f;
            float arcAppear = MathF.Max(info, m.uiDamageFlash);
            if (arcAppear < 0.01f && wheel < 0.01f && button < 0.01f) {
                return;
            }

            float time = ctrl.Time;
            Vector2 center = m.npc.Center - Main.screenPosition;
            float bodyR = BodyRadius(m.npc);

            if (focused) {
                DrawSpores(sb, ctrl, MathF.Max(arcAppear, button));
            }
            if (arcAppear > 0.01f) {
                DrawVitalHalo(sb, m, center, bodyR, arcAppear, time);
            }
            if (wheel > 0.01f) {
                DrawCommandWheel(sb, m, ctrl, center, bodyR, wheel, time);
            }
            if (button > 0.01f) {
                DrawBloomButton(sb, m, ctrl, button, wheel, time);
            }
            if (info > 0.01f) {
                DrawTitle(sb, m, ctrl, center, bodyR, info, wheel, time);
                if (!ctrl.WheelOpen) {
                    DrawSaddleHover(sb, m, info);
                }
            }
        }

        private static void DrawSpores(SpriteBatch sb, CrabulonCommandController ctrl, float alpha) {
            if (alpha < 0.01f) {
                return;
            }
            foreach (CrabulonCommandController.Spore s in ctrl.Spores) {
                float lr = s.MaxLife > 0f ? s.Life / s.MaxLife : 0f;
                Color c = (s.Color with { A = 0 }) * (lr * alpha * 0.7f);
                HalibutRenderer.DrawSoftGlow(sb, s.WorldPos - Main.screenPosition, s.Size * 2.4f, c);
            }
        }

        //生命/饱食顶弧
        private static void DrawVitalHalo(SpriteBatch sb, ModifyCrabulon m, Vector2 center, float bodyR, float alpha, float time) {
            float hp = m.npc.lifeMax > 0 ? MathHelper.Clamp((float)m.npc.life / m.npc.lifeMax, 0f, 1f) : 0f;
            float feed = MathHelper.Clamp(m.FeedValue / CrabulonConstants.MaxFeedValue, 0f, 1f);
            float breath = 0.5f + 0.5f * MathF.Sin(time * 2f);

            float hpR = bodyR * 0.86f;
            float feedR = bodyR * 0.68f;
            const float mid = -MathHelper.PiOver2;
            const float half = 1.18f;
            float a0 = mid - half;
            float a1 = mid + half;

            //生命弧
            HalibutRenderer.DrawArcStroke(sb, center, hpR, a0, a1, 4.5f, Dark * (0.7f * alpha));
            Color hpCol = hp > 0.5f ? Color.Lerp(Green, Cyan, (hp - 0.5f) * 2f) : Color.Lerp(Danger, Green, hp * 2f);
            if (m.uiDamageFlash > 0.01f) {
                hpCol = Color.Lerp(hpCol, Danger, m.uiDamageFlash);
            }
            float hpEnd = MathHelper.Lerp(a0, a1, hp);
            HalibutRenderer.DrawArcStroke(sb, center, hpR, a0, hpEnd, 3.6f, hpCol * alpha);
            HalibutRenderer.DrawSoftGlow(sb, center + HalibutRenderer.AngleDir(hpEnd) * hpR, 6f,
                (hpCol with { A = 0 }) * (alpha * (0.4f + breath * 0.3f)));

            //饱食弧
            float fa0 = a0 + 0.14f;
            float fa1 = a1 - 0.14f;
            HalibutRenderer.DrawArcStroke(sb, center, feedR, fa0, fa1, 2.6f, Dark * (0.6f * alpha));
            Color feedCol = Color.Lerp(new Color(80, 180, 110), Cyan, feed);
            float feedEnd = MathHelper.Lerp(fa0, fa1, feed);
            HalibutRenderer.DrawArcStroke(sb, center, feedR, fa0, feedEnd, 2f, feedCol * (0.9f * alpha));
        }

        private static void DrawCommandWheel(SpriteBatch sb, ModifyCrabulon m, CrabulonCommandController ctrl,
            Vector2 center, float bodyR, float progress, float time) {
            IReadOnlyList<CrabCommand> cmds = ctrl.ActiveCommands;
            int count = cmds.Count;
            if (count == 0) {
                return;
            }
            float a = MathHelper.Clamp(progress, 0f, 1f);
            float ease = VaultUtils.EaseOutBack(a);
            float rIn = MathHelper.Lerp(bodyR + 4f, bodyR + 12f, ease);
            float rOut = MathHelper.Lerp(bodyR + 12f, bodyR + 60f, ease);
            float iconR = (rIn + rOut) * 0.5f;

            //底环
            HalibutRenderer.DrawRing(sb, center, rIn - 3f, 1.2f, Cyan * (0.22f * a));

            for (int i = 0; i < count; i++) {
                CrabulonCommandController.GetSectorAngles(i, count, out float aStart, out float aEnd);
                float hover = ctrl.CommandHover(i);
                bool armedRelease = cmds[i] == CrabCommand.Release && ctrl.ReleaseArmed;
                Color accent = CommandColor(cmds[i]);
                if (armedRelease) {
                    hover = MathF.Max(hover, 0.6f + 0.4f * (0.5f + 0.5f * MathF.Sin(time * 8f)));
                }

                //菌盖底板
                Color bg = Color.Lerp(Dark, Color.Lerp(Dark, accent, 0.4f), hover) * (0.85f * a);
                HalibutRenderer.DrawArc(sb, center, rIn, rOut, aStart, aEnd, bg);
                if (hover > 0.01f) {
                    HalibutRenderer.DrawArc(sb, center, rIn, rOut, aStart, aEnd, accent * (hover * 0.18f * a));
                }

                //描边封口
                Color border = Color.Lerp(accent * 0.7f, Glow, hover);
                HalibutRenderer.DrawArcStroke(sb, center, rOut - 0.5f, aStart, aEnd, 1.4f, border * a);
                HalibutRenderer.DrawArcStroke(sb, center, rIn + 0.5f, aStart, aEnd, 1f, border * (0.55f * a));
                Vector2 dS = HalibutRenderer.AngleDir(aStart);
                Vector2 dE = HalibutRenderer.AngleDir(aEnd);
                HalibutRenderer.DrawLine(sb, center + dS * rIn, center + dS * rOut, 1.1f, border * (0.5f * a));
                HalibutRenderer.DrawLine(sb, center + dE * rIn, center + dE * rOut, 1.1f, border * (0.5f * a));

                //放生武装红环
                if (armedRelease) {
                    HalibutRenderer.DrawArcStroke(sb, center, rOut + 3f, aStart, aEnd, 2.2f, Danger * a);
                }

                float midA = (aStart + aEnd) * 0.5f;
                Vector2 iconPos = center + HalibutRenderer.AngleDir(midA) * iconR;
                DrawCommandGlyph(sb, cmds[i], m, iconPos, accent, hover, a);
            }

            //放生确认提示
            if (ctrl.HoveredCommand >= 0 && ctrl.HoveredCommand < count
                && cmds[ctrl.HoveredCommand] == CrabCommand.Release && ctrl.ReleaseArmed) {
                HalibutRenderer.DrawGlowTextCentered(sb, ModifyCrabulon.ReleaseConfirmText.Value,
                    center + new Vector2(0f, rOut + 16f), Danger * a, Color.Black * (0.4f * a), 0.7f);
            }
        }

        private static void DrawCommandGlyph(SpriteBatch sb, CrabCommand cmd, ModifyCrabulon m,
            Vector2 iconPos, Color accent, float hover, float alpha) {
            string label = CommandLabel(cmd, m);
            Color textColor = Color.Lerp(TextCol, Color.White, hover) * alpha;
            Color glowColor = accent * (0.3f * alpha);

            if (cmd == CrabCommand.Unsaddle && m.SaddleItem.Alives()) {
                VaultUtils.SimpleDrawItem(sb, m.SaddleItem.type, iconPos + new Vector2(0f, -11f), 22, 0.7f, 0f, Color.White * alpha);
                HalibutRenderer.DrawGlowTextCentered(sb, label, iconPos + new Vector2(0f, 8f), textColor, glowColor, 0.58f);
                return;
            }
            HalibutRenderer.DrawGlowTextCentered(sb, label, iconPos, textColor, glowColor, 0.62f + hover * 0.05f);
        }

        //展开键 +→x
        private static void DrawBloomButton(SpriteBatch sb, ModifyCrabulon m, CrabulonCommandController ctrl,
            float appear, float wheel, float time) {
            Vector2 pos = ButtonWorld(m, time, wheel) - Main.screenPosition;
            float hover = ctrl.ButtonHover;
            float pulse = 0.6f + 0.4f * MathF.Sin(time * 3f);
            float scale = ButtonBaseScale + hover * 0.4f;
            Color accent = Color.Lerp(Cyan, Glow, hover);

            HalibutRenderer.DrawSoftGlow(sb, pos, 11f * scale, (accent with { A = 0 }) * (appear * (0.35f + pulse * 0.3f)));
            HalibutRenderer.DrawDisc(sb, pos, 5.2f * scale, 1.4f, Dark * (0.92f * appear));
            HalibutRenderer.DrawRing(sb, pos, 5.2f * scale, 1.2f, accent * appear);

            float s = 3.1f * scale;
            float rot = MathHelper.Clamp(wheel, 0f, 1f) * MathHelper.PiOver4;
            Vector2 ax = HalibutRenderer.AngleDir(rot) * s;
            Vector2 ay = HalibutRenderer.AngleDir(rot + MathHelper.PiOver2) * s;
            Color glyph = accent * appear;
            HalibutRenderer.DrawLine(sb, pos - ax, pos + ax, 1.5f, glyph);
            HalibutRenderer.DrawLine(sb, pos - ay, pos + ay, 1.5f, glyph);

            //悬停展开键提示
            if (hover > 0.25f && wheel < 0.05f) {
                HalibutRenderer.DrawGlowTextCentered(sb, ModifyCrabulon.CommandHintText.Value,
                    pos + new Vector2(0f, -16f), TextCol * (appear * hover * 0.85f), Color.Black * (0.3f * appear), 0.52f);
            }
        }

        private static void DrawTitle(SpriteBatch sb, ModifyCrabulon m, CrabulonCommandController ctrl,
            Vector2 center, float bodyR, float info, float wheel, float time) {
            float topExtent = MathHelper.Lerp(bodyR + 30f, bodyR + 74f, MathHelper.Clamp(wheel, 0f, 1f));
            Vector2 namePos = center + new Vector2(0f, -topExtent);
            float breath = 0.5f + 0.5f * MathF.Sin(time * 2f);

            string name = m.npc.GivenOrTypeName;
            HalibutRenderer.DrawGlowTextCentered(sb, name, namePos,
                Color.Lerp(TextCol, Glow, breath * 0.25f) * info, Cyan * (0.3f * info), 0.78f);

            string status;
            Color sc;
            if (m.Crouch) {
                status = ModifyCrabulon.StatusRestText.Value;
                sc = new Color(110, 180, 255);
            }
            else if (m.Mount) {
                status = ModifyCrabulon.StatusMountText.Value;
                sc = Warm;
            }
            else {
                status = ModifyCrabulon.StatusFollowText.Value;
                sc = Green;
            }
            float pulse = 0.7f + 0.3f * MathF.Sin(time * 2.6f);
            HalibutRenderer.DrawGlowTextCentered(sb, status, namePos + new Vector2(0f, 15f),
                sc * (info * pulse), sc * (0.2f * info), 0.6f);
        }

        //鞍具悬停提示
        private static void DrawSaddleHover(SpriteBatch sb, ModifyCrabulon m, float alpha) {
            if (!m.hoverNPC) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player.Alives() && player.CWR().IsRotatingDuringDash) {
                return;
            }

            Item held = player.GetItem();
            Item saddleToDraw = null;
            string content = "";
            bool canDraw = false;

            if (held.type == ModContent.ItemType<MushroomSaddle>()) {
                canDraw = true;
                saddleToDraw = held;
                content = m.SaddleItem.Alives() ? ModifyCrabulon.ChangeSaddleText.Value : ModifyCrabulon.MountHoverText.Value;
            }
            else if (m.SaddleItem.Alives()) {
                canDraw = true;
                saddleToDraw = m.SaddleItem;
                content = m.Mount ? ModifyCrabulon.DismountText.Value : ModifyCrabulon.RideHoverText.Value;
            }
            if (!canDraw) {
                return;
            }

            Vector2 itemPos = Main.MouseWorld - Main.screenPosition + new Vector2(0f, 32f);
            if (saddleToDraw.Alives()) {
                VaultUtils.SimpleDrawItem(sb, saddleToDraw.type, itemPos, 32, 1f, 0f, Color.White * alpha);
            }

            Color textColor = VaultUtils.MultiStepColorLerp(MathF.Abs(MathF.Sin(Main.GameUpdateCount * 0.02f)), Cyan, Glow);
            Vector2 hoverSize = FontAssets.MouseText.Value.MeasureString(content) * 0.9f;
            Vector2 hoverPos = itemPos + new Vector2(0f, 36f);
            Utils.DrawBorderStringFourWay(sb, FontAssets.MouseText.Value, content,
                hoverPos.X, hoverPos.Y, textColor * alpha, Color.Black * alpha, hoverSize / 2f, 0.9f);
        }

        private static Color CommandColor(CrabCommand cmd) => cmd switch {
            CrabCommand.Crouch => Cyan,
            CrabCommand.Recall => Green,
            CrabCommand.Unsaddle => Warm,
            _ => Danger,
        };

        private static string CommandLabel(CrabCommand cmd, ModifyCrabulon m) => cmd switch {
            CrabCommand.Crouch => m.Crouch ? ModifyCrabulon.CrouchAltText.Value : ModifyCrabulon.CrouchText.Value,
            CrabCommand.Recall => ModifyCrabulon.RecallText.Value,
            CrabCommand.Unsaddle => ModifyCrabulon.UnsaddleText.Value,
            _ => ModifyCrabulon.ReleaseText.Value,
        };
    }
}
