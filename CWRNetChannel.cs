using System.IO;

//放在根命名空间：全模组的功能命名空间都是 CalamityOverhaul.* 的子空间，无需额外 using 即可派生信道
namespace CalamityOverhaul
{
    /// <summary>
    /// 去中心化网络信道：一个子类即一条消息通道，加载期由 <see cref="CWRNetWork"/> 自动发现，
    /// 按类型全名排序分配编号（tML 联机强制两端模组哈希一致，同一二进制保证两端编号一致）<br/>
    /// 新增联网功能只需在功能自己的文件里派生本类并实现 <see cref="Receive"/>，不再触碰任何中心文件<br/>
    /// 发包统一走 <see cref="CWRNetWork.GetPacket{T}"/>，信道编号已预写好
    /// </summary>
    public abstract class CWRNetChannel
    {
        /// <summary>加载期分配的信道编号，运行期只读</summary>
        internal byte ID { get; set; }

        /// <summary>
        /// 收包入口：收到本信道消息的端点被调用，语义与旧 NetHandle 一致，
        /// 内部自行判定 isServer/isClient 与转播<br/>
        /// 实例是全局单例，禁止在信道对象上携带每玩家状态
        /// </summary>
        public abstract void Receive(BinaryReader reader, int whoAmI);
    }
}
