using CalamityMod.Dusts;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal
{
    internal class SCalAltarScenario : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public override string Key => nameof(SCalAltarScenario);
        public string LocalizationCategory => "ADV";
        //设置场景默认使用硫磺火风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;
        //角色名称本地化
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText L1;
        public static LocalizedText L2;
        public static LocalizedText L3;
        public static int Count;
        void IWorldInfo.OnWorldLoad() {
            Count = -1;
        }
        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "硫火女巫");
            L1 = this.GetLocalization(nameof(L1), () => "现在还不是时候，你的前方还有另一个挡路的敌人");
            L2 = this.GetLocalization(nameof(L2), () => "去把你那堆机械玩具拼好，再把他打倒");
            L3 = this.GetLocalization(nameof(L3), () => "……怎么？需要我再说一遍吗？");
        }
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalsADV[3]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: false);

            string line = L1.Value;
            if (Count == 1) {
                line = L2.Value;
            }
            if (Count == 2) {
                line = L3.Value;
            }
            Add(Rolename1.Value, line);
        }
    }

    internal class ModifySCalAltar : TileOverride
    {
        public override int TargetID => CWRID.Tile_SCalAltar;
        public static void HitEffctByPlayer(Player player) {
            //硫磺火粒子爆发，使用Brimstone粒子
            for (int z = 0; z < 40; z++) {
                float angle = MathHelper.TwoPi * z / 40f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 12f);

                Dust dust = Dust.NewDustPerfect(
                    player.Center,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.8f, 3f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1.4f;
            }
            //硫磺火召唤音效 - 参考SCal的音效
            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.7f,
                Pitch = -0.4f
            }, player.Center);

            //低沉的地狱火焰音
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with {
                Volume = 0.6f,
                Pitch = -0.5f
            }, player.Center);
            PlayerDeathReason pd = PlayerDeathReason.ByCustomReason(CWRLocText.Instance.BloodAltar_Text3.ToNetworkText(player.name));
            player.Hurt(pd, 250, 0);
        }
        public static bool? Click() {
            if (EbnEffect.IsActive) {
                return false;//永恒燃烧的现在结局激活时无法使用灾厄祭坛
            }
            if (EbnPlayer.IsConquered(Main.LocalPlayer)) {
                if (!InWorldBossPhase.Downed29.Invoke()) {//没有击败星流巨械
                    if (++SCalAltarScenario.Count > 2) {
                        HitEffctByPlayer(Main.LocalPlayer);
                        return false;//无法使用灾厄祭坛
                    }
                    ScenarioManager.Reset<SCalAltarScenario>();
                    ScenarioManager.Start<SCalAltarScenario>();
                    return false;//无法使用灾厄祭坛
                }
            }
            return null;
        }
        public override bool? RightClick(int i, int j, Tile tile) => Click();
    }

    internal class ModifySCalAltarLarge : TileOverride
    {
        public override int TargetID => CWRID.Tile_SCalAltarLarge;
        public override bool? RightClick(int i, int j, Tile tile) => ModifySCalAltar.Click();
    }

    internal class ModifySupCalSystem : ModSystem
    {
        public override void PostUpdateNPCs() {
            if (NPC.AnyNPCs(CWRID.NPC_WITCH)) {
                int witch = NPC.FindFirstNPC(CWRID.NPC_WITCH);
                if (witch == -1) {
                    return;
                }
                bool hasEbn = false;
                foreach (var p in Main.ActivePlayers) {
                    //如果已经有人达成了永恒燃烧的现在结局，说明女巫已死，玩家替换女巫的位置
                    if (p.TryGetADVSave(out var save) && save.EternalBlazingNow) {
                        hasEbn = true;
                        p.Teleport(Main.npc[witch].Center, 999);
                    }
                }
                if (hasEbn) {
                    Main.npc[witch].active = false;
                    Main.npc[witch].netUpdate = true;
                }
            }
        }
    }

    internal class ModifyWITCH : ICWRLoader
    {
        void ICWRLoader.LoadData() {

            var type = CWRRef.GetNPC_WITCH_Type();
            if (type != null) {
                var meth = type.GetMethod("CanTownNPCSpawn", BindingFlags.Instance | BindingFlags.Public);
                VaultHook.Add(meth, OnCanTownNPCSpawnHook);
            }
        }
        private delegate bool OnCanTownNPCSpawnDelegate(object obj, int numTownNPCs);
        //临时钩子，后续改用前置实现
        private static bool OnCanTownNPCSpawnHook(OnCanTownNPCSpawnDelegate orig, object obj, int numTownNPCs) {
            if (Main.player.Any(EbnPlayer.OnEbn)) {//如果有玩家达成永恒燃烧的现在结局
                return false;//女巫不生成
            }
            return orig.Invoke(obj, numTownNPCs);
        }
    }

    internal class ModifySupCalNPC : NPCOverride, ICWRLoader
    {
        public override int TargetID => CWRID.NPC_SupremeCalamitas;

        private static bool originallyDownedCalamitas = false;

        private delegate void BossHeadSlotDelegate(ModNPC modNPC, ref int index);

        void ICWRLoader.LoadData() {
            var type = CWRRef.GetNPC_SupCal_Type();
            if (type != null) {
                var meth = type.GetMethod("BossHeadSlot", BindingFlags.Instance | BindingFlags.Public);
                VaultHook.Add(meth, OnBossHeadSlotHook);
            }
        }
        
        //临时钩子，后续改用前置实现
        private static void OnBossHeadSlotHook(BossHeadSlotDelegate orig, ModNPC modNPC, ref int index) {
            originallyDownedCalamitas = CWRRef.GetDownedCalamitas();
            if (EbnPlayer.IsConquered(Main.player[modNPC.NPC.target])) {
                CWRRef.SetDownedCalamitas(true);//如果被攻略了的话就无条件摘掉兜帽
            }
            orig.Invoke(modNPC, ref index);
            CWRRef.SetDownedCalamitas(originallyDownedCalamitas);
        }

        public override bool AI() {
            if (CWRRef.GetBossRushActive()) {
                return true;//Boss Rush模式下不进行任何修改
            }
            foreach (var p in Main.ActivePlayers) {
                //如果已经有人达成了永恒燃烧的现在结局，说明女巫已死，玩家替换女巫的位置
                if (p.TryGetADVSave(out var save) && save.EternalBlazingNow) {
                    p.Teleport(npc.Center, 999);
                    npc.active = false;
                    npc.netUpdate = true;
                    return false;
                }
            }
            return base.AI();
        }

        public override void PostAI() {
            if (EbnEffect.IsActive) {
                if (CWRRef.GetSupCalGiveUpCounter(npc) < 120) {
                    CWRRef.SetSupCalGiveUpCounter(npc, 120);
                }
            }
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            originallyDownedCalamitas = CWRRef.GetDownedCalamitas();
            if (EbnPlayer.IsConquered(Main.player[npc.target])) {
                CWRRef.SetDownedCalamitas(true);//如果被攻略了的话就无条件摘掉兜帽
            }
            return base.Draw(spriteBatch, screenPos, drawColor);
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            CWRRef.SetDownedCalamitas(originallyDownedCalamitas);
            return base.PostDraw(spriteBatch, screenPos, drawColor);
        }
    }
}
