using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles;
using CalamityOverhaul.Content.TimeFreezes;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips
{
    /// <summary>
    /// 单个被鞭目标的标记状态。owner 本地量（命中判定与收益全在攻击方端），
    /// 键控 <see cref="NetworkNPCIdentity"/> 防槽位复用继承脏层数；
    /// 跨端可见性由 <see cref="GsWhipSealPulseProj"/> 脉冲真弹幕承载
    /// </summary>
    internal sealed class WhipMarkState
    {
        /// <summary>鞭痕层衰减窗（帧）：超时未续鞭即清层</summary>
        internal const int MarkDecayFrames = 300;
        /// <summary>处决印宽限窗（帧）：转印后给玩家十秒组织踩拍</summary>
        internal const int SealDecayFrames = 600;

        /// <summary>当前鞭痕层数</summary>
        internal int Stacks;
        /// <summary>最近一次鞭中该目标的世界帧</summary>
        internal uint LastHitTick;
        /// <summary>处决印已点亮（鞭痕满层转化）</summary>
        internal bool ExecuteReady;
        /// <summary>处决印点亮时刻</summary>
        internal uint ExecuteReadyTick;
        /// <summary>登记的鞭面板伤（处决/连环爆的折算基数）</summary>
        internal int MarkDamage;
        /// <summary>鞭刑余韵截止帧：引爆后 120f 内自家召唤物 +10%</summary>
        internal uint AfterglowUntil;
        /// <summary>皮鞭处决追加：截止前自家仆从 +15%</summary>
        internal uint LeatherBoostUntil;
        /// <summary>万花筒棱彩暴露截止帧：期间自家召唤物 10% 概率强制暴击</summary>
        internal uint PrismExposeUntil;
        /// <summary>标记归属鞭的物品 ID（决定印记配色与层数上限口径）</summary>
        internal int SourceItemType;

        /// <summary>惰性衰减：读取前调用，超时的层与印就地清空</summary>
        internal void Refresh(uint now) {
            if (Stacks > 0 && now - LastHitTick > MarkDecayFrames && !ExecuteReady) {
                Stacks = 0;
            }
            if (ExecuteReady && now - ExecuteReadyTick > SealDecayFrames) {
                ExecuteReady = false;
                Stacks = 0;
            }
        }

        /// <summary>还持有任何活性状态（层/印/余韵/专属时限）</summary>
        internal bool IsAlive(uint now)
            => Stacks > 0 || ExecuteReady
            || now < AfterglowUntil || now < LeatherBoostUntil || now < PrismExposeUntil;
    }

    /// <summary>
    /// 鞭刑节拍的每玩家状态载体。节拍连击/挥击窗口/鞭痕字典全部是
    /// owner 本地玩法量（命中链只在攻击方端执行），不同步、不发包；
    /// 远端玩家实例上这些字段恒为初值，读到即无效果，天然联机安全
    /// </summary>
    internal class GsWhipPlayer : ModPlayer
    {
        //==================== 节拍状态（仅 myPlayer 路径消费） ====================

        /// <summary>当前节拍归属的鞭物品 ID，换鞭即重置</summary>
        internal int WhipItemType;
        /// <summary>本次挥击动画结束帧，即 on-beat 窗口开启帧；0 = 无在途挥击</summary>
        internal uint SwingEndTick;
        /// <summary>本次窗口宽度（帧），按实际动画帧数动态换算</summary>
        internal int WindowFrames;
        /// <summary>本次挥击是否已命中过任何敌人（空挥判定）</summary>
        internal bool SwingHasHit;
        /// <summary>本次挥击的收尾（空挥惩罚）是否已结算</summary>
        internal bool SwingSettled = true;
        /// <summary>本次挥击是否踩拍</summary>
        internal bool SwingOnBeat;
        /// <summary>本次挥击命中的敌人数</summary>
        internal int SwingHitCount;
        /// <summary>本次挥击命中过的 npc.whoAmI（波尼鞭连骨振排除已中目标用）</summary>
        internal readonly HashSet<int> SwingHitNPCs = [];

        //==================== 鞭痕字典（owner 本地） ====================

        /// <summary>鞭痕/处决印登记表，键 = 网络身份防槽位复用</summary>
        internal readonly Dictionary<NetworkNPCIdentity, WhipMarkState> Marks = [];

        private uint nextPulseTick;
        private uint nextSweepTick;
        private static readonly List<NetworkNPCIdentity> sweepBuffer = [];

        /// <summary>节拍全清（换鞭/超窗归零/空挥 Reset 政策）</summary>
        internal void ResetTempo() {
            SwingEndTick = 0;
            WindowFrames = 0;
            SwingHasHit = false;
            SwingSettled = true;
            SwingOnBeat = false;
            SwingHitCount = 0;
            SwingHitNPCs.Clear();
            BeatCombo = 0;
        }

        /// <summary>节拍连击层数（0~5）</summary>
        internal int BeatCombo;

        /// <summary>取或建该目标的标记条目；身份无效（假人边界等）返回 null</summary>
        internal WhipMarkState GetOrCreateMark(NPC npc) {
            if (!NetworkNPCIdentity.TryCapture(npc, out NetworkNPCIdentity id)) {
                return null;
            }
            if (!Marks.TryGetValue(id, out WhipMarkState st)) {
                st = new WhipMarkState();
                Marks[id] = st;
            }
            st.Refresh(Main.GameUpdateCount);
            return st;
        }

        /// <summary>查询该目标仍有活性的标记；顺带做惰性衰减</summary>
        internal bool TryGetMark(NPC npc, out WhipMarkState st) {
            st = null;
            if (Marks.Count == 0) {
                return false;
            }
            if (!NetworkNPCIdentity.TryCapture(npc, out NetworkNPCIdentity id)
                || !Marks.TryGetValue(id, out st)) {
                return false;
            }
            st.Refresh(Main.GameUpdateCount);
            return st.IsAlive(Main.GameUpdateCount);
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer || !GameModeSystem.GodSmithActive) {
                return;
            }
            uint now = Main.GameUpdateCount;
            //低频清扫：失活条目与身份失效条目出表，防字典无界增长
            if (now >= nextSweepTick) {
                nextSweepTick = now + 120;
                SweepDead(now);
            }
            //处决印脉冲：每秒一发全端可见的印记微光真弹幕（队友借此看见处决就绪）
            if (now >= nextPulseTick) {
                nextPulseTick = now + 60;
                foreach (KeyValuePair<NetworkNPCIdentity, WhipMarkState> kv in Marks) {
                    kv.Value.Refresh(now);
                    if (!kv.Value.ExecuteReady || !kv.Key.TryResolve(out NPC npc)) {
                        continue;
                    }
                    Projectile.NewProjectile(Player.GetSource_Misc("GsWhipSealPulse"),
                        npc.Center, Vector2.Zero, ModContent.ProjectileType<GsWhipSealPulseProj>(),
                        0, 0f, Player.whoAmI, npc.whoAmI, kv.Value.SourceItemType);
                }
            }
        }

        private void SweepDead(uint now) {
            sweepBuffer.Clear();
            foreach (KeyValuePair<NetworkNPCIdentity, WhipMarkState> kv in Marks) {
                kv.Value.Refresh(now);
                if (!kv.Value.IsAlive(now) || !kv.Key.TryResolve(out _)) {
                    sweepBuffer.Add(kv.Key);
                }
            }
            foreach (NetworkNPCIdentity id in sweepBuffer) {
                Marks.Remove(id);
            }
            sweepBuffer.Clear();
        }
    }

    /// <summary>
    /// 鞭痕的收益出口与印记可视化。加成结算发生在弹幕命中结算端
    /// （= 召唤物 owner 端），读的是该端本地玩家自己的鞭痕字典，
    /// 天然只有「自家召唤物」吃到加成，各端结论确定
    /// </summary>
    internal class GsWhipMarkNPC : GlobalNPC
    {
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            //自建钩子：模式门自查
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            //口径对齐 1.4.4 鞭 tag：仆从/哨兵本体与其派生弹一并吃鞭痕收益
            bool summonHit = projectile.minion || projectile.sentry
                || ProjectileID.Sets.MinionShot[projectile.type]
                || ProjectileID.Sets.SentryShot[projectile.type];
            if (!summonHit || projectile.owner < 0 || projectile.owner >= Main.maxPlayers) {
                return;
            }
            Player owner = Main.player[projectile.owner];
            if (owner?.active != true
                || !owner.GetModPlayer<GsWhipPlayer>().TryGetMark(npc, out WhipMarkState st)) {
                return;
            }
            uint now = Main.GameUpdateCount;
            float bonus = 0.02f * st.Stacks;
            if (now < st.AfterglowUntil) {
                bonus += 0.10f;   //鞭刑余韵
            }
            if (now < st.LeatherBoostUntil) {
                bonus += 0.15f;   //皮鞭处决追加
            }
            if (bonus > 0f) {
                modifiers.FinalDamage *= 1f + bonus;
            }
            //棱彩暴露：+10% 暴击的真实实现（结算端只跑一次，掷点安全）
            if (now < st.PrismExposeUntil && Main.rand.NextBool(10)) {
                modifiers.SetCrit();
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //印记常显是标记归属者的个人读数（队友看真弹幕脉冲），读本地玩家字典
            if (!GameModeSystem.GodSmithActive || Main.dedServ) {
                return;
            }
            GsWhipPlayer mp = Main.LocalPlayer?.GetModPlayer<GsWhipPlayer>();
            if (mp == null || mp.Marks.Count == 0 || !mp.TryGetMark(npc, out WhipMarkState st)) {
                return;
            }
            if (!st.ExecuteReady && st.Stacks <= 0) {
                return;
            }
            Texture2D star = CWRUtils.GetT2DAsset(CWRConstant.Masking + "StarTexture")?.Value;
            Texture2D cross = CWRUtils.GetT2DAsset(CWRConstant.Masking + "RayCross01")?.Value;
            Texture2D dot = CWRUtils.GetT2DAsset(CWRConstant.Masking + "StarTexture_White")?.Value;
            if (star == null || cross == null || dot == null) {
                return;
            }
            GsWhipScheme scheme = GsWhipScheme.SchemeOfItem(st.SourceItemType);
            float phase = npc.whoAmI * 0.77f;   //去同相：多目标印记不许齐闪
            Vector2 anchor = npc.Top + new Vector2(0f, -20f) - screenPos;

            if (st.ExecuteReady) {
                //处决印：金红星章缓旋 + 金色十字反旋，呼吸按 identity 相位错开
                float pulse = 0.82f + 0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4.2f + phase);
                float rot = Main.GlobalTimeWrappedHourly * 1.15f + phase;
                Color sealRed = new Color(255, 92, 46) { A = 0 };
                Color sealGold = new Color(255, 206, 96) { A = 0 };
                spriteBatch.Draw(star, anchor, null, sealRed * (0.85f * pulse), rot,
                    star.Size() * 0.5f, 0.26f * pulse, SpriteEffects.None, 0f);
                spriteBatch.Draw(cross, anchor, null, sealGold * (0.68f * pulse), -rot * 0.6f,
                    cross.Size() * 0.5f, 0.15f, SpriteEffects.None, 0f);
                return;
            }
            //鞭痕层：头顶一排微光点，配色随归属鞭（万花筒逐层点亮五色）
            for (int i = 0; i < st.Stacks; i++) {
                Color c = scheme?.MarkLayerColor(i) ?? new Color(255, 200, 120);
                c.A = 0;
                Vector2 pos = anchor + new Vector2((i - (st.Stacks - 1) * 0.5f) * 11f, 0f);
                float flick = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + phase + i * 1.3f);
                spriteBatch.Draw(dot, pos, null, c * (0.75f * flick), 0f,
                    dot.Size() * 0.5f, 0.085f, SpriteEffects.None, 0f);
            }
        }
    }
}
