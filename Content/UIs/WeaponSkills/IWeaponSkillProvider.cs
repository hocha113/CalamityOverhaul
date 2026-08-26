using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.UIs.WeaponSkills
{
    /// <summary>
    /// 技能槽单帧快照,<see cref="WeaponSkillHud"/> 每帧向武器索取
    /// </summary>
    internal struct WeaponSkillView
    {
        /// <summary>技能名,悬停面板题行</summary>
        public string Name;
        /// <summary>一句效果说明,悬停面板正文;可含换行</summary>
        public string Desc;
        /// <summary>消耗行(如魔力消耗),null 或空则不画</summary>
        public string CostLine;
        /// <summary>槽位身份亮色:缘环/辉光/悬停面板同源</summary>
        public Color Accent;
        /// <summary>冷却剩余帧,0=不在冷却</summary>
        public int CooldownLeft;
        /// <summary>冷却总帧,0=无冷却机制</summary>
        public int CooldownTotal;
        /// <summary>技能弹幕存活中,按钮亮"施放中"态</summary>
        public bool Alive;
        /// <summary>当前可触发</summary>
        public bool Ready;
    }

    /// <summary>
    /// 手持技能按钮 HUD 的武器侧契约,由武器 ModItem 实现
    /// <br/>手持实现者时 <see cref="WeaponSkillHud"/> 在屏幕左下角亮出两枚技能按钮
    /// <br/>布局与交互归 HUD,图标与色板归武器,各武器自行设计按钮面貌
    /// </summary>
    internal interface IWeaponSkillProvider
    {
        /// <summary>取槽位快照,slot 恒为 0(左)或 1(右)</summary>
        WeaponSkillView GetWeaponSkill(int slot, Player player);

        /// <summary>触发技能,仅本地客户端点击时调用;返回是否成功施放</summary>
        bool TriggerWeaponSkill(int slot, Player player);

        /// <summary>
        /// 绘制槽位图标,center/radius 为 UI 空间按钮心与图标半径
        /// <br/>lit:0=冷却暗态 1=就绪亮态;time 秒;alpha 整体透明度
        /// </summary>
        void DrawWeaponSkillIcon(SpriteBatch sb, int slot, Vector2 center, float radius, float lit, float time, float alpha);
    }
}
