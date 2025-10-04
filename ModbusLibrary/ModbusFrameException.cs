using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusLibrary
{
    /// <summary>
    /// 表示接收到的 Modbus 报文在格式、长度或校验上存在问题。
    /// 继承自 InvalidDataException，因为它代表了“收到的数据格式无效”。
    /// </summary>
    public class ModbusFrameException : System.IO.IOException
    {
        // 我们只需要提供标准的构造函数，让它可以像普通异常一样被创建和使用
        public ModbusFrameException() { }
        public ModbusFrameException(string message) : base(message) { }
        public ModbusFrameException(string message, Exception innerException) : base(message, innerException) { }
    }
}

