using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 魔法·前困难弹体族共享框架：施法节拍 / 咏唱增幅 / 连击共鸣。<br/>
    /// 核心循环：每次施法后，武器就绪时刻起开一段节拍窗（按 useTime 与攻速折算）；
    /// 窗内完成下一次施法 = 正拍，共鸣 +1 层并按比例返蓝；错过窗口按掉层节奏逐层衰减。
    /// 满层武装强化咏唱：下一次施法蓝耗翻倍、打出签名招并清层。<br/>
    /// 联机纪律：节拍/共鸣是本地玩家态（<see cref="GsChantPlayer"/>，myPlayer 守门），
    /// 层数只影响 owner 端伤害烘焙；弹幕形态经 MarkData 随生成包过线，各端一致渲染。<br/>
    /// MarkData 约定：0/1/2 = 平拍/正拍/强化原生弹（基类自动写入），≥10 = 方案私有形态码
    /// （生成前设 <see cref="GsChantPlayer.PendingForm"/>，打标窗口自动消费）；
    /// MarkData2 默认为施法瞬间层数，私有形态下语义自定
    /// </summary>
    internal abstract class GsChantScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "MagicChant";

        //==================== 节拍参数（子类按计划梯度覆写） ====================

        /// <summary>就绪后节拍窗时长（帧）</summary>
        protected virtual int WindowSpanTicks => 22;

        /// <summary>共鸣层上限</summary>
        protected virtual int MaxResonance => 5;

        /// <summary>每层伤害乘区</summary>
        protected virtual float ResonanceDamagePerStack => 0.04f;

        /// <summary>正拍施法返蓝比例（0 = 不返蓝，太空枪族的免蓝语义不动）</summary>
        protected virtual float OnBeatManaRefund => 0.30f;

        /// <summary>失拍是否直接清层（false = 按掉层节奏逐层衰减）</summary>
        protected virtual bool MissResets => false;

        /// <summary>失拍是否会掉层（太空枪「停滞不清」覆写 false）</summary>
        protected virtual bool DecayEnabled => true;

        /// <summary>满层强化咏唱的蓝耗倍率</summary>
        protected virtual float EmpowerManaMult => 2f;

        /// <summary>强化咏唱后清空全部层数</summary>
        protected virtual bool EmpowerConsumesAll => true;

        /// <summary>满层瞬间直接以该发为强化咏唱（太空枪充能语义），false = 武装到下一发</summary>
        protected virtual bool EmpowerTriggersInstantly => false;

        /// <summary>走标准节拍窗结算；碧水权杖等持续流变体覆写 false 自管层数</summary>
        protected virtual bool UsesStandardBeat => true;

        /// <summary>基础伤害乘区（强度定价，残酷 +50% 敌强下允许原版 100%~135%）</summary>
        protected virtual float BaseDamageMult => 1f;

        /// <summary>节拍读数与出手演出的主题色（各武器材质身份色板）</summary>
        protected virtual Color ChantColor => new(255, 214, 120);

        //==================== 形态码基线 ====================

        /// <summary>原生弹形态：平拍</summary>
        internal const float FormStraight = 0f;
        /// <summary>原生弹形态：正拍</summary>
        internal const float FormOnBeat = 1f;
        /// <summary>原生弹形态：强化咏唱</summary>
        internal const float FormEmpower = 2f;

        //==================== 状态入口 ====================

        /// <summary>取玩家的节拍状态</summary>
        protected static GsChantPlayer Chant(Player player) => player.GetModPlayer<GsChantPlayer>();

        /// <summary>本弹是否正拍或更高形态的原生弹</summary>
        protected static bool IsOnBeatProj(GodSmithProjRouter router)
            => router.MarkData == FormOnBeat || router.MarkData == FormEmpower;

        //==================== 节拍结算（密封，子类走 Chant* 扩展点） ====================

        public sealed override void GsModifyShootStats(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            GsChantPlayer chant = Chant(player);
            chant.EnsureBound(item.type);
            if (UsesStandardBeat) {
                ResolveBeat(chant, item, player);
            }
            ChantModifyShootStats(item, player, chant, ref position, ref velocity, ref type, ref damage, ref knockback);
        }

        /// <summary>节拍结算：判拍型、加/清层、返蓝、开下一窗。只在 owner 端的射击链内执行</summary>
        private void ResolveBeat(GsChantPlayer chant, Item item, Player player) {
            uint now = Main.GameUpdateCount;
            bool onBeat = chant.WindowCloseAt > 0 && now <= chant.WindowCloseAt;
            chant.ResonanceAtCast = chant.Resonance;

            if (chant.EmpowerArmed) {
                //武装态的这一发即强化咏唱（蓝耗倍率已在本次 PayMana 时生效）
                chant.CurrentBeat = ChantBeat.Empower;
                chant.EmpowerArmed = false;
                if (EmpowerConsumesAll) {
                    chant.Resonance = 0;
                }
            }
            else if (onBeat) {
                chant.CurrentBeat = ChantBeat.OnBeat;
                if (chant.Resonance < MaxResonance) {
                    chant.Resonance++;
                }
                chant.ResonanceAtCast = chant.Resonance;
                if (chant.Resonance >= MaxResonance) {
                    if (EmpowerTriggersInstantly) {
                        //充能语义：攒满的这一发直接升格
                        chant.CurrentBeat = ChantBeat.Empower;
                        if (EmpowerConsumesAll) {
                            chant.Resonance = 0;
                        }
                    }
                    else {
                        chant.EmpowerArmed = true;
                        SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.85f, Pitch = 0.2f }, player.Center);
                    }
                }
                //正拍返蓝：返还本次实际扣除的一部分（PayMana 先于射击链，基数已就绪）
                int refund = (int)(chant.LastManaConsumed * OnBeatManaRefund);
                if (refund > 0) {
                    player.statMana = Math.Min(player.statMana + refund, player.statManaMax2);
                    player.ManaEffect(refund);
                }
                //正拍确认音随层数升调，读数不看 UI 听杖尖
                SoundEngine.PlaySound(SoundID.Item4 with {
                    Volume = 0.35f, Pitch = 0.15f + 0.08f * chant.Resonance, MaxInstances = 3
                }, player.Center);
            }
            else {
                chant.CurrentBeat = ChantBeat.Straight;
                if (MissResets) {
                    chant.Resonance = 0;
                }
            }

            //按实际用时折算下一个节拍窗（攻速词条改变鼓点间距）
            float speed = Math.Max(0.1f, player.GetWeaponAttackSpeed(item));
            int useTicks = Math.Max(1, (int)(item.useTime / speed));
            chant.WindowOpenAt = now + (uint)useTicks;
            chant.WindowCloseAt = chant.WindowOpenAt + (uint)WindowSpanTicks;
            chant.DecayPeriod = useTicks + WindowSpanTicks;
            chant.NextDecayAt = chant.WindowCloseAt + (uint)chant.DecayPeriod;
        }

        public sealed override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            GsChantPlayer chant = Chant(player);
            SpawnCastMuzzle(player, position, velocity, chant);
            if (UsesStandardBeat && chant.CurrentBeat == ChantBeat.Empower) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = -0.1f }, position);
                return ChantEmpowerShoot(item, player, chant, source, position, velocity, type, damage, knockback);
            }
            return ChantShoot(item, player, chant, source, position, velocity, type, damage, knockback);
        }

        /// <summary>出手相：杖尖迸出本色法花（owner 端调用，粒子天然只在客户端）</summary>
        protected virtual void SpawnCastMuzzle(Player player, Vector2 position, Vector2 velocity, GsChantPlayer chant) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);
            int count = chant.CurrentBeat == ChantBeat.Empower ? 5 : 3;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Spark>(position + dir * 8f,
                    dir.RotatedByRandom(0.5) * Main.rand.NextFloat(1.5f, 4f),
                    ChantColor, Main.rand.NextFloat(0.2f, 0.35f))?.Configure(false, Main.rand.Next(8, 14));
            }
            if (chant.CurrentBeat != ChantBeat.Straight) {
                PRTLoader.NewParticle<PRT_Light>(position, dir * 1.5f, ChantColor, 0.14f)?.Configure(10, 0.8f);
            }
        }

        //==================== 手持：衰减与读数（密封，子类走 ChantHoldItem） ====================

        public sealed override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI == Main.myPlayer) {
                GsChantPlayer chant = Chant(player);
                chant.EnsureBound(item.type);
                if (UsesStandardBeat) {
                    TickDecay(chant);
                }
                DrawChantReadout(chant, item, player);
            }
            ChantHoldItem(item, player);
        }

        /// <summary>失拍衰减：窗口关闭后按掉层节奏逐层扣，离场过久直接清空</summary>
        private void TickDecay(GsChantPlayer chant) {
            if (!DecayEnabled || chant.Resonance <= 0 || chant.WindowCloseAt == 0) {
                return;
            }
            uint now = Main.GameUpdateCount;
            if (now <= chant.WindowCloseAt) {
                return;
            }
            if (now - chant.WindowCloseAt > 3600) {
                //切走武器超一分钟回来：鼓点早散了
                chant.Resonance = 0;
                chant.EmpowerArmed = false;
                return;
            }
            int period = Math.Max(20, chant.DecayPeriod);
            while (chant.Resonance > 0 && now >= chant.NextDecayAt) {
                chant.Resonance--;
                chant.NextDecayAt += (uint)period;
            }
            if (chant.Resonance <= 0) {
                chant.EmpowerArmed = false;
            }
        }

        /// <summary>杖尖读数：窗开一闪 + 环绕光点记层 + 武装金辉。全部只有本人可见（myPlayer 路径）</summary>
        private void DrawChantReadout(GsChantPlayer chant, Item item, Player player) {
            if (VaultUtils.isServer || !UsesStandardBeat) {
                return;
            }
            uint now = Main.GameUpdateCount;
            Vector2 tip = player.MountedCenter + GsAimUnit(player) * 26f;
            //节拍窗开启瞬间：杖尖一圈收缩环，这就是鼓点
            if (now == chant.WindowOpenAt && chant.WindowCloseAt > 0) {
                PRTLoader.NewParticle<PRT_ProcRing>(tip, Vector2.Zero, ChantColor, 1f)
                    ?.Configure(18f, 5f, 10);
            }
            //环绕光点记层：低频补充，在场数量与层数同阶
            if (chant.Resonance > 0 && now % 9 == 0) {
                for (int i = 0; i < chant.Resonance; i++) {
                    float ang = MathHelper.TwoPi * i / MaxResonance + now * 0.045f;
                    Vector2 orbit = player.MountedCenter + ang.ToRotationVector2() * 24f;
                    PRTLoader.NewParticle<PRT_Light>(orbit, player.velocity * 0.4f,
                        ChantColor, 0.07f)?.Configure(8, 0.55f, 0f, 1.2f, 0f, player);
                }
            }
            //武装态：手部金辉呼吸
            if (chant.EmpowerArmed && now % 6 == 0) {
                PRTLoader.NewParticle<PRT_Light>(tip + Main.rand.NextVector2Circular(4f, 4f),
                    -Vector2.UnitY * 0.5f, new Color(255, 226, 142), 0.1f)?.Configure(12, 0.7f);
            }
        }

        //==================== 数值面（密封转发） ====================

        public sealed override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            damage *= BaseDamageMult;
            GsChantPlayer chant = Chant(player);
            if (chant.BoundItemType == item.type && chant.Resonance > 0) {
                //层数乘区只在 owner 端有真实层数，弹幕伤害在 owner 端烘焙后随生成包过线
                damage *= 1f + chant.Resonance * ResonanceDamagePerStack;
            }
            ChantModifyWeaponDamage(item, player, chant, ref damage);
        }

        public sealed override void GsModifyManaCost(Item item, Player player, ref float reduce, ref float mult) {
            GsChantPlayer chant = Chant(player);
            if (UsesStandardBeat && chant.BoundItemType == item.type && chant.EmpowerArmed) {
                mult *= EmpowerManaMult;
            }
            ChantModifyManaCost(item, player, chant, ref reduce, ref mult);
        }

        public sealed override void GsOnConsumeMana(Item item, Player player, int manaConsumed) {
            if (player.whoAmI == Main.myPlayer) {
                Chant(player).LastManaConsumed = manaConsumed;
            }
        }

        //==================== 弹幕打标（密封：PendingForm 优先，否则写拍型与层数） ====================

        public sealed override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            GsChantPlayer chant = Chant(Main.player[proj.owner]);
            if (proj.owner == Main.myPlayer && chant.PendingForm != 0f) {
                router.MarkData = chant.PendingForm;
                router.MarkData2 = chant.PendingParam;
                chant.PendingForm = 0f;
                chant.PendingParam = 0f;
                MakeMechanicProj(proj);
            }
            else {
                router.MarkData = (float)chant.CurrentBeat;
                router.MarkData2 = chant.ResonanceAtCast;
            }
            ChantProjOnSpawnMarked(proj, router, chant);
        }

        public sealed override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            //二级弹幕承签：默认继承父标；生成前挂了 PendingForm 则改写为私有形态
            if (proj.owner == Main.myPlayer) {
                GsChantPlayer chant = Chant(Main.player[proj.owner]);
                if (chant.PendingForm != 0f) {
                    router.MarkData = chant.PendingForm;
                    router.MarkData2 = chant.PendingParam;
                    chant.PendingForm = 0f;
                    chant.PendingParam = 0f;
                    MakeMechanicProj(proj);
                }
            }
            ChantProjOnSpawnInherited(proj, router, parent, parentRouter);
        }

        /// <summary>
        /// 机制弹通用免疫改制：本体命中的原版全局免疫帧会把几帧后赶到的碎晶/链跳弹噎死，
        /// 挂了私有形态的弹一律改为每弹每目标一次的 local 免疫（判定只在 owner 端，改动无需过线）。
        /// 需要多跳的驻场形态在 ChantProjOnSpawn* 回调里自行改写 localNPCHitCooldown
        /// </summary>
        private static void MakeMechanicProj(Projectile proj) {
            proj.usesLocalNPCImmunity = true;
            proj.localNPCHitCooldown = -1;
        }

        /// <summary>为即将生成的一枚弹幕挂私有形态标（生成前调用，打标窗口自动消费）</summary>
        protected static void QueueForm(Player player, float form, float param = 0f) {
            GsChantPlayer chant = Chant(player);
            chant.PendingForm = form;
            chant.PendingParam = param;
        }

        //==================== 子类扩展点 ====================

        /// <summary>射击参数修改（节拍已结算，chant.CurrentBeat 可用）</summary>
        protected virtual void ChantModifyShootStats(Item item, Player player, GsChantPlayer chant,
            ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) { }

        /// <summary>常规/正拍施法。返回 null 走原版弹幕（自动打标），false = 已自行生成</summary>
        protected virtual bool? ChantShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) => null;

        /// <summary>强化咏唱（满层签名招）。默认退化为常规施法</summary>
        protected virtual bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback)
            => ChantShoot(item, player, chant, source, position, velocity, type, damage, knockback);

        /// <summary>追加的手持逻辑（衰减与读数之外）</summary>
        protected virtual void ChantHoldItem(Item item, Player player) { }

        /// <summary>追加的伤害修饰（基础乘区与层数乘区之后）</summary>
        protected virtual void ChantModifyWeaponDamage(Item item, Player player, GsChantPlayer chant,
            ref StatModifier damage) { }

        /// <summary>追加的魔耗修饰（强化倍率之后）</summary>
        protected virtual void ChantModifyManaCost(Item item, Player player, GsChantPlayer chant,
            ref float reduce, ref float mult) { }

        /// <summary>原生弹打标后回调（owner 端，MarkData 已写好，可改弹幕出生态）</summary>
        protected virtual void ChantProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router, GsChantPlayer chant) { }

        /// <summary>二级弹幕承签后回调（生成端）</summary>
        protected virtual void ChantProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) { }

        //==================== 通用小工具 ====================

        /// <summary>找离某点最近的可打敌怪；excludeWhoAmI 排除当前目标（链跳选新目标用）</summary>
        protected static NPC FindNearestEnemy(Vector2 center, float maxRange, int excludeWhoAmI = -1) {
            NPC best = null;
            float bestDist = maxRange * maxRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.whoAmI == excludeWhoAmI) {
                    continue;
                }
                float d = Vector2.DistanceSquared(npc.Center, center);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>把速度向目标方向按最大角速度缓转（各端确定性，供 PostAI 追踪用）</summary>
        protected static void SteerTowards(Projectile proj, Vector2 targetPos, float maxTurnRad) {
            float speed = proj.velocity.Length();
            if (speed < 0.5f) {
                return;
            }
            float current = proj.velocity.ToRotation();
            float wanted = (targetPos - proj.Center).ToRotation();
            float turned = Utils.AngleTowards(current, wanted, maxTurnRad);
            proj.velocity = turned.ToRotationVector2() * speed;
        }
    }
}
