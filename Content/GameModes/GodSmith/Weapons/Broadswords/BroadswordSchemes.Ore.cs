using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    //================================================================
    // 矿剑组七把：金属重量阶梯（铜最轻快 → 铂金最沉重）。
    // 拍表时长/顿帧/音高沿阶梯逐把下沉，每把另有独立签名机制。
    //================================================================

    #region 铜阔剑：导电
    /// <summary>
    /// 【铜导之刃】材质：新锻亮铜。签名：①全组最轻快的双拍连击，音高最高
    /// ②「导电」命中湿身目标伤害 +15% 并迸静电青白火花 ③铜橙刀光带绿锈垫影
    /// </summary>
    internal class GsCopperBroadsword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.CopperBroadsword;

        protected override int HeldProjID => ModContent.ProjectileType<GsCopperBroadswordHeld>();

        protected override int ComboBeats => 2;

        protected override string GsDescFallback =>
            "Reforged: a nimble two-beat combo; strikes conduct through drenched foes, dealing bonus damage with a static crackle";

        //铜橙+绿锈色板
        internal static readonly Color CopperBright = new(255, 202, 148); //亮铜刃缘
        internal static readonly Color CopperMain = new(198, 116, 62);    //铜身橙
        internal static readonly Color CopperZap = new(168, 240, 255);    //静电青白
        internal static readonly Color CopperRust = new(26, 48, 40);      //绿锈暗影

        //预算账：双拍均伤 ~1.06x、总帧短于 useAnimation（冷却吃 max 两者不提速）；
        //导电 +15% 仅湿身目标（雨战/水战摊入均值约 +2%）→ 综合 DPS ~原版 110%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 铜阔剑手持：双拍轻剑，全组最快出手（Raise 4/Slash 3/Recover 7 量级）、音高最高。
    /// 命中湿身目标走导电分支。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsCopperBroadswordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.CopperBroadsword;
        protected override Color EdgeBright => GsCopperBroadsword.CopperBright;
        protected override Color BodyMain => GsCopperBroadsword.CopperMain;
        protected override Color HotAccent => GsCopperBroadsword.CopperZap;
        protected override Color DeepShadow => GsCopperBroadsword.CopperRust;

        protected override int BeatCount => 2;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 1) {
                //第二拍略沉一分并小步前压，双拍有主副之分
                return new GsBroadBeat {
                    Raise = 4, Hold = 1, Slash = 3, Recover = 8,
                    RaiseBack = 1.75f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.04f,
                    DamageMult = 1.12f, Hitstop = 1, LungeSpeed = 1.2f, SwingPitch = 0.5f,
                };
            }
            return new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.6f, Follow = 0.9f, ReachScale = 0.95f, LeanAmp = 0.03f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.38f,
            };
        }

        /// <summary>轻剑不配终结厚响，只留高音快哨</summary>
        protected override void PlaySwingSound()
            => SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = Beat.SwingPitch }, Owner.Center);

        /// <summary>导电：湿身目标 +15% 伤害</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.wet) {
                modifiers.SourceDamage *= 1.15f;
            }
        }

        /// <summary>导电命中：静电青白火花 + 电尘（基类已守非服务器端）</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            if (!target.wet) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 7f);
                PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, GsCopperBroadsword.CopperZap
                    , Main.rand.NextFloat(0.3f, 0.55f))?.Configure(false, Main.rand.Next(10, 16));
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Electric
                    , Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f), 0, default, Main.rand.NextFloat(0.7f, 1f));
                d.noGravity = true;
            }
        }
    }
    #endregion

    #region 锡阔剑：锡鸣共振
    /// <summary>
    /// 【锡鸣之刃】材质：亮锡薄刃。签名：①四拍连击音高沿音阶逐拍上行
    /// ②第四拍共鸣拍伤害 +20%、白闪起手并伴高音脆响 ③亮锡银刀光
    /// </summary>
    internal class GsTinBroadsword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.TinBroadsword;

        protected override int HeldProjID => ModContent.ProjectileType<GsTinBroadswordHeld>();

        protected override int ComboBeats => 4;

        protected override string GsDescFallback =>
            "Reforged: a four-beat combo that rings up the scale; the fourth resonant strike lands harder in a white flash";

        //亮锡银色板
        internal static readonly Color TinBright = new(232, 238, 244);  //锡亮银
        internal static readonly Color TinMain = new(152, 162, 174);    //锡身灰
        internal static readonly Color TinRing = new(255, 255, 240);    //共鸣白
        internal static readonly Color TinDeep = new(40, 44, 54);       //锡暗影

        //预算账：拍均伤 (1+1+1+1.2)/4≈1.05，共鸣拍要连满四刀（断手 55 帧回拍）
        //实战摊 ~1.04 → base 1.05 → 综合 DPS ~原版 109%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 锡阔剑手持：四拍音阶剑，SwingPitch 逐拍 +0.1 上行；
    /// 第四拍共鸣拍伤害 +20%、SetFlash 白闪、MaxMana 脆响。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsTinBroadswordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.TinBroadsword;
        protected override Color EdgeBright => GsTinBroadsword.TinBright;
        protected override Color BodyMain => GsTinBroadsword.TinMain;
        protected override Color HotAccent => GsTinBroadsword.TinRing;
        protected override Color DeepShadow => GsTinBroadsword.TinDeep;

        protected override int BeatCount => 4;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 3) {
                //共鸣拍：微沉半档换一记加重的高音收束
                return new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 4, Recover = 9,
                    RaiseBack = 1.95f, Follow = 1.15f, ReachScale = 1.08f, LeanAmp = 0.055f,
                    DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 1.5f, SwingPitch = 0.42f,
                };
            }
            //前三拍等长，音高沿音阶上行
            return new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 3, Recover = 8,
                RaiseBack = 1.7f, Follow = 0.95f, ReachScale = 0.97f, LeanAmp = 0.035f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.12f + stage * 0.1f,
            };
        }

        /// <summary>音阶剑不用厚响垫底，共鸣脆响放 OnSlashBegin</summary>
        protected override void PlaySwingSound()
            => SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.75f, Pitch = Beat.SwingPitch }, Owner.Center);

        protected override void OnSlashBegin() {
            if (!IsFinisher) {
                return;
            }
            //共鸣：白闪 + 一记高音脆响
            SetFlash(8);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.7f, Pitch = 0.5f }, Owner.Center);
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            if (IsFinisher) {
                //共鸣拍命中放一朵共鸣白光
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GsTinBroadsword.TinRing, 0.28f)
                    ?.Configure(12, 0.9f);
            }
        }
    }
    #endregion

    #region 铅阔剑：铅坠劈
    /// <summary>
    /// 【铅坠重刃】材质：灌铅钝刃。签名：①全拍表沉钝、击退 +40%
    /// ②「铅坠劈」终结拍改为过顶下劈，命中顿帧 3 ③落劈震起地面尘土
    /// </summary>
    internal class GsLeadBroadsword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.LeadBroadsword;

        protected override int HeldProjID => ModContent.ProjectileType<GsLeadBroadswordHeld>();

        protected override string GsDescFallback =>
            "Reforged: slow, brutal swings with heavy knockback; the finisher drops as an overhead slam that shakes dust from the ground";

        //铅灰蓝色板
        internal static readonly Color LeadBright = new(172, 184, 208); //铅灰亮
        internal static readonly Color LeadMain = new(98, 108, 134);    //铅身蓝灰
        internal static readonly Color LeadHot = new(142, 162, 224);    //坠劈冷蓝
        internal static readonly Color LeadDeep = new(22, 25, 36);      //铅沉暗影

        //预算账：拍均伤 (1+1+1.35)/3≈1.12 但连段总帧长于原版节奏 → base 1.06
        //综合 DPS ~原版 108%；击退 +40% 是控场收益不进 DPS
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        public override void GsModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
            => knockback *= 1.4f;
    }

    /// <summary>
    /// 铅阔剑手持：三拍钝剑，两记慢横劈接「铅坠劈」——终结拍整替几何为过顶下劈
    /// （自前方拖上头顶、翻过天顶砸向脚前），命中顿帧 3，落劈震起地面尘土。
    /// ai[0]=拍号 ai[1]=交替符号（终结拍忽略符号，恒过顶）
    /// </summary>
    internal class GsLeadBroadswordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.LeadBroadsword;
        protected override Color EdgeBright => GsLeadBroadsword.LeadBright;
        protected override Color BodyMain => GsLeadBroadsword.LeadMain;
        protected override Color HotAccent => GsLeadBroadsword.LeadHot;
        protected override Color DeepShadow => GsLeadBroadsword.LeadDeep;

        private bool slammed;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //铅坠劈：长举过顶 + 顿帧 3，几何在 UpdateBladeTransform 整替
                return new GsBroadBeat {
                    Raise = 10, Hold = 3, Slash = 5, Recover = 13,
                    RaiseBack = 2.3f, Follow = 1.2f, ReachScale = 1.1f, LeanAmp = 0.1f,
                    DamageMult = 1.35f, Hitstop = 3, LungeSpeed = 2.2f, SwingPitch = -0.52f,
                };
            }
            return new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 11,
                RaiseBack = 2.0f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.06f,
                DamageMult = 1f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = stage == 0 ? -0.34f : -0.42f,
            };
        }

        protected override void OnStageInit() {
            base.OnStageInit();
            if (IsFinisher) {
                //过顶劈恒沿面朝向翻落，压掉交替符号，残影/涂抹方向随之对齐
                swingDir = facingDir;
            }
        }

        /// <summary>过顶劈的起止角：从头顶后方翻过天顶砸向脚前</summary>
        private float ChopStart => -MathHelper.PiOver2 - facingDir * 0.5f;
        private float ChopEnd => ChopStart + facingDir * 3.0f;

        /// <summary>终结拍整替几何为过顶下劈，普通拍走基类横劈</summary>
        protected override void UpdateBladeTransform(int phase) {
            if (!IsFinisher) {
                base.UpdateBladeTransform(phase);
                return;
            }
            float chopStart = ChopStart;
            switch (phase) {
                case PhaseRaise: {
                    //自前方低位拖上头顶，越拖越慢（铅的重量感）
                    float p = timer / (float)raiseDur;
                    float eased = 1f - MathF.Pow(1f - p, 3f);
                    float liftFrom = chopStart + facingDir * 1.25f;
                    mainAngle = MathHelper.Lerp(liftFrom, chopStart, eased);
                    mainReach = FullReach * MathHelper.Lerp(0.55f, 0.9f, eased);
                    slashProgress = 0f;
                    break;
                }
                case PhaseHold: {
                    float p = (timer - raiseDur) / (float)holdDur;
                    mainAngle = chopStart - facingDir * 0.06f * EaseOutQuad(p);
                    mainReach = FullReach * MathHelper.Lerp(0.9f, 0.95f, EaseOutQuad(p));
                    slashProgress = 0f;
                    break;
                }
                case PhaseSlash: {
                    float p = (timer - raiseDur - holdDur) / (float)slashDur;
                    slashProgress = p;
                    mainAngle = MathHelper.Lerp(chopStart, ChopEnd, SwingCurve(p));
                    mainReach = FullReach * (0.95f + 0.05f * MathF.Sin(MathHelper.Clamp(p * 1.8f, 0f, 1f) * MathHelper.Pi));
                    break;
                }
                default: {
                    float q = (timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    float settle = EaseOutQuad(Math.Min(1f, q * 2.2f));
                    mainAngle = ChopEnd + facingDir * 0.08f * (1f - settle);
                    mainReach = FullReach * MathHelper.Lerp(0.95f, 0.8f, q * q);
                    slashProgress = 1f;
                    float fadeDur = MathF.Max(4f, recoverDur * 0.7f);
                    fanFade = MathHelper.Clamp(1f - ((timer - raiseDur - holdDur - slashDur) / fadeDur), 0f, 1f);
                    break;
                }
            }
            mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
        }

        protected override void HandlePhaseEvents(int phase) {
            base.HandlePhaseEvents(phase);
            //落劈触底：收势首帧向脚下找地面，震起尘土
            if (IsFinisher && !slammed && phase == PhaseRecover) {
                slammed = true;
                if (!VaultUtils.isServer) {
                    SpawnSlamDust();
                }
            }
        }

        /// <summary>刃尖下方八格内找实心地面，沿地面震起烟尘与碎石</summary>
        private void SpawnSlamDust() {
            Point tile = mainTip.ToTileCoordinates();
            for (int j = 0; j < 8; j++) {
                if (!WorldGen.SolidTile(tile.X, tile.Y + j)) {
                    continue;
                }
                float groundY = (tile.Y + j) * 16f;
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.9f, Pitch = -0.5f }, mainTip);
                for (int i = 0; i < 14; i++) {
                    float x = mainTip.X + Main.rand.NextFloat(-52f, 52f);
                    Dust d = Dust.NewDustPerfect(new Vector2(x, groundY - 2f)
                        , Main.rand.NextBool() ? DustID.Smoke : DustID.Stone
                        , new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(0.8f, 2.4f))
                        , 120, default, Main.rand.NextFloat(0.9f, 1.5f));
                    d.noGravity = Main.rand.NextBool(3);
                }
                break;
            }
        }
    }
    #endregion

    #region 银阔剑：月辉刃
    /// <summary>
    /// 【月辉银刃】材质：淬月的纯银。签名：①夜间伤害 +12%
    /// ②命中迸溅白银辉光、夜里另升一粒月尘 ③终结拍涂抹带更长更亮、残影多一层
    /// </summary>
    internal class GsSilverBroadsword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.SilverBroadsword;

        protected override int HeldProjID => ModContent.ProjectileType<GsSilverBroadswordHeld>();

        protected override string GsDescFallback =>
            "Reforged: blessed silver that bites harder at night; the finisher smears a long trail of moonlit silver";

        //银白冷色板
        internal static readonly Color SilverBright = new(236, 246, 255); //银亮白
        internal static readonly Color SilverMain = new(172, 192, 216);   //银身冷灰
        internal static readonly Color SilverMoon = new(202, 226, 255);   //月辉淡蓝
        internal static readonly Color SilverDeep = new(30, 38, 52);      //银夜暗影

        //预算账：base 1.06，夜间 ×1.12≈1.19（未破 120% 上限）；
        //昼夜各半摊入均值 ≈1.12 → 综合 DPS ~原版 112%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            damage *= 1.06f;
            if (!Main.dayTime) {
                damage *= 1.12f;
            }
        }
    }

    /// <summary>
    /// 银阔剑手持：三拍中量剑。终结拍涂抹带更长更亮（覆 SmearOuterColor/GhostCount）；
    /// 命中迸银辉，夜里另升月尘。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsSilverBroadswordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.SilverBroadsword;
        protected override Color EdgeBright => GsSilverBroadsword.SilverBright;
        protected override Color BodyMain => GsSilverBroadsword.SilverMain;
        protected override Color HotAccent => GsSilverBroadsword.SilverMoon;
        protected override Color DeepShadow => GsSilverBroadsword.SilverDeep;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                return new GsBroadBeat {
                    Raise = 7, Hold = 2, Slash = 4, Recover = 11,
                    RaiseBack = 2.1f, Follow = 1.2f, ReachScale = 1.12f, LeanAmp = 0.07f,
                    DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 2.8f, SwingPitch = -0.24f,
                };
            }
            GsBroadBeat b = GsBroadBeat.Standard;
            b.SwingPitch = stage == 0 ? -0.04f : -0.12f;
            return b;
        }

        //终结拍的月辉涂抹：更亮的月色 + 多一层残影拖长挥迹
        protected override Color SmearOuterColor => IsFinisher ? GsSilverBroadsword.SilverMoon : EdgeBright;
        protected override int GhostCount => IsFinisher ? 4 : 2;

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //银辉迸溅
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GsSilverBroadsword.SilverBright, 0.22f)
                ?.Configure(12, 0.85f);
            if (!Main.dayTime) {
                //夜里另升一粒月尘
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f), Color.White, 0.8f)
                    ?.Configure(GsSilverBroadsword.SilverMoon, 22, 0.08f, 1.2f);
            }
        }
    }
    #endregion

    #region 钨阔剑：破甲刻痕
    /// <summary>
    /// 【钨钢刻刃】材质：致密钨钢。签名：①「破甲刻痕」同一目标连吃 3 刀后，
    /// 后续命中无视 8 点防御 ②命中钢屑火花密度全组最高 ③刻痕成型时金铁脆鸣提示
    /// </summary>
    internal class GsTungstenBroadsword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.TungstenBroadsword;

        protected override int HeldProjID => ModContent.ProjectileType<GsTungstenBroadswordHeld>();

        protected override string GsDescFallback =>
            "Reforged: notch the same target three times and every following cut ignores part of its armor";

        //钨绿灰色板
        internal static readonly Color TungstenBright = new(192, 216, 194); //钨亮绿灰
        internal static readonly Color TungstenMain = new(112, 138, 118);   //钨身绿灰
        internal static readonly Color TungstenHot = new(255, 182, 104);    //钢屑火橙
        internal static readonly Color TungstenDeep = new(24, 34, 28);      //钨沉暗影

        //预算账：base 1.05 + 破甲 8 点仅对高甲目标折 ~+4% 且要先垫 3 刀
        //→ 综合 DPS ~原版 109%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 钨阔剑手持：三拍重剑。owner 端按目标记刻痕数，同一目标连吃 3 刀后
    /// 后续命中破甲 8 点；挥砍钢屑火花密度全组最高。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsTungstenBroadswordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.TungstenBroadsword;
        protected override Color EdgeBright => GsTungstenBroadsword.TungstenBright;
        protected override Color BodyMain => GsTungstenBroadsword.TungstenMain;
        protected override Color HotAccent => GsTungstenBroadsword.TungstenHot;
        protected override Color DeepShadow => GsTungstenBroadsword.TungstenDeep;

        /// <summary>刻痕计数：whoAmI → (npc 类型, 累计刀数)。命中判定只在 owner 端跑，
        /// 本表只被本地玩家的挥砍读写；类型不符视为槽位复用，重新记数</summary>
        private static readonly Dictionary<int, (int npcType, int hits)> notches = [];

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                return new GsBroadBeat {
                    Raise = 9, Hold = 3, Slash = 5, Recover = 12,
                    RaiseBack = 2.25f, Follow = 1.2f, ReachScale = 1.16f, LeanAmp = 0.09f,
                    DamageMult = 1.34f, Hitstop = 2, LungeSpeed = 3.2f, SwingPitch = -0.42f,
                };
            }
            return new GsBroadBeat {
                Raise = 7, Hold = 2, Slash = 5, Recover = 10,
                RaiseBack = 1.95f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.055f,
                DamageMult = 1f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = stage == 0 ? -0.22f : -0.3f,
            };
        }

        /// <summary>刻痕满 3 后破甲 8 点</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (notches.TryGetValue(target.whoAmI, out (int npcType, int hits) n)
                && n.npcType == target.type && n.hits >= 3) {
                modifiers.ArmorPenetration += 8f;
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            PruneNotches();
            int hits = notches.TryGetValue(target.whoAmI, out (int npcType, int hits) n) && n.npcType == target.type
                ? n.hits + 1 : 1;
            notches[target.whoAmI] = (target.type, hits);
            //刻痕成型的瞬间给一记金铁脆鸣 + 一粒火橙星
            if (hits == 3 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.55f, Pitch = 0.35f }, target.Center);
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center, Vector2.Zero, Color.White, 0.9f)
                    ?.Configure(GsTungstenBroadsword.TungstenHot, 18, 0.1f, 1.1f);
            }
        }

        /// <summary>表过大时清掉已消亡/槽位复用的条目</summary>
        private static void PruneNotches() {
            if (notches.Count <= 64) {
                return;
            }
            List<int> dead = [];
            foreach (KeyValuePair<int, (int npcType, int hits)> kv in notches) {
                NPC npc = Main.npc[kv.Key];
                if (!npc.active || npc.type != kv.Value.npcType) {
                    dead.Add(kv.Key);
                }
            }
            foreach (int k in dead) {
                notches.Remove(k);
            }
        }

        /// <summary>挥砍钢屑密度全组最高：基类之上每帧再补一粒</summary>
        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (phase != PhaseSlash) {
                return;
            }
            Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.55f, 1f));
            PRTLoader.NewParticle<PRT_Spark>(at, sweepVel * Main.rand.NextFloat(4f, 9f)
                , Main.rand.NextBool() ? GsTungstenBroadsword.TungstenHot : GsTungstenBroadsword.TungstenBright
                , Main.rand.NextFloat(0.4f, 0.65f))?.Configure(true, Main.rand.Next(14, 22));
        }

        /// <summary>命中钢屑加量</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            Vector2 aimDir = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            for (int i = 0; i < 6; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.8) * Main.rand.NextFloat(4f, 10f);
                PRTLoader.NewParticle<PRT_Spark>(target.Center, vel
                    , Main.rand.NextBool() ? GsTungstenBroadsword.TungstenHot : GsTungstenBroadsword.TungstenBright
                    , Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(16, 26));
            }
        }
    }
    #endregion

    #region 金阔剑：鎏金
    /// <summary>
    /// 【鎏金华刃】材质：鎏金重剑。签名：①残影多一层且通体鎏金 ②命中掉金星屑、
    /// 偶尔一声金币脆响 ③终结拍在身怀金币时伤害 +8%（财气上刃的趣味设定）
    /// </summary>
    internal class GsGoldBroadsword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.GoldBroadsword;

        protected override int HeldProjID => ModContent.ProjectileType<GsGoldBroadswordHeld>();

        protected override string GsDescFallback =>
            "Reforged: gilded afterimages and coin-spark hits; the finisher strikes richer while gold sits in your purse";

        //鎏金色板
        internal static readonly Color GoldBright = new(255, 228, 142); //鎏金亮
        internal static readonly Color GoldMain = new(216, 162, 62);    //金身
        internal static readonly Color GoldHot = new(255, 202, 82);     //金芒
        internal static readonly Color GoldDeep = new(60, 42, 16);      //金沉暗影

        //预算账：base 1.06 + 终结拍条件 +8%（仅 1/3 出手、持金即触发，摊 ~+2.5%）
        //→ 综合 DPS ~原版 109%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;
    }

    /// <summary>
    /// 金阔剑手持：三拍华剑。残影比基类多一层、通体鎏金；命中掉 1~2 枚金星屑
    /// 并低概率金币脆响；终结拍身怀金币则 +8% 伤害。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsGoldBroadswordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.GoldBroadsword;
        protected override Color EdgeBright => GsGoldBroadsword.GoldBright;
        protected override Color BodyMain => GsGoldBroadsword.GoldMain;
        protected override Color HotAccent => GsGoldBroadsword.GoldHot;
        protected override Color DeepShadow => GsGoldBroadsword.GoldDeep;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                return new GsBroadBeat {
                    Raise = 8, Hold = 2, Slash = 5, Recover = 11,
                    RaiseBack = 2.2f, Follow = 1.25f, ReachScale = 1.15f, LeanAmp = 0.08f,
                    DamageMult = 1.32f, Hitstop = 2, LungeSpeed = 3.0f, SwingPitch = -0.3f,
                };
            }
            GsBroadBeat b = GsBroadBeat.Standard;
            b.Recover = 10;
            b.RaiseBack = 1.9f;
            b.LeanAmp = 0.05f;
            b.SwingPitch = stage == 0 ? -0.1f : -0.18f;
            return b;
        }

        //鎏金签名：残影比基类各多一层，金色由色板天然承担
        protected override int GhostCount => IsFinisher ? 4 : 3;

        /// <summary>终结拍身怀金币则 +8%（趣味设定；条件摊入方案侧包络注释）</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (IsFinisher && OwnerHasGold()) {
                modifiers.SourceDamage *= 1.08f;
            }
        }

        /// <summary>背包里是否有至少 1 枚金币（铂金币同算）</summary>
        private bool OwnerHasGold() {
            foreach (Item it in Owner.inventory) {
                if (it.stack > 0 && (it.type == ItemID.GoldCoin || it.type == ItemID.PlatinumCoin)) {
                    return true;
                }
            }
            return false;
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //金星屑 1~2 枚（纯演出）
            int stars = Main.rand.Next(1, 3);
            for (int i = 0; i < stars; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center + Main.rand.NextVector2Circular(12f, 12f)
                    , new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.5f, 1.8f)), Color.White
                    , Main.rand.NextFloat(0.7f, 1f))?.Configure(GsGoldBroadsword.GoldHot, Main.rand.Next(18, 26), 0.12f, 1.1f);
            }
            //低概率一声金币脆响
            if (Main.rand.NextBool(5)) {
                SoundEngine.PlaySound(SoundID.CoinPickup with { Volume = 0.5f, Pitch = Main.rand.NextFloat(-0.1f, 0.3f) }, target.Center);
            }
        }
    }
    #endregion

    #region 铂金阔剑：全重劈
    /// <summary>
    /// 【铂金压顶】材质：冷铸铂金。签名：①全组最重的三拍全重劈，音高最低顿帧最足
    /// ②终结拍 LungeSpeed 4 前压、击退 +50% ③终结斩切期贴刃双层冲击波涂抹
    /// </summary>
    internal class GsPlatinumBroadsword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.PlatinumBroadsword;

        protected override int HeldProjID => ModContent.ProjectileType<GsPlatinumBroadswordHeld>();

        protected override string GsDescFallback =>
            "Reforged: three full-weight cleaves; the finisher lunges with a double shockwave and crushing knockback";

        //冷白铂色板
        internal static readonly Color PlatBright = new(228, 238, 252); //铂亮白
        internal static readonly Color PlatMain = new(182, 196, 218);   //铂身冷灰
        internal static readonly Color PlatHot = new(172, 202, 255);    //压顶冷蓝
        internal static readonly Color PlatDeep = new(30, 36, 50);      //铂沉暗影

        //预算账：拍均伤 (1.05+1.05+1.42)/3≈1.17 × base 1.07 ≈ 1.25 倍/挥，
        //但连段总帧长于原版节奏 ~15% → 综合 DPS ~原版 110%；击退 +50% 不进 DPS
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.07f;

        public override void GsModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
            => knockback *= 1.5f;
    }

    /// <summary>
    /// 铂金阔剑手持：三拍全重劈（Raise 9~10/顿帧 2~3/音高全组最低/LeanAmp 大），
    /// 终结拍 LungeSpeed 4、DrawExtra 贴刃双层冲击波涂抹。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsPlatinumBroadswordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.PlatinumBroadsword;
        protected override Color EdgeBright => GsPlatinumBroadsword.PlatBright;
        protected override Color BodyMain => GsPlatinumBroadsword.PlatMain;
        protected override Color HotAccent => GsPlatinumBroadsword.PlatHot;
        protected override Color DeepShadow => GsPlatinumBroadsword.PlatDeep;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //压顶终结：全组最重的一击
                return new GsBroadBeat {
                    Raise = 10, Hold = 3, Slash = 6, Recover = 14,
                    RaiseBack = 2.45f, Follow = 1.3f, ReachScale = 1.22f, LeanAmp = 0.11f,
                    DamageMult = 1.42f, Hitstop = 3, LungeSpeed = 4f, SwingPitch = -0.65f,
                };
            }
            return new GsBroadBeat {
                Raise = stage == 0 ? 9 : 10, Hold = 3, Slash = 5, Recover = 12,
                RaiseBack = stage == 0 ? 2.1f : 2.2f, Follow = 1.05f, ReachScale = 1f
                , LeanAmp = stage == 0 ? 0.085f : 0.09f,
                DamageMult = 1.05f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = stage == 0 ? -0.52f : -0.58f,
            };
        }

        /// <summary>终结斩切期贴刃双层冲击波：随行程外扩、渐淡（确定性，不掷 Main.rand）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (!IsFinisher || CurrentPhase != PhaseSlash || slashProgress < 0.1f) {
                return;
            }
            Texture2D wave = CWRAsset.SemiCircularSmear?.Value;
            if (wave == null) {
                return;
            }
            float grow = 0.8f + slashProgress * 0.6f;
            float alpha = (1f - slashProgress * 0.6f) * 0.5f;
            Vector2 at = Hand + (mainAngle.ToRotationVector2() * mainReach * 0.7f) - Main.screenPosition;
            float rot = mainAngle + (swingDir * 0.35f);
            Color outer = GsPlatinumBroadsword.PlatBright * alpha;
            outer.A = 0;
            sb.Draw(wave, at, null, outer, rot, wave.Size() / 2f
                , new Vector2(0.6f, 0.34f) * grow * (mainReach / 118f), SpriteEffects.None, 0f);
            Color inner = GsPlatinumBroadsword.PlatHot * (alpha * 0.8f);
            inner.A = 0;
            sb.Draw(wave, at, null, inner, rot + (swingDir * 0.08f), wave.Size() / 2f
                , new Vector2(0.52f, 0.2f) * grow * (mainReach / 118f), SpriteEffects.None, 0f);
        }
    }
    #endregion
}
