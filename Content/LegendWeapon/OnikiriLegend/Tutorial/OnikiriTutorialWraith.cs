using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    /// <summary>
    /// 鬼切教程专用练习鬼影 NPC。
    /// 伤害 0、无掉落、不可自然生成；死亡时复位生命而非真正死亡。
    /// 绘制对齐正式鬼影雾影+鬼火眼（不用占位贴图当本体）。
    /// </summary>
    internal sealed class OnikiriTutorialWraith : ModNPC
    {
        private static readonly Dictionary<int, int> serverTargets = [];
        private static int localNpcIndex = -1;

        internal enum WraithPose : byte
        {
            Idle = 0,
            DashChannel = 1,
            PaperOffset = 2,
        }

        internal WraithPose CurrentPose { get; private set; } = WraithPose.Idle;

        internal void SetPose(WraithPose pose) => CurrentPose = pose;

        private float visualPhase;
        private Vector2 homePos;
        private bool homeSet;

        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults()
        {
            NPC.width = 44;
            NPC.height = 88;
            NPC.aiStyle = -1;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 99999;
            NPC.HitSound = Terraria.ID.SoundID.NPCHit1;
            NPC.DeathSound = Terraria.ID.SoundID.NPCDeath1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;
            NPC.dontTakeDamage = false;
            NPC.friendly = false;
            NPC.lavaImmune = true;
            NPC.ShowNameOnHover = true;
        }

        public override void SetStaticDefaults()
        {
            Terraria.ID.NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new() { Hide = true };
            Terraria.ID.NPCID.Sets.NPCBestiaryDrawOffset[Type] = drawModifiers;
        }

        public override void AI()
        {
            visualPhase += 0.045f;
            Player owner = ResolveOwner();
            if (owner == null || !owner.active) {
                NPC.velocity *= 0.9f;
                return;
            }

            if (!homeSet) {
                homePos = FindStandPos(owner);
                homeSet = true;
                NPC.Center = homePos;
            }

            //按姿态微调锚点：疾走通道略偏、面影错位到另一侧
            Vector2 target = homePos;
            float side = owner.direction == 0 ? 1f : owner.direction;
            target = CurrentPose switch {
                WraithPose.DashChannel => homePos + new Vector2(side * 40f, -12f),
                WraithPose.PaperOffset => homePos + new Vector2(-side * 160f, -20f),
                _ => homePos,
            };

            //缓跟锚点 + 轻微上下浮动
            Vector2 bob = new(0f, MathF.Sin(visualPhase) * 5f);
            Vector2 desire = target + bob - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desire * 0.12f, 0.18f);
            if (desire.LengthSquared() > 420f * 420f) {
                //玩家跑远则重锚，避免鬼影掉出视野
                homePos = FindStandPos(owner);
                NPC.Center = homePos;
                NPC.velocity = Vector2.Zero;
            }

            NPC.spriteDirection = owner.Center.X >= NPC.Center.X ? 1 : -1;
            NPC.timeLeft = 600;
        }

        public override bool CheckDead()
        {
            NPC.life = NPC.lifeMax;
            NPC.active = true;
            return false;
        }

        public override bool? CanBeHitByProjectile(Projectile projectile)
            => null;

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            DrawBody(spriteBatch, screenPos);
            return false;
        }

        /// <summary>雾影三层 + 鬼火眼（世界坐标批，不用 UIScaleMatrix）</summary>
        private void DrawBody(SpriteBatch sb, Vector2 screenPos)
        {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }

            Rectangle src = new(0, 0, 1, 1);
            Color body = OnikiriUITheme.Ink;
            Color rim = OnikiriUITheme.GhostDim;
            Color eye = OnikiriUITheme.GhostFire;
            const float alpha = 0.92f;

            Vector2 center = NPC.Center - screenPos;
            Vector2 size = NPC.Size;
            float bob = MathF.Sin(visualPhase) * 4f;
            Vector2 half = new(0.5f);

            //外晕
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Color add = new Color(rim.R, rim.G, rim.B, (byte)0);
                sb.Draw(glow, center + new Vector2(0f, bob), null, add * (alpha * 0.35f), 0f,
                    glow.Size() * 0.5f, new Vector2(size.X * 2.4f / glow.Width, size.Y * 2.1f / glow.Height),
                    SpriteEffects.None, 0f);
            }

            for (int i = 0; i < 3; i++) {
                float sway = MathF.Sin(visualPhase * (0.8f + i * 0.31f) + i * 2.1f) * (3f + i * 2f);
                float yOffset = size.Y * (0.30f - i * 0.27f);
                Vector2 pos = center + new Vector2(sway, bob + yOffset);
                Vector2 scale = new(size.X * (0.92f - i * 0.18f), size.Y * 0.46f);
                Color layer = Color.Lerp(body, rim, i * 0.22f) * (alpha * (0.42f - i * 0.08f));
                sb.Draw(pixel, pos, src, layer, 0f, half, scale, SpriteEffects.None, 0f);
            }

            //鬼火眼
            float flick = 0.75f + 0.25f * MathF.Sin(visualPhase * 6.3f);
            Vector2 eyeBase = center + new Vector2(0f, bob - size.Y * 0.24f);
            const float EyeSide = 0.14f;
            for (int side = -1; side <= 1; side += 2) {
                Vector2 eyePos = eyeBase + new Vector2(side * size.X * EyeSide, 0f);
                sb.Draw(pixel, eyePos, src, eye * (alpha * 0.35f * flick), 0f, half, new Vector2(8f, 5.5f), SpriteEffects.None, 0f);
                sb.Draw(pixel, eyePos, src, eye * (alpha * 0.95f * flick), 0f, half, new Vector2(3.4f, 2.6f), SpriteEffects.None, 0f);
            }

            //底缘焦痕，练习靶可读
            sb.Draw(pixel, center + new Vector2(0f, size.Y * 0.42f + bob), src,
                OnikiriUITheme.Deep * (alpha * 0.35f), 0f, half, new Vector2(size.X * 0.55f, 2.2f), SpriteEffects.None, 0f);
        }

        private Player ResolveOwner()
        {
            int who = (int)NPC.ai[0];
            if (who < 0 || who >= Main.maxPlayers) {
                return null;
            }
            Player p = Main.player[who];
            return p.active ? p : null;
        }

        private static Vector2 FindStandPos(Player p)
        {
            float side = p.direction == 0 ? 1f : p.direction;
            Vector2 prefer = p.Center + new Vector2(side * 180f, -20f);
            //向下扫一点找空位，避免卡进实心块
            for (int i = 0; i < 12; i++) {
                Vector2 tryPos = prefer + new Vector2(0f, i * 8f);
                if (!Collision.SolidCollision(tryPos - new Vector2(22f, 44f), 44, 88)) {
                    return tryPos;
                }
            }
            return prefer;
        }

        //====静态 API====

        internal static void EnsureLocalTarget()
        {
            if (localNpcIndex >= 0 && localNpcIndex < Main.maxNPCs && Main.npc[localNpcIndex].active
                && Main.npc[localNpcIndex].type == ModContent.NPCType<OnikiriTutorialWraith>())
                return;

            Player p = Main.LocalPlayer;
            Vector2 spawnPos = FindStandPos(p);
            localNpcIndex = NPC.NewNPC(p.GetSource_Misc("CWR_OnikiriTutorial"),
                (int)spawnPos.X, (int)spawnPos.Y, ModContent.NPCType<OnikiriTutorialWraith>(),
                ai0: p.whoAmI);
        }

        internal static void ReleaseLocalTarget()
        {
            if (localNpcIndex >= 0 && localNpcIndex < Main.maxNPCs)
            {
                NPC npc = Main.npc[localNpcIndex];
                if (npc.active && npc.type == ModContent.NPCType<OnikiriTutorialWraith>())
                    npc.active = false;
            }
            localNpcIndex = -1;
        }

        internal static int EnsureServerTarget(int playerWhoAmI)
        {
            if (!VaultUtils.isServer) return -1;
            if (serverTargets.TryGetValue(playerWhoAmI, out int existing)
                && existing >= 0 && existing < Main.maxNPCs
                && Main.npc[existing].active
                && Main.npc[existing].type == ModContent.NPCType<OnikiriTutorialWraith>())
                return existing;

            Player p = playerWhoAmI >= 0 && playerWhoAmI < Main.maxPlayers
                ? Main.player[playerWhoAmI] : null;
            if (p == null || !p.active) return -1;

            Vector2 spawnPos = FindStandPos(p);
            int idx = NPC.NewNPC(p.GetSource_Misc("CWR_OnikiriTutorial"),
                (int)spawnPos.X, (int)spawnPos.Y, ModContent.NPCType<OnikiriTutorialWraith>(),
                ai0: playerWhoAmI);
            serverTargets[playerWhoAmI] = idx;
            return idx;
        }

        internal static void ReleaseServerTarget(int playerWhoAmI)
        {
            if (!VaultUtils.isServer) return;
            if (!serverTargets.TryGetValue(playerWhoAmI, out int idx)) return;
            serverTargets.Remove(playerWhoAmI);
            if (idx >= 0 && idx < Main.maxNPCs)
            {
                NPC npc = Main.npc[idx];
                if (npc.active && npc.type == ModContent.NPCType<OnikiriTutorialWraith>())
                    npc.active = false;
            }
        }

        internal static void OnServerTargetConfirmed(int npcIndex)
            => localNpcIndex = npcIndex;

        internal static void OnServerTargetReleased()
            => localNpcIndex = -1;

        internal static void OnPoseSynced(byte pose)
        {
            if (localNpcIndex < 0 || localNpcIndex >= Main.maxNPCs) return;
            NPC npc = Main.npc[localNpcIndex];
            if (npc.active && npc.ModNPC is OnikiriTutorialWraith w)
                w.CurrentPose = (WraithPose)pose;
        }

        internal static NPC GetLocalTarget()
        {
            if (localNpcIndex < 0 || localNpcIndex >= Main.maxNPCs) return null;
            NPC npc = Main.npc[localNpcIndex];
            return npc.active && npc.type == ModContent.NPCType<OnikiriTutorialWraith>() ? npc : null;
        }

        internal static void ClearServerState()
        {
            serverTargets.Clear();
            localNpcIndex = -1;
        }
    }
}
