using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 2026-08 扩展批 Npc 协议共用的实体钩子。<br/>
    /// 载荷改写/相位偏移要在弹幕出生一刻动手，固件回滚要在 AI 前后配对伪装，
    /// 躯壳征用要接死亡边，相位偏移还要一条重绘通道——
    /// 这些时机协议基类都没有，集中挂在这一个文件里，不去动既有的分派点
    /// </summary>
    internal class HackNpcSourceProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        /// <summary>本弹已被载荷改写；权威端在 OnSpawn 里点亮，随 extraAI 到达各端</summary>
        public bool Rewritten;

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (source is not EntitySource_Parent { Entity: NPC npc } || !npc.active) {
                return;
            }

            //相位偏移：出生点跟着表现层的偏移走，攻击从"看起来的位置"打出。
            //OnSpawn 在 NewProjectile 的生成包发出之前跑，改完的落点会进首包，
            //远端不需要自己再算一遍
            ActiveHackEffect desync =
                HackEffectTracker.GetEffect<PhaseDesync>(npc.whoAmI);
            if (desync != null) {
                projectile.position += PhaseDesync.GetOffset(desync.Elapsed);
            }

            //载荷改写：只动真的会伤人的敌对弹
            if (!projectile.hostile || projectile.damage <= 0) return;
            ActiveHackEffect rewrite =
                HackEffectTracker.GetEffect<PayloadRewrite>(npc.whoAmI);
            if (rewrite == null) return;
            Player caster = ResolvePlayer(rewrite.CasterIndex);
            if (caster == null) return;

            Rewritten = true;
            ApplyRewrite(projectile, PayloadRewrite.ComputeDamageCap(caster));
            if (Main.netMode != NetmodeID.Server) PayloadRewrite.EmitFlip(projectile);
        }

        /// <summary>
        /// 敌我翻转在每个端各自落地：hostile / friendly 不在任何原版同步包里，
        /// 只在权威端翻等于单机正常联机空炮。<br/>
        /// owner 保持原样（NPC 弹一般是 255）：改成玩家索引会把服务端唯一的推送通道
        /// 关掉，还会让客户端按 owner+identity 反查未果、把同步包当成新弹再生成一发
        /// ——这是 <see cref="ProjectileHijack"/> 修复后的既有裁决，此处照抄。
        /// 于是改判后的 NPC 命中由服务端结算（伤害靠 SyncNPC 回传），
        /// 各端只负责把标志翻对，让玩家碰撞判定与观感在每台机器上一致
        /// </summary>
        internal static void ApplyRewrite(Projectile projectile, int damageCap) {
            projectile.hostile = false;
            projectile.friendly = true;
            projectile.DamageType = DamageClass.Generic;
            if (damageCap > 0) {
                projectile.damage = Math.Min(projectile.damage, damageCap);
            }
        }

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter,
            BinaryWriter binaryWriter) {
            //必须无条件写：extraAI 没有逐 Global 的分段头，别的模组一旦写了数据，
            //收端就会把所有 Global 的 Receive 都跑一遍，少写一位全线错位
            bitWriter.WriteBit(Rewritten);
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader,
            BinaryReader binaryReader) {
            bool rewritten = bitReader.ReadBit();
            if (!rewritten) return;
            bool firstSeen = !Rewritten;
            Rewritten = true;
            //封顶后的伤害已在权威端算好、随包内 damage 字段到达，这里只补翻标志
            ApplyRewrite(projectile, 0);
            if (firstSeen && Main.netMode != NetmodeID.Server) {
                PayloadRewrite.EmitFlip(projectile);
            }
        }

        private static Player ResolvePlayer(int index) {
            if (index < 0 || index >= Main.maxPlayers) return null;
            Player player = Main.player[index];
            return player?.active == true ? player : null;
        }
    }

    /// <summary>固件回滚的血量伪装、暴走余韵，躯壳征用的死亡边，相位偏移的幽灵重绘</summary>
    internal class HackNpcProtocolNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>固件回滚到期后的暴走余帧；各端从协议的移除钩各自点燃（实例字段不进同步包）</summary>
        public int FirmwareFrenzy;

        private static readonly Color FrenzyRed = new(255, 90, 50);

        private struct SpoofEntry
        {
            public int Life;
            public int Type;
        }

        //本帧被伪装的 npc 槽位 → 真实血量。PostAI 无条件还原
        //（tML 源码核过：NPCAI 里 PostAI 不受 PreAI 返回 false 影响），
        //PreAI 顶部的自愈与帧末兜底只挡异常中断
        private static readonly Dictionary<int, SpoofEntry> spoofedLife = [];

        //幽灵重绘会重入本类与其它 GlobalNPC 的绘制钩，靠这道闸断环
        private static bool drawingGhost;

        public override void Unload() {
            spoofedLife.Clear();
        }

        #region 固件回滚：AI 通道血量伪装

        public override bool PreAI(NPC npc) {
            //上一帧的残账自愈：正常路径 PostAI 一定还原，这里只兜异常
            if (spoofedLife.TryGetValue(npc.whoAmI, out SpoofEntry stale)) {
                if (stale.Type == npc.type) npc.life = stale.Life;
                spoofedLife.Remove(npc.whoAmI);
            }

            //各端都要撒谎：客户端也在本地模拟 NPC AI 做预测，只骗服务端会来回抽搐
            if (npc.life > 0
                && HackEffectTracker.HasEffect<FirmwareRollback>(npc.whoAmI)) {
                spoofedLife[npc.whoAmI] = new SpoofEntry {
                    Life = npc.life,
                    Type = npc.type,
                };
                //取 Max：真实血量高于伪装线时不反向压血
                npc.life = Math.Max(npc.life,
                    (int)(npc.lifeMax * FirmwareRollback.SpoofLifeRatio));
            }
            return true;
        }

        public override void PostAI(NPC npc) {
            if (spoofedLife.TryGetValue(npc.whoAmI, out SpoofEntry entry)) {
                if (entry.Type == npc.type) npc.life = entry.Life;
                spoofedLife.Remove(npc.whoAmI);
            }

            if (FirmwareFrenzy > 0) {
                FirmwareFrenzy--;
                //暴走：不逐帧乘 velocity（对积累式 AI 会复利跑飞），直接补 30% 位移
                npc.position += npc.velocity * 0.3f;
                if (Main.netMode != NetmodeID.Server && FirmwareFrenzy % 6 == 0) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                        npc.width * 0.45f, npc.height * 0.45f);
                    PRTLoader.NewParticle<PRT_Spark>(pos,
                        -npc.velocity.SafeNormalize(Vector2.UnitY) * 2f,
                        FrenzyRed, 0.7f)?.Configure(false, 14);
                }
            }
        }

        /// <summary>帧末兜底：AI 阶段异常中断时把没还的血量还回去；类型不符说明槽位已被复用，弃账</summary>
        internal static void FlushSpoofLeftovers() {
            if (spoofedLife.Count == 0) return;
            foreach (KeyValuePair<int, SpoofEntry> pair in spoofedLife) {
                if (pair.Key < 0 || pair.Key >= Main.maxNPCs) continue;
                NPC npc = Main.npc[pair.Key];
                if (npc.active && npc.type == pair.Value.Type) {
                    npc.life = pair.Value.Life;
                }
            }
            spoofedLife.Clear();
        }

        #endregion

        #region 躯壳征用：死亡边

        public override void OnKill(NPC npc) {
            //OnKill 只跑在权威端（checkDead 的战利品路径客户端早退），双保险再闸一次
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            ActiveHackEffect effect =
                HackEffectTracker.GetEffect<ShellRequisition>(npc.whoAmI);
            if (effect != null && !effect.Replicated) {
                ShellRequisition.OnMarkedKill(npc, effect);
            }
        }

        #endregion

        #region 相位偏移：幽灵重绘 + 判定线框

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            //幽灵那次重绘不压暗：鲜活的躯体画在偏移处，真身才是残壳
            if (drawingGhost) return;
            if (!HackEffectTracker.HasEffect<PhaseDesync>(npc.whoAmI)) return;
            drawColor = new Color(
                (int)(drawColor.R * 0.32f),
                (int)(drawColor.G * 0.38f),
                (int)(drawColor.B * 0.46f),
                (int)(drawColor.A * 0.6f));
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch,
            Vector2 screenPos, Color drawColor) {
            if (drawingGhost || Main.dedServ || !npc.active) return;
            ActiveHackEffect effect =
                HackEffectTracker.GetEffect<PhaseDesync>(npc.whoAmI);
            if (effect == null) return;

            //借原版绘制管线整体重画一遍：位移只存在于这一次调用的栈帧里，
            //还原写在 finally，不给任何"渗进游戏逻辑"的窗口
            Vector2 realPos = npc.position;
            drawingGhost = true;
            try {
                npc.position = realPos + PhaseDesync.GetOffset(effect.Elapsed);
                Main.instance.DrawNPCDirect(spriteBatch, npc, npc.behindTiles, screenPos);
            }
            finally {
                npc.position = realPos;
                drawingGhost = false;
            }

            DrawHitboxFrame(spriteBatch, npc);
        }

        //真实判定的细线框：会读框的玩家可以无视幻影，这是协议留出的反制窗口
        private static void DrawHitboxFrame(SpriteBatch sb, NPC npc) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) return;
            Rectangle src = new(0, 0, 1, 1);
            Rectangle box = npc.Hitbox;
            box.Offset((int)-Main.screenPosition.X, (int)-Main.screenPosition.Y);
            Color line = HackTheme.Accent * 0.8f;
            const int T = 2;
            sb.Draw(pixel, new Rectangle(box.X, box.Y, box.Width, T), src, line);
            sb.Draw(pixel, new Rectangle(box.X, box.Bottom - T, box.Width, T), src, line);
            sb.Draw(pixel, new Rectangle(box.X, box.Y, T, box.Height), src, line);
            sb.Draw(pixel, new Rectangle(box.Right - T, box.Y, T, box.Height), src, line);
        }

        #endregion
    }

    /// <summary>血量伪装的帧末兜底 pass，正常帧应当空转</summary>
    internal class HackNpcSpoofBackstop : ModSystem
    {
        public override void PostUpdateNPCs() => HackNpcProtocolNPC.FlushSpoofLeftovers();
    }
}
