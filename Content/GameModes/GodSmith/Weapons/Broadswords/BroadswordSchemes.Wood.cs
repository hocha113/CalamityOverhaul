using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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
    /// 生木弹性越打越顺 ②命中迸溅生木木屑与嫩芽绿微光 ③第三拍前压重劈
    /// </summary>
    internal class GsWoodenSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.WoodenSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsWoodenSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: green wood springs back; landing a hit primes the blade, " +
            "so the next slash within a breath raises twice as fast and deals 10% more damage";

        //松木色板
        internal static readonly Color SapBright = new(232, 214, 166);  //新木亮黄
        internal static readonly Color PineMain = new(176, 138, 90);    //松木体
        internal static readonly Color SproutHot = new(150, 220, 110);  //嫩芽绿
        internal static readonly Color BarkDeep = new(46, 34, 22);      //树皮深棕

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
    /// 木剑手持：三拍轻快劈砍，ai[2]=1 时为回弹斩（举相减半 + 伤害 +10% + 嫩芽绿闪）
    /// </summary>
    internal class GsWoodenSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.WoodenSword;
        protected override Color EdgeBright => GsWoodenSword.SapBright;
        protected override Color BodyMain => GsWoodenSword.PineMain;
        protected override Color HotAccent => GsWoodenSword.SproutHot;
        protected override Color DeepShadow => GsWoodenSword.BarkDeep;

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
                //生木回弹：举刀相减半，伤害 +10%，起手绿闪可感
                raiseDur = Math.Max(1, raiseDur / 2);
                totalDur = raiseDur + holdDur + slashDur + recoverDur;
                Projectile.damage = (int)(Projectile.damage * 1.10f);
                SetFlash(5);
            }
        }

        //回弹斩全程渗嫩芽绿
        protected override bool GlowAlways => IsFinisher || Rebounding;

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            //owner 守门写方案侧回弹窗口（myPlayer 消费）
            if (Owner.whoAmI == Main.myPlayer) {
                GsWoodenSword.ReboundTimer = 40;
            }
        }

        /// <summary>斩切期甩生木木屑，回弹斩补嫩芽绿光点（替换基类金属火星）</summary>
        protected override void HandleParticles(int phase) {
            if (phase != PhaseSlash) {
                return;
            }
            Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.55f, 1.0f));
            Dust chip = Dust.NewDustPerfect(at, DustID.t_LivingWood,
                sweepVel * Main.rand.NextFloat(2f, 5f), 60, default, Main.rand.NextFloat(0.8f, 1.2f));
            chip.noGravity = Main.rand.NextBool();
            if (Rebounding && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Light>(at, sweepVel * Main.rand.NextFloat(1.5f, 3f),
                    GsWoodenSword.SproutHot, Main.rand.NextFloat(0.08f, 0.14f))?.Configure(10, 0.7f);
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //生木木屑迸溅 + 嫩芽绿微光
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.t_LivingWood,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f), 40, default,
                    Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = Main.rand.NextBool(3);
            }
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                GsWoodenSword.SproutHot, 0.14f)?.Configure(9, 0.6f);
        }
    }

    #endregion

    #region 北极松木剑（霜脂）

    /// <summary>
    /// 【北极松木剑】材质：北地寒杉。签名：①霜脂，连续命中同一目标叠寒杉脂，
    /// 第 3 层迸霜雾并点上霜火 ②长滞帧的冻凝节奏，蓄而后发 ③滞帧期呵出霜息
    /// </summary>
    internal class GsBorealWoodSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BorealWoodSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsBorealWoodSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: repeated hits coat the same target in boreal sap; " +
            "the third layer bursts into frost mist and sets the target ablaze with frostburn";

        //寒杉色板
        internal static readonly Color FrostBright = new(220, 240, 250); //霜白
        internal static readonly Color ColdMain = new(130, 170, 200);    //寒杉青蓝
        internal static readonly Color IceHot = new(150, 230, 255);      //冰芯亮青
        internal static readonly Color NightDeep = new(26, 38, 52);      //极夜深蓝

        /// <summary>寒杉脂层数表：NPC → (层数, 最后命中帧)；单例静态，只在 myPlayer 路径读写</summary>
        internal static readonly Dictionary<int, (int stacks, uint time)> SapStacks = [];

        //底伤 +6%：终结拍 1.3x（拍均 ~1.10）+ 霜火 DoT，综合 DPS 约为原版 115%~120%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;
    }

    /// <summary>
    /// 北极松木剑手持：三拍冻凝斩，滞帧显著拉长（藏行程露停顿），
    /// 命中在方案侧记寒杉脂层数，第 3 层迸霜雾上霜火
    /// </summary>
    internal class GsBorealWoodSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BorealWoodSword;
        protected override Color EdgeBright => GsBorealWoodSword.FrostBright;
        protected override Color BodyMain => GsBorealWoodSword.ColdMain;
        protected override Color HotAccent => GsBorealWoodSword.IceHot;
        protected override Color DeepShadow => GsBorealWoodSword.NightDeep;

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
            //寒杉脂层数只在 owner 端记（myPlayer 消费）；上到第 3 层迸霜雾并挂霜火
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
                    for (int i = 0; i < 14; i++) {
                        Dust d = Dust.NewDustPerfect(target.Center, DustID.Frost,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f), 40, default,
                            Main.rand.NextFloat(1f, 1.6f));
                        d.noGravity = true;
                    }
                    PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                        GsBorealWoodSword.IceHot, 0.3f)?.Configure(12, 0.85f);
                }
            }
            else {
                dict[target.whoAmI] = (stacks, now);
            }
        }

        /// <summary>滞帧期沿刃呵出霜息，斩切期洒霜尘（替换基类火星）</summary>
        protected override void HandleParticles(int phase) {
            if (phase == PhaseHold && Main.rand.NextBool(2)) {
                //冻凝的呼吸：刃身周围浮起细霜
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.4f, 1f));
                Dust d = Dust.NewDustPerfect(at, DustID.Frost,
                    new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.7f)), 120, default, Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = true;
            }
            else if (phase == PhaseSlash) {
                Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.55f, 1.0f));
                Dust d = Dust.NewDustPerfect(at, DustID.Frost,
                    sweepVel * Main.rand.NextFloat(2f, 5f), 80, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = true;
                if (IsFinisher && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_Spark>(at, sweepVel * Main.rand.NextFloat(3f, 6f),
                        GsBorealWoodSword.IceHot, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(12, 18));
                }
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Frost,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 60, default,
                    Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }
    }

    #endregion

    #region 棕榈木剑（拍岸沙浪）

    /// <summary>
    /// 【棕榈木剑】材质：海滩棕榈木。签名：①拍岸沙浪，终结拍斩切爆发沿挥向甩出
    /// 3 团扇形沙浪弹幕，快速坠地命中一跳 ②长斩切相的宽弧横扫，像浪拍岸 ③斩切拖沙尘
    /// </summary>
    internal class GsPalmWoodSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.PalmWoodSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsPalmWoodSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: sweeping shore arcs; the third slash hurls a fan of three sand waves " +
            "that quickly crash to the ground, each hitting once";

        //棕榈沙金色板
        internal static readonly Color SandBright = new(245, 225, 170); //浅滩沙白
        internal static readonly Color PalmMain = new(210, 170, 105);   //棕榈木黄
        internal static readonly Color SunHot = new(255, 205, 120);     //日照沙金
        internal static readonly Color WetDeep = new(60, 45, 26);       //湿沙深褐

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
        protected override Color DeepShadow => GsPalmWoodSword.WetDeep;

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

        /// <summary>斩切期拖沙尘（有重力，像扬起的沙）</summary>
        protected override void HandleParticles(int phase) {
            if (phase != PhaseSlash) {
                return;
            }
            Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            int count = IsFinisher ? 2 : 1;
            for (int i = 0; i < count; i++) {
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1.0f));
                Dust d = Dust.NewDustPerfect(at, DustID.Sand,
                    sweepVel * Main.rand.NextFloat(2f, 5f), 60, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            for (int i = 0; i < 6; i++) {
                Dust.NewDustPerfect(target.Center, DustID.Sand,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f), 80, default,
                    Main.rand.NextFloat(0.9f, 1.3f));
            }
        }
    }

    #endregion

    #region 红木剑（藤蔓延势）

    /// <summary>
    /// 【红木剑】材质：丛林红木。签名：①藤蔓延势，四拍藤鞭连击，终结拍触及 1.3 倍
    /// 且刃尖延伸藤蔓虚影 ②终结拍命中毒藤上毒 ③音高逐拍下行的鞭打节奏
    /// </summary>
    internal class GsRichMahoganySword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.RichMahoganySword;

        protected override int HeldProjID => ModContent.ProjectileType<GsRichMahoganySwordHeld>();

        protected override int ComboBeats => 4;

        protected override string GsDescFallback =>
            "Reforged: a four-beat vine-whip combo; the fourth slash extends a phantom vine " +
            "for far greater reach and poisons whatever it entangles";

        //丛林红木色板
        internal static readonly Color LeafBright = new(205, 235, 150); //嫩叶浅绿
        internal static readonly Color MahoganyMain = new(150, 95, 65); //红木棕红
        internal static readonly Color VineHot = new(110, 205, 95);     //藤蔓浓绿
        internal static readonly Color JungleDeep = new(32, 40, 24);    //林荫深绿

        //底伤 +8%：终结拍 1.22x（四拍拍均 ~1.06）+ 毒藤 DOT 小额收益，
        //综合 DPS 约为原版 112%~116%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 红木剑手持：四拍藤鞭连击（族内唯一四拍），前三拍快鞭、
    /// 终结拍触及 1.3 倍并画刃尖藤蔓虚影，命中毒藤上毒
    /// </summary>
    internal class GsRichMahoganySwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.RichMahoganySword;
        protected override Color EdgeBright => GsRichMahoganySword.LeafBright;
        protected override Color BodyMain => GsRichMahoganySword.MahoganyMain;
        protected override Color HotAccent => GsRichMahoganySword.VineHot;
        protected override Color DeepShadow => GsRichMahoganySword.JungleDeep;

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

        /// <summary>斩切期洒丛林叶尘，终结拍补藤绿光点</summary>
        protected override void HandleParticles(int phase) {
            if (phase != PhaseSlash) {
                return;
            }
            Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1.0f));
            Dust d = Dust.NewDustPerfect(at, DustID.JunglePlants,
                sweepVel * Main.rand.NextFloat(2f, 4.5f), 80, default, Main.rand.NextFloat(0.8f, 1.2f));
            d.noGravity = Main.rand.NextBool();
            if (IsFinisher && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Light>(Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.9f, 1.25f)),
                    sweepVel * 1.5f, GsRichMahoganySword.VineHot, Main.rand.NextFloat(0.08f, 0.13f))?.Configure(9, 0.65f);
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            int leaves = IsFinisher ? 8 : 4;
            for (int i = 0; i < leaves; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.JunglePlants,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 60, default,
                    Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = Main.rand.NextBool(3);
            }
            if (IsFinisher) {
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                    GsRichMahoganySword.VineHot, 0.24f)?.Configure(12, 0.8f);
            }
        }

        /// <summary>终结拍刃尖延伸的藤蔓虚影：加色绿虚刀 + 沿延伸段的叶点（确定性抖动）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (!IsFinisher || CurrentPhase < PhaseSlash || fanFade <= 0.05f) {
                return;
            }
            Main.instance.LoadItem(SwordItemID);
            Texture2D tex = TextureAssets.Item[SwordItemID].Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);
            float scale = mainReach * (BladeTipFill - BladePark) * 2f / MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
            Vector2 dir = mainAngle.ToRotationVector2();

            //藤影：沿刃向再探出去的加色绿虚刀
            Vector2 vinePos = Hand + dir * (mainReach * BladePark * 1.30f) - Main.screenPosition;
            Color vine = GsRichMahoganySword.VineHot * (0.34f * fanFade);
            vine.A = 0;
            sb.Draw(tex, vinePos, null, vine, mainAngle + rotOffset, tex.Size() / 2f, scale * 1.22f, effect, 0);

            //延伸段小叶点：确定性侧摆，不掷 Main.rand
            Vector2 side = (mainAngle + MathHelper.PiOver2).ToRotationVector2();
            for (int i = 0; i < 3; i++) {
                float along = 1.02f + 0.12f * (i + 1);
                Vector2 at = Hand + dir * (mainReach * along)
                    + side * ((DrawRand01(i * 7 + 1) - 0.5f) * 16f) - Main.screenPosition;
                Color leaf = GsRichMahoganySword.LeafBright * (0.45f * fanFade);
                leaf.A = 0;
                sb.Draw(glow, at, null, leaf, 0f, glow.Size() / 2f,
                    0.15f + 0.05f * DrawRand01(i + 11), SpriteEffects.None, 0f);
            }
        }
    }

    #endregion

    #region 乌木剑（蚀木孢雾）

    /// <summary>
    /// 【乌木剑】材质：腐化乌木。签名：①蚀木孢雾，每拍斩切后在刀路中点留一团
    /// 30 帧驻留圆团孢雾，命中一跳低伤（与暗影蚀刃的弧形蚀痕区分：圆团不是弧痕）
    /// ②长收势的阴郁拖拍节奏 ③命中溅腐化紫尘
    /// </summary>
    internal class GsEbonwoodSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.EbonwoodSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsEbonwoodSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash leaves a lingering puff of corrupt spores at the midpoint " +
            "of its path, dealing one tick of light damage to anything caught inside";

        //腐化乌木色板
        internal static readonly Color PaleBright = new(190, 160, 215); //苍紫灰
        internal static readonly Color EbonMain = new(112, 92, 132);    //乌木紫灰
        internal static readonly Color SporeHot = new(150, 90, 200);    //孢子亮紫
        internal static readonly Color RotDeep = new(24, 18, 34);       //腐暗深紫

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
        protected override Color DeepShadow => GsEbonwoodSword.RotDeep;

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

        //乌木吸光
        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsEbonwoodSword.RotDeep, 0.22f);

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

        /// <summary>斩切期渗腐化紫尘（无重力浮尘，非火星）</summary>
        protected override void HandleParticles(int phase) {
            if (phase != PhaseSlash || !Main.rand.NextBool(2)) {
                return;
            }
            Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f));
            Dust d = Dust.NewDustPerfect(at, DustID.CorruptPlants,
                (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(1.5f, 3.5f),
                100, default, Main.rand.NextFloat(0.8f, 1.2f));
            d.noGravity = true;
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            int puffs = IsFinisher ? 7 : 4;
            for (int i = 0; i < puffs; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.CorruptPlants,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 80, default,
                    Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }
        }
    }

    #endregion

    #region 暗影木剑（嗜血纹）

    /// <summary>
    /// 【暗影木剑】材质：猩红暗影木。签名：①嗜血纹，对流血目标伤害 +18% 且触发回弹
    /// （下一拍举刀 -30%）②终结拍命中施加流血，自铺嗜血循环 ③命中流血目标血雾加倍
    /// </summary>
    internal class GsShadewoodSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.ShadewoodSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsShadewoodSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: the third strike opens a bleeding wound; hits against bleeding targets " +
            "deal 18% more damage and quicken your next slash";

        //猩红暗影木色板
        internal static readonly Color FleshBright = new(230, 130, 115); //血肉浅红
        internal static readonly Color ShadeMain = new(145, 58, 58);     //暗影木赤褐
        internal static readonly Color BloodHot = new(255, 64, 72);      //鲜血亮红
        internal static readonly Color GoreDeep = new(36, 12, 14);       //凝血暗红

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
    /// （ai[2]=1 时举刀 -30% + 血红闪）；终结拍命中施加流血
    /// </summary>
    internal class GsShadewoodSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.ShadewoodSword;
        protected override Color EdgeBright => GsShadewoodSword.FleshBright;
        protected override Color BodyMain => GsShadewoodSword.ShadeMain;
        protected override Color HotAccent => GsShadewoodSword.BloodHot;
        protected override Color DeepShadow => GsShadewoodSword.GoreDeep;

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
                //嗜血回弹：举刀 -30%，血红闪
                raiseDur = Math.Max(1, (int)(raiseDur * 0.7f));
                totalDur = raiseDur + holdDur + slashDur + recoverDur;
                SetFlash(5);
            }
        }

        protected override bool GlowAlways => IsFinisher || BloodRushing;

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

        /// <summary>斩切期甩暗影木屑与血红火花</summary>
        protected override void HandleParticles(int phase) {
            if (phase != PhaseSlash) {
                return;
            }
            Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.55f, 1.0f));
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(at, DustID.Shadewood,
                    sweepVel * Main.rand.NextFloat(2f, 4.5f), 60, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = Main.rand.NextBool();
            }
            else {
                PRTLoader.NewParticle<PRT_Spark>(at, sweepVel * Main.rand.NextFloat(3f, 6f),
                    BloodRushing ? GsShadewoodSword.BloodHot : GsShadewoodSword.FleshBright,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(12, 18));
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //命中流血目标血雾加倍
            int mist = target.HasBuff(BuffID.Bleeding) ? 8 : 3;
            for (int i = 0; i < mist; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f), 60, default,
                    Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = Main.rand.NextBool();
            }
            if (target.HasBuff(BuffID.Bleeding)) {
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                    GsShadewoodSword.BloodHot, 0.26f)?.Configure(10, 0.8f);
            }
        }
    }

    #endregion

    #region 灰烬木剑（余烬拍）

    /// <summary>
    /// 【灰烬木剑】材质：地狱灰烬木。签名：①余烬拍，每第三拍为灼烧重拍
    /// （几何独立：更重更慢），命中点燃 3 秒 ②灼烧拍全程灰烬橙热纹常亮 + 飘烬
    /// ③轻-轻-重的火钳节奏对比
    /// </summary>
    internal class GsAshWoodSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.AshWoodSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsAshWoodSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: two quick cuts followed by a slow scorching blow that sets enemies " +
            "on fire for 3 seconds";

        //地狱灰烬色板
        internal static readonly Color AshBright = new(212, 206, 198);  //灰烬浅灰
        internal static readonly Color CharMain = new(122, 116, 110);   //焦炭灰
        internal static readonly Color EmberHot = new(255, 150, 60);    //余烬橙
        internal static readonly Color CinderDeep = new(30, 26, 24);    //焦黑

        //底伤 +5%：余烬拍 1.5x（拍均 ~1.17，但余烬拍 30 帧超原版用时、实际摊薄）+ 点燃 DoT，
        //综合 DPS 约为原版 120%~126%（灰烬木剑弱势，允许至 135%）
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 灰烬木剑手持：轻-轻-重三拍。0/1 快斩，2 余烬重拍（长举高抬、重顿帧、深前压），
    /// 余烬拍命中点燃，全程热纹常亮 + 飘烬
    /// </summary>
    internal class GsAshWoodSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.AshWoodSword;
        protected override Color EdgeBright => GsAshWoodSword.AshBright;
        protected override Color BodyMain => GsAshWoodSword.CharMain;
        protected override Color HotAccent => GsAshWoodSword.EmberHot;
        protected override Color DeepShadow => GsAshWoodSword.CinderDeep;

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

        //焦炭吸光；余烬拍热纹常亮由基类 GlowAlways=IsFinisher 提供
        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsAshWoodSword.CinderDeep, 0.20f);

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

        /// <summary>余烬拍举相聚热、斩切喷火星飘烬；轻拍只掉灰</summary>
        protected override void HandleParticles(int phase) {
            if (IsFinisher) {
                if (phase is PhaseRaise or PhaseHold && Main.rand.NextBool(2)) {
                    //聚热：刃身升起火尘
                    Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.4f, 1f));
                    Dust d = Dust.NewDustPerfect(at, DustID.Torch,
                        new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)), 80, default, Main.rand.NextFloat(0.8f, 1.3f));
                    d.noGravity = true;
                }
                else if (phase == PhaseSlash) {
                    Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                    for (int i = 0; i < 2; i++) {
                        Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f));
                        Dust d = Dust.NewDustPerfect(at, Main.rand.NextBool() ? DustID.Torch : DustID.Ash,
                            sweepVel * Main.rand.NextFloat(2.5f, 6f), 60, default, Main.rand.NextFloat(0.9f, 1.4f));
                        d.noGravity = d.type == DustID.Torch;
                    }
                }
                else if (phase == PhaseRecover && timer % 3 == 0 && fanFade > 0.2f) {
                    //收势飘烬
                    Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 0.95f));
                    Dust d = Dust.NewDustPerfect(at, DustID.Torch,
                        new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)), 100, default, Main.rand.NextFloat(0.7f, 1f));
                    d.noGravity = true;
                }
            }
            else if (phase == PhaseSlash && Main.rand.NextBool(2)) {
                //轻拍掉灰
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f));
                Dust.NewDustPerfect(at, DustID.Ash,
                    (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(1.5f, 3f),
                    100, default, Main.rand.NextFloat(0.7f, 1f));
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            if (!IsFinisher) {
                return;
            }
            //余烬拍命中喷火
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Torch,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f), 60, default,
                    Main.rand.NextFloat(1.1f, 1.7f));
                d.noGravity = true;
            }
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                GsAshWoodSword.EmberHot, 0.3f)?.Configure(12, 0.85f);
        }
    }

    #endregion

    #region 仙人掌剑（棘刺反噬）

    /// <summary>
    /// 【仙人掌剑】材质：沙漠仙人掌。签名：①棘刺反噬，终结拍命中自命中点向后上方
    /// 弹出 2 根自旋仙人掌刺 ②紧凑短弧的干脆刺劈节奏（后摆最小）③命中溅仙人掌碎屑
    /// </summary>
    internal class GsCactusSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.CactusSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsCactusSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: compact desert cuts; when the third slash connects, two cactus needles " +
            "burst backward from the wound, each hitting once";

        //沙漠仙人掌色板
        internal static readonly Color PaleGreen = new(200, 235, 160); //仙人掌浅绿
        internal static readonly Color CactusMain = new(110, 160, 80); //仙人掌肉绿
        internal static readonly Color BloomHot = new(250, 240, 150);  //沙漠花黄
        internal static readonly Color ThornDeep = new(28, 42, 24);    //刺荫深绿

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
        protected override Color DeepShadow => GsCactusSword.ThornDeep;

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

        /// <summary>斩切期溅仙人掌碎屑与沙尘</summary>
        protected override void HandleParticles(int phase) {
            if (phase != PhaseSlash) {
                return;
            }
            Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.55f, 1.0f));
            Dust d = Dust.NewDustPerfect(at, Main.rand.NextBool(3) ? DustID.Sand : DustID.t_Cactus,
                sweepVel * Main.rand.NextFloat(2f, 4.5f), 60, default, Main.rand.NextFloat(0.8f, 1.2f));
            d.noGravity = Main.rand.NextBool(3);
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            int bits = IsFinisher ? 8 : 4;
            for (int i = 0; i < bits; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.t_Cactus,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f), 40, default,
                    Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = Main.rand.NextBool(3);
            }
        }
    }

    #endregion

    #region 支援弹幕

    /// <summary>
    /// 拍岸沙浪：棕榈木剑终结拍甩出的沙团。快速坠地、撞物块即散、命中一跳。
    /// 自绘：真 alpha 沙团（Extra_98 染沙黄）+ 加色日金边光；抖动全 identity 播种
    /// </summary>
    internal class GsPalmSandWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

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
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                    -Projectile.velocity * 0.15f, 100, default, Main.rand.NextFloat(0.7f, 1.1f));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 80, default,
                    Main.rand.NextFloat(0.9f, 1.4f));
            }
        }

        /// <summary>确定性伪随机（identity+salt 播种，逐帧稳定）</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blot == null || glow == null) {
                return false;
            }
            Vector2 at = Projectile.Center - Main.screenPosition;
            //沙团体：真 alpha 贴图染沙黄，沿速度向压扁
            float squash = 0.85f + 0.2f * SegRand(1);
            Color body = new Color(222, 185, 120) * 0.9f;
            Main.EntitySpriteDraw(blot, at, null, body, Projectile.rotation,
                blot.Size() * 0.5f, new Vector2(0.20f * squash, 0.13f), SpriteEffects.None, 0);
            //日金边光：加色 A=0
            Color rim = GsPalmWoodSword.SunHot * 0.5f;
            rim.A = 0;
            Main.EntitySpriteDraw(glow, at, null, rim, 0f,
                glow.Size() * 0.5f, 0.24f + 0.04f * SegRand(2), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 蚀木孢雾：乌木剑每拍留下的驻留圆团（非弧痕）。30 帧寿命，命中一跳低伤。
    /// 自绘：多粒真 alpha 暗紫团 + 加色紫边呼吸光；抖动全 identity 播种
    /// </summary>
    internal class GsEbonSporeMistProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Life = 30;
        private float Life01 => 1f - (Projectile.timeLeft / (float)Life);

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

        public override void AI() {
            Lighting.AddLight(Projectile.Center, GsEbonwoodSword.SporeHot.ToVector3() * (0.25f * (1f - Life01)));
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(6f, 26f),
                    DustID.CorruptPlants, new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.7f)), 120, default,
                    Main.rand.NextFloat(0.6f, 1f));
                d.noGravity = true;
            }
        }

        /// <summary>圆团判定：中心 36px 半径</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= 36f;

        /// <summary>确定性伪随机（identity+salt 播种，逐帧稳定）</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blot == null || glow == null) {
                return false;
            }
            float life = Life01;
            //首 4 帧涨起、末 8 帧散去
            float grow = MathHelper.Clamp(life * Life / 4f, 0f, 1f);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
            float vis = grow * fade;
            Vector2 center = Projectile.Center - Main.screenPosition;

            for (int i = 0; i < 5; i++) {
                float ang = SegRand(i) * MathHelper.TwoPi;
                float dist = 6f + 16f * SegRand(i + 20);
                Vector2 at = center + ang.ToRotationVector2() * dist;
                float segScale = 0.10f + 0.08f * SegRand(i + 40);
                //暗紫团体：真 alpha 压暗（加色物理上做不出暗团）
                Color dark = GsEbonwoodSword.RotDeep * (vis * 0.55f);
                Main.EntitySpriteDraw(blot, at, null, dark, ang,
                    blot.Size() * 0.5f, segScale, SpriteEffects.None, 0);
            }
            //紫边呼吸光：加色 A=0，各团错相
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + SegRand(7) * 6.28f);
            Color edge = GsEbonwoodSword.SporeHot * (vis * 0.4f * pulse);
            edge.A = 0;
            Main.EntitySpriteDraw(glow, center, null, edge, 0f,
                glow.Size() * 0.5f, 0.6f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 仙人掌棘刺：终结拍命中崩出的自旋小刺。抛物线快坠、撞物块即碎、命中一跳。
    /// 自绘：细长绿刺（MagicPixel 双层拉伸）+ 尖端亮点；抖动全 identity 播种
    /// </summary>
    internal class GsCactusNeedleProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

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

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.t_Cactus,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f), 60, default,
                    Main.rand.NextFloat(0.8f, 1.1f));
                d.noGravity = Main.rand.NextBool(3);
            }
        }

        /// <summary>确定性伪随机（identity+salt 播种，逐帧稳定）</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            Rectangle src = new(0, 0, 1, 1);
            Vector2 at = Projectile.Center - Main.screenPosition;
            Vector2 origin = new(0.5f, 0.5f);
            float len = 15f + 3f * SegRand(1);
            //刺体：深绿细杆
            Main.EntitySpriteDraw(pixel, at, src, GsCactusSword.ThornDeep * 0.95f, Projectile.rotation,
                origin, new Vector2(len, 3f), SpriteEffects.None, 0);
            //刺身亮面：靠尖端一半提亮
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Main.EntitySpriteDraw(pixel, at + dir * (len * 0.25f), src, GsCactusSword.CactusMain, Projectile.rotation,
                origin, new Vector2(len * 0.5f, 1.6f), SpriteEffects.None, 0);
            //尖端亮点：加色 A=0
            Color tip = GsCactusSword.BloomHot * 0.6f;
            tip.A = 0;
            Main.EntitySpriteDraw(glow, at + dir * (len * 0.5f), null, tip, 0f,
                glow.Size() * 0.5f, 0.10f, SpriteEffects.None, 0);
            return false;
        }
    }

    #endregion
}
