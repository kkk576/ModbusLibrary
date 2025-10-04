using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusLibrary
{

    /// <summary>
    /// 负责构建 Modbus 请求帧的内部工具类。
    /// </summary>
    internal class FrameBuilder
    {
        /// <summary>
        /// 这是一个私有辅助方法，用于将 C# 的 ushort (通常是小端序) 转换成大端序的字节数组。
        /// </summary>
        private static byte[] GetBigEndianBytes(ushort value)
        {
            // 使用 BitConverter 将 ushort 转换为字节数组
            byte[] bytes = BitConverter.GetBytes(value);

            // 如果当前系统是小端序，则需要反转字节数组以满足大端序要求
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }

        /// <summary>
        /// 构建 "读取线圈" (功能码 0x01) 的请求帧。
        /// </summary>
        /// <param name="slaveId">从站地址 (1-247)</param>
        /// <param name="startAddress">起始线圈地址</param>
        /// <param name="quantity">要读取的线圈数量 (1-2000)</param>
        /// <returns>完整的、包含 CRC 的请求报文字节数组</returns>
        internal static byte[] BuildReadCoilsRequest(byte slaveId, ushort startAddress, ushort quantity)
        {
            // 1. 创建一个列表用于动态组装字节
            var frame = new List<byte>();

            // 2. 添加从站地址 (1字节)
            frame.Add(slaveId);

            // 3. 添加功能码 0x01 (1字节)
            frame.Add(0x01);

            // 4. 添加起始地址 (2字节)，并使用辅助方法确保是大端序
            frame.AddRange(GetBigEndianBytes(startAddress));

            // 5. 添加线圈数量 (2字节)，同样确保是大端序
            frame.AddRange(GetBigEndianBytes(quantity));

            // 6. 此时报文数据区已准备好，调用我们之前写的 ModbusCrc 类来计算 CRC
            //    输入参数是当前 frame 列表中的所有字节
            byte[] crc = ModbusCrc.Calculate(frame.ToArray());

            // 7. 将 CRC(2字节, 小端序) 附加到报文末尾
            frame.AddRange(crc);

            // 8. 返回最终完整的字节数组
            return frame.ToArray();
        }

        /// <summary>
        /// 构建 "读取离散输入" (功能码 0x02) 的请求帧。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="startAddress">起始输入地址</param>
        /// <param name="quantity">要读取的输入数量</param>
        /// <returns>完整的、包含 CRC 的请求报文字节数组</returns>
        internal static byte[] BuildReadDiscreteInputsRequest(byte slaveId, ushort startAddress, ushort quantity)
        {
            var frame = new List<byte>();

            frame.Add(slaveId);
            frame.Add(0x02); // 功能码 0x02 (唯一的改动)

            frame.AddRange(GetBigEndianBytes(startAddress));
            frame.AddRange(GetBigEndianBytes(quantity));

            byte[] crc = ModbusCrc.Calculate(frame.ToArray());
            frame.AddRange(crc);

            return frame.ToArray();
        }

        /// <summary>
        /// 构建 "读取保持寄存器" (功能码 0x03) 的请求帧。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="startAddress">起始寄存器地址</param>
        /// <param name="quantity">要读取的寄存器数量</param>
        /// <returns>完整的、包含 CRC 的请求报文字节数组</returns>
        internal static byte[] BuildReadHoldingRegistersRequest(byte slaveId, ushort startAddress, ushort quantity)
        {
            var frame = new List<byte>();

            frame.Add(slaveId);
            frame.Add(0x03); // 功能码 0x03

            frame.AddRange(GetBigEndianBytes(startAddress));
            frame.AddRange(GetBigEndianBytes(quantity));

            byte[] crc = ModbusCrc.Calculate(frame.ToArray());
            frame.AddRange(crc);

            return frame.ToArray();
        }

        /// <summary>
        /// 构建 "读取输入寄存器" (功能码 0x04) 的请求帧。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="startAddress">起始输入寄存器地址</param>
        /// <param name="quantity">要读取的寄存器数量</param>
        /// <returns>完整的、包含 CRC 的请求报文字节数组</returns>
        internal static byte[] BuildReadInputRegistersRequest(byte slaveId, ushort startAddress, ushort quantity)
        {
            var frame = new List<byte>();

            frame.Add(slaveId);
            frame.Add(0x04); // 功能码 0x04 (唯一的改动)

            frame.AddRange(GetBigEndianBytes(startAddress));
            frame.AddRange(GetBigEndianBytes(quantity));

            byte[] crc = ModbusCrc.Calculate(frame.ToArray());
            frame.AddRange(crc);

            return frame.ToArray();
        }

        /// <summary>
        /// 构建 "写入单个线圈" (功能码 0x05) 的请求帧。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="coilAddress">要写入的线圈地址</param>
        /// <param name="isOn">要写入的状态，true 为 ON, false 为 OFF</param>
        /// <returns>完整的、包含 CRC 的请求报文字节数组</returns>
        internal static byte[] BuildWriteSingleCoilRequest(byte slaveId, ushort coilAddress, bool isOn)
        {
            var frame = new List<byte>();

            frame.Add(slaveId);
            frame.Add(0x05); // 功能码 0x05

            // 数据区包含地址和特殊值，都需要是大端序
            frame.AddRange(GetBigEndianBytes(coilAddress));

            // 根据布尔值参数，决定要写入的 16 位数值
            // 这是 C# 的三元运算符，是 if/else 的一种紧凑写法
            ushort valueToWrite = isOn ? (ushort)0xFF00 : (ushort)0x0000;

            frame.AddRange(GetBigEndianBytes(valueToWrite));

            // 计算并添加 CRC
            byte[] crc = ModbusCrc.Calculate(frame.ToArray());
            frame.AddRange(crc);

            return frame.ToArray();
        }

        /// <summary>
        /// 构建 "写入单个寄存器" (功能码 0x06) 的请求帧。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="registerAddress">要写入的寄存器地址</param>
        /// <param name="valueToWrite">要写入的16位值</param>
        /// <returns>完整的、包含 CRC 的请求报文字节数组</returns>
        internal static byte[] BuildWriteSingleRegisterRequest(byte slaveId, ushort registerAddress, ushort valueToWrite)
        {
            var frame = new List<byte>();

            frame.Add(slaveId);
            frame.Add(0x06); // 功能码 0x06

            // 数据区包含地址和值，都需要是大端序
            frame.AddRange(GetBigEndianBytes(registerAddress));
            frame.AddRange(GetBigEndianBytes(valueToWrite));

            // 计算并添加 CRC
            byte[] crc = ModbusCrc.Calculate(frame.ToArray());
            frame.AddRange(crc);

            return frame.ToArray();
        }

        /// <summary>
        /// 构建 "写入多个寄存器" (功能码 0x10) 的请求帧。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="startAddress">起始寄存器地址</param>
        /// <param name="valuesToWrite">要写入的 ushort 数组</param>
        /// <returns>完整的、包含 CRC 的请求报文字节数组</returns>
        internal static byte[] BuildWriteMultipleRegistersRequest(byte slaveId, ushort startAddress, ushort[] valuesToWrite)
        {
            var frame = new List<byte>();

            frame.Add(slaveId);
            frame.Add(0x10); // 功能码 0x10

            // 添加起始地址 (2字节, 大端序)
            frame.AddRange(GetBigEndianBytes(startAddress));

            // 添加要写入的寄存器数量 (2字节, 大端序)
            ushort quantity = (ushort)valuesToWrite.Length;
            frame.AddRange(GetBigEndianBytes(quantity));

            // 添加字节计数 (1字节)
            // 它的值是 寄存器数量 * 2
            byte byteCount = (byte)(quantity * 2);
            frame.Add(byteCount);

            // 遍历要写入的数值数组，将每个值转换为大端序字节并添加到帧中
            foreach (ushort value in valuesToWrite)
            {
                frame.AddRange(GetBigEndianBytes(value));
            }

            // 计算并添加 CRC
            byte[] crc = ModbusCrc.Calculate(frame.ToArray());
            frame.AddRange(crc);

            return frame.ToArray();
        }

        /// <summary>
        /// 构建 "写入多个线圈" (功能码 0x0F) 的请求帧。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="startAddress">起始线圈地址</param>
        /// <param name="valuesToWrite">要写入的 bool 数组 (true=ON, false=OFF)</param>
        /// <returns>完整的、包含 CRC 的请求报文字节数组</returns>
        internal static byte[] BuildWriteMultipleCoilsRequest(byte slaveId, ushort startAddress, bool[] valuesToWrite)
        {
            var frame = new List<byte>();

            frame.Add(slaveId);
            frame.Add(0x0F); // 功能码 0x0F

            frame.AddRange(GetBigEndianBytes(startAddress));

            ushort quantity = (ushort)valuesToWrite.Length;
            frame.AddRange(GetBigEndianBytes(quantity));

            // 使用我们刚讨论过的公式，计算需要多少字节来存放这些线圈状态
            byte byteCount = (byte)((quantity + 7) / 8);
            frame.Add(byteCount);

            // --- 数据打包逻辑 ---
            var dataBytes = new byte[byteCount];
            for (int i = 0; i < quantity; i++)
            {
                // 如果当前线圈状态为 ON (true)
                if (valuesToWrite[i])
                {
                    // 计算这个线圈应该在哪个字节 (byteIndex) 的哪个位 (bitIndex)
                    int byteIndex = i / 8;
                    int bitIndex = i % 8;

                    // 使用位或(|)运算，将该位置为 1
                    // (1 << bitIndex) 生成一个只有 bitIndex 位是 1 的掩码
                    dataBytes[byteIndex] |= (byte)(1 << bitIndex);
                }
            }

            frame.AddRange(dataBytes);
            // --- 数据打包结束 ---

            // 计算并添加 CRC
            byte[] crc = ModbusCrc.Calculate(frame.ToArray());
            frame.AddRange(crc);

            return frame.ToArray();
        }




    }
}
