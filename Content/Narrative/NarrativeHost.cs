using CalamityOverhaul.Content.Narrative.Presentation.Skins.Brimstone;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Draedon;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Onikiri;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Sea;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.SHPC;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.StarStream;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Sulfsea;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Tzeentch;
using CalamityOverhaul.Content.Scenarios.Draedon.Quest;
using CalamityOverhaul.Content.Scenarios.Draedon.Tzeentch;
using CalamityOverhaul.Content.Scenarios.Helen;
using CalamityOverhaul.Content.Scenarios.OldDuke;
using CalamityOverhaul.Content.Scenarios.Himayo;
using CalamityOverhaul.Content.Scenarios.OldDuke.Campsites;
using CalamityOverhaul.Content.Scenarios.Shepel;
using CalamityOverhaul.Content.Scenarios.SupCal;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using InnoVault.Cinematics;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Portraits;
using InnoVault.Narrative.Presentation;
using InnoVault.Narrative.Progress;
using InnoVault.Narrative.Runtime;
using InnoVault.Narrative.Services;
using InnoVault.Narrative.Styling;
using Terraria;
using SupCalFirstMet = CalamityOverhaul.Content.Scenarios.SupCal.FirstMetSupCal;

namespace CalamityOverhaul.Content.Narrative
{
    internal sealed class NarrativeHost : ICWRLoader
    {
        void ICWRLoader.LoadData() {
            NarrativeViews.UseDefaultDialogueView = false;
            NarrativeViews.UseDefaultChoiceView = false;
            NarrativeViews.UseDefaultPopupView = false;

            NarrativeServices.Progress = new StoryProgressProxy();
            NarrativeServices.RewardGrant = new RewardGrantService();
            NarrativeServices.Sync = new NarrativeSyncService();
        }

        void ICWRLoader.SetupData() {
            RegisterSkins();
            RegisterPortraits();
            RegisterBlockers();
        }

        void ICWRLoader.UnLoadData() {
            NarrativeServices.Progress = new MemoryNarrativeProgressStore();
            NarrativeServices.RewardGrant = null;
            NarrativeServices.Sync = null;
        }

        private static void RegisterSkins() {
            StyleRegistry.RegisterSet(NarrativeIds.Default, new SeaDialogueSkin(), new SeaChoiceSkin(), new SeaPopupSkin());
            StyleRegistry.RegisterSet(NarrativeIds.Sea, new SeaDialogueSkin(), new SeaChoiceSkin(), new SeaPopupSkin());
            StyleRegistry.RegisterSet(NarrativeIds.Sulfsea, new SulfseaDialogueSkin(), new SulfseaChoiceSkin(), new SulfseaPopupSkin());
            StyleRegistry.RegisterSet(NarrativeIds.Brimstone, new BrimstoneDialogueSkin(), new BrimstoneChoiceSkin(), new BrimstonePopupSkin());
            StyleRegistry.RegisterSet(NarrativeIds.Draedon, new DraedonDialogueSkin(), new DraedonChoiceSkin(), new DraedonPopupSkin());
            StyleRegistry.RegisterSet(NarrativeIds.StarStream, new StarStreamDialogueSkin(), new StarStreamChoiceSkin(), new StarStreamPopupSkin());
            StyleRegistry.RegisterSet(NarrativeIds.SHPC, new SHPCDialogueSkin(), new SHPCChoiceSkin(), new SHPCPopupSkin());
            StyleRegistry.RegisterSet(NarrativeIds.Tzeentch, new TzeentchDialogueSkin(), new TzeentchChoiceSkin(), new TzeentchPopupSkin());
            StyleRegistry.RegisterSet(NarrativeIds.Onikiri, new OnikiriDialogueSkin(), new OnikiriChoiceSkin(), new OnikiriPopupSkin());
        }

        private static void RegisterPortraits() {
            PortraitRegistry.Register(NarrativeIds.System)
                .Name(() => string.Empty);

            PortraitRegistry.Register(NarrativeIds.OldDuke)
                .Name(() => FirstMetOldDuke.OldDukeName.Value)
                .Portrait(() => OldDukeCampsite.OldDuke, () => OldDukeCampsite.PortraitRec)
                .AsSilhouette();

            PortraitRegistry.Register(NarrativeIds.HelenUnknown)
                .Name(() => FirstMet.Rolename1.Value)
                .Portrait(() => ADVAsset.HelenADV)
                .AsSilhouette();

            PortraitRegistry.Register(NarrativeIds.Helen)
                .Name(() => FirstMet.Rolename2.Value)
                .Portrait(() => ADVAsset.HelenADV)
                .Expression(NarrativeIds.Doubt, () => ADVAsset.Helen_doubtADV)
                .Expression(NarrativeIds.Enjoy, () => ADVAsset.Helen_enjoyADV)
                .Expression(NarrativeIds.Serious, () => ADVAsset.Helen2ADV)
                .Expression(NarrativeIds.Solemn, () => ADVAsset.Helen_solemnADV)
                .Expression(NarrativeIds.Amazed, () => ADVAsset.Helen_amazeADV)
                .Expression(NarrativeIds.Wrath, () => ADVAsset.Helen_wrathADV)
                .Expression(NarrativeIds.Silence, () => ADVAsset.Helen_silenceADV)
                .Expression(NarrativeIds.SlightAnnoyed, () => ADVAsset.Helen_slightAnnoyedADV)
                .Expression(NarrativeIds.Naughty, () => ADVAsset.Helen_naughtyADV)
                .Expression(NarrativeIds.Naughty2, () => ADVAsset.Helen_naughty2ADV)
                .Expression(NarrativeIds.Enjoy2, () => ADVAsset.Helen_enjoy2ADV)
                .Expression(NarrativeIds.Enjoy3, () => ADVAsset.Helen_enjoy3ADV)
                .Expression(NarrativeIds.Stern, () => ADVAsset.Helen_seriousADV)
                .Expression(NarrativeIds.Serious2, () => ADVAsset.Helen_serious2ADV);

            PortraitRegistry.Register(NarrativeIds.SupCalUnknown)
                .Name(() => SupCalFirstMet.Rolename1.Value)
                .Portrait(() => ADVAsset.SupCalsADV[4])
                .AsSilhouette();

            PortraitRegistry.Register(NarrativeIds.SupCalShadow)
                .Name(() => EternalBlazingNow.Rolename2.Value)
                .Portrait(() => ADVAsset.SupCalsADV[4])
                .AsSilhouette()
                .Expression(NarrativeIds.BeTo, () => ADVAsset.SupCalsADV[3]);

            PortraitRegistry.Register(NarrativeIds.SupCalFarewell)
                .Name(() => EternalBlazingNow.Rolename2.Value);

            PortraitRegistry.Register(NarrativeIds.SupCal)
                .Name(() => SupCalFirstMet.Rolename2.Value)
                .Portrait(() => ADVAsset.SupCalsADV[0])
                .Expression(NarrativeIds.CloseEye, () => ADVAsset.SupCalsADV[4])
                .Expression(NarrativeIds.BeTo, () => ADVAsset.SupCalsADV[3])
                .Expression(NarrativeIds.Despise, () => ADVAsset.SupCalsADV[5])
                .Expression(NarrativeIds.Shock, () => ADVAsset.SupCalsADV[2])
                .Expression(NarrativeIds.Smile, () => ADVAsset.SupCalsADV[1])
                .Expression(NarrativeIds.Sigh, () => ADVAsset.SupCalsADV[5]);

            PortraitRegistry.Register(NarrativeIds.DraedonSpeaker)
                .Name(() => DraedonQuestLine.QuestCategory.Value)
                .Portrait(() => ADVAsset.Draedon2ADV)
                .Expression(NarrativeIds.Red, () => ADVAsset.Draedon2RedADV)
                .Expression(NarrativeIds.Alt, () => ADVAsset.DraedonADV);

            PortraitRegistry.Register(CharacterId.ForMod(NarrativeIds.ModName, "SHPC"))
                .Name(() => FirstMetShepel.SpeakerName.Value);

            PortraitRegistry.Register(NarrativeIds.Shepel)
                .Name(() => FirstMetShepel.SpeakerName.Value);

            PortraitRegistry.Register(CharacterId.ForMod(NarrativeIds.ModName, "HalibutPlayer"))
                .Name(() => SupCalDefeat.Rolename2.Value.Replace("[Name]", Main.LocalPlayer.name))
                .Portrait(() => ADVAsset.HelenADV);

            PortraitRegistry.Register(CharacterId.ForMod(NarrativeIds.ModName, "Tzeentch"))
                .Name(() => FirstMetTzeentch.Rolename?.Value ?? "?????????????????????")
                .Portrait(() => ADVAsset.Tzeentch)
                .AsSilhouette();

            //鬼切伴侣角色,立绘待接入,当前仅显示名(演示场景 OnikiriStyleDemo 使用)
            PortraitRegistry.Register(NarrativeIds.Mayo)
                .Name(() => OnikiriStyleDemo.MayoName.Value);
        }

        private static void RegisterBlockers() {
            NarrativeScheduler.RegisterBlocker(() => CWRWorld.HasBoss);
            NarrativeScheduler.RegisterBlocker(() => CWRWorld.BossRush);
            NarrativeScheduler.RegisterBlocker(() => CutsceneDirector.IsPlaying);
        }
    }
}
