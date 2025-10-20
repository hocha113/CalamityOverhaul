namespace CalamityOverhaul.Content.MeleeModify
{
    public struct SwingDataStruct
    {
        /// <summary>
        /// 默认值为33
        /// </summary>
        public float starArg = 33;
        /// <summary>
        /// 默认值为4
        /// </summary>
        public float baseSwingSpeed = 4;
        /// <summary>
        /// 默认值为0.08f
        /// </summary>
        public float ler1_UpLengthSengs = 0.08f;
        /// <summary>
        /// 默认值为0.1f
        /// </summary>
        public float ler1_UpSpeedSengs = 0.1f;
        /// <summary>
        /// 默认值为0.012f
        /// </summary>
        public float ler1_UpSizeSengs = 0.012f;
        /// <summary>
        /// 默认值为0.01f
        /// </summary>
        public float ler2_DownLengthSengs = 0.01f;
        /// <summary>
        /// 默认值为0.1f
        /// </summary>
        public float ler2_DownSpeedSengs = 0.1f;
        /// <summary>
        /// 默认值为0
        /// </summary>
        public float ler2_DownSizeSengs = 0;
        /// <summary>
        /// 默认值为0
        /// </summary>
        public int minClampLength = 0;
        /// <summary>
        /// 默认值为0
        /// </summary>
        public int maxClampLength = 0;
        /// <summary>
        /// 默认值为0
        /// </summary>
        public int ler1Time = 0;
        /// <summary>
        /// 默认值为0
        /// </summary>
        public int maxSwingTime = 0;
        /// <summary>
        /// 默认值为1
        /// </summary>
        public float overSpeedUpSengs = 1;
        public SwingDataStruct() { }
    }
}
