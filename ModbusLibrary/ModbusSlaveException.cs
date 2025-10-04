using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyModbusLibrary
{
    /// <summary>
    /// 表示从 Modbus 从站设备返回了一个异常响应。
    /// 继承自 IOException，因为它本质上是一种 I/O 通信错误。
    /// </summary>
    public class ModbusSlaveException : System.IO.IOException
    {
        /// <summary>
        /// 获取从站返回的 Modbus 异常码。
        /// </summary>
        public byte ExceptionCode { get; }

        /// <summary>
        /// 根据 Modbus 异常码初始化一个新的 ModbusSlaveException 实例。
        /// </summary>
        /// <param name="exceptionCode">从站返回的1字节异常码。</param>
        public ModbusSlaveException(byte exceptionCode)
            : base(GetErrorMessage(exceptionCode)) // 调用基类(IOException)的构造函数，并传入一个友好的错误消息
        {
            this.ExceptionCode = exceptionCode;
        }

        // 为了保持最佳实践，我们也添加标准的构造函数
        public ModbusSlaveException(string message) : base(message) { }
        public ModbusSlaveException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// 一个私有辅助方法，用于将异常码转换为可读的错误消息。
        /// </summary>
        private static string GetErrorMessage(byte exceptionCode)
        {
            string description;
            switch (exceptionCode)
            {
                case 0x01:
                    description = "非法功能码: 从站不支持该功能码。";
                    break;
                case 0x02:
                    description = "非法数据地址: 请求的数据地址在从站中不存在或不可访问。";
                    break;
                case 0x03:
                    description = "非法数据值: 请求中包含了一个对于从站来说无效的数据值。";
                    break;
                case 0x04:
                    description = "从站设备故障: 从站在尝试执行请求时发生了不可恢复的错误。";
                    break;
                default:
                    description = "发生未知从站异常。";
                    break;
            }
            return $"从站返回异常，错误码 {exceptionCode:X2}: {description}";
        }
    }
}
