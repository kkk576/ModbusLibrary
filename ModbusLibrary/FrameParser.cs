using MyModbusLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusLibrary
{
    /// <summary>
    /// 负责解析 Modbus 响应帧的内部工具类。
    /// </summary>
    internal class FrameParser
    {
        /// <summary>
        /// 对任何传入的响应帧进行基础验证（CRC和最小长度）。
        /// 如果验证失败，则直接抛出异常。
        /// </summary>
        private static void ValidateFrame(byte[] responseFrame)
        {
            // 1. 检查最小长度
            // 一个正常的响应至少包含: 从站地址(1) + 功能码(1) + 字节计数(1) + CRC(2) = 5字节
            // 一个异常的响应包含: 从站地址(1) + 异常功能码(1) + 异常码(1) + CRC(2) = 5字节
            if (responseFrame.Length < 5)
            {
                throw new ModbusFrameException("响应帧长度不足5字节，无效。");
            }

            // 2. 验证CRC
            // 分离出数据部分和CRC部分
            var dataPart = new byte[responseFrame.Length - 2];
            Array.Copy(responseFrame, 0, dataPart, 0, dataPart.Length);

            byte[] receivedCrc = { responseFrame[responseFrame.Length - 2], responseFrame[responseFrame.Length - 1] };

            // 重新计算CRC并进行比较
            byte[] calculatedCrc = ModbusCrc.Calculate(dataPart);

            if (receivedCrc[0] != calculatedCrc[0] || receivedCrc[1] != calculatedCrc[1])
            {
                string errorMsg = $"CRC校验失败! " +
                                  $"收到: {receivedCrc[0]:X2}-{receivedCrc[1]:X2}, " +
                                  $"计算得: {calculatedCrc[0]:X2}-{calculatedCrc[1]:X2}";
                throw new ModbusFrameException(errorMsg);
            }
        }


        /// <summary>
        /// 从大端序的字节数组片段中解析一个 ushort。
        /// </summary>
        private static ushort GetValueFromBigEndianBytes(byte[] buffer, int startIndex)
        {
            // 从 buffer 中指定位置复制两个字节
            byte[] temp = { buffer[startIndex], buffer[startIndex + 1] };

            // 如果系统是小端序，需要反转字节以正确解析
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(temp);
            }
            return BitConverter.ToUInt16(temp, 0);
        }
        /// <summary>
        /// 解析 "读取线圈" (功能码 0x01) 的响应帧。
        /// </summary>
        /// <param name="responseFrame">从站返回的完整响应帧。</param>
        /// <param name="quantity">我们期望读取的线圈数量。</param>
        /// <returns>一个布尔数组，代表线圈的 ON/OFF 状态。</returns>
        internal static bool[] ParseReadCoilsResponse(byte[] responseFrame, ushort quantity)
        {
            // 1. 首先进行通用验证
            ValidateFrame(responseFrame);

            // 2. 检查是否是异常响应
            // 正常功能码是 0x01，异常时会返回 0x81 (0x01 + 0x80)
            if (responseFrame[1] > 0x80)
            {
                byte exceptionCode = responseFrame[2];
                throw new ModbusSlaveException(exceptionCode);
            }

            // 检查功能码是否是我们期望的 0x01 
            if (responseFrame[1] != 0x01)
            {
                throw new ModbusFrameException($"功能码不匹配，期望 0x01，收到 {responseFrame[1]:X2}");
            }

            // 3. 解析数据区
            byte byteCount = responseFrame[2];
            var coilStatus = new List<bool>();

            // 数据从第4个字节 (索引3) 开始
            for (int i = 0; i < byteCount; i++)
            {
                byte dataByte = responseFrame[3 + i];

                // 将一个字节中的8个位，逐一解包成 bool 值
                for (int j = 0; j < 8; j++)
                {
                    // 如果已解析的数量达到了我们期望的数量，就停止
                    if (coilStatus.Count >= quantity)
                    {
                        break;
                    }

                    // 使用位运算检查第 j 位是否为 1
                    // (dataByte & (1 << j)) 的结果不是0，就代表那一位是1 (ON)
                    coilStatus.Add((dataByte & (1 << j)) != 0);
                }
            }

            return coilStatus.ToArray();
        }

        /// <summary>
        /// 解析 "读取离散输入" (功能码 0x02) 的响应帧。
        /// </summary>
        /// <param name="responseFrame">从站返回的完整响应帧。</param>
        /// <param name="quantity">我们期望读取的输入数量。</param>
        /// <returns>一个布尔数组，代表输入的 ON/OFF 状态。</returns>
        internal static bool[] ParseReadDiscreteInputsResponse(byte[] responseFrame, ushort quantity)
        {
            // 1. 基础验证 (CRC, 最小长度)
            ValidateFrame(responseFrame);

            // 2. 检查是否是异常响应 (检查的是 0x82)
            if (responseFrame[1] > 0x80)
            {
                byte exceptionCode = responseFrame[2];
                throw new ModbusSlaveException(exceptionCode);
            }

            // 检查功能码是否是我们期望的 0x02 (唯一的逻辑改动)
            if (responseFrame[1] != 0x02)
            {
                throw new ModbusFrameException($"功能码不匹配，期望 0x02，收到 {responseFrame[1]:X2}");
            }

            // 3. 解析数据区 (这部分逻辑和 ParseReadCoilsResponse 完全一样)
            byte byteCount = responseFrame[2];
            var inputStatus = new List<bool>();

            for (int i = 0; i < byteCount; i++)
            {
                byte dataByte = responseFrame[3 + i];
                for (int j = 0; j < 8; j++)
                {
                    if (inputStatus.Count >= quantity)
                    {
                        break;
                    }
                    inputStatus.Add((dataByte & (1 << j)) != 0);
                }
            }

            return inputStatus.ToArray();
        }

        /// <summary>
        /// 解析 "读取保持寄存器" (功能码 0x03) 的响应帧。
        /// </summary>
        /// <param name="responseFrame">从站返回的完整响应帧。</param>
        /// <returns>一个 ushort 数组，代表寄存器的值。</returns>
        internal static ushort[] ParseReadHoldingRegistersResponse(byte[] responseFrame, ushort quantity)
        {
            // 1. 基础验证 (CRC, 最小长度)
            ValidateFrame(responseFrame);

            // 2. 检查是否是异常响应 (检查的是 0x83)
            if (responseFrame[1] > 0x80)
            {
                byte exceptionCode = responseFrame[2];
                throw new ModbusSlaveException(exceptionCode);
            }

            // 检查功能码是否是我们期望的 0x03
            if (responseFrame[1] != 0x03)
            {
                throw new ModbusFrameException($"功能码不匹配，期望 0x03，收到 {responseFrame[1]:X2}");
            }

            // 3. 解析数据区
            byte byteCount = responseFrame[2];

            // 检查从站返回的字节数是否等于我们期望的数量 * 2
            if (byteCount != quantity * 2)
            {
                throw new ModbusFrameException($"响应报文中的字节计数与请求的数量不符。期望 {quantity * 2} 字节，收到 {byteCount} 字节。");
            }

            // 检查字节计数是否是偶数 (因为一个寄存器是2字节)
            if (byteCount % 2 != 0)
            {
                throw new ModbusFrameException("响应数据区的字节计数不是偶数。");
            }

            int numRegisters = byteCount / 2;
            var registerValues = new ushort[numRegisters];

            // 数据从第4个字节 (索引3) 开始
            for (int i = 0; i < numRegisters; i++)
            {
                // 每次读取2个字节，并使用我们之前为 0x06 功能码创建的辅助方法来转换
                registerValues[i] = GetValueFromBigEndianBytes(responseFrame, 3 + (i * 2));
            }

            return registerValues;
        }

        /// <summary>
        /// 解析 "读取输入寄存器" (功能码 0x04) 的响应帧。
        /// </summary>
        /// <param name="responseFrame">从站返回的完整响应帧。</param>
        /// <param name="quantity">我们期望读取的寄存器数量。</param>
        /// <returns>一个 ushort 数组，代表寄存器的值。</returns>
        internal static ushort[] ParseReadInputRegistersResponse(byte[] responseFrame, ushort quantity)
        {
            // 1. 基础验证
            ValidateFrame(responseFrame);

            // 2. 检查异常
            if (responseFrame[1] > 0x80)
            {
                byte exceptionCode = responseFrame[2];
                throw new ModbusSlaveException(exceptionCode);
            }

            // 3. 检查功能码
            if (responseFrame[1] != 0x04)
            {
                throw new ModbusFrameException($"功能码不匹配，期望 0x04，收到 {responseFrame[1]:X2}");
            }

            // 4. 解析数据区
            byte byteCount = responseFrame[2];

            // 检查从站返回的字节数是否等于我们期望的数量 * 2
            if (byteCount != quantity * 2)
            {
                throw new ModbusFrameException($"响应报文中的字节计数与请求的数量不符。期望 {quantity * 2} 字节，收到 {byteCount} 字节。");
            }

            int numRegisters = byteCount / 2;
            var registerValues = new ushort[numRegisters];

            for (int i = 0; i < numRegisters; i++)
            {
                int startIndex = 3 + (i * 2);
                registerValues[i] = GetValueFromBigEndianBytes(responseFrame, startIndex);
            }

            return registerValues;
        }

        /// <summary>
        /// 解析 "写入单个线圈" (功能码 0x05) 的响应帧。
        /// 成功时不返回任何内容，失败则抛出异常。
        /// </summary>
        /// <param name="responseFrame">从站返回的完整响应帧。</param>
        /// <param name="expectedAddress">我们期望写入的地址。</param>
        /// <param name="expectedIsOn">我们期望写入的状态。</param>
        internal static void ParseWriteSingleCoilResponse(byte[] responseFrame, ushort expectedAddress, bool expectedIsOn)
        {
            // 1. 基础验证 (CRC, 最小长度)
            ValidateFrame(responseFrame);

            // 2. 检查是否是异常响应
            if (responseFrame[1] > 0x80)
            {
                byte exceptionCode = responseFrame[2];
                throw new ModbusSlaveException(exceptionCode);
            }

            // 3. 检查功能码是否正确
            if (responseFrame[1] != 0x05)
            {
                throw new ModbusFrameException($"功能码不匹配，期望 0x05，收到 {responseFrame[1]:X2}");
            }

            // 4. 检查报文长度是否为固定的8字节
            if (responseFrame.Length != 8)
            {
                throw new ModbusFrameException($"响应报文长度错误，期望 8 字节，收到 {responseFrame.Length} 字节");
            }

            // 5. 将期望的状态(bool)转换回协议中的数值，用于比较
            ushort expectedValue = expectedIsOn ? (ushort)0xFF00 : (ushort)0x0000;

            // 6. 从响应中提取地址和值
            ushort addressFromResponse = GetValueFromBigEndianBytes(responseFrame, 2);
            ushort valueFromResponse = GetValueFromBigEndianBytes(responseFrame, 4);

            // 7. 确认从站返回的回显是否与我们发送的请求一致
            if (addressFromResponse != expectedAddress || valueFromResponse != expectedValue)
            {
                throw new ModbusFrameException("响应内容与请求不匹配（回显错误）。");
            }
        }
        /// <summary>
        /// 解析 "写入单个寄存器" (功能码 0x06) 的响应帧。
        /// 成功时不返回任何内容，失败则抛出异常。
        /// </summary>
        /// <param name="responseFrame">从站返回的完整响应帧。</param>
        /// <param name="expectedAddress">我们期望写入的地址。</param>
        /// <param name="expectedValue">我们期望写入的值。</param>
        internal static void ParseWriteSingleRegisterResponse(byte[] responseFrame, ushort expectedAddress, ushort expectedValue)
        {
            // 1. 基础验证 (CRC, 最小长度)
            ValidateFrame(responseFrame);

            // 2. 检查是否是异常响应
            if (responseFrame[1] > 0x80)
            {
                byte exceptionCode = responseFrame[2];
                throw new ModbusSlaveException(exceptionCode);
            }

            // 3. 检查功能码是否正确
            if (responseFrame[1] != 0x06)
            {
                throw new ModbusFrameException($"功能码不匹配，期望 0x06，收到 {responseFrame[1]:X2}");
            }

            // 4. 检查报文长度是否为固定的8字节
            if (responseFrame.Length != 8)
            {
                throw new ModbusFrameException($"响应报文长度错误，期望 8 字节，收到 {responseFrame.Length} 字节");
            }

            // 5. 从响应中提取地址和值，进行验证
            ushort addressFromResponse = GetValueFromBigEndianBytes(responseFrame, 2);
            ushort valueFromResponse = GetValueFromBigEndianBytes(responseFrame, 4);

            // 6. 确认从站返回的回显是否与我们发送的请求一致
            if (addressFromResponse != expectedAddress || valueFromResponse != expectedValue)
            {
                throw new ModbusFrameException("响应内容与请求不匹配（回显错误）。");
            }
        }

        /// <summary>
        /// 解析 "写入多个寄存器" (功能码 0x10) 的响应帧。
        /// </summary>
        /// <param name="responseFrame">从站返回的完整响应帧。</param>
        /// <param name="expectedStartAddress">我们期望写入的起始地址。</param>
        /// <param name="expectedQuantity">我们期望写入的寄存器数量。</param>
        internal static void ParseWriteMultipleRegistersResponse(byte[] responseFrame, ushort expectedStartAddress, ushort expectedQuantity)
        {
            // 1. 基础验证
            ValidateFrame(responseFrame);

            // 2. 检查异常
            if (responseFrame[1] > 0x80)
            {
                byte exceptionCode = responseFrame[2];
                throw new ModbusSlaveException(exceptionCode);
            }

            // 3. 检查功能码
            if (responseFrame[1] != 0x10)
            {
                throw new ModbusFrameException($"功能码不匹配，期望 0x10，收到 {responseFrame[1]:X2}");
            }

            // 4. 检查报文长度是否为固定的8字节
            if (responseFrame.Length != 8)
            {
                throw new ModbusFrameException($"响应报文长度错误，期望 8 字节，收到 {responseFrame.Length} 字节");
            }

            // 5. 从响应中提取地址和数量
            ushort addressFromResponse = GetValueFromBigEndianBytes(responseFrame, 2);
            ushort quantityFromResponse = GetValueFromBigEndianBytes(responseFrame, 4);

            // 6. 确认从站返回的地址和数量是否与我们发送的请求一致
            if (addressFromResponse != expectedStartAddress || quantityFromResponse != expectedQuantity)
            {
                throw new ModbusFrameException("响应内容（起始地址或数量）与请求不匹配。");
            }
        }

        /// <summary>
        /// 解析 "写入多个线圈" (功能码 0x0F) 的响应帧。
        /// </summary>
        /// <param name="responseFrame">从站返回的完整响应帧。</param>
        /// <param name="expectedStartAddress">我们期望写入的起始地址。</param>
        /// <param name="expectedQuantity">我们期望写入的线圈数量。</param>
        internal static void ParseWriteMultipleCoilsResponse(byte[] responseFrame, ushort expectedStartAddress, ushort expectedQuantity)
        {
            ValidateFrame(responseFrame);

            if (responseFrame[1] > 0x80)
            {
                byte exceptionCode = responseFrame[2];
                throw new ModbusSlaveException(exceptionCode);
            }

            if (responseFrame[1] != 0x0F)
            {
                throw new ModbusFrameException($"功能码不匹配，期望 0x0F，收到 {responseFrame[1]:X2}");
            }

            if (responseFrame.Length != 8)
            {
                throw new ModbusFrameException($"响应报文长度错误，期望 8 字节，收到 {responseFrame.Length} 字节");
            }

            ushort addressFromResponse = GetValueFromBigEndianBytes(responseFrame, 2);
            ushort quantityFromResponse = GetValueFromBigEndianBytes(responseFrame, 4);

            if (addressFromResponse != expectedStartAddress || quantityFromResponse != expectedQuantity)
            {
                throw new ModbusFrameException("响应内容（起始地址或数量）与请求不匹配。");
            }
        }














    }
}
