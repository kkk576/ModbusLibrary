using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusLibrary
{
    /// <summary>
    /// Modbus RTU 主站（Master）的核心类。
    /// 负责处理串口通信并协调帧的构建与解析。
    /// </summary>
    public class ModbusRtuMaster : IDisposable
    {
        private readonly SerialPort _serialPort;

        /// <summary>
        /// 初始化 ModbusRtuMaster 的新实例。
        /// </summary>
        /// <param name="portName">串口名称 (例如 "COM3")</param>
        /// <param name="baudRate">波特率 (例如 9600)</param>
        /// <param name="readTimeout">读取超时时间（毫秒）</param>
        public ModbusRtuMaster(string portName, int baudRate = 9600, int readTimeout = 1000)
        {
            _serialPort = new SerialPort(portName)
            {
                BaudRate = baudRate,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                ReadTimeout = readTimeout // 这个超时对于接收数据至关重要
            };
        }

        /// <summary>
        /// 打开串口连接。
        /// </summary>
        public void Connect()
        {
            if (!_serialPort.IsOpen)
            {
                _serialPort.Open();
            }
        }

        /// <summary>
        /// 关闭串口连接。
        /// </summary>
        public void Disconnect()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }

        /// <summary>
        /// 释放串口资源。
        /// </summary>
        public void Dispose()
        {
            Disconnect(); // 确保端口已关闭
            _serialPort.Dispose();
        }


        /// <summary>
        /// 发送一个请求帧并等待接收响应帧。
        /// </summary>
        /// <param name="requestFrame">要发送的完整请求报文。</param>
        /// <returns>接收到的完整响应报文。</returns>
        private byte[] SendAndReceive(byte[] requestFrame)
        {
            if (!_serialPort.IsOpen)
            {
                throw new InvalidOperationException("串口未连接，无法进行通信。");
            }

            // 1. 清空串口的输入输出缓冲区，防止旧数据干扰
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();

            // ▼▼▼【新增日志】▼▼▼
            // 在发送前，打印出即将发送的报文
            // BitConverter.ToString 会将字节数组转换成 "01-0A-FF" 这样的十六进制字符串
            Console.WriteLine($"TX -> {BitConverter.ToString(requestFrame)}");
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

            // 2. 发送请求报文
            _serialPort.Write(requestFrame, 0, requestFrame.Length);

            // 3. 接收响应报文
            // 我们利用 ReadTimeout 来判断一帧数据是否结束（即发生了一段静默时间）
            var responseBuffer = new List<byte>();
            try
            {
                while (true)
                {
                    responseBuffer.Add((byte)_serialPort.ReadByte());
                }
            }
            catch (TimeoutException)
            {
                // 超时是正常的，它标志着一帧数据的结束。
            }

            // ▼▼▼【新增日志】▼▼▼
            // 在接收到数据后，打印出收到的原始报文
            byte[] responseFrame = responseBuffer.ToArray();
            if (responseFrame.Length > 0)
            {
                Console.WriteLine($"RX <- {BitConverter.ToString(responseFrame)}");
            }
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

            // 如果超时后仍然一个字节都没收到，说明从站无响应
            if (responseBuffer.Count == 0)
            {
                throw new TimeoutException("从站设备无响应。");
            }

            return responseBuffer.ToArray();
        }
        

        /// <summary>
        /// 功能码 0x01: 读取线圈状态。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="startAddress">起始线圈地址</param>
        /// <param name="quantity">要读取的线圈数量</param>
        /// <returns>一个布尔数组，代表线圈的 ON/OFF 状态。</returns>
        public bool[] ReadCoils(byte slaveId, ushort startAddress, ushort quantity)
        {
            // 步骤 1: 让 FrameBuilder 构建请求报文 ("说")
            byte[] request = FrameBuilder.BuildReadCoilsRequest(slaveId, startAddress, quantity);

            // 步骤 2: 调用私有方法，通过串口发送请求并接收响应 ("通信")
            byte[] response = SendAndReceive(request);

            // 步骤 3: 让 FrameParser 解析响应报文 ("听")
            bool[] result = FrameParser.ParseReadCoilsResponse(response, quantity);
            
            // 步骤 4: 返回最终结果给用户
            return result;
        }

        /// <summary>
        /// 功能码 0x02: 读取离散输入状态。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="startAddress">起始输入地址</param>
        /// <param name="quantity">要读取的输入数量</param>
        /// <returns>一个布尔数组，代表输入的 ON/OFF 状态。</returns>
        public bool[] ReadDiscreteInputs(byte slaveId, ushort startAddress, ushort quantity)
        {
            // 1. 构建请求报文
            byte[] request = FrameBuilder.BuildReadDiscreteInputsRequest(slaveId, startAddress, quantity);

            // 2. 发送并接收响应
            byte[] response = SendAndReceive(request);

            // 3. 解析响应报文
            bool[] result = FrameParser.ParseReadDiscreteInputsResponse(response, quantity);

            // 4. 返回最终结果
            return result;
        }

        /// <summary>
        /// 功能码 0x03: 读取保持寄存器。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="startAddress">起始寄存器地址</param>
        /// <param name="quantity">要读取的寄存器数量</param>
        /// <returns>一个 ushort 数组，代表寄存器的值。</returns>
        public ushort[] ReadHoldingRegisters(byte slaveId, ushort startAddress, ushort quantity)
        {
            // 1. 构建请求报文
            byte[] request = FrameBuilder.BuildReadHoldingRegistersRequest(slaveId, startAddress, quantity);

            // 2. 发送并接收响应
            byte[] response = SendAndReceive(request);

            // 3. 解析响应报文
            ushort[] result = FrameParser.ParseReadHoldingRegistersResponse(response, quantity);

            // 4. 返回最终结果
            return result;
        }

        /// <summary>
        /// 功能码 0x04: 读取输入寄存器。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="startAddress">起始输入寄存器地址</param>
        /// <param name="quantity">要读取的寄存器数量</param>
        /// <returns>一个 ushort 数组，代表寄存器的值。</returns>
        public ushort[] ReadInputRegisters(byte slaveId, ushort startAddress, ushort quantity)
        {
            // 1. 构建请求报文
            byte[] request = FrameBuilder.BuildReadInputRegistersRequest(slaveId, startAddress, quantity);

            // 2. 发送并接收响应
            byte[] response = SendAndReceive(request);

            // 3. 解析响应报文
            ushort[] result = FrameParser.ParseReadInputRegistersResponse(response, quantity);

            // 4. 返回最终结果
            return result;
        }

        /// <summary>
        /// 功能码 0x05: 写入单个线圈。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="coilAddress">要写入的线圈地址</param>
        /// <param name="isOn">要写入的状态，true 为 ON, false 为 OFF</param>
        public void WriteSingleCoil(byte slaveId, ushort coilAddress, bool isOn)
        {
            // 1. 构建请求报文
            byte[] request = FrameBuilder.BuildWriteSingleCoilRequest(slaveId, coilAddress, isOn);

            // 2. 发送并接收响应
            byte[] response = SendAndReceive(request);

            // 3. 解析并验证响应
            FrameParser.ParseWriteSingleCoilResponse(response, coilAddress, isOn);

            // 如果没有抛出异常，代表写入成功
        }

        /// <summary>
        /// 功能码 0x06: 写入单个保持寄存器。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="registerAddress">要写入的寄存器地址</param>
        /// <param name="valueToWrite">要写入的16位值</param>
        public void WriteSingleRegister(byte slaveId, ushort registerAddress, ushort valueToWrite)
        {
            // 1. 构建请求报文
            byte[] request = FrameBuilder.BuildWriteSingleRegisterRequest(slaveId, registerAddress, valueToWrite);

            // 2. 发送并接收响应
            byte[] response = SendAndReceive(request);

            // 3. 解析并验证响应
            //    我们将期望的地址和值传给解析器，用于验证回显是否正确
            FrameParser.ParseWriteSingleRegisterResponse(response, registerAddress, valueToWrite);

            // 如果没有抛出异常，代表写入成功
        }

        /// <summary>
        /// 功能码 0x10: 写入多个保持寄存器。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="startAddress">起始寄存器地址</param>
        /// <param name="valuesToWrite">要写入的 ushort 数组</param>
        public void WriteMultipleRegisters(byte slaveId, ushort startAddress, ushort[] valuesToWrite)
        {
            // 对输入参数进行一些基本验证
            if (valuesToWrite == null || valuesToWrite.Length == 0)
            {
                throw new ArgumentException("要写入的数据不能为空。", nameof(valuesToWrite));
            }
            // Modbus协议规定一次最多写入约123个寄存器
            if (valuesToWrite.Length > 123)
            {
                throw new ArgumentException("写入的寄存器数量不能超过123个。", nameof(valuesToWrite));
            }

            // 1. 构建请求报文
            byte[] request = FrameBuilder.BuildWriteMultipleRegistersRequest(slaveId, startAddress, valuesToWrite);

            // 2. 发送并接收响应
            byte[] response = SendAndReceive(request);

            // 3. 解析并验证响应
            //    我们将期望的起始地址和数量传给解析器进行验证
            FrameParser.ParseWriteMultipleRegistersResponse(response, startAddress, (ushort)valuesToWrite.Length);

            // 如果没有抛出异常，代表写入成功
        }


        /// <summary>
        /// 功能码 0x0F: 写入多个线圈。
        /// </summary>
        /// <param name="slaveId">从站地址</param>
        /// <param name="startAddress">起始线圈地址</param>
        /// <param name="valuesToWrite">要写入的 bool 数组 (true=ON, false=OFF)</param>
        public void WriteMultipleCoils(byte slaveId, ushort startAddress, bool[] valuesToWrite)
        {
            if (valuesToWrite == null || valuesToWrite.Length == 0)
            {
                throw new ArgumentException("要写入的数据不能为空。", nameof(valuesToWrite));
            }
            // 协议规定一次最多写入约 1968 个线圈
            if (valuesToWrite.Length > 1968)
            {
                throw new ArgumentException("写入的线圈数量不能超过1968个。", nameof(valuesToWrite));
            }

            // 1. 构建请求报文
            byte[] request = FrameBuilder.BuildWriteMultipleCoilsRequest(slaveId, startAddress, valuesToWrite);

            // 2. 发送并接收响应
            byte[] response = SendAndReceive(request);

            // 3. 解析并验证响应
            FrameParser.ParseWriteMultipleCoilsResponse(response, startAddress, (ushort)valuesToWrite.Length);

            // 如果没有抛出异常，代表写入成功
        }




    }
}
