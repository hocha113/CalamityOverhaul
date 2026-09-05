using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    //================================================================
    // 木剑组分片：8 把木质阔剑，主题锚「生木回弹」。
    // 每把独立拍表与签名机制，支援弹幕在文件尾部
    //================================================================

    #region 木剑（回弹连势）

    /// <summary>
    /// 【木剑】材质：鲜切松木。签名：①回弹连势，命中后 40 帧内的下一斩举刀减半且伤害 +10%，
    /// 生木弹性越打越顺 ②第三拍前压重劈
    /// </summary>
    internal class GsWoodenSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.WoodenSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsWoodenSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: green wood springs back; landing a hit primes the blade, so the next slash within a breath raises twice as fast and deals 10% more damage";
        internal static readonly Color SapBright = new(232, 214, 166);  //新木亮黄
        internal static readonly Color PineMain = new(176, 138, 90);    //松木体
        internal static readonly Color SproutHot = new(150, 220, 110);  //嫩芽绿

        /// <summary>回弹窗口倒计时（命中后 40 帧内下一斩吃增益）；单例静态，只在 myPlayer 路径读写</summary>
        internal static int ReboundTimer;

        //底伤 +16%：终结拍 1.25x（拍均 ~1.08）+ 回弹增益 10% 部分覆盖率，
        //综合 DPS 约为原版 122%~130%（木剑公认弱势，允许至 130%）
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.16f;

        /// <summary>照抄基类实现，NewProjectile 追加 ai[2]=回弹标记（消费后清零）</summary>
        public override bool? GsCanUseItem(Item item, Player player) {
            if (player.ownedProjectileCounts[HeldProjID] > 0) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                int beat = comboCounter % ComboBeats;
                float swingSign = comboCounter % 2 == 0 ? 1f : -1f;
                ModifyLocalSwing(item, player, ref beat, ref swingSign);
                comboCounter++;
                comboResetTimer = ComboResetFrames;
                float rebound = ReboundTimer > 0 ? 1f : 0f;
                ReboundTimer = 0;
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    HeldProjID, player.GetWeaponDamage(item), item.knockBack, player.whoAmI, beat, swingSign, rebound);
            }
            return false;
        }

        public override void GsHoldItem(Item item, Player player) {
            base.GsHoldItem(item, player);
            if (player.whoAmI == Main.myPlayer && ReboundTimer > 0) {
                ReboundTimer--;
            }
        }
    }

    /// <summary>
    /// 木剑手持：三拍轻快劈砍，ai[2]=1 时为回弹斩（举相减半 + 伤害 +10%）
    /// </summary>
    internal class GsWoodenSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.WoodenSword;
        protected override Color EdgeBright => GsWoodenSword.SapBright;
        protected override Color BodyMain => GsWoodenSword.PineMain;
        protected override Color HotAccent => GsWoodenSword.SproutHot;

        /// <summary>本斩是否吃到回弹增益（ai[2] 随生成包过线，各端一致）</summary>
        private bool Rebounding => Projectile.ai[2] >= 1f;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //前压重劈终结
                return new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 4, Recover = 9,
                    RaiseBack = 2.0f, Follow = 1.15f, ReachScale = 1.12f, LeanAmp = 0.06f,
                    DamageMult = 1.25f, Hitstop = 1, LungeSpeed = 2.4f, SwingPitch = -0.10f,
                };
            }
            //轻快交替斩：短举短收、音高偏亮
            return new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.7f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = stage == 0 ? 0.12f : 0.05f,
            };
        }

        protected override void OnStageInit() {
            base.OnStageInit();
            if (Rebounding) {
                //生木回弹：举刀相减半，伤害 +10%
                raiseDur = Math.Max(1, raiseDur / 2);
                totalDur = raiseDur + holdDur + slashDur + recoverDur;
                Projectile.damage = (int)(Projectile.damage * 1.10f);
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            //owner 守门写方案侧回弹窗口（myPlayer 消费）
            if (Owner.whoAmI == Main.myPlayer) {
                GsWoodenSword.ReboundTimer = 40;
            }
        }
    }

    #endregion

    #region 北极松木剑（霜脂）

    /// <summary>
    /// 【北极松木剑】材质：北地寒杉。签名：①霜脂，连续命中同一目标叠寒杉脂，
    /// 第 3 层点上霜火 ②长滞帧的冻凝节奏，蓄而后发
    /// </summary>
    internal class GsBorealWoodSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BorealWoodSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsBorealWoodSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: repeated hits coat the same target in boreal sap; the third layer bursts into frost mist and sets the target ablaze with frostburn";
        internal static readonly Color FrostBright = new(220, 240, 250); //霜白
        internal static readonly Color ColdMain = new(130, 170, 200);    //寒杉青蓝
        internal static readonly Color IceHot = new(150, 230, 255);      //冰芯亮青

        /// <summary>寒杉脂层数表：NPC → (层数, 最后命中帧)；单例静态，只在 myPlayer 路径读写</summary>
        internal static readonly Dictionary<int, (int stacks, uint time)> SapStacks = [];

        //底伤 +6%：终结拍 1.3x（拍均 ~1.10）+ 霜火 DoT，综合 DPS 约为原版 115%~120%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;
    }

    /// <summary>
    /// 北极松木剑手持：三拍冻凝斩，滞帧显著拉长（藏行程露停顿），
    /// 命中在方案侧记寒杉脂层数，第 3 层上霜火
    /// </summary>
    internal class GsBorealWoodSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BorealWoodSword;
        protected override Color EdgeBright => GsBorealWoodSword.FrostBright;
        protected override Color BodyMain => GsBorealWoodSword.ColdMain;
        protected override Color HotAccent => GsBorealWoodSword.IceHot;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //冻凝终结：滞帧最长，蓄满一口寒气再劈
                return new GsBroadBeat {
                    Raise = 7, Hold = 4, Slash = 4, Recover = 10,
                    RaiseBack = 2.1f, Follow = 1.1f, ReachScale = 1.15f, LeanAmp = 0.07f,
                    DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 2.6f, SwingPitch = -0.26f,
                };
            }
            //冻凝普通拍：长滞帧是本剑的节奏身份
            return new GsBroadBeat {
                Raise = 6, Hold = 3, Slash = 3, Recover = 8,
                RaiseBack = 1.9f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = stage == 0 ? -0.05f : -0.14f,
            };
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            //寒杉脂层数只在 owner 端记（myPlayer 消费）；上到第 3 层挂霜火
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            var dict = GsBorealWoodSword.SapStacks;
            if (dict.Count > 200) {
                dict.Clear();
            }
            uint now = Main.GameUpdateCount;
            if (!dict.TryGetValue(target.whoAmI, out var entry) || now - entry.time > 300) {
                entry = (0, now);
            }
            int stacks = entry.stacks + 1;
            if (stacks >= 3) {
                dict.Remove(target.whoAmI);
                target.AddBuff(BuffID.Frostburn, 240);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.6f, Pitch = 0.15f }, target.Center);
                }
            }
            else {
                dict[target.whoAmI] = (stacks, now);
            }
        }
    }

    #endregion

    #region 棕榈木剑（拍岸沙浪）

    /// <summary>
    /// 【棕榈木剑】材质：海滩棕榈木。签名：①拍岸沙浪，终结拍斩切爆发沿挥向甩出
    /// 3 团扇形沙浪弹幕，快速坠地命中一跳 ②长斩切相的宽弧横扫，像浪拍岸
    /// </summary>
    internal class GsPalmWoodSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.PalmWoodSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsPalmWoodSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: sweeping shore arcs; the third slash hurls a fan of three sand waves that quickly crash to the ground, each hitting once";
        internal static readonly Color SandBright = new(245, 225, 170); //浅滩沙白
        internal static readonly Color PalmMain = new(210, 170, 105);   //棕榈木黄
        internal static readonly Color SunHot = new(255, 205, 120);     //日照沙金

        //底伤 +4%：终结拍 1.25x（拍均 ~1.08）+ 每 3 斩 3 团 35% 沙浪（摊 ~+8%），
        //综合 DPS 约为原版 116%~120%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.04f;
    }

    /// <summary>
    /// 棕榈木剑手持：三拍宽弧横扫，斩切相拉长（浪势绵长），
    /// 终结拍爆发首帧甩出 3 团扇形沙浪
    /// </summary>
    internal class GsPalmWoodSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.PalmWoodSword;
        protected override Color EdgeBright => GsPalmWoodSword.SandBright;
        protected override Color BodyMain => GsPalmWoodSword.PalmMain;
        protected override Color HotAccent => GsPalmWoodSword.SunHot;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //拍岸终结：最宽的弧、最长的浪
                return new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 6, Recover = 10,
                    RaiseBack = 2.3f, Follow = 1.5f, ReachScale = 1.2f, LeanAmp = 0.07f,
                    DamageMult = 1.25f, Hitstop = 1, LungeSpeed = 2.8f, SwingPitch = 0f,
                };
            }
            //宽弧横扫：斩切相长，浪势绵长
            return new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 5, Recover = 8,
                RaiseBack = 2.1f, Follow = 1.3f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = stage == 0 ? 0.18f : 0.10f,
            };
        }

        protected override void OnSlashBegin() {
            if (!IsFinisher) {
                return;
            }
            //拍岸沙浪：沿瞄准向扇形甩 3 团沙（SpawnOwnedProj 守 owner）
            int type = ModContent.ProjectileType<GsPalmSandWaveProj>();
            int dmg = Math.Max(1, (int)(Projectile.damage * 0.35f));
            for (int i = -1; i <= 1; i++) {
                Vector2 vel = (baseAngle + i * 0.30f).ToRotationVector2() * (8f - MathF.Abs(i) * 1.2f);
                SpawnOwnedProj(type, Hand + baseAngle.ToRotationVector2() * 30f, vel, dmg, 2f);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.5f, Pitch = 0.2f }, Owner.Center);
            }
        }
    }

    #endregion

    #region 红木剑（藤蔓延势）

    /// <summary>
    /// 【红木剑】材质：丛林红木。签名：①藤蔓延势，四拍藤鞭连击，终结拍触及 1.3 倍
    /// ②终结拍命中毒藤上毒 ③音高逐拍下行的鞭打节奏
    /// </summary>
    internal class GsRichMahoganySword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.RichMahoganySword;

        protected override int HeldProjID => ModContent.ProjectileType<GsRichMahoganySwordHeld>();

        protected override int ComboBeats => 4;

        protected override string GsDescFallback =>
            "Reforged: a four-beat vine-whip combo; the fourth slash extends a phantom vine for far greater reach and poisons whatever it entangles";
        internal static readonly Color LeafBright = new(205, 235, 150); //嫩叶浅绿
        internal static readonly Color MahoganyMain = new(150, 95, 65); //红木棕红
        internal static readonly Color VineHot = new(110, 205, 95);     //藤蔓浓绿

        //底伤 +8%：终结拍 1.22x（四拍拍均 ~1.06）+ 毒藤 DOT 小额收益，
        //综合 DPS 约为原版 112%~116%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 红木剑手持：四拍藤鞭连击（族内唯一四拍），前三拍快鞭、
    /// 终结拍触及 1.3 倍，命中毒藤上毒
    /// </summary>
    internal class GsRichMahoganySwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.RichMahoganySword;
        protected override Color EdgeBright => GsRichMahoganySword.LeafBright;
        protected override Color BodyMain => GsRichMahoganySword.MahoganyMain;
        protected override Color HotAccent => GsRichMahoganySword.VineHot;

        protected override int BeatCount => 4;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 3) {
                //藤蔓延势终结：触及 1.3 倍，跟进最深
                return new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 5, Recover = 10,
                    RaiseBack = 2.2f, Follow = 1.4f, ReachScale = 1.3f, LeanAmp = 0.075f,
                    DamageMult = 1.22f, Hitstop = 2, LungeSpeed = 2.6f, SwingPitch = -0.15f,
                };
            }
            //快鞭三连：短举短收、跟进偏深（鞭势），音高逐拍下行
            return new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 4, Recover = 6,
                RaiseBack = 1.9f, Follow = 1.2f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = 0.15f - stage * 0.07f,
            };
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            //毒藤：终结拍命中上毒（Slow 对 NPC 无效，Poisoned 是真实生效的丛林路径）
            if (IsFinisher) {
                target.AddBuff(BuffID.Poisoned, 90);
            }
        }
    }

    #endregion

    #region 乌木剑（蚀木孢雾）

    /// <summary>
    /// 【乌木剑】材质：腐化乌木。签名：①蚀木孢雾，每拍斩切后在刀路中点留一团
    /// 30 帧驻留圆团孢雾，命中一跳低伤（与暗影蚀刃的弧形蚀痕区分：圆团不是弧痕）
    /// ②长收势的阴郁拖拍节奏
    /// </summary>
    internal class GsEbonwoodSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.EbonwoodSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsEbonwoodSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash leaves a lingering puff of corrupt spores at the midpoint of its path, dealing one tick of light damage to anything caught inside";
        internal static readonly Color PaleBright = new(190, 160, 215); //苍紫灰
        internal static readonly Color EbonMain = new(112, 92, 132);    //乌木紫灰
        internal static readonly Color SporeHot = new(150, 90, 200);    //孢子亮紫

        //底伤 +3%：终结拍 1.25x（拍均 ~1.08）+ 每拍 15% 孢雾一跳（有效覆盖摊 ~+8%），
        //综合 DPS 约为原版 116%~122%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.03f;
    }

    /// <summary>
    /// 乌木剑手持：三拍阴郁拖斩（收势最长，孢雾有时间弥散），
    /// 每拍收势首帧在刀路中点留一团孢雾
    /// </summary>
    internal class GsEbonwoodSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.EbonwoodSword;
        protected override Color EdgeBright => GsEbonwoodSword.PaleBright;
        protected override Color BodyMain => GsEbonwoodSword.EbonMain;
        protected override Color HotAccent => GsEbonwoodSword.SporeHot;

        private bool mistSpawned;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                return new GsBroadBeat {
                    Raise = 7, Hold = 2, Slash = 5, Recover = 12,
                    RaiseBack = 2.1f, Follow = 1.2f, ReachScale = 1.1f, LeanAmp = 0.065f,
                    DamageMult = 1.25f, Hitstop = 2, LungeSpeed = 2.2f, SwingPitch = -0.35f,
                };
            }
            //阴郁拖拍：收势拉长（木组里最沉的尾音），音高压低
            return new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 11,
                RaiseBack = 1.85f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = stage == 0 ? -0.18f : -0.24f,
            };
        }

        protected override void HandlePhaseEvents(int phase) {
            base.HandlePhaseEvents(phase);
            //收势首帧在刀路中点留孢雾（每拍都留；SpawnOwnedProj 守 owner）
            if (!mistSpawned && phase == PhaseRecover) {
                mistSpawned = true;
                float midAng = MathHelper.Lerp(ArcStart, ArcEnd, 0.5f);
                Vector2 at = Hand + midAng.ToRotationVector2() * (FullReach * 0.66f);
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.15f));
                SpawnOwnedProj(ModContent.ProjectileType<GsEbonSporeMistProj>(), at, Vector2.Zero, dmg, 0f);
            }
        }
    }

    #endregion

    #region 暗影木剑（嗜血纹）

    /// <summary>
    /// 【暗影木剑】材质：猩红暗影木。签名：①嗜血纹，对流血目标伤害 +18% 且触发回弹
    /// （下一拍举刀 -30%）②终结拍命中施加流血，自铺嗜血循环
    /// </summary>
    internal class GsShadewoodSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.ShadewoodSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsShadewoodSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: the third strike opens a bleeding wound; hits against bleeding targets deal 18% more damage and quicken your next slash";
        internal static readonly Color FleshBright = new(230, 130, 115); //血肉浅红
        internal static readonly Color ShadeMain = new(145, 58, 58);     //暗影木赤褐
        internal static readonly Color BloodHot = new(255, 64, 72);      //鲜血亮红

        /// <summary>嗜血回弹窗口倒计时；单例静态，只在 myPlayer 路径读写</summary>
        internal static int BloodRush;

        //底伤 +3%：终结拍 1.3x（拍均 ~1.10）+ 对流血目标 +18%（条件覆盖率），
        //综合 DPS 约为原版 113%（未流血）~133%（流血高覆盖，弱势武器允许至 135%）
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.03f;

        /// <summary>照抄基类实现，NewProjectile 追加 ai[2]=嗜血回弹标记（消费后清零）</summary>
        public override bool? GsCanUseItem(Item item, Player player) {
            if (player.ownedProjectileCounts[HeldProjID] > 0) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                int beat = comboCounter % ComboBeats;
                float swingSign = comboCounter % 2 == 0 ? 1f : -1f;
                ModifyLocalSwing(item, player, ref beat, ref swingSign);
                comboCounter++;
                comboResetTimer = ComboResetFrames;
                float rush = BloodRush > 0 ? 1f : 0f;
                BloodRush = 0;
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    HeldProjID, player.GetWeaponDamage(item), item.knockBack, player.whoAmI, beat, swingSign, rush);
            }
            return false;
        }

        public override void GsHoldItem(Item item, Player player) {
            base.GsHoldItem(item, player);
            if (player.whoAmI == Main.myPlayer && BloodRush > 0) {
                BloodRush--;
            }
        }
    }

    /// <summary>
    /// 暗影木剑手持：三拍凶斩。对流血目标 +18% 伤害；命中流血目标开嗜血回弹窗口
    /// （ai[2]=1 时举刀 -30%）；终结拍命中施加流血
    /// </summary>
    internal class GsShadewoodSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.ShadewoodSword;
        protected override Color EdgeBright => GsShadewoodSword.FleshBright;
        protected override Color BodyMain => GsShadewoodSword.ShadeMain;
        protected override Color HotAccent => GsShadewoodSword.BloodHot;

        /// <summary>本斩是否吃到嗜血回弹（ai[2] 随生成包过线）</summary>
        private bool BloodRushing => Projectile.ai[2] >= 1f;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //开创口的终结：前压最猛
                return new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                    RaiseBack = 2.0f, Follow = 1.15f, ReachScale = 1.1f, LeanAmp = 0.07f,
                    DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 3.0f, SwingPitch = -0.20f,
                };
            }
            //凶斩：短滞快收，杀气外露
            return new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.8f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = stage == 0 ? -0.02f : -0.10f,
            };
        }

        protected override void OnStageInit() {
            base.OnStageInit();
            if (BloodRushing) {
                //嗜血回弹：举刀 -30%
                raiseDur = Math.Max(1, (int)(raiseDur * 0.7f));
                totalDur = raiseDur + holdDur + slashDur + recoverDur;
            }
        }

        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            //嗜血纹：对流血目标 +18%
            if (target.HasBuff(BuffID.Bleeding)) {
                modifiers.FinalDamage *= 1.18f;
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            //先判流血再开创口：命中流血目标开回弹窗口（owner 守门写方案侧字段）
            if (target.HasBuff(BuffID.Bleeding) && Owner.whoAmI == Main.myPlayer) {
                GsShadewoodSword.BloodRush = 40;
            }
            //终结拍开创口（各端一致量）
            if (IsFinisher) {
                target.AddBuff(BuffID.Bleeding, 300);
            }
        }
    }

    #endregion

    #region 灰烬木剑（余烬拍）

    /// <summary>
    /// 【灰烬木剑】材质：地狱灰烬木。签名：①余烬拍，每第三拍为灼烧重拍
    /// （几何独立：更重更慢），命中点燃 3 秒 ②轻-轻-重的火钳节奏对比
    /// </summary>
    internal class GsAshWoodSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.AshWoodSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsAshWoodSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: two quick cuts followed by a slow scorching blow that sets enemies on fire for 3 seconds";
        internal static readonly Color AshBright = new(212, 206, 198);  //灰烬浅灰
        internal static readonly Color CharMain = new(122, 116, 110);   //焦炭灰
        internal static readonly Color EmberHot = new(255, 150, 60);    //余烬橙

        //底伤 +5%：余烬拍 1.5x（拍均 ~1.17，但余烬拍 30 帧超原版用时、实际摊薄）+ 点燃 DoT，
        //综合 DPS 约为原版 120%~126%（灰烬木剑弱势，允许至 135%）
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 灰烬木剑手持：轻-轻-重三拍。0/1 快斩，2 余烬重拍（长举高抬、重顿帧、深前压），
    /// 余烬拍命中点燃
    /// </summary>
    internal class GsAshWoodSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.AshWoodSword;
        protected override Color EdgeBright => GsAshWoodSword.AshBright;
        protected override Color BodyMain => GsAshWoodSword.CharMain;
        protected override Color HotAccent => GsAshWoodSword.EmberHot;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //余烬拍：木组最重的一拍，几何整体放大
                return new GsBroadBeat {
                    Raise = 9, Hold = 3, Slash = 6, Recover = 12,
                    RaiseBack = 2.4f, Follow = 1.2f, ReachScale = 1.22f, LeanAmp = 0.095f,
                    DamageMult = 1.5f, Hitstop = 3, LungeSpeed = 3.5f, SwingPitch = -0.40f,
                };
            }
            //轻拍：最短的起手，火钳夹击式快斩
            return new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 6,
                RaiseBack = 1.7f, Follow = 0.9f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = stage == 0 ? 0.10f : 0.02f,
            };
        }

        protected override void PlaySwingSound() {
            base.PlaySwingSound();
            //余烬拍补一记火焰喷吐
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = -0.2f }, Owner.Center);
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            //余烬拍命中点燃 3 秒（各端一致量）
            if (IsFinisher) {
                target.AddBuff(BuffID.OnFire, 180);
            }
        }
    }

    #endregion

    #region 仙人掌剑（棘刺反噬）

    /// <summary>
    /// 【仙人掌剑】材质：沙漠仙人掌。签名：①棘刺反噬，终结拍命中自命中点向后上方
    /// 弹出 2 根自旋仙人掌刺 ②紧凑短弧的干脆刺劈节奏（后摆最小）
    /// </summary>
    internal class GsCactusSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.CactusSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsCactusSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: compact desert cuts; when the third slash connects, two cactus needles burst backward from the wound, each hitting once";
        internal static readonly Color PaleGreen = new(200, 235, 160); //仙人掌浅绿
        internal static readonly Color CactusMain = new(110, 160, 80); //仙人掌肉绿
        internal static readonly Color BloomHot = new(250, 240, 150);  //沙漠花黄

        //底伤 +5%：终结拍 1.28x（拍均 ~1.09）+ 终结命中 2 根 35% 棘刺（摊 ~+6%），
        //综合 DPS 约为原版 118%~123%（仙人掌剑弱势，允许至 125%）
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 仙人掌剑手持：三拍紧凑短弧（后摆全族最小，干脆的刺劈），
    /// 终结拍首个命中向后上方崩出 2 根自旋棘刺
    /// </summary>
    internal class GsCactusSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.CactusSword;
        protected override Color EdgeBright => GsCactusSword.PaleGreen;
        protected override Color BodyMain => GsCactusSword.CactusMain;
        protected override Color HotAccent => GsCactusSword.BloomHot;

        private bool needlesSpawned;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                return new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 4, Recover = 9,
                    RaiseBack = 1.9f, Follow = 1.05f, ReachScale = 1.15f, LeanAmp = 0.06f,
                    DamageMult = 1.28f, Hitstop = 2, LungeSpeed = 2.0f, SwingPitch = -0.05f,
                };
            }
            //紧凑短弧：后摆 1.5（全族最小），高音干脆
            return new GsBroadBeat {
                Raise = 4, Hold = 2, Slash = 3, Recover = 7,
                RaiseBack = 1.5f, Follow = 0.85f, ReachScale = 1f, LeanAmp = 0.035f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = stage == 0 ? 0.20f : 0.14f,
            };
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            //棘刺反噬：终结拍每挥一次只崩一次（首个命中触发；SpawnOwnedProj 守 owner）
            if (!IsFinisher || needlesSpawned) {
                return;
            }
            needlesSpawned = true;
            int type = ModContent.ProjectileType<GsCactusNeedleProj>();
            int dmg = Math.Max(1, (int)(Projectile.damage * 0.35f));
            for (int i = 0; i < 2; i++) {
                //自命中点向后上方抛出，两根散开
                Vector2 vel = new(-facingDir * (2.6f + i * 1.8f), -5.5f - i * 1.6f);
                SpawnOwnedProj(type, target.Center, vel, dmg, 1f);
            }
        }
    }

    #endregion

    #region 支援弹幕

    /// <summary>
    /// 拍岸沙浪：棕榈木剑终结拍甩出的沙团。快速坠地、撞物块即散、命中一跳；用原版沙枪沙球贴图
    /// </summary>
    internal class GsPalmSandWaveProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallGun;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.SandBallGun];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 50;
        }

        public override void AI() {
            //快速坠地的沙团
            Projectile.velocity.Y += 0.55f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
        }
    }

    /// <summary>
    /// 蚀木孢雾：乌木剑每拍留下的驻留圆团（非弧痕）。30 帧寿命，命中一跳低伤；
    /// 用原版孢子云贴图按判定半径画一笔作范围提示
    /// </summary>
    internal class GsEbonSporeMistProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SporeCloud;

        private const int Life = 30;
        private const float Radius = 36f;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.SporeCloud];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Life;//每目标只跳一次
            Projectile.timeLeft = Life;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Projectile.timeLeft > 4 ? null : false;

        /// <summary>圆团判定：中心 36px 半径</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        /// <summary>范围提示：原版孢子云贴图按判定半径缩放画一笔，末 8 帧淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frame = tex.Frame(1, Math.Max(1, Main.projFrames[Type]), 0, 0);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
            float scale = Radius * 2f / MathF.Max(frame.Width, 1);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, lightColor * fade,
                0f, frame.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 仙人掌棘刺：终结拍命中崩出的自旋小刺。抛物线快坠、撞物块即碎、命中一跳；用原版松针贴图
    /// </summary>
    internal class GsCactusNeedleProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.PineNeedleFriendly;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.PineNeedleFriendly];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            //抛物线快坠 + 自旋
            Projectile.velocity.Y += 0.4f;
            if (Projectile.velocity.Y > 15f) {
                Projectile.velocity.Y = 15f;
            }
            Projectile.rotation += 0.38f * (Projectile.velocity.X >= 0f ? 1f : -1f);
        }
    }

    #endregion
}
