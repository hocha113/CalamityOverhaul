namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>
    /// 对话预处理参数，用于在对话显示前修改内容
    /// </summary>
    public class DialoguePreProcessArgs
    {
        /// <summary>
        /// 说话者名称
        /// </summary>
        public string Speaker;

        /// <summary>
        /// 对话内容
        /// </summary>
        public string Content;

        /// <summary>
        /// 当前对话在序列中的索引（从0开始）
        /// </summary>
        public int Index;

        /// <summary>
        /// 对话序列的总数
        /// </summary>
        public int Total;
    }
}
