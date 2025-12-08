using System.Collections.Generic;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    public class QuestNode
    {
        //节点唯一标识符
        public string ID;
        //节点名称
        public string Name;
        //节点描述
        public string Description;
        //节点在图表中的位置
        public Vector2 Position;
        //前置任务ID列表
        public List<string> ParentIDs = new();
        //子任务ID列表
        public List<string> ChildIDs = new();
        //是否已完成
        public bool IsCompleted;
        //是否已解锁
        public bool IsUnlocked;
        //图标纹理路径
        public string IconTexturePath;
    }
}
