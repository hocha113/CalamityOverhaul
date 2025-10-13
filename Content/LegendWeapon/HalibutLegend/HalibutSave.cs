using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutSave : ModPlayer
    {
        /// <summary>
        /// 大比目鱼UI技能栏（用于保存加载）
        /// </summary>
        public readonly List<SkillSlot> halibutUISkillSlots = [];
        /// <summary>
        /// 所有眼睛列表（用于保存加载）
        /// </summary>
        internal readonly List<SeaEyeButton> eyes = [];
        /// <summary>
        /// 按激活顺序排列的眼睛列表（用于保存加载）
        /// </summary>
        public readonly List<SeaEyeButton> activationSequence = [];//按激活顺序排列
        /// <summary>
        /// 大比目鱼技能实例（用于保存加载）
        /// </summary>
        public FishSkill FishSkill;
        /// <summary>
        /// 深渊复苏系统存档数据
        /// </summary>
        public ResurrectionSystem ResurrectionSaveData = new();
        /// <summary>
        /// 设置静态默认值
        /// </summary>
        public override void SetStaticDefaults() {
            for (int i = 0; i < DomainUI.MaxEyes; i++) {
                float angle = i / (float)DomainUI.MaxEyes * MathHelper.TwoPi - MathHelper.PiOver2;
                eyes.Add(new SeaEyeButton(i, angle));
            }
        }
        //首先需要明确的一点是，玩家是单实例的，UI也是单实例的，所以在保存加载中不要使用静态数据，因为需要每个玩家之间的数据独立
        public override void SaveData(TagCompound tag) {
            IList<TagCompound> list = [];
            foreach (var slot in halibutUISkillSlots) {
                if (slot.FishSkill == null) {
                    continue;
                }
                TagCompound skillTag = [];
                skillTag["Name"] = slot.FishSkill.FullName;
                slot.FishSkill.SaveData(tag);
                list.Add(skillTag);
            }
            tag["FishSkills"] = list;

            if (Player.TryGetOverride<HalibutPlayer>(out var halibutPlayer) && halibutPlayer.SkillID > 0) {
                var skill = FishSkill.IDToInstance.GetValueOrDefault(halibutPlayer.SkillID);
                if (skill != null) {
                    tag["HalibutTargetSkillName"] = skill.FullName;
                }
            }

            //保存激活的眼睛索引列表（按激活顺序）
            List<int> activeEyeIndices = [];
            foreach (var eye in activationSequence) {
                if (eye.IsActive) {
                    activeEyeIndices.Add(eye.Index);
                }
            }
            tag["ActiveEyeIndices"] = activeEyeIndices;

            //保存深渊复苏系统数据
            if (Player.TryGetOverride<HalibutPlayer>(out var hPlayer)) {
                tag["ResurrectionSystem"] = hPlayer.ResurrectionSystem.SaveData();
            }
        }

        public override void LoadData(TagCompound tag) {
            if (tag.TryGet<IList<TagCompound>>("FishSkills", out var list)) {
                halibutUISkillSlots.Clear();
                foreach (var skillTag in list) {
                    if (!skillTag.TryGet<string>("Name", out var name) ||
                        !FishSkill.NameToInstance.TryGetValue(name, out var fishSkill)) {
                        continue;
                    }
                    fishSkill.LoadData(skillTag);
                    halibutUISkillSlots.Add(HalibutUIPanel.AddSkillSlot(fishSkill, 1f));
                }

                if (tag.TryGet<string>("HalibutTargetSkillName", out var skillName)) {
                    FishSkill = FishSkill.NameToInstance.GetValueOrDefault(skillName);
                }
            }

            //读取激活的眼睛索引列表
            if (tag.TryGet<List<int>>("ActiveEyeIndices", out var activeIndices)) {
                //清空当前激活序列
                activationSequence.Clear();
                if (eyes.Count == 0) {
                    for (int i = 0; i < DomainUI.MaxEyes; i++) {
                        float angle = i / (float)DomainUI.MaxEyes * MathHelper.TwoPi - MathHelper.PiOver2;
                        eyes.Add(new SeaEyeButton(i, angle));
                    }
                }
                //重置所有眼睛状态
                foreach (var eye in eyes) {
                    eye.IsActive = false;
                    eye.LayerNumber = null;
                }
                //按保存的顺序重新激活眼睛
                foreach (int index in activeIndices) {
                    if (index >= 0 && index < eyes.Count) {
                        var eye = eyes[index];
                        eye.IsActive = true;
                        activationSequence.Add(eye);
                        eye.LayerNumber = activationSequence.Count;
                    }
                }
                //更新圆环
                DomainUI.Instance.UpdateRings(activationSequence.Count);
                DomainUI.Instance.lastActiveEyeCount = activationSequence.Count;
            }

            //加载深渊复苏系统数据
            if (tag.TryGet<TagCompound>("ResurrectionSystem", out var resurrectionTag)) {
                if (Player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    halibutPlayer.ResurrectionSystem.LoadData(resurrectionTag);
                }
            }
        }
    }
}
