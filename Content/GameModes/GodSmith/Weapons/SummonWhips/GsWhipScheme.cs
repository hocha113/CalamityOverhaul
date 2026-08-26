using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips
{
    /// <summary>空挥（本次挥击无命中）的节拍惩罚政策</summary>
    internal enum MissPolicyKind
    {
        /// <summary>不罚（皮鞭教学位专属）</summary>
        None,
        /// <summary>连击 -1</summary>
        Step,
        /// <summary>全归零（万花筒高手向专属）</summary>
        Reset
    }

    /// <summary>
    /// 鞭族方案基类：鞭刑节拍状态机的唯一实现处。<br/>
    /// 节拍口径：挥鞭动画结束帧起开 W 帧 on-beat 窗，窗内开始下一挥 = 踩拍；
    /// 踩拍叠连击（上限 5，每层挥速 +4%），空挥按政策罚，超窗归零。
    /// W 按实际动画帧数动态换算（攻速 buff 生效时窗口同步缩短，节奏感不漂）。<br/>
    /// 标记口径：鞭命中叠「鞭痕」层（自家召唤物对其 +2%/层），满层转「处决印」，
    /// 下一次踩拍命中引爆：owner 生成真弹幕演出（全端可见）+ 120f 鞭刑余韵。<br/>
    /// 九鞭全部保留原版鞭弹幕与原版 tag buff（GsShoot 返回 null 直通，
    /// 不压原版 AI），增强全走路由打标 + 类型通道
    /// </summary>
    internal abstract class GsWhipScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "SummonWhips";

        //==================== 子类参数面 ====================

        /// <summary>原版鞭弹幕类型（打标通道的命中入口）</summary>
        public abstract int WhipProjType { get; }

        /// <summary>默认动画帧数下的基准 on-beat 窗口（帧）；实际窗口按攻速同步缩放</summary>
        public abstract int BaseWindowFrames { get; }

        /// <summary>鞭痕层数上限 M，满层转处决印</summary>
        public abstract int MarkCap { get; }

        /// <summary>空挥惩罚政策</summary>
        public virtual MissPolicyKind MissPolicy => MissPolicyKind.Step;

        /// <summary>节拍连击上限</summary>
        public virtual int BeatComboCap => 5;

        /// <summary>面板伤微调旋钮（±10% 内，机制层承担主要强度）</summary>
        public virtual float DamageTweak => 1f;

        /// <summary>标记主题色：印记、升温拖尾、拍点微光的基色</summary>
        public abstract Color MarkColor { get; }

        /// <summary>鞭痕层数点的逐层配色（万花筒五色专用重写点）</summary>
        public virtual Color MarkLayerColor(int layer) => MarkColor;

        /// <summary>处决印是否由「踩拍鞭击」引爆；火鞭改由原版爆炸引爆故关掉</summary>
        protected virtual bool ExecuteByWhipHit => true;

        /// <summary>按物品 ID 反查鞭族方案（印记绘制/脉冲配色用）</summary>
        internal static GsWhipScheme SchemeOfItem(int itemType)
            => TryGetScheme(itemType, out GodSmithScheme scheme) ? scheme as GsWhipScheme : null;

        //==================== 数值 ====================

        public sealed override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= DamageTweak;

        /// <summary>踩拍升速：每层 +4%，封顶 +20%。远端玩家的节拍字段恒 0，读到即 1f</summary>
        public sealed override float GsUseSpeedMultiplier(Item item, Player player) {
            GsWhipPlayer mp = player.GetModPlayer<GsWhipPlayer>();
            if (mp.WhipItemType != item.type || mp.BeatCombo <= 0) {
                return 1f;
            }
            return 1f + 0.04f * Math.Min(mp.BeatCombo, BeatComboCap);
        }

        //==================== 节拍状态机 ====================

        /// <summary>射击链只在 owner 端执行：节拍判定与登记天然本地</summary>
        public sealed override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            GsWhipPlayer mp = player.GetModPlayer<GsWhipPlayer>();
            uint now = Main.GameUpdateCount;
            if (mp.WhipItemType != item.type) {
                mp.ResetTempo();
                mp.WhipItemType = item.type;
            }
            SettleSwing(mp, now);
            //踩拍判定：上一挥动画结束帧起 W 帧内开始本挥即踩拍；超窗归零
            bool onBeat = false;
            if (mp.SwingEndTick != 0) {
                if (now <= mp.SwingEndTick + (uint)mp.WindowFrames) {
                    onBeat = now >= mp.SwingEndTick;
                }
                else {
                    mp.BeatCombo = 0;
                }
            }
            if (onBeat) {
                mp.BeatCombo = Math.Min(BeatComboCap, mp.BeatCombo + 1);
                if (!VaultUtils.isServer) {
                    //踩拍层音：鞭响 tick 叠魔力泛音，音高随层数爬升
                    SoundEngine.PlaySound(SoundID.Item153 with {
                        Volume = 0.3f, Pitch = -0.15f + 0.1f * mp.BeatCombo
                    }, player.Center);
                    SoundEngine.PlaySound(SoundID.MaxMana with {
                        Volume = 0.22f, Pitch = 0.25f + 0.12f * mp.BeatCombo
                    }, player.Center);
                }
            }
            //登记本挥：itemAnimationMax 是攻速换算后的实际动画帧，窗口随之同缩
            int animFrames = Math.Max(1, player.itemAnimationMax);
            int baseAnim = Math.Max(1, item.useAnimation);
            mp.SwingEndTick = now + (uint)animFrames;
            mp.WindowFrames = Math.Clamp(
                (int)MathF.Round(animFrames * (float)BaseWindowFrames / baseAnim), 4, 40);
            mp.SwingHasHit = false;
            mp.SwingSettled = false;
            mp.SwingOnBeat = onBeat;
            mp.SwingHitCount = 0;
            mp.SwingHitNPCs.Clear();
            OnSwingStart(item, player, mp, onBeat);
            return null;
        }

        /// <summary>空挥结算（幂等）：挥击结束仍无命中即按政策罚</summary>
        private void SettleSwing(GsWhipPlayer mp, uint now) {
            if (mp.SwingSettled || mp.SwingEndTick == 0 || now < mp.SwingEndTick) {
                return;
            }
            mp.SwingSettled = true;
            if (mp.SwingHasHit) {
                return;
            }
            switch (MissPolicy) {
                case MissPolicyKind.Step:
                    mp.BeatCombo = Math.Max(0, mp.BeatCombo - 1);
                    break;
                case MissPolicyKind.Reset:
                    mp.BeatCombo = 0;
                    break;
            }
        }

        public sealed override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            GsWhipPlayer mp = player.GetModPlayer<GsWhipPlayer>();
            if (mp.WhipItemType != item.type || mp.SwingEndTick == 0) {
                return;
            }
            uint now = Main.GameUpdateCount;
            SettleSwing(mp, now);
            //窗口开启瞬间：鞭柄一记拍点微光（归属者的个人节奏读数）
            if (now == mp.SwingEndTick && !VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_GsWhipBeatSpark>(
                    player.MountedCenter + new Vector2(player.direction * 14f, -6f),
                    -Vector2.UnitY * 0.4f,
                    Color.Lerp(new Color(255, 232, 170), MarkColor, 0.35f), 0.72f);
            }
            //超窗：节拍归零并合窗
            if (now > mp.SwingEndTick + (uint)mp.WindowFrames) {
                mp.BeatCombo = 0;
                mp.SwingEndTick = 0;
            }
        }

        //==================== 打标与命中流转 ====================

        /// <summary>owner 端打标窗口：节拍快照随生成包过线，各端按同一层数渲染升温</summary>
        public sealed override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            if (proj.type == WhipProjType) {
                GsWhipPlayer mp = Main.player[proj.owner].GetModPlayer<GsWhipPlayer>();
                router.MarkData = mp.BeatCombo;
                router.MarkData2 = mp.SwingOnBeat ? 1f : 0f;
            }
            OnMarkedSpawn(proj, router);
        }

        public sealed override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (proj.type == WhipProjType && router.IsMarked) {
                WhipHitFlow(proj, target, router);
            }
            OnWhipProjHit(proj, target, hit, damageDone, router);
        }

        /// <summary>鞭本体命中流转（owner 端）：空挥记账 → 处决引爆 → 叠痕转印</summary>
        private void WhipHitFlow(Projectile proj, NPC target, GodSmithProjRouter router) {
            Player player = Main.player[proj.owner];
            GsWhipPlayer mp = player.GetModPlayer<GsWhipPlayer>();
            uint now = Main.GameUpdateCount;
            mp.SwingHasHit = true;
            if (mp.SwingHitNPCs.Add(target.whoAmI)) {
                mp.SwingHitCount++;
            }
            //假人放行便于试节奏；非战斗体不入标记
            bool markable = target.type == NPCID.TargetDummy
                || (!target.friendly && target.CanBeChasedBy(proj));
            if (!markable) {
                return;
            }
            WhipMarkState st = mp.GetOrCreateMark(target);
            if (st == null) {
                return;
            }
            st.SourceItemType = TargetItemID;
            bool onBeat = router.MarkData2 >= 1f;
            //引爆：命中前已挂印且本挥踩拍（满层那一击只转印，下一踩拍击才爆）
            if (st.ExecuteReady && onBeat && ExecuteByWhipHit) {
                DetonateSeal(player, target, proj, st);
                return;
            }
            int gain = MarkGainOnHit(onBeat);
            if (gain <= 0) {
                return;
            }
            st.LastHitTick = now;
            st.MarkDamage = proj.damage;
            if (st.ExecuteReady) {
                return;   //已挂印只续时，不再叠层
            }
            st.Stacks = Math.Min(MarkCap, st.Stacks + gain);
            if (st.Stacks >= MarkCap) {
                st.ExecuteReady = true;
                st.ExecuteReadyTick = now;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.35f }, target.Center);
                }
                OnSealLit(player, target, st);
            }
        }

        /// <summary>
        /// 处决印引爆的统一入口（owner 端）：清印、开 120f 鞭刑余韵、
        /// 个人确认反馈，然后交给子类演出。鞭击引爆与火鞭的原版爆炸引爆共用
        /// </summary>
        protected void DetonateSeal(Player player, NPC target, Projectile sourceProj, WhipMarkState st) {
            uint now = Main.GameUpdateCount;
            st.ExecuteReady = false;
            st.Stacks = 0;
            st.LastHitTick = now;
            st.AfterglowUntil = now + 120;
            if (!VaultUtils.isServer) {
                //个人确认 tick：主演出音由处决弹幕在各端播（命中钩子只在攻击方端跑）
                SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.45f, Pitch = -0.4f }, target.Center);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        Main.rand.NextVector2Circular(5f, 5f),
                        i % 2 == 0 ? new Color(255, 92, 46) : new Color(255, 206, 96),
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(12, 20));
                }
            }
            OnExecute(player, target, sourceProj, st);
        }

        //==================== 鞭体演出（升温拖尾 + 鞭梢顶点回调） ====================

        /// <summary>鞭弹幕的每弹幕本地包：鞭梢顶点回调的一次性闩锁</summary>
        private sealed class WhipProjLocal
        {
            public bool ApexFired;
        }

        public sealed override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type == WhipProjType && router.IsMarked) {
                HeatTrail(proj, router);
                ApexCheck(proj, router);
            }
            OnWhipProjPostAI(proj, router);
        }

        /// <summary>升温拖尾：节拍层数越高鞭梢火星越炽白（归属者本地视觉，预算每帧 1 粒）</summary>
        private void HeatTrail(Projectile proj, GodSmithProjRouter router) {
            int combo = (int)router.MarkData;
            if (combo <= 0 || VaultUtils.isServer || proj.owner != Main.myPlayer
                || Main.GameUpdateCount % 3 != 0) {
                return;
            }
            List<Vector2> pts = proj.GetWhipControlPoints();
            if (pts.Count < 6) {
                return;
            }
            //只在鞭梢后 1/3 段撒点，层数决定色温与个头
            int idx = pts.Count - 1 - Main.rand.Next(pts.Count / 3);
            Color heat = Color.Lerp(new Color(255, 170, 80), new Color(255, 244, 214), combo / 5f);
            heat = Color.Lerp(MarkColor, heat, 0.55f);
            PRTLoader.NewParticle<PRT_Spark>(pts[idx],
                Main.rand.NextVector2Circular(0.6f, 0.6f), heat,
                0.26f + 0.05f * combo)?.Configure(false, Main.rand.Next(8, 13));
        }

        /// <summary>鞭梢最远点一次性回调（owner 端）：杜兰达尔剑气/晨星蓄势震荡的出手点</summary>
        private void ApexCheck(Projectile proj, GodSmithProjRouter router) {
            if (proj.owner != Main.myPlayer) {
                return;
            }
            WhipProjLocal local = router.GetOrCreateState<WhipProjLocal>();
            if (local.ApexFired) {
                return;
            }
            Projectile.GetWhipSettings(proj, out float timeToFlyOut, out _, out _);
            if (proj.ai[0] < timeToFlyOut * 0.5f) {
                return;
            }
            local.ApexFired = true;
            List<Vector2> pts = proj.GetWhipControlPoints();
            if (pts.Count > 0) {
                OnWhipApex(Main.player[proj.owner], proj, router, pts[^1]);
            }
        }

        //==================== 子类扩展点 ====================

        /// <summary>挥击登记完成时（owner 端）</summary>
        protected virtual void OnSwingStart(Item item, Player player, GsWhipPlayer mp, bool onBeat) { }

        /// <summary>打标窗口内的追加处理（owner 端，MarkData 已写好）</summary>
        protected virtual void OnMarkedSpawn(Projectile proj, GodSmithProjRouter router) { }

        /// <summary>本次命中叠多少层鞭痕；火鞭改为只有踩拍命中叠引信</summary>
        protected virtual int MarkGainOnHit(bool onBeat) => 1;

        /// <summary>鞭痕满层转处决印瞬间（owner 端）</summary>
        protected virtual void OnSealLit(Player player, NPC target, WhipMarkState st) { }

        /// <summary>处决引爆（owner 端）：生成真弹幕演出，伤害基数用 st.MarkDamage</summary>
        protected abstract void OnExecute(Player player, NPC target, Projectile whipProj, WhipMarkState st);

        /// <summary>鞭梢最远点（owner 端一次性）：踩拍专属出手挂这里</summary>
        protected virtual void OnWhipApex(Player player, Projectile whipProj, GodSmithProjRouter router, Vector2 tipPos) { }

        /// <summary>鞭弹幕与类型通道弹幕的命中扩展（owner 端）</summary>
        protected virtual void OnWhipProjHit(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) { }

        /// <summary>鞭弹幕与类型通道弹幕的 PostAI 扩展（各端）</summary>
        protected virtual void OnWhipProjPostAI(Projectile proj, GodSmithProjRouter router) { }
    }
}
