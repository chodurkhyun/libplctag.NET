﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using libplctag.NativeImport;

namespace libplctag
{

    public sealed class Tag : IDisposable
    {

        private const int ASYNC_STATUS_POLL_INTERVAL = 2;

        public Protocol Protocol { get; }
        public IPAddress Gateway { get; }
        public string Path { get; }
        public PlcType PlcType { get; }
        public int ElementSize { get; }
        public int ElementCount { get; }
        public string Name { get; }
        public bool UseConnectedMessaging { get; }
        public int ReadCacheMillisecondDuration
        {
            get
            {
                var result = plctag.plc_tag_get_int_attribute(tagHandle, "read_cache_ms", int.MinValue);
                if (result == int.MinValue)
                    throw new LibPlcTagException();
                return result;
            }
            set
            {
                var result = (Status)plctag.plc_tag_set_int_attribute(tagHandle, "read_cache_ms", value);
                if (result != Status.Ok)
                    throw new LibPlcTagException(result);
            }
        }

        private readonly int tagHandle;


        /// <summary>
        /// Provides a new tag. If the PLC type is Logix, the port type and slot has to be specified.
        /// </summary>
        /// <param name="gateway">IP address of the gateway for this protocol. Could be the IP address of the PLC you want to access.</param>
        /// <param name="path">Path to access the PLC from the gateway. Required for Logix, optional for others. 
        /// <param name="plcType">PLC type</param>
        /// <param name="elementSize">The size of an element in bytes. The tag is assumed to be composed of elements of the same size. For structure tags, use the total size of the structure.</param>
        /// <param name="name">The textual name of the tag to access. The name is anything allowed by the protocol. E.g. myDataStruct.rotationTimer.ACC, myDINTArray[42] etc.</param>
        /// <param name="elementCount">elements count: 1- single, n-array.</param>
        /// <param name="millisecondTimeout"></param>
        /// <param name="protocol">Currently only ab_eip supported.</param>
        /// <param name="readCacheMillisecondDuration">Set the amount of time to cache read results</param>
        /// <param name="useConnectedMessaging">Control whether to use connected or unconnected messaging.</param>
        public Tag(IPAddress gateway,
                   string path,
                   PlcType plcType,
                   int elementSize,
                   string name,
                   int millisecondTimeout,
                   int elementCount = 1,
                   Protocol protocol = Protocol.ab_eip,
                   int readCacheMillisecondDuration = default,
                   bool useConnectedMessaging = true)
        {

            Protocol = protocol;
            Gateway = gateway;
            Path = path;
            PlcType = plcType;
            ElementSize = elementSize;
            ElementCount = elementCount;
            Name = name;
            UseConnectedMessaging = useConnectedMessaging;

            var attributeString = GetAttributeString(protocol, gateway, path, plcType, elementSize, elementCount, name, readCacheMillisecondDuration, useConnectedMessaging);

            var result = plctag.plc_tag_create(attributeString, millisecondTimeout);
            if (result < 0)
                throw new LibPlcTagException((Status)result);
            else
                tagHandle = result;

        }

        ~Tag()
        {
            Dispose();
        }

        private static string GetAttributeString(Protocol protocol, IPAddress gateway, string path, PlcType plcType, int elementSize, int elementCount, string name, int readCacheMillisecondDuration, bool useConnectedMessaging)
        {

            var attributes = new Dictionary<string, string>();

            attributes.Add("protocol", protocol.ToString());
            attributes.Add("gateway", gateway.ToString());

            if (!string.IsNullOrEmpty(path))
                attributes.Add("path", path);

            attributes.Add("plc", plcType.ToString().ToLower());
            attributes.Add("elem_size", elementSize.ToString());
            attributes.Add("elem_count", elementCount.ToString());
            attributes.Add("name", name);

            if (readCacheMillisecondDuration > 0)
                attributes.Add("read_cache_ms", readCacheMillisecondDuration.ToString());

            attributes.Add("use_connected_msg", useConnectedMessaging ? "1" : "0");

            string separator = "&";
            return string.Join(separator, attributes.Select(attr => $"{attr.Key}={attr.Value}"));

        }

        public void Dispose()
        {
            var result = (Status)plctag.plc_tag_destroy(tagHandle);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);
        }

        public void Abort()
        {
            var result = (Status)plctag.plc_tag_abort(tagHandle);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);
        }

        public void Read(int millisecondTimeout)
        {

            if (millisecondTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondTimeout), "Must be greater than 0 for a synchronous read");

            var result = (Status)plctag.plc_tag_read(tagHandle, millisecondTimeout);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);

        }

        public async Task ReadAsync(int millisecondTimeout, CancellationToken token = default)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(millisecondTimeout);
                await ReadAsync(cts.Token);
            }
        }

        public async Task ReadAsync(CancellationToken token = default)
        {

            var status = (Status)plctag.plc_tag_read(tagHandle, 0);

            using (token.Register(() => Abort()))
            {
                while (status == Status.Pending)
                {
                    await Task.Delay(ASYNC_STATUS_POLL_INTERVAL, token);
                    status = GetStatus();
                }
            }

            if (status != Status.Ok)
                throw new LibPlcTagException(status);

        }

        public void Write(int millisecondTimeout)
        {

            if (millisecondTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondTimeout), "Must be greater than 0 for a synchronous write");

            var result = (Status)plctag.plc_tag_write(tagHandle, millisecondTimeout);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);

        }

        public async Task WriteAsync(int millisecondTimeout, CancellationToken token = default)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(millisecondTimeout);
                await WriteAsync(cts.Token);
            }
        }

        public async Task WriteAsync(CancellationToken token = default)
        {

            var status = (Status)plctag.plc_tag_write(tagHandle, 0);

            using (token.Register(() => Abort()))
            {
                while (status == Status.Pending)
                {
                    await Task.Delay(ASYNC_STATUS_POLL_INTERVAL, token);
                    status = GetStatus();
                }
            }

            if (status != Status.Ok)
                throw new LibPlcTagException(status);

        }

        public int GetSize()
        {
            var result = plctag.plc_tag_get_size(tagHandle);
            if (result < 0)
                throw new LibPlcTagException((Status)result);
            else
                return result;
        }

        public Status GetStatus() => (Status)plctag.plc_tag_status(tagHandle);

        public bool GetBit(int offset)
        {
            var result = plctag.plc_tag_get_bit(tagHandle, offset);
            if (result == 0)
                return false;
            else if (result == 1)
                return true;
            else
                throw new LibPlcTagException((Status)result);
        }

        public void SetBit(int offset, bool value)
        {
            int valueAsInteger = value == true ? 1 : 0;
            var result = (Status)plctag.plc_tag_set_bit(tagHandle, offset, valueAsInteger);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);
        }

        public ulong GetUInt64(int offset)
        {
            var result = plctag.plc_tag_get_uint64(tagHandle, offset);
            if (result == ulong.MaxValue)
                throw new LibPlcTagException();
            return result;
        }
        public void SetUInt64(int offset, ulong value)
        {
            var result = (Status)plctag.plc_tag_set_uint64(tagHandle, offset, value);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);
        }

        public long GetInt64(int offset)
        {
            var result = plctag.plc_tag_get_int64(tagHandle, offset);
            if (result == long.MinValue)
                throw new LibPlcTagException();
            return result;
        }

        public void SetInt64(int offset, long value)
        {
            var result = (Status)plctag.plc_tag_set_int64(tagHandle, offset, value);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);
        }

        public uint GetUInt32(int offset)
        {
            var result = plctag.plc_tag_get_uint32(tagHandle, offset);
            if (result == uint.MaxValue)
                throw new LibPlcTagException();
            return result;
        }

        public void SetUInt32(int offset, uint value)
        {
            var result = (Status)plctag.plc_tag_set_uint32(tagHandle, offset, value);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);
        }

        public int GetInt32(int offset)
        {
            var result = plctag.plc_tag_get_int32(tagHandle, offset);
            if (result == int.MinValue)
                throw new LibPlcTagException();
            return result;
        }

        public void SetInt32(int offset, int value)
        {
            var result = (Status)plctag.plc_tag_set_int32(tagHandle, offset, value);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);
        }

        public ushort GetUInt16(int offset)
        {
            var result = plctag.plc_tag_get_uint16(tagHandle, offset);
            if (result == ushort.MaxValue)
                throw new LibPlcTagException();
            return result;
        }

        public void SetUInt16(int offset, ushort value)
        {
           var result = (Status)plctag.plc_tag_set_uint16(tagHandle, offset, value);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);
        }

        public short GetInt16(int offset)
        {
            var result = plctag.plc_tag_get_int16(tagHandle, offset);
            if (result == short.MinValue)
                throw new LibPlcTagException();
            return result;
        }
        public void SetInt16(int offset, short value)
        {
            var result = (Status)plctag.plc_tag_set_int16(tagHandle, offset, value);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);
        }

        public byte GetUInt8(int offset)
        {
            var result = plctag.plc_tag_get_uint8(tagHandle, offset);
            if (result == byte.MaxValue)
                throw new LibPlcTagException();
            return result;
        }

        public void SetUInt8(int offset, byte value)
        {
            var result = (Status)plctag.plc_tag_set_uint8(tagHandle, offset, value);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);
        }

        public sbyte GetInt8(int offset)
        {
            var result = plctag.plc_tag_get_int8(tagHandle, offset);
            if (result == sbyte.MinValue)
                throw new LibPlcTagException();
            return result;
        }

        public void SetInt8(int offset, sbyte value)
        {
            var result = (Status)plctag.plc_tag_set_int8(tagHandle, offset, value);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);
        }

        public double GetFloat64(int offset)
        {
            var result = plctag.plc_tag_get_float64(tagHandle, offset);
            if (result == double.MinValue)
                throw new LibPlcTagException();
            return result;
        }
        public void SetFloat64(int offset, double value)
        {
            var result = (Status)plctag.plc_tag_set_float64(tagHandle, offset, value);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);
        }

        public float GetFloat32(int offset)
        {
            var result = plctag.plc_tag_get_float32(tagHandle, offset);
            if (result == float.MinValue)
                throw new LibPlcTagException();
            return result;
        }
        public void SetFloat32(int offset, float value)
        {
            var result = (Status)plctag.plc_tag_set_float32(tagHandle, offset, value);
            if (result != Status.Ok)
                throw new LibPlcTagException(result);
        }

    }

}