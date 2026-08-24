using CalamityOverhaul.Common;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions.Deeds
{
    //刀縁名册：一铭一縁。条件一律"手持鬼切时做成的事"，
    //玩家提示写在拓本物品的 DeedHint 词条里，此处只写判据

    /// <summary>蜘蛛切：斩够蛛形，刀自己认得那股腥气</summary>
    internal sealed class DeedKumokiri : OniMeiDeed
    {
        public override string MeiKey => nameof(MeiKumokiri);
        public override OniMeiDeedChannel Channel => OniMeiDeedChannel.Kill;
        public override OniMeiDeedProgressKind ProgressKind => OniMeiDeedProgressKind.Count;
        public override int NeedCount => 30;
        public override int SortOrder => 47;

        public override int Test(in OniMeiDeedContext context)
            => OniMeiDeedTargets.IsArachnid(context.Npc) ? 1 : 0;
    }

    /// <summary>鬼丸：站着一动不动挨了一记还站着，刀替你动了那一次</summary>
    internal sealed class DeedOnimaru : OniMeiDeed
    {
        /// <summary>十秒</summary>
        private const int StillNeed = 600;

        public override string MeiKey => nameof(MeiOnimaru);
        public override OniMeiDeedChannel Channel => OniMeiDeedChannel.HeldTick;
        public override int SortOrder => 22;

        public override int Test(in OniMeiDeedContext context)
            => context.Tracker.StillTicks >= StillNeed && context.Tracker.HurtWhileStill ? 1 : 0;
    }

    /// <summary>雷切：雷暴天露天斩下一个首领，刃才认得那口雷</summary>
    internal sealed class DeedRaikiri : OniMeiDeed
    {
        public override string MeiKey => nameof(MeiRaikiri);
        public override OniMeiDeedChannel Channel => OniMeiDeedChannel.Kill;
        public override int SortOrder => 33;

        public override int Test(in OniMeiDeedContext context)
            => OniMeiDeedEnvironment.IsStorming
            && OniMeiDeedEnvironment.HasOpenSky(context.Player)
            && OniMeiDeedTargets.IsBossTier(context.Npc) ? 1 : 0;
    }

    /// <summary>鵺切：夜里从高处斩落一只飞的</summary>
    internal sealed class DeedNuekiri : OniMeiDeed
    {
        /// <summary>三十格</summary>
        private const float HeightNeed = 480f;

        public override string MeiKey => nameof(MeiNuekiri);
        public override OniMeiDeedChannel Channel => OniMeiDeedChannel.Kill;
        public override int SortOrder => 44;

        public override int Test(in OniMeiDeedContext context)
            => !Main.dayTime
            && OniMeiDeedEnvironment.IsFlyer(context.Npc)
            && OniMeiDeedEnvironment.HeightAboveGround(context.Player) >= HeightNeed ? 1 : 0;
    }

    /// <summary>紙樋：在里世界斩够面影纸型，手才知道纸该怎么裁</summary>
    internal sealed class DeedKamihi : OniMeiDeed
    {
        public override string MeiKey => nameof(MeiKamihi);
        public override OniMeiDeedChannel Channel => OniMeiDeedChannel.OmokageSever;
        public override OniMeiDeedProgressKind ProgressKind => OniMeiDeedProgressKind.Count;
        public override int NeedCount => 20;
        public override int SortOrder => 57;

        public override int Test(in OniMeiDeedContext context) => 1;
    }

    /// <summary>空樋：连续十五秒不落地，其间还砍着</summary>
    internal sealed class DeedSorahi : OniMeiDeed
    {
        /// <summary>十五秒</summary>
        private const int AirNeed = 900;
        private const int HitNeed = 5;

        public override string MeiKey => nameof(MeiSorahi);
        public override OniMeiDeedChannel Channel => OniMeiDeedChannel.HeldTick;
        public override int SortOrder => 58;

        public override int Test(in OniMeiDeedContext context)
            => context.Tracker.AirborneTicks >= AirNeed
            && context.Tracker.AirborneHits >= HitNeed ? 1 : 0;
    }

    /// <summary>鏡樋：用残心了结一只学人样的东西</summary>
    internal sealed class DeedKagamihi : OniMeiDeed
    {
        public override string MeiKey => nameof(MeiKagamihi);
        public override OniMeiDeedChannel Channel => OniMeiDeedChannel.Kill;
        public override int SortOrder => 59;

        public override int Test(in OniMeiDeedContext context)
            => context.KillSource == OniMeiDeedKillSource.Zanshin
            && OniMeiDeedTargets.IsCounterfeit(context.Npc) ? 1 : 0;
    }

    /// <summary>雨樋：雨里连着飞满十秒，樱瓣沾透了才留得住水</summary>
    internal sealed class DeedAmahi : OniMeiDeed
    {
        /// <summary>十秒</summary>
        private const int RainFlightNeed = 600;

        public override string MeiKey => nameof(MeiAmahi);
        public override OniMeiDeedChannel Channel => OniMeiDeedChannel.SakuraTick;
        public override int SortOrder => 66;

        public override int Test(in OniMeiDeedContext context)
            => context.Tracker.SakuraRainTicks >= RainFlightNeed ? 1 : 0;
    }

    /// <summary>綴樋：一次疾走串起六个不同主体</summary>
    internal sealed class DeedTsuzurihi : OniMeiDeed
    {
        private const int PierceNeed = 6;

        public override string MeiKey => nameof(MeiTsuzurihi);
        public override OniMeiDeedChannel Channel => OniMeiDeedChannel.DashPierce;
        public override int SortOrder => 67;

        public override int Test(in OniMeiDeedContext context)
            => context.Amount >= PierceNeed ? 1 : 0;
    }

    /// <summary>梵鐘：整场首领战灭世与终结都没动用，就这么把它敲下来</summary>
    internal sealed class DeedBonsho : OniMeiDeed
    {
        public override string MeiKey => nameof(MeiBonsho);
        public override OniMeiDeedChannel Channel => OniMeiDeedChannel.Kill;
        public override int SortOrder => 76;

        public override int Test(in OniMeiDeedContext context)
            => !context.Tracker.ExecutionUsedInFight
            && OniMeiDeedTargets.IsBossTier(context.Npc) ? 1 : 0;
    }

    /// <summary>般若：自己只剩一口气时把首领斩了</summary>
    internal sealed class DeedHannya : OniMeiDeed
    {
        private const float LifeRatioNeed = 0.10f;

        public override string MeiKey => nameof(MeiHannya);
        public override OniMeiDeedChannel Channel => OniMeiDeedChannel.Kill;
        public override int SortOrder => 77;

        public override int Test(in OniMeiDeedContext context) {
            Player player = context.Player;
            if (player.statLifeMax2 <= 0
                || player.statLife / (float)player.statLifeMax2 > LifeRatioNeed) {
                return 0;
            }
            return OniMeiDeedTargets.IsBossTier(context.Npc) ? 1 : 0;
        }
    }

    /// <summary>枯山水：钉在一处打满三十秒且一下没挨着</summary>
    internal sealed class DeedKaresansui : OniMeiDeed
    {
        /// <summary>三十秒</summary>
        private const int PlantedNeed = 1800;

        public override string MeiKey => nameof(MeiKaresansui);
        public override OniMeiDeedChannel Channel => OniMeiDeedChannel.HeldTick;
        public override int SortOrder => 79;

        public override int Test(in OniMeiDeedContext context)
            => context.Tracker.PlantedFightTicks >= PlantedNeed ? 1 : 0;
    }

    /// <summary>千手：用终结送走八种不同的首领，一手一位</summary>
    internal sealed class DeedSenju : OniMeiDeed
    {
        public override string MeiKey => nameof(MeiSenju);
        public override OniMeiDeedChannel Channel => OniMeiDeedChannel.Kill;
        public override OniMeiDeedProgressKind ProgressKind => OniMeiDeedProgressKind.Count;
        public override int NeedCount => 8;
        public override int SortOrder => 88;

        public override int Test(in OniMeiDeedContext context)
            => context.KillSource == OniMeiDeedKillSource.Finale
            && OniMeiDeedTargets.IsBossTier(context.Npc) ? 1 : 0;

        /// <summary>按种类去重：同一个首领刷八遍不算八位</summary>
        public override int MarkOf(in OniMeiDeedContext context) => context.Npc?.type ?? 0;
    }

    /// <summary>刀縁判据里反复用到的目标分类，集中在此以免各縁各写一份</summary>
    internal static class OniMeiDeedTargets
    {
        /// <summary>学人样的东西：各类宝箱怪与灾厄的仿造体</summary>
        internal static bool IsCounterfeit(NPC npc) {
            if (npc == null) {
                return false;
            }
            if (npc.type is NPCID.Mimic or NPCID.PresentMimic or NPCID.IceMimic
                or NPCID.BigMimicCorruption or NPCID.BigMimicCrimson
                or NPCID.BigMimicHallow or NPCID.BigMimicJungle) {
                return true;
            }
            return npc.type == CWRID.NPC_CalamitasClone;
        }

        /// <summary>蛛形：蛛巢三族 + 血爬虫 + 沙蝎，都是"八条腿贴墙爬"的那类</summary>
        internal static bool IsArachnid(NPC npc) => npc != null && npc.type switch {
            NPCID.WallCreeper or NPCID.WallCreeperWall => true,
            NPCID.BlackRecluse or NPCID.BlackRecluseWall => true,
            NPCID.JungleCreeper or NPCID.JungleCreeperWall => true,
            NPCID.BloodCrawler or NPCID.BloodCrawlerWall => true,
            NPCID.DesertScorpionWalk or NPCID.DesertScorpionWall => true,
            NPCID.DesertBeast => true,
            _ => false,
        };

        /// <summary>首领级：蠕虫归主体后判定</summary>
        internal static bool IsBossTier(NPC npc) {
            NPC root = OniMeiCombat.ResolveEffectRoot(npc);
            return root != null && NpcGroupHelper.IsBossTier(root);
        }
    }
}
