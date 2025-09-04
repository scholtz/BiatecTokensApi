using System;
using Algorand;
using Algorand.Algod;
using Algorand.Algod.Model;
using Algorand.Algod.Model.Transactions;
using AVM.ClientGenerator;
using AVM.ClientGenerator.Core;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AVM.ClientGenerator.ABI.ARC56;
using Algorand.AVM.ClientGenerator.ABI.ARC56;

namespace BiatecTokensApi.Generated
{


    public class Arc1644Proxy : ProxyBase
    {
        public override AppDescriptionArc56 App { get; set; }

        public Arc1644Proxy(DefaultApi defaultApi, ulong appId) : base(defaultApi, appId)
        {
            App = Newtonsoft.Json.JsonConvert.DeserializeObject<AVM.ClientGenerator.ABI.ARC56.AppDescriptionArc56>(Encoding.UTF8.GetString(Convert.FromBase64String(_ARC56DATA))) ?? throw new Exception("Error reading ARC56 data");

        }

        public class Structs
        {
            public class ApprovalStruct : AVMObjectType
            {
                public AVM.ClientGenerator.ABI.ARC4.Types.UInt256 ApprovalAmount { get; set; }

                public Algorand.Address Owner { get; set; }

                public Algorand.Address Spender { get; set; }

                public byte[] ToByteArray()
                {
                    var ret = new List<byte>();
                    var stringRef = new Dictionary<int, byte[]>();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vApprovalAmount = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("uint256");
                    vApprovalAmount.From(ApprovalAmount);
                    ret.AddRange(vApprovalAmount.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vOwner = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vOwner.From(Owner);
                    ret.AddRange(vOwner.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vSpender = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vSpender.From(Spender);
                    ret.AddRange(vSpender.Encode());
                    foreach (var item in stringRef)
                    {
                        var b1 = ret.Count;
                        ret[item.Key] = Convert.ToByte(b1 / 256);
                        ret[item.Key + 1] = Convert.ToByte(b1 % 256);
                        ret.AddRange(item.Value);
                    }
                    return ret.ToArray();

                }

                public static ApprovalStruct Parse(byte[] bytes)
                {
                    var queue = new Queue<byte>(bytes);
                    var prefixOffset = 0;
                    var retPrefix = new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] };
                    if (retPrefix.SequenceEqual(Constants.RetPrefix))
                    {
                        prefixOffset = 4;
                        for (int i = 0; i < 4 && queue.Count > 0; i++) { queue.Dequeue(); }
                    }
                    var ret = new ApprovalStruct();
                    uint count = 0;
                    var vApprovalAmount = new AVM.ClientGenerator.ABI.ARC4.Types.UInt256();
                    count = vApprovalAmount.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    ret.ApprovalAmount = vApprovalAmount;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vOwner = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vOwner.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueOwner = vOwner.ToValue();
                    if (valueOwner is Algorand.Address vOwnerValue) { ret.Owner = vOwnerValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vSpender = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vSpender.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueSpender = vSpender.ToValue();
                    if (valueSpender is Algorand.Address vSpenderValue) { ret.Spender = vSpenderValue; }
                    return ret;

                }

                public override string ToString()
                {
                    return $"{this.GetType().ToString()} {BitConverter.ToString(ToByteArray()).Replace("-", "")}";
                }
                public override bool Equals(object? obj)
                {
                    return Equals(obj as ApprovalStruct);
                }
                public bool Equals(ApprovalStruct? other)
                {
                    return other is not null && ToByteArray().SequenceEqual(other.ToByteArray());
                }
                public override int GetHashCode()
                {
                    return ToByteArray().GetHashCode();
                }
                public static bool operator ==(ApprovalStruct left, ApprovalStruct right)
                {
                    return EqualityComparer<ApprovalStruct>.Default.Equals(left, right);
                }
                public static bool operator !=(ApprovalStruct left, ApprovalStruct right)
                {
                    return !(left == right);
                }

            }

            public class Arc1410HoldingPartitionsPaginatedKey : AVMObjectType
            {
                public Algorand.Address Holder { get; set; }

                public ulong Page { get; set; }

                public byte[] ToByteArray()
                {
                    var ret = new List<byte>();
                    var stringRef = new Dictionary<int, byte[]>();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vHolder = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vHolder.From(Holder);
                    ret.AddRange(vHolder.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vPage = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("uint64");
                    vPage.From(Page);
                    ret.AddRange(vPage.Encode());
                    foreach (var item in stringRef)
                    {
                        var b1 = ret.Count;
                        ret[item.Key] = Convert.ToByte(b1 / 256);
                        ret[item.Key + 1] = Convert.ToByte(b1 % 256);
                        ret.AddRange(item.Value);
                    }
                    return ret.ToArray();

                }

                public static Arc1410HoldingPartitionsPaginatedKey Parse(byte[] bytes)
                {
                    var queue = new Queue<byte>(bytes);
                    var prefixOffset = 0;
                    var retPrefix = new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] };
                    if (retPrefix.SequenceEqual(Constants.RetPrefix))
                    {
                        prefixOffset = 4;
                        for (int i = 0; i < 4 && queue.Count > 0; i++) { queue.Dequeue(); }
                    }
                    var ret = new Arc1410HoldingPartitionsPaginatedKey();
                    uint count = 0;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vHolder = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vHolder.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueHolder = vHolder.ToValue();
                    if (valueHolder is Algorand.Address vHolderValue) { ret.Holder = vHolderValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vPage = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("uint64");
                    count = vPage.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valuePage = vPage.ToValue();
                    if (valuePage is ulong vPageValue) { ret.Page = vPageValue; }
                    return ret;

                }

                public override string ToString()
                {
                    return $"{this.GetType().ToString()} {BitConverter.ToString(ToByteArray()).Replace("-", "")}";
                }
                public override bool Equals(object? obj)
                {
                    return Equals(obj as Arc1410HoldingPartitionsPaginatedKey);
                }
                public bool Equals(Arc1410HoldingPartitionsPaginatedKey? other)
                {
                    return other is not null && ToByteArray().SequenceEqual(other.ToByteArray());
                }
                public override int GetHashCode()
                {
                    return ToByteArray().GetHashCode();
                }
                public static bool operator ==(Arc1410HoldingPartitionsPaginatedKey left, Arc1410HoldingPartitionsPaginatedKey right)
                {
                    return EqualityComparer<Arc1410HoldingPartitionsPaginatedKey>.Default.Equals(left, right);
                }
                public static bool operator !=(Arc1410HoldingPartitionsPaginatedKey left, Arc1410HoldingPartitionsPaginatedKey right)
                {
                    return !(left == right);
                }

            }

            public class Arc1410OperatorKey : AVMObjectType
            {
                public Algorand.Address Holder { get; set; }

                public Algorand.Address Operator { get; set; }

                public Algorand.Address Partition { get; set; }

                public byte[] ToByteArray()
                {
                    var ret = new List<byte>();
                    var stringRef = new Dictionary<int, byte[]>();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vHolder = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vHolder.From(Holder);
                    ret.AddRange(vHolder.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vOperator = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vOperator.From(Operator);
                    ret.AddRange(vOperator.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vPartition = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vPartition.From(Partition);
                    ret.AddRange(vPartition.Encode());
                    foreach (var item in stringRef)
                    {
                        var b1 = ret.Count;
                        ret[item.Key] = Convert.ToByte(b1 / 256);
                        ret[item.Key + 1] = Convert.ToByte(b1 % 256);
                        ret.AddRange(item.Value);
                    }
                    return ret.ToArray();

                }

                public static Arc1410OperatorKey Parse(byte[] bytes)
                {
                    var queue = new Queue<byte>(bytes);
                    var prefixOffset = 0;
                    var retPrefix = new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] };
                    if (retPrefix.SequenceEqual(Constants.RetPrefix))
                    {
                        prefixOffset = 4;
                        for (int i = 0; i < 4 && queue.Count > 0; i++) { queue.Dequeue(); }
                    }
                    var ret = new Arc1410OperatorKey();
                    uint count = 0;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vHolder = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vHolder.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueHolder = vHolder.ToValue();
                    if (valueHolder is Algorand.Address vHolderValue) { ret.Holder = vHolderValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vOperator = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vOperator.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueOperator = vOperator.ToValue();
                    if (valueOperator is Algorand.Address vOperatorValue) { ret.Operator = vOperatorValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vPartition = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vPartition.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valuePartition = vPartition.ToValue();
                    if (valuePartition is Algorand.Address vPartitionValue) { ret.Partition = vPartitionValue; }
                    return ret;

                }

                public override string ToString()
                {
                    return $"{this.GetType().ToString()} {BitConverter.ToString(ToByteArray()).Replace("-", "")}";
                }
                public override bool Equals(object? obj)
                {
                    return Equals(obj as Arc1410OperatorKey);
                }
                public bool Equals(Arc1410OperatorKey? other)
                {
                    return other is not null && ToByteArray().SequenceEqual(other.ToByteArray());
                }
                public override int GetHashCode()
                {
                    return ToByteArray().GetHashCode();
                }
                public static bool operator ==(Arc1410OperatorKey left, Arc1410OperatorKey right)
                {
                    return EqualityComparer<Arc1410OperatorKey>.Default.Equals(left, right);
                }
                public static bool operator !=(Arc1410OperatorKey left, Arc1410OperatorKey right)
                {
                    return !(left == right);
                }

            }

            public class Arc1410OperatorPortionKey : AVMObjectType
            {
                public Algorand.Address Holder { get; set; }

                public Algorand.Address Operator { get; set; }

                public Algorand.Address Partition { get; set; }

                public byte[] ToByteArray()
                {
                    var ret = new List<byte>();
                    var stringRef = new Dictionary<int, byte[]>();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vHolder = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vHolder.From(Holder);
                    ret.AddRange(vHolder.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vOperator = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vOperator.From(Operator);
                    ret.AddRange(vOperator.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vPartition = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vPartition.From(Partition);
                    ret.AddRange(vPartition.Encode());
                    foreach (var item in stringRef)
                    {
                        var b1 = ret.Count;
                        ret[item.Key] = Convert.ToByte(b1 / 256);
                        ret[item.Key + 1] = Convert.ToByte(b1 % 256);
                        ret.AddRange(item.Value);
                    }
                    return ret.ToArray();

                }

                public static Arc1410OperatorPortionKey Parse(byte[] bytes)
                {
                    var queue = new Queue<byte>(bytes);
                    var prefixOffset = 0;
                    var retPrefix = new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] };
                    if (retPrefix.SequenceEqual(Constants.RetPrefix))
                    {
                        prefixOffset = 4;
                        for (int i = 0; i < 4 && queue.Count > 0; i++) { queue.Dequeue(); }
                    }
                    var ret = new Arc1410OperatorPortionKey();
                    uint count = 0;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vHolder = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vHolder.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueHolder = vHolder.ToValue();
                    if (valueHolder is Algorand.Address vHolderValue) { ret.Holder = vHolderValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vOperator = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vOperator.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueOperator = vOperator.ToValue();
                    if (valueOperator is Algorand.Address vOperatorValue) { ret.Operator = vOperatorValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vPartition = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vPartition.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valuePartition = vPartition.ToValue();
                    if (valuePartition is Algorand.Address vPartitionValue) { ret.Partition = vPartitionValue; }
                    return ret;

                }

                public override string ToString()
                {
                    return $"{this.GetType().ToString()} {BitConverter.ToString(ToByteArray()).Replace("-", "")}";
                }
                public override bool Equals(object? obj)
                {
                    return Equals(obj as Arc1410OperatorPortionKey);
                }
                public bool Equals(Arc1410OperatorPortionKey? other)
                {
                    return other is not null && ToByteArray().SequenceEqual(other.ToByteArray());
                }
                public override int GetHashCode()
                {
                    return ToByteArray().GetHashCode();
                }
                public static bool operator ==(Arc1410OperatorPortionKey left, Arc1410OperatorPortionKey right)
                {
                    return EqualityComparer<Arc1410OperatorPortionKey>.Default.Equals(left, right);
                }
                public static bool operator !=(Arc1410OperatorPortionKey left, Arc1410OperatorPortionKey right)
                {
                    return !(left == right);
                }

            }

            public class Arc1410PartitionKey : AVMObjectType
            {
                public Algorand.Address Holder { get; set; }

                public Algorand.Address Partition { get; set; }

                public byte[] ToByteArray()
                {
                    var ret = new List<byte>();
                    var stringRef = new Dictionary<int, byte[]>();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vHolder = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vHolder.From(Holder);
                    ret.AddRange(vHolder.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vPartition = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vPartition.From(Partition);
                    ret.AddRange(vPartition.Encode());
                    foreach (var item in stringRef)
                    {
                        var b1 = ret.Count;
                        ret[item.Key] = Convert.ToByte(b1 / 256);
                        ret[item.Key + 1] = Convert.ToByte(b1 % 256);
                        ret.AddRange(item.Value);
                    }
                    return ret.ToArray();

                }

                public static Arc1410PartitionKey Parse(byte[] bytes)
                {
                    var queue = new Queue<byte>(bytes);
                    var prefixOffset = 0;
                    var retPrefix = new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] };
                    if (retPrefix.SequenceEqual(Constants.RetPrefix))
                    {
                        prefixOffset = 4;
                        for (int i = 0; i < 4 && queue.Count > 0; i++) { queue.Dequeue(); }
                    }
                    var ret = new Arc1410PartitionKey();
                    uint count = 0;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vHolder = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vHolder.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueHolder = vHolder.ToValue();
                    if (valueHolder is Algorand.Address vHolderValue) { ret.Holder = vHolderValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vPartition = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vPartition.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valuePartition = vPartition.ToValue();
                    if (valuePartition is Algorand.Address vPartitionValue) { ret.Partition = vPartitionValue; }
                    return ret;

                }

                public override string ToString()
                {
                    return $"{this.GetType().ToString()} {BitConverter.ToString(ToByteArray()).Replace("-", "")}";
                }
                public override bool Equals(object? obj)
                {
                    return Equals(obj as Arc1410PartitionKey);
                }
                public bool Equals(Arc1410PartitionKey? other)
                {
                    return other is not null && ToByteArray().SequenceEqual(other.ToByteArray());
                }
                public override int GetHashCode()
                {
                    return ToByteArray().GetHashCode();
                }
                public static bool operator ==(Arc1410PartitionKey left, Arc1410PartitionKey right)
                {
                    return EqualityComparer<Arc1410PartitionKey>.Default.Equals(left, right);
                }
                public static bool operator !=(Arc1410PartitionKey left, Arc1410PartitionKey right)
                {
                    return !(left == right);
                }

            }

            public class Arc1410CanTransferByPartitionReturn : AVMObjectType
            {
                public byte Code { get; set; }

                public string Status { get; set; }

                public Algorand.Address ReceiverPartition { get; set; }

                public byte[] ToByteArray()
                {
                    var ret = new List<byte>();
                    var stringRef = new Dictionary<int, byte[]>();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vCode = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte");
                    vCode.From(Code);
                    ret.AddRange(vCode.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vStatus = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("string");
                    vStatus.From(Status);
                    stringRef[ret.Count] = vStatus.Encode();
                    ret.AddRange(new byte[2]);
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vReceiverPartition = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vReceiverPartition.From(ReceiverPartition);
                    ret.AddRange(vReceiverPartition.Encode());
                    foreach (var item in stringRef)
                    {
                        var b1 = ret.Count;
                        ret[item.Key] = Convert.ToByte(b1 / 256);
                        ret[item.Key + 1] = Convert.ToByte(b1 % 256);
                        ret.AddRange(item.Value);
                    }
                    return ret.ToArray();

                }

                public static Arc1410CanTransferByPartitionReturn Parse(byte[] bytes)
                {
                    var queue = new Queue<byte>(bytes);
                    var prefixOffset = 0;
                    var retPrefix = new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] };
                    if (retPrefix.SequenceEqual(Constants.RetPrefix))
                    {
                        prefixOffset = 4;
                        for (int i = 0; i < 4 && queue.Count > 0; i++) { queue.Dequeue(); }
                    }
                    var ret = new Arc1410CanTransferByPartitionReturn();
                    uint count = 0;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vCode = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte");
                    count = vCode.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueCode = vCode.ToValue();
                    if (valueCode is byte vCodeValue) { ret.Code = vCodeValue; }
                    var indexStatus = queue.Dequeue() * 256 + queue.Dequeue();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vStatus = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("string");
                    vStatus.Decode(bytes.Skip(indexStatus + prefixOffset).ToArray());
                    var valueStatus = vStatus.ToValue();
                    if (valueStatus is string vStatusValue) { ret.Status = vStatusValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vReceiverPartition = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vReceiverPartition.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueReceiverPartition = vReceiverPartition.ToValue();
                    if (valueReceiverPartition is Algorand.Address vReceiverPartitionValue) { ret.ReceiverPartition = vReceiverPartitionValue; }
                    return ret;

                }

                public override string ToString()
                {
                    return $"{this.GetType().ToString()} {BitConverter.ToString(ToByteArray()).Replace("-", "")}";
                }
                public override bool Equals(object? obj)
                {
                    return Equals(obj as Arc1410CanTransferByPartitionReturn);
                }
                public bool Equals(Arc1410CanTransferByPartitionReturn? other)
                {
                    return other is not null && ToByteArray().SequenceEqual(other.ToByteArray());
                }
                public override int GetHashCode()
                {
                    return ToByteArray().GetHashCode();
                }
                public static bool operator ==(Arc1410CanTransferByPartitionReturn left, Arc1410CanTransferByPartitionReturn right)
                {
                    return EqualityComparer<Arc1410CanTransferByPartitionReturn>.Default.Equals(left, right);
                }
                public static bool operator !=(Arc1410CanTransferByPartitionReturn left, Arc1410CanTransferByPartitionReturn right)
                {
                    return !(left == right);
                }

            }

            public class Arc1410PartitionIssue : AVMObjectType
            {
                public Algorand.Address To { get; set; }

                public Algorand.Address Partition { get; set; }

                public AVM.ClientGenerator.ABI.ARC4.Types.UInt256 Amount { get; set; }

                public byte[] Data { get; set; }

                public byte[] ToByteArray()
                {
                    var ret = new List<byte>();
                    var stringRef = new Dictionary<int, byte[]>();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vTo = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vTo.From(To);
                    ret.AddRange(vTo.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vPartition = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vPartition.From(Partition);
                    ret.AddRange(vPartition.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vAmount = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("uint256");
                    vAmount.From(Amount);
                    ret.AddRange(vAmount.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    vData.From(Data);
                    ret.AddRange(vData.Encode());
                    foreach (var item in stringRef)
                    {
                        var b1 = ret.Count;
                        ret[item.Key] = Convert.ToByte(b1 / 256);
                        ret[item.Key + 1] = Convert.ToByte(b1 % 256);
                        ret.AddRange(item.Value);
                    }
                    return ret.ToArray();

                }

                public static Arc1410PartitionIssue Parse(byte[] bytes)
                {
                    var queue = new Queue<byte>(bytes);
                    var prefixOffset = 0;
                    var retPrefix = new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] };
                    if (retPrefix.SequenceEqual(Constants.RetPrefix))
                    {
                        prefixOffset = 4;
                        for (int i = 0; i < 4 && queue.Count > 0; i++) { queue.Dequeue(); }
                    }
                    var ret = new Arc1410PartitionIssue();
                    uint count = 0;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vTo = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vTo.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueTo = vTo.ToValue();
                    if (valueTo is Algorand.Address vToValue) { ret.To = vToValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vPartition = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vPartition.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valuePartition = vPartition.ToValue();
                    if (valuePartition is Algorand.Address vPartitionValue) { ret.Partition = vPartitionValue; }
                    var vAmount = new AVM.ClientGenerator.ABI.ARC4.Types.UInt256();
                    count = vAmount.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    ret.Amount = vAmount;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    count = vData.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueData = vData.ToValue();
                    if (valueData is byte[] vDataValue) { ret.Data = vDataValue; }
                    return ret;

                }

                public override string ToString()
                {
                    return $"{this.GetType().ToString()} {BitConverter.ToString(ToByteArray()).Replace("-", "")}";
                }
                public override bool Equals(object? obj)
                {
                    return Equals(obj as Arc1410PartitionIssue);
                }
                public bool Equals(Arc1410PartitionIssue? other)
                {
                    return other is not null && ToByteArray().SequenceEqual(other.ToByteArray());
                }
                public override int GetHashCode()
                {
                    return ToByteArray().GetHashCode();
                }
                public static bool operator ==(Arc1410PartitionIssue left, Arc1410PartitionIssue right)
                {
                    return EqualityComparer<Arc1410PartitionIssue>.Default.Equals(left, right);
                }
                public static bool operator !=(Arc1410PartitionIssue left, Arc1410PartitionIssue right)
                {
                    return !(left == right);
                }

            }

            public class Arc1410PartitionRedeem : AVMObjectType
            {
                public Algorand.Address From { get; set; }

                public Algorand.Address Partition { get; set; }

                public AVM.ClientGenerator.ABI.ARC4.Types.UInt256 Amount { get; set; }

                public byte[] Data { get; set; }

                public byte[] ToByteArray()
                {
                    var ret = new List<byte>();
                    var stringRef = new Dictionary<int, byte[]>();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vFrom = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vFrom.From(From);
                    ret.AddRange(vFrom.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vPartition = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vPartition.From(Partition);
                    ret.AddRange(vPartition.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vAmount = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("uint256");
                    vAmount.From(Amount);
                    ret.AddRange(vAmount.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    vData.From(Data);
                    ret.AddRange(vData.Encode());
                    foreach (var item in stringRef)
                    {
                        var b1 = ret.Count;
                        ret[item.Key] = Convert.ToByte(b1 / 256);
                        ret[item.Key + 1] = Convert.ToByte(b1 % 256);
                        ret.AddRange(item.Value);
                    }
                    return ret.ToArray();

                }

                public static Arc1410PartitionRedeem Parse(byte[] bytes)
                {
                    var queue = new Queue<byte>(bytes);
                    var prefixOffset = 0;
                    var retPrefix = new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] };
                    if (retPrefix.SequenceEqual(Constants.RetPrefix))
                    {
                        prefixOffset = 4;
                        for (int i = 0; i < 4 && queue.Count > 0; i++) { queue.Dequeue(); }
                    }
                    var ret = new Arc1410PartitionRedeem();
                    uint count = 0;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vFrom = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vFrom.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueFrom = vFrom.ToValue();
                    if (valueFrom is Algorand.Address vFromValue) { ret.From = vFromValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vPartition = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vPartition.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valuePartition = vPartition.ToValue();
                    if (valuePartition is Algorand.Address vPartitionValue) { ret.Partition = vPartitionValue; }
                    var vAmount = new AVM.ClientGenerator.ABI.ARC4.Types.UInt256();
                    count = vAmount.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    ret.Amount = vAmount;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    count = vData.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueData = vData.ToValue();
                    if (valueData is byte[] vDataValue) { ret.Data = vDataValue; }
                    return ret;

                }

                public override string ToString()
                {
                    return $"{this.GetType().ToString()} {BitConverter.ToString(ToByteArray()).Replace("-", "")}";
                }
                public override bool Equals(object? obj)
                {
                    return Equals(obj as Arc1410PartitionRedeem);
                }
                public bool Equals(Arc1410PartitionRedeem? other)
                {
                    return other is not null && ToByteArray().SequenceEqual(other.ToByteArray());
                }
                public override int GetHashCode()
                {
                    return ToByteArray().GetHashCode();
                }
                public static bool operator ==(Arc1410PartitionRedeem left, Arc1410PartitionRedeem right)
                {
                    return EqualityComparer<Arc1410PartitionRedeem>.Default.Equals(left, right);
                }
                public static bool operator !=(Arc1410PartitionRedeem left, Arc1410PartitionRedeem right)
                {
                    return !(left == right);
                }

            }

            public class Arc1410PartitionTransfer : AVMObjectType
            {
                public Algorand.Address From { get; set; }

                public Algorand.Address To { get; set; }

                public Algorand.Address Partition { get; set; }

                public AVM.ClientGenerator.ABI.ARC4.Types.UInt256 Amount { get; set; }

                public byte[] Data { get; set; }

                public byte[] ToByteArray()
                {
                    var ret = new List<byte>();
                    var stringRef = new Dictionary<int, byte[]>();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vFrom = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vFrom.From(From);
                    ret.AddRange(vFrom.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vTo = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vTo.From(To);
                    ret.AddRange(vTo.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vPartition = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vPartition.From(Partition);
                    ret.AddRange(vPartition.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vAmount = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("uint256");
                    vAmount.From(Amount);
                    ret.AddRange(vAmount.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    vData.From(Data);
                    ret.AddRange(vData.Encode());
                    foreach (var item in stringRef)
                    {
                        var b1 = ret.Count;
                        ret[item.Key] = Convert.ToByte(b1 / 256);
                        ret[item.Key + 1] = Convert.ToByte(b1 % 256);
                        ret.AddRange(item.Value);
                    }
                    return ret.ToArray();

                }

                public static Arc1410PartitionTransfer Parse(byte[] bytes)
                {
                    var queue = new Queue<byte>(bytes);
                    var prefixOffset = 0;
                    var retPrefix = new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] };
                    if (retPrefix.SequenceEqual(Constants.RetPrefix))
                    {
                        prefixOffset = 4;
                        for (int i = 0; i < 4 && queue.Count > 0; i++) { queue.Dequeue(); }
                    }
                    var ret = new Arc1410PartitionTransfer();
                    uint count = 0;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vFrom = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vFrom.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueFrom = vFrom.ToValue();
                    if (valueFrom is Algorand.Address vFromValue) { ret.From = vFromValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vTo = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vTo.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueTo = vTo.ToValue();
                    if (valueTo is Algorand.Address vToValue) { ret.To = vToValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vPartition = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vPartition.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valuePartition = vPartition.ToValue();
                    if (valuePartition is Algorand.Address vPartitionValue) { ret.Partition = vPartitionValue; }
                    var vAmount = new AVM.ClientGenerator.ABI.ARC4.Types.UInt256();
                    count = vAmount.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    ret.Amount = vAmount;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    count = vData.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueData = vData.ToValue();
                    if (valueData is byte[] vDataValue) { ret.Data = vDataValue; }
                    return ret;

                }

                public override string ToString()
                {
                    return $"{this.GetType().ToString()} {BitConverter.ToString(ToByteArray()).Replace("-", "")}";
                }
                public override bool Equals(object? obj)
                {
                    return Equals(obj as Arc1410PartitionTransfer);
                }
                public bool Equals(Arc1410PartitionTransfer? other)
                {
                    return other is not null && ToByteArray().SequenceEqual(other.ToByteArray());
                }
                public override int GetHashCode()
                {
                    return ToByteArray().GetHashCode();
                }
                public static bool operator ==(Arc1410PartitionTransfer left, Arc1410PartitionTransfer right)
                {
                    return EqualityComparer<Arc1410PartitionTransfer>.Default.Equals(left, right);
                }
                public static bool operator !=(Arc1410PartitionTransfer left, Arc1410PartitionTransfer right)
                {
                    return !(left == right);
                }

            }

            public class Arc1594IssueEvent : AVMObjectType
            {
                public Algorand.Address To { get; set; }

                public AVM.ClientGenerator.ABI.ARC4.Types.UInt256 Amount { get; set; }

                public byte[] Data { get; set; }

                public byte[] ToByteArray()
                {
                    var ret = new List<byte>();
                    var stringRef = new Dictionary<int, byte[]>();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vTo = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vTo.From(To);
                    ret.AddRange(vTo.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vAmount = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("uint256");
                    vAmount.From(Amount);
                    ret.AddRange(vAmount.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    vData.From(Data);
                    ret.AddRange(vData.Encode());
                    foreach (var item in stringRef)
                    {
                        var b1 = ret.Count;
                        ret[item.Key] = Convert.ToByte(b1 / 256);
                        ret[item.Key + 1] = Convert.ToByte(b1 % 256);
                        ret.AddRange(item.Value);
                    }
                    return ret.ToArray();

                }

                public static Arc1594IssueEvent Parse(byte[] bytes)
                {
                    var queue = new Queue<byte>(bytes);
                    var prefixOffset = 0;
                    var retPrefix = new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] };
                    if (retPrefix.SequenceEqual(Constants.RetPrefix))
                    {
                        prefixOffset = 4;
                        for (int i = 0; i < 4 && queue.Count > 0; i++) { queue.Dequeue(); }
                    }
                    var ret = new Arc1594IssueEvent();
                    uint count = 0;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vTo = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vTo.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueTo = vTo.ToValue();
                    if (valueTo is Algorand.Address vToValue) { ret.To = vToValue; }
                    var vAmount = new AVM.ClientGenerator.ABI.ARC4.Types.UInt256();
                    count = vAmount.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    ret.Amount = vAmount;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    count = vData.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueData = vData.ToValue();
                    if (valueData is byte[] vDataValue) { ret.Data = vDataValue; }
                    return ret;

                }

                public override string ToString()
                {
                    return $"{this.GetType().ToString()} {BitConverter.ToString(ToByteArray()).Replace("-", "")}";
                }
                public override bool Equals(object? obj)
                {
                    return Equals(obj as Arc1594IssueEvent);
                }
                public bool Equals(Arc1594IssueEvent? other)
                {
                    return other is not null && ToByteArray().SequenceEqual(other.ToByteArray());
                }
                public override int GetHashCode()
                {
                    return ToByteArray().GetHashCode();
                }
                public static bool operator ==(Arc1594IssueEvent left, Arc1594IssueEvent right)
                {
                    return EqualityComparer<Arc1594IssueEvent>.Default.Equals(left, right);
                }
                public static bool operator !=(Arc1594IssueEvent left, Arc1594IssueEvent right)
                {
                    return !(left == right);
                }

            }

            public class Arc1594RedeemEvent : AVMObjectType
            {
                public Algorand.Address From { get; set; }

                public AVM.ClientGenerator.ABI.ARC4.Types.UInt256 Amount { get; set; }

                public byte[] Data { get; set; }

                public byte[] ToByteArray()
                {
                    var ret = new List<byte>();
                    var stringRef = new Dictionary<int, byte[]>();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vFrom = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vFrom.From(From);
                    ret.AddRange(vFrom.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vAmount = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("uint256");
                    vAmount.From(Amount);
                    ret.AddRange(vAmount.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    vData.From(Data);
                    ret.AddRange(vData.Encode());
                    foreach (var item in stringRef)
                    {
                        var b1 = ret.Count;
                        ret[item.Key] = Convert.ToByte(b1 / 256);
                        ret[item.Key + 1] = Convert.ToByte(b1 % 256);
                        ret.AddRange(item.Value);
                    }
                    return ret.ToArray();

                }

                public static Arc1594RedeemEvent Parse(byte[] bytes)
                {
                    var queue = new Queue<byte>(bytes);
                    var prefixOffset = 0;
                    var retPrefix = new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] };
                    if (retPrefix.SequenceEqual(Constants.RetPrefix))
                    {
                        prefixOffset = 4;
                        for (int i = 0; i < 4 && queue.Count > 0; i++) { queue.Dequeue(); }
                    }
                    var ret = new Arc1594RedeemEvent();
                    uint count = 0;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vFrom = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vFrom.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueFrom = vFrom.ToValue();
                    if (valueFrom is Algorand.Address vFromValue) { ret.From = vFromValue; }
                    var vAmount = new AVM.ClientGenerator.ABI.ARC4.Types.UInt256();
                    count = vAmount.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    ret.Amount = vAmount;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    count = vData.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueData = vData.ToValue();
                    if (valueData is byte[] vDataValue) { ret.Data = vDataValue; }
                    return ret;

                }

                public override string ToString()
                {
                    return $"{this.GetType().ToString()} {BitConverter.ToString(ToByteArray()).Replace("-", "")}";
                }
                public override bool Equals(object? obj)
                {
                    return Equals(obj as Arc1594RedeemEvent);
                }
                public bool Equals(Arc1594RedeemEvent? other)
                {
                    return other is not null && ToByteArray().SequenceEqual(other.ToByteArray());
                }
                public override int GetHashCode()
                {
                    return ToByteArray().GetHashCode();
                }
                public static bool operator ==(Arc1594RedeemEvent left, Arc1594RedeemEvent right)
                {
                    return EqualityComparer<Arc1594RedeemEvent>.Default.Equals(left, right);
                }
                public static bool operator !=(Arc1594RedeemEvent left, Arc1594RedeemEvent right)
                {
                    return !(left == right);
                }

            }

            public class Arc1644ControllerChangedEvent : AVMObjectType
            {
                public Algorand.Address Old { get; set; }

                public Algorand.Address Neu { get; set; }

                public byte[] ToByteArray()
                {
                    var ret = new List<byte>();
                    var stringRef = new Dictionary<int, byte[]>();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vOld = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vOld.From(Old);
                    ret.AddRange(vOld.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vNeu = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vNeu.From(Neu);
                    ret.AddRange(vNeu.Encode());
                    foreach (var item in stringRef)
                    {
                        var b1 = ret.Count;
                        ret[item.Key] = Convert.ToByte(b1 / 256);
                        ret[item.Key + 1] = Convert.ToByte(b1 % 256);
                        ret.AddRange(item.Value);
                    }
                    return ret.ToArray();

                }

                public static Arc1644ControllerChangedEvent Parse(byte[] bytes)
                {
                    var queue = new Queue<byte>(bytes);
                    var prefixOffset = 0;
                    var retPrefix = new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] };
                    if (retPrefix.SequenceEqual(Constants.RetPrefix))
                    {
                        prefixOffset = 4;
                        for (int i = 0; i < 4 && queue.Count > 0; i++) { queue.Dequeue(); }
                    }
                    var ret = new Arc1644ControllerChangedEvent();
                    uint count = 0;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vOld = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vOld.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueOld = vOld.ToValue();
                    if (valueOld is Algorand.Address vOldValue) { ret.Old = vOldValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vNeu = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vNeu.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueNeu = vNeu.ToValue();
                    if (valueNeu is Algorand.Address vNeuValue) { ret.Neu = vNeuValue; }
                    return ret;

                }

                public override string ToString()
                {
                    return $"{this.GetType().ToString()} {BitConverter.ToString(ToByteArray()).Replace("-", "")}";
                }
                public override bool Equals(object? obj)
                {
                    return Equals(obj as Arc1644ControllerChangedEvent);
                }
                public bool Equals(Arc1644ControllerChangedEvent? other)
                {
                    return other is not null && ToByteArray().SequenceEqual(other.ToByteArray());
                }
                public override int GetHashCode()
                {
                    return ToByteArray().GetHashCode();
                }
                public static bool operator ==(Arc1644ControllerChangedEvent left, Arc1644ControllerChangedEvent right)
                {
                    return EqualityComparer<Arc1644ControllerChangedEvent>.Default.Equals(left, right);
                }
                public static bool operator !=(Arc1644ControllerChangedEvent left, Arc1644ControllerChangedEvent right)
                {
                    return !(left == right);
                }

            }

            public class Arc1644ControllerRedeemEvent : AVMObjectType
            {
                public Algorand.Address Controller { get; set; }

                public Algorand.Address From { get; set; }

                public AVM.ClientGenerator.ABI.ARC4.Types.UInt256 Amount { get; set; }

                public byte Code { get; set; }

                public byte[] OperatorData { get; set; }

                public byte[] ToByteArray()
                {
                    var ret = new List<byte>();
                    var stringRef = new Dictionary<int, byte[]>();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vController = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vController.From(Controller);
                    ret.AddRange(vController.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vFrom = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vFrom.From(From);
                    ret.AddRange(vFrom.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vAmount = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("uint256");
                    vAmount.From(Amount);
                    ret.AddRange(vAmount.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vCode = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte");
                    vCode.From(Code);
                    ret.AddRange(vCode.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vOperatorData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    vOperatorData.From(OperatorData);
                    ret.AddRange(vOperatorData.Encode());
                    foreach (var item in stringRef)
                    {
                        var b1 = ret.Count;
                        ret[item.Key] = Convert.ToByte(b1 / 256);
                        ret[item.Key + 1] = Convert.ToByte(b1 % 256);
                        ret.AddRange(item.Value);
                    }
                    return ret.ToArray();

                }

                public static Arc1644ControllerRedeemEvent Parse(byte[] bytes)
                {
                    var queue = new Queue<byte>(bytes);
                    var prefixOffset = 0;
                    var retPrefix = new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] };
                    if (retPrefix.SequenceEqual(Constants.RetPrefix))
                    {
                        prefixOffset = 4;
                        for (int i = 0; i < 4 && queue.Count > 0; i++) { queue.Dequeue(); }
                    }
                    var ret = new Arc1644ControllerRedeemEvent();
                    uint count = 0;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vController = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vController.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueController = vController.ToValue();
                    if (valueController is Algorand.Address vControllerValue) { ret.Controller = vControllerValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vFrom = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vFrom.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueFrom = vFrom.ToValue();
                    if (valueFrom is Algorand.Address vFromValue) { ret.From = vFromValue; }
                    var vAmount = new AVM.ClientGenerator.ABI.ARC4.Types.UInt256();
                    count = vAmount.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    ret.Amount = vAmount;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vCode = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte");
                    count = vCode.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueCode = vCode.ToValue();
                    if (valueCode is byte vCodeValue) { ret.Code = vCodeValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vOperatorData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    count = vOperatorData.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueOperatorData = vOperatorData.ToValue();
                    if (valueOperatorData is byte[] vOperatorDataValue) { ret.OperatorData = vOperatorDataValue; }
                    return ret;

                }

                public override string ToString()
                {
                    return $"{this.GetType().ToString()} {BitConverter.ToString(ToByteArray()).Replace("-", "")}";
                }
                public override bool Equals(object? obj)
                {
                    return Equals(obj as Arc1644ControllerRedeemEvent);
                }
                public bool Equals(Arc1644ControllerRedeemEvent? other)
                {
                    return other is not null && ToByteArray().SequenceEqual(other.ToByteArray());
                }
                public override int GetHashCode()
                {
                    return ToByteArray().GetHashCode();
                }
                public static bool operator ==(Arc1644ControllerRedeemEvent left, Arc1644ControllerRedeemEvent right)
                {
                    return EqualityComparer<Arc1644ControllerRedeemEvent>.Default.Equals(left, right);
                }
                public static bool operator !=(Arc1644ControllerRedeemEvent left, Arc1644ControllerRedeemEvent right)
                {
                    return !(left == right);
                }

            }

            public class Arc1644ControllerTransferEvent : AVMObjectType
            {
                public Algorand.Address Controller { get; set; }

                public Algorand.Address From { get; set; }

                public Algorand.Address To { get; set; }

                public AVM.ClientGenerator.ABI.ARC4.Types.UInt256 Amount { get; set; }

                public byte Code { get; set; }

                public byte[] Data { get; set; }

                public byte[] OperatorData { get; set; }

                public byte[] ToByteArray()
                {
                    var ret = new List<byte>();
                    var stringRef = new Dictionary<int, byte[]>();
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vController = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vController.From(Controller);
                    ret.AddRange(vController.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vFrom = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vFrom.From(From);
                    ret.AddRange(vFrom.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vTo = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    vTo.From(To);
                    ret.AddRange(vTo.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vAmount = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("uint256");
                    vAmount.From(Amount);
                    ret.AddRange(vAmount.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vCode = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte");
                    vCode.From(Code);
                    ret.AddRange(vCode.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    vData.From(Data);
                    ret.AddRange(vData.Encode());
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vOperatorData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    vOperatorData.From(OperatorData);
                    ret.AddRange(vOperatorData.Encode());
                    foreach (var item in stringRef)
                    {
                        var b1 = ret.Count;
                        ret[item.Key] = Convert.ToByte(b1 / 256);
                        ret[item.Key + 1] = Convert.ToByte(b1 % 256);
                        ret.AddRange(item.Value);
                    }
                    return ret.ToArray();

                }

                public static Arc1644ControllerTransferEvent Parse(byte[] bytes)
                {
                    var queue = new Queue<byte>(bytes);
                    var prefixOffset = 0;
                    var retPrefix = new byte[4] { bytes[0], bytes[1], bytes[2], bytes[3] };
                    if (retPrefix.SequenceEqual(Constants.RetPrefix))
                    {
                        prefixOffset = 4;
                        for (int i = 0; i < 4 && queue.Count > 0; i++) { queue.Dequeue(); }
                    }
                    var ret = new Arc1644ControllerTransferEvent();
                    uint count = 0;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vController = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vController.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueController = vController.ToValue();
                    if (valueController is Algorand.Address vControllerValue) { ret.Controller = vControllerValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vFrom = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vFrom.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueFrom = vFrom.ToValue();
                    if (valueFrom is Algorand.Address vFromValue) { ret.From = vFromValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vTo = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("address");
                    count = vTo.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueTo = vTo.ToValue();
                    if (valueTo is Algorand.Address vToValue) { ret.To = vToValue; }
                    var vAmount = new AVM.ClientGenerator.ABI.ARC4.Types.UInt256();
                    count = vAmount.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    ret.Amount = vAmount;
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vCode = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte");
                    count = vCode.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueCode = vCode.ToValue();
                    if (valueCode is byte vCodeValue) { ret.Code = vCodeValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    count = vData.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueData = vData.ToValue();
                    if (valueData is byte[] vDataValue) { ret.Data = vDataValue; }
                    AVM.ClientGenerator.ABI.ARC4.Types.WireType vOperatorData = AVM.ClientGenerator.ABI.ARC4.Types.WireType.FromABIDescription("byte[]");
                    count = vOperatorData.Decode(queue.ToArray());
                    for (int i = 0; i < Convert.ToInt32(count); i++) { queue.Dequeue(); }
                    var valueOperatorData = vOperatorData.ToValue();
                    if (valueOperatorData is byte[] vOperatorDataValue) { ret.OperatorData = vOperatorDataValue; }
                    return ret;

                }

                public override string ToString()
                {
                    return $"{this.GetType().ToString()} {BitConverter.ToString(ToByteArray()).Replace("-", "")}";
                }
                public override bool Equals(object? obj)
                {
                    return Equals(obj as Arc1644ControllerTransferEvent);
                }
                public bool Equals(Arc1644ControllerTransferEvent? other)
                {
                    return other is not null && ToByteArray().SequenceEqual(other.ToByteArray());
                }
                public override int GetHashCode()
                {
                    return ToByteArray().GetHashCode();
                }
                public static bool operator ==(Arc1644ControllerTransferEvent left, Arc1644ControllerTransferEvent right)
                {
                    return EqualityComparer<Arc1644ControllerTransferEvent>.Default.Equals(left, right);
                }
                public static bool operator !=(Arc1644ControllerTransferEvent left, Arc1644ControllerTransferEvent right)
                {
                    return !(left == right);
                }

            }

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="new_controller"> </param>
        public async Task Arc1644SetController(Address new_controller, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { new_controller });
            byte[] abiHandle = { 4, 84, 114, 208 };
            var new_controllerAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); new_controllerAbi.From(new_controller);

            var result = await base.CallApp(new List<object> { abiHandle, new_controllerAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc1644SetController_Transactions(Address new_controller, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 4, 84, 114, 208 };
            var new_controllerAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); new_controllerAbi.From(new_controller);

            return await base.MakeTransactionList(new List<object> { abiHandle, new_controllerAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="flag"> </param>
        public async Task Arc1644SetControllable(bool flag, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 125, 121, 4, 164 };
            var flagAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Bool(); flagAbi.From(flag);

            var result = await base.CallApp(new List<object> { abiHandle, flagAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc1644SetControllable_Transactions(bool flag, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 125, 121, 4, 164 };
            var flagAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Bool(); flagAbi.From(flag);

            return await base.MakeTransactionList(new List<object> { abiHandle, flagAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="flag"> </param>
        public async Task Arc1644SetRequireJustification(bool flag, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 230, 244, 248, 97 };
            var flagAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Bool(); flagAbi.From(flag);

            var result = await base.CallApp(new List<object> { abiHandle, flagAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc1644SetRequireJustification_Transactions(bool flag, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 230, 244, 248, 97 };
            var flagAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Bool(); flagAbi.From(flag);

            return await base.MakeTransactionList(new List<object> { abiHandle, flagAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="interval"> </param>
        public async Task Arc1644SetMinActionInterval(ulong interval, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 46, 189, 45, 52 };
            var intervalAbi = new AVM.ClientGenerator.ABI.ARC4.Types.UInt64(); intervalAbi.From(interval);

            var result = await base.CallApp(new List<object> { abiHandle, intervalAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc1644SetMinActionInterval_Transactions(ulong interval, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 46, 189, 45, 52 };
            var intervalAbi = new AVM.ClientGenerator.ABI.ARC4.Types.UInt64(); intervalAbi.From(interval);

            return await base.MakeTransactionList(new List<object> { abiHandle, intervalAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        public async Task<ulong> Arc1644IsControllable(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 238, 111, 45, 14 };

            var result = await base.SimApp(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.UInt64();
            returnValueObj.Decode(lastLogReturnData);
            return BitConverter.ToUInt64(ReverseIfLittleEndian(lastLogReturnData), 0);

        }

        public async Task<List<Transaction>> Arc1644IsControllable_Transactions(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 238, 111, 45, 14 };

            return await base.MakeTransactionList(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="from"> </param>
        /// <param name="to"> </param>
        /// <param name="amount"> </param>
        /// <param name="data"> </param>
        /// <param name="operator_data"> </param>
        public async Task<ulong> Arc1644ControllerTransfer(Address from, Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, byte[] operator_data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { from, to });
            byte[] abiHandle = { 29, 92, 122, 23 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);
            var operator_dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); operator_dataAbi.From(operator_data);

            var result = await base.CallApp(new List<object> { abiHandle, fromAbi, toAbi, amount, dataAbi, operator_dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.UInt64();
            returnValueObj.Decode(lastLogReturnData);
            return BitConverter.ToUInt64(ReverseIfLittleEndian(lastLogReturnData), 0);

        }

        public async Task<List<Transaction>> Arc1644ControllerTransfer_Transactions(Address from, Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, byte[] operator_data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 29, 92, 122, 23 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);
            var operator_dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); operator_dataAbi.From(operator_data);

            return await base.MakeTransactionList(new List<object> { abiHandle, fromAbi, toAbi, amount, dataAbi, operator_dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="from"> </param>
        /// <param name="amount"> </param>
        /// <param name="operator_data"> </param>
        public async Task<ulong> Arc1644ControllerRedeem(Address from, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] operator_data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { from });
            byte[] abiHandle = { 229, 122, 110, 24 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var operator_dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); operator_dataAbi.From(operator_data);

            var result = await base.CallApp(new List<object> { abiHandle, fromAbi, amount, operator_dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.UInt64();
            returnValueObj.Decode(lastLogReturnData);
            return BitConverter.ToUInt64(ReverseIfLittleEndian(lastLogReturnData), 0);

        }

        public async Task<List<Transaction>> Arc1644ControllerRedeem_Transactions(Address from, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] operator_data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 229, 122, 110, 24 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var operator_dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); operator_dataAbi.From(operator_data);

            return await base.MakeTransactionList(new List<object> { abiHandle, fromAbi, amount, operator_dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="flag"> </param>
        public async Task Arc1594SetIssuable(bool flag, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 101, 177, 104, 42 };
            var flagAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Bool(); flagAbi.From(flag);

            var result = await base.CallApp(new List<object> { abiHandle, flagAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc1594SetIssuable_Transactions(bool flag, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 101, 177, 104, 42 };
            var flagAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Bool(); flagAbi.From(flag);

            return await base.MakeTransactionList(new List<object> { abiHandle, flagAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="to"> </param>
        /// <param name="amount"> </param>
        /// <param name="data"> </param>
        public async Task Arc1594Issue(Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { to });
            byte[] abiHandle = { 1, 48, 89, 155 };
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            var result = await base.CallApp(new List<object> { abiHandle, toAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc1594Issue_Transactions(Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 1, 48, 89, 155 };
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            return await base.MakeTransactionList(new List<object> { abiHandle, toAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="from"> </param>
        /// <param name="amount"> </param>
        /// <param name="data"> </param>
        public async Task Arc1594RedeemFrom(Address from, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { from });
            byte[] abiHandle = { 20, 43, 95, 203 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            var result = await base.CallApp(new List<object> { abiHandle, fromAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc1594RedeemFrom_Transactions(Address from, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 20, 43, 95, 203 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            return await base.MakeTransactionList(new List<object> { abiHandle, fromAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="amount"> </param>
        /// <param name="data"> </param>
        public async Task Arc1594Redeem(AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 248, 131, 142, 185 };
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            var result = await base.CallApp(new List<object> { abiHandle, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc1594Redeem_Transactions(AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 248, 131, 142, 185 };
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            return await base.MakeTransactionList(new List<object> { abiHandle, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="to"> </param>
        /// <param name="amount"> </param>
        /// <param name="data"> </param>
        public async Task<bool> Arc1594TransferWithData(Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { to });
            byte[] abiHandle = { 49, 136, 43, 250 };
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            var result = await base.CallApp(new List<object> { abiHandle, toAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.Bool();
            returnValueObj.Decode(lastLogReturnData);
            return BitConverter.ToBoolean(ReverseIfLittleEndian(lastLogReturnData), 0);

        }

        public async Task<List<Transaction>> Arc1594TransferWithData_Transactions(Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 49, 136, 43, 250 };
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            return await base.MakeTransactionList(new List<object> { abiHandle, toAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="from"> </param>
        /// <param name="to"> </param>
        /// <param name="amount"> </param>
        /// <param name="data"> </param>
        public async Task<bool> Arc1594TransferFromWithData(Address from, Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { from, to });
            byte[] abiHandle = { 169, 204, 161, 111 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            var result = await base.CallApp(new List<object> { abiHandle, fromAbi, toAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.Bool();
            returnValueObj.Decode(lastLogReturnData);
            return BitConverter.ToBoolean(ReverseIfLittleEndian(lastLogReturnData), 0);

        }

        public async Task<List<Transaction>> Arc1594TransferFromWithData_Transactions(Address from, Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 169, 204, 161, 111 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            return await base.MakeTransactionList(new List<object> { abiHandle, fromAbi, toAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        public async Task<bool> Arc1594IsIssuable(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 38, 101, 151, 192 };

            var result = await base.SimApp(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.Bool();
            returnValueObj.Decode(lastLogReturnData);
            return BitConverter.ToBoolean(ReverseIfLittleEndian(lastLogReturnData), 0);

        }

        public async Task<List<Transaction>> Arc1594IsIssuable_Transactions(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 38, 101, 151, 192 };

            return await base.MakeTransactionList(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="holder"> </param>
        /// <param name="partition"> </param>
        public async Task<AVM.ClientGenerator.ABI.ARC4.Types.UInt256> Arc1410BalanceOfPartition(Address holder, Address partition, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { holder, partition });
            byte[] abiHandle = { 53, 248, 19, 95 };
            var holderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); holderAbi.From(holder);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);

            var result = await base.SimApp(new List<object> { abiHandle, holderAbi, partitionAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.UInt256();
            returnValueObj.Decode(lastLogReturnData);
            return returnValueObj;

        }

        public async Task<List<Transaction>> Arc1410BalanceOfPartition_Transactions(Address holder, Address partition, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 53, 248, 19, 95 };
            var holderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); holderAbi.From(holder);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);

            return await base.MakeTransactionList(new List<object> { abiHandle, holderAbi, partitionAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="to"> </param>
        /// <param name="value"> </param>
        public async Task<bool> Arc200Transfer(Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 value, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { to });
            byte[] abiHandle = { 218, 112, 37, 185 };
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);

            var result = await base.CallApp(new List<object> { abiHandle, toAbi, value }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.Bool();
            returnValueObj.Decode(lastLogReturnData);
            return BitConverter.ToBoolean(ReverseIfLittleEndian(lastLogReturnData), 0);

        }

        public async Task<List<Transaction>> Arc200Transfer_Transactions(Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 value, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 218, 112, 37, 185 };
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);

            return await base.MakeTransactionList(new List<object> { abiHandle, toAbi, value }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///Transfer an amount of tokens from partition to receiver. Sender must be msg.sender or authorized operator.
        ///</summary>
        /// <param name="partition"> </param>
        /// <param name="to"> </param>
        /// <param name="amount"> </param>
        /// <param name="data"> </param>
        public async Task<Algorand.Address> Arc1410TransferByPartition(Address partition, Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { partition, to });
            byte[] abiHandle = { 63, 37, 103, 19 };
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            var result = await base.CallApp(new List<object> { abiHandle, partitionAbi, toAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.Address();
            returnValueObj.Decode(lastLogReturnData);
            return new Algorand.Address(returnValueObj.ToByteArray());

        }

        public async Task<List<Transaction>> Arc1410TransferByPartition_Transactions(Address partition, Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 63, 37, 103, 19 };
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            return await base.MakeTransactionList(new List<object> { abiHandle, partitionAbi, toAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="holder"> </param>
        /// <param name="page"> </param>
        public async Task<Algorand.Address[]> Arc1410PartitionsOf(Address holder, ulong page, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { holder });
            byte[] abiHandle = { 149, 180, 249, 227 };
            var holderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); holderAbi.From(holder);
            var pageAbi = new AVM.ClientGenerator.ABI.ARC4.Types.UInt64(); pageAbi.From(page);

            var result = await base.CallApp(new List<object> { abiHandle, holderAbi, pageAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            throw new Exception("Conversion not implemented"); // <unknown return conversion>

        }

        public async Task<List<Transaction>> Arc1410PartitionsOf_Transactions(Address holder, ulong page, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 149, 180, 249, 227 };
            var holderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); holderAbi.From(holder);
            var pageAbi = new AVM.ClientGenerator.ABI.ARC4.Types.UInt64(); pageAbi.From(page);

            return await base.MakeTransactionList(new List<object> { abiHandle, holderAbi, pageAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="holder"> </param>
        /// <param name="operator"> </param>
        /// <param name="partition"> </param>
        public async Task<bool> Arc1410IsOperator(Address holder, Address _operator, Address partition, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { holder,_operator, partition });
            byte[] abiHandle = { 128, 204, 73, 171 };
            var holderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); holderAbi.From(holder);
            var operatorAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); operatorAbi.From(_operator);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);

            var result = await base.SimApp(new List<object> { abiHandle, holderAbi, operatorAbi, partitionAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.Bool();
            returnValueObj.Decode(lastLogReturnData);
            return BitConverter.ToBoolean(ReverseIfLittleEndian(lastLogReturnData), 0);

        }

        public async Task<List<Transaction>> Arc1410IsOperator_Transactions(Address holder, Address _operator, Address partition, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 128, 204, 73, 171 };
            var holderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); holderAbi.From(holder);
            var operatorAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); operatorAbi.From(_operator);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);

            return await base.MakeTransactionList(new List<object> { abiHandle, holderAbi, operatorAbi, partitionAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="holder"> </param>
        /// <param name="operator"> </param>
        /// <param name="partition"> </param>
        public async Task Arc1410AuthorizeOperator(Address holder, Address _operator, Address partition, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { holder,_operator, partition });
            byte[] abiHandle = { 7, 150, 33, 101 };
            var holderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); holderAbi.From(holder);
            var operatorAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); operatorAbi.From(_operator);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);

            var result = await base.CallApp(new List<object> { abiHandle, holderAbi, operatorAbi, partitionAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc1410AuthorizeOperator_Transactions(Address holder, Address _operator, Address partition, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 7, 150, 33, 101 };
            var holderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); holderAbi.From(holder);
            var operatorAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); operatorAbi.From(_operator);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);

            return await base.MakeTransactionList(new List<object> { abiHandle, holderAbi, operatorAbi, partitionAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="holder"> </param>
        /// <param name="operator"> </param>
        /// <param name="partition"> </param>
        public async Task Arc1410RevokeOperator(Address holder, Address _operator, Address partition, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { holder,_operator, partition });
            byte[] abiHandle = { 231, 137, 97, 218 };
            var holderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); holderAbi.From(holder);
            var operatorAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); operatorAbi.From(_operator);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);

            var result = await base.CallApp(new List<object> { abiHandle, holderAbi, operatorAbi, partitionAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc1410RevokeOperator_Transactions(Address holder, Address _operator, Address partition, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 231, 137, 97, 218 };
            var holderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); holderAbi.From(holder);
            var operatorAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); operatorAbi.From(_operator);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);

            return await base.MakeTransactionList(new List<object> { abiHandle, holderAbi, operatorAbi, partitionAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="from"> </param>
        /// <param name="partition"> </param>
        /// <param name="to"> </param>
        /// <param name="amount"> </param>
        /// <param name="data"> </param>
        public async Task<Algorand.Address> Arc1410OperatorTransferByPartition(Address from, Address partition, Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { from, partition, to });
            byte[] abiHandle = { 253, 148, 128, 215 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            var result = await base.CallApp(new List<object> { abiHandle, fromAbi, partitionAbi, toAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.Address();
            returnValueObj.Decode(lastLogReturnData);
            return new Algorand.Address(returnValueObj.ToByteArray());

        }

        public async Task<List<Transaction>> Arc1410OperatorTransferByPartition_Transactions(Address from, Address partition, Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 253, 148, 128, 215 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            return await base.MakeTransactionList(new List<object> { abiHandle, fromAbi, partitionAbi, toAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="from"> </param>
        /// <param name="partition"> </param>
        /// <param name="to"> </param>
        /// <param name="amount"> </param>
        /// <param name="data"> </param>
        public async Task<Structs.Arc1410CanTransferByPartitionReturn> Arc1410CanTransferByPartition(Address from, Address partition, Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { from, partition, to });
            byte[] abiHandle = { 177, 177, 214, 154 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            var result = await base.CallApp(new List<object> { abiHandle, fromAbi, partitionAbi, toAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            return Structs.Arc1410CanTransferByPartitionReturn.Parse(result.Last());

        }

        public async Task<List<Transaction>> Arc1410CanTransferByPartition_Transactions(Address from, Address partition, Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 177, 177, 214, 154 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            return await base.MakeTransactionList(new List<object> { abiHandle, fromAbi, partitionAbi, toAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="holder"> </param>
        /// <param name="operator"> </param>
        /// <param name="partition"> </param>
        /// <param name="amount"> </param>
        public async Task Arc1410AuthorizeOperatorByPortion(Address holder, Address _operator, Address partition, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { holder,_operator, partition });
            byte[] abiHandle = { 193, 190, 215, 137 };
            var holderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); holderAbi.From(holder);
            var operatorAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); operatorAbi.From(_operator);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);

            var result = await base.CallApp(new List<object> { abiHandle, holderAbi, operatorAbi, partitionAbi, amount }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc1410AuthorizeOperatorByPortion_Transactions(Address holder, Address _operator, Address partition, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 193, 190, 215, 137 };
            var holderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); holderAbi.From(holder);
            var operatorAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); operatorAbi.From(_operator);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);

            return await base.MakeTransactionList(new List<object> { abiHandle, holderAbi, operatorAbi, partitionAbi, amount }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="holder"> </param>
        /// <param name="operator"> </param>
        /// <param name="partition"> </param>
        public async Task<bool> Arc1410IsOperatorByPortion(Address holder, Address _operator, Address partition, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { holder,_operator, partition });
            byte[] abiHandle = { 59, 254, 24, 51 };
            var holderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); holderAbi.From(holder);
            var operatorAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); operatorAbi.From(_operator);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);

            var result = await base.SimApp(new List<object> { abiHandle, holderAbi, operatorAbi, partitionAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.Bool();
            returnValueObj.Decode(lastLogReturnData);
            return BitConverter.ToBoolean(ReverseIfLittleEndian(lastLogReturnData), 0);

        }

        public async Task<List<Transaction>> Arc1410IsOperatorByPortion_Transactions(Address holder, Address _operator, Address partition, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 59, 254, 24, 51 };
            var holderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); holderAbi.From(holder);
            var operatorAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); operatorAbi.From(_operator);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);

            return await base.MakeTransactionList(new List<object> { abiHandle, holderAbi, operatorAbi, partitionAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="to"> </param>
        /// <param name="partition"> </param>
        /// <param name="amount"> </param>
        /// <param name="data"> </param>
        public async Task Arc1410IssueByPartition(Address to, Address partition, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { to, partition });
            byte[] abiHandle = { 89, 156, 209, 165 };
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            var result = await base.CallApp(new List<object> { abiHandle, toAbi, partitionAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc1410IssueByPartition_Transactions(Address to, Address partition, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 89, 156, 209, 165 };
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            return await base.MakeTransactionList(new List<object> { abiHandle, toAbi, partitionAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="partition"> </param>
        /// <param name="amount"> </param>
        /// <param name="data"> </param>
        public async Task Arc1410RedeemByPartition(Address partition, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { partition });
            byte[] abiHandle = { 109, 233, 65, 102 };
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            var result = await base.CallApp(new List<object> { abiHandle, partitionAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc1410RedeemByPartition_Transactions(Address partition, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 109, 233, 65, 102 };
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            return await base.MakeTransactionList(new List<object> { abiHandle, partitionAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="from"> </param>
        /// <param name="partition"> </param>
        /// <param name="amount"> </param>
        /// <param name="data"> </param>
        public async Task Arc1410OperatorRedeemByPartition(Address from, Address partition, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { from, partition });
            byte[] abiHandle = { 40, 240, 35, 215 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            var result = await base.CallApp(new List<object> { abiHandle, fromAbi, partitionAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc1410OperatorRedeemByPartition_Transactions(Address from, Address partition, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 amount, byte[] data, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 40, 240, 35, 215 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var partitionAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); partitionAbi.From(partition);
            var dataAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); dataAbi.From(data);

            return await base.MakeTransactionList(new List<object> { abiHandle, fromAbi, partitionAbi, amount, dataAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="name"> </param>
        /// <param name="symbol"> </param>
        /// <param name="decimals"> </param>
        /// <param name="totalSupply"> </param>
        public async Task<bool> Bootstrap(byte[] name, byte[] symbol, byte decimals, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 totalSupply, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 151, 83, 130, 226 };
            var nameAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); nameAbi.From(name);
            var symbolAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); symbolAbi.From(symbol);
            var decimalsAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Byte(); decimalsAbi.From(decimals);

            var result = await base.CallApp(new List<object> { abiHandle, nameAbi, symbolAbi, decimalsAbi, totalSupply }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.Bool();
            returnValueObj.Decode(lastLogReturnData);
            return BitConverter.ToBoolean(ReverseIfLittleEndian(lastLogReturnData), 0);

        }

        public async Task<List<Transaction>> Bootstrap_Transactions(byte[] name, byte[] symbol, byte decimals, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 totalSupply, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 151, 83, 130, 226 };
            var nameAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); nameAbi.From(name);
            var symbolAbi = new AVM.ClientGenerator.ABI.ARC4.Types.VariableArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(); symbolAbi.From(symbol);
            var decimalsAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Byte(); decimalsAbi.From(decimals);

            return await base.MakeTransactionList(new List<object> { abiHandle, nameAbi, symbolAbi, decimalsAbi, totalSupply }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///Returns the name of the token
        ///</summary>
        public async Task<byte[]> Arc200Name(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 101, 125, 19, 236 };

            var result = await base.SimApp(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.FixedArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(32);
            returnValueObj.Decode(lastLogReturnData);
            return returnValueObj.ToByteArray();

        }

        public async Task<List<Transaction>> Arc200Name_Transactions(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 101, 125, 19, 236 };

            return await base.MakeTransactionList(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///Returns the symbol of the token
        ///</summary>
        public async Task<byte[]> Arc200Symbol(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 182, 174, 26, 37 };

            var result = await base.SimApp(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.FixedArray<AVM.ClientGenerator.ABI.ARC4.Types.Byte>(8);
            returnValueObj.Decode(lastLogReturnData);
            return returnValueObj.ToByteArray();

        }

        public async Task<List<Transaction>> Arc200Symbol_Transactions(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 182, 174, 26, 37 };

            return await base.MakeTransactionList(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///Returns the decimals of the token
        ///</summary>
        public async Task<byte> Arc200Decimals(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 132, 236, 19, 213 };

            var result = await base.SimApp(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.Byte();
            returnValueObj.Decode(lastLogReturnData);
            return ReverseIfLittleEndian(lastLogReturnData)[0];

        }

        public async Task<List<Transaction>> Arc200Decimals_Transactions(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 132, 236, 19, 213 };

            return await base.MakeTransactionList(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///Returns the total supply of the token
        ///</summary>
        public async Task<AVM.ClientGenerator.ABI.ARC4.Types.UInt256> Arc200TotalSupply(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 236, 153, 96, 65 };

            var result = await base.SimApp(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.UInt256();
            returnValueObj.Decode(lastLogReturnData);
            return returnValueObj;

        }

        public async Task<List<Transaction>> Arc200TotalSupply_Transactions(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 236, 153, 96, 65 };

            return await base.MakeTransactionList(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///Returns the current balance of the owner of the token
        ///</summary>
        /// <param name="owner">The address of the owner of the token </param>
        public async Task<AVM.ClientGenerator.ABI.ARC4.Types.UInt256> Arc200BalanceOf(Address owner, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { owner });
            byte[] abiHandle = { 130, 229, 115, 196 };
            var ownerAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); ownerAbi.From(owner);

            var result = await base.SimApp(new List<object> { abiHandle, ownerAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.UInt256();
            returnValueObj.Decode(lastLogReturnData);
            return returnValueObj;

        }

        public async Task<List<Transaction>> Arc200BalanceOf_Transactions(Address owner, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 130, 229, 115, 196 };
            var ownerAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); ownerAbi.From(owner);

            return await base.MakeTransactionList(new List<object> { abiHandle, ownerAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///Transfers tokens from source to destination as approved spender
        ///</summary>
        /// <param name="from">The source of the transfer </param>
        /// <param name="to">The destination of the transfer </param>
        /// <param name="value">Amount of tokens to transfer </param>
        public async Task<bool> Arc200TransferFrom(Address from, Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 value, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { from, to });
            byte[] abiHandle = { 74, 150, 143, 143 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);

            var result = await base.CallApp(new List<object> { abiHandle, fromAbi, toAbi, value }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.Bool();
            returnValueObj.Decode(lastLogReturnData);
            return BitConverter.ToBoolean(ReverseIfLittleEndian(lastLogReturnData), 0);

        }

        public async Task<List<Transaction>> Arc200TransferFrom_Transactions(Address from, Address to, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 value, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 74, 150, 143, 143 };
            var fromAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); fromAbi.From(from);
            var toAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); toAbi.From(to);

            return await base.MakeTransactionList(new List<object> { abiHandle, fromAbi, toAbi, value }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///Approve spender for a token
        ///</summary>
        /// <param name="spender">Who is allowed to take tokens on owner's behalf </param>
        /// <param name="value">Amount of tokens to be taken by spender </param>
        public async Task<bool> Arc200Approve(Address spender, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 value, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { spender });
            byte[] abiHandle = { 181, 66, 33, 37 };
            var spenderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); spenderAbi.From(spender);

            var result = await base.CallApp(new List<object> { abiHandle, spenderAbi, value }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.Bool();
            returnValueObj.Decode(lastLogReturnData);
            return BitConverter.ToBoolean(ReverseIfLittleEndian(lastLogReturnData), 0);

        }

        public async Task<List<Transaction>> Arc200Approve_Transactions(Address spender, AVM.ClientGenerator.ABI.ARC4.Types.UInt256 value, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 181, 66, 33, 37 };
            var spenderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); spenderAbi.From(spender);

            return await base.MakeTransactionList(new List<object> { abiHandle, spenderAbi, value }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///Returns the current allowance of the spender of the tokens of the owner
        ///</summary>
        /// <param name="owner">Owner's account </param>
        /// <param name="spender">Who is allowed to take tokens on owner's behalf </param>
        public async Task<AVM.ClientGenerator.ABI.ARC4.Types.UInt256> Arc200Allowance(Address owner, Address spender, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { owner, spender });
            byte[] abiHandle = { 187, 179, 25, 243 };
            var ownerAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); ownerAbi.From(owner);
            var spenderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); spenderAbi.From(spender);

            var result = await base.SimApp(new List<object> { abiHandle, ownerAbi, spenderAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.UInt256();
            returnValueObj.Decode(lastLogReturnData);
            return returnValueObj;

        }

        public async Task<List<Transaction>> Arc200Allowance_Transactions(Address owner, Address spender, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 187, 179, 25, 243 };
            var ownerAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); ownerAbi.From(owner);
            var spenderAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); spenderAbi.From(spender);

            return await base.MakeTransactionList(new List<object> { abiHandle, ownerAbi, spenderAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        public async Task<Algorand.Address> Arc88Owner(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 7, 2, 101, 78 };

            var result = await base.SimApp(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.Address();
            returnValueObj.Decode(lastLogReturnData);
            return new Algorand.Address(returnValueObj.ToByteArray());

        }

        public async Task<List<Transaction>> Arc88Owner_Transactions(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 7, 2, 101, 78 };

            return await base.MakeTransactionList(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="query"> </param>
        public async Task<bool> Arc88IsOwner(Address query, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { query });
            byte[] abiHandle = { 208, 21, 114, 78 };
            var queryAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); queryAbi.From(query);

            var result = await base.SimApp(new List<object> { abiHandle, queryAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);
            var lastLogBytes = result.Last();
            if (lastLogBytes.Length < 4 || lastLogBytes[0] != 21 || lastLogBytes[1] != 31 || lastLogBytes[2] != 124 || lastLogBytes[3] != 117) throw new Exception("Invalid ABI handle");
            var lastLogReturnData = lastLogBytes.Skip(4).ToArray();
            var returnValueObj = new AVM.ClientGenerator.ABI.ARC4.Types.Bool();
            returnValueObj.Decode(lastLogReturnData);
            return BitConverter.ToBoolean(ReverseIfLittleEndian(lastLogReturnData), 0);

        }

        public async Task<List<Transaction>> Arc88IsOwner_Transactions(Address query, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 208, 21, 114, 78 };
            var queryAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); queryAbi.From(query);

            return await base.MakeTransactionList(new List<object> { abiHandle, queryAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///Explicit initialization override (creation group recommended). Fails if already initialized.
        ///</summary>
        /// <param name="new_owner"> </param>
        public async Task Arc88InitializeOwner(Address new_owner, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { new_owner });
            byte[] abiHandle = { 2, 159, 236, 192 };
            var new_ownerAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); new_ownerAbi.From(new_owner);

            var result = await base.CallApp(new List<object> { abiHandle, new_ownerAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc88InitializeOwner_Transactions(Address new_owner, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 2, 159, 236, 192 };
            var new_ownerAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); new_ownerAbi.From(new_owner);

            return await base.MakeTransactionList(new List<object> { abiHandle, new_ownerAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="new_owner"> </param>
        public async Task Arc88TransferOwnership(Address new_owner, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { new_owner });
            byte[] abiHandle = { 115, 73, 51, 78 };
            var new_ownerAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); new_ownerAbi.From(new_owner);

            var result = await base.CallApp(new List<object> { abiHandle, new_ownerAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc88TransferOwnership_Transactions(Address new_owner, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 115, 73, 51, 78 };
            var new_ownerAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); new_ownerAbi.From(new_owner);

            return await base.MakeTransactionList(new List<object> { abiHandle, new_ownerAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        public async Task Arc88RenounceOwnership(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 219, 124, 130, 239 };

            var result = await base.CallApp(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc88RenounceOwnership_Transactions(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 219, 124, 130, 239 };

            return await base.MakeTransactionList(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        /// <param name="pending"> </param>
        public async Task Arc88TransferOwnershipRequest(Address pending, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            _tx_accounts.AddRange(new List<Address> { pending });
            byte[] abiHandle = { 253, 44, 44, 110 };
            var pendingAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); pendingAbi.From(pending);

            var result = await base.CallApp(new List<object> { abiHandle, pendingAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc88TransferOwnershipRequest_Transactions(Address pending, Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 253, 44, 44, 110 };
            var pendingAbi = new AVM.ClientGenerator.ABI.ARC4.Types.Address(); pendingAbi.From(pending);

            return await base.MakeTransactionList(new List<object> { abiHandle, pendingAbi }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        public async Task Arc88AcceptOwnership(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 66, 165, 240, 101 };

            var result = await base.CallApp(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc88AcceptOwnership_Transactions(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 66, 165, 240, 101 };

            return await base.MakeTransactionList(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///
        ///</summary>
        public async Task Arc88CancelOwnershipRequest(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 173, 79, 104, 234 };

            var result = await base.CallApp(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> Arc88CancelOwnershipRequest_Transactions(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.NoOp)
        {
            byte[] abiHandle = { 173, 79, 104, 234 };

            return await base.MakeTransactionList(new List<object> { abiHandle }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        ///<summary>
        ///Constructor Bare Action
        ///</summary>
        public async Task CreateApplication(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.CreateApplication)
        {
            _tx_boxes ??= new List<BoxRef>();
            _tx_transactions ??= new List<Transaction>();
            _tx_assets ??= new List<ulong>();
            _tx_apps ??= new List<ulong>();
            _tx_accounts ??= new List<Address>();
            byte[] abiHandle = { 0, 193, 250, 21 };

            var result = await base.CallApp(new List<object> { }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        public async Task<List<Transaction>> CreateApplication_Transactions(Account _tx_sender, ulong? _tx_fee, string _tx_note = "", ulong _tx_roundValidity = 1000, List<BoxRef>? _tx_boxes = null, List<Transaction>? _tx_transactions = null, List<ulong>? _tx_assets = null, List<ulong>? _tx_apps = null, List<Address>? _tx_accounts = null, AVM.ClientGenerator.Core.OnCompleteType _tx_callType = AVM.ClientGenerator.Core.OnCompleteType.CreateApplication)
        {
            byte[] abiHandle = { 0, 193, 250, 21 };

            return await base.MakeTransactionList(new List<object> { }, _tx_fee: _tx_fee, _tx_callType: _tx_callType, _tx_roundValidity: _tx_roundValidity, _tx_note: _tx_note, _tx_sender: _tx_sender, _tx_transactions: _tx_transactions, _tx_apps: _tx_apps, _tx_assets: _tx_assets, _tx_accounts: _tx_accounts, _tx_boxes: _tx_boxes);

        }

        protected override ulong? ExtraProgramPages { get; set; } = 2;
        protected string _ARC56DATA = "eyJhcmNzIjpbMjIsMjhdLCJuYW1lIjoiQXJjMTY0NCIsImRlc2MiOm51bGwsIm5ldHdvcmtzIjp7fSwic3RydWN0cyI6eyJBcHByb3ZhbFN0cnVjdCI6W3sibmFtZSI6ImFwcHJvdmFsQW1vdW50IiwidHlwZSI6InVpbnQyNTYifSx7Im5hbWUiOiJvd25lciIsInR5cGUiOiJhZGRyZXNzIn0seyJuYW1lIjoic3BlbmRlciIsInR5cGUiOiJhZGRyZXNzIn1dLCJhcmMxNDEwX0hvbGRpbmdQYXJ0aXRpb25zUGFnaW5hdGVkS2V5IjpbeyJuYW1lIjoiaG9sZGVyIiwidHlwZSI6ImFkZHJlc3MifSx7Im5hbWUiOiJwYWdlIiwidHlwZSI6InVpbnQ2NCJ9XSwiYXJjMTQxMF9PcGVyYXRvcktleSI6W3sibmFtZSI6ImhvbGRlciIsInR5cGUiOiJhZGRyZXNzIn0seyJuYW1lIjoib3BlcmF0b3IiLCJ0eXBlIjoiYWRkcmVzcyJ9LHsibmFtZSI6InBhcnRpdGlvbiIsInR5cGUiOiJhZGRyZXNzIn1dLCJhcmMxNDEwX09wZXJhdG9yUG9ydGlvbktleSI6W3sibmFtZSI6ImhvbGRlciIsInR5cGUiOiJhZGRyZXNzIn0seyJuYW1lIjoib3BlcmF0b3IiLCJ0eXBlIjoiYWRkcmVzcyJ9LHsibmFtZSI6InBhcnRpdGlvbiIsInR5cGUiOiJhZGRyZXNzIn1dLCJhcmMxNDEwX1BhcnRpdGlvbktleSI6W3sibmFtZSI6ImhvbGRlciIsInR5cGUiOiJhZGRyZXNzIn0seyJuYW1lIjoicGFydGl0aW9uIiwidHlwZSI6ImFkZHJlc3MifV0sImFyYzE0MTBfY2FuX3RyYW5zZmVyX2J5X3BhcnRpdGlvbl9yZXR1cm4iOlt7Im5hbWUiOiJjb2RlIiwidHlwZSI6ImJ5dGUifSx7Im5hbWUiOiJzdGF0dXMiLCJ0eXBlIjoic3RyaW5nIn0seyJuYW1lIjoicmVjZWl2ZXJQYXJ0aXRpb24iLCJ0eXBlIjoiYWRkcmVzcyJ9XSwiYXJjMTQxMF9wYXJ0aXRpb25faXNzdWUiOlt7Im5hbWUiOiJ0byIsInR5cGUiOiJhZGRyZXNzIn0seyJuYW1lIjoicGFydGl0aW9uIiwidHlwZSI6ImFkZHJlc3MifSx7Im5hbWUiOiJhbW91bnQiLCJ0eXBlIjoidWludDI1NiJ9LHsibmFtZSI6ImRhdGEiLCJ0eXBlIjoiYnl0ZVtdIn1dLCJhcmMxNDEwX3BhcnRpdGlvbl9yZWRlZW0iOlt7Im5hbWUiOiJmcm9tIiwidHlwZSI6ImFkZHJlc3MifSx7Im5hbWUiOiJwYXJ0aXRpb24iLCJ0eXBlIjoiYWRkcmVzcyJ9LHsibmFtZSI6ImFtb3VudCIsInR5cGUiOiJ1aW50MjU2In0seyJuYW1lIjoiZGF0YSIsInR5cGUiOiJieXRlW10ifV0sImFyYzE0MTBfcGFydGl0aW9uX3RyYW5zZmVyIjpbeyJuYW1lIjoiZnJvbSIsInR5cGUiOiJhZGRyZXNzIn0seyJuYW1lIjoidG8iLCJ0eXBlIjoiYWRkcmVzcyJ9LHsibmFtZSI6InBhcnRpdGlvbiIsInR5cGUiOiJhZGRyZXNzIn0seyJuYW1lIjoiYW1vdW50IiwidHlwZSI6InVpbnQyNTYifSx7Im5hbWUiOiJkYXRhIiwidHlwZSI6ImJ5dGVbXSJ9XSwiYXJjMTU5NF9pc3N1ZV9ldmVudCI6W3sibmFtZSI6InRvIiwidHlwZSI6ImFkZHJlc3MifSx7Im5hbWUiOiJhbW91bnQiLCJ0eXBlIjoidWludDI1NiJ9LHsibmFtZSI6ImRhdGEiLCJ0eXBlIjoiYnl0ZVtdIn1dLCJhcmMxNTk0X3JlZGVlbV9ldmVudCI6W3sibmFtZSI6ImZyb20iLCJ0eXBlIjoiYWRkcmVzcyJ9LHsibmFtZSI6ImFtb3VudCIsInR5cGUiOiJ1aW50MjU2In0seyJuYW1lIjoiZGF0YSIsInR5cGUiOiJieXRlW10ifV0sImFyYzE2NDRfY29udHJvbGxlcl9jaGFuZ2VkX2V2ZW50IjpbeyJuYW1lIjoib2xkIiwidHlwZSI6ImFkZHJlc3MifSx7Im5hbWUiOiJuZXUiLCJ0eXBlIjoiYWRkcmVzcyJ9XSwiYXJjMTY0NF9jb250cm9sbGVyX3JlZGVlbV9ldmVudCI6W3sibmFtZSI6ImNvbnRyb2xsZXIiLCJ0eXBlIjoiYWRkcmVzcyJ9LHsibmFtZSI6ImZyb20iLCJ0eXBlIjoiYWRkcmVzcyJ9LHsibmFtZSI6ImFtb3VudCIsInR5cGUiOiJ1aW50MjU2In0seyJuYW1lIjoiY29kZSIsInR5cGUiOiJieXRlIn0seyJuYW1lIjoib3BlcmF0b3JfZGF0YSIsInR5cGUiOiJieXRlW10ifV0sImFyYzE2NDRfY29udHJvbGxlcl90cmFuc2Zlcl9ldmVudCI6W3sibmFtZSI6ImNvbnRyb2xsZXIiLCJ0eXBlIjoiYWRkcmVzcyJ9LHsibmFtZSI6ImZyb20iLCJ0eXBlIjoiYWRkcmVzcyJ9LHsibmFtZSI6InRvIiwidHlwZSI6ImFkZHJlc3MifSx7Im5hbWUiOiJhbW91bnQiLCJ0eXBlIjoidWludDI1NiJ9LHsibmFtZSI6ImNvZGUiLCJ0eXBlIjoiYnl0ZSJ9LHsibmFtZSI6ImRhdGEiLCJ0eXBlIjoiYnl0ZVtdIn0seyJuYW1lIjoib3BlcmF0b3JfZGF0YSIsInR5cGUiOiJieXRlW10ifV19LCJNZXRob2RzIjpbeyJuYW1lIjoiYXJjMTY0NF9zZXRfY29udHJvbGxlciIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoibmV3X2NvbnRyb2xsZXIiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9XSwicmV0dXJucyI6eyJ0eXBlIjoidm9pZCIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6ZmFsc2UsImV2ZW50cyI6W3sibmFtZSI6IkNvbnRyb2xsZXJDaGFuZ2VkIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6IihhZGRyZXNzLGFkZHJlc3MpIiwic3RydWN0IjoiYXJjMTY0NF9jb250cm9sbGVyX2NoYW5nZWRfZXZlbnQiLCJuYW1lIjoiMCIsImRlc2MiOm51bGx9XX1dLCJyZWNvbW1lbmRhdGlvbnMiOnsiaW5uZXJUcmFuc2FjdGlvbkNvdW50IjpudWxsLCJib3hlcyI6bnVsbCwiYWNjb3VudHMiOm51bGwsImFwcHMiOm51bGwsImFzc2V0cyI6bnVsbH19LHsibmFtZSI6ImFyYzE2NDRfc2V0X2NvbnRyb2xsYWJsZSIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiJib29sIiwic3RydWN0IjpudWxsLCJuYW1lIjoiZmxhZyIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH1dLCJyZXR1cm5zIjp7InR5cGUiOiJ2b2lkIiwic3RydWN0IjpudWxsLCJkZXNjIjpudWxsfSwiYWN0aW9ucyI6eyJjcmVhdGUiOltdLCJjYWxsIjpbIk5vT3AiXX0sInJlYWRvbmx5IjpmYWxzZSwiZXZlbnRzIjpbXSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmMxNjQ0X3NldF9yZXF1aXJlX2p1c3RpZmljYXRpb24iLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYm9vbCIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImZsYWciLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9XSwicmV0dXJucyI6eyJ0eXBlIjoidm9pZCIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6ZmFsc2UsImV2ZW50cyI6W10sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjMTY0NF9zZXRfbWluX2FjdGlvbl9pbnRlcnZhbCIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiJ1aW50NjQiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJpbnRlcnZhbCIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH1dLCJyZXR1cm5zIjp7InR5cGUiOiJ2b2lkIiwic3RydWN0IjpudWxsLCJkZXNjIjpudWxsfSwiYWN0aW9ucyI6eyJjcmVhdGUiOltdLCJjYWxsIjpbIk5vT3AiXX0sInJlYWRvbmx5IjpmYWxzZSwiZXZlbnRzIjpbXSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmMxNjQ0X2lzX2NvbnRyb2xsYWJsZSIsImRlc2MiOm51bGwsImFyZ3MiOltdLCJyZXR1cm5zIjp7InR5cGUiOiJ1aW50NjQiLCJzdHJ1Y3QiOm51bGwsImRlc2MiOm51bGx9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOnRydWUsImV2ZW50cyI6W10sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjMTY0NF9jb250cm9sbGVyX3RyYW5zZmVyIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJmcm9tIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoidG8iLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6InVpbnQyNTYiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJhbW91bnQiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6ImJ5dGVbXSIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImRhdGEiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6ImJ5dGVbXSIsInN0cnVjdCI6bnVsbCwibmFtZSI6Im9wZXJhdG9yX2RhdGEiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9XSwicmV0dXJucyI6eyJ0eXBlIjoidWludDY0Iiwic3RydWN0IjpudWxsLCJkZXNjIjpudWxsfSwiYWN0aW9ucyI6eyJjcmVhdGUiOltdLCJjYWxsIjpbIk5vT3AiXX0sInJlYWRvbmx5IjpmYWxzZSwiZXZlbnRzIjpbeyJuYW1lIjoiQ29udHJvbGxlclRyYW5zZmVyIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6IihhZGRyZXNzLGFkZHJlc3MsYWRkcmVzcyx1aW50MjU2LGJ5dGUsYnl0ZVtdLGJ5dGVbXSkiLCJzdHJ1Y3QiOiJhcmMxNjQ0X2NvbnRyb2xsZXJfdHJhbnNmZXJfZXZlbnQiLCJuYW1lIjoiMCIsImRlc2MiOm51bGx9XX1dLCJyZWNvbW1lbmRhdGlvbnMiOnsiaW5uZXJUcmFuc2FjdGlvbkNvdW50IjpudWxsLCJib3hlcyI6bnVsbCwiYWNjb3VudHMiOm51bGwsImFwcHMiOm51bGwsImFzc2V0cyI6bnVsbH19LHsibmFtZSI6ImFyYzE2NDRfY29udHJvbGxlcl9yZWRlZW0iLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImZyb20iLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6InVpbnQyNTYiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJhbW91bnQiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6ImJ5dGVbXSIsInN0cnVjdCI6bnVsbCwibmFtZSI6Im9wZXJhdG9yX2RhdGEiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9XSwicmV0dXJucyI6eyJ0eXBlIjoidWludDY0Iiwic3RydWN0IjpudWxsLCJkZXNjIjpudWxsfSwiYWN0aW9ucyI6eyJjcmVhdGUiOltdLCJjYWxsIjpbIk5vT3AiXX0sInJlYWRvbmx5IjpmYWxzZSwiZXZlbnRzIjpbeyJuYW1lIjoiQ29udHJvbGxlclJlZGVlbSIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiIoYWRkcmVzcyxhZGRyZXNzLHVpbnQyNTYsYnl0ZSxieXRlW10pIiwic3RydWN0IjoiYXJjMTY0NF9jb250cm9sbGVyX3JlZGVlbV9ldmVudCIsIm5hbWUiOiIwIiwiZGVzYyI6bnVsbH1dfV0sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjMTU5NF9zZXRfaXNzdWFibGUiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYm9vbCIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImZsYWciLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9XSwicmV0dXJucyI6eyJ0eXBlIjoidm9pZCIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6ZmFsc2UsImV2ZW50cyI6W10sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjMTU5NF9pc3N1ZSIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoidG8iLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6InVpbnQyNTYiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJhbW91bnQiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6ImJ5dGVbXSIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImRhdGEiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9XSwicmV0dXJucyI6eyJ0eXBlIjoidm9pZCIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6ZmFsc2UsImV2ZW50cyI6W3sibmFtZSI6Iklzc3VlIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6IihhZGRyZXNzLGFkZHJlc3MsdWludDI1NixieXRlW10pIiwic3RydWN0IjoiYXJjMTQxMF9wYXJ0aXRpb25faXNzdWUiLCJuYW1lIjoiMCIsImRlc2MiOm51bGx9XX0seyJuYW1lIjoiSXNzdWUiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiKGFkZHJlc3MsdWludDI1NixieXRlW10pIiwic3RydWN0IjoiYXJjMTU5NF9pc3N1ZV9ldmVudCIsIm5hbWUiOiIwIiwiZGVzYyI6bnVsbH1dfV0sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjMTU5NF9yZWRlZW1Gcm9tIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJmcm9tIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJ1aW50MjU2Iiwic3RydWN0IjpudWxsLCJuYW1lIjoiYW1vdW50IiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJieXRlW10iLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJkYXRhIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfV0sInJldHVybnMiOnsidHlwZSI6InZvaWQiLCJzdHJ1Y3QiOm51bGwsImRlc2MiOm51bGx9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOmZhbHNlLCJldmVudHMiOlt7Im5hbWUiOiJSZWRlZW0iLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiKGFkZHJlc3MsdWludDI1NixieXRlW10pIiwic3RydWN0IjoiYXJjMTU5NF9yZWRlZW1fZXZlbnQiLCJuYW1lIjoiMCIsImRlc2MiOm51bGx9XX1dLCJyZWNvbW1lbmRhdGlvbnMiOnsiaW5uZXJUcmFuc2FjdGlvbkNvdW50IjpudWxsLCJib3hlcyI6bnVsbCwiYWNjb3VudHMiOm51bGwsImFwcHMiOm51bGwsImFzc2V0cyI6bnVsbH19LHsibmFtZSI6ImFyYzE1OTRfcmVkZWVtIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6InVpbnQyNTYiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJhbW91bnQiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6ImJ5dGVbXSIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImRhdGEiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9XSwicmV0dXJucyI6eyJ0eXBlIjoidm9pZCIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6ZmFsc2UsImV2ZW50cyI6W3sibmFtZSI6IlJlZGVlbSIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiIoYWRkcmVzcyx1aW50MjU2LGJ5dGVbXSkiLCJzdHJ1Y3QiOiJhcmMxNTk0X3JlZGVlbV9ldmVudCIsIm5hbWUiOiIwIiwiZGVzYyI6bnVsbH1dfV0sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjMTU5NF90cmFuc2Zlcl93aXRoX2RhdGEiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InRvIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJ1aW50MjU2Iiwic3RydWN0IjpudWxsLCJuYW1lIjoiYW1vdW50IiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJieXRlW10iLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJkYXRhIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfV0sInJldHVybnMiOnsidHlwZSI6ImJvb2wiLCJzdHJ1Y3QiOm51bGwsImRlc2MiOm51bGx9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOmZhbHNlLCJldmVudHMiOlt7Im5hbWUiOiJUcmFuc2ZlciIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiIoYWRkcmVzcyxhZGRyZXNzLGFkZHJlc3MsdWludDI1NixieXRlW10pIiwic3RydWN0IjoiYXJjMTQxMF9wYXJ0aXRpb25fdHJhbnNmZXIiLCJuYW1lIjoiMCIsImRlc2MiOm51bGx9XX0seyJuYW1lIjoiYXJjMjAwX1RyYW5zZmVyIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJmcm9tIiwiZGVzYyI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InRvIiwiZGVzYyI6bnVsbH0seyJ0eXBlIjoidWludDI1NiIsInN0cnVjdCI6bnVsbCwibmFtZSI6InZhbHVlIiwiZGVzYyI6bnVsbH1dfV0sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjMTU5NF90cmFuc2Zlcl9mcm9tX3dpdGhfZGF0YSIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoiZnJvbSIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InRvIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJ1aW50MjU2Iiwic3RydWN0IjpudWxsLCJuYW1lIjoiYW1vdW50IiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJieXRlW10iLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJkYXRhIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfV0sInJldHVybnMiOnsidHlwZSI6ImJvb2wiLCJzdHJ1Y3QiOm51bGwsImRlc2MiOm51bGx9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOmZhbHNlLCJldmVudHMiOlt7Im5hbWUiOiJhcmMyMDBfQXBwcm92YWwiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6Im93bmVyIiwiZGVzYyI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InNwZW5kZXIiLCJkZXNjIjpudWxsfSx7InR5cGUiOiJ1aW50MjU2Iiwic3RydWN0IjpudWxsLCJuYW1lIjoidmFsdWUiLCJkZXNjIjpudWxsfV19LHsibmFtZSI6ImFyYzIwMF9UcmFuc2ZlciIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoiZnJvbSIsImRlc2MiOm51bGx9LHsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJ0byIsImRlc2MiOm51bGx9LHsidHlwZSI6InVpbnQyNTYiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJ2YWx1ZSIsImRlc2MiOm51bGx9XX1dLCJyZWNvbW1lbmRhdGlvbnMiOnsiaW5uZXJUcmFuc2FjdGlvbkNvdW50IjpudWxsLCJib3hlcyI6bnVsbCwiYWNjb3VudHMiOm51bGwsImFwcHMiOm51bGwsImFzc2V0cyI6bnVsbH19LHsibmFtZSI6ImFyYzE1OTRfaXNfaXNzdWFibGUiLCJkZXNjIjpudWxsLCJhcmdzIjpbXSwicmV0dXJucyI6eyJ0eXBlIjoiYm9vbCIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6dHJ1ZSwiZXZlbnRzIjpbXSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmMxNDEwX2JhbGFuY2Vfb2ZfcGFydGl0aW9uIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJob2xkZXIiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJwYXJ0aXRpb24iLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9XSwicmV0dXJucyI6eyJ0eXBlIjoidWludDI1NiIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6dHJ1ZSwiZXZlbnRzIjpbXSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmMyMDBfdHJhbnNmZXIiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InRvIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJ1aW50MjU2Iiwic3RydWN0IjpudWxsLCJuYW1lIjoidmFsdWUiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9XSwicmV0dXJucyI6eyJ0eXBlIjoiYm9vbCIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6ZmFsc2UsImV2ZW50cyI6W3sibmFtZSI6IlRyYW5zZmVyIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6IihhZGRyZXNzLGFkZHJlc3MsYWRkcmVzcyx1aW50MjU2LGJ5dGVbXSkiLCJzdHJ1Y3QiOiJhcmMxNDEwX3BhcnRpdGlvbl90cmFuc2ZlciIsIm5hbWUiOiIwIiwiZGVzYyI6bnVsbH1dfSx7Im5hbWUiOiJhcmMyMDBfVHJhbnNmZXIiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImZyb20iLCJkZXNjIjpudWxsfSx7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoidG8iLCJkZXNjIjpudWxsfSx7InR5cGUiOiJ1aW50MjU2Iiwic3RydWN0IjpudWxsLCJuYW1lIjoidmFsdWUiLCJkZXNjIjpudWxsfV19XSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmMxNDEwX3RyYW5zZmVyX2J5X3BhcnRpdGlvbiIsImRlc2MiOiJUcmFuc2ZlciBhbiBhbW91bnQgb2YgdG9rZW5zIGZyb20gcGFydGl0aW9uIHRvIHJlY2VpdmVyLiBTZW5kZXIgbXVzdCBiZSBtc2cuc2VuZGVyIG9yIGF1dGhvcml6ZWQgb3BlcmF0b3IuIiwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJwYXJ0aXRpb24iLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJ0byIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoidWludDI1NiIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImFtb3VudCIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoiYnl0ZVtdIiwic3RydWN0IjpudWxsLCJuYW1lIjoiZGF0YSIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH1dLCJyZXR1cm5zIjp7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJkZXNjIjpudWxsfSwiYWN0aW9ucyI6eyJjcmVhdGUiOltdLCJjYWxsIjpbIk5vT3AiXX0sInJlYWRvbmx5IjpmYWxzZSwiZXZlbnRzIjpbeyJuYW1lIjoiVHJhbnNmZXIiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiKGFkZHJlc3MsYWRkcmVzcyxhZGRyZXNzLHVpbnQyNTYsYnl0ZVtdKSIsInN0cnVjdCI6ImFyYzE0MTBfcGFydGl0aW9uX3RyYW5zZmVyIiwibmFtZSI6IjAiLCJkZXNjIjpudWxsfV19XSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmMxNDEwX3BhcnRpdGlvbnNfb2YiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImhvbGRlciIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoidWludDY0Iiwic3RydWN0IjpudWxsLCJuYW1lIjoicGFnZSIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH1dLCJyZXR1cm5zIjp7InR5cGUiOiJhZGRyZXNzW10iLCJzdHJ1Y3QiOm51bGwsImRlc2MiOm51bGx9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOmZhbHNlLCJldmVudHMiOltdLCJyZWNvbW1lbmRhdGlvbnMiOnsiaW5uZXJUcmFuc2FjdGlvbkNvdW50IjpudWxsLCJib3hlcyI6bnVsbCwiYWNjb3VudHMiOm51bGwsImFwcHMiOm51bGwsImFzc2V0cyI6bnVsbH19LHsibmFtZSI6ImFyYzE0MTBfaXNfb3BlcmF0b3IiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImhvbGRlciIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6Im9wZXJhdG9yIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoicGFydGl0aW9uIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfV0sInJldHVybnMiOnsidHlwZSI6ImJvb2wiLCJzdHJ1Y3QiOm51bGwsImRlc2MiOm51bGx9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOnRydWUsImV2ZW50cyI6W10sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjMTQxMF9hdXRob3JpemVfb3BlcmF0b3IiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImhvbGRlciIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6Im9wZXJhdG9yIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoicGFydGl0aW9uIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfV0sInJldHVybnMiOnsidHlwZSI6InZvaWQiLCJzdHJ1Y3QiOm51bGwsImRlc2MiOm51bGx9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOmZhbHNlLCJldmVudHMiOltdLCJyZWNvbW1lbmRhdGlvbnMiOnsiaW5uZXJUcmFuc2FjdGlvbkNvdW50IjpudWxsLCJib3hlcyI6bnVsbCwiYWNjb3VudHMiOm51bGwsImFwcHMiOm51bGwsImFzc2V0cyI6bnVsbH19LHsibmFtZSI6ImFyYzE0MTBfcmV2b2tlX29wZXJhdG9yIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJob2xkZXIiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJvcGVyYXRvciIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InBhcnRpdGlvbiIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH1dLCJyZXR1cm5zIjp7InR5cGUiOiJ2b2lkIiwic3RydWN0IjpudWxsLCJkZXNjIjpudWxsfSwiYWN0aW9ucyI6eyJjcmVhdGUiOltdLCJjYWxsIjpbIk5vT3AiXX0sInJlYWRvbmx5IjpmYWxzZSwiZXZlbnRzIjpbXSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmMxNDEwX29wZXJhdG9yX3RyYW5zZmVyX2J5X3BhcnRpdGlvbiIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoiZnJvbSIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InBhcnRpdGlvbiIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InRvIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJ1aW50MjU2Iiwic3RydWN0IjpudWxsLCJuYW1lIjoiYW1vdW50IiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJieXRlW10iLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJkYXRhIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfV0sInJldHVybnMiOnsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsImRlc2MiOm51bGx9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOmZhbHNlLCJldmVudHMiOlt7Im5hbWUiOiJUcmFuc2ZlciIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiIoYWRkcmVzcyxhZGRyZXNzLGFkZHJlc3MsdWludDI1NixieXRlW10pIiwic3RydWN0IjoiYXJjMTQxMF9wYXJ0aXRpb25fdHJhbnNmZXIiLCJuYW1lIjoiMCIsImRlc2MiOm51bGx9XX1dLCJyZWNvbW1lbmRhdGlvbnMiOnsiaW5uZXJUcmFuc2FjdGlvbkNvdW50IjpudWxsLCJib3hlcyI6bnVsbCwiYWNjb3VudHMiOm51bGwsImFwcHMiOm51bGwsImFzc2V0cyI6bnVsbH19LHsibmFtZSI6ImFyYzE0MTBfY2FuX3RyYW5zZmVyX2J5X3BhcnRpdGlvbiIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoiZnJvbSIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InBhcnRpdGlvbiIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InRvIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJ1aW50MjU2Iiwic3RydWN0IjpudWxsLCJuYW1lIjoiYW1vdW50IiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJieXRlW10iLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJkYXRhIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfV0sInJldHVybnMiOnsidHlwZSI6IihieXRlLHN0cmluZyxhZGRyZXNzKSIsInN0cnVjdCI6ImFyYzE0MTBfY2FuX3RyYW5zZmVyX2J5X3BhcnRpdGlvbl9yZXR1cm4iLCJkZXNjIjpudWxsfSwiYWN0aW9ucyI6eyJjcmVhdGUiOltdLCJjYWxsIjpbIk5vT3AiXX0sInJlYWRvbmx5IjpmYWxzZSwiZXZlbnRzIjpbXSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmMxNDEwX2F1dGhvcml6ZV9vcGVyYXRvcl9ieV9wb3J0aW9uIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJob2xkZXIiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJvcGVyYXRvciIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InBhcnRpdGlvbiIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoidWludDI1NiIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImFtb3VudCIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH1dLCJyZXR1cm5zIjp7InR5cGUiOiJ2b2lkIiwic3RydWN0IjpudWxsLCJkZXNjIjpudWxsfSwiYWN0aW9ucyI6eyJjcmVhdGUiOltdLCJjYWxsIjpbIk5vT3AiXX0sInJlYWRvbmx5IjpmYWxzZSwiZXZlbnRzIjpbXSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmMxNDEwX2lzX29wZXJhdG9yX2J5X3BvcnRpb24iLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImhvbGRlciIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6Im9wZXJhdG9yIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoicGFydGl0aW9uIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfV0sInJldHVybnMiOnsidHlwZSI6ImJvb2wiLCJzdHJ1Y3QiOm51bGwsImRlc2MiOm51bGx9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOnRydWUsImV2ZW50cyI6W10sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjMTQxMF9pc3N1ZV9ieV9wYXJ0aXRpb24iLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InRvIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoicGFydGl0aW9uIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJ1aW50MjU2Iiwic3RydWN0IjpudWxsLCJuYW1lIjoiYW1vdW50IiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJieXRlW10iLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJkYXRhIiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfV0sInJldHVybnMiOnsidHlwZSI6InZvaWQiLCJzdHJ1Y3QiOm51bGwsImRlc2MiOm51bGx9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOmZhbHNlLCJldmVudHMiOlt7Im5hbWUiOiJJc3N1ZSIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiIoYWRkcmVzcyxhZGRyZXNzLHVpbnQyNTYsYnl0ZVtdKSIsInN0cnVjdCI6ImFyYzE0MTBfcGFydGl0aW9uX2lzc3VlIiwibmFtZSI6IjAiLCJkZXNjIjpudWxsfV19XSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmMxNDEwX3JlZGVlbV9ieV9wYXJ0aXRpb24iLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InBhcnRpdGlvbiIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoidWludDI1NiIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImFtb3VudCIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoiYnl0ZVtdIiwic3RydWN0IjpudWxsLCJuYW1lIjoiZGF0YSIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH1dLCJyZXR1cm5zIjp7InR5cGUiOiJ2b2lkIiwic3RydWN0IjpudWxsLCJkZXNjIjpudWxsfSwiYWN0aW9ucyI6eyJjcmVhdGUiOltdLCJjYWxsIjpbIk5vT3AiXX0sInJlYWRvbmx5IjpmYWxzZSwiZXZlbnRzIjpbeyJuYW1lIjoiUmVkZWVtIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6IihhZGRyZXNzLGFkZHJlc3MsdWludDI1NixieXRlW10pIiwic3RydWN0IjoiYXJjMTQxMF9wYXJ0aXRpb25fcmVkZWVtIiwibmFtZSI6IjAiLCJkZXNjIjpudWxsfV19XSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmMxNDEwX29wZXJhdG9yX3JlZGVlbV9ieV9wYXJ0aXRpb24iLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImZyb20iLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJwYXJ0aXRpb24iLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6InVpbnQyNTYiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJhbW91bnQiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6ImJ5dGVbXSIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImRhdGEiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9XSwicmV0dXJucyI6eyJ0eXBlIjoidm9pZCIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6ZmFsc2UsImV2ZW50cyI6W3sibmFtZSI6IlJlZGVlbSIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiIoYWRkcmVzcyxhZGRyZXNzLHVpbnQyNTYsYnl0ZVtdKSIsInN0cnVjdCI6ImFyYzE0MTBfcGFydGl0aW9uX3JlZGVlbSIsIm5hbWUiOiIwIiwiZGVzYyI6bnVsbH1dfV0sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYm9vdHN0cmFwIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6ImJ5dGVbXSIsInN0cnVjdCI6bnVsbCwibmFtZSI6Im5hbWUiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6ImJ5dGVbXSIsInN0cnVjdCI6bnVsbCwibmFtZSI6InN5bWJvbCIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoidWludDgiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJkZWNpbWFscyIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoidWludDI1NiIsInN0cnVjdCI6bnVsbCwibmFtZSI6InRvdGFsU3VwcGx5IiwiZGVzYyI6bnVsbCwiZGVmYXVsdFZhbHVlIjpudWxsfV0sInJldHVybnMiOnsidHlwZSI6ImJvb2wiLCJzdHJ1Y3QiOm51bGwsImRlc2MiOm51bGx9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOmZhbHNlLCJldmVudHMiOlt7Im5hbWUiOiJhcmMyMDBfVHJhbnNmZXIiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImZyb20iLCJkZXNjIjpudWxsfSx7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoidG8iLCJkZXNjIjpudWxsfSx7InR5cGUiOiJ1aW50MjU2Iiwic3RydWN0IjpudWxsLCJuYW1lIjoidmFsdWUiLCJkZXNjIjpudWxsfV19XSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmMyMDBfbmFtZSIsImRlc2MiOiJSZXR1cm5zIHRoZSBuYW1lIG9mIHRoZSB0b2tlbiIsImFyZ3MiOltdLCJyZXR1cm5zIjp7InR5cGUiOiJieXRlWzMyXSIsInN0cnVjdCI6bnVsbCwiZGVzYyI6IlRoZSBuYW1lIG9mIHRoZSB0b2tlbiJ9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOnRydWUsImV2ZW50cyI6W10sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjMjAwX3N5bWJvbCIsImRlc2MiOiJSZXR1cm5zIHRoZSBzeW1ib2wgb2YgdGhlIHRva2VuIiwiYXJncyI6W10sInJldHVybnMiOnsidHlwZSI6ImJ5dGVbOF0iLCJzdHJ1Y3QiOm51bGwsImRlc2MiOiJUaGUgc3ltYm9sIG9mIHRoZSB0b2tlbiJ9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOnRydWUsImV2ZW50cyI6W10sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjMjAwX2RlY2ltYWxzIiwiZGVzYyI6IlJldHVybnMgdGhlIGRlY2ltYWxzIG9mIHRoZSB0b2tlbiIsImFyZ3MiOltdLCJyZXR1cm5zIjp7InR5cGUiOiJ1aW50OCIsInN0cnVjdCI6bnVsbCwiZGVzYyI6IlRoZSBkZWNpbWFscyBvZiB0aGUgdG9rZW4ifSwiYWN0aW9ucyI6eyJjcmVhdGUiOltdLCJjYWxsIjpbIk5vT3AiXX0sInJlYWRvbmx5Ijp0cnVlLCJldmVudHMiOltdLCJyZWNvbW1lbmRhdGlvbnMiOnsiaW5uZXJUcmFuc2FjdGlvbkNvdW50IjpudWxsLCJib3hlcyI6bnVsbCwiYWNjb3VudHMiOm51bGwsImFwcHMiOm51bGwsImFzc2V0cyI6bnVsbH19LHsibmFtZSI6ImFyYzIwMF90b3RhbFN1cHBseSIsImRlc2MiOiJSZXR1cm5zIHRoZSB0b3RhbCBzdXBwbHkgb2YgdGhlIHRva2VuIiwiYXJncyI6W10sInJldHVybnMiOnsidHlwZSI6InVpbnQyNTYiLCJzdHJ1Y3QiOm51bGwsImRlc2MiOiJUaGUgdG90YWwgc3VwcGx5IG9mIHRoZSB0b2tlbiJ9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOnRydWUsImV2ZW50cyI6W10sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjMjAwX2JhbGFuY2VPZiIsImRlc2MiOiJSZXR1cm5zIHRoZSBjdXJyZW50IGJhbGFuY2Ugb2YgdGhlIG93bmVyIG9mIHRoZSB0b2tlbiIsImFyZ3MiOlt7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoib3duZXIiLCJkZXNjIjoiVGhlIGFkZHJlc3Mgb2YgdGhlIG93bmVyIG9mIHRoZSB0b2tlbiIsImRlZmF1bHRWYWx1ZSI6bnVsbH1dLCJyZXR1cm5zIjp7InR5cGUiOiJ1aW50MjU2Iiwic3RydWN0IjpudWxsLCJkZXNjIjoiVGhlIGN1cnJlbnQgYmFsYW5jZSBvZiB0aGUgaG9sZGVyIG9mIHRoZSB0b2tlbiJ9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOnRydWUsImV2ZW50cyI6W10sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjMjAwX3RyYW5zZmVyRnJvbSIsImRlc2MiOiJUcmFuc2ZlcnMgdG9rZW5zIGZyb20gc291cmNlIHRvIGRlc3RpbmF0aW9uIGFzIGFwcHJvdmVkIHNwZW5kZXIiLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6ImZyb20iLCJkZXNjIjoiVGhlIHNvdXJjZSBvZiB0aGUgdHJhbnNmZXIiLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJ0byIsImRlc2MiOiJUaGUgZGVzdGluYXRpb24gb2YgdGhlIHRyYW5zZmVyIiwiZGVmYXVsdFZhbHVlIjpudWxsfSx7InR5cGUiOiJ1aW50MjU2Iiwic3RydWN0IjpudWxsLCJuYW1lIjoidmFsdWUiLCJkZXNjIjoiQW1vdW50IG9mIHRva2VucyB0byB0cmFuc2ZlciIsImRlZmF1bHRWYWx1ZSI6bnVsbH1dLCJyZXR1cm5zIjp7InR5cGUiOiJib29sIiwic3RydWN0IjpudWxsLCJkZXNjIjoiU3VjY2VzcyJ9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOmZhbHNlLCJldmVudHMiOlt7Im5hbWUiOiJhcmMyMDBfQXBwcm92YWwiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6Im93bmVyIiwiZGVzYyI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InNwZW5kZXIiLCJkZXNjIjpudWxsfSx7InR5cGUiOiJ1aW50MjU2Iiwic3RydWN0IjpudWxsLCJuYW1lIjoidmFsdWUiLCJkZXNjIjpudWxsfV19LHsibmFtZSI6ImFyYzIwMF9UcmFuc2ZlciIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoiZnJvbSIsImRlc2MiOm51bGx9LHsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJ0byIsImRlc2MiOm51bGx9LHsidHlwZSI6InVpbnQyNTYiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJ2YWx1ZSIsImRlc2MiOm51bGx9XX1dLCJyZWNvbW1lbmRhdGlvbnMiOnsiaW5uZXJUcmFuc2FjdGlvbkNvdW50IjpudWxsLCJib3hlcyI6bnVsbCwiYWNjb3VudHMiOm51bGwsImFwcHMiOm51bGwsImFzc2V0cyI6bnVsbH19LHsibmFtZSI6ImFyYzIwMF9hcHByb3ZlIiwiZGVzYyI6IkFwcHJvdmUgc3BlbmRlciBmb3IgYSB0b2tlbiIsImFyZ3MiOlt7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoic3BlbmRlciIsImRlc2MiOiJXaG8gaXMgYWxsb3dlZCB0byB0YWtlIHRva2VucyBvbiBvd25lcidzIGJlaGFsZiIsImRlZmF1bHRWYWx1ZSI6bnVsbH0seyJ0eXBlIjoidWludDI1NiIsInN0cnVjdCI6bnVsbCwibmFtZSI6InZhbHVlIiwiZGVzYyI6IkFtb3VudCBvZiB0b2tlbnMgdG8gYmUgdGFrZW4gYnkgc3BlbmRlciIsImRlZmF1bHRWYWx1ZSI6bnVsbH1dLCJyZXR1cm5zIjp7InR5cGUiOiJib29sIiwic3RydWN0IjpudWxsLCJkZXNjIjoiU3VjY2VzcyJ9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOmZhbHNlLCJldmVudHMiOlt7Im5hbWUiOiJhcmMyMDBfQXBwcm92YWwiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6Im93bmVyIiwiZGVzYyI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InNwZW5kZXIiLCJkZXNjIjpudWxsfSx7InR5cGUiOiJ1aW50MjU2Iiwic3RydWN0IjpudWxsLCJuYW1lIjoidmFsdWUiLCJkZXNjIjpudWxsfV19XSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmMyMDBfYWxsb3dhbmNlIiwiZGVzYyI6IlJldHVybnMgdGhlIGN1cnJlbnQgYWxsb3dhbmNlIG9mIHRoZSBzcGVuZGVyIG9mIHRoZSB0b2tlbnMgb2YgdGhlIG93bmVyIiwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJvd25lciIsImRlc2MiOiJPd25lcidzIGFjY291bnQiLCJkZWZhdWx0VmFsdWUiOm51bGx9LHsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJzcGVuZGVyIiwiZGVzYyI6IldobyBpcyBhbGxvd2VkIHRvIHRha2UgdG9rZW5zIG9uIG93bmVyJ3MgYmVoYWxmIiwiZGVmYXVsdFZhbHVlIjpudWxsfV0sInJldHVybnMiOnsidHlwZSI6InVpbnQyNTYiLCJzdHJ1Y3QiOm51bGwsImRlc2MiOiJUaGUgcmVtYWluaW5nIGFsbG93YW5jZSJ9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOnRydWUsImV2ZW50cyI6W10sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjODhfb3duZXIiLCJkZXNjIjpudWxsLCJhcmdzIjpbXSwicmV0dXJucyI6eyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6dHJ1ZSwiZXZlbnRzIjpbXSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmM4OF9pc19vd25lciIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoicXVlcnkiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9XSwicmV0dXJucyI6eyJ0eXBlIjoiYm9vbCIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6dHJ1ZSwiZXZlbnRzIjpbXSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmM4OF9pbml0aWFsaXplX293bmVyIiwiZGVzYyI6IkV4cGxpY2l0IGluaXRpYWxpemF0aW9uIG92ZXJyaWRlIChjcmVhdGlvbiBncm91cCByZWNvbW1lbmRlZCkuIEZhaWxzIGlmIGFscmVhZHkgaW5pdGlhbGl6ZWQuIiwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJuZXdfb3duZXIiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9XSwicmV0dXJucyI6eyJ0eXBlIjoidm9pZCIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6ZmFsc2UsImV2ZW50cyI6W10sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX0seyJuYW1lIjoiYXJjODhfdHJhbnNmZXJfb3duZXJzaGlwIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJuZXdfb3duZXIiLCJkZXNjIjpudWxsLCJkZWZhdWx0VmFsdWUiOm51bGx9XSwicmV0dXJucyI6eyJ0eXBlIjoidm9pZCIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6ZmFsc2UsImV2ZW50cyI6W3sibmFtZSI6ImFyYzg4X093bmVyc2hpcFRyYW5zZmVycmVkIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJwcmV2aW91c19vd25lciIsImRlc2MiOm51bGx9LHsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJuZXdfb3duZXIiLCJkZXNjIjpudWxsfV19XSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmM4OF9yZW5vdW5jZV9vd25lcnNoaXAiLCJkZXNjIjpudWxsLCJhcmdzIjpbXSwicmV0dXJucyI6eyJ0eXBlIjoidm9pZCIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6ZmFsc2UsImV2ZW50cyI6W3sibmFtZSI6ImFyYzg4X093bmVyc2hpcFJlbm91bmNlZCIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoicHJldmlvdXNfb3duZXIiLCJkZXNjIjpudWxsfV19XSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmM4OF90cmFuc2Zlcl9vd25lcnNoaXBfcmVxdWVzdCIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoicGVuZGluZyIsImRlc2MiOm51bGwsImRlZmF1bHRWYWx1ZSI6bnVsbH1dLCJyZXR1cm5zIjp7InR5cGUiOiJ2b2lkIiwic3RydWN0IjpudWxsLCJkZXNjIjpudWxsfSwiYWN0aW9ucyI6eyJjcmVhdGUiOltdLCJjYWxsIjpbIk5vT3AiXX0sInJlYWRvbmx5IjpmYWxzZSwiZXZlbnRzIjpbeyJuYW1lIjoiYXJjODhfT3duZXJzaGlwVHJhbnNmZXJSZXF1ZXN0ZWQiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InByZXZpb3VzX293bmVyIiwiZGVzYyI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InBlbmRpbmdfb3duZXIiLCJkZXNjIjpudWxsfV19XSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmM4OF9hY2NlcHRfb3duZXJzaGlwIiwiZGVzYyI6bnVsbCwiYXJncyI6W10sInJldHVybnMiOnsidHlwZSI6InZvaWQiLCJzdHJ1Y3QiOm51bGwsImRlc2MiOm51bGx9LCJhY3Rpb25zIjp7ImNyZWF0ZSI6W10sImNhbGwiOlsiTm9PcCJdfSwicmVhZG9ubHkiOmZhbHNlLCJldmVudHMiOlt7Im5hbWUiOiJhcmM4OF9Pd25lcnNoaXBUcmFuc2ZlckFjY2VwdGVkIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJwcmV2aW91c19vd25lciIsImRlc2MiOm51bGx9LHsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJuZXdfb3duZXIiLCJkZXNjIjpudWxsfV19LHsibmFtZSI6ImFyYzg4X093bmVyc2hpcFRyYW5zZmVycmVkIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJwcmV2aW91c19vd25lciIsImRlc2MiOm51bGx9LHsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJuZXdfb3duZXIiLCJkZXNjIjpudWxsfV19XSwicmVjb21tZW5kYXRpb25zIjp7ImlubmVyVHJhbnNhY3Rpb25Db3VudCI6bnVsbCwiYm94ZXMiOm51bGwsImFjY291bnRzIjpudWxsLCJhcHBzIjpudWxsLCJhc3NldHMiOm51bGx9fSx7Im5hbWUiOiJhcmM4OF9jYW5jZWxfb3duZXJzaGlwX3JlcXVlc3QiLCJkZXNjIjpudWxsLCJhcmdzIjpbXSwicmV0dXJucyI6eyJ0eXBlIjoidm9pZCIsInN0cnVjdCI6bnVsbCwiZGVzYyI6bnVsbH0sImFjdGlvbnMiOnsiY3JlYXRlIjpbXSwiY2FsbCI6WyJOb09wIl19LCJyZWFkb25seSI6ZmFsc2UsImV2ZW50cyI6W10sInJlY29tbWVuZGF0aW9ucyI6eyJpbm5lclRyYW5zYWN0aW9uQ291bnQiOm51bGwsImJveGVzIjpudWxsLCJhY2NvdW50cyI6bnVsbCwiYXBwcyI6bnVsbCwiYXNzZXRzIjpudWxsfX1dLCJzdGF0ZSI6eyJzY2hlbWEiOnsiZ2xvYmFsIjp7ImludHMiOjAsImJ5dGVzIjoxNH0sImxvY2FsIjp7ImludHMiOjAsImJ5dGVzIjowfX0sImtleXMiOnsiZ2xvYmFsIjp7ImRlc2MiOm51bGwsImtleVR5cGUiOiIiLCJ2YWx1ZVR5cGUiOiIiLCJrZXkiOiIifSwibG9jYWwiOnsiZGVzYyI6bnVsbCwia2V5VHlwZSI6IiIsInZhbHVlVHlwZSI6IiIsImtleSI6IiJ9LCJib3giOnsiZGVzYyI6bnVsbCwia2V5VHlwZSI6IiIsInZhbHVlVHlwZSI6IiIsImtleSI6IiJ9fSwibWFwcyI6eyJnbG9iYWwiOnsiZGVzYyI6bnVsbCwia2V5VHlwZSI6IiIsInZhbHVlVHlwZSI6IiIsInByZWZpeCI6bnVsbH0sImxvY2FsIjp7ImRlc2MiOm51bGwsImtleVR5cGUiOiIiLCJ2YWx1ZVR5cGUiOiIiLCJwcmVmaXgiOm51bGx9LCJib3giOnsiZGVzYyI6bnVsbCwia2V5VHlwZSI6IiIsInZhbHVlVHlwZSI6IiIsInByZWZpeCI6bnVsbH19fSwiYmFyZUFjdGlvbnMiOnsiY3JlYXRlIjpbIk5vT3AiXSwiY2FsbCI6W119LCJzb3VyY2VJbmZvIjp7ImFwcHJvdmFsIjp7InNvdXJjZUluZm8iOlt7InBjIjpbMjExNiwyMTI5LDIyMTksMjIzMiwyMzM1LDI0MTYsMjQ2NSwyNDk3LDI2MjEsMjc2MywyOTU4LDMxMzUsMzE5OCwzMjU2LDMyNzEsMzM4OCwzNDc3LDM1NjAsMzYyNSwzNjYzLDM3NDksMzc1NiwzNzg2LDM3OTksMzkwNywzOTU0LDM5NjEsMzk5NCw0MDA3LDQyOTMsNDQyNV0sImVycm9yTWVzc2FnZSI6IkJveCBtdXN0IGhhdmUgdmFsdWUiLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOlszMjI0LDQ0MjZdLCJlcnJvck1lc3NhZ2UiOiJJbmRleCBhY2Nlc3MgaXMgb3V0IG9mIGJvdW5kcyIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzM3OTQsNDAwMl0sImVycm9yTWVzc2FnZSI6Ikluc3VmZmljaWVudCBiYWxhbmNlIiwidGVhbCI6bnVsbCwic291cmNlIjpudWxsfSx7InBjIjpbNDMxNF0sImVycm9yTWVzc2FnZSI6Ikluc3VmZmljaWVudCBiYWxhbmNlIGF0IHRoZSBzZW5kZXIgYWNjb3VudCIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzM3NTMsMzk1OF0sImVycm9yTWVzc2FnZSI6Ikluc3VmZmljaWVudCBwYXJ0aXRpb24gYmFsYW5jZSIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzMzNjMsMzU5MiwzNzMwXSwiZXJyb3JNZXNzYWdlIjoiSW52YWxpZCBhbW91bnQiLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOls0MDgwXSwiZXJyb3JNZXNzYWdlIjoiTmFtZSBvZiB0aGUgYXNzZXQgbXVzdCBiZSBsb25nZXIgb3IgZXF1YWwgdG8gMSBjaGFyYWN0ZXIiLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOls0MDgzXSwiZXJyb3JNZXNzYWdlIjoiTmFtZSBvZiB0aGUgYXNzZXQgbXVzdCBiZSBzaG9ydGVyIG9yIGVxdWFsIHRvIDMyIGNoYXJhY3RlcnMiLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOlsyNjQ4LDM5MzRdLCJlcnJvck1lc3NhZ2UiOiJOb3QgYXV0aG9yaXplZCBvcGVyYXRvciIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzUwNSw1MTcsNTI5LDU0NCw1NTYsNTcxLDU4Niw2MDUsNjIxLDY0Myw2NjUsNjkwLDcwOSw3MjUsNzQxLDc1Nyw3NzMsODAxLDgyNSw4NDYsODcwLDg5NSw5MTksOTUwLDk4MSwxMDAyLDEwMjMsMTA0OCwxMDcwLDEwOTgsMTEyMCwxMTQyLDExNTgsMTE4NiwxMjExLDEyMjksMTI1MCwxMjcxLDEyODYsMTMxMSwxMzQyLDEzNTgsMTM3MywxMzg4LDE0MDNdLCJlcnJvck1lc3NhZ2UiOiJPbkNvbXBsZXRpb24gaXMgbm90IE5vT3AiLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOls0MDcyXSwiZXJyb3JNZXNzYWdlIjoiT25seSBkZXBsb3llciBvZiB0aGlzIHNtYXJ0IGNvbnRyYWN0IGNhbiBjYWxsIGJvb3RzdHJhcCBtZXRob2QiLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOlsyNTIyXSwiZXJyb3JNZXNzYWdlIjoiT25seSBob2xkZXIgY2FuIGF1dGhvcml6ZSIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzM0OTldLCJlcnJvck1lc3NhZ2UiOiJPbmx5IGhvbGRlciBjYW4gYXV0aG9yaXplIHBvcnRpb24iLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOlsyNTQ3XSwiZXJyb3JNZXNzYWdlIjoiT25seSBob2xkZXIgY2FuIHJldm9rZSIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzM3NDYsMzk1MV0sImVycm9yTWVzc2FnZSI6IlBhcnRpdGlvbiBiYWxhbmNlIG1pc3NpbmciLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOlsyNjI2LDM5MTJdLCJlcnJvck1lc3NhZ2UiOiJQb3J0aW9uIGFsbG93YW5jZSBleGNlZWRlZCIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzQwOTFdLCJlcnJvck1lc3NhZ2UiOiJTeW1ib2wgb2YgdGhlIGFzc2V0IG11c3QgYmUgbG9uZ2VyIG9yIGVxdWFsIHRvIDEgY2hhcmFjdGVyIiwidGVhbCI6bnVsbCwic291cmNlIjpudWxsfSx7InBjIjpbNDA5NV0sImVycm9yTWVzc2FnZSI6IlN5bWJvbCBvZiB0aGUgYXNzZXQgbXVzdCBiZSBzaG9ydGVyIG9yIGVxdWFsIHRvIDggY2hhcmFjdGVycyIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzQxMDJdLCJlcnJvck1lc3NhZ2UiOiJUaGlzIG1ldGhvZCBjYW4gYmUgY2FsbGVkIG9ubHkgb25jZSIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzQ1ODldLCJlcnJvck1lc3NhZ2UiOiJhbHJlYWR5X2luaXRpYWxpemVkIiwidGVhbCI6bnVsbCwic291cmNlIjpudWxsfSx7InBjIjpbMTQyM10sImVycm9yTWVzc2FnZSI6ImNhbiBvbmx5IGNhbGwgd2hlbiBjcmVhdGluZyIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzUwOCw1MjAsNTMyLDU0Nyw1NTksNTc0LDU4OSw2MDgsNjI0LDY0Niw2NjgsNjkzLDcxMiw3MjgsNzQ0LDc2MCw3NzYsODA0LDgyOCw4NDksODczLDg5OCw5MjIsOTUzLDk4NCwxMDA1LDEwMjYsMTA1MSwxMDczLDExMDEsMTEyMywxMTQ1LDExNjEsMTE4OSwxMjE0LDEyMzIsMTI1MywxMjc0LDEyODksMTMxNCwxMzQ1LDEzNjEsMTM3NiwxMzkxLDE0MDZdLCJlcnJvck1lc3NhZ2UiOiJjYW4gb25seSBjYWxsIHdoZW4gbm90IGNyZWF0aW5nIiwidGVhbCI6bnVsbCwic291cmNlIjpudWxsfSx7InBjIjpbMTQ1MCwxNDY2LDE0OTcsMTUyNiwxNTQ0LDE1NTAsMTU4OSwxNjY0LDE3MTUsMTkyMCwyMDE3LDIxNTEsMjI1NCwyMzIwLDM2ODIsMzgxOCw0MDI2LDQxNTUsNDE3MCw0MTg2LDQxOTEsNDQ5MCw0NTE5LDQ1NDEsNDU1Myw0NTgwLDQ2MjAsNDYzMSw0NjUzLDQ2NTksNDY4NCw0NzA1LDQ3MTgsNDc0OCw0NzU2LDQ3OTFdLCJlcnJvck1lc3NhZ2UiOiJjaGVjayBHbG9iYWxTdGF0ZSBleGlzdHMiLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOlsxNDc1XSwiZXJyb3JNZXNzYWdlIjoiY29udHJvbGxlcl9kaXNhYmxlZCIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzE3NzUsMTg5NV0sImVycm9yTWVzc2FnZSI6Imluc3VmZmljaWVudCIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzQyMThdLCJlcnJvck1lc3NhZ2UiOiJpbnN1ZmZpY2llbnQgYXBwcm92YWwiLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOlsyMTI0LDIyMjddLCJlcnJvck1lc3NhZ2UiOiJpbnN1ZmZpY2llbnRfYmFsYW5jZSIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzQxNjMsNDE3OSw0Mzk1XSwiZXJyb3JNZXNzYWdlIjoiaW52YWxpZCBzaXplIiwidGVhbCI6bnVsbCwic291cmNlIjpudWxsfSx7InBjIjpbMjAwMywyMDk4LDIyMDRdLCJlcnJvck1lc3NhZ2UiOiJpbnZhbGlkX2Ftb3VudCIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzIwMjZdLCJlcnJvck1lc3NhZ2UiOiJpc3N1YW5jZV9kaXNhYmxlZCIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzE1MTFdLCJlcnJvck1lc3NhZ2UiOiJqdXN0aWZpY2F0aW9uX3JlcXVpcmVkIiwidGVhbCI6bnVsbCwic291cmNlIjpudWxsfSx7InBjIjpbMTQ0M10sImVycm9yTWVzc2FnZSI6Im5vX2NvbnRyb2xsZXIiLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOlsyMDkyXSwiZXJyb3JNZXNzYWdlIjoibm90X2F1dGgiLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOlsxNDUyXSwiZXJyb3JNZXNzYWdlIjoibm90X2NvbnRyb2xsZXIiLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOls0NjIyLDQ2NTUsNDY4Niw0NzkzXSwiZXJyb3JNZXNzYWdlIjoibm90X293bmVyIiwidGVhbCI6bnVsbCwic291cmNlIjpudWxsfSx7InBjIjpbNDc0MSw0NzUyXSwiZXJyb3JNZXNzYWdlIjoibm90X3BlbmRpbmdfb3duZXIiLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOlsxNDM1LDE5NzgsMzU4Nl0sImVycm9yTWVzc2FnZSI6Im9ubHlfb3duZXIiLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOlsxNzgzLDE4MDksMTkwMywxOTI4LDIxMzcsMjE1OSwyMjQwLDIyNjIsMjYzNywzMzk2LDM0ODUsMzYzMywzNjcxLDM2OTAsMzc2NCwzODA3LDM4MjYsMzkyMywzOTY5LDQwMTUsNDAzNCw0MjI2LDQzMzIsNDM1NV0sImVycm9yTWVzc2FnZSI6Im92ZXJmbG93IiwidGVhbCI6bnVsbCwic291cmNlIjpudWxsfSx7InBjIjpbNDcwOV0sImVycm9yTWVzc2FnZSI6InBlbmRpbmdfdHJhbnNmZXJfZXhpc3RzIiwidGVhbCI6bnVsbCwic291cmNlIjpudWxsfSx7InBjIjpbMTU2MF0sImVycm9yTWVzc2FnZSI6InJhdGVfbGltaXRlZCIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH0seyJwYyI6WzE3NjVdLCJlcnJvck1lc3NhZ2UiOiJzYW1lX2FkZHIiLCJ0ZWFsIjpudWxsLCJzb3VyY2UiOm51bGx9LHsicGMiOls0NTk0LDQ2MjcsNDY5MV0sImVycm9yTWVzc2FnZSI6Inplcm9fYWRkcmVzc19ub3RfYWxsb3dlZCIsInRlYWwiOm51bGwsInNvdXJjZSI6bnVsbH1dLCJwY09mZnNldE1ldGhvZCI6Im5vbmUifSwiY2xlYXIiOnsic291cmNlSW5mbyI6W10sInBjT2Zmc2V0TWV0aG9kIjoibm9uZSJ9fSwic291cmNlIjp7ImFwcHJvdmFsIjoiSTNCeVlXZHRZU0IyWlhKemFXOXVJREV3Q2lOd2NtRm5iV0VnZEhsd1pYUnlZV05ySUdaaGJITmxDZ292THlCQVlXeG5iM0poYm1SbWIzVnVaR0YwYVc5dUwyRnNaMjl5WVc1a0xYUjVjR1Z6WTNKcGNIUXZZWEpqTkM5cGJtUmxlQzVrTG5Sek9qcERiMjUwY21GamRDNWhjSEJ5YjNaaGJGQnliMmR5WVcwb0tTQXRQaUIxYVc1ME5qUTZDbTFoYVc0NkNpQWdJQ0JwYm5SallteHZZMnNnTUNBeElETXlJRGd4Q2lBZ0lDQmllWFJsWTJKc2IyTnJJREI0TVRVeFpqZGpOelVnTUhnd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd0lDSmhjbU00T0Y5dklpQWlkQ0lnSW1JaUlDSmpkSEpzWlc0aUlEQjRNREF3TWlBd2VEZ3dJQ0p3SWlBaVlYSmpPRGhmY0c4aUlDSmpkSEpzSWlBd2VEQXdJQ0poY21NNE9GOXZhU0lnSW05d1lTSWdJbTFqWVdraUlDSnBjM01pSUNKb2NGOWhJaUFpYjNBaUlDSnlhblZ6ZENJZ0lteGpZWElpSURCNE1EQXdNREF3TURBd01EQXdNREF3TUNBd2VEQXdORElnTUhnd01TQXdlREF3TURFZ01IZ3dNRFl5SURCNFpEZG1ZelJoT1RnZ01IZ3dNREF3SURCNE5XTXlOMkkwWm1NZ01IZzNPVGd6WXpNMVl5QXdlRFF6TlRWa01tRmtDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPakk1Q2lBZ0lDQXZMeUJsZUhCdmNuUWdZMnhoYzNNZ1FYSmpNVFkwTkNCbGVIUmxibVJ6SUVGeVl6RTFPVFFnZXdvZ0lDQWdkSGh1SUU1MWJVRndjRUZ5WjNNS0lDQWdJR0o2SUcxaGFXNWZZbUZ5WlY5eWIzVjBhVzVuUURVeUNpQWdJQ0J3ZFhOb1lubDBaWE56SURCNE1EUTFORGN5WkRBZ01IZzNaRGM1TURSaE5DQXdlR1UyWmpSbU9EWXhJREI0TW1WaVpESmtNelFnTUhobFpUWm1NbVF3WlNBd2VERmtOV00zWVRFM0lEQjRaVFUzWVRabE1UZ2dNSGcyTldJeE5qZ3lZU0F3ZURBeE16QTFPVGxpSURCNE1UUXlZalZtWTJJZ01IaG1PRGd6T0dWaU9TQXdlRE14T0RneVltWmhJREI0WVRsalkyRXhObVlnTUhneU5qWTFPVGRqTUNBd2VETTFaamd4TXpWbUlEQjRaR0UzTURJMVlqa2dNSGd6WmpJMU5qY3hNeUF3ZURrMVlqUm1PV1V6SURCNE9EQmpZelE1WVdJZ01IZ3dOemsyTWpFMk5TQXdlR1UzT0RrMk1XUmhJREI0Wm1RNU5EZ3daRGNnTUhoaU1XSXhaRFk1WVNBd2VHTXhZbVZrTnpnNUlEQjRNMkptWlRFNE16TWdNSGcxT1RsalpERmhOU0F3ZURaa1pUazBNVFkySURCNE1qaG1NREl6WkRjZ01IZzVOelV6T0RKbE1pQXdlRFkxTjJReE0yVmpJREI0WWpaaFpURmhNalVnTUhnNE5HVmpNVE5rTlNBd2VHVmpPVGsyTURReElEQjRPREpsTlRjell6UWdNSGcwWVRrMk9HWTRaaUF3ZUdJMU5ESXlNVEkxSURCNFltSmlNekU1WmpNZ01IZ3dOekF5TmpVMFpTQXdlR1F3TVRVM01qUmxJREI0TURJNVptVmpZekFnTUhnM016UTVNek0wWlNBd2VHUmlOMk00TW1WbUlEQjRabVF5WXpKak5tVWdNSGcwTW1FMVpqQTJOU0F3ZUdGa05HWTJPR1ZoSUM4dklHMWxkR2h2WkNBaVlYSmpNVFkwTkY5elpYUmZZMjl1ZEhKdmJHeGxjaWhoWkdSeVpYTnpLWFp2YVdRaUxDQnRaWFJvYjJRZ0ltRnlZekUyTkRSZmMyVjBYMk52Ym5SeWIyeHNZV0pzWlNoaWIyOXNLWFp2YVdRaUxDQnRaWFJvYjJRZ0ltRnlZekUyTkRSZmMyVjBYM0psY1hWcGNtVmZhblZ6ZEdsbWFXTmhkR2x2YmloaWIyOXNLWFp2YVdRaUxDQnRaWFJvYjJRZ0ltRnlZekUyTkRSZmMyVjBYMjFwYmw5aFkzUnBiMjVmYVc1MFpYSjJZV3dvZFdsdWREWTBLWFp2YVdRaUxDQnRaWFJvYjJRZ0ltRnlZekUyTkRSZmFYTmZZMjl1ZEhKdmJHeGhZbXhsS0NsMWFXNTBOalFpTENCdFpYUm9iMlFnSW1GeVl6RTJORFJmWTI5dWRISnZiR3hsY2w5MGNtRnVjMlpsY2loaFpHUnlaWE56TEdGa1pISmxjM01zZFdsdWRESTFOaXhpZVhSbFcxMHNZbmwwWlZ0ZEtYVnBiblEyTkNJc0lHMWxkR2h2WkNBaVlYSmpNVFkwTkY5amIyNTBjbTlzYkdWeVgzSmxaR1ZsYlNoaFpHUnlaWE56TEhWcGJuUXlOVFlzWW5sMFpWdGRLWFZwYm5RMk5DSXNJRzFsZEdodlpDQWlZWEpqTVRVNU5GOXpaWFJmYVhOemRXRmliR1VvWW05dmJDbDJiMmxrSWl3Z2JXVjBhRzlrSUNKaGNtTXhOVGswWDJsemMzVmxLR0ZrWkhKbGMzTXNkV2x1ZERJMU5peGllWFJsVzEwcGRtOXBaQ0lzSUcxbGRHaHZaQ0FpWVhKak1UVTVORjl5WldSbFpXMUdjbTl0S0dGa1pISmxjM01zZFdsdWRESTFOaXhpZVhSbFcxMHBkbTlwWkNJc0lHMWxkR2h2WkNBaVlYSmpNVFU1TkY5eVpXUmxaVzBvZFdsdWRESTFOaXhpZVhSbFcxMHBkbTlwWkNJc0lHMWxkR2h2WkNBaVlYSmpNVFU1TkY5MGNtRnVjMlpsY2w5M2FYUm9YMlJoZEdFb1lXUmtjbVZ6Y3l4MWFXNTBNalUyTEdKNWRHVmJYU2xpYjI5c0lpd2diV1YwYUc5a0lDSmhjbU14TlRrMFgzUnlZVzV6Wm1WeVgyWnliMjFmZDJsMGFGOWtZWFJoS0dGa1pISmxjM01zWVdSa2NtVnpjeXgxYVc1ME1qVTJMR0o1ZEdWYlhTbGliMjlzSWl3Z2JXVjBhRzlrSUNKaGNtTXhOVGswWDJselgybHpjM1ZoWW14bEtDbGliMjlzSWl3Z2JXVjBhRzlrSUNKaGNtTXhOREV3WDJKaGJHRnVZMlZmYjJaZmNHRnlkR2wwYVc5dUtHRmtaSEpsYzNNc1lXUmtjbVZ6Y3lsMWFXNTBNalUySWl3Z2JXVjBhRzlrSUNKaGNtTXlNREJmZEhKaGJuTm1aWElvWVdSa2NtVnpjeXgxYVc1ME1qVTJLV0p2YjJ3aUxDQnRaWFJvYjJRZ0ltRnlZekUwTVRCZmRISmhibk5tWlhKZllubGZjR0Z5ZEdsMGFXOXVLR0ZrWkhKbGMzTXNZV1JrY21WemN5eDFhVzUwTWpVMkxHSjVkR1ZiWFNsaFpHUnlaWE56SWl3Z2JXVjBhRzlrSUNKaGNtTXhOREV3WDNCaGNuUnBkR2x2Ym5OZmIyWW9ZV1JrY21WemN5eDFhVzUwTmpRcFlXUmtjbVZ6YzF0ZElpd2diV1YwYUc5a0lDSmhjbU14TkRFd1gybHpYMjl3WlhKaGRHOXlLR0ZrWkhKbGMzTXNZV1JrY21WemN5eGhaR1J5WlhOektXSnZiMndpTENCdFpYUm9iMlFnSW1GeVl6RTBNVEJmWVhWMGFHOXlhWHBsWDI5d1pYSmhkRzl5S0dGa1pISmxjM01zWVdSa2NtVnpjeXhoWkdSeVpYTnpLWFp2YVdRaUxDQnRaWFJvYjJRZ0ltRnlZekUwTVRCZmNtVjJiMnRsWDI5d1pYSmhkRzl5S0dGa1pISmxjM01zWVdSa2NtVnpjeXhoWkdSeVpYTnpLWFp2YVdRaUxDQnRaWFJvYjJRZ0ltRnlZekUwTVRCZmIzQmxjbUYwYjNKZmRISmhibk5tWlhKZllubGZjR0Z5ZEdsMGFXOXVLR0ZrWkhKbGMzTXNZV1JrY21WemN5eGhaR1J5WlhOekxIVnBiblF5TlRZc1lubDBaVnRkS1dGa1pISmxjM01pTENCdFpYUm9iMlFnSW1GeVl6RTBNVEJmWTJGdVgzUnlZVzV6Wm1WeVgySjVYM0JoY25ScGRHbHZiaWhoWkdSeVpYTnpMR0ZrWkhKbGMzTXNZV1JrY21WemN5eDFhVzUwTWpVMkxHSjVkR1ZiWFNrb1lubDBaU3h6ZEhKcGJtY3NZV1JrY21WemN5a2lMQ0J0WlhSb2IyUWdJbUZ5WXpFME1UQmZZWFYwYUc5eWFYcGxYMjl3WlhKaGRHOXlYMko1WDNCdmNuUnBiMjRvWVdSa2NtVnpjeXhoWkdSeVpYTnpMR0ZrWkhKbGMzTXNkV2x1ZERJMU5pbDJiMmxrSWl3Z2JXVjBhRzlrSUNKaGNtTXhOREV3WDJselgyOXdaWEpoZEc5eVgySjVYM0J2Y25ScGIyNG9ZV1JrY21WemN5eGhaR1J5WlhOekxHRmtaSEpsYzNNcFltOXZiQ0lzSUcxbGRHaHZaQ0FpWVhKak1UUXhNRjlwYzNOMVpWOWllVjl3WVhKMGFYUnBiMjRvWVdSa2NtVnpjeXhoWkdSeVpYTnpMSFZwYm5ReU5UWXNZbmwwWlZ0ZEtYWnZhV1FpTENCdFpYUm9iMlFnSW1GeVl6RTBNVEJmY21Wa1pXVnRYMko1WDNCaGNuUnBkR2x2YmloaFpHUnlaWE56TEhWcGJuUXlOVFlzWW5sMFpWdGRLWFp2YVdRaUxDQnRaWFJvYjJRZ0ltRnlZekUwTVRCZmIzQmxjbUYwYjNKZmNtVmtaV1Z0WDJKNVgzQmhjblJwZEdsdmJpaGhaR1J5WlhOekxHRmtaSEpsYzNNc2RXbHVkREkxTml4aWVYUmxXMTBwZG05cFpDSXNJRzFsZEdodlpDQWlZbTl2ZEhOMGNtRndLR0o1ZEdWYlhTeGllWFJsVzEwc2RXbHVkRGdzZFdsdWRESTFOaWxpYjI5c0lpd2diV1YwYUc5a0lDSmhjbU15TURCZmJtRnRaU2dwWW5sMFpWc3pNbDBpTENCdFpYUm9iMlFnSW1GeVl6SXdNRjl6ZVcxaWIyd29LV0o1ZEdWYk9GMGlMQ0J0WlhSb2IyUWdJbUZ5WXpJd01GOWtaV05wYldGc2N5Z3BkV2x1ZERnaUxDQnRaWFJvYjJRZ0ltRnlZekl3TUY5MGIzUmhiRk4xY0hCc2VTZ3BkV2x1ZERJMU5pSXNJRzFsZEdodlpDQWlZWEpqTWpBd1gySmhiR0Z1WTJWUFppaGhaR1J5WlhOektYVnBiblF5TlRZaUxDQnRaWFJvYjJRZ0ltRnlZekl3TUY5MGNtRnVjMlpsY2taeWIyMG9ZV1JrY21WemN5eGhaR1J5WlhOekxIVnBiblF5TlRZcFltOXZiQ0lzSUcxbGRHaHZaQ0FpWVhKak1qQXdYMkZ3Y0hKdmRtVW9ZV1JrY21WemN5eDFhVzUwTWpVMktXSnZiMndpTENCdFpYUm9iMlFnSW1GeVl6SXdNRjloYkd4dmQyRnVZMlVvWVdSa2NtVnpjeXhoWkdSeVpYTnpLWFZwYm5ReU5UWWlMQ0J0WlhSb2IyUWdJbUZ5WXpnNFgyOTNibVZ5S0NsaFpHUnlaWE56SWl3Z2JXVjBhRzlrSUNKaGNtTTRPRjlwYzE5dmQyNWxjaWhoWkdSeVpYTnpLV0p2YjJ3aUxDQnRaWFJvYjJRZ0ltRnlZemc0WDJsdWFYUnBZV3hwZW1WZmIzZHVaWElvWVdSa2NtVnpjeWwyYjJsa0lpd2diV1YwYUc5a0lDSmhjbU00T0Y5MGNtRnVjMlpsY2w5dmQyNWxjbk5vYVhBb1lXUmtjbVZ6Y3lsMmIybGtJaXdnYldWMGFHOWtJQ0poY21NNE9GOXlaVzV2ZFc1alpWOXZkMjVsY25Ob2FYQW9LWFp2YVdRaUxDQnRaWFJvYjJRZ0ltRnlZemc0WDNSeVlXNXpabVZ5WDI5M2JtVnljMmhwY0Y5eVpYRjFaWE4wS0dGa1pISmxjM01wZG05cFpDSXNJRzFsZEdodlpDQWlZWEpqT0RoZllXTmpaWEIwWDI5M2JtVnljMmhwY0NncGRtOXBaQ0lzSUcxbGRHaHZaQ0FpWVhKak9EaGZZMkZ1WTJWc1gyOTNibVZ5YzJocGNGOXlaWEYxWlhOMEtDbDJiMmxrSWdvZ0lDQWdkSGh1WVNCQmNIQnNhV05oZEdsdmJrRnlaM01nTUFvZ0lDQWdiV0YwWTJnZ2JXRnBibDloY21NeE5qUTBYM05sZEY5amIyNTBjbTlzYkdWeVgzSnZkWFJsUURVZ2JXRnBibDloY21NeE5qUTBYM05sZEY5amIyNTBjbTlzYkdGaWJHVmZjbTkxZEdWQU5pQnRZV2x1WDJGeVl6RTJORFJmYzJWMFgzSmxjWFZwY21WZmFuVnpkR2xtYVdOaGRHbHZibDl5YjNWMFpVQTNJRzFoYVc1ZllYSmpNVFkwTkY5elpYUmZiV2x1WDJGamRHbHZibDlwYm5SbGNuWmhiRjl5YjNWMFpVQTRJRzFoYVc1ZllYSmpNVFkwTkY5cGMxOWpiMjUwY205c2JHRmliR1ZmY205MWRHVkFPU0J0WVdsdVgyRnlZekUyTkRSZlkyOXVkSEp2Ykd4bGNsOTBjbUZ1YzJabGNsOXliM1YwWlVBeE1DQnRZV2x1WDJGeVl6RTJORFJmWTI5dWRISnZiR3hsY2w5eVpXUmxaVzFmY205MWRHVkFNVEVnYldGcGJsOWhjbU14TlRrMFgzTmxkRjlwYzNOMVlXSnNaVjl5YjNWMFpVQXhNaUJ0WVdsdVgyRnlZekUxT1RSZmFYTnpkV1ZmY205MWRHVkFNVE1nYldGcGJsOWhjbU14TlRrMFgzSmxaR1ZsYlVaeWIyMWZjbTkxZEdWQU1UUWdiV0ZwYmw5aGNtTXhOVGswWDNKbFpHVmxiVjl5YjNWMFpVQXhOU0J0WVdsdVgyRnlZekUxT1RSZmRISmhibk5tWlhKZmQybDBhRjlrWVhSaFgzSnZkWFJsUURFMklHMWhhVzVmWVhKak1UVTVORjkwY21GdWMyWmxjbDltY205dFgzZHBkR2hmWkdGMFlWOXliM1YwWlVBeE55QnRZV2x1WDJGeVl6RTFPVFJmYVhOZmFYTnpkV0ZpYkdWZmNtOTFkR1ZBTVRnZ2JXRnBibDloY21NeE5ERXdYMkpoYkdGdVkyVmZiMlpmY0dGeWRHbDBhVzl1WDNKdmRYUmxRREU1SUcxaGFXNWZZWEpqTWpBd1gzUnlZVzV6Wm1WeVgzSnZkWFJsUURJd0lHMWhhVzVmWVhKak1UUXhNRjkwY21GdWMyWmxjbDlpZVY5d1lYSjBhWFJwYjI1ZmNtOTFkR1ZBTWpFZ2JXRnBibDloY21NeE5ERXdYM0JoY25ScGRHbHZibk5mYjJaZmNtOTFkR1ZBTWpJZ2JXRnBibDloY21NeE5ERXdYMmx6WDI5d1pYSmhkRzl5WDNKdmRYUmxRREl6SUcxaGFXNWZZWEpqTVRReE1GOWhkWFJvYjNKcGVtVmZiM0JsY21GMGIzSmZjbTkxZEdWQU1qUWdiV0ZwYmw5aGNtTXhOREV3WDNKbGRtOXJaVjl2Y0dWeVlYUnZjbDl5YjNWMFpVQXlOU0J0WVdsdVgyRnlZekUwTVRCZmIzQmxjbUYwYjNKZmRISmhibk5tWlhKZllubGZjR0Z5ZEdsMGFXOXVYM0p2ZFhSbFFESTJJRzFoYVc1ZllYSmpNVFF4TUY5allXNWZkSEpoYm5ObVpYSmZZbmxmY0dGeWRHbDBhVzl1WDNKdmRYUmxRREkzSUcxaGFXNWZZWEpqTVRReE1GOWhkWFJvYjNKcGVtVmZiM0JsY21GMGIzSmZZbmxmY0c5eWRHbHZibDl5YjNWMFpVQXlPQ0J0WVdsdVgyRnlZekUwTVRCZmFYTmZiM0JsY21GMGIzSmZZbmxmY0c5eWRHbHZibDl5YjNWMFpVQXlPU0J0WVdsdVgyRnlZekUwTVRCZmFYTnpkV1ZmWW5sZmNHRnlkR2wwYVc5dVgzSnZkWFJsUURNd0lHMWhhVzVmWVhKak1UUXhNRjl5WldSbFpXMWZZbmxmY0dGeWRHbDBhVzl1WDNKdmRYUmxRRE14SUcxaGFXNWZZWEpqTVRReE1GOXZjR1Z5WVhSdmNsOXlaV1JsWlcxZllubGZjR0Z5ZEdsMGFXOXVYM0p2ZFhSbFFETXlJRzFoYVc1ZlltOXZkSE4wY21Gd1gzSnZkWFJsUURNeklHMWhhVzVmWVhKak1qQXdYMjVoYldWZmNtOTFkR1ZBTXpRZ2JXRnBibDloY21NeU1EQmZjM2x0WW05c1gzSnZkWFJsUURNMUlHMWhhVzVmWVhKak1qQXdYMlJsWTJsdFlXeHpYM0p2ZFhSbFFETTJJRzFoYVc1ZllYSmpNakF3WDNSdmRHRnNVM1Z3Y0d4NVgzSnZkWFJsUURNM0lHMWhhVzVmWVhKak1qQXdYMkpoYkdGdVkyVlBabDl5YjNWMFpVQXpPQ0J0WVdsdVgyRnlZekl3TUY5MGNtRnVjMlpsY2taeWIyMWZjbTkxZEdWQU16a2diV0ZwYmw5aGNtTXlNREJmWVhCd2NtOTJaVjl5YjNWMFpVQTBNQ0J0WVdsdVgyRnlZekl3TUY5aGJHeHZkMkZ1WTJWZmNtOTFkR1ZBTkRFZ2JXRnBibDloY21NNE9GOXZkMjVsY2w5eWIzVjBaVUEwTWlCdFlXbHVYMkZ5WXpnNFgybHpYMjkzYm1WeVgzSnZkWFJsUURReklHMWhhVzVmWVhKak9EaGZhVzVwZEdsaGJHbDZaVjl2ZDI1bGNsOXliM1YwWlVBME5DQnRZV2x1WDJGeVl6ZzRYM1J5WVc1elptVnlYMjkzYm1WeWMyaHBjRjl5YjNWMFpVQTBOU0J0WVdsdVgyRnlZemc0WDNKbGJtOTFibU5sWDI5M2JtVnljMmhwY0Y5eWIzVjBaVUEwTmlCdFlXbHVYMkZ5WXpnNFgzUnlZVzV6Wm1WeVgyOTNibVZ5YzJocGNGOXlaWEYxWlhOMFgzSnZkWFJsUURRM0lHMWhhVzVmWVhKak9EaGZZV05qWlhCMFgyOTNibVZ5YzJocGNGOXliM1YwWlVBME9DQnRZV2x1WDJGeVl6ZzRYMk5oYm1ObGJGOXZkMjVsY25Ob2FYQmZjbVZ4ZFdWemRGOXliM1YwWlVBME9Rb0tiV0ZwYmw5aFpuUmxjbDlwWmw5bGJITmxRRFUyT2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pveU9Rb2dJQ0FnTHk4Z1pYaHdiM0owSUdOc1lYTnpJRUZ5WXpFMk5EUWdaWGgwWlc1a2N5QkJjbU14TlRrMElIc0tJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0J5WlhSMWNtNEtDbTFoYVc1ZllYSmpPRGhmWTJGdVkyVnNYMjkzYm1WeWMyaHBjRjl5WlhGMVpYTjBYM0p2ZFhSbFFEUTVPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZNVEF6Q2lBZ0lDQXZMeUJBWVhKak5DNWhZbWx0WlhSb2IyUW9LUW9nSUNBZ2RIaHVJRTl1UTI5dGNHeGxkR2x2YmdvZ0lDQWdJUW9nSUNBZ1lYTnpaWEowSUM4dklFOXVRMjl0Y0d4bGRHbHZiaUJwY3lCdWIzUWdUbTlQY0FvZ0lDQWdkSGh1SUVGd2NHeHBZMkYwYVc5dVNVUUtJQ0FnSUdGemMyVnlkQ0F2THlCallXNGdiMjVzZVNCallXeHNJSGRvWlc0Z2JtOTBJR055WldGMGFXNW5DaUFnSUNCallXeHNjM1ZpSUdGeVl6ZzRYMk5oYm1ObGJGOXZkMjVsY25Ob2FYQmZjbVZ4ZFdWemRBb2dJQ0FnYVc1MFkxOHhJQzh2SURFS0lDQWdJSEpsZEhWeWJnb0tiV0ZwYmw5aGNtTTRPRjloWTJObGNIUmZiM2R1WlhKemFHbHdYM0p2ZFhSbFFEUTRPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZPVEFLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDZ3BDaUFnSUNCMGVHNGdUMjVEYjIxd2JHVjBhVzl1Q2lBZ0lDQWhDaUFnSUNCaGMzTmxjblFnTHk4Z1QyNURiMjF3YkdWMGFXOXVJR2x6SUc1dmRDQk9iMDl3Q2lBZ0lDQjBlRzRnUVhCd2JHbGpZWFJwYjI1SlJBb2dJQ0FnWVhOelpYSjBJQzh2SUdOaGJpQnZibXg1SUdOaGJHd2dkMmhsYmlCdWIzUWdZM0psWVhScGJtY0tJQ0FnSUdOaGJHeHpkV0lnWVhKak9EaGZZV05qWlhCMFgyOTNibVZ5YzJocGNBb2dJQ0FnYVc1MFkxOHhJQzh2SURFS0lDQWdJSEpsZEhWeWJnb0tiV0ZwYmw5aGNtTTRPRjkwY21GdWMyWmxjbDl2ZDI1bGNuTm9hWEJmY21WeGRXVnpkRjl5YjNWMFpVQTBOem9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPamM0Q2lBZ0lDQXZMeUJBWVhKak5DNWhZbWx0WlhSb2IyUW9LUW9nSUNBZ2RIaHVJRTl1UTI5dGNHeGxkR2x2YmdvZ0lDQWdJUW9nSUNBZ1lYTnpaWEowSUM4dklFOXVRMjl0Y0d4bGRHbHZiaUJwY3lCdWIzUWdUbTlQY0FvZ0lDQWdkSGh1SUVGd2NHeHBZMkYwYVc5dVNVUUtJQ0FnSUdGemMyVnlkQ0F2THlCallXNGdiMjVzZVNCallXeHNJSGRvWlc0Z2JtOTBJR055WldGMGFXNW5DaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPakk1Q2lBZ0lDQXZMeUJsZUhCdmNuUWdZMnhoYzNNZ1FYSmpNVFkwTkNCbGVIUmxibVJ6SUVGeVl6RTFPVFFnZXdvZ0lDQWdkSGh1WVNCQmNIQnNhV05oZEdsdmJrRnlaM01nTVFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZemc0TG1Gc1oyOHVkSE02TnpnS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2dwQ2lBZ0lDQmpZV3hzYzNWaUlHRnlZemc0WDNSeVlXNXpabVZ5WDI5M2JtVnljMmhwY0Y5eVpYRjFaWE4wQ2lBZ0lDQnBiblJqWHpFZ0x5OGdNUW9nSUNBZ2NtVjBkWEp1Q2dwdFlXbHVYMkZ5WXpnNFgzSmxibTkxYm1ObFgyOTNibVZ5YzJocGNGOXliM1YwWlVBME5qb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NNE9DNWhiR2R2TG5Sek9qWTRDaUFnSUNBdkx5QkFZWEpqTkM1aFltbHRaWFJvYjJRb0tRb2dJQ0FnZEhodUlFOXVRMjl0Y0d4bGRHbHZiZ29nSUNBZ0lRb2dJQ0FnWVhOelpYSjBJQzh2SUU5dVEyOXRjR3hsZEdsdmJpQnBjeUJ1YjNRZ1RtOVBjQW9nSUNBZ2RIaHVJRUZ3Y0d4cFkyRjBhVzl1U1VRS0lDQWdJR0Z6YzJWeWRDQXZMeUJqWVc0Z2IyNXNlU0JqWVd4c0lIZG9aVzRnYm05MElHTnlaV0YwYVc1bkNpQWdJQ0JqWVd4c2MzVmlJR0Z5WXpnNFgzSmxibTkxYm1ObFgyOTNibVZ5YzJocGNBb2dJQ0FnYVc1MFkxOHhJQzh2SURFS0lDQWdJSEpsZEhWeWJnb0tiV0ZwYmw5aGNtTTRPRjkwY21GdWMyWmxjbDl2ZDI1bGNuTm9hWEJmY205MWRHVkFORFU2Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpPRGd1WVd4bmJ5NTBjem8xT0FvZ0lDQWdMeThnUUdGeVl6UXVZV0pwYldWMGFHOWtLQ2tLSUNBZ0lIUjRiaUJQYmtOdmJYQnNaWFJwYjI0S0lDQWdJQ0VLSUNBZ0lHRnpjMlZ5ZENBdkx5QlBia052YlhCc1pYUnBiMjRnYVhNZ2JtOTBJRTV2VDNBS0lDQWdJSFI0YmlCQmNIQnNhV05oZEdsdmJrbEVDaUFnSUNCaGMzTmxjblFnTHk4Z1kyRnVJRzl1YkhrZ1kyRnNiQ0IzYUdWdUlHNXZkQ0JqY21WaGRHbHVad29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem95T1FvZ0lDQWdMeThnWlhod2IzSjBJR05zWVhOeklFRnlZekUyTkRRZ1pYaDBaVzVrY3lCQmNtTXhOVGswSUhzS0lDQWdJSFI0Ym1FZ1FYQndiR2xqWVhScGIyNUJjbWR6SURFS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU00T0M1aGJHZHZMblJ6T2pVNENpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvS1FvZ0lDQWdZMkZzYkhOMVlpQmhjbU00T0Y5MGNtRnVjMlpsY2w5dmQyNWxjbk5vYVhBS0lDQWdJR2x1ZEdOZk1TQXZMeUF4Q2lBZ0lDQnlaWFIxY200S0NtMWhhVzVmWVhKak9EaGZhVzVwZEdsaGJHbDZaVjl2ZDI1bGNsOXliM1YwWlVBME5Eb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NNE9DNWhiR2R2TG5Sek9qVXdDaUFnSUNBdkx5QkFZWEpqTkM1aFltbHRaWFJvYjJRb0tRb2dJQ0FnZEhodUlFOXVRMjl0Y0d4bGRHbHZiZ29nSUNBZ0lRb2dJQ0FnWVhOelpYSjBJQzh2SUU5dVEyOXRjR3hsZEdsdmJpQnBjeUJ1YjNRZ1RtOVBjQW9nSUNBZ2RIaHVJRUZ3Y0d4cFkyRjBhVzl1U1VRS0lDQWdJR0Z6YzJWeWRDQXZMeUJqWVc0Z2IyNXNlU0JqWVd4c0lIZG9aVzRnYm05MElHTnlaV0YwYVc1bkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qSTVDaUFnSUNBdkx5QmxlSEJ2Y25RZ1kyeGhjM01nUVhKak1UWTBOQ0JsZUhSbGJtUnpJRUZ5WXpFMU9UUWdld29nSUNBZ2RIaHVZU0JCY0hCc2FXTmhkR2x2YmtGeVozTWdNUW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZOVEFLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDZ3BDaUFnSUNCallXeHNjM1ZpSUdGeVl6ZzRYMmx1YVhScFlXeHBlbVZmYjNkdVpYSUtJQ0FnSUdsdWRHTmZNU0F2THlBeENpQWdJQ0J5WlhSMWNtNEtDbTFoYVc1ZllYSmpPRGhmYVhOZmIzZHVaWEpmY205MWRHVkFORE02Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpPRGd1WVd4bmJ5NTBjem8wTVFvZ0lDQWdMeThnUUdGeVl6UXVZV0pwYldWMGFHOWtLSHNnY21WaFpHOXViSGs2SUhSeWRXVWdmU2tLSUNBZ0lIUjRiaUJQYmtOdmJYQnNaWFJwYjI0S0lDQWdJQ0VLSUNBZ0lHRnpjMlZ5ZENBdkx5QlBia052YlhCc1pYUnBiMjRnYVhNZ2JtOTBJRTV2VDNBS0lDQWdJSFI0YmlCQmNIQnNhV05oZEdsdmJrbEVDaUFnSUNCaGMzTmxjblFnTHk4Z1kyRnVJRzl1YkhrZ1kyRnNiQ0IzYUdWdUlHNXZkQ0JqY21WaGRHbHVad29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem95T1FvZ0lDQWdMeThnWlhod2IzSjBJR05zWVhOeklFRnlZekUyTkRRZ1pYaDBaVzVrY3lCQmNtTXhOVGswSUhzS0lDQWdJSFI0Ym1FZ1FYQndiR2xqWVhScGIyNUJjbWR6SURFS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU00T0M1aGJHZHZMblJ6T2pReENpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvZXlCeVpXRmtiMjVzZVRvZ2RISjFaU0I5S1FvZ0lDQWdZMkZzYkhOMVlpQmhjbU00T0Y5cGMxOXZkMjVsY2dvZ0lDQWdZbmwwWldOZk1DQXZMeUF3ZURFMU1XWTNZemMxQ2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR3h2WndvZ0lDQWdhVzUwWTE4eElDOHZJREVLSUNBZ0lISmxkSFZ5YmdvS2JXRnBibDloY21NNE9GOXZkMjVsY2w5eWIzVjBaVUEwTWpvS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU00T0M1aGJHZHZMblJ6T2pNMUNpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvZXlCeVpXRmtiMjVzZVRvZ2RISjFaU0I5S1FvZ0lDQWdkSGh1SUU5dVEyOXRjR3hsZEdsdmJnb2dJQ0FnSVFvZ0lDQWdZWE56WlhKMElDOHZJRTl1UTI5dGNHeGxkR2x2YmlCcGN5QnViM1FnVG05UGNBb2dJQ0FnZEhodUlFRndjR3hwWTJGMGFXOXVTVVFLSUNBZ0lHRnpjMlZ5ZENBdkx5QmpZVzRnYjI1c2VTQmpZV3hzSUhkb1pXNGdibTkwSUdOeVpXRjBhVzVuQ2lBZ0lDQmpZV3hzYzNWaUlHRnlZemc0WDI5M2JtVnlDaUFnSUNCaWVYUmxZMTh3SUM4dklEQjRNVFV4Wmpkak56VUtJQ0FnSUhOM1lYQUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ2JHOW5DaUFnSUNCcGJuUmpYekVnTHk4Z01Rb2dJQ0FnY21WMGRYSnVDZ3B0WVdsdVgyRnlZekl3TUY5aGJHeHZkMkZ1WTJWZmNtOTFkR1ZBTkRFNkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk1UYzNDaUFnSUNBdkx5QkFZWEpqTkM1aFltbHRaWFJvYjJRb2V5QnlaV0ZrYjI1c2VUb2dkSEoxWlNCOUtRb2dJQ0FnZEhodUlFOXVRMjl0Y0d4bGRHbHZiZ29nSUNBZ0lRb2dJQ0FnWVhOelpYSjBJQzh2SUU5dVEyOXRjR3hsZEdsdmJpQnBjeUJ1YjNRZ1RtOVBjQW9nSUNBZ2RIaHVJRUZ3Y0d4cFkyRjBhVzl1U1VRS0lDQWdJR0Z6YzJWeWRDQXZMeUJqWVc0Z2IyNXNlU0JqWVd4c0lIZG9aVzRnYm05MElHTnlaV0YwYVc1bkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qSTVDaUFnSUNBdkx5QmxlSEJ2Y25RZ1kyeGhjM01nUVhKak1UWTBOQ0JsZUhSbGJtUnpJRUZ5WXpFMU9UUWdld29nSUNBZ2RIaHVZU0JCY0hCc2FXTmhkR2x2YmtGeVozTWdNUW9nSUNBZ2RIaHVZU0JCY0hCc2FXTmhkR2x2YmtGeVozTWdNZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPakUzTndvZ0lDQWdMeThnUUdGeVl6UXVZV0pwYldWMGFHOWtLSHNnY21WaFpHOXViSGs2SUhSeWRXVWdmU2tLSUNBZ0lHTmhiR3h6ZFdJZ1lYSmpNakF3WDJGc2JHOTNZVzVqWlFvZ0lDQWdZbmwwWldOZk1DQXZMeUF3ZURFMU1XWTNZemMxQ2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR3h2WndvZ0lDQWdhVzUwWTE4eElDOHZJREVLSUNBZ0lISmxkSFZ5YmdvS2JXRnBibDloY21NeU1EQmZZWEJ3Y205MlpWOXliM1YwWlVBME1Eb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeU1EQXVZV3huYnk1MGN6b3hOalVLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDZ3BDaUFnSUNCMGVHNGdUMjVEYjIxd2JHVjBhVzl1Q2lBZ0lDQWhDaUFnSUNCaGMzTmxjblFnTHk4Z1QyNURiMjF3YkdWMGFXOXVJR2x6SUc1dmRDQk9iMDl3Q2lBZ0lDQjBlRzRnUVhCd2JHbGpZWFJwYjI1SlJBb2dJQ0FnWVhOelpYSjBJQzh2SUdOaGJpQnZibXg1SUdOaGJHd2dkMmhsYmlCdWIzUWdZM0psWVhScGJtY0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZNamtLSUNBZ0lDOHZJR1Y0Y0c5eWRDQmpiR0Z6Y3lCQmNtTXhOalEwSUdWNGRHVnVaSE1nUVhKak1UVTVOQ0I3Q2lBZ0lDQjBlRzVoSUVGd2NHeHBZMkYwYVc5dVFYSm5jeUF4Q2lBZ0lDQjBlRzVoSUVGd2NHeHBZMkYwYVc5dVFYSm5jeUF5Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNakF3TG1Gc1oyOHVkSE02TVRZMUNpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvS1FvZ0lDQWdZMkZzYkhOMVlpQmhjbU15TURCZllYQndjbTkyWlFvZ0lDQWdZbmwwWldOZk1DQXZMeUF3ZURFMU1XWTNZemMxQ2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR3h2WndvZ0lDQWdhVzUwWTE4eElDOHZJREVLSUNBZ0lISmxkSFZ5YmdvS2JXRnBibDloY21NeU1EQmZkSEpoYm5ObVpYSkdjbTl0WDNKdmRYUmxRRE01T2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekl3TUM1aGJHZHZMblJ6T2pFME9Bb2dJQ0FnTHk4Z1FHRnlZelF1WVdKcGJXVjBhRzlrS0NrS0lDQWdJSFI0YmlCUGJrTnZiWEJzWlhScGIyNEtJQ0FnSUNFS0lDQWdJR0Z6YzJWeWRDQXZMeUJQYmtOdmJYQnNaWFJwYjI0Z2FYTWdibTkwSUU1dlQzQUtJQ0FnSUhSNGJpQkJjSEJzYVdOaGRHbHZia2xFQ2lBZ0lDQmhjM05sY25RZ0x5OGdZMkZ1SUc5dWJIa2dZMkZzYkNCM2FHVnVJRzV2ZENCamNtVmhkR2x1WndvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pveU9Rb2dJQ0FnTHk4Z1pYaHdiM0owSUdOc1lYTnpJRUZ5WXpFMk5EUWdaWGgwWlc1a2N5QkJjbU14TlRrMElIc0tJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklERUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklESUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklETUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeU1EQXVZV3huYnk1MGN6b3hORGdLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDZ3BDaUFnSUNCallXeHNjM1ZpSUdGeVl6SXdNRjkwY21GdWMyWmxja1p5YjIwS0lDQWdJR0o1ZEdWalh6QWdMeThnTUhneE5URm1OMk0zTlFvZ0lDQWdjM2RoY0FvZ0lDQWdZMjl1WTJGMENpQWdJQ0JzYjJjS0lDQWdJR2x1ZEdOZk1TQXZMeUF4Q2lBZ0lDQnlaWFIxY200S0NtMWhhVzVmWVhKak1qQXdYMkpoYkdGdVkyVlBabDl5YjNWMFpVQXpPRG9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem94TWpNS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2g3SUhKbFlXUnZibXg1T2lCMGNuVmxJSDBwQ2lBZ0lDQjBlRzRnVDI1RGIyMXdiR1YwYVc5dUNpQWdJQ0FoQ2lBZ0lDQmhjM05sY25RZ0x5OGdUMjVEYjIxd2JHVjBhVzl1SUdseklHNXZkQ0JPYjA5d0NpQWdJQ0IwZUc0Z1FYQndiR2xqWVhScGIyNUpSQW9nSUNBZ1lYTnpaWEowSUM4dklHTmhiaUJ2Ym14NUlHTmhiR3dnZDJobGJpQnViM1FnWTNKbFlYUnBibWNLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TWprS0lDQWdJQzh2SUdWNGNHOXlkQ0JqYkdGemN5QkJjbU14TmpRMElHVjRkR1Z1WkhNZ1FYSmpNVFU1TkNCN0NpQWdJQ0IwZUc1aElFRndjR3hwWTJGMGFXOXVRWEpuY3lBeENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk1USXpDaUFnSUNBdkx5QkFZWEpqTkM1aFltbHRaWFJvYjJRb2V5QnlaV0ZrYjI1c2VUb2dkSEoxWlNCOUtRb2dJQ0FnWTJGc2JITjFZaUJoY21NeU1EQmZZbUZzWVc1alpVOW1DaUFnSUNCaWVYUmxZMTh3SUM4dklEQjRNVFV4Wmpkak56VUtJQ0FnSUhOM1lYQUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ2JHOW5DaUFnSUNCcGJuUmpYekVnTHk4Z01Rb2dJQ0FnY21WMGRYSnVDZ3B0WVdsdVgyRnlZekl3TUY5MGIzUmhiRk4xY0hCc2VWOXliM1YwWlVBek56b0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeU1EQXVZV3huYnk1MGN6b3hNVElLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDaDdJSEpsWVdSdmJteDVPaUIwY25WbElIMHBDaUFnSUNCMGVHNGdUMjVEYjIxd2JHVjBhVzl1Q2lBZ0lDQWhDaUFnSUNCaGMzTmxjblFnTHk4Z1QyNURiMjF3YkdWMGFXOXVJR2x6SUc1dmRDQk9iMDl3Q2lBZ0lDQjBlRzRnUVhCd2JHbGpZWFJwYjI1SlJBb2dJQ0FnWVhOelpYSjBJQzh2SUdOaGJpQnZibXg1SUdOaGJHd2dkMmhsYmlCdWIzUWdZM0psWVhScGJtY0tJQ0FnSUdOaGJHeHpkV0lnWVhKak1qQXdYM1J2ZEdGc1UzVndjR3g1Q2lBZ0lDQmllWFJsWTE4d0lDOHZJREI0TVRVeFpqZGpOelVLSUNBZ0lITjNZWEFLSUNBZ0lHTnZibU5oZEFvZ0lDQWdiRzluQ2lBZ0lDQnBiblJqWHpFZ0x5OGdNUW9nSUNBZ2NtVjBkWEp1Q2dwdFlXbHVYMkZ5WXpJd01GOWtaV05wYldGc2MxOXliM1YwWlVBek5qb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeU1EQXVZV3huYnk1MGN6b3hNRElLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDaDdJSEpsWVdSdmJteDVPaUIwY25WbElIMHBDaUFnSUNCMGVHNGdUMjVEYjIxd2JHVjBhVzl1Q2lBZ0lDQWhDaUFnSUNCaGMzTmxjblFnTHk4Z1QyNURiMjF3YkdWMGFXOXVJR2x6SUc1dmRDQk9iMDl3Q2lBZ0lDQjBlRzRnUVhCd2JHbGpZWFJwYjI1SlJBb2dJQ0FnWVhOelpYSjBJQzh2SUdOaGJpQnZibXg1SUdOaGJHd2dkMmhsYmlCdWIzUWdZM0psWVhScGJtY0tJQ0FnSUdOaGJHeHpkV0lnWVhKak1qQXdYMlJsWTJsdFlXeHpDaUFnSUNCaWVYUmxZMTh3SUM4dklEQjRNVFV4Wmpkak56VUtJQ0FnSUhOM1lYQUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ2JHOW5DaUFnSUNCcGJuUmpYekVnTHk4Z01Rb2dJQ0FnY21WMGRYSnVDZ3B0WVdsdVgyRnlZekl3TUY5emVXMWliMnhmY205MWRHVkFNelU2Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNakF3TG1Gc1oyOHVkSE02T1RJS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2g3SUhKbFlXUnZibXg1T2lCMGNuVmxJSDBwQ2lBZ0lDQjBlRzRnVDI1RGIyMXdiR1YwYVc5dUNpQWdJQ0FoQ2lBZ0lDQmhjM05sY25RZ0x5OGdUMjVEYjIxd2JHVjBhVzl1SUdseklHNXZkQ0JPYjA5d0NpQWdJQ0IwZUc0Z1FYQndiR2xqWVhScGIyNUpSQW9nSUNBZ1lYTnpaWEowSUM4dklHTmhiaUJ2Ym14NUlHTmhiR3dnZDJobGJpQnViM1FnWTNKbFlYUnBibWNLSUNBZ0lHTmhiR3h6ZFdJZ1lYSmpNakF3WDNONWJXSnZiQW9nSUNBZ1lubDBaV05mTUNBdkx5QXdlREUxTVdZM1l6YzFDaUFnSUNCemQyRndDaUFnSUNCamIyNWpZWFFLSUNBZ0lHeHZad29nSUNBZ2FXNTBZMTh4SUM4dklERUtJQ0FnSUhKbGRIVnliZ29LYldGcGJsOWhjbU15TURCZmJtRnRaVjl5YjNWMFpVQXpORG9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem80TWdvZ0lDQWdMeThnUUdGeVl6UXVZV0pwYldWMGFHOWtLSHNnY21WaFpHOXViSGs2SUhSeWRXVWdmU2tLSUNBZ0lIUjRiaUJQYmtOdmJYQnNaWFJwYjI0S0lDQWdJQ0VLSUNBZ0lHRnpjMlZ5ZENBdkx5QlBia052YlhCc1pYUnBiMjRnYVhNZ2JtOTBJRTV2VDNBS0lDQWdJSFI0YmlCQmNIQnNhV05oZEdsdmJrbEVDaUFnSUNCaGMzTmxjblFnTHk4Z1kyRnVJRzl1YkhrZ1kyRnNiQ0IzYUdWdUlHNXZkQ0JqY21WaGRHbHVad29nSUNBZ1kyRnNiSE4xWWlCaGNtTXlNREJmYm1GdFpRb2dJQ0FnWW5sMFpXTmZNQ0F2THlBd2VERTFNV1kzWXpjMUNpQWdJQ0J6ZDJGd0NpQWdJQ0JqYjI1allYUUtJQ0FnSUd4dlp3b2dJQ0FnYVc1MFkxOHhJQzh2SURFS0lDQWdJSEpsZEhWeWJnb0tiV0ZwYmw5aWIyOTBjM1J5WVhCZmNtOTFkR1ZBTXpNNkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk5UWUtJQ0FnSUM4dklFQmhjbU0wTG1GaWFXMWxkR2h2WkNncENpQWdJQ0IwZUc0Z1QyNURiMjF3YkdWMGFXOXVDaUFnSUNBaENpQWdJQ0JoYzNObGNuUWdMeThnVDI1RGIyMXdiR1YwYVc5dUlHbHpJRzV2ZENCT2IwOXdDaUFnSUNCMGVHNGdRWEJ3YkdsallYUnBiMjVKUkFvZ0lDQWdZWE56WlhKMElDOHZJR05oYmlCdmJteDVJR05oYkd3Z2QyaGxiaUJ1YjNRZ1kzSmxZWFJwYm1jS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk1qa0tJQ0FnSUM4dklHVjRjRzl5ZENCamJHRnpjeUJCY21NeE5qUTBJR1Y0ZEdWdVpITWdRWEpqTVRVNU5DQjdDaUFnSUNCMGVHNWhJRUZ3Y0d4cFkyRjBhVzl1UVhKbmN5QXhDaUFnSUNCMGVHNWhJRUZ3Y0d4cFkyRjBhVzl1UVhKbmN5QXlDaUFnSUNCMGVHNWhJRUZ3Y0d4cFkyRjBhVzl1UVhKbmN5QXpDaUFnSUNCMGVHNWhJRUZ3Y0d4cFkyRjBhVzl1UVhKbmN5QTBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1qQXdMbUZzWjI4dWRITTZOVFlLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDZ3BDaUFnSUNCallXeHNjM1ZpSUdKdmIzUnpkSEpoY0FvZ0lDQWdZbmwwWldOZk1DQXZMeUF3ZURFMU1XWTNZemMxQ2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR3h2WndvZ0lDQWdhVzUwWTE4eElDOHZJREVLSUNBZ0lISmxkSFZ5YmdvS2JXRnBibDloY21NeE5ERXdYMjl3WlhKaGRHOXlYM0psWkdWbGJWOWllVjl3WVhKMGFYUnBiMjVmY205MWRHVkFNekk2Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pReU13b2dJQ0FnTHk4Z1FHRnlZelF1WVdKcGJXVjBhRzlrS0NrS0lDQWdJSFI0YmlCUGJrTnZiWEJzWlhScGIyNEtJQ0FnSUNFS0lDQWdJR0Z6YzJWeWRDQXZMeUJQYmtOdmJYQnNaWFJwYjI0Z2FYTWdibTkwSUU1dlQzQUtJQ0FnSUhSNGJpQkJjSEJzYVdOaGRHbHZia2xFQ2lBZ0lDQmhjM05sY25RZ0x5OGdZMkZ1SUc5dWJIa2dZMkZzYkNCM2FHVnVJRzV2ZENCamNtVmhkR2x1WndvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pveU9Rb2dJQ0FnTHk4Z1pYaHdiM0owSUdOc1lYTnpJRUZ5WXpFMk5EUWdaWGgwWlc1a2N5QkJjbU14TlRrMElIc0tJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklERUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklESUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklETUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklEUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZOREl6Q2lBZ0lDQXZMeUJBWVhKak5DNWhZbWx0WlhSb2IyUW9LUW9nSUNBZ1kyRnNiSE4xWWlCaGNtTXhOREV3WDI5d1pYSmhkRzl5WDNKbFpHVmxiVjlpZVY5d1lYSjBhWFJwYjI0S0lDQWdJR2x1ZEdOZk1TQXZMeUF4Q2lBZ0lDQnlaWFIxY200S0NtMWhhVzVmWVhKak1UUXhNRjl5WldSbFpXMWZZbmxmY0dGeWRHbDBhVzl1WDNKdmRYUmxRRE14T2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvME1EZ0tJQ0FnSUM4dklFQmhjbU0wTG1GaWFXMWxkR2h2WkNncENpQWdJQ0IwZUc0Z1QyNURiMjF3YkdWMGFXOXVDaUFnSUNBaENpQWdJQ0JoYzNObGNuUWdMeThnVDI1RGIyMXdiR1YwYVc5dUlHbHpJRzV2ZENCT2IwOXdDaUFnSUNCMGVHNGdRWEJ3YkdsallYUnBiMjVKUkFvZ0lDQWdZWE56WlhKMElDOHZJR05oYmlCdmJteDVJR05oYkd3Z2QyaGxiaUJ1YjNRZ1kzSmxZWFJwYm1jS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk1qa0tJQ0FnSUM4dklHVjRjRzl5ZENCamJHRnpjeUJCY21NeE5qUTBJR1Y0ZEdWdVpITWdRWEpqTVRVNU5DQjdDaUFnSUNCMGVHNWhJRUZ3Y0d4cFkyRjBhVzl1UVhKbmN5QXhDaUFnSUNCMGVHNWhJRUZ3Y0d4cFkyRjBhVzl1UVhKbmN5QXlDaUFnSUNCMGVHNWhJRUZ3Y0d4cFkyRjBhVzl1UVhKbmN5QXpDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPalF3T0FvZ0lDQWdMeThnUUdGeVl6UXVZV0pwYldWMGFHOWtLQ2tLSUNBZ0lHTmhiR3h6ZFdJZ1lYSmpNVFF4TUY5eVpXUmxaVzFmWW5sZmNHRnlkR2wwYVc5dUNpQWdJQ0JwYm5Salh6RWdMeThnTVFvZ0lDQWdjbVYwZFhKdUNncHRZV2x1WDJGeVl6RTBNVEJmYVhOemRXVmZZbmxmY0dGeWRHbDBhVzl1WDNKdmRYUmxRRE13T2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvek9ETUtJQ0FnSUM4dklFQmhjbU0wTG1GaWFXMWxkR2h2WkNncENpQWdJQ0IwZUc0Z1QyNURiMjF3YkdWMGFXOXVDaUFnSUNBaENpQWdJQ0JoYzNObGNuUWdMeThnVDI1RGIyMXdiR1YwYVc5dUlHbHpJRzV2ZENCT2IwOXdDaUFnSUNCMGVHNGdRWEJ3YkdsallYUnBiMjVKUkFvZ0lDQWdZWE56WlhKMElDOHZJR05oYmlCdmJteDVJR05oYkd3Z2QyaGxiaUJ1YjNRZ1kzSmxZWFJwYm1jS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk1qa0tJQ0FnSUM4dklHVjRjRzl5ZENCamJHRnpjeUJCY21NeE5qUTBJR1Y0ZEdWdVpITWdRWEpqTVRVNU5DQjdDaUFnSUNCMGVHNWhJRUZ3Y0d4cFkyRjBhVzl1UVhKbmN5QXhDaUFnSUNCMGVHNWhJRUZ3Y0d4cFkyRjBhVzl1UVhKbmN5QXlDaUFnSUNCMGVHNWhJRUZ3Y0d4cFkyRjBhVzl1UVhKbmN5QXpDaUFnSUNCMGVHNWhJRUZ3Y0d4cFkyRjBhVzl1UVhKbmN5QTBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPak00TXdvZ0lDQWdMeThnUUdGeVl6UXVZV0pwYldWMGFHOWtLQ2tLSUNBZ0lHTmhiR3h6ZFdJZ1lYSmpNVFF4TUY5cGMzTjFaVjlpZVY5d1lYSjBhWFJwYjI0S0lDQWdJR2x1ZEdOZk1TQXZMeUF4Q2lBZ0lDQnlaWFIxY200S0NtMWhhVzVmWVhKak1UUXhNRjlwYzE5dmNHVnlZWFJ2Y2w5aWVWOXdiM0owYVc5dVgzSnZkWFJsUURJNU9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3pOekVLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDaDdJSEpsWVdSdmJteDVPaUIwY25WbElIMHBDaUFnSUNCMGVHNGdUMjVEYjIxd2JHVjBhVzl1Q2lBZ0lDQWhDaUFnSUNCaGMzTmxjblFnTHk4Z1QyNURiMjF3YkdWMGFXOXVJR2x6SUc1dmRDQk9iMDl3Q2lBZ0lDQjBlRzRnUVhCd2JHbGpZWFJwYjI1SlJBb2dJQ0FnWVhOelpYSjBJQzh2SUdOaGJpQnZibXg1SUdOaGJHd2dkMmhsYmlCdWIzUWdZM0psWVhScGJtY0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZNamtLSUNBZ0lDOHZJR1Y0Y0c5eWRDQmpiR0Z6Y3lCQmNtTXhOalEwSUdWNGRHVnVaSE1nUVhKak1UVTVOQ0I3Q2lBZ0lDQjBlRzVoSUVGd2NHeHBZMkYwYVc5dVFYSm5jeUF4Q2lBZ0lDQjBlRzVoSUVGd2NHeHBZMkYwYVc5dVFYSm5jeUF5Q2lBZ0lDQjBlRzVoSUVGd2NHeHBZMkYwYVc5dVFYSm5jeUF6Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pNM01Rb2dJQ0FnTHk4Z1FHRnlZelF1WVdKcGJXVjBhRzlrS0hzZ2NtVmhaRzl1YkhrNklIUnlkV1VnZlNrS0lDQWdJR05oYkd4emRXSWdZWEpqTVRReE1GOXBjMTl2Y0dWeVlYUnZjbDlpZVY5d2IzSjBhVzl1Q2lBZ0lDQmllWFJsWTE4d0lDOHZJREI0TVRVeFpqZGpOelVLSUNBZ0lITjNZWEFLSUNBZ0lHTnZibU5oZEFvZ0lDQWdiRzluQ2lBZ0lDQnBiblJqWHpFZ0x5OGdNUW9nSUNBZ2NtVjBkWEp1Q2dwdFlXbHVYMkZ5WXpFME1UQmZZWFYwYUc5eWFYcGxYMjl3WlhKaGRHOXlYMko1WDNCdmNuUnBiMjVmY205MWRHVkFNamc2Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pNMU9Rb2dJQ0FnTHk4Z1FHRnlZelF1WVdKcGJXVjBhRzlrS0NrS0lDQWdJSFI0YmlCUGJrTnZiWEJzWlhScGIyNEtJQ0FnSUNFS0lDQWdJR0Z6YzJWeWRDQXZMeUJQYmtOdmJYQnNaWFJwYjI0Z2FYTWdibTkwSUU1dlQzQUtJQ0FnSUhSNGJpQkJjSEJzYVdOaGRHbHZia2xFQ2lBZ0lDQmhjM05sY25RZ0x5OGdZMkZ1SUc5dWJIa2dZMkZzYkNCM2FHVnVJRzV2ZENCamNtVmhkR2x1WndvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pveU9Rb2dJQ0FnTHk4Z1pYaHdiM0owSUdOc1lYTnpJRUZ5WXpFMk5EUWdaWGgwWlc1a2N5QkJjbU14TlRrMElIc0tJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklERUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklESUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklETUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklEUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNelU1Q2lBZ0lDQXZMeUJBWVhKak5DNWhZbWx0WlhSb2IyUW9LUW9nSUNBZ1kyRnNiSE4xWWlCaGNtTXhOREV3WDJGMWRHaHZjbWw2WlY5dmNHVnlZWFJ2Y2w5aWVWOXdiM0owYVc5dUNpQWdJQ0JwYm5Salh6RWdMeThnTVFvZ0lDQWdjbVYwZFhKdUNncHRZV2x1WDJGeVl6RTBNVEJmWTJGdVgzUnlZVzV6Wm1WeVgySjVYM0JoY25ScGRHbHZibDl5YjNWMFpVQXlOem9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TVRjMENpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvS1FvZ0lDQWdkSGh1SUU5dVEyOXRjR3hsZEdsdmJnb2dJQ0FnSVFvZ0lDQWdZWE56WlhKMElDOHZJRTl1UTI5dGNHeGxkR2x2YmlCcGN5QnViM1FnVG05UGNBb2dJQ0FnZEhodUlFRndjR3hwWTJGMGFXOXVTVVFLSUNBZ0lHRnpjMlZ5ZENBdkx5QmpZVzRnYjI1c2VTQmpZV3hzSUhkb1pXNGdibTkwSUdOeVpXRjBhVzVuQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pJNUNpQWdJQ0F2THlCbGVIQnZjblFnWTJ4aGMzTWdRWEpqTVRZME5DQmxlSFJsYm1SeklFRnlZekUxT1RRZ2V3b2dJQ0FnZEhodVlTQkJjSEJzYVdOaGRHbHZia0Z5WjNNZ01Rb2dJQ0FnZEhodVlTQkJjSEJzYVdOaGRHbHZia0Z5WjNNZ01nb2dJQ0FnZEhodVlTQkJjSEJzYVdOaGRHbHZia0Z5WjNNZ013b2dJQ0FnZEhodVlTQkJjSEJzYVdOaGRHbHZia0Z5WjNNZ05Bb2dJQ0FnZEhodVlTQkJjSEJzYVdOaGRHbHZia0Z5WjNNZ05Rb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3hOelFLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDZ3BDaUFnSUNCallXeHNjM1ZpSUdGeVl6RTBNVEJmWTJGdVgzUnlZVzV6Wm1WeVgySjVYM0JoY25ScGRHbHZiZ29nSUNBZ1lubDBaV05mTUNBdkx5QXdlREUxTVdZM1l6YzFDaUFnSUNCemQyRndDaUFnSUNCamIyNWpZWFFLSUNBZ0lHeHZad29nSUNBZ2FXNTBZMTh4SUM4dklERUtJQ0FnSUhKbGRIVnliZ29LYldGcGJsOWhjbU14TkRFd1gyOXdaWEpoZEc5eVgzUnlZVzV6Wm1WeVgySjVYM0JoY25ScGRHbHZibDl5YjNWMFpVQXlOam9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TVRRMENpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvS1FvZ0lDQWdkSGh1SUU5dVEyOXRjR3hsZEdsdmJnb2dJQ0FnSVFvZ0lDQWdZWE56WlhKMElDOHZJRTl1UTI5dGNHeGxkR2x2YmlCcGN5QnViM1FnVG05UGNBb2dJQ0FnZEhodUlFRndjR3hwWTJGMGFXOXVTVVFLSUNBZ0lHRnpjMlZ5ZENBdkx5QmpZVzRnYjI1c2VTQmpZV3hzSUhkb1pXNGdibTkwSUdOeVpXRjBhVzVuQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pJNUNpQWdJQ0F2THlCbGVIQnZjblFnWTJ4aGMzTWdRWEpqTVRZME5DQmxlSFJsYm1SeklFRnlZekUxT1RRZ2V3b2dJQ0FnZEhodVlTQkJjSEJzYVdOaGRHbHZia0Z5WjNNZ01Rb2dJQ0FnZEhodVlTQkJjSEJzYVdOaGRHbHZia0Z5WjNNZ01nb2dJQ0FnZEhodVlTQkJjSEJzYVdOaGRHbHZia0Z5WjNNZ013b2dJQ0FnZEhodVlTQkJjSEJzYVdOaGRHbHZia0Z5WjNNZ05Bb2dJQ0FnZEhodVlTQkJjSEJzYVdOaGRHbHZia0Z5WjNNZ05Rb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3hORFFLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDZ3BDaUFnSUNCallXeHNjM1ZpSUdGeVl6RTBNVEJmYjNCbGNtRjBiM0pmZEhKaGJuTm1aWEpmWW5sZmNHRnlkR2wwYVc5dUNpQWdJQ0JpZVhSbFkxOHdJQzh2SURCNE1UVXhaamRqTnpVS0lDQWdJSE4zWVhBS0lDQWdJR052Ym1OaGRBb2dJQ0FnYkc5bkNpQWdJQ0JwYm5Salh6RWdMeThnTVFvZ0lDQWdjbVYwZFhKdUNncHRZV2x1WDJGeVl6RTBNVEJmY21WMmIydGxYMjl3WlhKaGRHOXlYM0p2ZFhSbFFESTFPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem94TXpVS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2dwQ2lBZ0lDQjBlRzRnVDI1RGIyMXdiR1YwYVc5dUNpQWdJQ0FoQ2lBZ0lDQmhjM05sY25RZ0x5OGdUMjVEYjIxd2JHVjBhVzl1SUdseklHNXZkQ0JPYjA5d0NpQWdJQ0IwZUc0Z1FYQndiR2xqWVhScGIyNUpSQW9nSUNBZ1lYTnpaWEowSUM4dklHTmhiaUJ2Ym14NUlHTmhiR3dnZDJobGJpQnViM1FnWTNKbFlYUnBibWNLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TWprS0lDQWdJQzh2SUdWNGNHOXlkQ0JqYkdGemN5QkJjbU14TmpRMElHVjRkR1Z1WkhNZ1FYSmpNVFU1TkNCN0NpQWdJQ0IwZUc1aElFRndjR3hwWTJGMGFXOXVRWEpuY3lBeENpQWdJQ0IwZUc1aElFRndjR3hwWTJGMGFXOXVRWEpuY3lBeUNpQWdJQ0IwZUc1aElFRndjR3hwWTJGMGFXOXVRWEpuY3lBekNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qRXpOUW9nSUNBZ0x5OGdRR0Z5WXpRdVlXSnBiV1YwYUc5a0tDa0tJQ0FnSUdOaGJHeHpkV0lnWVhKak1UUXhNRjl5WlhadmEyVmZiM0JsY21GMGIzSUtJQ0FnSUdsdWRHTmZNU0F2THlBeENpQWdJQ0J5WlhSMWNtNEtDbTFoYVc1ZllYSmpNVFF4TUY5aGRYUm9iM0pwZW1WZmIzQmxjbUYwYjNKZmNtOTFkR1ZBTWpRNkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qRXlPQW9nSUNBZ0x5OGdRR0Z5WXpRdVlXSnBiV1YwYUc5a0tDa0tJQ0FnSUhSNGJpQlBia052YlhCc1pYUnBiMjRLSUNBZ0lDRUtJQ0FnSUdGemMyVnlkQ0F2THlCUGJrTnZiWEJzWlhScGIyNGdhWE1nYm05MElFNXZUM0FLSUNBZ0lIUjRiaUJCY0hCc2FXTmhkR2x2YmtsRUNpQWdJQ0JoYzNObGNuUWdMeThnWTJGdUlHOXViSGtnWTJGc2JDQjNhR1Z1SUc1dmRDQmpjbVZoZEdsdVp3b2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6b3lPUW9nSUNBZ0x5OGdaWGh3YjNKMElHTnNZWE56SUVGeVl6RTJORFFnWlhoMFpXNWtjeUJCY21NeE5UazBJSHNLSUNBZ0lIUjRibUVnUVhCd2JHbGpZWFJwYjI1QmNtZHpJREVLSUNBZ0lIUjRibUVnUVhCd2JHbGpZWFJwYjI1QmNtZHpJRElLSUNBZ0lIUjRibUVnUVhCd2JHbGpZWFJwYjI1QmNtZHpJRE1LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TVRJNENpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvS1FvZ0lDQWdZMkZzYkhOMVlpQmhjbU14TkRFd1gyRjFkR2h2Y21sNlpWOXZjR1Z5WVhSdmNnb2dJQ0FnYVc1MFkxOHhJQzh2SURFS0lDQWdJSEpsZEhWeWJnb0tiV0ZwYmw5aGNtTXhOREV3WDJselgyOXdaWEpoZEc5eVgzSnZkWFJsUURJek9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3hNVFFLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDaDdJSEpsWVdSdmJteDVPaUIwY25WbElIMHBDaUFnSUNCMGVHNGdUMjVEYjIxd2JHVjBhVzl1Q2lBZ0lDQWhDaUFnSUNCaGMzTmxjblFnTHk4Z1QyNURiMjF3YkdWMGFXOXVJR2x6SUc1dmRDQk9iMDl3Q2lBZ0lDQjBlRzRnUVhCd2JHbGpZWFJwYjI1SlJBb2dJQ0FnWVhOelpYSjBJQzh2SUdOaGJpQnZibXg1SUdOaGJHd2dkMmhsYmlCdWIzUWdZM0psWVhScGJtY0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZNamtLSUNBZ0lDOHZJR1Y0Y0c5eWRDQmpiR0Z6Y3lCQmNtTXhOalEwSUdWNGRHVnVaSE1nUVhKak1UVTVOQ0I3Q2lBZ0lDQjBlRzVoSUVGd2NHeHBZMkYwYVc5dVFYSm5jeUF4Q2lBZ0lDQjBlRzVoSUVGd2NHeHBZMkYwYVc5dVFYSm5jeUF5Q2lBZ0lDQjBlRzVoSUVGd2NHeHBZMkYwYVc5dVFYSm5jeUF6Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pFeE5Bb2dJQ0FnTHk4Z1FHRnlZelF1WVdKcGJXVjBhRzlrS0hzZ2NtVmhaRzl1YkhrNklIUnlkV1VnZlNrS0lDQWdJR05oYkd4emRXSWdZWEpqTVRReE1GOXBjMTl2Y0dWeVlYUnZjZ29nSUNBZ1lubDBaV05mTUNBdkx5QXdlREUxTVdZM1l6YzFDaUFnSUNCemQyRndDaUFnSUNCamIyNWpZWFFLSUNBZ0lHeHZad29nSUNBZ2FXNTBZMTh4SUM4dklERUtJQ0FnSUhKbGRIVnliZ29LYldGcGJsOWhjbU14TkRFd1gzQmhjblJwZEdsdmJuTmZiMlpmY205MWRHVkFNakk2Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pFd053b2dJQ0FnTHk4Z1FHRnlZelF1WVdKcGJXVjBhRzlrS0NrS0lDQWdJSFI0YmlCUGJrTnZiWEJzWlhScGIyNEtJQ0FnSUNFS0lDQWdJR0Z6YzJWeWRDQXZMeUJQYmtOdmJYQnNaWFJwYjI0Z2FYTWdibTkwSUU1dlQzQUtJQ0FnSUhSNGJpQkJjSEJzYVdOaGRHbHZia2xFQ2lBZ0lDQmhjM05sY25RZ0x5OGdZMkZ1SUc5dWJIa2dZMkZzYkNCM2FHVnVJRzV2ZENCamNtVmhkR2x1WndvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pveU9Rb2dJQ0FnTHk4Z1pYaHdiM0owSUdOc1lYTnpJRUZ5WXpFMk5EUWdaWGgwWlc1a2N5QkJjbU14TlRrMElIc0tJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklERUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklESUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNVEEzQ2lBZ0lDQXZMeUJBWVhKak5DNWhZbWx0WlhSb2IyUW9LUW9nSUNBZ1kyRnNiSE4xWWlCaGNtTXhOREV3WDNCaGNuUnBkR2x2Ym5OZmIyWUtJQ0FnSUdKNWRHVmpYekFnTHk4Z01IZ3hOVEZtTjJNM05Rb2dJQ0FnYzNkaGNBb2dJQ0FnWTI5dVkyRjBDaUFnSUNCc2IyY0tJQ0FnSUdsdWRHTmZNU0F2THlBeENpQWdJQ0J5WlhSMWNtNEtDbTFoYVc1ZllYSmpNVFF4TUY5MGNtRnVjMlpsY2w5aWVWOXdZWEowYVhScGIyNWZjbTkxZEdWQU1qRTZDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPamt6Q2lBZ0lDQXZMeUJBWVhKak5DNWhZbWx0WlhSb2IyUW9LUW9nSUNBZ2RIaHVJRTl1UTI5dGNHeGxkR2x2YmdvZ0lDQWdJUW9nSUNBZ1lYTnpaWEowSUM4dklFOXVRMjl0Y0d4bGRHbHZiaUJwY3lCdWIzUWdUbTlQY0FvZ0lDQWdkSGh1SUVGd2NHeHBZMkYwYVc5dVNVUUtJQ0FnSUdGemMyVnlkQ0F2THlCallXNGdiMjVzZVNCallXeHNJSGRvWlc0Z2JtOTBJR055WldGMGFXNW5DaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPakk1Q2lBZ0lDQXZMeUJsZUhCdmNuUWdZMnhoYzNNZ1FYSmpNVFkwTkNCbGVIUmxibVJ6SUVGeVl6RTFPVFFnZXdvZ0lDQWdkSGh1WVNCQmNIQnNhV05oZEdsdmJrRnlaM01nTVFvZ0lDQWdkSGh1WVNCQmNIQnNhV05oZEdsdmJrRnlaM01nTWdvZ0lDQWdkSGh1WVNCQmNIQnNhV05oZEdsdmJrRnlaM01nTXdvZ0lDQWdkSGh1WVNCQmNIQnNhV05oZEdsdmJrRnlaM01nTkFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvNU13b2dJQ0FnTHk4Z1FHRnlZelF1WVdKcGJXVjBhRzlrS0NrS0lDQWdJR05oYkd4emRXSWdZWEpqTVRReE1GOTBjbUZ1YzJabGNsOWllVjl3WVhKMGFYUnBiMjRLSUNBZ0lHSjVkR1ZqWHpBZ0x5OGdNSGd4TlRGbU4yTTNOUW9nSUNBZ2MzZGhjQW9nSUNBZ1kyOXVZMkYwQ2lBZ0lDQnNiMmNLSUNBZ0lHbHVkR05mTVNBdkx5QXhDaUFnSUNCeVpYUjFjbTRLQ20xaGFXNWZZWEpqTWpBd1gzUnlZVzV6Wm1WeVgzSnZkWFJsUURJd09nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6bzNPQW9nSUNBZ0x5OGdRR0Z5WXpRdVlXSnBiV1YwYUc5a0tDa0tJQ0FnSUhSNGJpQlBia052YlhCc1pYUnBiMjRLSUNBZ0lDRUtJQ0FnSUdGemMyVnlkQ0F2THlCUGJrTnZiWEJzWlhScGIyNGdhWE1nYm05MElFNXZUM0FLSUNBZ0lIUjRiaUJCY0hCc2FXTmhkR2x2YmtsRUNpQWdJQ0JoYzNObGNuUWdMeThnWTJGdUlHOXViSGtnWTJGc2JDQjNhR1Z1SUc1dmRDQmpjbVZoZEdsdVp3b2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6b3lPUW9nSUNBZ0x5OGdaWGh3YjNKMElHTnNZWE56SUVGeVl6RTJORFFnWlhoMFpXNWtjeUJCY21NeE5UazBJSHNLSUNBZ0lIUjRibUVnUVhCd2JHbGpZWFJwYjI1QmNtZHpJREVLSUNBZ0lIUjRibUVnUVhCd2JHbGpZWFJwYjI1QmNtZHpJRElLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TnpnS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2dwQ2lBZ0lDQmpZV3hzYzNWaUlHRnlZekl3TUY5MGNtRnVjMlpsY2dvZ0lDQWdZbmwwWldOZk1DQXZMeUF3ZURFMU1XWTNZemMxQ2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR3h2WndvZ0lDQWdhVzUwWTE4eElDOHZJREVLSUNBZ0lISmxkSFZ5YmdvS2JXRnBibDloY21NeE5ERXdYMkpoYkdGdVkyVmZiMlpmY0dGeWRHbDBhVzl1WDNKdmRYUmxRREU1T2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvMk9Rb2dJQ0FnTHk4Z1FHRnlZelF1WVdKcGJXVjBhRzlrS0hzZ2NtVmhaRzl1YkhrNklIUnlkV1VnZlNrS0lDQWdJSFI0YmlCUGJrTnZiWEJzWlhScGIyNEtJQ0FnSUNFS0lDQWdJR0Z6YzJWeWRDQXZMeUJQYmtOdmJYQnNaWFJwYjI0Z2FYTWdibTkwSUU1dlQzQUtJQ0FnSUhSNGJpQkJjSEJzYVdOaGRHbHZia2xFQ2lBZ0lDQmhjM05sY25RZ0x5OGdZMkZ1SUc5dWJIa2dZMkZzYkNCM2FHVnVJRzV2ZENCamNtVmhkR2x1WndvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pveU9Rb2dJQ0FnTHk4Z1pYaHdiM0owSUdOc1lYTnpJRUZ5WXpFMk5EUWdaWGgwWlc1a2N5QkJjbU14TlRrMElIc0tJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklERUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklESUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZOamtLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDaDdJSEpsWVdSdmJteDVPaUIwY25WbElIMHBDaUFnSUNCallXeHNjM1ZpSUdGeVl6RTBNVEJmWW1Gc1lXNWpaVjl2Wmw5d1lYSjBhWFJwYjI0S0lDQWdJR0o1ZEdWalh6QWdMeThnTUhneE5URm1OMk0zTlFvZ0lDQWdjM2RoY0FvZ0lDQWdZMjl1WTJGMENpQWdJQ0JzYjJjS0lDQWdJR2x1ZEdOZk1TQXZMeUF4Q2lBZ0lDQnlaWFIxY200S0NtMWhhVzVmWVhKak1UVTVORjlwYzE5cGMzTjFZV0pzWlY5eWIzVjBaVUF4T0RvS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TlRrMExtRnNaMjh1ZEhNNk9EY0tJQ0FnSUM4dklFQmhjbU0wTG1GaWFXMWxkR2h2WkNoN0lISmxZV1J2Ym14NU9pQjBjblZsSUgwcENpQWdJQ0IwZUc0Z1QyNURiMjF3YkdWMGFXOXVDaUFnSUNBaENpQWdJQ0JoYzNObGNuUWdMeThnVDI1RGIyMXdiR1YwYVc5dUlHbHpJRzV2ZENCT2IwOXdDaUFnSUNCMGVHNGdRWEJ3YkdsallYUnBiMjVKUkFvZ0lDQWdZWE56WlhKMElDOHZJR05oYmlCdmJteDVJR05oYkd3Z2QyaGxiaUJ1YjNRZ1kzSmxZWFJwYm1jS0lDQWdJR05oYkd4emRXSWdZWEpqTVRVNU5GOXBjMTlwYzNOMVlXSnNaUW9nSUNBZ1lubDBaV05mTUNBdkx5QXdlREUxTVdZM1l6YzFDaUFnSUNCemQyRndDaUFnSUNCamIyNWpZWFFLSUNBZ0lHeHZad29nSUNBZ2FXNTBZMTh4SUM4dklERUtJQ0FnSUhKbGRIVnliZ29LYldGcGJsOWhjbU14TlRrMFgzUnlZVzV6Wm1WeVgyWnliMjFmZDJsMGFGOWtZWFJoWDNKdmRYUmxRREUzT2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUxT1RRdVlXeG5ieTUwY3pvM05Rb2dJQ0FnTHk4Z1FHRnlZelF1WVdKcGJXVjBhRzlrS0NrS0lDQWdJSFI0YmlCUGJrTnZiWEJzWlhScGIyNEtJQ0FnSUNFS0lDQWdJR0Z6YzJWeWRDQXZMeUJQYmtOdmJYQnNaWFJwYjI0Z2FYTWdibTkwSUU1dlQzQUtJQ0FnSUhSNGJpQkJjSEJzYVdOaGRHbHZia2xFQ2lBZ0lDQmhjM05sY25RZ0x5OGdZMkZ1SUc5dWJIa2dZMkZzYkNCM2FHVnVJRzV2ZENCamNtVmhkR2x1WndvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pveU9Rb2dJQ0FnTHk4Z1pYaHdiM0owSUdOc1lYTnpJRUZ5WXpFMk5EUWdaWGgwWlc1a2N5QkJjbU14TlRrMElIc0tJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklERUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklESUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklETUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklEUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5UazBMbUZzWjI4dWRITTZOelVLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDZ3BDaUFnSUNCallXeHNjM1ZpSUdGeVl6RTFPVFJmZEhKaGJuTm1aWEpmWm5KdmJWOTNhWFJvWDJSaGRHRUtJQ0FnSUdKNWRHVmpYekFnTHk4Z01IZ3hOVEZtTjJNM05Rb2dJQ0FnYzNkaGNBb2dJQ0FnWTI5dVkyRjBDaUFnSUNCc2IyY0tJQ0FnSUdsdWRHTmZNU0F2THlBeENpQWdJQ0J5WlhSMWNtNEtDbTFoYVc1ZllYSmpNVFU1TkY5MGNtRnVjMlpsY2w5M2FYUm9YMlJoZEdGZmNtOTFkR1ZBTVRZNkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRVNU5DNWhiR2R2TG5Sek9qWTNDaUFnSUNBdkx5QkFZWEpqTkM1aFltbHRaWFJvYjJRb0tRb2dJQ0FnZEhodUlFOXVRMjl0Y0d4bGRHbHZiZ29nSUNBZ0lRb2dJQ0FnWVhOelpYSjBJQzh2SUU5dVEyOXRjR3hsZEdsdmJpQnBjeUJ1YjNRZ1RtOVBjQW9nSUNBZ2RIaHVJRUZ3Y0d4cFkyRjBhVzl1U1VRS0lDQWdJR0Z6YzJWeWRDQXZMeUJqWVc0Z2IyNXNlU0JqWVd4c0lIZG9aVzRnYm05MElHTnlaV0YwYVc1bkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qSTVDaUFnSUNBdkx5QmxlSEJ2Y25RZ1kyeGhjM01nUVhKak1UWTBOQ0JsZUhSbGJtUnpJRUZ5WXpFMU9UUWdld29nSUNBZ2RIaHVZU0JCY0hCc2FXTmhkR2x2YmtGeVozTWdNUW9nSUNBZ2RIaHVZU0JCY0hCc2FXTmhkR2x2YmtGeVozTWdNZ29nSUNBZ2RIaHVZU0JCY0hCc2FXTmhkR2x2YmtGeVozTWdNd29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTFPVFF1WVd4bmJ5NTBjem8yTndvZ0lDQWdMeThnUUdGeVl6UXVZV0pwYldWMGFHOWtLQ2tLSUNBZ0lHTmhiR3h6ZFdJZ1lYSmpNVFU1TkY5MGNtRnVjMlpsY2w5M2FYUm9YMlJoZEdFS0lDQWdJR0o1ZEdWalh6QWdMeThnTUhneE5URm1OMk0zTlFvZ0lDQWdjM2RoY0FvZ0lDQWdZMjl1WTJGMENpQWdJQ0JzYjJjS0lDQWdJR2x1ZEdOZk1TQXZMeUF4Q2lBZ0lDQnlaWFIxY200S0NtMWhhVzVmWVhKak1UVTVORjl5WldSbFpXMWZjbTkxZEdWQU1UVTZDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UVTVOQzVoYkdkdkxuUnpPalUyQ2lBZ0lDQXZMeUJBWVhKak5DNWhZbWx0WlhSb2IyUW9LUW9nSUNBZ2RIaHVJRTl1UTI5dGNHeGxkR2x2YmdvZ0lDQWdJUW9nSUNBZ1lYTnpaWEowSUM4dklFOXVRMjl0Y0d4bGRHbHZiaUJwY3lCdWIzUWdUbTlQY0FvZ0lDQWdkSGh1SUVGd2NHeHBZMkYwYVc5dVNVUUtJQ0FnSUdGemMyVnlkQ0F2THlCallXNGdiMjVzZVNCallXeHNJSGRvWlc0Z2JtOTBJR055WldGMGFXNW5DaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPakk1Q2lBZ0lDQXZMeUJsZUhCdmNuUWdZMnhoYzNNZ1FYSmpNVFkwTkNCbGVIUmxibVJ6SUVGeVl6RTFPVFFnZXdvZ0lDQWdkSGh1WVNCQmNIQnNhV05oZEdsdmJrRnlaM01nTVFvZ0lDQWdkSGh1WVNCQmNIQnNhV05oZEdsdmJrRnlaM01nTWdvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUxT1RRdVlXeG5ieTUwY3pvMU5nb2dJQ0FnTHk4Z1FHRnlZelF1WVdKcGJXVjBhRzlrS0NrS0lDQWdJR05oYkd4emRXSWdZWEpqTVRVNU5GOXlaV1JsWlcwS0lDQWdJR2x1ZEdOZk1TQXZMeUF4Q2lBZ0lDQnlaWFIxY200S0NtMWhhVzVmWVhKak1UVTVORjl5WldSbFpXMUdjbTl0WDNKdmRYUmxRREUwT2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUxT1RRdVlXeG5ieTUwY3pvME5Rb2dJQ0FnTHk4Z1FHRnlZelF1WVdKcGJXVjBhRzlrS0NrS0lDQWdJSFI0YmlCUGJrTnZiWEJzWlhScGIyNEtJQ0FnSUNFS0lDQWdJR0Z6YzJWeWRDQXZMeUJQYmtOdmJYQnNaWFJwYjI0Z2FYTWdibTkwSUU1dlQzQUtJQ0FnSUhSNGJpQkJjSEJzYVdOaGRHbHZia2xFQ2lBZ0lDQmhjM05sY25RZ0x5OGdZMkZ1SUc5dWJIa2dZMkZzYkNCM2FHVnVJRzV2ZENCamNtVmhkR2x1WndvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pveU9Rb2dJQ0FnTHk4Z1pYaHdiM0owSUdOc1lYTnpJRUZ5WXpFMk5EUWdaWGgwWlc1a2N5QkJjbU14TlRrMElIc0tJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklERUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklESUtJQ0FnSUhSNGJtRWdRWEJ3YkdsallYUnBiMjVCY21keklETUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5UazBMbUZzWjI4dWRITTZORFVLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDZ3BDaUFnSUNCallXeHNjM1ZpSUdGeVl6RTFPVFJmY21Wa1pXVnRSbkp2YlFvZ0lDQWdhVzUwWTE4eElDOHZJREVLSUNBZ0lISmxkSFZ5YmdvS2JXRnBibDloY21NeE5UazBYMmx6YzNWbFgzSnZkWFJsUURFek9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMU9UUXVZV3huYnk1MGN6b3pOUW9nSUNBZ0x5OGdRR0Z5WXpRdVlXSnBiV1YwYUc5a0tDa0tJQ0FnSUhSNGJpQlBia052YlhCc1pYUnBiMjRLSUNBZ0lDRUtJQ0FnSUdGemMyVnlkQ0F2THlCUGJrTnZiWEJzWlhScGIyNGdhWE1nYm05MElFNXZUM0FLSUNBZ0lIUjRiaUJCY0hCc2FXTmhkR2x2YmtsRUNpQWdJQ0JoYzNObGNuUWdMeThnWTJGdUlHOXViSGtnWTJGc2JDQjNhR1Z1SUc1dmRDQmpjbVZoZEdsdVp3b2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6b3lPUW9nSUNBZ0x5OGdaWGh3YjNKMElHTnNZWE56SUVGeVl6RTJORFFnWlhoMFpXNWtjeUJCY21NeE5UazBJSHNLSUNBZ0lIUjRibUVnUVhCd2JHbGpZWFJwYjI1QmNtZHpJREVLSUNBZ0lIUjRibUVnUVhCd2JHbGpZWFJwYjI1QmNtZHpJRElLSUNBZ0lIUjRibUVnUVhCd2JHbGpZWFJwYjI1QmNtZHpJRE1LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOVGswTG1Gc1oyOHVkSE02TXpVS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2dwQ2lBZ0lDQmpZV3hzYzNWaUlHRnlZekUxT1RSZmFYTnpkV1VLSUNBZ0lHbHVkR05mTVNBdkx5QXhDaUFnSUNCeVpYUjFjbTRLQ20xaGFXNWZZWEpqTVRVNU5GOXpaWFJmYVhOemRXRmliR1ZmY205MWRHVkFNVEk2Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFU1TkM1aGJHZHZMblJ6T2pJNENpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvS1FvZ0lDQWdkSGh1SUU5dVEyOXRjR3hsZEdsdmJnb2dJQ0FnSVFvZ0lDQWdZWE56WlhKMElDOHZJRTl1UTI5dGNHeGxkR2x2YmlCcGN5QnViM1FnVG05UGNBb2dJQ0FnZEhodUlFRndjR3hwWTJGMGFXOXVTVVFLSUNBZ0lHRnpjMlZ5ZENBdkx5QmpZVzRnYjI1c2VTQmpZV3hzSUhkb1pXNGdibTkwSUdOeVpXRjBhVzVuQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pJNUNpQWdJQ0F2THlCbGVIQnZjblFnWTJ4aGMzTWdRWEpqTVRZME5DQmxlSFJsYm1SeklFRnlZekUxT1RRZ2V3b2dJQ0FnZEhodVlTQkJjSEJzYVdOaGRHbHZia0Z5WjNNZ01Rb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMU9UUXVZV3huYnk1MGN6b3lPQW9nSUNBZ0x5OGdRR0Z5WXpRdVlXSnBiV1YwYUc5a0tDa0tJQ0FnSUdOaGJHeHpkV0lnWVhKak1UVTVORjl6WlhSZmFYTnpkV0ZpYkdVS0lDQWdJR2x1ZEdOZk1TQXZMeUF4Q2lBZ0lDQnlaWFIxY200S0NtMWhhVzVmWVhKak1UWTBORjlqYjI1MGNtOXNiR1Z5WDNKbFpHVmxiVjl5YjNWMFpVQXhNVG9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TVRVeUNpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvS1FvZ0lDQWdkSGh1SUU5dVEyOXRjR3hsZEdsdmJnb2dJQ0FnSVFvZ0lDQWdZWE56WlhKMElDOHZJRTl1UTI5dGNHeGxkR2x2YmlCcGN5QnViM1FnVG05UGNBb2dJQ0FnZEhodUlFRndjR3hwWTJGMGFXOXVTVVFLSUNBZ0lHRnpjMlZ5ZENBdkx5QmpZVzRnYjI1c2VTQmpZV3hzSUhkb1pXNGdibTkwSUdOeVpXRjBhVzVuQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pJNUNpQWdJQ0F2THlCbGVIQnZjblFnWTJ4aGMzTWdRWEpqTVRZME5DQmxlSFJsYm1SeklFRnlZekUxT1RRZ2V3b2dJQ0FnZEhodVlTQkJjSEJzYVdOaGRHbHZia0Z5WjNNZ01Rb2dJQ0FnZEhodVlTQkJjSEJzYVdOaGRHbHZia0Z5WjNNZ01nb2dJQ0FnZEhodVlTQkJjSEJzYVdOaGRHbHZia0Z5WjNNZ013b2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6b3hOVElLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDZ3BDaUFnSUNCallXeHNjM1ZpSUdGeVl6RTJORFJmWTI5dWRISnZiR3hsY2w5eVpXUmxaVzBLSUNBZ0lHSjVkR1ZqWHpBZ0x5OGdNSGd4TlRGbU4yTTNOUW9nSUNBZ2MzZGhjQW9nSUNBZ1kyOXVZMkYwQ2lBZ0lDQnNiMmNLSUNBZ0lHbHVkR05mTVNBdkx5QXhDaUFnSUNCeVpYUjFjbTRLQ20xaGFXNWZZWEpqTVRZME5GOWpiMjUwY205c2JHVnlYM1J5WVc1elptVnlYM0p2ZFhSbFFERXdPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem94TVRjS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2dwQ2lBZ0lDQjBlRzRnVDI1RGIyMXdiR1YwYVc5dUNpQWdJQ0FoQ2lBZ0lDQmhjM05sY25RZ0x5OGdUMjVEYjIxd2JHVjBhVzl1SUdseklHNXZkQ0JPYjA5d0NpQWdJQ0IwZUc0Z1FYQndiR2xqWVhScGIyNUpSQW9nSUNBZ1lYTnpaWEowSUM4dklHTmhiaUJ2Ym14NUlHTmhiR3dnZDJobGJpQnViM1FnWTNKbFlYUnBibWNLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TWprS0lDQWdJQzh2SUdWNGNHOXlkQ0JqYkdGemN5QkJjbU14TmpRMElHVjRkR1Z1WkhNZ1FYSmpNVFU1TkNCN0NpQWdJQ0IwZUc1aElFRndjR3hwWTJGMGFXOXVRWEpuY3lBeENpQWdJQ0IwZUc1aElFRndjR3hwWTJGMGFXOXVRWEpuY3lBeUNpQWdJQ0IwZUc1aElFRndjR3hwWTJGMGFXOXVRWEpuY3lBekNpQWdJQ0IwZUc1aElFRndjR3hwWTJGMGFXOXVRWEpuY3lBMENpQWdJQ0IwZUc1aElFRndjR3hwWTJGMGFXOXVRWEpuY3lBMUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qRXhOd29nSUNBZ0x5OGdRR0Z5WXpRdVlXSnBiV1YwYUc5a0tDa0tJQ0FnSUdOaGJHeHpkV0lnWVhKak1UWTBORjlqYjI1MGNtOXNiR1Z5WDNSeVlXNXpabVZ5Q2lBZ0lDQmllWFJsWTE4d0lDOHZJREI0TVRVeFpqZGpOelVLSUNBZ0lITjNZWEFLSUNBZ0lHTnZibU5oZEFvZ0lDQWdiRzluQ2lBZ0lDQnBiblJqWHpFZ0x5OGdNUW9nSUNBZ2NtVjBkWEp1Q2dwdFlXbHVYMkZ5WXpFMk5EUmZhWE5mWTI5dWRISnZiR3hoWW14bFgzSnZkWFJsUURrNkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qRXdPUW9nSUNBZ0x5OGdRR0Z5WXpRdVlXSnBiV1YwYUc5a0tIc2djbVZoWkc5dWJIazZJSFJ5ZFdVZ2ZTa0tJQ0FnSUhSNGJpQlBia052YlhCc1pYUnBiMjRLSUNBZ0lDRUtJQ0FnSUdGemMyVnlkQ0F2THlCUGJrTnZiWEJzWlhScGIyNGdhWE1nYm05MElFNXZUM0FLSUNBZ0lIUjRiaUJCY0hCc2FXTmhkR2x2YmtsRUNpQWdJQ0JoYzNObGNuUWdMeThnWTJGdUlHOXViSGtnWTJGc2JDQjNhR1Z1SUc1dmRDQmpjbVZoZEdsdVp3b2dJQ0FnWTJGc2JITjFZaUJoY21NeE5qUTBYMmx6WDJOdmJuUnliMnhzWVdKc1pRb2dJQ0FnWW5sMFpXTmZNQ0F2THlBd2VERTFNV1kzWXpjMUNpQWdJQ0J6ZDJGd0NpQWdJQ0JqYjI1allYUUtJQ0FnSUd4dlp3b2dJQ0FnYVc1MFkxOHhJQzh2SURFS0lDQWdJSEpsZEhWeWJnb0tiV0ZwYmw5aGNtTXhOalEwWDNObGRGOXRhVzVmWVdOMGFXOXVYMmx1ZEdWeWRtRnNYM0p2ZFhSbFFEZzZDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPakV3TWdvZ0lDQWdMeThnUUdGeVl6UXVZV0pwYldWMGFHOWtLQ2tLSUNBZ0lIUjRiaUJQYmtOdmJYQnNaWFJwYjI0S0lDQWdJQ0VLSUNBZ0lHRnpjMlZ5ZENBdkx5QlBia052YlhCc1pYUnBiMjRnYVhNZ2JtOTBJRTV2VDNBS0lDQWdJSFI0YmlCQmNIQnNhV05oZEdsdmJrbEVDaUFnSUNCaGMzTmxjblFnTHk4Z1kyRnVJRzl1YkhrZ1kyRnNiQ0IzYUdWdUlHNXZkQ0JqY21WaGRHbHVad29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem95T1FvZ0lDQWdMeThnWlhod2IzSjBJR05zWVhOeklFRnlZekUyTkRRZ1pYaDBaVzVrY3lCQmNtTXhOVGswSUhzS0lDQWdJSFI0Ym1FZ1FYQndiR2xqWVhScGIyNUJjbWR6SURFS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk1UQXlDaUFnSUNBdkx5QkFZWEpqTkM1aFltbHRaWFJvYjJRb0tRb2dJQ0FnWTJGc2JITjFZaUJoY21NeE5qUTBYM05sZEY5dGFXNWZZV04wYVc5dVgybHVkR1Z5ZG1Gc0NpQWdJQ0JwYm5Salh6RWdMeThnTVFvZ0lDQWdjbVYwZFhKdUNncHRZV2x1WDJGeVl6RTJORFJmYzJWMFgzSmxjWFZwY21WZmFuVnpkR2xtYVdOaGRHbHZibDl5YjNWMFpVQTNPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem81TmdvZ0lDQWdMeThnUUdGeVl6UXVZV0pwYldWMGFHOWtLQ2tLSUNBZ0lIUjRiaUJQYmtOdmJYQnNaWFJwYjI0S0lDQWdJQ0VLSUNBZ0lHRnpjMlZ5ZENBdkx5QlBia052YlhCc1pYUnBiMjRnYVhNZ2JtOTBJRTV2VDNBS0lDQWdJSFI0YmlCQmNIQnNhV05oZEdsdmJrbEVDaUFnSUNCaGMzTmxjblFnTHk4Z1kyRnVJRzl1YkhrZ1kyRnNiQ0IzYUdWdUlHNXZkQ0JqY21WaGRHbHVad29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem95T1FvZ0lDQWdMeThnWlhod2IzSjBJR05zWVhOeklFRnlZekUyTkRRZ1pYaDBaVzVrY3lCQmNtTXhOVGswSUhzS0lDQWdJSFI0Ym1FZ1FYQndiR2xqWVhScGIyNUJjbWR6SURFS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk9UWUtJQ0FnSUM4dklFQmhjbU0wTG1GaWFXMWxkR2h2WkNncENpQWdJQ0JqWVd4c2MzVmlJR0Z5WXpFMk5EUmZjMlYwWDNKbGNYVnBjbVZmYW5WemRHbG1hV05oZEdsdmJnb2dJQ0FnYVc1MFkxOHhJQzh2SURFS0lDQWdJSEpsZEhWeWJnb0tiV0ZwYmw5aGNtTXhOalEwWDNObGRGOWpiMjUwY205c2JHRmliR1ZmY205MWRHVkFOam9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02T0RJS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2dwQ2lBZ0lDQjBlRzRnVDI1RGIyMXdiR1YwYVc5dUNpQWdJQ0FoQ2lBZ0lDQmhjM05sY25RZ0x5OGdUMjVEYjIxd2JHVjBhVzl1SUdseklHNXZkQ0JPYjA5d0NpQWdJQ0IwZUc0Z1FYQndiR2xqWVhScGIyNUpSQW9nSUNBZ1lYTnpaWEowSUM4dklHTmhiaUJ2Ym14NUlHTmhiR3dnZDJobGJpQnViM1FnWTNKbFlYUnBibWNLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TWprS0lDQWdJQzh2SUdWNGNHOXlkQ0JqYkdGemN5QkJjbU14TmpRMElHVjRkR1Z1WkhNZ1FYSmpNVFU1TkNCN0NpQWdJQ0IwZUc1aElFRndjR3hwWTJGMGFXOXVRWEpuY3lBeENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qZ3lDaUFnSUNBdkx5QkFZWEpqTkM1aFltbHRaWFJvYjJRb0tRb2dJQ0FnWTJGc2JITjFZaUJoY21NeE5qUTBYM05sZEY5amIyNTBjbTlzYkdGaWJHVUtJQ0FnSUdsdWRHTmZNU0F2THlBeENpQWdJQ0J5WlhSMWNtNEtDbTFoYVc1ZllYSmpNVFkwTkY5elpYUmZZMjl1ZEhKdmJHeGxjbDl5YjNWMFpVQTFPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem8zTVFvZ0lDQWdMeThnUUdGeVl6UXVZV0pwYldWMGFHOWtLQ2tLSUNBZ0lIUjRiaUJQYmtOdmJYQnNaWFJwYjI0S0lDQWdJQ0VLSUNBZ0lHRnpjMlZ5ZENBdkx5QlBia052YlhCc1pYUnBiMjRnYVhNZ2JtOTBJRTV2VDNBS0lDQWdJSFI0YmlCQmNIQnNhV05oZEdsdmJrbEVDaUFnSUNCaGMzTmxjblFnTHk4Z1kyRnVJRzl1YkhrZ1kyRnNiQ0IzYUdWdUlHNXZkQ0JqY21WaGRHbHVad29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem95T1FvZ0lDQWdMeThnWlhod2IzSjBJR05zWVhOeklFRnlZekUyTkRRZ1pYaDBaVzVrY3lCQmNtTXhOVGswSUhzS0lDQWdJSFI0Ym1FZ1FYQndiR2xqWVhScGIyNUJjbWR6SURFS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk56RUtJQ0FnSUM4dklFQmhjbU0wTG1GaWFXMWxkR2h2WkNncENpQWdJQ0JqWVd4c2MzVmlJR0Z5WXpFMk5EUmZjMlYwWDJOdmJuUnliMnhzWlhJS0lDQWdJR2x1ZEdOZk1TQXZMeUF4Q2lBZ0lDQnlaWFIxY200S0NtMWhhVzVmWW1GeVpWOXliM1YwYVc1blFEVXlPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem95T1FvZ0lDQWdMeThnWlhod2IzSjBJR05zWVhOeklFRnlZekUyTkRRZ1pYaDBaVzVrY3lCQmNtTXhOVGswSUhzS0lDQWdJSFI0YmlCUGJrTnZiWEJzWlhScGIyNEtJQ0FnSUdKdWVpQnRZV2x1WDJGbWRHVnlYMmxtWDJWc2MyVkFOVFlLSUNBZ0lIUjRiaUJCY0hCc2FXTmhkR2x2YmtsRUNpQWdJQ0FoQ2lBZ0lDQmhjM05sY25RZ0x5OGdZMkZ1SUc5dWJIa2dZMkZzYkNCM2FHVnVJR055WldGMGFXNW5DaUFnSUNCcGJuUmpYekVnTHk4Z01Rb2dJQ0FnY21WMGRYSnVDZ29LTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6bzZRWEpqTVRZME5DNWZiMjVzZVU5M2JtVnlLQ2tnTFQ0Z2RtOXBaRG9LWDI5dWJIbFBkMjVsY2pvS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk5ETUtJQ0FnSUM4dklHRnpjMlZ5ZENoMGFHbHpMbUZ5WXpnNFgybHpYMjkzYm1WeUtHNWxkeUJoY21NMExrRmtaSEpsYzNNb1ZIaHVMbk5sYm1SbGNpa3BMbTVoZEdsMlpTQTlQVDBnZEhKMVpTd2dKMjl1YkhsZmIzZHVaWEluS1FvZ0lDQWdkSGh1SUZObGJtUmxjZ29nSUNBZ1kyRnNiSE4xWWlCaGNtTTRPRjlwYzE5dmQyNWxjZ29nSUNBZ2FXNTBZMTh3SUM4dklEQUtJQ0FnSUdkbGRHSnBkQW9nSUNBZ2FXNTBZMTh4SUM4dklERUtJQ0FnSUQwOUNpQWdJQ0JoYzNObGNuUWdMeThnYjI1c2VWOXZkMjVsY2dvZ0lDQWdjbVYwYzNWaUNnb0tMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pvNlFYSmpNVFkwTkM1ZmIyNXNlVU52Ym5SeWIyeHNaWElvS1NBdFBpQjJiMmxrT2dwZmIyNXNlVU52Ym5SeWIyeHNaWEk2Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pNeENpQWdJQ0F2THlCd2RXSnNhV01nWTI5dWRISnZiR3hsY2lBOUlFZHNiMkpoYkZOMFlYUmxQR0Z5WXpRdVFXUmtjbVZ6Y3o0b2V5QnJaWGs2SUNkamRISnNKeUI5S1FvZ0lDQWdhVzUwWTE4d0lDOHZJREFLSUNBZ0lHSjVkR1ZqSURFd0lDOHZJQ0pqZEhKc0lnb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6bzBOd29nSUNBZ0x5OGdZWE56WlhKMEtIUm9hWE11WTI5dWRISnZiR3hsY2k1b1lYTldZV3gxWlN3Z0oyNXZYMk52Ym5SeWIyeHNaWEluS1FvZ0lDQWdZWEJ3WDJkc2IySmhiRjluWlhSZlpYZ0tJQ0FnSUdKMWNua2dNUW9nSUNBZ1lYTnpaWEowSUM4dklHNXZYMk52Ym5SeWIyeHNaWElLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TkRnS0lDQWdJQzh2SUdGemMyVnlkQ2h1WlhjZ1lYSmpOQzVCWkdSeVpYTnpLRlI0Ymk1elpXNWtaWElwSUQwOVBTQjBhR2x6TG1OdmJuUnliMnhzWlhJdWRtRnNkV1VzSUNkdWIzUmZZMjl1ZEhKdmJHeGxjaWNwQ2lBZ0lDQjBlRzRnVTJWdVpHVnlDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPak14Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdZMjl1ZEhKdmJHeGxjaUE5SUVkc2IySmhiRk4wWVhSbFBHRnlZelF1UVdSa2NtVnpjejRvZXlCclpYazZJQ2RqZEhKc0p5QjlLUW9nSUNBZ2FXNTBZMTh3SUM4dklEQUtJQ0FnSUdKNWRHVmpJREV3SUM4dklDSmpkSEpzSWdvZ0lDQWdZWEJ3WDJkc2IySmhiRjluWlhSZlpYZ0tJQ0FnSUdGemMyVnlkQ0F2THlCamFHVmpheUJIYkc5aVlXeFRkR0YwWlNCbGVHbHpkSE1LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TkRnS0lDQWdJQzh2SUdGemMyVnlkQ2h1WlhjZ1lYSmpOQzVCWkdSeVpYTnpLRlI0Ymk1elpXNWtaWElwSUQwOVBTQjBhR2x6TG1OdmJuUnliMnhzWlhJdWRtRnNkV1VzSUNkdWIzUmZZMjl1ZEhKdmJHeGxjaWNwQ2lBZ0lDQTlQUW9nSUNBZ1lYTnpaWEowSUM4dklHNXZkRjlqYjI1MGNtOXNiR1Z5Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pNeUNpQWdJQ0F2THlCd2RXSnNhV01nWTI5dWRISnZiR3hoWW14bElEMGdSMnh2WW1Gc1UzUmhkR1U4WVhKak5DNUNiMjlzUGloN0lHdGxlVG9nSjJOMGNteGxiaWNnZlNrS0lDQWdJR2x1ZEdOZk1DQXZMeUF3Q2lBZ0lDQmllWFJsWXlBMUlDOHZJQ0pqZEhKc1pXNGlDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPalE1Q2lBZ0lDQXZMeUJoYzNObGNuUW9kR2hwY3k1amIyNTBjbTlzYkdGaWJHVXVhR0Z6Vm1Gc2RXVWdKaVlnZEdocGN5NWpiMjUwY205c2JHRmliR1V1ZG1Gc2RXVXVibUYwYVhabElEMDlQU0IwY25WbExDQW5ZMjl1ZEhKdmJHeGxjbDlrYVhOaFlteGxaQ2NwQ2lBZ0lDQmhjSEJmWjJ4dlltRnNYMmRsZEY5bGVBb2dJQ0FnWW5WeWVTQXhDaUFnSUNCaWVpQmZiMjVzZVVOdmJuUnliMnhzWlhKZlltOXZiRjltWVd4elpVQXpDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPak15Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdZMjl1ZEhKdmJHeGhZbXhsSUQwZ1IyeHZZbUZzVTNSaGRHVThZWEpqTkM1Q2IyOXNQaWg3SUd0bGVUb2dKMk4wY214bGJpY2dmU2tLSUNBZ0lHbHVkR05mTUNBdkx5QXdDaUFnSUNCaWVYUmxZeUExSUM4dklDSmpkSEpzWlc0aUNpQWdJQ0JoY0hCZloyeHZZbUZzWDJkbGRGOWxlQW9nSUNBZ1lYTnpaWEowSUM4dklHTm9aV05ySUVkc2IySmhiRk4wWVhSbElHVjRhWE4wY3dvZ0lDQWdhVzUwWTE4d0lDOHZJREFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TkRrS0lDQWdJQzh2SUdGemMyVnlkQ2gwYUdsekxtTnZiblJ5YjJ4c1lXSnNaUzVvWVhOV1lXeDFaU0FtSmlCMGFHbHpMbU52Ym5SeWIyeHNZV0pzWlM1MllXeDFaUzV1WVhScGRtVWdQVDA5SUhSeWRXVXNJQ2RqYjI1MGNtOXNiR1Z5WDJScGMyRmliR1ZrSnlrS0lDQWdJR2RsZEdKcGRBb2dJQ0FnYVc1MFkxOHhJQzh2SURFS0lDQWdJRDA5Q2lBZ0lDQmllaUJmYjI1c2VVTnZiblJ5YjJ4c1pYSmZZbTl2YkY5bVlXeHpaVUF6Q2lBZ0lDQnBiblJqWHpFZ0x5OGdNUW9LWDI5dWJIbERiMjUwY205c2JHVnlYMkp2YjJ4ZmJXVnlaMlZBTkRvS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk5Ea0tJQ0FnSUM4dklHRnpjMlZ5ZENoMGFHbHpMbU52Ym5SeWIyeHNZV0pzWlM1b1lYTldZV3gxWlNBbUppQjBhR2x6TG1OdmJuUnliMnhzWVdKc1pTNTJZV3gxWlM1dVlYUnBkbVVnUFQwOUlIUnlkV1VzSUNkamIyNTBjbTlzYkdWeVgyUnBjMkZpYkdWa0p5a0tJQ0FnSUdGemMyVnlkQ0F2THlCamIyNTBjbTlzYkdWeVgyUnBjMkZpYkdWa0NpQWdJQ0J5WlhSemRXSUtDbDl2Ym14NVEyOXVkSEp2Ykd4bGNsOWliMjlzWDJaaGJITmxRRE02Q2lBZ0lDQnBiblJqWHpBZ0x5OGdNQW9nSUNBZ1lpQmZiMjVzZVVOdmJuUnliMnhzWlhKZlltOXZiRjl0WlhKblpVQTBDZ29LTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6bzZRWEpqTVRZME5DNWZZMmhsWTJ0S2RYTjBhV1pwWTJGMGFXOXVLRzl3WlhKaGRHOXlYMlJoZEdFNklHSjVkR1Z6S1NBdFBpQjJiMmxrT2dwZlkyaGxZMnRLZFhOMGFXWnBZMkYwYVc5dU9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6bzFNZ29nSUNBZ0x5OGdjSEp2ZEdWamRHVmtJRjlqYUdWamEwcDFjM1JwWm1sallYUnBiMjRvYjNCbGNtRjBiM0pmWkdGMFlUb2dZWEpqTkM1RWVXNWhiV2xqUW5sMFpYTXBPaUIyYjJsa0lIc0tJQ0FnSUhCeWIzUnZJREVnTUFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pvek13b2dJQ0FnTHk4Z2NIVmliR2xqSUhKbGNYVnBjbVZLZFhOMGFXWnBZMkYwYVc5dUlEMGdSMnh2WW1Gc1UzUmhkR1U4WVhKak5DNUNiMjlzUGloN0lHdGxlVG9nSjNKcWRYTjBKeUI5S1FvZ0lDQWdhVzUwWTE4d0lDOHZJREFLSUNBZ0lHSjVkR1ZqSURFNElDOHZJQ0p5YW5WemRDSUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZOVE1LSUNBZ0lDOHZJR2xtSUNoMGFHbHpMbkpsY1hWcGNtVktkWE4wYVdacFkyRjBhVzl1TG1oaGMxWmhiSFZsSUNZbUlIUm9hWE11Y21WeGRXbHlaVXAxYzNScFptbGpZWFJwYjI0dWRtRnNkV1V1Ym1GMGFYWmxJRDA5UFNCMGNuVmxLU0I3Q2lBZ0lDQmhjSEJmWjJ4dlltRnNYMmRsZEY5bGVBb2dJQ0FnWW5WeWVTQXhDaUFnSUNCaWVpQmZZMmhsWTJ0S2RYTjBhV1pwWTJGMGFXOXVYMkZtZEdWeVgybG1YMlZzYzJWQU13b2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6b3pNd29nSUNBZ0x5OGdjSFZpYkdsaklISmxjWFZwY21WS2RYTjBhV1pwWTJGMGFXOXVJRDBnUjJ4dlltRnNVM1JoZEdVOFlYSmpOQzVDYjI5c1BpaDdJR3RsZVRvZ0ozSnFkWE4wSnlCOUtRb2dJQ0FnYVc1MFkxOHdJQzh2SURBS0lDQWdJR0o1ZEdWaklERTRJQzh2SUNKeWFuVnpkQ0lLSUNBZ0lHRndjRjluYkc5aVlXeGZaMlYwWDJWNENpQWdJQ0JoYzNObGNuUWdMeThnWTJobFkyc2dSMnh2WW1Gc1UzUmhkR1VnWlhocGMzUnpDaUFnSUNCcGJuUmpYekFnTHk4Z01Bb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6bzFNd29nSUNBZ0x5OGdhV1lnS0hSb2FYTXVjbVZ4ZFdseVpVcDFjM1JwWm1sallYUnBiMjR1YUdGelZtRnNkV1VnSmlZZ2RHaHBjeTV5WlhGMWFYSmxTblZ6ZEdsbWFXTmhkR2x2Ymk1MllXeDFaUzV1WVhScGRtVWdQVDA5SUhSeWRXVXBJSHNLSUNBZ0lHZGxkR0pwZEFvZ0lDQWdhVzUwWTE4eElDOHZJREVLSUNBZ0lEMDlDaUFnSUNCaWVpQmZZMmhsWTJ0S2RYTjBhV1pwWTJGMGFXOXVYMkZtZEdWeVgybG1YMlZzYzJWQU13b2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6bzFOQW9nSUNBZ0x5OGdZWE56WlhKMEtHOXdaWEpoZEc5eVgyUmhkR0V1Ym1GMGFYWmxMbXhsYm1kMGFDQStJREFzSUNkcWRYTjBhV1pwWTJGMGFXOXVYM0psY1hWcGNtVmtKeWtLSUNBZ0lHWnlZVzFsWDJScFp5QXRNUW9nSUNBZ1pYaDBjbUZqZENBeUlEQUtJQ0FnSUd4bGJnb2dJQ0FnWVhOelpYSjBJQzh2SUdwMWMzUnBabWxqWVhScGIyNWZjbVZ4ZFdseVpXUUtDbDlqYUdWamEwcDFjM1JwWm1sallYUnBiMjVmWVdaMFpYSmZhV1pmWld4elpVQXpPZ29nSUNBZ2NtVjBjM1ZpQ2dvS0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem82UVhKak1UWTBOQzVmY21GMFpVeHBiV2wwS0NrZ0xUNGdkbTlwWkRvS1gzSmhkR1ZNYVcxcGREb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZNelVLSUNBZ0lDOHZJSEIxWW14cFl5QnRhVzVEYjI1MGNtOXNiR1Z5UVdOMGFXOXVTVzUwWlhKMllXd2dQU0JIYkc5aVlXeFRkR0YwWlR4aGNtTTBMbFZwYm5ST05qUStLSHNnYTJWNU9pQW5iV05oYVNjZ2ZTa0tJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0JpZVhSbFl5QXhOQ0F2THlBaWJXTmhhU0lLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TlRrS0lDQWdJQzh2SUdsbUlDaDBhR2x6TG0xcGJrTnZiblJ5YjJ4c1pYSkJZM1JwYjI1SmJuUmxjblpoYkM1b1lYTldZV3gxWlNBbUppQjBhR2x6TG0xcGJrTnZiblJ5YjJ4c1pYSkJZM1JwYjI1SmJuUmxjblpoYkM1MllXeDFaUzV1WVhScGRtVWdQaUF3YmlrZ2V3b2dJQ0FnWVhCd1gyZHNiMkpoYkY5blpYUmZaWGdLSUNBZ0lHSjFjbmtnTVFvZ0lDQWdZbm9nWDNKaGRHVk1hVzFwZEY5aFpuUmxjbDlwWmw5bGJITmxRRFVLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TXpVS0lDQWdJQzh2SUhCMVlteHBZeUJ0YVc1RGIyNTBjbTlzYkdWeVFXTjBhVzl1U1c1MFpYSjJZV3dnUFNCSGJHOWlZV3hUZEdGMFpUeGhjbU0wTGxWcGJuUk9OalErS0hzZ2EyVjVPaUFuYldOaGFTY2dmU2tLSUNBZ0lHbHVkR05mTUNBdkx5QXdDaUFnSUNCaWVYUmxZeUF4TkNBdkx5QWliV05oYVNJS0lDQWdJR0Z3Y0Y5bmJHOWlZV3hmWjJWMFgyVjRDaUFnSUNCaGMzTmxjblFnTHk4Z1kyaGxZMnNnUjJ4dlltRnNVM1JoZEdVZ1pYaHBjM1J6Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pVNUNpQWdJQ0F2THlCcFppQW9kR2hwY3k1dGFXNURiMjUwY205c2JHVnlRV04wYVc5dVNXNTBaWEoyWVd3dWFHRnpWbUZzZFdVZ0ppWWdkR2hwY3k1dGFXNURiMjUwY205c2JHVnlRV04wYVc5dVNXNTBaWEoyWVd3dWRtRnNkV1V1Ym1GMGFYWmxJRDRnTUc0cElIc0tJQ0FnSUdKMGIya0tJQ0FnSUdKNklGOXlZWFJsVEdsdGFYUmZZV1owWlhKZmFXWmZaV3h6WlVBMUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qTTBDaUFnSUNBdkx5QndkV0pzYVdNZ2JHRnpkRU52Ym5SeWIyeHNaWEpCWTNScGIyNVNiM1Z1WkNBOUlFZHNiMkpoYkZOMFlYUmxQR0Z5WXpRdVZXbHVkRTQyTkQ0b2V5QnJaWGs2SUNkc1kyRnlKeUI5S1NBdkx5QnZjSFJwYjI1aGJDQnlZWFJsSUd4cGJXbDBJSFJ5WVdOcmFXNW5DaUFnSUNCcGJuUmpYekFnTHk4Z01Bb2dJQ0FnWW5sMFpXTWdNVGtnTHk4Z0lteGpZWElpQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pZd0NpQWdJQ0F2THlCcFppQW9kR2hwY3k1c1lYTjBRMjl1ZEhKdmJHeGxja0ZqZEdsdmJsSnZkVzVrTG1oaGMxWmhiSFZsS1NCN0NpQWdJQ0JoY0hCZloyeHZZbUZzWDJkbGRGOWxlQW9nSUNBZ1luVnllU0F4Q2lBZ0lDQmllaUJmY21GMFpVeHBiV2wwWDJGbWRHVnlYMmxtWDJWc2MyVkFOQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem96TkFvZ0lDQWdMeThnY0hWaWJHbGpJR3hoYzNSRGIyNTBjbTlzYkdWeVFXTjBhVzl1VW05MWJtUWdQU0JIYkc5aVlXeFRkR0YwWlR4aGNtTTBMbFZwYm5ST05qUStLSHNnYTJWNU9pQW5iR05oY2ljZ2ZTa2dMeThnYjNCMGFXOXVZV3dnY21GMFpTQnNhVzFwZENCMGNtRmphMmx1WndvZ0lDQWdhVzUwWTE4d0lDOHZJREFLSUNBZ0lHSjVkR1ZqSURFNUlDOHZJQ0pzWTJGeUlnb2dJQ0FnWVhCd1gyZHNiMkpoYkY5blpYUmZaWGdLSUNBZ0lHRnpjMlZ5ZENBdkx5QmphR1ZqYXlCSGJHOWlZV3hUZEdGMFpTQmxlR2x6ZEhNS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk5qRUtJQ0FnSUM4dklHTnZibk4wSUd4aGMzUWdQU0IwYUdsekxteGhjM1JEYjI1MGNtOXNiR1Z5UVdOMGFXOXVVbTkxYm1RdWRtRnNkV1V1Ym1GMGFYWmxDaUFnSUNCaWRHOXBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPak0xQ2lBZ0lDQXZMeUJ3ZFdKc2FXTWdiV2x1UTI5dWRISnZiR3hsY2tGamRHbHZia2x1ZEdWeWRtRnNJRDBnUjJ4dlltRnNVM1JoZEdVOFlYSmpOQzVWYVc1MFRqWTBQaWg3SUd0bGVUb2dKMjFqWVdrbklIMHBDaUFnSUNCcGJuUmpYekFnTHk4Z01Bb2dJQ0FnWW5sMFpXTWdNVFFnTHk4Z0ltMWpZV2tpQ2lBZ0lDQmhjSEJmWjJ4dlltRnNYMmRsZEY5bGVBb2dJQ0FnWVhOelpYSjBJQzh2SUdOb1pXTnJJRWRzYjJKaGJGTjBZWFJsSUdWNGFYTjBjd29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem8yTWdvZ0lDQWdMeThnWTI5dWMzUWdiV2x1UjJGd0lEMGdkR2hwY3k1dGFXNURiMjUwY205c2JHVnlRV04wYVc5dVNXNTBaWEoyWVd3dWRtRnNkV1V1Ym1GMGFYWmxDaUFnSUNCaWRHOXBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPall6Q2lBZ0lDQXZMeUJqYjI1emRDQmpkWEp5Wlc1MElEMGdibVYzSUdGeVl6UXVWV2x1ZEU0Mk5DaEhiRzlpWVd3dWNtOTFibVFwTG01aGRHbDJaUW9nSUNBZ1oyeHZZbUZzSUZKdmRXNWtDaUFnSUNCcGRHOWlDaUFnSUNCaWRHOXBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPalkwQ2lBZ0lDQXZMeUJoYzNObGNuUW9ZM1Z5Y21WdWRDQStQU0JzWVhOMElDc2diV2x1UjJGd0xDQW5jbUYwWlY5c2FXMXBkR1ZrSnlrS0lDQWdJR052ZG1WeUlESUtJQ0FnSUNzS0lDQWdJRDQ5Q2lBZ0lDQmhjM05sY25RZ0x5OGdjbUYwWlY5c2FXMXBkR1ZrQ2dwZmNtRjBaVXhwYldsMFgyRm1kR1Z5WDJsbVgyVnNjMlZBTkRvS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk5qWUtJQ0FnSUM4dklIUm9hWE11YkdGemRFTnZiblJ5YjJ4c1pYSkJZM1JwYjI1U2IzVnVaQzUyWVd4MVpTQTlJRzVsZHlCaGNtTTBMbFZwYm5ST05qUW9SMnh2WW1Gc0xuSnZkVzVrS1FvZ0lDQWdaMnh2WW1Gc0lGSnZkVzVrQ2lBZ0lDQnBkRzlpQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pNMENpQWdJQ0F2THlCd2RXSnNhV01nYkdGemRFTnZiblJ5YjJ4c1pYSkJZM1JwYjI1U2IzVnVaQ0E5SUVkc2IySmhiRk4wWVhSbFBHRnlZelF1VldsdWRFNDJORDRvZXlCclpYazZJQ2RzWTJGeUp5QjlLU0F2THlCdmNIUnBiMjVoYkNCeVlYUmxJR3hwYldsMElIUnlZV05yYVc1bkNpQWdJQ0JpZVhSbFl5QXhPU0F2THlBaWJHTmhjaUlLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TmpZS0lDQWdJQzh2SUhSb2FYTXViR0Z6ZEVOdmJuUnliMnhzWlhKQlkzUnBiMjVTYjNWdVpDNTJZV3gxWlNBOUlHNWxkeUJoY21NMExsVnBiblJPTmpRb1IyeHZZbUZzTG5KdmRXNWtLUW9nSUNBZ2MzZGhjQW9nSUNBZ1lYQndYMmRzYjJKaGJGOXdkWFFLQ2w5eVlYUmxUR2x0YVhSZllXWjBaWEpmYVdaZlpXeHpaVUExT2dvZ0lDQWdjbVYwYzNWaUNnb0tMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pvNlFYSmpNVFkwTkM1aGNtTXhOalEwWDNObGRGOWpiMjUwY205c2JHVnlLRzVsZDE5amIyNTBjbTlzYkdWeU9pQmllWFJsY3lrZ0xUNGdkbTlwWkRvS1lYSmpNVFkwTkY5elpYUmZZMjl1ZEhKdmJHeGxjam9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TnpFdE56SUtJQ0FnSUM4dklFQmhjbU0wTG1GaWFXMWxkR2h2WkNncENpQWdJQ0F2THlCd2RXSnNhV01nWVhKak1UWTBORjl6WlhSZlkyOXVkSEp2Ykd4bGNpaHVaWGRmWTI5dWRISnZiR3hsY2pvZ1lYSmpOQzVCWkdSeVpYTnpLVG9nZG05cFpDQjdDaUFnSUNCd2NtOTBieUF4SURBS0lDQWdJR2x1ZEdOZk1DQXZMeUF3Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pjekNpQWdJQ0F2THlCMGFHbHpMbDl2Ym14NVQzZHVaWElvS1FvZ0lDQWdZMkZzYkhOMVlpQmZiMjVzZVU5M2JtVnlDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPak14Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdZMjl1ZEhKdmJHeGxjaUE5SUVkc2IySmhiRk4wWVhSbFBHRnlZelF1UVdSa2NtVnpjejRvZXlCclpYazZJQ2RqZEhKc0p5QjlLUW9nSUNBZ2FXNTBZMTh3SUM4dklEQUtJQ0FnSUdKNWRHVmpJREV3SUM4dklDSmpkSEpzSWdvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pvM05Bb2dJQ0FnTHk4Z1kyOXVjM1FnYjJ4a0lEMGdkR2hwY3k1amIyNTBjbTlzYkdWeUxtaGhjMVpoYkhWbElEOGdkR2hwY3k1amIyNTBjbTlzYkdWeUxuWmhiSFZsSURvZ2JtVjNJR0Z5WXpRdVFXUmtjbVZ6Y3lncENpQWdJQ0JoY0hCZloyeHZZbUZzWDJkbGRGOWxlQW9nSUNBZ1luVnllU0F4Q2lBZ0lDQmllaUJoY21NeE5qUTBYM05sZEY5amIyNTBjbTlzYkdWeVgzUmxjbTVoY25sZlptRnNjMlZBTWdvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pvek1Rb2dJQ0FnTHk4Z2NIVmliR2xqSUdOdmJuUnliMnhzWlhJZ1BTQkhiRzlpWVd4VGRHRjBaVHhoY21NMExrRmtaSEpsYzNNK0tIc2dhMlY1T2lBblkzUnliQ2NnZlNrS0lDQWdJR2x1ZEdOZk1DQXZMeUF3Q2lBZ0lDQmllWFJsWXlBeE1DQXZMeUFpWTNSeWJDSUtJQ0FnSUdGd2NGOW5iRzlpWVd4ZloyVjBYMlY0Q2lBZ0lDQmhjM05sY25RZ0x5OGdZMmhsWTJzZ1IyeHZZbUZzVTNSaGRHVWdaWGhwYzNSekNpQWdJQ0JtY21GdFpWOWlkWEo1SURBS0NtRnlZekUyTkRSZmMyVjBYMk52Ym5SeWIyeHNaWEpmZEdWeWJtRnllVjl0WlhKblpVQXpPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem96TVFvZ0lDQWdMeThnY0hWaWJHbGpJR052Ym5SeWIyeHNaWElnUFNCSGJHOWlZV3hUZEdGMFpUeGhjbU0wTGtGa1pISmxjM00rS0hzZ2EyVjVPaUFuWTNSeWJDY2dmU2tLSUNBZ0lHSjVkR1ZqSURFd0lDOHZJQ0pqZEhKc0lnb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6bzNOUW9nSUNBZ0x5OGdkR2hwY3k1amIyNTBjbTlzYkdWeUxuWmhiSFZsSUQwZ2JtVjNYMk52Ym5SeWIyeHNaWElLSUNBZ0lHWnlZVzFsWDJScFp5QXRNUW9nSUNBZ1lYQndYMmRzYjJKaGJGOXdkWFFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TXpJS0lDQWdJQzh2SUhCMVlteHBZeUJqYjI1MGNtOXNiR0ZpYkdVZ1BTQkhiRzlpWVd4VGRHRjBaVHhoY21NMExrSnZiMncrS0hzZ2EyVjVPaUFuWTNSeWJHVnVKeUI5S1FvZ0lDQWdhVzUwWTE4d0lDOHZJREFLSUNBZ0lHSjVkR1ZqSURVZ0x5OGdJbU4wY214bGJpSUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZOellLSUNBZ0lDOHZJR2xtSUNnaGRHaHBjeTVqYjI1MGNtOXNiR0ZpYkdVdWFHRnpWbUZzZFdVcElIc0tJQ0FnSUdGd2NGOW5iRzlpWVd4ZloyVjBYMlY0Q2lBZ0lDQmlkWEo1SURFS0lDQWdJR0p1ZWlCaGNtTXhOalEwWDNObGRGOWpiMjUwY205c2JHVnlYMkZtZEdWeVgybG1YMlZzYzJWQU5Rb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6b3pNZ29nSUNBZ0x5OGdjSFZpYkdsaklHTnZiblJ5YjJ4c1lXSnNaU0E5SUVkc2IySmhiRk4wWVhSbFBHRnlZelF1UW05dmJENG9leUJyWlhrNklDZGpkSEpzWlc0bklIMHBDaUFnSUNCaWVYUmxZeUExSUM4dklDSmpkSEpzWlc0aUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qYzNDaUFnSUNBdkx5QjBhR2x6TG1OdmJuUnliMnhzWVdKc1pTNTJZV3gxWlNBOUlHNWxkeUJoY21NMExrSnZiMndvZEhKMVpTa0tJQ0FnSUdKNWRHVmpJRGNnTHk4Z01IZzRNQW9nSUNBZ1lYQndYMmRzYjJKaGJGOXdkWFFLQ21GeVl6RTJORFJmYzJWMFgyTnZiblJ5YjJ4c1pYSmZZV1owWlhKZmFXWmZaV3h6WlVBMU9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6bzNPUW9nSUNBZ0x5OGdaVzFwZENnblEyOXVkSEp2Ykd4bGNrTm9ZVzVuWldRbkxDQnVaWGNnWVhKak1UWTBORjlqYjI1MGNtOXNiR1Z5WDJOb1lXNW5aV1JmWlhabGJuUW9leUJ2YkdRc0lHNWxkVG9nYm1WM1gyTnZiblJ5YjJ4c1pYSWdmU2twQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dNQW9nSUNBZ1puSmhiV1ZmWkdsbklDMHhDaUFnSUNCamIyNWpZWFFLSUNBZ0lIQjFjMmhpZVhSbGN5QXdlRFF3T1dOak5UY3dJQzh2SUcxbGRHaHZaQ0FpUTI5dWRISnZiR3hsY2tOb1lXNW5aV1FvS0dGa1pISmxjM01zWVdSa2NtVnpjeWtwSWdvZ0lDQWdjM2RoY0FvZ0lDQWdZMjl1WTJGMENpQWdJQ0JzYjJjS0lDQWdJSEpsZEhOMVlnb0tZWEpqTVRZME5GOXpaWFJmWTI5dWRISnZiR3hsY2w5MFpYSnVZWEo1WDJaaGJITmxRREk2Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pjMENpQWdJQ0F2THlCamIyNXpkQ0J2YkdRZ1BTQjBhR2x6TG1OdmJuUnliMnhzWlhJdWFHRnpWbUZzZFdVZ1B5QjBhR2x6TG1OdmJuUnliMnhzWlhJdWRtRnNkV1VnT2lCdVpYY2dZWEpqTkM1QlpHUnlaWE56S0NrS0lDQWdJR0o1ZEdWalh6RWdMeThnWVdSa2NpQkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQldUVklSa3RSQ2lBZ0lDQm1jbUZ0WlY5aWRYSjVJREFLSUNBZ0lHSWdZWEpqTVRZME5GOXpaWFJmWTI5dWRISnZiR3hsY2w5MFpYSnVZWEo1WDIxbGNtZGxRRE1LQ2dvdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPanBCY21NeE5qUTBMbUZ5WXpFMk5EUmZjMlYwWDJOdmJuUnliMnhzWVdKc1pTaG1iR0ZuT2lCaWVYUmxjeWtnTFQ0Z2RtOXBaRG9LWVhKak1UWTBORjl6WlhSZlkyOXVkSEp2Ykd4aFlteGxPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem80TWkwNE13b2dJQ0FnTHk4Z1FHRnlZelF1WVdKcGJXVjBhRzlrS0NrS0lDQWdJQzh2SUhCMVlteHBZeUJoY21NeE5qUTBYM05sZEY5amIyNTBjbTlzYkdGaWJHVW9abXhoWnpvZ1lYSmpOQzVDYjI5c0tUb2dkbTlwWkNCN0NpQWdJQ0J3Y205MGJ5QXhJREFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02T0RRS0lDQWdJQzh2SUhSb2FYTXVYMjl1YkhsUGQyNWxjaWdwQ2lBZ0lDQmpZV3hzYzNWaUlGOXZibXg1VDNkdVpYSUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZPRFlLSUNBZ0lDOHZJR2xtSUNobWJHRm5MbTVoZEdsMlpTQTlQVDBnWm1Gc2MyVXBJSHNLSUNBZ0lHWnlZVzFsWDJScFp5QXRNUW9nSUNBZ2FXNTBZMTh3SUM4dklEQUtJQ0FnSUdkbGRHSnBkQW9nSUNBZ1ltNTZJR0Z5WXpFMk5EUmZjMlYwWDJOdmJuUnliMnhzWVdKc1pWOWxiSE5sWDJKdlpIbEFNZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem96TWdvZ0lDQWdMeThnY0hWaWJHbGpJR052Ym5SeWIyeHNZV0pzWlNBOUlFZHNiMkpoYkZOMFlYUmxQR0Z5WXpRdVFtOXZiRDRvZXlCclpYazZJQ2RqZEhKc1pXNG5JSDBwQ2lBZ0lDQmllWFJsWXlBMUlDOHZJQ0pqZEhKc1pXNGlDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPamczQ2lBZ0lDQXZMeUIwYUdsekxtTnZiblJ5YjJ4c1lXSnNaUzUyWVd4MVpTQTlJR1pzWVdjS0lDQWdJR1p5WVcxbFgyUnBaeUF0TVFvZ0lDQWdZWEJ3WDJkc2IySmhiRjl3ZFhRS0NtRnlZekUyTkRSZmMyVjBYMk52Ym5SeWIyeHNZV0pzWlY5aFpuUmxjbDlwWmw5bGJITmxRRFk2Q2lBZ0lDQnlaWFJ6ZFdJS0NtRnlZekUyTkRSZmMyVjBYMk52Ym5SeWIyeHNZV0pzWlY5bGJITmxYMkp2WkhsQU1qb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZNeklLSUNBZ0lDOHZJSEIxWW14cFl5QmpiMjUwY205c2JHRmliR1VnUFNCSGJHOWlZV3hUZEdGMFpUeGhjbU0wTGtKdmIydytLSHNnYTJWNU9pQW5ZM1J5YkdWdUp5QjlLUW9nSUNBZ2FXNTBZMTh3SUM4dklEQUtJQ0FnSUdKNWRHVmpJRFVnTHk4Z0ltTjBjbXhsYmlJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk9UQUtJQ0FnSUM4dklHbG1JQ2doZEdocGN5NWpiMjUwY205c2JHRmliR1V1YUdGelZtRnNkV1VnZkh3Z2RHaHBjeTVqYjI1MGNtOXNiR0ZpYkdVdWRtRnNkV1V1Ym1GMGFYWmxJRDA5UFNCMGNuVmxLU0I3Q2lBZ0lDQmhjSEJmWjJ4dlltRnNYMmRsZEY5bGVBb2dJQ0FnWW5WeWVTQXhDaUFnSUNCaWVpQmhjbU14TmpRMFgzTmxkRjlqYjI1MGNtOXNiR0ZpYkdWZmFXWmZZbTlrZVVBMENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qTXlDaUFnSUNBdkx5QndkV0pzYVdNZ1kyOXVkSEp2Ykd4aFlteGxJRDBnUjJ4dlltRnNVM1JoZEdVOFlYSmpOQzVDYjI5c1BpaDdJR3RsZVRvZ0oyTjBjbXhsYmljZ2ZTa0tJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0JpZVhSbFl5QTFJQzh2SUNKamRISnNaVzRpQ2lBZ0lDQmhjSEJmWjJ4dlltRnNYMmRsZEY5bGVBb2dJQ0FnWVhOelpYSjBJQzh2SUdOb1pXTnJJRWRzYjJKaGJGTjBZWFJsSUdWNGFYTjBjd29nSUNBZ2FXNTBZMTh3SUM4dklEQUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZPVEFLSUNBZ0lDOHZJR2xtSUNnaGRHaHBjeTVqYjI1MGNtOXNiR0ZpYkdVdWFHRnpWbUZzZFdVZ2ZId2dkR2hwY3k1amIyNTBjbTlzYkdGaWJHVXVkbUZzZFdVdWJtRjBhWFpsSUQwOVBTQjBjblZsS1NCN0NpQWdJQ0JuWlhSaWFYUUtJQ0FnSUdsdWRHTmZNU0F2THlBeENpQWdJQ0E5UFFvZ0lDQWdZbm9nWVhKak1UWTBORjl6WlhSZlkyOXVkSEp2Ykd4aFlteGxYMkZtZEdWeVgybG1YMlZzYzJWQU5nb0tZWEpqTVRZME5GOXpaWFJmWTI5dWRISnZiR3hoWW14bFgybG1YMkp2WkhsQU5Eb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZNeklLSUNBZ0lDOHZJSEIxWW14cFl5QmpiMjUwY205c2JHRmliR1VnUFNCSGJHOWlZV3hUZEdGMFpUeGhjbU0wTGtKdmIydytLSHNnYTJWNU9pQW5ZM1J5YkdWdUp5QjlLUW9nSUNBZ1lubDBaV01nTlNBdkx5QWlZM1J5YkdWdUlnb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6bzVNUW9nSUNBZ0x5OGdkR2hwY3k1amIyNTBjbTlzYkdGaWJHVXVkbUZzZFdVZ1BTQm1iR0ZuQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVEVLSUNBZ0lHRndjRjluYkc5aVlXeGZjSFYwQ2lBZ0lDQnlaWFJ6ZFdJS0Nnb3ZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pwQmNtTXhOalEwTG1GeVl6RTJORFJmYzJWMFgzSmxjWFZwY21WZmFuVnpkR2xtYVdOaGRHbHZiaWhtYkdGbk9pQmllWFJsY3lrZ0xUNGdkbTlwWkRvS1lYSmpNVFkwTkY5elpYUmZjbVZ4ZFdseVpWOXFkWE4wYVdacFkyRjBhVzl1T2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pvNU5pMDVOd29nSUNBZ0x5OGdRR0Z5WXpRdVlXSnBiV1YwYUc5a0tDa0tJQ0FnSUM4dklIQjFZbXhwWXlCaGNtTXhOalEwWDNObGRGOXlaWEYxYVhKbFgycDFjM1JwWm1sallYUnBiMjRvWm14aFp6b2dZWEpqTkM1Q2IyOXNLVG9nZG05cFpDQjdDaUFnSUNCd2NtOTBieUF4SURBS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk9UZ0tJQ0FnSUM4dklIUm9hWE11WDI5dWJIbFBkMjVsY2lncENpQWdJQ0JqWVd4c2MzVmlJRjl2Ym14NVQzZHVaWElLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TXpNS0lDQWdJQzh2SUhCMVlteHBZeUJ5WlhGMWFYSmxTblZ6ZEdsbWFXTmhkR2x2YmlBOUlFZHNiMkpoYkZOMFlYUmxQR0Z5WXpRdVFtOXZiRDRvZXlCclpYazZJQ2R5YW5WemRDY2dmU2tLSUNBZ0lHSjVkR1ZqSURFNElDOHZJQ0p5YW5WemRDSUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZPVGtLSUNBZ0lDOHZJSFJvYVhNdWNtVnhkV2x5WlVwMWMzUnBabWxqWVhScGIyNHVkbUZzZFdVZ1BTQm1iR0ZuQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVEVLSUNBZ0lHRndjRjluYkc5aVlXeGZjSFYwQ2lBZ0lDQnlaWFJ6ZFdJS0Nnb3ZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pwQmNtTXhOalEwTG1GeVl6RTJORFJmYzJWMFgyMXBibDloWTNScGIyNWZhVzUwWlhKMllXd29hVzUwWlhKMllXdzZJR0o1ZEdWektTQXRQaUIyYjJsa09ncGhjbU14TmpRMFgzTmxkRjl0YVc1ZllXTjBhVzl1WDJsdWRHVnlkbUZzT2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pveE1ESXRNVEF6Q2lBZ0lDQXZMeUJBWVhKak5DNWhZbWx0WlhSb2IyUW9LUW9nSUNBZ0x5OGdjSFZpYkdsaklHRnlZekUyTkRSZmMyVjBYMjFwYmw5aFkzUnBiMjVmYVc1MFpYSjJZV3dvYVc1MFpYSjJZV3c2SUdGeVl6UXVWV2x1ZEU0Mk5DazZJSFp2YVdRZ2V3b2dJQ0FnY0hKdmRHOGdNU0F3Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pFd05Bb2dJQ0FnTHk4Z2RHaHBjeTVmYjI1c2VVOTNibVZ5S0NrS0lDQWdJR05oYkd4emRXSWdYMjl1YkhsUGQyNWxjZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem96TlFvZ0lDQWdMeThnY0hWaWJHbGpJRzFwYmtOdmJuUnliMnhzWlhKQlkzUnBiMjVKYm5SbGNuWmhiQ0E5SUVkc2IySmhiRk4wWVhSbFBHRnlZelF1VldsdWRFNDJORDRvZXlCclpYazZJQ2R0WTJGcEp5QjlLUW9nSUNBZ1lubDBaV01nTVRRZ0x5OGdJbTFqWVdraUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qRXdOUW9nSUNBZ0x5OGdkR2hwY3k1dGFXNURiMjUwY205c2JHVnlRV04wYVc5dVNXNTBaWEoyWVd3dWRtRnNkV1VnUFNCcGJuUmxjblpoYkFvZ0lDQWdabkpoYldWZlpHbG5JQzB4Q2lBZ0lDQmhjSEJmWjJ4dlltRnNYM0IxZEFvZ0lDQWdjbVYwYzNWaUNnb0tMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pvNlFYSmpNVFkwTkM1aGNtTXhOalEwWDJselgyTnZiblJ5YjJ4c1lXSnNaU2dwSUMwK0lHSjVkR1Z6T2dwaGNtTXhOalEwWDJselgyTnZiblJ5YjJ4c1lXSnNaVG9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TXpJS0lDQWdJQzh2SUhCMVlteHBZeUJqYjI1MGNtOXNiR0ZpYkdVZ1BTQkhiRzlpWVd4VGRHRjBaVHhoY21NMExrSnZiMncrS0hzZ2EyVjVPaUFuWTNSeWJHVnVKeUI5S1FvZ0lDQWdhVzUwWTE4d0lDOHZJREFLSUNBZ0lHSjVkR1ZqSURVZ0x5OGdJbU4wY214bGJpSUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZNVEV4Q2lBZ0lDQXZMeUJwWmlBb2RHaHBjeTVqYjI1MGNtOXNiR0ZpYkdVdWFHRnpWbUZzZFdVZ0ppWWdkR2hwY3k1amIyNTBjbTlzYkdGaWJHVXVkbUZzZFdVdWJtRjBhWFpsSUQwOVBTQjBjblZsSUNZbUlIUm9hWE11WTI5dWRISnZiR3hsY2k1b1lYTldZV3gxWlNrZ2V3b2dJQ0FnWVhCd1gyZHNiMkpoYkY5blpYUmZaWGdLSUNBZ0lHSjFjbmtnTVFvZ0lDQWdZbm9nWVhKak1UWTBORjlwYzE5amIyNTBjbTlzYkdGaWJHVmZZV1owWlhKZmFXWmZaV3h6WlVBMENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qTXlDaUFnSUNBdkx5QndkV0pzYVdNZ1kyOXVkSEp2Ykd4aFlteGxJRDBnUjJ4dlltRnNVM1JoZEdVOFlYSmpOQzVDYjI5c1BpaDdJR3RsZVRvZ0oyTjBjbXhsYmljZ2ZTa0tJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0JpZVhSbFl5QTFJQzh2SUNKamRISnNaVzRpQ2lBZ0lDQmhjSEJmWjJ4dlltRnNYMmRsZEY5bGVBb2dJQ0FnWVhOelpYSjBJQzh2SUdOb1pXTnJJRWRzYjJKaGJGTjBZWFJsSUdWNGFYTjBjd29nSUNBZ2FXNTBZMTh3SUM4dklEQUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZNVEV4Q2lBZ0lDQXZMeUJwWmlBb2RHaHBjeTVqYjI1MGNtOXNiR0ZpYkdVdWFHRnpWbUZzZFdVZ0ppWWdkR2hwY3k1amIyNTBjbTlzYkdGaWJHVXVkbUZzZFdVdWJtRjBhWFpsSUQwOVBTQjBjblZsSUNZbUlIUm9hWE11WTI5dWRISnZiR3hsY2k1b1lYTldZV3gxWlNrZ2V3b2dJQ0FnWjJWMFltbDBDaUFnSUNCcGJuUmpYekVnTHk4Z01Rb2dJQ0FnUFQwS0lDQWdJR0o2SUdGeVl6RTJORFJmYVhOZlkyOXVkSEp2Ykd4aFlteGxYMkZtZEdWeVgybG1YMlZzYzJWQU5Bb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6b3pNUW9nSUNBZ0x5OGdjSFZpYkdsaklHTnZiblJ5YjJ4c1pYSWdQU0JIYkc5aVlXeFRkR0YwWlR4aGNtTTBMa0ZrWkhKbGMzTStLSHNnYTJWNU9pQW5ZM1J5YkNjZ2ZTa0tJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0JpZVhSbFl5QXhNQ0F2THlBaVkzUnliQ0lLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TVRFeENpQWdJQ0F2THlCcFppQW9kR2hwY3k1amIyNTBjbTlzYkdGaWJHVXVhR0Z6Vm1Gc2RXVWdKaVlnZEdocGN5NWpiMjUwY205c2JHRmliR1V1ZG1Gc2RXVXVibUYwYVhabElEMDlQU0IwY25WbElDWW1JSFJvYVhNdVkyOXVkSEp2Ykd4bGNpNW9ZWE5XWVd4MVpTa2dld29nSUNBZ1lYQndYMmRzYjJKaGJGOW5aWFJmWlhnS0lDQWdJR0oxY25rZ01Rb2dJQ0FnWW5vZ1lYSmpNVFkwTkY5cGMxOWpiMjUwY205c2JHRmliR1ZmWVdaMFpYSmZhV1pmWld4elpVQTBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPakV4TWdvZ0lDQWdMeThnY21WMGRYSnVJRzVsZHlCaGNtTTBMbFZwYm5ST05qUW9NVzRwQ2lBZ0lDQndkWE5vWW5sMFpYTWdNSGd3TURBd01EQXdNREF3TURBd01EQXhDaUFnSUNCeVpYUnpkV0lLQ21GeVl6RTJORFJmYVhOZlkyOXVkSEp2Ykd4aFlteGxYMkZtZEdWeVgybG1YMlZzYzJWQU5Eb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZNVEUwQ2lBZ0lDQXZMeUJ5WlhSMWNtNGdibVYzSUdGeVl6UXVWV2x1ZEU0Mk5DZ3diaWtLSUNBZ0lHSjVkR1ZqSURJd0lDOHZJREI0TURBd01EQXdNREF3TURBd01EQXdNQW9nSUNBZ2NtVjBjM1ZpQ2dvS0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem82UVhKak1UWTBOQzVoY21NeE5qUTBYMk52Ym5SeWIyeHNaWEpmZEhKaGJuTm1aWElvWm5KdmJUb2dZbmwwWlhNc0lIUnZPaUJpZVhSbGN5d2dZVzF2ZFc1ME9pQmllWFJsY3l3Z1pHRjBZVG9nWW5sMFpYTXNJRzl3WlhKaGRHOXlYMlJoZEdFNklHSjVkR1Z6S1NBdFBpQmllWFJsY3pvS1lYSmpNVFkwTkY5amIyNTBjbTlzYkdWeVgzUnlZVzV6Wm1WeU9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6b3hNVGN0TVRJMENpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvS1FvZ0lDQWdMeThnY0hWaWJHbGpJR0Z5WXpFMk5EUmZZMjl1ZEhKdmJHeGxjbDkwY21GdWMyWmxjaWdLSUNBZ0lDOHZJQ0FnWm5KdmJUb2dZWEpqTkM1QlpHUnlaWE56TEFvZ0lDQWdMeThnSUNCMGJ6b2dZWEpqTkM1QlpHUnlaWE56TEFvZ0lDQWdMeThnSUNCaGJXOTFiblE2SUdGeVl6UXVWV2x1ZEU0eU5UWXNDaUFnSUNBdkx5QWdJR1JoZEdFNklHRnlZelF1UkhsdVlXMXBZMEo1ZEdWekxBb2dJQ0FnTHk4Z0lDQnZjR1Z5WVhSdmNsOWtZWFJoT2lCaGNtTTBMa1I1Ym1GdGFXTkNlWFJsY3l3S0lDQWdJQzh2SUNrNklHRnlZelF1VldsdWRFNDJOQ0I3Q2lBZ0lDQndjbTkwYnlBMUlERUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZNVEkxQ2lBZ0lDQXZMeUIwYUdsekxsOXZibXg1UTI5dWRISnZiR3hsY2lncENpQWdJQ0JqWVd4c2MzVmlJRjl2Ym14NVEyOXVkSEp2Ykd4bGNnb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6b3hNallLSUNBZ0lDOHZJSFJvYVhNdVgyTm9aV05yU25WemRHbG1hV05oZEdsdmJpaHZjR1Z5WVhSdmNsOWtZWFJoS1FvZ0lDQWdabkpoYldWZlpHbG5JQzB4Q2lBZ0lDQmpZV3hzYzNWaUlGOWphR1ZqYTBwMWMzUnBabWxqWVhScGIyNEtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZNVEkzQ2lBZ0lDQXZMeUIwYUdsekxsOXlZWFJsVEdsdGFYUW9LUW9nSUNBZ1kyRnNiSE4xWWlCZmNtRjBaVXhwYldsMENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qRXlPUW9nSUNBZ0x5OGdZWE56WlhKMEtHWnliMjBnSVQwOUlIUnZMQ0FuYzJGdFpWOWhaR1J5SnlrS0lDQWdJR1p5WVcxbFgyUnBaeUF0TlFvZ0lDQWdabkpoYldWZlpHbG5JQzAwQ2lBZ0lDQWhQUW9nSUNBZ1lYTnpaWEowSUM4dklITmhiV1ZmWVdSa2Nnb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6b3hNekFLSUNBZ0lDOHZJR052Ym5OMElHWnliMjFDWVd3Z1BTQjBhR2x6TGw5aVlXeGhibU5sVDJZb1puSnZiU2tLSUNBZ0lHWnlZVzFsWDJScFp5QXROUW9nSUNBZ1kyRnNiSE4xWWlCZlltRnNZVzVqWlU5bUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qRXpNUW9nSUNBZ0x5OGdZWE56WlhKMEtHWnliMjFDWVd3dWJtRjBhWFpsSUQ0OUlHRnRiM1Z1ZEM1dVlYUnBkbVVzSUNkcGJuTjFabVpwWTJsbGJuUW5LUW9nSUNBZ1pIVndDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUTUtJQ0FnSUdJK1BRb2dJQ0FnWVhOelpYSjBJQzh2SUdsdWMzVm1abWxqYVdWdWRBb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6b3hNek1LSUNBZ0lDOHZJSFJvYVhNdVltRnNZVzVqWlhNb1puSnZiU2t1ZG1Gc2RXVWdQU0J1WlhjZ1lYSmpOQzVWYVc1MFRqSTFOaWhtY205dFFtRnNMbTVoZEdsMlpTQXRJR0Z0YjNWdWRDNXVZWFJwZG1VcENpQWdJQ0JtY21GdFpWOWthV2NnTFRNS0lDQWdJR0l0Q2lBZ0lDQmtkWEFLSUNBZ0lHeGxiZ29nSUNBZ2FXNTBZMTh5SUM4dklETXlDaUFnSUNBOFBRb2dJQ0FnWVhOelpYSjBJQzh2SUc5MlpYSm1iRzkzQ2lBZ0lDQnBiblJqWHpJZ0x5OGdNeklLSUNBZ0lHSjZaWEp2Q2lBZ0lDQnpkMkZ3Q2lBZ0lDQmthV2NnTVFvZ0lDQWdZbndLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem8xTXdvZ0lDQWdMeThnY0hWaWJHbGpJR0poYkdGdVkyVnpJRDBnUW05NFRXRndQRUZrWkhKbGMzTXNJRlZwYm5ST01qVTJQaWg3SUd0bGVWQnlaV1pwZURvZ0oySW5JSDBwQ2lBZ0lDQmllWFJsWXlBMElDOHZJQ0ppSWdvZ0lDQWdabkpoYldWZlpHbG5JQzAxQ2lBZ0lDQmpiMjVqWVhRS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk1UTXpDaUFnSUNBdkx5QjBhR2x6TG1KaGJHRnVZMlZ6S0daeWIyMHBMblpoYkhWbElEMGdibVYzSUdGeVl6UXVWV2x1ZEU0eU5UWW9abkp2YlVKaGJDNXVZWFJwZG1VZ0xTQmhiVzkxYm5RdWJtRjBhWFpsS1FvZ0lDQWdjM2RoY0FvZ0lDQWdZbTk0WDNCMWRBb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6b3hNelFLSUNBZ0lDOHZJR052Ym5OMElIUnZRbUZzSUQwZ2RHaHBjeTVmWW1Gc1lXNWpaVTltS0hSdktRb2dJQ0FnWm5KaGJXVmZaR2xuSUMwMENpQWdJQ0JqWVd4c2MzVmlJRjlpWVd4aGJtTmxUMllLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TVRNMUNpQWdJQ0F2THlCMGFHbHpMbUpoYkdGdVkyVnpLSFJ2S1M1MllXeDFaU0E5SUc1bGR5QmhjbU0wTGxWcGJuUk9NalUyS0hSdlFtRnNMbTVoZEdsMlpTQXJJR0Z0YjNWdWRDNXVZWFJwZG1VcENpQWdJQ0JtY21GdFpWOWthV2NnTFRNS0lDQWdJR0lyQ2lBZ0lDQmtkWEFLSUNBZ0lHeGxiZ29nSUNBZ2FXNTBZMTh5SUM4dklETXlDaUFnSUNBOFBRb2dJQ0FnWVhOelpYSjBJQzh2SUc5MlpYSm1iRzkzQ2lBZ0lDQmlmQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPalV6Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdZbUZzWVc1alpYTWdQU0JDYjNoTllYQThRV1JrY21WemN5d2dWV2x1ZEU0eU5UWStLSHNnYTJWNVVISmxabWw0T2lBbllpY2dmU2tLSUNBZ0lHSjVkR1ZqSURRZ0x5OGdJbUlpQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVFFLSUNBZ0lHTnZibU5oZEFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pveE16VUtJQ0FnSUM4dklIUm9hWE11WW1Gc1lXNWpaWE1vZEc4cExuWmhiSFZsSUQwZ2JtVjNJR0Z5WXpRdVZXbHVkRTR5TlRZb2RHOUNZV3d1Ym1GMGFYWmxJQ3NnWVcxdmRXNTBMbTVoZEdsMlpTa0tJQ0FnSUhOM1lYQUtJQ0FnSUdKdmVGOXdkWFFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TVRRd0NpQWdJQ0F2THlCamIyNTBjbTlzYkdWeU9pQnVaWGNnWVhKak5DNUJaR1J5WlhOektGUjRiaTV6Wlc1a1pYSXBMQW9nSUNBZ2RIaHVJRk5sYm1SbGNnb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6b3hNemt0TVRRM0NpQWdJQ0F2THlCdVpYY2dZWEpqTVRZME5GOWpiMjUwY205c2JHVnlYM1J5WVc1elptVnlYMlYyWlc1MEtIc0tJQ0FnSUM4dklDQWdZMjl1ZEhKdmJHeGxjam9nYm1WM0lHRnlZelF1UVdSa2NtVnpjeWhVZUc0dWMyVnVaR1Z5S1N3S0lDQWdJQzh2SUNBZ1puSnZiU3dLSUNBZ0lDOHZJQ0FnZEc4c0NpQWdJQ0F2THlBZ0lHRnRiM1Z1ZEN3S0lDQWdJQzh2SUNBZ1kyOWtaU3dLSUNBZ0lDOHZJQ0FnWkdGMFlTd0tJQ0FnSUM4dklDQWdiM0JsY21GMGIzSmZaR0YwWVN3S0lDQWdJQzh2SUgwcExBb2dJQ0FnWm5KaGJXVmZaR2xuSUMwMUNpQWdJQ0JqYjI1allYUUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE5Bb2dJQ0FnWTI5dVkyRjBDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUTUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem95TlFvZ0lDQWdMeThnWTI5dWMzUWdRMDlFUlY5VFZVTkRSVk5USUQwZ2JtVjNJR0Z5WXpRdVFubDBaU2d3ZURVeEtRb2dJQ0FnY0hWemFHSjVkR1Z6SURCNE5URUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZNVE01TFRFME53b2dJQ0FnTHk4Z2JtVjNJR0Z5WXpFMk5EUmZZMjl1ZEhKdmJHeGxjbDkwY21GdWMyWmxjbDlsZG1WdWRDaDdDaUFnSUNBdkx5QWdJR052Ym5SeWIyeHNaWEk2SUc1bGR5QmhjbU0wTGtGa1pISmxjM01vVkhodUxuTmxibVJsY2lrc0NpQWdJQ0F2THlBZ0lHWnliMjBzQ2lBZ0lDQXZMeUFnSUhSdkxBb2dJQ0FnTHk4Z0lDQmhiVzkxYm5Rc0NpQWdJQ0F2THlBZ0lHTnZaR1VzQ2lBZ0lDQXZMeUFnSUdSaGRHRXNDaUFnSUNBdkx5QWdJRzl3WlhKaGRHOXlYMlJoZEdFc0NpQWdJQ0F2THlCOUtTd0tJQ0FnSUdOdmJtTmhkQW9nSUNBZ2NIVnphR0o1ZEdWeklEQjRNREE0TlFvZ0lDQWdZMjl1WTJGMENpQWdJQ0JtY21GdFpWOWthV2NnTFRJS0lDQWdJR3hsYmdvZ0lDQWdjSFZ6YUdsdWRDQXhNek1nTHk4Z01UTXpDaUFnSUNBckNpQWdJQ0JwZEc5aUNpQWdJQ0JsZUhSeVlXTjBJRFlnTWdvZ0lDQWdZMjl1WTJGMENpQWdJQ0JtY21GdFpWOWthV2NnTFRJS0lDQWdJR052Ym1OaGRBb2dJQ0FnWm5KaGJXVmZaR2xuSUMweENpQWdJQ0JqYjI1allYUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZNVE0zTFRFME9Bb2dJQ0FnTHk4Z1pXMXBkQ2dLSUNBZ0lDOHZJQ0FnSjBOdmJuUnliMnhzWlhKVWNtRnVjMlpsY2ljc0NpQWdJQ0F2THlBZ0lHNWxkeUJoY21NeE5qUTBYMk52Ym5SeWIyeHNaWEpmZEhKaGJuTm1aWEpmWlhabGJuUW9ld29nSUNBZ0x5OGdJQ0FnSUdOdmJuUnliMnhzWlhJNklHNWxkeUJoY21NMExrRmtaSEpsYzNNb1ZIaHVMbk5sYm1SbGNpa3NDaUFnSUNBdkx5QWdJQ0FnWm5KdmJTd0tJQ0FnSUM4dklDQWdJQ0IwYnl3S0lDQWdJQzh2SUNBZ0lDQmhiVzkxYm5Rc0NpQWdJQ0F2THlBZ0lDQWdZMjlrWlN3S0lDQWdJQzh2SUNBZ0lDQmtZWFJoTEFvZ0lDQWdMeThnSUNBZ0lHOXdaWEpoZEc5eVgyUmhkR0VzQ2lBZ0lDQXZMeUFnSUgwcExBb2dJQ0FnTHk4Z0tRb2dJQ0FnWW5sMFpXTWdOaUF2THlBd2VEQXdNRElLSUNBZ0lITjNZWEFLSUNBZ0lHTnZibU5oZEFvZ0lDQWdjSFZ6YUdKNWRHVnpJREI0TXpRMlpXRTNPVFVnTHk4Z2JXVjBhRzlrSUNKRGIyNTBjbTlzYkdWeVZISmhibk5tWlhJb0tHRmtaSEpsYzNNc1lXUmtjbVZ6Y3l4aFpHUnlaWE56TEhWcGJuUXlOVFlzWW5sMFpTeGllWFJsVzEwc1lubDBaVnRkS1NraUNpQWdJQ0J6ZDJGd0NpQWdJQ0JqYjI1allYUUtJQ0FnSUd4dlp3b2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMk5EUXVZV3huYnk1MGN6b3hORGtLSUNBZ0lDOHZJSEpsZEhWeWJpQnVaWGNnWVhKak5DNVZhVzUwVGpZMEtHTnZaR1V1Ym1GMGFYWmxLUW9nSUNBZ2FXNTBZMTh6SUM4dklEZ3hDaUFnSUNCcGRHOWlDaUFnSUNCeVpYUnpkV0lLQ2dvdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UWTBOQzVoYkdkdkxuUnpPanBCY21NeE5qUTBMbUZ5WXpFMk5EUmZZMjl1ZEhKdmJHeGxjbDl5WldSbFpXMG9abkp2YlRvZ1lubDBaWE1zSUdGdGIzVnVkRG9nWW5sMFpYTXNJRzl3WlhKaGRHOXlYMlJoZEdFNklHSjVkR1Z6S1NBdFBpQmllWFJsY3pvS1lYSmpNVFkwTkY5amIyNTBjbTlzYkdWeVgzSmxaR1ZsYlRvS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk1UVXlMVEUxTndvZ0lDQWdMeThnUUdGeVl6UXVZV0pwYldWMGFHOWtLQ2tLSUNBZ0lDOHZJSEIxWW14cFl5QmhjbU14TmpRMFgyTnZiblJ5YjJ4c1pYSmZjbVZrWldWdEtBb2dJQ0FnTHk4Z0lDQm1jbTl0T2lCaGNtTTBMa0ZrWkhKbGMzTXNDaUFnSUNBdkx5QWdJR0Z0YjNWdWREb2dZWEpqTkM1VmFXNTBUakkxTml3S0lDQWdJQzh2SUNBZ2IzQmxjbUYwYjNKZlpHRjBZVG9nWVhKak5DNUVlVzVoYldsalFubDBaWE1zQ2lBZ0lDQXZMeUFwT2lCaGNtTTBMbFZwYm5ST05qUWdld29nSUNBZ2NISnZkRzhnTXlBeENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qRTFPQW9nSUNBZ0x5OGdkR2hwY3k1ZmIyNXNlVU52Ym5SeWIyeHNaWElvS1FvZ0lDQWdZMkZzYkhOMVlpQmZiMjVzZVVOdmJuUnliMnhzWlhJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk1UVTVDaUFnSUNBdkx5QjBhR2x6TGw5amFHVmphMHAxYzNScFptbGpZWFJwYjI0b2IzQmxjbUYwYjNKZlpHRjBZU2tLSUNBZ0lHWnlZVzFsWDJScFp5QXRNUW9nSUNBZ1kyRnNiSE4xWWlCZlkyaGxZMnRLZFhOMGFXWnBZMkYwYVc5dUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qRTJNQW9nSUNBZ0x5OGdkR2hwY3k1ZmNtRjBaVXhwYldsMEtDa0tJQ0FnSUdOaGJHeHpkV0lnWDNKaGRHVk1hVzFwZEFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pveE5qRUtJQ0FnSUM4dklHTnZibk4wSUdaeWIyMUNZV3dnUFNCMGFHbHpMbDlpWVd4aGJtTmxUMllvWm5KdmJTa0tJQ0FnSUdaeVlXMWxYMlJwWnlBdE13b2dJQ0FnWTJGc2JITjFZaUJmWW1Gc1lXNWpaVTltQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pFMk1nb2dJQ0FnTHk4Z1lYTnpaWEowS0daeWIyMUNZV3d1Ym1GMGFYWmxJRDQ5SUdGdGIzVnVkQzV1WVhScGRtVXNJQ2RwYm5OMVptWnBZMmxsYm5RbktRb2dJQ0FnWkhWd0NpQWdJQ0JtY21GdFpWOWthV2NnTFRJS0lDQWdJR0krUFFvZ0lDQWdZWE56WlhKMElDOHZJR2x1YzNWbVptbGphV1Z1ZEFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pveE5qTUtJQ0FnSUM4dklIUm9hWE11WW1Gc1lXNWpaWE1vWm5KdmJTa3VkbUZzZFdVZ1BTQnVaWGNnWVhKak5DNVZhVzUwVGpJMU5paG1jbTl0UW1Gc0xtNWhkR2wyWlNBdElHRnRiM1Z1ZEM1dVlYUnBkbVVwQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVElLSUNBZ0lHSXRDaUFnSUNCa2RYQUtJQ0FnSUd4bGJnb2dJQ0FnYVc1MFkxOHlJQzh2SURNeUNpQWdJQ0E4UFFvZ0lDQWdZWE56WlhKMElDOHZJRzkyWlhKbWJHOTNDaUFnSUNCcGJuUmpYeklnTHk4Z016SUtJQ0FnSUdKNlpYSnZDaUFnSUNCemQyRndDaUFnSUNCa2FXY2dNUW9nSUNBZ1lud0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeU1EQXVZV3huYnk1MGN6bzFNd29nSUNBZ0x5OGdjSFZpYkdsaklHSmhiR0Z1WTJWeklEMGdRbTk0VFdGd1BFRmtaSEpsYzNNc0lGVnBiblJPTWpVMlBpaDdJR3RsZVZCeVpXWnBlRG9nSjJJbklIMHBDaUFnSUNCaWVYUmxZeUEwSUM4dklDSmlJZ29nSUNBZ1puSmhiV1ZmWkdsbklDMHpDaUFnSUNCamIyNWpZWFFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOalEwTG1Gc1oyOHVkSE02TVRZekNpQWdJQ0F2THlCMGFHbHpMbUpoYkdGdVkyVnpLR1p5YjIwcExuWmhiSFZsSUQwZ2JtVjNJR0Z5WXpRdVZXbHVkRTR5TlRZb1puSnZiVUpoYkM1dVlYUnBkbVVnTFNCaGJXOTFiblF1Ym1GMGFYWmxLUW9nSUNBZ2MzZGhjQW9nSUNBZ1ltOTRYM0IxZEFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekl3TUM1aGJHZHZMblJ6T2pVeENpQWdJQ0F2THlCd2RXSnNhV01nZEc5MFlXeFRkWEJ3YkhrZ1BTQkhiRzlpWVd4VGRHRjBaVHhWYVc1MFRqSTFOajRvZXlCclpYazZJQ2QwSnlCOUtRb2dJQ0FnYVc1MFkxOHdJQzh2SURBS0lDQWdJR0o1ZEdWalh6TWdMeThnSW5RaUNpQWdJQ0JoY0hCZloyeHZZbUZzWDJkbGRGOWxlQW9nSUNBZ1lYTnpaWEowSUM4dklHTm9aV05ySUVkc2IySmhiRk4wWVhSbElHVjRhWE4wY3dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pveE5qUUtJQ0FnSUM4dklIUm9hWE11ZEc5MFlXeFRkWEJ3YkhrdWRtRnNkV1VnUFNCdVpYY2dZWEpqTkM1VmFXNTBUakkxTmloMGFHbHpMblJ2ZEdGc1UzVndjR3g1TG5aaGJIVmxMbTVoZEdsMlpTQXRJR0Z0YjNWdWRDNXVZWFJwZG1VcENpQWdJQ0JtY21GdFpWOWthV2NnTFRJS0lDQWdJR0l0Q2lBZ0lDQmtkWEFLSUNBZ0lHeGxiZ29nSUNBZ2FXNTBZMTh5SUM4dklETXlDaUFnSUNBOFBRb2dJQ0FnWVhOelpYSjBJQzh2SUc5MlpYSm1iRzkzQ2lBZ0lDQmlmQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPalV4Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdkRzkwWVd4VGRYQndiSGtnUFNCSGJHOWlZV3hUZEdGMFpUeFZhVzUwVGpJMU5qNG9leUJyWlhrNklDZDBKeUI5S1FvZ0lDQWdZbmwwWldOZk15QXZMeUFpZENJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TmpRMExtRnNaMjh1ZEhNNk1UWTBDaUFnSUNBdkx5QjBhR2x6TG5SdmRHRnNVM1Z3Y0d4NUxuWmhiSFZsSUQwZ2JtVjNJR0Z5WXpRdVZXbHVkRTR5TlRZb2RHaHBjeTUwYjNSaGJGTjFjSEJzZVM1MllXeDFaUzV1WVhScGRtVWdMU0JoYlc5MWJuUXVibUYwYVhabEtRb2dJQ0FnYzNkaGNBb2dJQ0FnWVhCd1gyZHNiMkpoYkY5d2RYUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5qUTBMbUZzWjI4dWRITTZNVFk1Q2lBZ0lDQXZMeUJqYjI1MGNtOXNiR1Z5T2lCdVpYY2dZWEpqTkM1QlpHUnlaWE56S0ZSNGJpNXpaVzVrWlhJcExBb2dJQ0FnZEhodUlGTmxibVJsY2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUyTkRRdVlXeG5ieTUwY3pveE5qZ3RNVGMwQ2lBZ0lDQXZMeUJ1WlhjZ1lYSmpNVFkwTkY5amIyNTBjbTlzYkdWeVgzSmxaR1ZsYlY5bGRtVnVkQ2g3Q2lBZ0lDQXZMeUFnSUdOdmJuUnliMnhzWlhJNklHNWxkeUJoY21NMExrRmtaSEpsYzNNb1ZIaHVMbk5sYm1SbGNpa3NDaUFnSUNBdkx5QWdJR1p5YjIwc0NpQWdJQ0F2THlBZ0lHRnRiM1Z1ZEN3S0lDQWdJQzh2SUNBZ1kyOWtaU3dLSUNBZ0lDOHZJQ0FnYjNCbGNtRjBiM0pmWkdGMFlTd0tJQ0FnSUM4dklIMHBMQW9nSUNBZ1puSmhiV1ZmWkdsbklDMHpDaUFnSUNCamIyNWpZWFFLSUNBZ0lHWnlZVzFsWDJScFp5QXRNZ29nSUNBZ1kyOXVZMkYwQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFkwTkM1aGJHZHZMblJ6T2pJMUNpQWdJQ0F2THlCamIyNXpkQ0JEVDBSRlgxTlZRME5GVTFNZ1BTQnVaWGNnWVhKak5DNUNlWFJsS0RCNE5URXBDaUFnSUNCd2RYTm9ZbmwwWlhNZ01IZzFNUW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTJORFF1WVd4bmJ5NTBjem94TmpndE1UYzBDaUFnSUNBdkx5QnVaWGNnWVhKak1UWTBORjlqYjI1MGNtOXNiR1Z5WDNKbFpHVmxiVjlsZG1WdWRDaDdDaUFnSUNBdkx5QWdJR052Ym5SeWIyeHNaWEk2SUc1bGR5QmhjbU0wTGtGa1pISmxjM01vVkhodUxuTmxibVJsY2lrc0NpQWdJQ0F2THlBZ0lHWnliMjBzQ2lBZ0lDQXZMeUFnSUdGdGIzVnVkQ3dLSUNBZ0lDOHZJQ0FnWTI5a1pTd0tJQ0FnSUM4dklDQWdiM0JsY21GMGIzSmZaR0YwWVN3S0lDQWdJQzh2SUgwcExBb2dJQ0FnWTI5dVkyRjBDaUFnSUNCd2RYTm9ZbmwwWlhNZ01IZ3dNRFl6Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR1p5WVcxbFgyUnBaeUF0TVFvZ0lDQWdZMjl1WTJGMENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qRTJOaTB4TnpVS0lDQWdJQzh2SUdWdGFYUW9DaUFnSUNBdkx5QWdJQ2REYjI1MGNtOXNiR1Z5VW1Wa1pXVnRKeXdLSUNBZ0lDOHZJQ0FnYm1WM0lHRnlZekUyTkRSZlkyOXVkSEp2Ykd4bGNsOXlaV1JsWlcxZlpYWmxiblFvZXdvZ0lDQWdMeThnSUNBZ0lHTnZiblJ5YjJ4c1pYSTZJRzVsZHlCaGNtTTBMa0ZrWkhKbGMzTW9WSGh1TG5ObGJtUmxjaWtzQ2lBZ0lDQXZMeUFnSUNBZ1puSnZiU3dLSUNBZ0lDOHZJQ0FnSUNCaGJXOTFiblFzQ2lBZ0lDQXZMeUFnSUNBZ1kyOWtaU3dLSUNBZ0lDOHZJQ0FnSUNCdmNHVnlZWFJ2Y2w5a1lYUmhMQW9nSUNBZ0x5OGdJQ0I5S1N3S0lDQWdJQzh2SUNrS0lDQWdJR0o1ZEdWaklEWWdMeThnTUhnd01EQXlDaUFnSUNCemQyRndDaUFnSUNCamIyNWpZWFFLSUNBZ0lIQjFjMmhpZVhSbGN5QXdlREJrWldVeE5HWTFJQzh2SUcxbGRHaHZaQ0FpUTI5dWRISnZiR3hsY2xKbFpHVmxiU2dvWVdSa2NtVnpjeXhoWkdSeVpYTnpMSFZwYm5ReU5UWXNZbmwwWlN4aWVYUmxXMTBwS1NJS0lDQWdJSE4zWVhBS0lDQWdJR052Ym1OaGRBb2dJQ0FnYkc5bkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRZME5DNWhiR2R2TG5Sek9qRTNOZ29nSUNBZ0x5OGdjbVYwZFhKdUlHNWxkeUJoY21NMExsVnBiblJPTmpRb1kyOWtaUzV1WVhScGRtVXBDaUFnSUNCcGJuUmpYek1nTHk4Z09ERUtJQ0FnSUdsMGIySUtJQ0FnSUhKbGRITjFZZ29LQ2k4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5UazBMbUZzWjI4dWRITTZPa0Z5WXpFMU9UUXVYMjl1YkhsUGQyNWxjaWdwSUMwK0lIWnZhV1E2Q25OdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TlRrMExtRnNaMjh1ZEhNNk9rRnlZekUxT1RRdVgyOXViSGxQZDI1bGNqb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5UazBMbUZzWjI4dWRITTZNak1LSUNBZ0lDOHZJR0Z6YzJWeWRDaDBhR2x6TG1GeVl6ZzRYMmx6WDI5M2JtVnlLRzVsZHlCaGNtTTBMa0ZrWkhKbGMzTW9WSGh1TG5ObGJtUmxjaWtwTG01aGRHbDJaU0E5UFQwZ2RISjFaU3dnSjI5dWJIbGZiM2R1WlhJbktRb2dJQ0FnZEhodUlGTmxibVJsY2dvZ0lDQWdZMkZzYkhOMVlpQmhjbU00T0Y5cGMxOXZkMjVsY2dvZ0lDQWdhVzUwWTE4d0lDOHZJREFLSUNBZ0lHZGxkR0pwZEFvZ0lDQWdhVzUwWTE4eElDOHZJREVLSUNBZ0lEMDlDaUFnSUNCaGMzTmxjblFnTHk4Z2IyNXNlVjl2ZDI1bGNnb2dJQ0FnY21WMGMzVmlDZ29LTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMU9UUXVZV3huYnk1MGN6bzZRWEpqTVRVNU5DNWhjbU14TlRrMFgzTmxkRjlwYzNOMVlXSnNaU2htYkdGbk9pQmllWFJsY3lrZ0xUNGdkbTlwWkRvS1lYSmpNVFU1TkY5elpYUmZhWE56ZFdGaWJHVTZDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UVTVOQzVoYkdkdkxuUnpPakk0TFRJNUNpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvS1FvZ0lDQWdMeThnY0hWaWJHbGpJR0Z5WXpFMU9UUmZjMlYwWDJsemMzVmhZbXhsS0dac1lXYzZJR0Z5WXpRdVFtOXZiQ2s2SUhadmFXUWdld29nSUNBZ2NISnZkRzhnTVNBd0NpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRVNU5DNWhiR2R2TG5Sek9qTXdDaUFnSUNBdkx5QjBhR2x6TGw5dmJteDVUM2R1WlhJb0tRb2dJQ0FnWTJGc2JITjFZaUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFU1TkM1aGJHZHZMblJ6T2pwQmNtTXhOVGswTGw5dmJteDVUM2R1WlhJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TlRrMExtRnNaMjh1ZEhNNk1UVUtJQ0FnSUM4dklIQjFZbXhwWXlCcGMzTjFZV0pzWlNBOUlFZHNiMkpoYkZOMFlYUmxQR0Z5WXpRdVFtOXZiRDRvZXlCclpYazZJQ2RwYzNNbklIMHBJQzh2SUZSeWRXVWdQU0JwYzNOMVlXSnNaUW9nSUNBZ1lubDBaV01nTVRVZ0x5OGdJbWx6Y3lJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TlRrMExtRnNaMjh1ZEhNNk16RUtJQ0FnSUM4dklIUm9hWE11YVhOemRXRmliR1V1ZG1Gc2RXVWdQU0JtYkdGbkNpQWdJQ0JtY21GdFpWOWthV2NnTFRFS0lDQWdJR0Z3Y0Y5bmJHOWlZV3hmY0hWMENpQWdJQ0J5WlhSemRXSUtDZ292THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRVNU5DNWhiR2R2TG5Sek9qcEJjbU14TlRrMExtRnlZekUxT1RSZmFYTnpkV1VvZEc4NklHSjVkR1Z6TENCaGJXOTFiblE2SUdKNWRHVnpMQ0JrWVhSaE9pQmllWFJsY3lrZ0xUNGdkbTlwWkRvS1lYSmpNVFU1TkY5cGMzTjFaVG9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOVGswTG1Gc1oyOHVkSE02TXpVdE16WUtJQ0FnSUM4dklFQmhjbU0wTG1GaWFXMWxkR2h2WkNncENpQWdJQ0F2THlCd2RXSnNhV01nWVhKak1UVTVORjlwYzNOMVpTaDBiem9nWVhKak5DNUJaR1J5WlhOekxDQmhiVzkxYm5RNklHRnlZelF1VldsdWRFNHlOVFlzSUdSaGRHRTZJR0Z5WXpRdVJIbHVZVzFwWTBKNWRHVnpLVG9nZG05cFpDQjdDaUFnSUNCd2NtOTBieUF6SURBS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TlRrMExtRnNaMjh1ZEhNNk16Y0tJQ0FnSUM4dklIUm9hWE11WDI5dWJIbFBkMjVsY2lncENpQWdJQ0JqWVd4c2MzVmlJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOVGswTG1Gc1oyOHVkSE02T2tGeVl6RTFPVFF1WDI5dWJIbFBkMjVsY2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUxT1RRdVlXeG5ieTUwY3pvek9Bb2dJQ0FnTHk4Z1lYTnpaWEowS0dGdGIzVnVkQzV1WVhScGRtVWdQaUF3Yml3Z0oybHVkbUZzYVdSZllXMXZkVzUwSnlrS0lDQWdJR1p5WVcxbFgyUnBaeUF0TWdvZ0lDQWdjSFZ6YUdKNWRHVnpJREI0Q2lBZ0lDQmlQZ29nSUNBZ1lYTnpaWEowSUM4dklHbHVkbUZzYVdSZllXMXZkVzUwQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFU1TkM1aGJHZHZMblJ6T2pFMUNpQWdJQ0F2THlCd2RXSnNhV01nYVhOemRXRmliR1VnUFNCSGJHOWlZV3hUZEdGMFpUeGhjbU0wTGtKdmIydytLSHNnYTJWNU9pQW5hWE56SnlCOUtTQXZMeUJVY25WbElEMGdhWE56ZFdGaWJHVUtJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0JpZVhSbFl5QXhOU0F2THlBaWFYTnpJZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTFPVFF1WVd4bmJ5NTBjem96T1FvZ0lDQWdMeThnWVhOelpYSjBLSFJvYVhNdWFYTnpkV0ZpYkdVdWFHRnpWbUZzZFdVZ0ppWWdkR2hwY3k1cGMzTjFZV0pzWlM1MllXeDFaUzV1WVhScGRtVWdQVDA5SUhSeWRXVXNJQ2RwYzNOMVlXNWpaVjlrYVhOaFlteGxaQ2NwQ2lBZ0lDQmhjSEJmWjJ4dlltRnNYMmRsZEY5bGVBb2dJQ0FnWW5WeWVTQXhDaUFnSUNCaWVpQmhjbU14TlRrMFgybHpjM1ZsWDJKdmIyeGZabUZzYzJWQU13b2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMU9UUXVZV3huYnk1MGN6b3hOUW9nSUNBZ0x5OGdjSFZpYkdsaklHbHpjM1ZoWW14bElEMGdSMnh2WW1Gc1UzUmhkR1U4WVhKak5DNUNiMjlzUGloN0lHdGxlVG9nSjJsemN5Y2dmU2tnTHk4Z1ZISjFaU0E5SUdsemMzVmhZbXhsQ2lBZ0lDQnBiblJqWHpBZ0x5OGdNQW9nSUNBZ1lubDBaV01nTVRVZ0x5OGdJbWx6Y3lJS0lDQWdJR0Z3Y0Y5bmJHOWlZV3hmWjJWMFgyVjRDaUFnSUNCaGMzTmxjblFnTHk4Z1kyaGxZMnNnUjJ4dlltRnNVM1JoZEdVZ1pYaHBjM1J6Q2lBZ0lDQnBiblJqWHpBZ0x5OGdNQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTFPVFF1WVd4bmJ5NTBjem96T1FvZ0lDQWdMeThnWVhOelpYSjBLSFJvYVhNdWFYTnpkV0ZpYkdVdWFHRnpWbUZzZFdVZ0ppWWdkR2hwY3k1cGMzTjFZV0pzWlM1MllXeDFaUzV1WVhScGRtVWdQVDA5SUhSeWRXVXNJQ2RwYzNOMVlXNWpaVjlrYVhOaFlteGxaQ2NwQ2lBZ0lDQm5aWFJpYVhRS0lDQWdJR2x1ZEdOZk1TQXZMeUF4Q2lBZ0lDQTlQUW9nSUNBZ1lub2dZWEpqTVRVNU5GOXBjM04xWlY5aWIyOXNYMlpoYkhObFFETUtJQ0FnSUdsdWRHTmZNU0F2THlBeENncGhjbU14TlRrMFgybHpjM1ZsWDJKdmIyeGZiV1Z5WjJWQU5Eb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5UazBMbUZzWjI4dWRITTZNemtLSUNBZ0lDOHZJR0Z6YzJWeWRDaDBhR2x6TG1semMzVmhZbXhsTG1oaGMxWmhiSFZsSUNZbUlIUm9hWE11YVhOemRXRmliR1V1ZG1Gc2RXVXVibUYwYVhabElEMDlQU0IwY25WbExDQW5hWE56ZFdGdVkyVmZaR2x6WVdKc1pXUW5LUW9nSUNBZ1lYTnpaWEowSUM4dklHbHpjM1ZoYm1ObFgyUnBjMkZpYkdWa0NpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRVNU5DNWhiR2R2TG5Sek9qUXhDaUFnSUNBdkx5QjBhR2x6TG1GeVl6RTBNVEJmYVhOemRXVmZZbmxmY0dGeWRHbDBhVzl1S0hSdkxDQnVaWGNnWVhKak5DNUJaR1J5WlhOektDa3NJR0Z0YjNWdWRDd2daR0YwWVNrS0lDQWdJR1p5WVcxbFgyUnBaeUF0TXdvZ0lDQWdZbmwwWldOZk1TQXZMeUJoWkdSeUlFRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGWk5VaEdTMUVLSUNBZ0lHWnlZVzFsWDJScFp5QXRNZ29nSUNBZ1puSmhiV1ZmWkdsbklDMHhDaUFnSUNCallXeHNjM1ZpSUdGeVl6RTBNVEJmYVhOemRXVmZZbmxmY0dGeWRHbDBhVzl1Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFU1TkM1aGJHZHZMblJ6T2pReUNpQWdJQ0F2THlCbGJXbDBLQ2RKYzNOMVpTY3NJRzVsZHlCaGNtTXhOVGswWDJsemMzVmxYMlYyWlc1MEtIc2dkRzhzSUdGdGIzVnVkQ3dnWkdGMFlTQjlLU2tLSUNBZ0lHWnlZVzFsWDJScFp5QXRNd29nSUNBZ1puSmhiV1ZmWkdsbklDMHlDaUFnSUNCamIyNWpZWFFLSUNBZ0lHSjVkR1ZqSURJeElDOHZJREI0TURBME1nb2dJQ0FnWTI5dVkyRjBDaUFnSUNCbWNtRnRaVjlrYVdjZ0xURUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ1lubDBaV01nTmlBdkx5QXdlREF3TURJS0lDQWdJSE4zWVhBS0lDQWdJR052Ym1OaGRBb2dJQ0FnY0hWemFHSjVkR1Z6SURCNFpqSmxPVGs0WVdZZ0x5OGdiV1YwYUc5a0lDSkpjM04xWlNnb1lXUmtjbVZ6Y3l4MWFXNTBNalUyTEdKNWRHVmJYU2twSWdvZ0lDQWdjM2RoY0FvZ0lDQWdZMjl1WTJGMENpQWdJQ0JzYjJjS0lDQWdJSEpsZEhOMVlnb0tZWEpqTVRVNU5GOXBjM04xWlY5aWIyOXNYMlpoYkhObFFETTZDaUFnSUNCcGJuUmpYekFnTHk4Z01Bb2dJQ0FnWWlCaGNtTXhOVGswWDJsemMzVmxYMkp2YjJ4ZmJXVnlaMlZBTkFvS0NpOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOVGswTG1Gc1oyOHVkSE02T2tGeVl6RTFPVFF1WVhKak1UVTVORjl5WldSbFpXMUdjbTl0S0daeWIyMDZJR0o1ZEdWekxDQmhiVzkxYm5RNklHSjVkR1Z6TENCa1lYUmhPaUJpZVhSbGN5a2dMVDRnZG05cFpEb0tZWEpqTVRVNU5GOXlaV1JsWlcxR2NtOXRPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTFPVFF1WVd4bmJ5NTBjem8wTlMwME5nb2dJQ0FnTHk4Z1FHRnlZelF1WVdKcGJXVjBhRzlrS0NrS0lDQWdJQzh2SUhCMVlteHBZeUJoY21NeE5UazBYM0psWkdWbGJVWnliMjBvWm5KdmJUb2dZWEpqTkM1QlpHUnlaWE56TENCaGJXOTFiblE2SUdGeVl6UXVWV2x1ZEU0eU5UWXNJR1JoZEdFNklHRnlZelF1UkhsdVlXMXBZMEo1ZEdWektUb2dkbTlwWkNCN0NpQWdJQ0J3Y205MGJ5QXpJREFLSUNBZ0lHbHVkR05mTUNBdkx5QXdDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UVTVOQzVoYkdkdkxuUnpPalEzQ2lBZ0lDQXZMeUJqYjI1emRDQnpaVzVrWlhJZ1BTQnVaWGNnWVhKak5DNUJaR1J5WlhOektGUjRiaTV6Wlc1a1pYSXBDaUFnSUNCMGVHNGdVMlZ1WkdWeUNpQWdJQ0JrZFhBS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TlRrMExtRnNaMjh1ZEhNNk5EZ0tJQ0FnSUM4dklHRnpjMlZ5ZENoelpXNWtaWElnUFQwOUlHWnliMjBnZkh3Z2RHaHBjeTVoY21NNE9GOXBjMTl2ZDI1bGNpaHpaVzVrWlhJcExtNWhkR2wyWlNBOVBUMGdkSEoxWlN3Z0oyNXZkRjloZFhSb0p5a0tJQ0FnSUdaeVlXMWxYMlJwWnlBdE13b2dJQ0FnUFQwS0lDQWdJR0p1ZWlCaGNtTXhOVGswWDNKbFpHVmxiVVp5YjIxZlltOXZiRjkwY25WbFFESUtJQ0FnSUdaeVlXMWxYMlJwWnlBeENpQWdJQ0JqWVd4c2MzVmlJR0Z5WXpnNFgybHpYMjkzYm1WeUNpQWdJQ0JwYm5Salh6QWdMeThnTUFvZ0lDQWdaMlYwWW1sMENpQWdJQ0JwYm5Salh6RWdMeThnTVFvZ0lDQWdQVDBLSUNBZ0lHSjZJR0Z5WXpFMU9UUmZjbVZrWldWdFJuSnZiVjlpYjI5c1gyWmhiSE5sUURNS0NtRnlZekUxT1RSZmNtVmtaV1Z0Um5KdmJWOWliMjlzWDNSeWRXVkFNam9LSUNBZ0lHbHVkR05mTVNBdkx5QXhDZ3BoY21NeE5UazBYM0psWkdWbGJVWnliMjFmWW05dmJGOXRaWEpuWlVBME9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMU9UUXVZV3huYnk1MGN6bzBPQW9nSUNBZ0x5OGdZWE56WlhKMEtITmxibVJsY2lBOVBUMGdabkp2YlNCOGZDQjBhR2x6TG1GeVl6ZzRYMmx6WDI5M2JtVnlLSE5sYm1SbGNpa3VibUYwYVhabElEMDlQU0IwY25WbExDQW5ibTkwWDJGMWRHZ25LUW9nSUNBZ1lYTnpaWEowSUM4dklHNXZkRjloZFhSb0NpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRVNU5DNWhiR2R2TG5Sek9qUTVDaUFnSUNBdkx5QmhjM05sY25Rb1lXMXZkVzUwTG01aGRHbDJaU0ErSURCdUxDQW5hVzUyWVd4cFpGOWhiVzkxYm5RbktRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0J3ZFhOb1lubDBaWE1nTUhnS0lDQWdJR0krQ2lBZ0lDQmhjM05sY25RZ0x5OGdhVzUyWVd4cFpGOWhiVzkxYm5RS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pvMU13b2dJQ0FnTHk4Z2NIVmliR2xqSUdKaGJHRnVZMlZ6SUQwZ1FtOTRUV0Z3UEVGa1pISmxjM01zSUZWcGJuUk9NalUyUGloN0lHdGxlVkJ5WldacGVEb2dKMkluSUgwcENpQWdJQ0JpZVhSbFl5QTBJQzh2SUNKaUlnb2dJQ0FnWm5KaGJXVmZaR2xuSUMwekNpQWdJQ0JqYjI1allYUUtJQ0FnSUdSMWNBb2dJQ0FnWm5KaGJXVmZZblZ5ZVNBd0NpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRVNU5DNWhiR2R2TG5Sek9qVXdDaUFnSUNBdkx5QmhjM05sY25Rb2RHaHBjeTVpWVd4aGJtTmxjeWhtY205dEtTNWxlR2x6ZEhNZ0ppWWdkR2hwY3k1aVlXeGhibU5sY3lobWNtOXRLUzUyWVd4MVpTNXVZWFJwZG1VZ1BqMGdZVzF2ZFc1MExtNWhkR2wyWlN3Z0oybHVjM1ZtWm1samFXVnVkRjlpWVd4aGJtTmxKeWtLSUNBZ0lHSnZlRjlzWlc0S0lDQWdJR0oxY25rZ01Rb2dJQ0FnWW5vZ1lYSmpNVFU1TkY5eVpXUmxaVzFHY205dFgySnZiMnhmWm1Gc2MyVkFOd29nSUNBZ1puSmhiV1ZmWkdsbklEQUtJQ0FnSUdKdmVGOW5aWFFLSUNBZ0lHRnpjMlZ5ZENBdkx5QkNiM2dnYlhWemRDQm9ZWFpsSUhaaGJIVmxDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUSUtJQ0FnSUdJK1BRb2dJQ0FnWW5vZ1lYSmpNVFU1TkY5eVpXUmxaVzFHY205dFgySnZiMnhmWm1Gc2MyVkFOd29nSUNBZ2FXNTBZMTh4SUM4dklERUtDbUZ5WXpFMU9UUmZjbVZrWldWdFJuSnZiVjlpYjI5c1gyMWxjbWRsUURnNkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRVNU5DNWhiR2R2TG5Sek9qVXdDaUFnSUNBdkx5QmhjM05sY25Rb2RHaHBjeTVpWVd4aGJtTmxjeWhtY205dEtTNWxlR2x6ZEhNZ0ppWWdkR2hwY3k1aVlXeGhibU5sY3lobWNtOXRLUzUyWVd4MVpTNXVZWFJwZG1VZ1BqMGdZVzF2ZFc1MExtNWhkR2wyWlN3Z0oybHVjM1ZtWm1samFXVnVkRjlpWVd4aGJtTmxKeWtLSUNBZ0lHRnpjMlZ5ZENBdkx5QnBibk4xWm1acFkybGxiblJmWW1Gc1lXNWpaUW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTFPVFF1WVd4bmJ5NTBjem8xTVFvZ0lDQWdMeThnZEdocGN5NWlZV3hoYm1ObGN5aG1jbTl0S1M1MllXeDFaU0E5SUc1bGR5QmhjbU0wTGxWcGJuUk9NalUyS0hSb2FYTXVZbUZzWVc1alpYTW9abkp2YlNrdWRtRnNkV1V1Ym1GMGFYWmxJQzBnWVcxdmRXNTBMbTVoZEdsMlpTa0tJQ0FnSUdaeVlXMWxYMlJwWnlBd0NpQWdJQ0JrZFhBS0lDQWdJR0p2ZUY5blpYUUtJQ0FnSUdGemMyVnlkQ0F2THlCQ2IzZ2diWFZ6ZENCb1lYWmxJSFpoYkhWbENpQWdJQ0JtY21GdFpWOWthV2NnTFRJS0lDQWdJR0l0Q2lBZ0lDQmtkWEFLSUNBZ0lHeGxiZ29nSUNBZ2FXNTBZMTh5SUM4dklETXlDaUFnSUNBOFBRb2dJQ0FnWVhOelpYSjBJQzh2SUc5MlpYSm1iRzkzQ2lBZ0lDQnBiblJqWHpJZ0x5OGdNeklLSUNBZ0lHSjZaWEp2Q2lBZ0lDQnpkMkZ3Q2lBZ0lDQmthV2NnTVFvZ0lDQWdZbndLSUNBZ0lIVnVZMjkyWlhJZ01nb2dJQ0FnYzNkaGNBb2dJQ0FnWW05NFgzQjFkQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPalV4Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdkRzkwWVd4VGRYQndiSGtnUFNCSGJHOWlZV3hUZEdGMFpUeFZhVzUwVGpJMU5qNG9leUJyWlhrNklDZDBKeUI5S1FvZ0lDQWdhVzUwWTE4d0lDOHZJREFLSUNBZ0lHSjVkR1ZqWHpNZ0x5OGdJblFpQ2lBZ0lDQmhjSEJmWjJ4dlltRnNYMmRsZEY5bGVBb2dJQ0FnWVhOelpYSjBJQzh2SUdOb1pXTnJJRWRzYjJKaGJGTjBZWFJsSUdWNGFYTjBjd29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTFPVFF1WVd4bmJ5NTBjem8xTWdvZ0lDQWdMeThnZEdocGN5NTBiM1JoYkZOMWNIQnNlUzUyWVd4MVpTQTlJRzVsZHlCaGNtTTBMbFZwYm5ST01qVTJLSFJvYVhNdWRHOTBZV3hUZFhCd2JIa3VkbUZzZFdVdWJtRjBhWFpsSUMwZ1lXMXZkVzUwTG01aGRHbDJaU2tLSUNBZ0lHWnlZVzFsWDJScFp5QXRNZ29nSUNBZ1lpMEtJQ0FnSUdSMWNBb2dJQ0FnYkdWdUNpQWdJQ0JwYm5Salh6SWdMeThnTXpJS0lDQWdJRHc5Q2lBZ0lDQmhjM05sY25RZ0x5OGdiM1psY21ac2IzY0tJQ0FnSUdKOENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk5URUtJQ0FnSUM4dklIQjFZbXhwWXlCMGIzUmhiRk4xY0hCc2VTQTlJRWRzYjJKaGJGTjBZWFJsUEZWcGJuUk9NalUyUGloN0lHdGxlVG9nSjNRbklIMHBDaUFnSUNCaWVYUmxZMTh6SUM4dklDSjBJZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTFPVFF1WVd4bmJ5NTBjem8xTWdvZ0lDQWdMeThnZEdocGN5NTBiM1JoYkZOMWNIQnNlUzUyWVd4MVpTQTlJRzVsZHlCaGNtTTBMbFZwYm5ST01qVTJLSFJvYVhNdWRHOTBZV3hUZFhCd2JIa3VkbUZzZFdVdWJtRjBhWFpsSUMwZ1lXMXZkVzUwTG01aGRHbDJaU2tLSUNBZ0lITjNZWEFLSUNBZ0lHRndjRjluYkc5aVlXeGZjSFYwQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFU1TkM1aGJHZHZMblJ6T2pVekNpQWdJQ0F2THlCbGJXbDBLQ2RTWldSbFpXMG5MQ0J1WlhjZ1lYSmpNVFU1TkY5eVpXUmxaVzFmWlhabGJuUW9leUJtY205dExDQmhiVzkxYm5Rc0lHUmhkR0VnZlNrcENpQWdJQ0JtY21GdFpWOWthV2NnTFRNS0lDQWdJR1p5WVcxbFgyUnBaeUF0TWdvZ0lDQWdZMjl1WTJGMENpQWdJQ0JpZVhSbFl5QXlNU0F2THlBd2VEQXdORElLSUNBZ0lHTnZibU5oZEFvZ0lDQWdabkpoYldWZlpHbG5JQzB4Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR0o1ZEdWaklEWWdMeThnTUhnd01EQXlDaUFnSUNCemQyRndDaUFnSUNCamIyNWpZWFFLSUNBZ0lHSjVkR1ZqSURJMUlDOHZJRzFsZEdodlpDQWlVbVZrWldWdEtDaGhaR1J5WlhOekxIVnBiblF5TlRZc1lubDBaVnRkS1NraUNpQWdJQ0J6ZDJGd0NpQWdJQ0JqYjI1allYUUtJQ0FnSUd4dlp3b2dJQ0FnY21WMGMzVmlDZ3BoY21NeE5UazBYM0psWkdWbGJVWnliMjFmWW05dmJGOW1ZV3h6WlVBM09nb2dJQ0FnYVc1MFkxOHdJQzh2SURBS0lDQWdJR0lnWVhKak1UVTVORjl5WldSbFpXMUdjbTl0WDJKdmIyeGZiV1Z5WjJWQU9Bb0tZWEpqTVRVNU5GOXlaV1JsWlcxR2NtOXRYMkp2YjJ4ZlptRnNjMlZBTXpvS0lDQWdJR2x1ZEdOZk1DQXZMeUF3Q2lBZ0lDQmlJR0Z5WXpFMU9UUmZjbVZrWldWdFJuSnZiVjlpYjI5c1gyMWxjbWRsUURRS0Nnb3ZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFU1TkM1aGJHZHZMblJ6T2pwQmNtTXhOVGswTG1GeVl6RTFPVFJmY21Wa1pXVnRLR0Z0YjNWdWREb2dZbmwwWlhNc0lHUmhkR0U2SUdKNWRHVnpLU0F0UGlCMmIybGtPZ3BoY21NeE5UazBYM0psWkdWbGJUb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5UazBMbUZzWjI4dWRITTZOVFl0TlRjS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2dwQ2lBZ0lDQXZMeUJ3ZFdKc2FXTWdZWEpqTVRVNU5GOXlaV1JsWlcwb1lXMXZkVzUwT2lCaGNtTTBMbFZwYm5ST01qVTJMQ0JrWVhSaE9pQmhjbU0wTGtSNWJtRnRhV05DZVhSbGN5azZJSFp2YVdRZ2V3b2dJQ0FnY0hKdmRHOGdNaUF3Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFU1TkM1aGJHZHZMblJ6T2pVNENpQWdJQ0F2THlCamIyNXpkQ0JtY205dElEMGdibVYzSUdGeVl6UXVRV1JrY21WemN5aFVlRzR1YzJWdVpHVnlLUW9nSUNBZ2RIaHVJRk5sYm1SbGNnb2dJQ0FnWkhWd0NpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRVNU5DNWhiR2R2TG5Sek9qVTVDaUFnSUNBdkx5QmhjM05sY25Rb1lXMXZkVzUwTG01aGRHbDJaU0ErSURCdUxDQW5hVzUyWVd4cFpGOWhiVzkxYm5RbktRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0J3ZFhOb1lubDBaWE1nTUhnS0lDQWdJR0krQ2lBZ0lDQmhjM05sY25RZ0x5OGdhVzUyWVd4cFpGOWhiVzkxYm5RS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pvMU13b2dJQ0FnTHk4Z2NIVmliR2xqSUdKaGJHRnVZMlZ6SUQwZ1FtOTRUV0Z3UEVGa1pISmxjM01zSUZWcGJuUk9NalUyUGloN0lHdGxlVkJ5WldacGVEb2dKMkluSUgwcENpQWdJQ0JpZVhSbFl5QTBJQzh2SUNKaUlnb2dJQ0FnYzNkaGNBb2dJQ0FnWTI5dVkyRjBDaUFnSUNCa2RYQUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5UazBMbUZzWjI4dWRITTZOakFLSUNBZ0lDOHZJR0Z6YzJWeWRDaDBhR2x6TG1KaGJHRnVZMlZ6S0daeWIyMHBMbVY0YVhOMGN5QW1KaUIwYUdsekxtSmhiR0Z1WTJWektHWnliMjBwTG5aaGJIVmxMbTVoZEdsMlpTQStQU0JoYlc5MWJuUXVibUYwYVhabExDQW5hVzV6ZFdabWFXTnBaVzUwWDJKaGJHRnVZMlVuS1FvZ0lDQWdZbTk0WDJ4bGJnb2dJQ0FnWW5WeWVTQXhDaUFnSUNCaWVpQmhjbU14TlRrMFgzSmxaR1ZsYlY5aWIyOXNYMlpoYkhObFFETUtJQ0FnSUdaeVlXMWxYMlJwWnlBeENpQWdJQ0JpYjNoZloyVjBDaUFnSUNCaGMzTmxjblFnTHk4Z1FtOTRJRzExYzNRZ2FHRjJaU0IyWVd4MVpRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0JpUGowS0lDQWdJR0o2SUdGeVl6RTFPVFJmY21Wa1pXVnRYMkp2YjJ4ZlptRnNjMlZBTXdvZ0lDQWdhVzUwWTE4eElDOHZJREVLQ21GeVl6RTFPVFJmY21Wa1pXVnRYMkp2YjJ4ZmJXVnlaMlZBTkRvS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TlRrMExtRnNaMjh1ZEhNNk5qQUtJQ0FnSUM4dklHRnpjMlZ5ZENoMGFHbHpMbUpoYkdGdVkyVnpLR1p5YjIwcExtVjRhWE4wY3lBbUppQjBhR2x6TG1KaGJHRnVZMlZ6S0daeWIyMHBMblpoYkhWbExtNWhkR2wyWlNBK1BTQmhiVzkxYm5RdWJtRjBhWFpsTENBbmFXNXpkV1ptYVdOcFpXNTBYMkpoYkdGdVkyVW5LUW9nSUNBZ1lYTnpaWEowSUM4dklHbHVjM1ZtWm1samFXVnVkRjlpWVd4aGJtTmxDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UVTVOQzVoYkdkdkxuUnpPall4Q2lBZ0lDQXZMeUIwYUdsekxtSmhiR0Z1WTJWektHWnliMjBwTG5aaGJIVmxJRDBnYm1WM0lHRnlZelF1VldsdWRFNHlOVFlvZEdocGN5NWlZV3hoYm1ObGN5aG1jbTl0S1M1MllXeDFaUzV1WVhScGRtVWdMU0JoYlc5MWJuUXVibUYwYVhabEtRb2dJQ0FnWm5KaGJXVmZaR2xuSURFS0lDQWdJR1IxY0FvZ0lDQWdZbTk0WDJkbGRBb2dJQ0FnWVhOelpYSjBJQzh2SUVKdmVDQnRkWE4wSUdoaGRtVWdkbUZzZFdVS0lDQWdJR1p5WVcxbFgyUnBaeUF0TWdvZ0lDQWdZaTBLSUNBZ0lHUjFjQW9nSUNBZ2JHVnVDaUFnSUNCcGJuUmpYeklnTHk4Z016SUtJQ0FnSUR3OUNpQWdJQ0JoYzNObGNuUWdMeThnYjNabGNtWnNiM2NLSUNBZ0lHbHVkR05mTWlBdkx5QXpNZ29nSUNBZ1lucGxjbThLSUNBZ0lITjNZWEFLSUNBZ0lHUnBaeUF4Q2lBZ0lDQmlmQW9nSUNBZ2RXNWpiM1psY2lBeUNpQWdJQ0J6ZDJGd0NpQWdJQ0JpYjNoZmNIVjBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1qQXdMbUZzWjI4dWRITTZOVEVLSUNBZ0lDOHZJSEIxWW14cFl5QjBiM1JoYkZOMWNIQnNlU0E5SUVkc2IySmhiRk4wWVhSbFBGVnBiblJPTWpVMlBpaDdJR3RsZVRvZ0ozUW5JSDBwQ2lBZ0lDQnBiblJqWHpBZ0x5OGdNQW9nSUNBZ1lubDBaV05mTXlBdkx5QWlkQ0lLSUNBZ0lHRndjRjluYkc5aVlXeGZaMlYwWDJWNENpQWdJQ0JoYzNObGNuUWdMeThnWTJobFkyc2dSMnh2WW1Gc1UzUmhkR1VnWlhocGMzUnpDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UVTVOQzVoYkdkdkxuUnpPall5Q2lBZ0lDQXZMeUIwYUdsekxuUnZkR0ZzVTNWd2NHeDVMblpoYkhWbElEMGdibVYzSUdGeVl6UXVWV2x1ZEU0eU5UWW9kR2hwY3k1MGIzUmhiRk4xY0hCc2VTNTJZV3gxWlM1dVlYUnBkbVVnTFNCaGJXOTFiblF1Ym1GMGFYWmxLUW9nSUNBZ1puSmhiV1ZmWkdsbklDMHlDaUFnSUNCaUxRb2dJQ0FnWkhWd0NpQWdJQ0JzWlc0S0lDQWdJR2x1ZEdOZk1pQXZMeUF6TWdvZ0lDQWdQRDBLSUNBZ0lHRnpjMlZ5ZENBdkx5QnZkbVZ5Wm14dmR3b2dJQ0FnWW53S0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pvMU1Rb2dJQ0FnTHk4Z2NIVmliR2xqSUhSdmRHRnNVM1Z3Y0d4NUlEMGdSMnh2WW1Gc1UzUmhkR1U4VldsdWRFNHlOVFkrS0hzZ2EyVjVPaUFuZENjZ2ZTa0tJQ0FnSUdKNWRHVmpYek1nTHk4Z0luUWlDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UVTVOQzVoYkdkdkxuUnpPall5Q2lBZ0lDQXZMeUIwYUdsekxuUnZkR0ZzVTNWd2NHeDVMblpoYkhWbElEMGdibVYzSUdGeVl6UXVWV2x1ZEU0eU5UWW9kR2hwY3k1MGIzUmhiRk4xY0hCc2VTNTJZV3gxWlM1dVlYUnBkbVVnTFNCaGJXOTFiblF1Ym1GMGFYWmxLUW9nSUNBZ2MzZGhjQW9nSUNBZ1lYQndYMmRzYjJKaGJGOXdkWFFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOVGswTG1Gc1oyOHVkSE02TmpNS0lDQWdJQzh2SUdWdGFYUW9KMUpsWkdWbGJTY3NJRzVsZHlCaGNtTXhOVGswWDNKbFpHVmxiVjlsZG1WdWRDaDdJR1p5YjIwc0lHRnRiM1Z1ZEN3Z1pHRjBZU0I5S1NrS0lDQWdJR1p5WVcxbFgyUnBaeUF3Q2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVElLSUNBZ0lHTnZibU5oZEFvZ0lDQWdZbmwwWldNZ01qRWdMeThnTUhnd01EUXlDaUFnSUNCamIyNWpZWFFLSUNBZ0lHWnlZVzFsWDJScFp5QXRNUW9nSUNBZ1kyOXVZMkYwQ2lBZ0lDQmllWFJsWXlBMklDOHZJREI0TURBd01nb2dJQ0FnYzNkaGNBb2dJQ0FnWTI5dVkyRjBDaUFnSUNCaWVYUmxZeUF5TlNBdkx5QnRaWFJvYjJRZ0lsSmxaR1ZsYlNnb1lXUmtjbVZ6Y3l4MWFXNTBNalUyTEdKNWRHVmJYU2twSWdvZ0lDQWdjM2RoY0FvZ0lDQWdZMjl1WTJGMENpQWdJQ0JzYjJjS0lDQWdJSEpsZEhOMVlnb0tZWEpqTVRVNU5GOXlaV1JsWlcxZlltOXZiRjltWVd4elpVQXpPZ29nSUNBZ2FXNTBZMTh3SUM4dklEQUtJQ0FnSUdJZ1lYSmpNVFU1TkY5eVpXUmxaVzFmWW05dmJGOXRaWEpuWlVBMENnb0tMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUxT1RRdVlXeG5ieTUwY3pvNlFYSmpNVFU1TkM1aGNtTXhOVGswWDNSeVlXNXpabVZ5WDNkcGRHaGZaR0YwWVNoMGJ6b2dZbmwwWlhNc0lHRnRiM1Z1ZERvZ1lubDBaWE1zSUdSaGRHRTZJR0o1ZEdWektTQXRQaUJpZVhSbGN6b0tZWEpqTVRVNU5GOTBjbUZ1YzJabGNsOTNhWFJvWDJSaGRHRTZDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UVTVOQzVoYkdkdkxuUnpPalkzTFRZNENpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvS1FvZ0lDQWdMeThnY0hWaWJHbGpJR0Z5WXpFMU9UUmZkSEpoYm5ObVpYSmZkMmwwYUY5a1lYUmhLSFJ2T2lCaGNtTTBMa0ZrWkhKbGMzTXNJR0Z0YjNWdWREb2dZWEpqTkM1VmFXNTBUakkxTml3Z1pHRjBZVG9nWVhKak5DNUVlVzVoYldsalFubDBaWE1wT2lCaGNtTTBMa0p2YjJ3Z2V3b2dJQ0FnY0hKdmRHOGdNeUF4Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFU1TkM1aGJHZHZMblJ6T2pjd0NpQWdJQ0F2THlCamIyNXpkQ0J5WlhNZ1BTQjBhR2x6TG1GeVl6SXdNRjkwY21GdWMyWmxjaWgwYnl3Z1lXMXZkVzUwS1FvZ0lDQWdabkpoYldWZlpHbG5JQzB6Q2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVElLSUNBZ0lHTmhiR3h6ZFdJZ1lYSmpNakF3WDNSeVlXNXpabVZ5Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFU1TkM1aGJHZHZMblJ6T2pjeUNpQWdJQ0F2THlCeVpYUjFjbTRnY21WekNpQWdJQ0J5WlhSemRXSUtDZ292THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRVNU5DNWhiR2R2TG5Sek9qcEJjbU14TlRrMExtRnlZekUxT1RSZmRISmhibk5tWlhKZlpuSnZiVjkzYVhSb1gyUmhkR0VvWm5KdmJUb2dZbmwwWlhNc0lIUnZPaUJpZVhSbGN5d2dZVzF2ZFc1ME9pQmllWFJsY3l3Z1pHRjBZVG9nWW5sMFpYTXBJQzArSUdKNWRHVnpPZ3BoY21NeE5UazBYM1J5WVc1elptVnlYMlp5YjIxZmQybDBhRjlrWVhSaE9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMU9UUXVZV3huYnk1MGN6bzNOUzA0TVFvZ0lDQWdMeThnUUdGeVl6UXVZV0pwYldWMGFHOWtLQ2tLSUNBZ0lDOHZJSEIxWW14cFl5QmhjbU14TlRrMFgzUnlZVzV6Wm1WeVgyWnliMjFmZDJsMGFGOWtZWFJoS0FvZ0lDQWdMeThnSUNCbWNtOXRPaUJoY21NMExrRmtaSEpsYzNNc0NpQWdJQ0F2THlBZ0lIUnZPaUJoY21NMExrRmtaSEpsYzNNc0NpQWdJQ0F2THlBZ0lHRnRiM1Z1ZERvZ1lYSmpOQzVWYVc1MFRqSTFOaXdLSUNBZ0lDOHZJQ0FnWkdGMFlUb2dZWEpqTkM1RWVXNWhiV2xqUW5sMFpYTXNDaUFnSUNBdkx5QXBPaUJoY21NMExrSnZiMndnZXdvZ0lDQWdjSEp2ZEc4Z05DQXhDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UVTVOQzVoYkdkdkxuUnpPamd5Q2lBZ0lDQXZMeUJqYjI1emRDQnlaWE1nUFNCMGFHbHpMbUZ5WXpJd01GOTBjbUZ1YzJabGNrWnliMjBvWm5KdmJTd2dkRzhzSUdGdGIzVnVkQ2tLSUNBZ0lHWnlZVzFsWDJScFp5QXROQW9nSUNBZ1puSmhiV1ZmWkdsbklDMHpDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUSUtJQ0FnSUdOaGJHeHpkV0lnWVhKak1qQXdYM1J5WVc1elptVnlSbkp2YlFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUxT1RRdVlXeG5ieTUwY3pvNE13b2dJQ0FnTHk4Z2NtVjBkWEp1SUhKbGN3b2dJQ0FnY21WMGMzVmlDZ29LTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFMU9UUXVZV3huYnk1MGN6bzZRWEpqTVRVNU5DNWhjbU14TlRrMFgybHpYMmx6YzNWaFlteGxLQ2tnTFQ0Z1lubDBaWE02Q21GeVl6RTFPVFJmYVhOZmFYTnpkV0ZpYkdVNkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRVNU5DNWhiR2R2TG5Sek9qRTFDaUFnSUNBdkx5QndkV0pzYVdNZ2FYTnpkV0ZpYkdVZ1BTQkhiRzlpWVd4VGRHRjBaVHhoY21NMExrSnZiMncrS0hzZ2EyVjVPaUFuYVhOekp5QjlLU0F2THlCVWNuVmxJRDBnYVhOemRXRmliR1VLSUNBZ0lHbHVkR05mTUNBdkx5QXdDaUFnSUNCaWVYUmxZeUF4TlNBdkx5QWlhWE56SWdvZ0lDQWdZWEJ3WDJkc2IySmhiRjluWlhSZlpYZ0tJQ0FnSUdGemMyVnlkQ0F2THlCamFHVmpheUJIYkc5aVlXeFRkR0YwWlNCbGVHbHpkSE1LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOVGswTG1Gc1oyOHVkSE02T0RrS0lDQWdJQzh2SUhKbGRIVnliaUIwYUdsekxtbHpjM1ZoWW14bExuWmhiSFZsQ2lBZ0lDQnlaWFJ6ZFdJS0Nnb3ZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pwQmNtTXhOREV3TG1GeVl6RTBNVEJmWW1Gc1lXNWpaVjl2Wmw5d1lYSjBhWFJwYjI0b2FHOXNaR1Z5T2lCaWVYUmxjeXdnY0dGeWRHbDBhVzl1T2lCaWVYUmxjeWtnTFQ0Z1lubDBaWE02Q21GeVl6RTBNVEJmWW1Gc1lXNWpaVjl2Wmw5d1lYSjBhWFJwYjI0NkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qWTVMVGN3Q2lBZ0lDQXZMeUJBWVhKak5DNWhZbWx0WlhSb2IyUW9leUJ5WldGa2IyNXNlVG9nZEhKMVpTQjlLUW9nSUNBZ0x5OGdjSFZpYkdsaklHRnlZekUwTVRCZlltRnNZVzVqWlY5dlpsOXdZWEowYVhScGIyNG9hRzlzWkdWeU9pQmhjbU0wTGtGa1pISmxjM01zSUhCaGNuUnBkR2x2YmpvZ1lYSmpOQzVCWkdSeVpYTnpLVG9nWVhKak5DNVZhVzUwVGpJMU5pQjdDaUFnSUNCd2NtOTBieUF5SURFS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk56RXROelFLSUNBZ0lDOHZJR052Ym5OMElHdGxlU0E5SUc1bGR5QmhjbU14TkRFd1gxQmhjblJwZEdsdmJrdGxlU2g3Q2lBZ0lDQXZMeUFnSUdodmJHUmxjam9nYUc5c1pHVnlMQW9nSUNBZ0x5OGdJQ0J3WVhKMGFYUnBiMjQ2SUhCaGNuUnBkR2x2Yml3S0lDQWdJQzh2SUgwcENpQWdJQ0JtY21GdFpWOWthV2NnTFRJS0lDQWdJR1p5WVcxbFgyUnBaeUF0TVFvZ0lDQWdZMjl1WTJGMENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qVTNDaUFnSUNBdkx5QndkV0pzYVdNZ2NHRnlkR2wwYVc5dWN5QTlJRUp2ZUUxaGNEeGhjbU14TkRFd1gxQmhjblJwZEdsdmJrdGxlU3dnWVhKak5DNVZhVzUwVGpJMU5qNG9leUJyWlhsUWNtVm1hWGc2SUNkd0p5QjlLUW9nSUNBZ1lubDBaV01nT0NBdkx5QWljQ0lLSUNBZ0lITjNZWEFLSUNBZ0lHTnZibU5oZEFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvM05Rb2dJQ0FnTHk4Z2NtVjBkWEp1SUhSb2FYTXVjR0Z5ZEdsMGFXOXVjeWhyWlhrcExuWmhiSFZsQ2lBZ0lDQmliM2hmWjJWMENpQWdJQ0JoYzNObGNuUWdMeThnUW05NElHMTFjM1FnYUdGMlpTQjJZV3gxWlFvZ0lDQWdjbVYwYzNWaUNnb0tMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvNlFYSmpNVFF4TUM1aGNtTXlNREJmZEhKaGJuTm1aWElvZEc4NklHSjVkR1Z6TENCMllXeDFaVG9nWW5sMFpYTXBJQzArSUdKNWRHVnpPZ3BoY21NeU1EQmZkSEpoYm5ObVpYSTZDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPamM0TFRjNUNpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvS1FvZ0lDQWdMeThnY0hWaWJHbGpJRzkyWlhKeWFXUmxJR0Z5WXpJd01GOTBjbUZ1YzJabGNpaDBiem9nWVhKak5DNUJaR1J5WlhOekxDQjJZV3gxWlRvZ1lYSmpOQzVWYVc1MFRqSTFOaWs2SUdGeVl6UXVRbTl2YkNCN0NpQWdJQ0J3Y205MGJ5QXlJREVLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02T0RFS0lDQWdJQzh2SUc1bGR5QmhjbU0wTGtGa1pISmxjM01vVkhodUxuTmxibVJsY2lrc0NpQWdJQ0IwZUc0Z1UyVnVaR1Z5Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pneUNpQWdJQ0F2THlCdVpYY2dZWEpqTkM1QlpHUnlaWE56S0Nrc0NpQWdJQ0JpZVhSbFkxOHhJQzh2SUdGa1pISWdRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFWazFTRVpMVVFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvNE1DMDROd29nSUNBZ0x5OGdkR2hwY3k1ZmRISmhibk5tWlhKZmNHRnlkR2wwYVc5dUtBb2dJQ0FnTHk4Z0lDQnVaWGNnWVhKak5DNUJaR1J5WlhOektGUjRiaTV6Wlc1a1pYSXBMQW9nSUNBZ0x5OGdJQ0J1WlhjZ1lYSmpOQzVCWkdSeVpYTnpLQ2tzQ2lBZ0lDQXZMeUFnSUhSdkxBb2dJQ0FnTHk4Z0lDQnVaWGNnWVhKak5DNUJaR1J5WlhOektDa3NDaUFnSUNBdkx5QWdJSFpoYkhWbExBb2dJQ0FnTHk4Z0lDQnVaWGNnWVhKak5DNUVlVzVoYldsalFubDBaWE1vS1N3S0lDQWdJQzh2SUNrS0lDQWdJR1p5WVcxbFgyUnBaeUF0TWdvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvNE5Bb2dJQ0FnTHk4Z2JtVjNJR0Z5WXpRdVFXUmtjbVZ6Y3lncExBb2dJQ0FnWW5sMFpXTmZNU0F2THlCaFpHUnlJRUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRlpOVWhHUzFFS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk9EQXRPRGNLSUNBZ0lDOHZJSFJvYVhNdVgzUnlZVzV6Wm1WeVgzQmhjblJwZEdsdmJpZ0tJQ0FnSUM4dklDQWdibVYzSUdGeVl6UXVRV1JrY21WemN5aFVlRzR1YzJWdVpHVnlLU3dLSUNBZ0lDOHZJQ0FnYm1WM0lHRnlZelF1UVdSa2NtVnpjeWdwTEFvZ0lDQWdMeThnSUNCMGJ5d0tJQ0FnSUM4dklDQWdibVYzSUdGeVl6UXVRV1JrY21WemN5Z3BMQW9nSUNBZ0x5OGdJQ0IyWVd4MVpTd0tJQ0FnSUM4dklDQWdibVYzSUdGeVl6UXVSSGx1WVcxcFkwSjVkR1Z6S0Nrc0NpQWdJQ0F2THlBcENpQWdJQ0JtY21GdFpWOWthV2NnTFRFS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk9EWUtJQ0FnSUM4dklHNWxkeUJoY21NMExrUjVibUZ0YVdOQ2VYUmxjeWdwTEFvZ0lDQWdZbmwwWldNZ01qWWdMeThnTUhnd01EQXdDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPamd3TFRnM0NpQWdJQ0F2THlCMGFHbHpMbDkwY21GdWMyWmxjbDl3WVhKMGFYUnBiMjRvQ2lBZ0lDQXZMeUFnSUc1bGR5QmhjbU0wTGtGa1pISmxjM01vVkhodUxuTmxibVJsY2lrc0NpQWdJQ0F2THlBZ0lHNWxkeUJoY21NMExrRmtaSEpsYzNNb0tTd0tJQ0FnSUM4dklDQWdkRzhzQ2lBZ0lDQXZMeUFnSUc1bGR5QmhjbU0wTGtGa1pISmxjM01vS1N3S0lDQWdJQzh2SUNBZ2RtRnNkV1VzQ2lBZ0lDQXZMeUFnSUc1bGR5QmhjbU0wTGtSNWJtRnRhV05DZVhSbGN5Z3BMQW9nSUNBZ0x5OGdLUW9nSUNBZ1kyRnNiSE4xWWlCZmRISmhibk5tWlhKZmNHRnlkR2wwYVc5dUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qZzRDaUFnSUNBdkx5QnlaWFIxY200Z2RHaHBjeTVmZEhKaGJuTm1aWElvYm1WM0lHRnlZelF1UVdSa2NtVnpjeWhVZUc0dWMyVnVaR1Z5S1N3Z2RHOHNJSFpoYkhWbEtRb2dJQ0FnZEhodUlGTmxibVJsY2dvZ0lDQWdabkpoYldWZlpHbG5JQzB5Q2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVEVLSUNBZ0lHTmhiR3h6ZFdJZ1gzUnlZVzV6Wm1WeUNpQWdJQ0J5WlhSemRXSUtDZ292THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qcEJjbU14TkRFd0xtRnlZekUwTVRCZmRISmhibk5tWlhKZllubGZjR0Z5ZEdsMGFXOXVLSEJoY25ScGRHbHZiam9nWW5sMFpYTXNJSFJ2T2lCaWVYUmxjeXdnWVcxdmRXNTBPaUJpZVhSbGN5d2daR0YwWVRvZ1lubDBaWE1wSUMwK0lHSjVkR1Z6T2dwaGNtTXhOREV3WDNSeVlXNXpabVZ5WDJKNVgzQmhjblJwZEdsdmJqb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZPVE10T1RrS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2dwQ2lBZ0lDQXZMeUJ3ZFdKc2FXTWdZWEpqTVRReE1GOTBjbUZ1YzJabGNsOWllVjl3WVhKMGFYUnBiMjRvQ2lBZ0lDQXZMeUFnSUhCaGNuUnBkR2x2YmpvZ1lYSmpOQzVCWkdSeVpYTnpMQW9nSUNBZ0x5OGdJQ0IwYnpvZ1lYSmpOQzVCWkdSeVpYTnpMQW9nSUNBZ0x5OGdJQ0JoYlc5MWJuUTZJR0Z5WXpRdVZXbHVkRTR5TlRZc0NpQWdJQ0F2THlBZ0lHUmhkR0U2SUdGeVl6UXVSSGx1WVcxcFkwSjVkR1Z6TEFvZ0lDQWdMeThnS1RvZ1lYSmpOQzVCWkdSeVpYTnpJSHNLSUNBZ0lIQnliM1J2SURRZ01Rb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3hNREFLSUNBZ0lDOHZJR052Ym5OMElITmxibVJsY2lBOUlHNWxkeUJoY21NMExrRmtaSEpsYzNNb1ZIaHVMbk5sYm1SbGNpa0tJQ0FnSUhSNGJpQlRaVzVrWlhJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk1UQXlDaUFnSUNBdkx5QnNaWFFnY21WalpXbDJaWEpRWVhKMGFYUnBiMjRnUFNCMGFHbHpMbDl5WldObGFYWmxjbEJoY25ScGRHbHZiaWgwYnl3Z2NHRnlkR2wwYVc5dUtRb2dJQ0FnWm5KaGJXVmZaR2xuSUMwekNpQWdJQ0JtY21GdFpWOWthV2NnTFRRS0lDQWdJR05oYkd4emRXSWdYM0psWTJWcGRtVnlVR0Z5ZEdsMGFXOXVDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPakV3TXdvZ0lDQWdMeThnZEdocGN5NWZkSEpoYm5ObVpYSmZjR0Z5ZEdsMGFXOXVLSE5sYm1SbGNpd2djR0Z5ZEdsMGFXOXVMQ0IwYnl3Z2NtVmpaV2wyWlhKUVlYSjBhWFJwYjI0c0lHRnRiM1Z1ZEN3Z1pHRjBZU2tLSUNBZ0lITjNZWEFLSUNBZ0lHWnlZVzFsWDJScFp5QXROQW9nSUNBZ1puSmhiV1ZmWkdsbklDMHpDaUFnSUNCa2FXY2dNd29nSUNBZ1puSmhiV1ZmWkdsbklDMHlDaUFnSUNCbWNtRnRaVjlrYVdjZ0xURUtJQ0FnSUdOaGJHeHpkV0lnWDNSeVlXNXpabVZ5WDNCaGNuUnBkR2x2YmdvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pveE1EUUtJQ0FnSUM4dklISmxkSFZ5YmlCeVpXTmxhWFpsY2xCaGNuUnBkR2x2YmdvZ0lDQWdjbVYwYzNWaUNnb0tMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvNlFYSmpNVFF4TUM1aGNtTXhOREV3WDNCaGNuUnBkR2x2Ym5OZmIyWW9hRzlzWkdWeU9pQmllWFJsY3l3Z2NHRm5aVG9nWW5sMFpYTXBJQzArSUdKNWRHVnpPZ3BoY21NeE5ERXdYM0JoY25ScGRHbHZibk5mYjJZNkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qRXdOeTB4TURnS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2dwQ2lBZ0lDQXZMeUJ3ZFdKc2FXTWdZWEpqTVRReE1GOXdZWEowYVhScGIyNXpYMjltS0dodmJHUmxjam9nWVhKak5DNUJaR1J5WlhOekxDQndZV2RsT2lCaGNtTTBMbFZwYm5ST05qUXBPaUJoY21NMExrRmtaSEpsYzNOYlhTQjdDaUFnSUNCd2NtOTBieUF5SURFS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk1UQTVDaUFnSUNBdkx5QmpiMjV6ZENCclpYa2dQU0J1WlhjZ1lYSmpNVFF4TUY5SWIyeGthVzVuVUdGeWRHbDBhVzl1YzFCaFoybHVZWFJsWkV0bGVTaDdJR2h2YkdSbGNqb2dhRzlzWkdWeUxDQndZV2RsT2lCd1lXZGxJSDBwQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVElLSUNBZ0lHWnlZVzFsWDJScFp5QXRNUW9nSUNBZ1kyOXVZMkYwQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pZd0NpQWdJQ0F2THlCclpYbFFjbVZtYVhnNklDZG9jRjloSnl3S0lDQWdJR0o1ZEdWaklERTJJQzh2SUNKb2NGOWhJZ29nSUNBZ2MzZGhjQW9nSUNBZ1kyOXVZMkYwQ2lBZ0lDQmtkWEFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TVRFd0NpQWdJQ0F2THlCcFppQW9JWFJvYVhNdWFHOXNaR1Z5VUdGeWRHbDBhVzl1YzBGa1pISmxjM05sY3loclpYa3BMbVY0YVhOMGN5a2djbVYwZFhKdUlGdGRDaUFnSUNCaWIzaGZiR1Z1Q2lBZ0lDQmlkWEo1SURFS0lDQWdJR0p1ZWlCaGNtTXhOREV3WDNCaGNuUnBkR2x2Ym5OZmIyWmZZV1owWlhKZmFXWmZaV3h6WlVBeUNpQWdJQ0JpZVhSbFl5QXlOaUF2THlBd2VEQXdNREFLSUNBZ0lITjNZWEFLSUNBZ0lISmxkSE4xWWdvS1lYSmpNVFF4TUY5d1lYSjBhWFJwYjI1elgyOW1YMkZtZEdWeVgybG1YMlZzYzJWQU1qb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNVEV4Q2lBZ0lDQXZMeUJ5WlhSMWNtNGdkR2hwY3k1b2IyeGtaWEpRWVhKMGFYUnBiMjV6UVdSa2NtVnpjMlZ6S0d0bGVTa3VkbUZzZFdVS0lDQWdJR1p5WVcxbFgyUnBaeUF3Q2lBZ0lDQmliM2hmWjJWMENpQWdJQ0JoYzNObGNuUWdMeThnUW05NElHMTFjM1FnYUdGMlpTQjJZV3gxWlFvZ0lDQWdjM2RoY0FvZ0lDQWdjbVYwYzNWaUNnb0tMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvNlFYSmpNVFF4TUM1aGNtTXhOREV3WDJselgyOXdaWEpoZEc5eUtHaHZiR1JsY2pvZ1lubDBaWE1zSUc5d1pYSmhkRzl5T2lCaWVYUmxjeXdnY0dGeWRHbDBhVzl1T2lCaWVYUmxjeWtnTFQ0Z1lubDBaWE02Q21GeVl6RTBNVEJmYVhOZmIzQmxjbUYwYjNJNkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qRXhOQzB4TVRVS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2g3SUhKbFlXUnZibXg1T2lCMGNuVmxJSDBwQ2lBZ0lDQXZMeUJ3ZFdKc2FXTWdZWEpqTVRReE1GOXBjMTl2Y0dWeVlYUnZjaWhvYjJ4a1pYSTZJR0Z5WXpRdVFXUmtjbVZ6Y3l3Z2IzQmxjbUYwYjNJNklHRnlZelF1UVdSa2NtVnpjeXdnY0dGeWRHbDBhVzl1T2lCaGNtTTBMa0ZrWkhKbGMzTXBPaUJoY21NMExrSnZiMndnZXdvZ0lDQWdjSEp2ZEc4Z015QXhDaUFnSUNCcGJuUmpYekFnTHk4Z01Bb2dJQ0FnWkhWd2JpQXlDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPakV4TmdvZ0lDQWdMeThnYVdZZ0tHOXdaWEpoZEc5eUlEMDlQU0JvYjJ4a1pYSXBJSEpsZEhWeWJpQnVaWGNnWVhKak5DNUNiMjlzS0hSeWRXVXBDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUSUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE13b2dJQ0FnUFQwS0lDQWdJR0o2SUdGeVl6RTBNVEJmYVhOZmIzQmxjbUYwYjNKZllXWjBaWEpmYVdaZlpXeHpaVUF5Q2lBZ0lDQmllWFJsWXlBM0lDOHZJREI0T0RBS0lDQWdJR1p5WVcxbFgySjFjbmtnTUFvZ0lDQWdjbVYwYzNWaUNncGhjbU14TkRFd1gybHpYMjl3WlhKaGRHOXlYMkZtZEdWeVgybG1YMlZzYzJWQU1qb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNVEUzQ2lBZ0lDQXZMeUJqYjI1emRDQnpjR1ZqYVdacFl5QTlJRzVsZHlCaGNtTXhOREV3WDA5d1pYSmhkRzl5UzJWNUtIc2dhRzlzWkdWeU9pQm9iMnhrWlhJc0lHOXdaWEpoZEc5eU9pQnZjR1Z5WVhSdmNpd2djR0Z5ZEdsMGFXOXVPaUJ3WVhKMGFYUnBiMjRnZlNrS0lDQWdJR1p5WVcxbFgyUnBaeUF0TXdvZ0lDQWdabkpoYldWZlpHbG5JQzB5Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR1IxY0FvZ0lDQWdabkpoYldWZlluVnllU0F3Q2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVEVLSUNBZ0lHTnZibU5oZEFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvMk1nb2dJQ0FnTHk4Z2NIVmliR2xqSUc5d1pYSmhkRzl5Y3lBOUlFSnZlRTFoY0R4aGNtTXhOREV3WDA5d1pYSmhkRzl5UzJWNUxDQmhjbU0wTGtKNWRHVStLSHNnYTJWNVVISmxabWw0T2lBbmIzQW5JSDBwSUM4dklIWmhiSFZsSUQwZ01TQmhkWFJvYjNKcGVtVmtDaUFnSUNCaWVYUmxZeUF4TnlBdkx5QWliM0FpQ2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR1IxY0FvZ0lDQWdabkpoYldWZlluVnllU0F4Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pFeE9Bb2dJQ0FnTHk4Z2FXWWdLSFJvYVhNdWIzQmxjbUYwYjNKektITndaV05wWm1saktTNWxlR2x6ZEhNZ0ppWWdkR2hwY3k1dmNHVnlZWFJ2Y25Nb2MzQmxZMmxtYVdNcExuWmhiSFZsTG01aGRHbDJaU0E5UFQwZ01Ta2dld29nSUNBZ1ltOTRYMnhsYmdvZ0lDQWdZblZ5ZVNBeENpQWdJQ0JpZWlCaGNtTXhOREV3WDJselgyOXdaWEpoZEc5eVgyRm1kR1Z5WDJsbVgyVnNjMlZBTlFvZ0lDQWdabkpoYldWZlpHbG5JREVLSUNBZ0lHSnZlRjluWlhRS0lDQWdJR0Z6YzJWeWRDQXZMeUJDYjNnZ2JYVnpkQ0JvWVhabElIWmhiSFZsQ2lBZ0lDQmlkRzlwQ2lBZ0lDQnBiblJqWHpFZ0x5OGdNUW9nSUNBZ1BUMEtJQ0FnSUdKNklHRnlZekUwTVRCZmFYTmZiM0JsY21GMGIzSmZZV1owWlhKZmFXWmZaV3h6WlVBMUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qRXhPUW9nSUNBZ0x5OGdjbVYwZFhKdUlHNWxkeUJoY21NMExrSnZiMndvZEhKMVpTa0tJQ0FnSUdKNWRHVmpJRGNnTHk4Z01IZzRNQW9nSUNBZ1puSmhiV1ZmWW5WeWVTQXdDaUFnSUNCeVpYUnpkV0lLQ21GeVl6RTBNVEJmYVhOZmIzQmxjbUYwYjNKZllXWjBaWEpmYVdaZlpXeHpaVUExT2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pveE1qRUtJQ0FnSUM4dklHTnZibk4wSUdkc2IySmhiRXRsZVNBOUlHNWxkeUJoY21NeE5ERXdYMDl3WlhKaGRHOXlTMlY1S0hzZ2FHOXNaR1Z5T2lCb2IyeGtaWElzSUc5d1pYSmhkRzl5T2lCdmNHVnlZWFJ2Y2l3Z2NHRnlkR2wwYVc5dU9pQnVaWGNnWVhKak5DNUJaR1J5WlhOektDa2dmU2tLSUNBZ0lHWnlZVzFsWDJScFp5QXdDaUFnSUNCaWVYUmxZMTh4SUM4dklHRmtaSElnUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVmsxU0VaTFVRb2dJQ0FnWTI5dVkyRjBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPall5Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdiM0JsY21GMGIzSnpJRDBnUW05NFRXRndQR0Z5WXpFME1UQmZUM0JsY21GMGIzSkxaWGtzSUdGeVl6UXVRbmwwWlQ0b2V5QnJaWGxRY21WbWFYZzZJQ2R2Y0NjZ2ZTa2dMeThnZG1Gc2RXVWdQU0F4SUdGMWRHaHZjbWw2WldRS0lDQWdJR0o1ZEdWaklERTNJQzh2SUNKdmNDSUtJQ0FnSUhOM1lYQUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ1pIVndDaUFnSUNCbWNtRnRaVjlpZFhKNUlESUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNVEl5Q2lBZ0lDQXZMeUJwWmlBb2RHaHBjeTV2Y0dWeVlYUnZjbk1vWjJ4dlltRnNTMlY1S1M1bGVHbHpkSE1nSmlZZ2RHaHBjeTV2Y0dWeVlYUnZjbk1vWjJ4dlltRnNTMlY1S1M1MllXeDFaUzV1WVhScGRtVWdQVDA5SURFcElIc0tJQ0FnSUdKdmVGOXNaVzRLSUNBZ0lHSjFjbmtnTVFvZ0lDQWdZbm9nWVhKak1UUXhNRjlwYzE5dmNHVnlZWFJ2Y2w5aFpuUmxjbDlwWmw5bGJITmxRRGdLSUNBZ0lHWnlZVzFsWDJScFp5QXlDaUFnSUNCaWIzaGZaMlYwQ2lBZ0lDQmhjM05sY25RZ0x5OGdRbTk0SUcxMWMzUWdhR0YyWlNCMllXeDFaUW9nSUNBZ1luUnZhUW9nSUNBZ2FXNTBZMTh4SUM4dklERUtJQ0FnSUQwOUNpQWdJQ0JpZWlCaGNtTXhOREV3WDJselgyOXdaWEpoZEc5eVgyRm1kR1Z5WDJsbVgyVnNjMlZBT0FvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pveE1qTUtJQ0FnSUM4dklISmxkSFZ5YmlCdVpYY2dZWEpqTkM1Q2IyOXNLSFJ5ZFdVcENpQWdJQ0JpZVhSbFl5QTNJQzh2SURCNE9EQUtJQ0FnSUdaeVlXMWxYMkoxY25rZ01Bb2dJQ0FnY21WMGMzVmlDZ3BoY21NeE5ERXdYMmx6WDI5d1pYSmhkRzl5WDJGbWRHVnlYMmxtWDJWc2MyVkFPRG9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TVRJMUNpQWdJQ0F2THlCeVpYUjFjbTRnYm1WM0lHRnlZelF1UW05dmJDaG1ZV3h6WlNrS0lDQWdJR0o1ZEdWaklERXhJQzh2SURCNE1EQUtJQ0FnSUdaeVlXMWxYMkoxY25rZ01Bb2dJQ0FnY21WMGMzVmlDZ29LTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6bzZRWEpqTVRReE1DNWhjbU14TkRFd1gyRjFkR2h2Y21sNlpWOXZjR1Z5WVhSdmNpaG9iMnhrWlhJNklHSjVkR1Z6TENCdmNHVnlZWFJ2Y2pvZ1lubDBaWE1zSUhCaGNuUnBkR2x2YmpvZ1lubDBaWE1wSUMwK0lIWnZhV1E2Q21GeVl6RTBNVEJmWVhWMGFHOXlhWHBsWDI5d1pYSmhkRzl5T2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pveE1qZ3RNVEk1Q2lBZ0lDQXZMeUJBWVhKak5DNWhZbWx0WlhSb2IyUW9LUW9nSUNBZ0x5OGdjSFZpYkdsaklHRnlZekUwTVRCZllYVjBhRzl5YVhwbFgyOXdaWEpoZEc5eUtHaHZiR1JsY2pvZ1lYSmpOQzVCWkdSeVpYTnpMQ0J2Y0dWeVlYUnZjam9nWVhKak5DNUJaR1J5WlhOekxDQndZWEowYVhScGIyNDZJR0Z5WXpRdVFXUmtjbVZ6Y3lrNklIWnZhV1FnZXdvZ0lDQWdjSEp2ZEc4Z015QXdDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPakV6TUFvZ0lDQWdMeThnWVhOelpYSjBLRzVsZHlCaGNtTTBMa0ZrWkhKbGMzTW9WSGh1TG5ObGJtUmxjaWtnUFQwOUlHaHZiR1JsY2l3Z0owOXViSGtnYUc5c1pHVnlJR05oYmlCaGRYUm9iM0pwZW1VbktRb2dJQ0FnZEhodUlGTmxibVJsY2dvZ0lDQWdabkpoYldWZlpHbG5JQzB6Q2lBZ0lDQTlQUW9nSUNBZ1lYTnpaWEowSUM4dklFOXViSGtnYUc5c1pHVnlJR05oYmlCaGRYUm9iM0pwZW1VS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk1UTXhDaUFnSUNBdkx5QmpiMjV6ZENCclpYa2dQU0J1WlhjZ1lYSmpNVFF4TUY5UGNHVnlZWFJ2Y2t0bGVTaDdJR2h2YkdSbGNqb2dhRzlzWkdWeUxDQnZjR1Z5WVhSdmNqb2diM0JsY21GMGIzSXNJSEJoY25ScGRHbHZiam9nY0dGeWRHbDBhVzl1SUgwcENpQWdJQ0JtY21GdFpWOWthV2NnTFRNS0lDQWdJR1p5WVcxbFgyUnBaeUF0TWdvZ0lDQWdZMjl1WTJGMENpQWdJQ0JtY21GdFpWOWthV2NnTFRFS0lDQWdJR052Ym1OaGRBb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6bzJNZ29nSUNBZ0x5OGdjSFZpYkdsaklHOXdaWEpoZEc5eWN5QTlJRUp2ZUUxaGNEeGhjbU14TkRFd1gwOXdaWEpoZEc5eVMyVjVMQ0JoY21NMExrSjVkR1UrS0hzZ2EyVjVVSEpsWm1sNE9pQW5iM0FuSUgwcElDOHZJSFpoYkhWbElEMGdNU0JoZFhSb2IzSnBlbVZrQ2lBZ0lDQmllWFJsWXlBeE55QXZMeUFpYjNBaUNpQWdJQ0J6ZDJGd0NpQWdJQ0JqYjI1allYUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNVE15Q2lBZ0lDQXZMeUIwYUdsekxtOXdaWEpoZEc5eWN5aHJaWGtwTG5aaGJIVmxJRDBnYm1WM0lHRnlZelF1UW5sMFpTZ3hLUW9nSUNBZ1lubDBaV01nTWpJZ0x5OGdNSGd3TVFvZ0lDQWdZbTk0WDNCMWRBb2dJQ0FnY21WMGMzVmlDZ29LTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6bzZRWEpqTVRReE1DNWhjbU14TkRFd1gzSmxkbTlyWlY5dmNHVnlZWFJ2Y2lob2IyeGtaWEk2SUdKNWRHVnpMQ0J2Y0dWeVlYUnZjam9nWW5sMFpYTXNJSEJoY25ScGRHbHZiam9nWW5sMFpYTXBJQzArSUhadmFXUTZDbUZ5WXpFME1UQmZjbVYyYjJ0bFgyOXdaWEpoZEc5eU9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3hNelV0TVRNMkNpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvS1FvZ0lDQWdMeThnY0hWaWJHbGpJR0Z5WXpFME1UQmZjbVYyYjJ0bFgyOXdaWEpoZEc5eUtHaHZiR1JsY2pvZ1lYSmpOQzVCWkdSeVpYTnpMQ0J2Y0dWeVlYUnZjam9nWVhKak5DNUJaR1J5WlhOekxDQndZWEowYVhScGIyNDZJR0Z5WXpRdVFXUmtjbVZ6Y3lrNklIWnZhV1FnZXdvZ0lDQWdjSEp2ZEc4Z015QXdDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPakV6TndvZ0lDQWdMeThnWVhOelpYSjBLRzVsZHlCaGNtTTBMa0ZrWkhKbGMzTW9WSGh1TG5ObGJtUmxjaWtnUFQwOUlHaHZiR1JsY2l3Z0owOXViSGtnYUc5c1pHVnlJR05oYmlCeVpYWnZhMlVuS1FvZ0lDQWdkSGh1SUZObGJtUmxjZ29nSUNBZ1puSmhiV1ZmWkdsbklDMHpDaUFnSUNBOVBRb2dJQ0FnWVhOelpYSjBJQzh2SUU5dWJIa2dhRzlzWkdWeUlHTmhiaUJ5WlhadmEyVUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNVE00Q2lBZ0lDQXZMeUJqYjI1emRDQnJaWGtnUFNCdVpYY2dZWEpqTVRReE1GOVBjR1Z5WVhSdmNrdGxlU2g3SUdodmJHUmxjam9nYUc5c1pHVnlMQ0J2Y0dWeVlYUnZjam9nYjNCbGNtRjBiM0lzSUhCaGNuUnBkR2x2YmpvZ2NHRnlkR2wwYVc5dUlIMHBDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUTUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE1nb2dJQ0FnWTI5dVkyRjBDaUFnSUNCbWNtRnRaVjlrYVdjZ0xURUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem8yTWdvZ0lDQWdMeThnY0hWaWJHbGpJRzl3WlhKaGRHOXljeUE5SUVKdmVFMWhjRHhoY21NeE5ERXdYMDl3WlhKaGRHOXlTMlY1TENCaGNtTTBMa0o1ZEdVK0tIc2dhMlY1VUhKbFptbDRPaUFuYjNBbklIMHBJQzh2SUhaaGJIVmxJRDBnTVNCaGRYUm9iM0pwZW1Wa0NpQWdJQ0JpZVhSbFl5QXhOeUF2THlBaWIzQWlDaUFnSUNCemQyRndDaUFnSUNCamIyNWpZWFFLSUNBZ0lHUjFjQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem94TXprS0lDQWdJQzh2SUdsbUlDaDBhR2x6TG05d1pYSmhkRzl5Y3loclpYa3BMbVY0YVhOMGN5a2dld29nSUNBZ1ltOTRYMnhsYmdvZ0lDQWdZblZ5ZVNBeENpQWdJQ0JpZWlCaGNtTXhOREV3WDNKbGRtOXJaVjl2Y0dWeVlYUnZjbDloWm5SbGNsOXBabDlsYkhObFFESUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNVFF3Q2lBZ0lDQXZMeUIwYUdsekxtOXdaWEpoZEc5eWN5aHJaWGtwTG1SbGJHVjBaU2dwQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dNQW9nSUNBZ1ltOTRYMlJsYkFvZ0lDQWdjRzl3Q2dwaGNtTXhOREV3WDNKbGRtOXJaVjl2Y0dWeVlYUnZjbDloWm5SbGNsOXBabDlsYkhObFFESTZDaUFnSUNCeVpYUnpkV0lLQ2dvdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPanBCY21NeE5ERXdMbUZ5WXpFME1UQmZiM0JsY21GMGIzSmZkSEpoYm5ObVpYSmZZbmxmY0dGeWRHbDBhVzl1S0daeWIyMDZJR0o1ZEdWekxDQndZWEowYVhScGIyNDZJR0o1ZEdWekxDQjBiem9nWW5sMFpYTXNJR0Z0YjNWdWREb2dZbmwwWlhNc0lHUmhkR0U2SUdKNWRHVnpLU0F0UGlCaWVYUmxjem9LWVhKak1UUXhNRjl2Y0dWeVlYUnZjbDkwY21GdWMyWmxjbDlpZVY5d1lYSjBhWFJwYjI0NkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qRTBOQzB4TlRFS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2dwQ2lBZ0lDQXZMeUJ3ZFdKc2FXTWdZWEpqTVRReE1GOXZjR1Z5WVhSdmNsOTBjbUZ1YzJabGNsOWllVjl3WVhKMGFYUnBiMjRvQ2lBZ0lDQXZMeUFnSUdaeWIyMDZJR0Z5WXpRdVFXUmtjbVZ6Y3l3S0lDQWdJQzh2SUNBZ2NHRnlkR2wwYVc5dU9pQmhjbU0wTGtGa1pISmxjM01zQ2lBZ0lDQXZMeUFnSUhSdk9pQmhjbU0wTGtGa1pISmxjM01zQ2lBZ0lDQXZMeUFnSUdGdGIzVnVkRG9nWVhKak5DNVZhVzUwVGpJMU5pd0tJQ0FnSUM4dklDQWdaR0YwWVRvZ1lYSmpOQzVFZVc1aGJXbGpRbmwwWlhNc0NpQWdJQ0F2THlBcE9pQmhjbU0wTGtGa1pISmxjM01nZXdvZ0lDQWdjSEp2ZEc4Z05TQXhDaUFnSUNCcGJuUmpYekFnTHk4Z01Bb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3hOVElLSUNBZ0lDOHZJR052Ym5OMElITmxibVJsY2lBOUlHNWxkeUJoY21NMExrRmtaSEpsYzNNb1ZIaHVMbk5sYm1SbGNpa0tJQ0FnSUhSNGJpQlRaVzVrWlhJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk1UVTBDaUFnSUNBdkx5QnNaWFFnWVhWMGFHOXlhWHBsWkNBOUlIUm9hWE11WVhKak1UUXhNRjlwYzE5dmNHVnlZWFJ2Y2lobWNtOXRMQ0J6Wlc1a1pYSXNJSEJoY25ScGRHbHZiaWt1Ym1GMGFYWmxJRDA5UFNCMGNuVmxDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUVUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNVFV5Q2lBZ0lDQXZMeUJqYjI1emRDQnpaVzVrWlhJZ1BTQnVaWGNnWVhKak5DNUJaR1J5WlhOektGUjRiaTV6Wlc1a1pYSXBDaUFnSUNCMGVHNGdVMlZ1WkdWeUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qRTFOQW9nSUNBZ0x5OGdiR1YwSUdGMWRHaHZjbWw2WldRZ1BTQjBhR2x6TG1GeVl6RTBNVEJmYVhOZmIzQmxjbUYwYjNJb1puSnZiU3dnYzJWdVpHVnlMQ0J3WVhKMGFYUnBiMjRwTG01aGRHbDJaU0E5UFQwZ2RISjFaUW9nSUNBZ1puSmhiV1ZmWkdsbklDMDBDaUFnSUNCallXeHNjM1ZpSUdGeVl6RTBNVEJmYVhOZmIzQmxjbUYwYjNJS0lDQWdJR2x1ZEdOZk1DQXZMeUF3Q2lBZ0lDQm5aWFJpYVhRS0lDQWdJR2x1ZEdOZk1TQXZMeUF4Q2lBZ0lDQTlQUW9nSUNBZ1pIVndiaUF5Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pFMU5nb2dJQ0FnTHk4Z2FXWWdLQ0ZoZFhSb2IzSnBlbVZrS1NCN0NpQWdJQ0JpYm5vZ1lYSmpNVFF4TUY5dmNHVnlZWFJ2Y2w5MGNtRnVjMlpsY2w5aWVWOXdZWEowYVhScGIyNWZZV1owWlhKZmFXWmZaV3h6WlVBMENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qRTFPQW9nSUNBZ0x5OGdZMjl1YzNRZ2NFdGxlU0E5SUc1bGR5QmhjbU14TkRFd1gwOXdaWEpoZEc5eVVHOXlkR2x2Ymt0bGVTaDdJR2h2YkdSbGNqb2dabkp2YlN3Z2IzQmxjbUYwYjNJNklITmxibVJsY2l3Z2NHRnlkR2wwYVc5dUlIMHBDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUVUtJQ0FnSUdaeVlXMWxYMlJwWnlBeENpQWdJQ0JqYjI1allYUUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE5Bb2dJQ0FnWTI5dVkyRjBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPall6Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdiM0JsY21GMGIzSlFiM0owYVc5dVFXeHNiM2RoYm1ObGN5QTlJRUp2ZUUxaGNEeGhjbU14TkRFd1gwOXdaWEpoZEc5eVVHOXlkR2x2Ymt0bGVTd2dZWEpqTkM1VmFXNTBUakkxTmo0b2V5QnJaWGxRY21WbWFYZzZJQ2R2Y0dFbklIMHBDaUFnSUNCaWVYUmxZeUF4TXlBdkx5QWliM0JoSWdvZ0lDQWdjM2RoY0FvZ0lDQWdZMjl1WTJGMENpQWdJQ0JrZFhBS0lDQWdJR1p5WVcxbFgySjFjbmtnTUFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pveE5Ua0tJQ0FnSUM4dklHbG1JQ2gwYUdsekxtOXdaWEpoZEc5eVVHOXlkR2x2YmtGc2JHOTNZVzVqWlhNb2NFdGxlU2t1WlhocGMzUnpLU0I3Q2lBZ0lDQmliM2hmYkdWdUNpQWdJQ0JpZFhKNUlERUtJQ0FnSUdKNklHRnlZekUwTVRCZmIzQmxjbUYwYjNKZmRISmhibk5tWlhKZllubGZjR0Z5ZEdsMGFXOXVYMkZtZEdWeVgybG1YMlZzYzJWQU13b2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3hOakFLSUNBZ0lDOHZJR052Ym5OMElISmxiV0ZwYm1sdVp5QTlJSFJvYVhNdWIzQmxjbUYwYjNKUWIzSjBhVzl1UVd4c2IzZGhibU5sY3lod1MyVjVLUzUyWVd4MVpRb2dJQ0FnWm5KaGJXVmZaR2xuSURBS0lDQWdJR1IxY0FvZ0lDQWdZbTk0WDJkbGRBb2dJQ0FnWVhOelpYSjBJQzh2SUVKdmVDQnRkWE4wSUdoaGRtVWdkbUZzZFdVS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk1UWXhDaUFnSUNBdkx5QmhjM05sY25Rb2NtVnRZV2x1YVc1bkxtNWhkR2wyWlNBK1BTQmhiVzkxYm5RdWJtRjBhWFpsTENBblVHOXlkR2x2YmlCaGJHeHZkMkZ1WTJVZ1pYaGpaV1ZrWldRbktRb2dJQ0FnWkhWd0NpQWdJQ0JtY21GdFpWOWthV2NnTFRJS0lDQWdJR0krUFFvZ0lDQWdZWE56WlhKMElDOHZJRkJ2Y25ScGIyNGdZV3hzYjNkaGJtTmxJR1Y0WTJWbFpHVmtDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPakUyTWdvZ0lDQWdMeThnWVhWMGFHOXlhWHBsWkNBOUlIUnlkV1VLSUNBZ0lHbHVkR05mTVNBdkx5QXhDaUFnSUNCbWNtRnRaVjlpZFhKNUlESUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNVFkxQ2lBZ0lDQXZMeUIwYUdsekxtOXdaWEpoZEc5eVVHOXlkR2x2YmtGc2JHOTNZVzVqWlhNb2NFdGxlU2t1ZG1Gc2RXVWdQU0J1WlhjZ1lYSmpOQzVWYVc1MFRqSTFOaWh5WlcxaGFXNXBibWN1Ym1GMGFYWmxJQzBnWVcxdmRXNTBMbTVoZEdsMlpTa0tJQ0FnSUdaeVlXMWxYMlJwWnlBdE1nb2dJQ0FnWWkwS0lDQWdJR1IxY0FvZ0lDQWdiR1Z1Q2lBZ0lDQnBiblJqWHpJZ0x5OGdNeklLSUNBZ0lEdzlDaUFnSUNCaGMzTmxjblFnTHk4Z2IzWmxjbVpzYjNjS0lDQWdJR2x1ZEdOZk1pQXZMeUF6TWdvZ0lDQWdZbnBsY204S0lDQWdJR0o4Q2lBZ0lDQmliM2hmY0hWMENncGhjbU14TkRFd1gyOXdaWEpoZEc5eVgzUnlZVzV6Wm1WeVgySjVYM0JoY25ScGRHbHZibDloWm5SbGNsOXBabDlsYkhObFFETTZDaUFnSUNCbWNtRnRaVjlrYVdjZ01nb2dJQ0FnWm5KaGJXVmZZblZ5ZVNBekNncGhjbU14TkRFd1gyOXdaWEpoZEc5eVgzUnlZVzV6Wm1WeVgySjVYM0JoY25ScGRHbHZibDloWm5SbGNsOXBabDlsYkhObFFEUTZDaUFnSUNCbWNtRnRaVjlrYVdjZ013b2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3hOamdLSUNBZ0lDOHZJR0Z6YzJWeWRDaGhkWFJvYjNKcGVtVmtMQ0FuVG05MElHRjFkR2h2Y21sNlpXUWdiM0JsY21GMGIzSW5LUW9nSUNBZ1lYTnpaWEowSUM4dklFNXZkQ0JoZFhSb2IzSnBlbVZrSUc5d1pYSmhkRzl5Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pFMk9Rb2dJQ0FnTHk4Z2JHVjBJSEpsWTJWcGRtVnlVR0Z5ZEdsMGFXOXVJRDBnZEdocGN5NWZjbVZqWldsMlpYSlFZWEowYVhScGIyNG9kRzhzSUhCaGNuUnBkR2x2YmlrS0lDQWdJR1p5WVcxbFgyUnBaeUF0TXdvZ0lDQWdabkpoYldWZlpHbG5JQzAwQ2lBZ0lDQmpZV3hzYzNWaUlGOXlaV05sYVhabGNsQmhjblJwZEdsdmJnb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3hOekFLSUNBZ0lDOHZJSFJvYVhNdVgzUnlZVzV6Wm1WeVgzQmhjblJwZEdsdmJpaG1jbTl0TENCd1lYSjBhWFJwYjI0c0lIUnZMQ0J5WldObGFYWmxjbEJoY25ScGRHbHZiaXdnWVcxdmRXNTBMQ0JrWVhSaEtRb2dJQ0FnWm5KaGJXVmZaR2xuSUMwMUNpQWdJQ0JtY21GdFpWOWthV2NnTFRRS0lDQWdJR1p5WVcxbFgyUnBaeUF0TXdvZ0lDQWdaR2xuSURNS0lDQWdJR1p5WVcxbFgyUnBaeUF0TWdvZ0lDQWdabkpoYldWZlpHbG5JQzB4Q2lBZ0lDQmpZV3hzYzNWaUlGOTBjbUZ1YzJabGNsOXdZWEowYVhScGIyNEtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNVGN4Q2lBZ0lDQXZMeUJ5WlhSMWNtNGdjbVZqWldsMlpYSlFZWEowYVhScGIyNEtJQ0FnSUdaeVlXMWxYMkoxY25rZ01Bb2dJQ0FnY21WMGMzVmlDZ29LTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6bzZRWEpqTVRReE1DNWhjbU14TkRFd1gyTmhibDkwY21GdWMyWmxjbDlpZVY5d1lYSjBhWFJwYjI0b1puSnZiVG9nWW5sMFpYTXNJSEJoY25ScGRHbHZiam9nWW5sMFpYTXNJSFJ2T2lCaWVYUmxjeXdnWVcxdmRXNTBPaUJpZVhSbGN5d2daR0YwWVRvZ1lubDBaWE1wSUMwK0lHSjVkR1Z6T2dwaGNtTXhOREV3WDJOaGJsOTBjbUZ1YzJabGNsOWllVjl3WVhKMGFYUnBiMjQ2Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pFM05DMHhPREVLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDZ3BDaUFnSUNBdkx5QndkV0pzYVdNZ1lYSmpNVFF4TUY5allXNWZkSEpoYm5ObVpYSmZZbmxmY0dGeWRHbDBhVzl1S0FvZ0lDQWdMeThnSUNCbWNtOXRPaUJoY21NMExrRmtaSEpsYzNNc0NpQWdJQ0F2THlBZ0lIQmhjblJwZEdsdmJqb2dZWEpqTkM1QlpHUnlaWE56TEFvZ0lDQWdMeThnSUNCMGJ6b2dZWEpqTkM1QlpHUnlaWE56TEFvZ0lDQWdMeThnSUNCaGJXOTFiblE2SUdGeVl6UXVWV2x1ZEU0eU5UWXNDaUFnSUNBdkx5QWdJR1JoZEdFNklHRnlZelF1UkhsdVlXMXBZMEo1ZEdWekxBb2dJQ0FnTHk4Z0tUb2dZWEpqTVRReE1GOWpZVzVmZEhKaGJuTm1aWEpmWW5sZmNHRnlkR2wwYVc5dVgzSmxkSFZ5YmlCN0NpQWdJQ0J3Y205MGJ5QTFJREVLSUNBZ0lHbHVkR05mTUNBdkx5QXdDaUFnSUNCa2RYQUtJQ0FnSUhCMWMyaGllWFJsY3lBaUlnb2dJQ0FnWkhWd0NpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qSTFNUW9nSUNBZ0x5OGdjbVYwZFhKdUlIUm9hWE11Y0dGeWRHbDBhVzl1Y3lodVpYY2dZWEpqTVRReE1GOVFZWEowYVhScGIyNUxaWGtvZXlCb2IyeGtaWEk2SUdodmJHUmxjaXdnY0dGeWRHbDBhVzl1T2lCd1lYSjBhWFJwYjI0Z2ZTa3BMbVY0YVhOMGN3b2dJQ0FnWm5KaGJXVmZaR2xuSUMwMUNpQWdJQ0JtY21GdFpWOWthV2NnTFRRS0lDQWdJR052Ym1OaGRBb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6bzFOd29nSUNBZ0x5OGdjSFZpYkdsaklIQmhjblJwZEdsdmJuTWdQU0JDYjNoTllYQThZWEpqTVRReE1GOVFZWEowYVhScGIyNUxaWGtzSUdGeVl6UXVWV2x1ZEU0eU5UWStLSHNnYTJWNVVISmxabWw0T2lBbmNDY2dmU2tLSUNBZ0lHSjVkR1ZqSURnZ0x5OGdJbkFpQ2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR1IxY0FvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pveU5URUtJQ0FnSUM4dklISmxkSFZ5YmlCMGFHbHpMbkJoY25ScGRHbHZibk1vYm1WM0lHRnlZekUwTVRCZlVHRnlkR2wwYVc5dVMyVjVLSHNnYUc5c1pHVnlPaUJvYjJ4a1pYSXNJSEJoY25ScGRHbHZiam9nY0dGeWRHbDBhVzl1SUgwcEtTNWxlR2x6ZEhNS0lDQWdJR0p2ZUY5c1pXNEtJQ0FnSUdKMWNua2dNUW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem94T0RJS0lDQWdJQzh2SUdsbUlDZ2hkR2hwY3k1ZmRtRnNhV1JRWVhKMGFYUnBiMjRvWm5KdmJTd2djR0Z5ZEdsMGFXOXVLU2tnZXdvZ0lDQWdZbTU2SUdGeVl6RTBNVEJmWTJGdVgzUnlZVzV6Wm1WeVgySjVYM0JoY25ScGRHbHZibDloWm5SbGNsOXBabDlsYkhObFFESUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNVGd6TFRFNE53b2dJQ0FnTHk4Z2NtVjBkWEp1SUc1bGR5QmhjbU14TkRFd1gyTmhibDkwY21GdWMyWmxjbDlpZVY5d1lYSjBhWFJwYjI1ZmNtVjBkWEp1S0hzS0lDQWdJQzh2SUNBZ1kyOWtaVG9nYm1WM0lHRnlZelF1UW5sMFpTZ3dlRFV3S1N3S0lDQWdJQzh2SUNBZ2MzUmhkSFZ6T2lCdVpYY2dZWEpqTkM1VGRISW9KMUJoY25ScGRHbHZiaUJ1YjNRZ1pYaHBjM1J6Snlrc0NpQWdJQ0F2THlBZ0lISmxZMlZwZG1WeVVHRnlkR2wwYVc5dU9pQnVaWGNnWVhKak5DNUJaR1J5WlhOektDa3NDaUFnSUNBdkx5QjlLUW9nSUNBZ2NIVnphR0o1ZEdWeklHSmhjMlV6TWloTFFVRkRSMEZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJTMFpCV1V4VFQxSlZXRWt5VEZCT1dWRkhORE16VlVWQ1UxaFJNa3hVVDFKYVVTa0tJQ0FnSUdaeVlXMWxYMkoxY25rZ01Bb2dJQ0FnY21WMGMzVmlDZ3BoY21NeE5ERXdYMk5oYmw5MGNtRnVjMlpsY2w5aWVWOXdZWEowYVhScGIyNWZZV1owWlhKZmFXWmZaV3h6WlVBeU9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3hPVEFLSUNBZ0lDOHZJSFJvYVhNdWNHRnlkR2wwYVc5dWN5aHVaWGNnWVhKak1UUXhNRjlRWVhKMGFYUnBiMjVMWlhrb2V5Qm9iMnhrWlhJNklHWnliMjBzSUhCaGNuUnBkR2x2YmpvZ2NHRnlkR2wwYVc5dUlIMHBLUzUyWVd4MVpTNXVZWFJwZG1VZ1BDQmhiVzkxYm5RdWJtRjBhWFpsQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dOQW9nSUNBZ1ltOTRYMmRsZEFvZ0lDQWdZWE56WlhKMElDOHZJRUp2ZUNCdGRYTjBJR2hoZG1VZ2RtRnNkV1VLSUNBZ0lHWnlZVzFsWDJScFp5QXRNZ29nSUNBZ1lqd0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNVGc1TFRFNU1Rb2dJQ0FnTHk4Z2FXWWdLQW9nSUNBZ0x5OGdJQ0IwYUdsekxuQmhjblJwZEdsdmJuTW9ibVYzSUdGeVl6RTBNVEJmVUdGeWRHbDBhVzl1UzJWNUtIc2dhRzlzWkdWeU9pQm1jbTl0TENCd1lYSjBhWFJwYjI0NklIQmhjblJwZEdsdmJpQjlLU2t1ZG1Gc2RXVXVibUYwYVhabElEd2dZVzF2ZFc1MExtNWhkR2wyWlFvZ0lDQWdMeThnS1NCN0NpQWdJQ0JpZWlCaGNtTXhOREV3WDJOaGJsOTBjbUZ1YzJabGNsOWllVjl3WVhKMGFYUnBiMjVmWVdaMFpYSmZhV1pmWld4elpVQTBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPakU1TWkweE9UWUtJQ0FnSUM4dklISmxkSFZ5YmlCdVpYY2dZWEpqTVRReE1GOWpZVzVmZEhKaGJuTm1aWEpmWW5sZmNHRnlkR2wwYVc5dVgzSmxkSFZ5YmloN0NpQWdJQ0F2THlBZ0lHTnZaR1U2SUc1bGR5QmhjbU0wTGtKNWRHVW9NSGcxTWlrc0NpQWdJQ0F2THlBZ0lITjBZWFIxY3pvZ2JtVjNJR0Z5WXpRdVUzUnlLQ2RKYm5OMVptWnBZMmxsYm5RZ1ltRnNZVzVqWlNjcExBb2dJQ0FnTHk4Z0lDQnlaV05sYVhabGNsQmhjblJwZEdsdmJqb2dibVYzSUdGeVl6UXVRV1JrY21WemN5Z3BMQW9nSUNBZ0x5OGdmU2tLSUNBZ0lIQjFjMmhpZVhSbGN5QmlZWE5sTXpJb1MwbEJRMGRCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVXRGVXpOVVZFOVdWRWROTWt4RVRrWlRWelExUWtGTlNsRlhXVmxNVDAxT1UxRXBDaUFnSUNCbWNtRnRaVjlpZFhKNUlEQUtJQ0FnSUhKbGRITjFZZ29LWVhKak1UUXhNRjlqWVc1ZmRISmhibk5tWlhKZllubGZjR0Z5ZEdsMGFXOXVYMkZtZEdWeVgybG1YMlZzYzJWQU5Eb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNVGs1Q2lBZ0lDQXZMeUJwWmlBb2RHOGdQVDA5SUc1bGR5QmhjbU0wTGtGa1pISmxjM01vS1NrZ2V3b2dJQ0FnWm5KaGJXVmZaR2xuSUMwekNpQWdJQ0JpZVhSbFkxOHhJQzh2SUdGa1pISWdRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFWazFTRVpMVVFvZ0lDQWdQVDBLSUNBZ0lHSjZJR0Z5WXpFME1UQmZZMkZ1WDNSeVlXNXpabVZ5WDJKNVgzQmhjblJwZEdsdmJsOWhablJsY2w5cFpsOWxiSE5sUURZS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk1qQXdMVEl3TkFvZ0lDQWdMeThnY21WMGRYSnVJRzVsZHlCaGNtTXhOREV3WDJOaGJsOTBjbUZ1YzJabGNsOWllVjl3WVhKMGFYUnBiMjVmY21WMGRYSnVLSHNLSUNBZ0lDOHZJQ0FnWTI5a1pUb2dibVYzSUdGeVl6UXVRbmwwWlNnd2VEVTNLU3dLSUNBZ0lDOHZJQ0FnYzNSaGRIVnpPaUJ1WlhjZ1lYSmpOQzVUZEhJb0owbHVkbUZzYVdRZ2NtVmpaV2wyWlhJbktTd0tJQ0FnSUM4dklDQWdjbVZqWldsMlpYSlFZWEowYVhScGIyNDZJRzVsZHlCaGNtTTBMa0ZrWkhKbGMzTW9LU3dLSUNBZ0lDOHZJSDBwQ2lBZ0lDQndkWE5vWW5sMFpYTWdZbUZ6WlRNeUtFczBRVU5IUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZKUlZNelZGZE5SbGRIVTFwQ1FVOUtVMWRIV2t4S1QxcFRXRVVwQ2lBZ0lDQm1jbUZ0WlY5aWRYSjVJREFLSUNBZ0lISmxkSE4xWWdvS1lYSmpNVFF4TUY5allXNWZkSEpoYm5ObVpYSmZZbmxmY0dGeWRHbDBhVzl1WDJGbWRHVnlYMmxtWDJWc2MyVkFOam9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TWpBNENpQWdJQ0F2THlCamIyNXpkQ0J6Wlc1a1pYSkJaR1J5SUQwZ2JtVjNJR0Z5WXpRdVFXUmtjbVZ6Y3loVWVHNHVjMlZ1WkdWeUtRb2dJQ0FnZEhodUlGTmxibVJsY2dvZ0lDQWdaSFZ3Q2lBZ0lDQm1jbUZ0WlY5aWRYSjVJREFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TWpBNUNpQWdJQ0F2THlCcFppQW9jMlZ1WkdWeVFXUmtjaUFoUFQwZ1puSnZiU2tnZXdvZ0lDQWdabkpoYldWZlpHbG5JQzAxQ2lBZ0lDQWhQUW9nSUNBZ1lub2dZWEpqTVRReE1GOWpZVzVmZEhKaGJuTm1aWEpmWW5sZmNHRnlkR2wwYVc5dVgyRm1kR1Z5WDJsbVgyVnNjMlZBTVRZS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk1qRXdDaUFnSUNBdkx5QnNaWFFnWVhWMGFHOXlhWHBsWkNBOUlIUm9hWE11WVhKak1UUXhNRjlwYzE5dmNHVnlZWFJ2Y2lobWNtOXRMQ0J6Wlc1a1pYSkJaR1J5TENCd1lYSjBhWFJwYjI0cExtNWhkR2wyWlNBOVBUMGdkSEoxWlFvZ0lDQWdabkpoYldWZlpHbG5JQzAxQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dNQW9nSUNBZ1puSmhiV1ZmWkdsbklDMDBDaUFnSUNCallXeHNjM1ZpSUdGeVl6RTBNVEJmYVhOZmIzQmxjbUYwYjNJS0lDQWdJR2x1ZEdOZk1DQXZMeUF3Q2lBZ0lDQm5aWFJpYVhRS0lDQWdJR2x1ZEdOZk1TQXZMeUF4Q2lBZ0lDQTlQUW9nSUNBZ1pIVndDaUFnSUNCbWNtRnRaVjlpZFhKNUlESUtJQ0FnSUdSMWNBb2dJQ0FnWm5KaGJXVmZZblZ5ZVNBekNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qSXhNUW9nSUNBZ0x5OGdhV1lnS0NGaGRYUm9iM0pwZW1Wa0tTQjdDaUFnSUNCaWJub2dZWEpqTVRReE1GOWpZVzVmZEhKaGJuTm1aWEpmWW5sZmNHRnlkR2wwYVc5dVgyRm1kR1Z5WDJsbVgyVnNjMlZBTVRNS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk1qRXlDaUFnSUNBdkx5QmpiMjV6ZENCd1MyVjVJRDBnYm1WM0lHRnlZekUwTVRCZlQzQmxjbUYwYjNKUWIzSjBhVzl1UzJWNUtIc2dhRzlzWkdWeU9pQm1jbTl0TENCdmNHVnlZWFJ2Y2pvZ2MyVnVaR1Z5UVdSa2Npd2djR0Z5ZEdsMGFXOXVJSDBwQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVFVLSUNBZ0lHWnlZVzFsWDJScFp5QXdDaUFnSUNCamIyNWpZWFFLSUNBZ0lHWnlZVzFsWDJScFp5QXROQW9nSUNBZ1kyOXVZMkYwQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pZekNpQWdJQ0F2THlCd2RXSnNhV01nYjNCbGNtRjBiM0pRYjNKMGFXOXVRV3hzYjNkaGJtTmxjeUE5SUVKdmVFMWhjRHhoY21NeE5ERXdYMDl3WlhKaGRHOXlVRzl5ZEdsdmJrdGxlU3dnWVhKak5DNVZhVzUwVGpJMU5qNG9leUJyWlhsUWNtVm1hWGc2SUNkdmNHRW5JSDBwQ2lBZ0lDQmllWFJsWXlBeE15QXZMeUFpYjNCaElnb2dJQ0FnYzNkaGNBb2dJQ0FnWTI5dVkyRjBDaUFnSUNCa2RYQUtJQ0FnSUdaeVlXMWxYMkoxY25rZ01Rb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3lNVE1LSUNBZ0lDOHZJR2xtSUNoMGFHbHpMbTl3WlhKaGRHOXlVRzl5ZEdsdmJrRnNiRzkzWVc1alpYTW9jRXRsZVNrdVpYaHBjM1J6S1NCN0NpQWdJQ0JpYjNoZmJHVnVDaUFnSUNCaWRYSjVJREVLSUNBZ0lHWnlZVzFsWDJScFp5QXlDaUFnSUNCbWNtRnRaVjlpZFhKNUlETUtJQ0FnSUdKNklHRnlZekUwTVRCZlkyRnVYM1J5WVc1elptVnlYMko1WDNCaGNuUnBkR2x2Ymw5aFpuUmxjbDlwWmw5bGJITmxRREV6Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pJeE5Bb2dJQ0FnTHk4Z1kyOXVjM1FnY21WdFlXbHVhVzVuSUQwZ2RHaHBjeTV2Y0dWeVlYUnZjbEJ2Y25ScGIyNUJiR3h2ZDJGdVkyVnpLSEJMWlhrcExuWmhiSFZsQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dNUW9nSUNBZ1ltOTRYMmRsZEFvZ0lDQWdZWE56WlhKMElDOHZJRUp2ZUNCdGRYTjBJR2hoZG1VZ2RtRnNkV1VLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TWpFMUNpQWdJQ0F2THlCcFppQW9jbVZ0WVdsdWFXNW5MbTVoZEdsMlpTQStQU0JoYlc5MWJuUXVibUYwYVhabEtTQjdDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUSUtJQ0FnSUdJK1BRb2dJQ0FnWW5vZ1lYSmpNVFF4TUY5allXNWZkSEpoYm5ObVpYSmZZbmxmY0dGeWRHbDBhVzl1WDJGbWRHVnlYMmxtWDJWc2MyVkFNVEVLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TWpFMkNpQWdJQ0F2THlCaGRYUm9iM0pwZW1Wa0lEMGdkSEoxWlFvZ0lDQWdhVzUwWTE4eElDOHZJREVLSUNBZ0lHWnlZVzFsWDJKMWNua2dNZ29LWVhKak1UUXhNRjlqWVc1ZmRISmhibk5tWlhKZllubGZjR0Z5ZEdsMGFXOXVYMkZtZEdWeVgybG1YMlZzYzJWQU1URTZDaUFnSUNCbWNtRnRaVjlrYVdjZ01nb2dJQ0FnWm5KaGJXVmZZblZ5ZVNBekNncGhjbU14TkRFd1gyTmhibDkwY21GdWMyWmxjbDlpZVY5d1lYSjBhWFJwYjI1ZllXWjBaWEpmYVdaZlpXeHpaVUF4TXpvS0lDQWdJR1p5WVcxbFgyUnBaeUF6Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pJeU1Bb2dJQ0FnTHk4Z2FXWWdLQ0ZoZFhSb2IzSnBlbVZrS1NCN0NpQWdJQ0JpYm5vZ1lYSmpNVFF4TUY5allXNWZkSEpoYm5ObVpYSmZZbmxmY0dGeWRHbDBhVzl1WDJGbWRHVnlYMmxtWDJWc2MyVkFNVFlLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TWpJeExUSXlOUW9nSUNBZ0x5OGdjbVYwZFhKdUlHNWxkeUJoY21NeE5ERXdYMk5oYmw5MGNtRnVjMlpsY2w5aWVWOXdZWEowYVhScGIyNWZjbVYwZFhKdUtIc0tJQ0FnSUM4dklDQWdZMjlrWlRvZ2JtVjNJR0Z5WXpRdVFubDBaU2d3ZURVNEtTd0tJQ0FnSUM4dklDQWdjM1JoZEhWek9pQnVaWGNnWVhKak5DNVRkSElvSjA5d1pYSmhkRzl5SUc1dmRDQmhkWFJvYjNKcGVtVmtKeWtzQ2lBZ0lDQXZMeUFnSUhKbFkyVnBkbVZ5VUdGeWRHbDBhVzl1T2lCdVpYY2dZWEpqTkM1QlpHUnlaWE56S0Nrc0NpQWdJQ0F2THlCOUtRb2dJQ0FnY0hWemFHSjVkR1Z6SUdKaGMyVXpNaWhNUVVGRFIwRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlRGVTJORVJHVDBwUldFa3pNMU5GUWxoSE5qVkNRVTFHTWxoSk1rUlFUMHBWV0ZWYVRFVXBDaUFnSUNCbWNtRnRaVjlpZFhKNUlEQUtJQ0FnSUhKbGRITjFZZ29LWVhKak1UUXhNRjlqWVc1ZmRISmhibk5tWlhKZllubGZjR0Z5ZEdsMGFXOXVYMkZtZEdWeVgybG1YMlZzYzJWQU1UWTZDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPakl5T1FvZ0lDQWdMeThnYkdWMElISmxZMlZwZG1WeVVHRnlkR2wwYVc5dUlEMGdkR2hwY3k1ZmNtVmpaV2wyWlhKUVlYSjBhWFJwYjI0b2RHOHNJSEJoY25ScGRHbHZiaWtLSUNBZ0lHWnlZVzFsWDJScFp5QXRNd29nSUNBZ1puSmhiV1ZmWkdsbklDMDBDaUFnSUNCallXeHNjM1ZpSUY5eVpXTmxhWFpsY2xCaGNuUnBkR2x2YmdvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pveU16RXRNak0xQ2lBZ0lDQXZMeUJ5WlhSMWNtNGdibVYzSUdGeVl6RTBNVEJmWTJGdVgzUnlZVzV6Wm1WeVgySjVYM0JoY25ScGRHbHZibDl5WlhSMWNtNG9ld29nSUNBZ0x5OGdJQ0JqYjJSbE9pQnVaWGNnWVhKak5DNUNlWFJsS0RCNE5URXBMQW9nSUNBZ0x5OGdJQ0J6ZEdGMGRYTTZJRzVsZHlCaGNtTTBMbE4wY2lnblUzVmpZMlZ6Y3ljcExBb2dJQ0FnTHk4Z0lDQnlaV05sYVhabGNsQmhjblJwZEdsdmJqb2djbVZqWldsMlpYSlFZWEowYVhScGIyNHNDaUFnSUNBdkx5QjlLUW9nSUNBZ2NIVnphR0o1ZEdWeklEQjROVEV3TURJekNpQWdJQ0J6ZDJGd0NpQWdJQ0JqYjI1allYUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNak16Q2lBZ0lDQXZMeUJ6ZEdGMGRYTTZJRzVsZHlCaGNtTTBMbE4wY2lnblUzVmpZMlZ6Y3ljcExBb2dJQ0FnY0hWemFHSjVkR1Z6SURCNE1EQXdOelV6TnpVMk16WXpOalUzTXpjekNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qSXpNUzB5TXpVS0lDQWdJQzh2SUhKbGRIVnliaUJ1WlhjZ1lYSmpNVFF4TUY5allXNWZkSEpoYm5ObVpYSmZZbmxmY0dGeWRHbDBhVzl1WDNKbGRIVnliaWg3Q2lBZ0lDQXZMeUFnSUdOdlpHVTZJRzVsZHlCaGNtTTBMa0o1ZEdVb01IZzFNU2tzQ2lBZ0lDQXZMeUFnSUhOMFlYUjFjem9nYm1WM0lHRnlZelF1VTNSeUtDZFRkV05qWlhOekp5a3NDaUFnSUNBdkx5QWdJSEpsWTJWcGRtVnlVR0Z5ZEdsMGFXOXVPaUJ5WldObGFYWmxjbEJoY25ScGRHbHZiaXdLSUNBZ0lDOHZJSDBwQ2lBZ0lDQmpiMjVqWVhRS0lDQWdJR1p5WVcxbFgySjFjbmtnTUFvZ0lDQWdjbVYwYzNWaUNnb0tMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvNlFYSmpNVFF4TUM1ZmNtVmpaV2wyWlhKUVlYSjBhWFJwYjI0b2NtVmpaV2wyWlhJNklHSjVkR1Z6TENCd1lYSjBhWFJwYjI0NklHSjVkR1Z6S1NBdFBpQmllWFJsY3pvS1gzSmxZMlZwZG1WeVVHRnlkR2wwYVc5dU9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3lORE1LSUNBZ0lDOHZJSEJ5YjNSbFkzUmxaQ0JmY21WalpXbDJaWEpRWVhKMGFYUnBiMjRvY21WalpXbDJaWEk2SUdGeVl6UXVRV1JrY21WemN5d2djR0Z5ZEdsMGFXOXVPaUJoY21NMExrRmtaSEpsYzNNcE9pQmhjbU0wTGtGa1pISmxjM01nZXdvZ0lDQWdjSEp2ZEc4Z01pQXhDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPakkwTkFvZ0lDQWdMeThnYkdWMElISmxZMlZwZG1WeVVHRnlkR2wwYVc5dUlEMGdibVYzSUdGeVl6UXVRV1JrY21WemN5Z3BDaUFnSUNCaWVYUmxZMTh4SUM4dklHRmtaSElnUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVmsxU0VaTFVRb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3lORFVLSUNBZ0lDOHZJR2xtSUNoMGFHbHpMbkJoY25ScGRHbHZibk1vYm1WM0lHRnlZekUwTVRCZlVHRnlkR2wwYVc5dVMyVjVLSHNnYUc5c1pHVnlPaUJ5WldObGFYWmxjaXdnY0dGeWRHbDBhVzl1T2lCd1lYSjBhWFJwYjI0Z2ZTa3BMbVY0YVhOMGN5a2dld29nSUNBZ1puSmhiV1ZmWkdsbklDMHlDaUFnSUNCbWNtRnRaVjlrYVdjZ0xURUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem8xTndvZ0lDQWdMeThnY0hWaWJHbGpJSEJoY25ScGRHbHZibk1nUFNCQ2IzaE5ZWEE4WVhKak1UUXhNRjlRWVhKMGFYUnBiMjVMWlhrc0lHRnlZelF1VldsdWRFNHlOVFkrS0hzZ2EyVjVVSEpsWm1sNE9pQW5jQ2NnZlNrS0lDQWdJR0o1ZEdWaklEZ2dMeThnSW5BaUNpQWdJQ0J6ZDJGd0NpQWdJQ0JqYjI1allYUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNalExQ2lBZ0lDQXZMeUJwWmlBb2RHaHBjeTV3WVhKMGFYUnBiMjV6S0c1bGR5QmhjbU14TkRFd1gxQmhjblJwZEdsdmJrdGxlU2g3SUdodmJHUmxjam9nY21WalpXbDJaWElzSUhCaGNuUnBkR2x2YmpvZ2NHRnlkR2wwYVc5dUlIMHBLUzVsZUdsemRITXBJSHNLSUNBZ0lHSnZlRjlzWlc0S0lDQWdJR0oxY25rZ01Rb2dJQ0FnWW5vZ1gzSmxZMlZwZG1WeVVHRnlkR2wwYVc5dVgyRm1kR1Z5WDJsbVgyVnNjMlZBTWdvZ0lDQWdabkpoYldWZlpHbG5JQzB4Q2lBZ0lDQm1jbUZ0WlY5aWRYSjVJREFLQ2w5eVpXTmxhWFpsY2xCaGNuUnBkR2x2Ymw5aFpuUmxjbDlwWmw5bGJITmxRREk2Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pJME9Bb2dJQ0FnTHk4Z2NtVjBkWEp1SUhKbFkyVnBkbVZ5VUdGeWRHbDBhVzl1Q2lBZ0lDQm1jbUZ0WlY5a2FXY2dNQW9nSUNBZ2MzZGhjQW9nSUNBZ2NtVjBjM1ZpQ2dvS0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem82UVhKak1UUXhNQzVmWVdSa1gzQmhjblJwWTJsd1lYUnBiMjVmZEc5ZmFHOXNaR1Z5S0dodmJHUmxjam9nWW5sMFpYTXNJSEJoY25ScFkybHdZWFJwYjI0NklHSjVkR1Z6S1NBdFBpQjJiMmxrT2dwZllXUmtYM0JoY25ScFkybHdZWFJwYjI1ZmRHOWZhRzlzWkdWeU9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3lOalFLSUNBZ0lDOHZJSEJ5YjNSbFkzUmxaQ0JmWVdSa1gzQmhjblJwWTJsd1lYUnBiMjVmZEc5ZmFHOXNaR1Z5S0dodmJHUmxjam9nWVhKak5DNUJaR1J5WlhOekxDQndZWEowYVdOcGNHRjBhVzl1T2lCaGNtTTBMa0ZrWkhKbGMzTXBPaUIyYjJsa0lIc0tJQ0FnSUhCeWIzUnZJRElnTUFvZ0lDQWdhVzUwWTE4d0lDOHZJREFLSUNBZ0lHUjFjRzRnTkFvZ0lDQWdjSFZ6YUdKNWRHVnpJQ0lpQ2lBZ0lDQmtkWEJ1SURRS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk5UZ0tJQ0FnSUM4dklIQjFZbXhwWXlCb2IyeGtaWEpRWVhKMGFYUnBiMjV6UTNWeWNtVnVkRkJoWjJVZ1BTQkNiM2hOWVhBOFlYSmpOQzVCWkdSeVpYTnpMQ0JoY21NMExsVnBiblJPTmpRK0tIc2dhMlY1VUhKbFptbDRPaUFuYUhCZmNDY2dmU2tLSUNBZ0lIQjFjMmhpZVhSbGN5QWlhSEJmY0NJS0lDQWdJR1p5WVcxbFgyUnBaeUF0TWdvZ0lDQWdZMjl1WTJGMENpQWdJQ0JrZFhBS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk1qWTJDaUFnSUNBdkx5QnBaaUFvSVhSb2FYTXVhRzlzWkdWeVVHRnlkR2wwYVc5dWMwTjFjbkpsYm5SUVlXZGxLR2h2YkdSbGNpa3VaWGhwYzNSektTQjdDaUFnSUNCaWIzaGZiR1Z1Q2lBZ0lDQmlkWEo1SURFS0lDQWdJR0p1ZWlCZllXUmtYM0JoY25ScFkybHdZWFJwYjI1ZmRHOWZhRzlzWkdWeVgyRm1kR1Z5WDJsbVgyVnNjMlZBTWdvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pveU5qY0tJQ0FnSUM4dklIUm9hWE11YUc5c1pHVnlVR0Z5ZEdsMGFXOXVjME4xY25KbGJuUlFZV2RsS0dodmJHUmxjaWt1ZG1Gc2RXVWdQU0J3WVdkbENpQWdJQ0JtY21GdFpWOWthV2NnTVRBS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk1qWTFDaUFnSUNBdkx5QnNaWFFnY0dGblpTQTlJRzVsZHlCaGNtTTBMbFZwYm5ST05qUW9NQ2tLSUNBZ0lHSjVkR1ZqSURJd0lDOHZJREI0TURBd01EQXdNREF3TURBd01EQXdNQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem95TmpjS0lDQWdJQzh2SUhSb2FYTXVhRzlzWkdWeVVHRnlkR2wwYVc5dWMwTjFjbkpsYm5SUVlXZGxLR2h2YkdSbGNpa3VkbUZzZFdVZ1BTQndZV2RsQ2lBZ0lDQmliM2hmY0hWMENncGZZV1JrWDNCaGNuUnBZMmx3WVhScGIyNWZkRzlmYUc5c1pHVnlYMkZtZEdWeVgybG1YMlZzYzJWQU1qb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNalk1Q2lBZ0lDQXZMeUJqYjI1emRDQnNZWE4wVUdGblpTQTlJSFJvYVhNdWFHOXNaR1Z5VUdGeWRHbDBhVzl1YzBOMWNuSmxiblJRWVdkbEtHaHZiR1JsY2lrdWRtRnNkV1VLSUNBZ0lHWnlZVzFsWDJScFp5QXhNQW9nSUNBZ1ltOTRYMmRsZEFvZ0lDQWdjM2RoY0FvZ0lDQWdabkpoYldWZlluVnllU0F5Q2lBZ0lDQmhjM05sY25RZ0x5OGdRbTk0SUcxMWMzUWdhR0YyWlNCMllXeDFaUW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem95TnpBS0lDQWdJQzh2SUd4bGRDQm1iM1Z1WkNBOUlHWmhiSE5sQ2lBZ0lDQnBiblJqWHpBZ0x5OGdNQW9nSUNBZ1puSmhiV1ZmWW5WeWVTQTJDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPakkyTlFvZ0lDQWdMeThnYkdWMElIQmhaMlVnUFNCdVpYY2dZWEpqTkM1VmFXNTBUalkwS0RBcENpQWdJQ0JpZVhSbFl5QXlNQ0F2THlBd2VEQXdNREF3TURBd01EQXdNREF3TURBS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk1qY3hDaUFnSUNBdkx5Qm1iM0lnS0d4bGRDQmpkWEpRWVdkbElEMGdjR0ZuWlRzZ1kzVnlVR0ZuWlM1dVlYUnBkbVVnUENCc1lYTjBVR0ZuWlM1dVlYUnBkbVU3SUdOMWNsQmhaMlVnUFNCdVpYY2dZWEpqTkM1VmFXNTBUalkwS0dOMWNsQmhaMlV1Ym1GMGFYWmxJQ3NnTVNrcElIc0tJQ0FnSUdaeVlXMWxYMkoxY25rZ01Rb0tYMkZrWkY5d1lYSjBhV05wY0dGMGFXOXVYM1J2WDJodmJHUmxjbDkzYUdsc1pWOTBiM0JBTXpvS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk1qY3hDaUFnSUNBdkx5Qm1iM0lnS0d4bGRDQmpkWEpRWVdkbElEMGdjR0ZuWlRzZ1kzVnlVR0ZuWlM1dVlYUnBkbVVnUENCc1lYTjBVR0ZuWlM1dVlYUnBkbVU3SUdOMWNsQmhaMlVnUFNCdVpYY2dZWEpqTkM1VmFXNTBUalkwS0dOMWNsQmhaMlV1Ym1GMGFYWmxJQ3NnTVNrcElIc0tJQ0FnSUdaeVlXMWxYMlJwWnlBeENpQWdJQ0JpZEc5cENpQWdJQ0JrZFhBS0lDQWdJR1p5WVcxbFgySjFjbmtnT0FvZ0lDQWdabkpoYldWZlpHbG5JRElLSUNBZ0lHSjBiMmtLSUNBZ0lHUjFjQW9nSUNBZ1puSmhiV1ZmWW5WeWVTQTVDaUFnSUNBOENpQWdJQ0JpZWlCZllXUmtYM0JoY25ScFkybHdZWFJwYjI1ZmRHOWZhRzlzWkdWeVgySnNiMk5yUURFd0NpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qSTNNaTB5TnpVS0lDQWdJQzh2SUdOdmJuTjBJSEJoWjJsdVlYUmxaRXRsZVNBOUlHNWxkeUJoY21NeE5ERXdYMGh2YkdScGJtZFFZWEowYVhScGIyNXpVR0ZuYVc1aGRHVmtTMlY1S0hzS0lDQWdJQzh2SUNBZ2FHOXNaR1Z5T2lCb2IyeGtaWElzQ2lBZ0lDQXZMeUFnSUhCaFoyVTZJR04xY2xCaFoyVXNDaUFnSUNBdkx5QjlLUW9nSUNBZ1puSmhiV1ZmWkdsbklDMHlDaUFnSUNCbWNtRnRaVjlrYVdjZ01Rb2dJQ0FnWTI5dVkyRjBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPall3Q2lBZ0lDQXZMeUJyWlhsUWNtVm1hWGc2SUNkb2NGOWhKeXdLSUNBZ0lHSjVkR1ZqSURFMklDOHZJQ0pvY0Y5aElnb2dJQ0FnYzNkaGNBb2dJQ0FnWTI5dVkyRjBDaUFnSUNCa2RYQUtJQ0FnSUdaeVlXMWxYMkoxY25rZ05Bb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3lOellLSUNBZ0lDOHZJR2xtSUNnaGRHaHBjeTVvYjJ4a1pYSlFZWEowYVhScGIyNXpRV1JrY21WemMyVnpLSEJoWjJsdVlYUmxaRXRsZVNrdVpYaHBjM1J6S1NCN0NpQWdJQ0JpYjNoZmJHVnVDaUFnSUNCaWRYSjVJREVLSUNBZ0lHSnVlaUJmWVdSa1gzQmhjblJwWTJsd1lYUnBiMjVmZEc5ZmFHOXNaR1Z5WDJGbWRHVnlYMmxtWDJWc2MyVkFOZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem95TnpjS0lDQWdJQzh2SUhSb2FYTXVhRzlzWkdWeVVHRnlkR2wwYVc5dWMwRmtaSEpsYzNObGN5aHdZV2RwYm1GMFpXUkxaWGtwTG5aaGJIVmxJRDBnVzNCaGNuUnBZMmx3WVhScGIyNWRDaUFnSUNCaWVYUmxZeUF5TXlBdkx5QXdlREF3TURFS0lDQWdJR1p5WVcxbFgyUnBaeUF0TVFvZ0lDQWdZMjl1WTJGMENpQWdJQ0JtY21GdFpWOWthV2NnTkFvZ0lDQWdaSFZ3Q2lBZ0lDQmliM2hmWkdWc0NpQWdJQ0J3YjNBS0lDQWdJSE4zWVhBS0lDQWdJR0p2ZUY5d2RYUUtDbDloWkdSZmNHRnlkR2xqYVhCaGRHbHZibDkwYjE5b2IyeGtaWEpmWVdaMFpYSmZhV1pmWld4elpVQTJPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem95T0RBS0lDQWdJQzh2SUdsbUlDaDBhR2x6TG1OdmJuUmhhVzV6UVdSa2NtVnpjeWgwYUdsekxtaHZiR1JsY2xCaGNuUnBkR2x2Ym5OQlpHUnlaWE56WlhNb2NHRm5hVzVoZEdWa1MyVjVLUzUyWVd4MVpTd2djR0Z5ZEdsamFYQmhkR2x2YmlrcElIc0tJQ0FnSUdaeVlXMWxYMlJwWnlBMENpQWdJQ0JpYjNoZloyVjBDaUFnSUNCemQyRndDaUFnSUNCa2RYQUtJQ0FnSUdOdmRtVnlJRElLSUNBZ0lHWnlZVzFsWDJKMWNua2dNQW9nSUNBZ1lYTnpaWEowSUM4dklFSnZlQ0J0ZFhOMElHaGhkbVVnZG1Gc2RXVUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNalUwQ2lBZ0lDQXZMeUJtYjNJZ0tHTnZibk4wSUhZZ2IyWWdZU2tnZXdvZ0lDQWdhVzUwWTE4d0lDOHZJREFLSUNBZ0lHVjRkSEpoWTNSZmRXbHVkREUyQ2lBZ0lDQm1jbUZ0WlY5aWRYSjVJRFVLSUNBZ0lHbHVkR05mTUNBdkx5QXdDaUFnSUNCbWNtRnRaVjlpZFhKNUlEY0tDbDloWkdSZmNHRnlkR2xqYVhCaGRHbHZibDkwYjE5b2IyeGtaWEpmWm05eVgyaGxZV1JsY2tBeE56b0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNalUwQ2lBZ0lDQXZMeUJtYjNJZ0tHTnZibk4wSUhZZ2IyWWdZU2tnZXdvZ0lDQWdabkpoYldWZlpHbG5JRGNLSUNBZ0lHWnlZVzFsWDJScFp5QTFDaUFnSUNBOENpQWdJQ0JpZWlCZllXUmtYM0JoY25ScFkybHdZWFJwYjI1ZmRHOWZhRzlzWkdWeVgyRm1kR1Z5WDJadmNrQXlNUW9nSUNBZ1puSmhiV1ZmWkdsbklEQUtJQ0FnSUdWNGRISmhZM1FnTWlBd0NpQWdJQ0JtY21GdFpWOWthV2NnTndvZ0lDQWdhVzUwWTE4eUlDOHZJRE15Q2lBZ0lDQXFDaUFnSUNCcGJuUmpYeklnTHk4Z016SUtJQ0FnSUdWNGRISmhZM1F6SUM4dklHOXVJR1Z5Y205eU9pQkpibVJsZUNCaFkyTmxjM01nYVhNZ2IzVjBJRzltSUdKdmRXNWtjd29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem95TlRVS0lDQWdJQzh2SUdsbUlDaDJJRDA5UFNCNEtTQnlaWFIxY200Z2RISjFaUW9nSUNBZ1puSmhiV1ZmWkdsbklDMHhDaUFnSUNBOVBRb2dJQ0FnWW5vZ1gyRmtaRjl3WVhKMGFXTnBjR0YwYVc5dVgzUnZYMmh2YkdSbGNsOWhablJsY2w5cFpsOWxiSE5sUURJd0NpQWdJQ0JwYm5Salh6RWdMeThnTVFvS1gyRmtaRjl3WVhKMGFXTnBjR0YwYVc5dVgzUnZYMmh2YkdSbGNsOWhablJsY2w5cGJteHBibVZrWDNOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk9rRnlZekUwTVRBdVkyOXVkR0ZwYm5OQlpHUnlaWE56UURJeU9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3lPREFLSUNBZ0lDOHZJR2xtSUNoMGFHbHpMbU52Ym5SaGFXNXpRV1JrY21WemN5aDBhR2x6TG1odmJHUmxjbEJoY25ScGRHbHZibk5CWkdSeVpYTnpaWE1vY0dGbmFXNWhkR1ZrUzJWNUtTNTJZV3gxWlN3Z2NHRnlkR2xqYVhCaGRHbHZiaWtwSUhzS0lDQWdJR0o2SUY5aFpHUmZjR0Z5ZEdsamFYQmhkR2x2Ymw5MGIxOW9iMnhrWlhKZllXWjBaWEpmYVdaZlpXeHpaVUE0Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pJNE1Rb2dJQ0FnTHk4Z1ptOTFibVFnUFNCMGNuVmxDaUFnSUNCcGJuUmpYekVnTHk4Z01Rb2dJQ0FnWm5KaGJXVmZZblZ5ZVNBMkNncGZZV1JrWDNCaGNuUnBZMmx3WVhScGIyNWZkRzlmYUc5c1pHVnlYMkpzYjJOclFERXdPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem95T0RVS0lDQWdJQzh2SUdsbUlDZ2habTkxYm1RcElIc0tJQ0FnSUdaeVlXMWxYMlJwWnlBMkNpQWdJQ0JpYm5vZ1gyRmtaRjl3WVhKMGFXTnBjR0YwYVc5dVgzUnZYMmh2YkdSbGNsOWhablJsY2w5cFpsOWxiSE5sUURFMUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qSTROaTB5T0RrS0lDQWdJQzh2SUdOdmJuTjBJSEJoWjJsdVlYUmxaRXRsZVNBOUlHNWxkeUJoY21NeE5ERXdYMGh2YkdScGJtZFFZWEowYVhScGIyNXpVR0ZuYVc1aGRHVmtTMlY1S0hzS0lDQWdJQzh2SUNBZ2FHOXNaR1Z5T2lCb2IyeGtaWElzQ2lBZ0lDQXZMeUFnSUhCaFoyVTZJR3hoYzNSUVlXZGxMQW9nSUNBZ0x5OGdmU2tLSUNBZ0lHWnlZVzFsWDJScFp5QXRNZ29nSUNBZ1puSmhiV1ZmWkdsbklESUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem8yTUFvZ0lDQWdMeThnYTJWNVVISmxabWw0T2lBbmFIQmZZU2NzQ2lBZ0lDQmllWFJsWXlBeE5pQXZMeUFpYUhCZllTSUtJQ0FnSUhOM1lYQUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ1pIVndDaUFnSUNCbWNtRnRaVjlpZFhKNUlETUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNamt3Q2lBZ0lDQXZMeUJqYjI1emRDQnBkR1Z0YzBOdmRXNTBJRDBnYm1WM0lHRnlZelF1VldsdWRFNDJOQ2gwYUdsekxtaHZiR1JsY2xCaGNuUnBkR2x2Ym5OQlpHUnlaWE56WlhNb2NHRm5hVzVoZEdWa1MyVjVLUzUyWVd4MVpTNXNaVzVuZEdncENpQWdJQ0JpYjNoZloyVjBDaUFnSUNCaGMzTmxjblFnTHk4Z1FtOTRJRzExYzNRZ2FHRjJaU0IyWVd4MVpRb2dJQ0FnYVc1MFkxOHdJQzh2SURBS0lDQWdJR1Y0ZEhKaFkzUmZkV2x1ZERFMkNpQWdJQ0JwZEc5aUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qSTVNUW9nSUNBZ0x5OGdhV1lnS0dsMFpXMXpRMjkxYm5RdWJtRjBhWFpsSUR3Z01UQXBJSHNLSUNBZ0lHSjBiMmtLSUNBZ0lIQjFjMmhwYm5RZ01UQWdMeThnTVRBS0lDQWdJRHdLSUNBZ0lHSjZJRjloWkdSZmNHRnlkR2xqYVhCaGRHbHZibDkwYjE5b2IyeGtaWEpmWld4elpWOWliMlI1UURFekNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qSTVOQW9nSUNBZ0x5OGdMaTR1ZEdocGN5NW9iMnhrWlhKUVlYSjBhWFJwYjI1elFXUmtjbVZ6YzJWektIQmhaMmx1WVhSbFpFdGxlU2t1ZG1Gc2RXVXNDaUFnSUNCbWNtRnRaVjlrYVdjZ013b2dJQ0FnWkhWd0NpQWdJQ0JpYjNoZloyVjBDaUFnSUNCaGMzTmxjblFnTHk4Z1FtOTRJRzExYzNRZ2FHRjJaU0IyWVd4MVpRb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3lPVE10TWprMkNpQWdJQ0F2THlCMGFHbHpMbWh2YkdSbGNsQmhjblJwZEdsdmJuTkJaR1J5WlhOelpYTW9jR0ZuYVc1aGRHVmtTMlY1S1M1MllXeDFaU0E5SUZzS0lDQWdJQzh2SUNBZ0xpNHVkR2hwY3k1b2IyeGtaWEpRWVhKMGFYUnBiMjV6UVdSa2NtVnpjMlZ6S0hCaFoybHVZWFJsWkV0bGVTa3VkbUZzZFdVc0NpQWdJQ0F2THlBZ0lIQmhjblJwWTJsd1lYUnBiMjRzQ2lBZ0lDQXZMeUJkQ2lBZ0lDQmxlSFJ5WVdOMElESWdNQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem95T1RVS0lDQWdJQzh2SUhCaGNuUnBZMmx3WVhScGIyNHNDaUFnSUNCaWVYUmxZeUF5TXlBdkx5QXdlREF3TURFS0lDQWdJR1p5WVcxbFgyUnBaeUF0TVFvZ0lDQWdZMjl1WTJGMENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qSTVNeTB5T1RZS0lDQWdJQzh2SUhSb2FYTXVhRzlzWkdWeVVHRnlkR2wwYVc5dWMwRmtaSEpsYzNObGN5aHdZV2RwYm1GMFpXUkxaWGtwTG5aaGJIVmxJRDBnV3dvZ0lDQWdMeThnSUNBdUxpNTBhR2x6TG1odmJHUmxjbEJoY25ScGRHbHZibk5CWkdSeVpYTnpaWE1vY0dGbmFXNWhkR1ZrUzJWNUtTNTJZV3gxWlN3S0lDQWdJQzh2SUNBZ2NHRnlkR2xqYVhCaGRHbHZiaXdLSUNBZ0lDOHZJRjBLSUNBZ0lHVjRkSEpoWTNRZ01pQXdDaUFnSUNCamIyNWpZWFFLSUNBZ0lHUjFjQW9nSUNBZ2JHVnVDaUFnSUNCcGJuUmpYeklnTHk4Z016SUtJQ0FnSUM4S0lDQWdJR2wwYjJJS0lDQWdJR1Y0ZEhKaFkzUWdOaUF5Q2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR1JwWnlBeENpQWdJQ0JpYjNoZlpHVnNDaUFnSUNCd2IzQUtJQ0FnSUdKdmVGOXdkWFFLQ2w5aFpHUmZjR0Z5ZEdsamFYQmhkR2x2Ymw5MGIxOW9iMnhrWlhKZllXWjBaWEpmYVdaZlpXeHpaVUF4TlRvS0lDQWdJSEpsZEhOMVlnb0tYMkZrWkY5d1lYSjBhV05wY0dGMGFXOXVYM1J2WDJodmJHUmxjbDlsYkhObFgySnZaSGxBTVRNNkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qSTVPQW9nSUNBZ0x5OGdZMjl1YzNRZ2JtVjNUR0Z6ZEZCaFoyVWdQU0J1WlhjZ1lYSmpOQzVWYVc1MFRqWTBLR3hoYzNSUVlXZGxMbTVoZEdsMlpTQXJJREVwQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dPUW9nSUNBZ2FXNTBZMTh4SUM4dklERUtJQ0FnSUNzS0lDQWdJR2wwYjJJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk1qazVDaUFnSUNBdkx5QjBhR2x6TG1odmJHUmxjbEJoY25ScGRHbHZibk5EZFhKeVpXNTBVR0ZuWlNob2IyeGtaWElwTG5aaGJIVmxJRDBnYm1WM1RHRnpkRkJoWjJVS0lDQWdJR1p5WVcxbFgyUnBaeUF4TUFvZ0lDQWdaR2xuSURFS0lDQWdJR0p2ZUY5d2RYUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNekF3TFRNd013b2dJQ0FnTHk4Z1kyOXVjM1FnYm1WM1VHRm5hVzVoZEdWa1MyVjVJRDBnYm1WM0lHRnlZekUwTVRCZlNHOXNaR2x1WjFCaGNuUnBkR2x2Ym5OUVlXZHBibUYwWldSTFpYa29ld29nSUNBZ0x5OGdJQ0JvYjJ4a1pYSTZJR2h2YkdSbGNpd0tJQ0FnSUM4dklDQWdjR0ZuWlRvZ2JtVjNUR0Z6ZEZCaFoyVXNDaUFnSUNBdkx5QjlLUW9nSUNBZ1puSmhiV1ZmWkdsbklDMHlDaUFnSUNCemQyRndDaUFnSUNCamIyNWpZWFFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TXpBMENpQWdJQ0F2THlCMGFHbHpMbWh2YkdSbGNsQmhjblJwZEdsdmJuTkJaR1J5WlhOelpYTW9ibVYzVUdGbmFXNWhkR1ZrUzJWNUtTNTJZV3gxWlNBOUlGdHdZWEowYVdOcGNHRjBhVzl1WFFvZ0lDQWdZbmwwWldNZ01qTWdMeThnTUhnd01EQXhDaUFnSUNCbWNtRnRaVjlrYVdjZ0xURUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem8yTUFvZ0lDQWdMeThnYTJWNVVISmxabWw0T2lBbmFIQmZZU2NzQ2lBZ0lDQmllWFJsWXlBeE5pQXZMeUFpYUhCZllTSUtJQ0FnSUhWdVkyOTJaWElnTWdvZ0lDQWdZMjl1WTJGMENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qTXdOQW9nSUNBZ0x5OGdkR2hwY3k1b2IyeGtaWEpRWVhKMGFYUnBiMjV6UVdSa2NtVnpjMlZ6S0c1bGQxQmhaMmx1WVhSbFpFdGxlU2t1ZG1Gc2RXVWdQU0JiY0dGeWRHbGphWEJoZEdsdmJsMEtJQ0FnSUdSMWNBb2dJQ0FnWW05NFgyUmxiQW9nSUNBZ2NHOXdDaUFnSUNCemQyRndDaUFnSUNCaWIzaGZjSFYwQ2lBZ0lDQnlaWFJ6ZFdJS0NsOWhaR1JmY0dGeWRHbGphWEJoZEdsdmJsOTBiMTlvYjJ4a1pYSmZZV1owWlhKZmFXWmZaV3h6WlVBNE9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3lOekVLSUNBZ0lDOHZJR1p2Y2lBb2JHVjBJR04xY2xCaFoyVWdQU0J3WVdkbE95QmpkWEpRWVdkbExtNWhkR2wyWlNBOElHeGhjM1JRWVdkbExtNWhkR2wyWlRzZ1kzVnlVR0ZuWlNBOUlHNWxkeUJoY21NMExsVnBiblJPTmpRb1kzVnlVR0ZuWlM1dVlYUnBkbVVnS3lBeEtTa2dld29nSUNBZ1puSmhiV1ZmWkdsbklEZ0tJQ0FnSUdsdWRHTmZNU0F2THlBeENpQWdJQ0FyQ2lBZ0lDQnBkRzlpQ2lBZ0lDQm1jbUZ0WlY5aWRYSjVJREVLSUNBZ0lHSWdYMkZrWkY5d1lYSjBhV05wY0dGMGFXOXVYM1J2WDJodmJHUmxjbDkzYUdsc1pWOTBiM0JBTXdvS1gyRmtaRjl3WVhKMGFXTnBjR0YwYVc5dVgzUnZYMmh2YkdSbGNsOWhablJsY2w5cFpsOWxiSE5sUURJd09nb2dJQ0FnWm5KaGJXVmZaR2xuSURjS0lDQWdJR2x1ZEdOZk1TQXZMeUF4Q2lBZ0lDQXJDaUFnSUNCbWNtRnRaVjlpZFhKNUlEY0tJQ0FnSUdJZ1gyRmtaRjl3WVhKMGFXTnBjR0YwYVc5dVgzUnZYMmh2YkdSbGNsOW1iM0pmYUdWaFpHVnlRREUzQ2dwZllXUmtYM0JoY25ScFkybHdZWFJwYjI1ZmRHOWZhRzlzWkdWeVgyRm1kR1Z5WDJadmNrQXlNVG9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TWpVM0NpQWdJQ0F2THlCeVpYUjFjbTRnWm1Gc2MyVUtJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qSTRNQW9nSUNBZ0x5OGdhV1lnS0hSb2FYTXVZMjl1ZEdGcGJuTkJaR1J5WlhOektIUm9hWE11YUc5c1pHVnlVR0Z5ZEdsMGFXOXVjMEZrWkhKbGMzTmxjeWh3WVdkcGJtRjBaV1JMWlhrcExuWmhiSFZsTENCd1lYSjBhV05wY0dGMGFXOXVLU2tnZXdvZ0lDQWdZaUJmWVdSa1gzQmhjblJwWTJsd1lYUnBiMjVmZEc5ZmFHOXNaR1Z5WDJGbWRHVnlYMmx1YkdsdVpXUmZjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem82UVhKak1UUXhNQzVqYjI1MFlXbHVjMEZrWkhKbGMzTkFNaklLQ2dvdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPanBCY21NeE5ERXdMbDkwY21GdWMyWmxjbDl3WVhKMGFYUnBiMjRvWm5KdmJUb2dZbmwwWlhNc0lHWnliMjFRWVhKMGFYUnBiMjQ2SUdKNWRHVnpMQ0IwYnpvZ1lubDBaWE1zSUhSdlVHRnlkR2wwYVc5dU9pQmllWFJsY3l3Z1lXMXZkVzUwT2lCaWVYUmxjeXdnWkdGMFlUb2dZbmwwWlhNcElDMCtJSFp2YVdRNkNsOTBjbUZ1YzJabGNsOXdZWEowYVhScGIyNDZDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPak14T0Mwek1qVUtJQ0FnSUM4dklIQnliM1JsWTNSbFpDQmZkSEpoYm5ObVpYSmZjR0Z5ZEdsMGFXOXVLQW9nSUNBZ0x5OGdJQ0JtY205dE9pQmhjbU0wTGtGa1pISmxjM01zQ2lBZ0lDQXZMeUFnSUdaeWIyMVFZWEowYVhScGIyNDZJR0Z5WXpRdVFXUmtjbVZ6Y3l3S0lDQWdJQzh2SUNBZ2RHODZJR0Z5WXpRdVFXUmtjbVZ6Y3l3S0lDQWdJQzh2SUNBZ2RHOVFZWEowYVhScGIyNDZJR0Z5WXpRdVFXUmtjbVZ6Y3l3S0lDQWdJQzh2SUNBZ1lXMXZkVzUwT2lCaGNtTTBMbFZwYm5ST01qVTJMQW9nSUNBZ0x5OGdJQ0JrWVhSaE9pQmhjbU0wTGtSNWJtRnRhV05DZVhSbGN5d0tJQ0FnSUM4dklDazZJSFp2YVdRZ2V3b2dJQ0FnY0hKdmRHOGdOaUF3Q2lBZ0lDQnBiblJqWHpBZ0x5OGdNQW9nSUNBZ1pIVndDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPak15TmdvZ0lDQWdMeThnWVhOelpYSjBLR0Z0YjNWdWRDNXVZWFJwZG1VZ1BpQXdMQ0FuU1c1MllXeHBaQ0JoYlc5MWJuUW5LUW9nSUNBZ1puSmhiV1ZmWkdsbklDMHlDaUFnSUNCd2RYTm9ZbmwwWlhNZ01IZ0tJQ0FnSUdJK0NpQWdJQ0JoYzNObGNuUWdMeThnU1c1MllXeHBaQ0JoYlc5MWJuUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNekk0Q2lBZ0lDQXZMeUJqYjI1emRDQm1jbTl0UzJWNUlEMGdibVYzSUdGeVl6RTBNVEJmVUdGeWRHbDBhVzl1UzJWNUtIc2dhRzlzWkdWeU9pQm1jbTl0TENCd1lYSjBhWFJwYjI0NklHWnliMjFRWVhKMGFYUnBiMjRnZlNrS0lDQWdJR1p5WVcxbFgyUnBaeUF0TmdvZ0lDQWdabkpoYldWZlpHbG5JQzAxQ2lBZ0lDQmpiMjVqWVhRS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk5UY0tJQ0FnSUM4dklIQjFZbXhwWXlCd1lYSjBhWFJwYjI1eklEMGdRbTk0VFdGd1BHRnlZekUwTVRCZlVHRnlkR2wwYVc5dVMyVjVMQ0JoY21NMExsVnBiblJPTWpVMlBpaDdJR3RsZVZCeVpXWnBlRG9nSjNBbklIMHBDaUFnSUNCaWVYUmxZeUE0SUM4dklDSndJZ29nSUNBZ2MzZGhjQW9nSUNBZ1kyOXVZMkYwQ2lBZ0lDQmtkWEFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TXpJNUNpQWdJQ0F2THlCcFppQW9JWFJvYVhNdWNHRnlkR2wwYVc5dWN5aG1jbTl0UzJWNUtTNWxlR2x6ZEhNcElIc0tJQ0FnSUdKdmVGOXNaVzRLSUNBZ0lHSjFjbmtnTVFvZ0lDQWdZbTU2SUY5MGNtRnVjMlpsY2w5d1lYSjBhWFJwYjI1ZllXWjBaWEpmYVdaZlpXeHpaVUF5Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pNek1Bb2dJQ0FnTHk4Z2RHaHBjeTV3WVhKMGFYUnBiMjV6S0daeWIyMUxaWGtwTG5aaGJIVmxJRDBnYm1WM0lHRnlZelF1VldsdWRFNHlOVFlvTUNrS0lDQWdJR1p5WVcxbFgyUnBaeUF5Q2lBZ0lDQmllWFJsWTE4eElDOHZJREI0TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TUFvZ0lDQWdZbTk0WDNCMWRBb0tYM1J5WVc1elptVnlYM0JoY25ScGRHbHZibDloWm5SbGNsOXBabDlsYkhObFFESTZDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPak16TWdvZ0lDQWdMeThnZEdocGN5NXdZWEowYVhScGIyNXpLR1p5YjIxTFpYa3BMblpoYkhWbElEMGdibVYzSUdGeVl6UXVWV2x1ZEU0eU5UWW9kR2hwY3k1d1lYSjBhWFJwYjI1ektHWnliMjFMWlhrcExuWmhiSFZsTG01aGRHbDJaU0F0SUdGdGIzVnVkQzV1WVhScGRtVXBDaUFnSUNCbWNtRnRaVjlrYVdjZ01nb2dJQ0FnWkhWd0NpQWdJQ0JpYjNoZloyVjBDaUFnSUNCaGMzTmxjblFnTHk4Z1FtOTRJRzExYzNRZ2FHRjJaU0IyWVd4MVpRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0JpTFFvZ0lDQWdaSFZ3Q2lBZ0lDQnNaVzRLSUNBZ0lHbHVkR05mTWlBdkx5QXpNZ29nSUNBZ1BEMEtJQ0FnSUdGemMyVnlkQ0F2THlCdmRtVnlabXh2ZHdvZ0lDQWdhVzUwWTE4eUlDOHZJRE15Q2lBZ0lDQmllbVZ5YndvZ0lDQWdaSFZ3Q2lBZ0lDQm1jbUZ0WlY5aWRYSjVJREFLSUNBZ0lHSjhDaUFnSUNCaWIzaGZjSFYwQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pNek55MHpORE1LSUNBZ0lDOHZJRzVsZHlCaGNtTXhOREV3WDNCaGNuUnBkR2x2Ymw5MGNtRnVjMlpsY2loN0NpQWdJQ0F2THlBZ0lHWnliMjA2SUdaeWIyMHNDaUFnSUNBdkx5QWdJSFJ2T2lCMGJ5d0tJQ0FnSUM4dklDQWdjR0Z5ZEdsMGFXOXVPaUJtY205dFVHRnlkR2wwYVc5dUxBb2dJQ0FnTHk4Z0lDQmhiVzkxYm5RNklHRnRiM1Z1ZEN3S0lDQWdJQzh2SUNBZ1pHRjBZVG9nWkdGMFlTd0tJQ0FnSUM4dklIMHBMQW9nSUNBZ1puSmhiV1ZmWkdsbklDMDJDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUUUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ1puSmhiV1ZmWkdsbklDMDFDaUFnSUNCamIyNWpZWFFLSUNBZ0lHWnlZVzFsWDJScFp5QXRNZ29nSUNBZ1kyOXVZMkYwQ2lBZ0lDQndkWE5vWW5sMFpYTWdNSGd3TURneUNpQWdJQ0JqYjI1allYUUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE1Rb2dJQ0FnWTI5dVkyRjBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPak16TlMwek5EUUtJQ0FnSUM4dklHVnRhWFFvQ2lBZ0lDQXZMeUFnSUNkVWNtRnVjMlpsY2ljc0NpQWdJQ0F2THlBZ0lHNWxkeUJoY21NeE5ERXdYM0JoY25ScGRHbHZibDkwY21GdWMyWmxjaWg3Q2lBZ0lDQXZMeUFnSUNBZ1puSnZiVG9nWm5KdmJTd0tJQ0FnSUM4dklDQWdJQ0IwYnpvZ2RHOHNDaUFnSUNBdkx5QWdJQ0FnY0dGeWRHbDBhVzl1T2lCbWNtOXRVR0Z5ZEdsMGFXOXVMQW9nSUNBZ0x5OGdJQ0FnSUdGdGIzVnVkRG9nWVcxdmRXNTBMQW9nSUNBZ0x5OGdJQ0FnSUdSaGRHRTZJR1JoZEdFc0NpQWdJQ0F2THlBZ0lIMHBMQW9nSUNBZ0x5OGdLUW9nSUNBZ1lubDBaV01nTmlBdkx5QXdlREF3TURJS0lDQWdJSE4zWVhBS0lDQWdJR052Ym1OaGRBb2dJQ0FnY0hWemFHSjVkR1Z6SURCNE1qQTJZamM1TkRBZ0x5OGdiV1YwYUc5a0lDSlVjbUZ1YzJabGNpZ29ZV1JrY21WemN5eGhaR1J5WlhOekxHRmtaSEpsYzNNc2RXbHVkREkxTml4aWVYUmxXMTBwS1NJS0lDQWdJSE4zWVhBS0lDQWdJR052Ym1OaGRBb2dJQ0FnYkc5bkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qTTBOd29nSUNBZ0x5OGdhV1lnS0hSdlVHRnlkR2wwYVc5dUlDRTlQU0JtY205dFVHRnlkR2wwYVc5dUtTQjdDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUTUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE5Rb2dJQ0FnSVQwS0lDQWdJR0o2SUY5MGNtRnVjMlpsY2w5d1lYSjBhWFJwYjI1ZllXWjBaWEpmYVdaZlpXeHpaVUEwQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pNME9Bb2dJQ0FnTHk4Z2RHaHBjeTVmWVdSa1gzQmhjblJwWTJsd1lYUnBiMjVmZEc5ZmFHOXNaR1Z5S0hSdkxDQjBiMUJoY25ScGRHbHZiaWtLSUNBZ0lHWnlZVzFsWDJScFp5QXROQW9nSUNBZ1puSmhiV1ZmWkdsbklDMHpDaUFnSUNCallXeHNjM1ZpSUY5aFpHUmZjR0Z5ZEdsamFYQmhkR2x2Ymw5MGIxOW9iMnhrWlhJS0NsOTBjbUZ1YzJabGNsOXdZWEowYVhScGIyNWZZV1owWlhKZmFXWmZaV3h6WlVBME9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3pOVElLSUNBZ0lDOHZJR052Ym5OMElIUnZTMlY1SUQwZ2JtVjNJR0Z5WXpFME1UQmZVR0Z5ZEdsMGFXOXVTMlY1S0hzZ2FHOXNaR1Z5T2lCMGJ5d2djR0Z5ZEdsMGFXOXVPaUIwYjFCaGNuUnBkR2x2YmlCOUtRb2dJQ0FnWm5KaGJXVmZaR2xuSUMwMENpQWdJQ0JtY21GdFpWOWthV2NnTFRNS0lDQWdJR052Ym1OaGRBb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6bzFOd29nSUNBZ0x5OGdjSFZpYkdsaklIQmhjblJwZEdsdmJuTWdQU0JDYjNoTllYQThZWEpqTVRReE1GOVFZWEowYVhScGIyNUxaWGtzSUdGeVl6UXVWV2x1ZEU0eU5UWStLSHNnYTJWNVVISmxabWw0T2lBbmNDY2dmU2tLSUNBZ0lHSjVkR1ZqSURnZ0x5OGdJbkFpQ2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR1IxY0FvZ0lDQWdabkpoYldWZlluVnllU0F4Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pNMU13b2dJQ0FnTHk4Z2FXWWdLQ0YwYUdsekxuQmhjblJwZEdsdmJuTW9kRzlMWlhrcExtVjRhWE4wY3lrZ2V3b2dJQ0FnWW05NFgyeGxiZ29nSUNBZ1luVnllU0F4Q2lBZ0lDQmlibm9nWDNSeVlXNXpabVZ5WDNCaGNuUnBkR2x2Ymw5aFpuUmxjbDlwWmw5bGJITmxRRFlLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TXpVMENpQWdJQ0F2THlCMGFHbHpMbkJoY25ScGRHbHZibk1vZEc5TFpYa3BMblpoYkhWbElEMGdibVYzSUdGeVl6UXVWV2x1ZEU0eU5UWW9NQ2tLSUNBZ0lHWnlZVzFsWDJScFp5QXhDaUFnSUNCaWVYUmxZMTh4SUM4dklEQjRNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNQW9nSUNBZ1ltOTRYM0IxZEFvS1gzUnlZVzV6Wm1WeVgzQmhjblJwZEdsdmJsOWhablJsY2w5cFpsOWxiSE5sUURZNkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qTTFOZ29nSUNBZ0x5OGdkR2hwY3k1d1lYSjBhWFJwYjI1ektIUnZTMlY1S1M1MllXeDFaU0E5SUc1bGR5QmhjbU0wTGxWcGJuUk9NalUyS0hSb2FYTXVjR0Z5ZEdsMGFXOXVjeWgwYjB0bGVTa3VkbUZzZFdVdWJtRjBhWFpsSUNzZ1lXMXZkVzUwTG01aGRHbDJaU2tLSUNBZ0lHWnlZVzFsWDJScFp5QXhDaUFnSUNCa2RYQUtJQ0FnSUdKdmVGOW5aWFFLSUNBZ0lHRnpjMlZ5ZENBdkx5QkNiM2dnYlhWemRDQm9ZWFpsSUhaaGJIVmxDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUSUtJQ0FnSUdJckNpQWdJQ0JrZFhBS0lDQWdJR3hsYmdvZ0lDQWdhVzUwWTE4eUlDOHZJRE15Q2lBZ0lDQThQUW9nSUNBZ1lYTnpaWEowSUM4dklHOTJaWEptYkc5M0NpQWdJQ0JtY21GdFpWOWthV2NnTUFvZ0lDQWdZbndLSUNBZ0lHSnZlRjl3ZFhRS0lDQWdJSEpsZEhOMVlnb0tDaTh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk9rRnlZekUwTVRBdVlYSmpNVFF4TUY5aGRYUm9iM0pwZW1WZmIzQmxjbUYwYjNKZllubGZjRzl5ZEdsdmJpaG9iMnhrWlhJNklHSjVkR1Z6TENCdmNHVnlZWFJ2Y2pvZ1lubDBaWE1zSUhCaGNuUnBkR2x2YmpvZ1lubDBaWE1zSUdGdGIzVnVkRG9nWW5sMFpYTXBJQzArSUhadmFXUTZDbUZ5WXpFME1UQmZZWFYwYUc5eWFYcGxYMjl3WlhKaGRHOXlYMko1WDNCdmNuUnBiMjQ2Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pNMU9TMHpOalVLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDZ3BDaUFnSUNBdkx5QndkV0pzYVdNZ1lYSmpNVFF4TUY5aGRYUm9iM0pwZW1WZmIzQmxjbUYwYjNKZllubGZjRzl5ZEdsdmJpZ0tJQ0FnSUM4dklDQWdhRzlzWkdWeU9pQmhjbU0wTGtGa1pISmxjM01zQ2lBZ0lDQXZMeUFnSUc5d1pYSmhkRzl5T2lCaGNtTTBMa0ZrWkhKbGMzTXNDaUFnSUNBdkx5QWdJSEJoY25ScGRHbHZiam9nWVhKak5DNUJaR1J5WlhOekxBb2dJQ0FnTHk4Z0lDQmhiVzkxYm5RNklHRnlZelF1VldsdWRFNHlOVFlzQ2lBZ0lDQXZMeUFwT2lCMmIybGtJSHNLSUNBZ0lIQnliM1J2SURRZ01Bb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3pOallLSUNBZ0lDOHZJR0Z6YzJWeWRDaHVaWGNnWVhKak5DNUJaR1J5WlhOektGUjRiaTV6Wlc1a1pYSXBJRDA5UFNCb2IyeGtaWElzSUNkUGJteDVJR2h2YkdSbGNpQmpZVzRnWVhWMGFHOXlhWHBsSUhCdmNuUnBiMjRuS1FvZ0lDQWdkSGh1SUZObGJtUmxjZ29nSUNBZ1puSmhiV1ZmWkdsbklDMDBDaUFnSUNBOVBRb2dJQ0FnWVhOelpYSjBJQzh2SUU5dWJIa2dhRzlzWkdWeUlHTmhiaUJoZFhSb2IzSnBlbVVnY0c5eWRHbHZiZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem96TmpjS0lDQWdJQzh2SUdOdmJuTjBJR3RsZVNBOUlHNWxkeUJoY21NeE5ERXdYMDl3WlhKaGRHOXlVRzl5ZEdsdmJrdGxlU2g3SUdodmJHUmxjaXdnYjNCbGNtRjBiM0lzSUhCaGNuUnBkR2x2YmlCOUtRb2dJQ0FnWm5KaGJXVmZaR2xuSUMwMENpQWdJQ0JtY21GdFpWOWthV2NnTFRNS0lDQWdJR052Ym1OaGRBb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0JqYjI1allYUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZOak1LSUNBZ0lDOHZJSEIxWW14cFl5QnZjR1Z5WVhSdmNsQnZjblJwYjI1QmJHeHZkMkZ1WTJWeklEMGdRbTk0VFdGd1BHRnlZekUwTVRCZlQzQmxjbUYwYjNKUWIzSjBhVzl1UzJWNUxDQmhjbU0wTGxWcGJuUk9NalUyUGloN0lHdGxlVkJ5WldacGVEb2dKMjl3WVNjZ2ZTa0tJQ0FnSUdKNWRHVmpJREV6SUM4dklDSnZjR0VpQ2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk16WTRDaUFnSUNBdkx5QjBhR2x6TG05d1pYSmhkRzl5VUc5eWRHbHZia0ZzYkc5M1lXNWpaWE1vYTJWNUtTNTJZV3gxWlNBOUlHRnRiM1Z1ZEFvZ0lDQWdabkpoYldWZlpHbG5JQzB4Q2lBZ0lDQmliM2hmY0hWMENpQWdJQ0J5WlhSemRXSUtDZ292THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qcEJjbU14TkRFd0xtRnlZekUwTVRCZmFYTmZiM0JsY21GMGIzSmZZbmxmY0c5eWRHbHZiaWhvYjJ4a1pYSTZJR0o1ZEdWekxDQnZjR1Z5WVhSdmNqb2dZbmwwWlhNc0lIQmhjblJwZEdsdmJqb2dZbmwwWlhNcElDMCtJR0o1ZEdWek9ncGhjbU14TkRFd1gybHpYMjl3WlhKaGRHOXlYMko1WDNCdmNuUnBiMjQ2Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pNM01TMHpOellLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDaDdJSEpsWVdSdmJteDVPaUIwY25WbElIMHBDaUFnSUNBdkx5QndkV0pzYVdNZ1lYSmpNVFF4TUY5cGMxOXZjR1Z5WVhSdmNsOWllVjl3YjNKMGFXOXVLQW9nSUNBZ0x5OGdJQ0JvYjJ4a1pYSTZJR0Z5WXpRdVFXUmtjbVZ6Y3l3S0lDQWdJQzh2SUNBZ2IzQmxjbUYwYjNJNklHRnlZelF1UVdSa2NtVnpjeXdLSUNBZ0lDOHZJQ0FnY0dGeWRHbDBhVzl1T2lCaGNtTTBMa0ZrWkhKbGMzTXNDaUFnSUNBdkx5QXBPaUJoY21NMExrSnZiMndnZXdvZ0lDQWdjSEp2ZEc4Z015QXhDaUFnSUNCcGJuUmpYekFnTHk4Z01Bb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3pOemNLSUNBZ0lDOHZJR2xtSUNodmNHVnlZWFJ2Y2lBOVBUMGdhRzlzWkdWeUtTQnlaWFIxY200Z2JtVjNJR0Z5WXpRdVFtOXZiQ2gwY25WbEtRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0JtY21GdFpWOWthV2NnTFRNS0lDQWdJRDA5Q2lBZ0lDQmllaUJoY21NeE5ERXdYMmx6WDI5d1pYSmhkRzl5WDJKNVgzQnZjblJwYjI1ZllXWjBaWEpmYVdaZlpXeHpaVUF5Q2lBZ0lDQmllWFJsWXlBM0lDOHZJREI0T0RBS0lDQWdJSE4zWVhBS0lDQWdJSEpsZEhOMVlnb0tZWEpqTVRReE1GOXBjMTl2Y0dWeVlYUnZjbDlpZVY5d2IzSjBhVzl1WDJGbWRHVnlYMmxtWDJWc2MyVkFNam9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TXpjNENpQWdJQ0F2THlCamIyNXpkQ0JyWlhrZ1BTQnVaWGNnWVhKak1UUXhNRjlQY0dWeVlYUnZjbEJ2Y25ScGIyNUxaWGtvZXlCb2IyeGtaWElzSUc5d1pYSmhkRzl5TENCd1lYSjBhWFJwYjI0Z2ZTa0tJQ0FnSUdaeVlXMWxYMlJwWnlBdE13b2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0JqYjI1allYUUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE1Rb2dJQ0FnWTI5dVkyRjBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPall6Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdiM0JsY21GMGIzSlFiM0owYVc5dVFXeHNiM2RoYm1ObGN5QTlJRUp2ZUUxaGNEeGhjbU14TkRFd1gwOXdaWEpoZEc5eVVHOXlkR2x2Ymt0bGVTd2dZWEpqTkM1VmFXNTBUakkxTmo0b2V5QnJaWGxRY21WbWFYZzZJQ2R2Y0dFbklIMHBDaUFnSUNCaWVYUmxZeUF4TXlBdkx5QWliM0JoSWdvZ0lDQWdjM2RoY0FvZ0lDQWdZMjl1WTJGMENpQWdJQ0JrZFhBS0lDQWdJR1p5WVcxbFgySjFjbmtnTUFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvek56a0tJQ0FnSUM4dklHbG1JQ2doZEdocGN5NXZjR1Z5WVhSdmNsQnZjblJwYjI1QmJHeHZkMkZ1WTJWektHdGxlU2t1WlhocGMzUnpLU0J5WlhSMWNtNGdibVYzSUdGeVl6UXVRbTl2YkNobVlXeHpaU2tLSUNBZ0lHSnZlRjlzWlc0S0lDQWdJR0oxY25rZ01Rb2dJQ0FnWW01NklHRnlZekUwTVRCZmFYTmZiM0JsY21GMGIzSmZZbmxmY0c5eWRHbHZibDloWm5SbGNsOXBabDlsYkhObFFEUUtJQ0FnSUdKNWRHVmpJREV4SUM4dklEQjRNREFLSUNBZ0lITjNZWEFLSUNBZ0lISmxkSE4xWWdvS1lYSmpNVFF4TUY5cGMxOXZjR1Z5WVhSdmNsOWllVjl3YjNKMGFXOXVYMkZtZEdWeVgybG1YMlZzYzJWQU5Eb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNemd3Q2lBZ0lDQXZMeUJ5WlhSMWNtNGdibVYzSUdGeVl6UXVRbTl2YkNoMGFHbHpMbTl3WlhKaGRHOXlVRzl5ZEdsdmJrRnNiRzkzWVc1alpYTW9hMlY1S1M1MllXeDFaUzV1WVhScGRtVWdQaUF3S1FvZ0lDQWdabkpoYldWZlpHbG5JREFLSUNBZ0lHSnZlRjluWlhRS0lDQWdJR0Z6YzJWeWRDQXZMeUJDYjNnZ2JYVnpkQ0JvWVhabElIWmhiSFZsQ2lBZ0lDQndkWE5vWW5sMFpYTWdNSGdLSUNBZ0lHSStDaUFnSUNCaWVYUmxZeUF4TVNBdkx5QXdlREF3Q2lBZ0lDQnBiblJqWHpBZ0x5OGdNQW9nSUNBZ2RXNWpiM1psY2lBeUNpQWdJQ0J6WlhSaWFYUUtJQ0FnSUhOM1lYQUtJQ0FnSUhKbGRITjFZZ29LQ2k4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZPa0Z5WXpFME1UQXVZWEpqTVRReE1GOXBjM04xWlY5aWVWOXdZWEowYVhScGIyNG9kRzg2SUdKNWRHVnpMQ0J3WVhKMGFYUnBiMjQ2SUdKNWRHVnpMQ0JoYlc5MWJuUTZJR0o1ZEdWekxDQmtZWFJoT2lCaWVYUmxjeWtnTFQ0Z2RtOXBaRG9LWVhKak1UUXhNRjlwYzNOMVpWOWllVjl3WVhKMGFYUnBiMjQ2Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pNNE15MHpPRGtLSUNBZ0lDOHZJRUJoY21NMExtRmlhVzFsZEdodlpDZ3BDaUFnSUNBdkx5QndkV0pzYVdNZ1lYSmpNVFF4TUY5cGMzTjFaVjlpZVY5d1lYSjBhWFJwYjI0b0NpQWdJQ0F2THlBZ0lIUnZPaUJoY21NMExrRmtaSEpsYzNNc0NpQWdJQ0F2THlBZ0lIQmhjblJwZEdsdmJqb2dZWEpqTkM1QlpHUnlaWE56TEFvZ0lDQWdMeThnSUNCaGJXOTFiblE2SUdGeVl6UXVWV2x1ZEU0eU5UWXNDaUFnSUNBdkx5QWdJR1JoZEdFNklHRnlZelF1UkhsdVlXMXBZMEo1ZEdWekxBb2dJQ0FnTHk4Z0tUb2dkbTlwWkNCN0NpQWdJQ0J3Y205MGJ5QTBJREFLSUNBZ0lHbHVkR05mTUNBdkx5QXdDaUFnSUNCa2RYQUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNemt3Q2lBZ0lDQXZMeUJoYzNObGNuUW9kR2hwY3k1aGNtTTRPRjlwYzE5dmQyNWxjaWh1WlhjZ1lYSmpOQzVCWkdSeVpYTnpLRlI0Ymk1elpXNWtaWElwS1M1dVlYUnBkbVVnUFQwOUlIUnlkV1VzSUNkdmJteDVYMjkzYm1WeUp5a0tJQ0FnSUhSNGJpQlRaVzVrWlhJS0lDQWdJR05oYkd4emRXSWdZWEpqT0RoZmFYTmZiM2R1WlhJS0lDQWdJR2x1ZEdOZk1DQXZMeUF3Q2lBZ0lDQm5aWFJpYVhRS0lDQWdJR2x1ZEdOZk1TQXZMeUF4Q2lBZ0lDQTlQUW9nSUNBZ1lYTnpaWEowSUM4dklHOXViSGxmYjNkdVpYSUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNemt4Q2lBZ0lDQXZMeUJoYzNObGNuUW9ZVzF2ZFc1MExtNWhkR2wyWlNBK0lEQXNJQ2RKYm5aaGJHbGtJR0Z0YjNWdWRDY3BDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUSUtJQ0FnSUhCMWMyaGllWFJsY3lBd2VBb2dJQ0FnWWo0S0lDQWdJR0Z6YzJWeWRDQXZMeUJKYm5aaGJHbGtJR0Z0YjNWdWRBb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3pPVE1LSUNBZ0lDOHZJR052Ym5OMElIUnZTMlY1SUQwZ2JtVjNJR0Z5WXpFME1UQmZVR0Z5ZEdsMGFXOXVTMlY1S0hzZ2FHOXNaR1Z5T2lCMGJ5d2djR0Z5ZEdsMGFXOXVJSDBwQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVFFLSUNBZ0lHWnlZVzFsWDJScFp5QXRNd29nSUNBZ1kyOXVZMkYwQ2lBZ0lDQmtkWEFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TlRjS0lDQWdJQzh2SUhCMVlteHBZeUJ3WVhKMGFYUnBiMjV6SUQwZ1FtOTRUV0Z3UEdGeVl6RTBNVEJmVUdGeWRHbDBhVzl1UzJWNUxDQmhjbU0wTGxWcGJuUk9NalUyUGloN0lHdGxlVkJ5WldacGVEb2dKM0FuSUgwcENpQWdJQ0JpZVhSbFl5QTRJQzh2SUNKd0lnb2dJQ0FnYzNkaGNBb2dJQ0FnWTI5dVkyRjBDaUFnSUNCa2RYQUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZNemswQ2lBZ0lDQXZMeUJwWmlBb0lYUm9hWE11Y0dGeWRHbDBhVzl1Y3loMGIwdGxlU2t1WlhocGMzUnpLU0I3Q2lBZ0lDQmliM2hmYkdWdUNpQWdJQ0JpZFhKNUlERUtJQ0FnSUdKdWVpQmhjbU14TkRFd1gybHpjM1ZsWDJKNVgzQmhjblJwZEdsdmJsOWhablJsY2w5cFpsOWxiSE5sUURJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk16azFDaUFnSUNBdkx5QjBhR2x6TG5CaGNuUnBkR2x2Ym5Nb2RHOUxaWGtwTG5aaGJIVmxJRDBnYm1WM0lHRnlZelF1VldsdWRFNHlOVFlvTUNrS0lDQWdJR1p5WVcxbFgyUnBaeUF6Q2lBZ0lDQmllWFJsWTE4eElDOHZJREI0TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TUFvZ0lDQWdZbTk0WDNCMWRBb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3pPVFlLSUNBZ0lDOHZJSFJvYVhNdVgyRmtaRjl3WVhKMGFXTnBjR0YwYVc5dVgzUnZYMmh2YkdSbGNpaDBieXdnY0dGeWRHbDBhVzl1S1FvZ0lDQWdabkpoYldWZlpHbG5JQzAwQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVE1LSUNBZ0lHTmhiR3h6ZFdJZ1gyRmtaRjl3WVhKMGFXTnBjR0YwYVc5dVgzUnZYMmh2YkdSbGNnb0tZWEpqTVRReE1GOXBjM04xWlY5aWVWOXdZWEowYVhScGIyNWZZV1owWlhKZmFXWmZaV3h6WlVBeU9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6b3pPVGdLSUNBZ0lDOHZJSFJvYVhNdWNHRnlkR2wwYVc5dWN5aDBiMHRsZVNrdWRtRnNkV1VnUFNCdVpYY2dZWEpqTkM1VmFXNTBUakkxTmloMGFHbHpMbkJoY25ScGRHbHZibk1vZEc5TFpYa3BMblpoYkhWbExtNWhkR2wyWlNBcklHRnRiM1Z1ZEM1dVlYUnBkbVVwQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dNd29nSUNBZ1pIVndDaUFnSUNCaWIzaGZaMlYwQ2lBZ0lDQmhjM05sY25RZ0x5OGdRbTk0SUcxMWMzUWdhR0YyWlNCMllXeDFaUW9nSUNBZ1puSmhiV1ZmWkdsbklDMHlDaUFnSUNCaUt3b2dJQ0FnWkhWd0NpQWdJQ0JzWlc0S0lDQWdJR2x1ZEdOZk1pQXZMeUF6TWdvZ0lDQWdQRDBLSUNBZ0lHRnpjMlZ5ZENBdkx5QnZkbVZ5Wm14dmR3b2dJQ0FnYVc1MFkxOHlJQzh2SURNeUNpQWdJQ0JpZW1WeWJ3b2dJQ0FnWkhWd0NpQWdJQ0JtY21GdFpWOWlkWEo1SURBS0lDQWdJR0o4Q2lBZ0lDQmliM2hmY0hWMENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk5UTUtJQ0FnSUM4dklIQjFZbXhwWXlCaVlXeGhibU5sY3lBOUlFSnZlRTFoY0R4QlpHUnlaWE56TENCVmFXNTBUakkxTmo0b2V5QnJaWGxRY21WbWFYZzZJQ2RpSnlCOUtRb2dJQ0FnWW5sMFpXTWdOQ0F2THlBaVlpSUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE5Bb2dJQ0FnWTI5dVkyRjBDaUFnSUNCa2RYQUtJQ0FnSUdaeVlXMWxYMkoxY25rZ01Rb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6bzBNREFLSUNBZ0lDOHZJR2xtSUNnaGRHaHBjeTVpWVd4aGJtTmxjeWgwYnlrdVpYaHBjM1J6S1NCN0NpQWdJQ0JpYjNoZmJHVnVDaUFnSUNCaWRYSjVJREVLSUNBZ0lHSnVlaUJoY21NeE5ERXdYMmx6YzNWbFgySjVYM0JoY25ScGRHbHZibDloWm5SbGNsOXBabDlsYkhObFFEUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZOREF4Q2lBZ0lDQXZMeUIwYUdsekxtSmhiR0Z1WTJWektIUnZLUzUyWVd4MVpTQTlJRzVsZHlCaGNtTTBMbFZwYm5ST01qVTJLREFwQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dNUW9nSUNBZ1lubDBaV05mTVNBdkx5QXdlREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREFLSUNBZ0lHSnZlRjl3ZFhRS0NtRnlZekUwTVRCZmFYTnpkV1ZmWW5sZmNHRnlkR2wwYVc5dVgyRm1kR1Z5WDJsbVgyVnNjMlZBTkRvS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk5EQXpDaUFnSUNBdkx5QjBhR2x6TG1KaGJHRnVZMlZ6S0hSdktTNTJZV3gxWlNBOUlHNWxkeUJoY21NMExsVnBiblJPTWpVMktIUm9hWE11WW1Gc1lXNWpaWE1vZEc4cExuWmhiSFZsTG01aGRHbDJaU0FySUdGdGIzVnVkQzV1WVhScGRtVXBDaUFnSUNCbWNtRnRaVjlrYVdjZ01Rb2dJQ0FnWkhWd0NpQWdJQ0JpYjNoZloyVjBDaUFnSUNCaGMzTmxjblFnTHk4Z1FtOTRJRzExYzNRZ2FHRjJaU0IyWVd4MVpRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0JpS3dvZ0lDQWdaSFZ3Q2lBZ0lDQnNaVzRLSUNBZ0lHbHVkR05mTWlBdkx5QXpNZ29nSUNBZ1BEMEtJQ0FnSUdGemMyVnlkQ0F2THlCdmRtVnlabXh2ZHdvZ0lDQWdabkpoYldWZlpHbG5JREFLSUNBZ0lHUjFjQW9nSUNBZ1kyOTJaWElnTXdvZ0lDQWdZbndLSUNBZ0lHSnZlRjl3ZFhRS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pvMU1Rb2dJQ0FnTHk4Z2NIVmliR2xqSUhSdmRHRnNVM1Z3Y0d4NUlEMGdSMnh2WW1Gc1UzUmhkR1U4VldsdWRFNHlOVFkrS0hzZ2EyVjVPaUFuZENjZ2ZTa0tJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0JpZVhSbFkxOHpJQzh2SUNKMElnb2dJQ0FnWVhCd1gyZHNiMkpoYkY5blpYUmZaWGdLSUNBZ0lHRnpjMlZ5ZENBdkx5QmphR1ZqYXlCSGJHOWlZV3hUZEdGMFpTQmxlR2x6ZEhNS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk5EQTBDaUFnSUNBdkx5QjBhR2x6TG5SdmRHRnNVM1Z3Y0d4NUxuWmhiSFZsSUQwZ2JtVjNJR0Z5WXpRdVZXbHVkRTR5TlRZb2RHaHBjeTUwYjNSaGJGTjFjSEJzZVM1MllXeDFaUzV1WVhScGRtVWdLeUJoYlc5MWJuUXVibUYwYVhabEtRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0JpS3dvZ0lDQWdaSFZ3Q2lBZ0lDQnNaVzRLSUNBZ0lHbHVkR05mTWlBdkx5QXpNZ29nSUNBZ1BEMEtJQ0FnSUdGemMyVnlkQ0F2THlCdmRtVnlabXh2ZHdvZ0lDQWdZbndLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem8xTVFvZ0lDQWdMeThnY0hWaWJHbGpJSFJ2ZEdGc1UzVndjR3g1SUQwZ1IyeHZZbUZzVTNSaGRHVThWV2x1ZEU0eU5UWStLSHNnYTJWNU9pQW5kQ2NnZlNrS0lDQWdJR0o1ZEdWalh6TWdMeThnSW5RaUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qUXdOQW9nSUNBZ0x5OGdkR2hwY3k1MGIzUmhiRk4xY0hCc2VTNTJZV3gxWlNBOUlHNWxkeUJoY21NMExsVnBiblJPTWpVMktIUm9hWE11ZEc5MFlXeFRkWEJ3YkhrdWRtRnNkV1V1Ym1GMGFYWmxJQ3NnWVcxdmRXNTBMbTVoZEdsMlpTa0tJQ0FnSUhOM1lYQUtJQ0FnSUdGd2NGOW5iRzlpWVd4ZmNIVjBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPalF3TlFvZ0lDQWdMeThnWlcxcGRDZ25TWE56ZFdVbkxDQnVaWGNnWVhKak1UUXhNRjl3WVhKMGFYUnBiMjVmYVhOemRXVW9leUIwYnl3Z2NHRnlkR2wwYVc5dUxDQmhiVzkxYm5Rc0lHUmhkR0VnZlNrcENpQWdJQ0JtY21GdFpWOWthV2NnTWdvZ0lDQWdabkpoYldWZlpHbG5JQzB5Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR0o1ZEdWaklESTBJQzh2SURCNE1EQTJNZ29nSUNBZ1kyOXVZMkYwQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVEVLSUNBZ0lHTnZibU5oZEFvZ0lDQWdZbmwwWldNZ05pQXZMeUF3ZURBd01ESUtJQ0FnSUhOM1lYQUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ2NIVnphR0o1ZEdWeklEQjRabUUwTkROaU1XSWdMeThnYldWMGFHOWtJQ0pKYzNOMVpTZ29ZV1JrY21WemN5eGhaR1J5WlhOekxIVnBiblF5TlRZc1lubDBaVnRkS1NraUNpQWdJQ0J6ZDJGd0NpQWdJQ0JqYjI1allYUUtJQ0FnSUd4dlp3b2dJQ0FnY21WMGMzVmlDZ29LTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6bzZRWEpqTVRReE1DNWhjbU14TkRFd1gzSmxaR1ZsYlY5aWVWOXdZWEowYVhScGIyNG9jR0Z5ZEdsMGFXOXVPaUJpZVhSbGN5d2dZVzF2ZFc1ME9pQmllWFJsY3l3Z1pHRjBZVG9nWW5sMFpYTXBJQzArSUhadmFXUTZDbUZ5WXpFME1UQmZjbVZrWldWdFgySjVYM0JoY25ScGRHbHZiam9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TkRBNExUUXdPUW9nSUNBZ0x5OGdRR0Z5WXpRdVlXSnBiV1YwYUc5a0tDa0tJQ0FnSUM4dklIQjFZbXhwWXlCaGNtTXhOREV3WDNKbFpHVmxiVjlpZVY5d1lYSjBhWFJwYjI0b2NHRnlkR2wwYVc5dU9pQmhjbU0wTGtGa1pISmxjM01zSUdGdGIzVnVkRG9nWVhKak5DNVZhVzUwVGpJMU5pd2daR0YwWVRvZ1lYSmpOQzVFZVc1aGJXbGpRbmwwWlhNcE9pQjJiMmxrSUhzS0lDQWdJSEJ5YjNSdklETWdNQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem8wTVRBS0lDQWdJQzh2SUdOdmJuTjBJR1p5YjIwZ1BTQnVaWGNnWVhKak5DNUJaR1J5WlhOektGUjRiaTV6Wlc1a1pYSXBDaUFnSUNCMGVHNGdVMlZ1WkdWeUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qUXhNUW9nSUNBZ0x5OGdZWE56WlhKMEtHRnRiM1Z1ZEM1dVlYUnBkbVVnUGlBd0xDQW5TVzUyWVd4cFpDQmhiVzkxYm5RbktRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0J3ZFhOb1lubDBaWE1nTUhnS0lDQWdJR0krQ2lBZ0lDQmhjM05sY25RZ0x5OGdTVzUyWVd4cFpDQmhiVzkxYm5RS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk5ERXlDaUFnSUNBdkx5QmpiMjV6ZENCbWNtOXRTMlY1SUQwZ2JtVjNJR0Z5WXpFME1UQmZVR0Z5ZEdsMGFXOXVTMlY1S0hzZ2FHOXNaR1Z5T2lCbWNtOXRMQ0J3WVhKMGFYUnBiMjRnZlNrS0lDQWdJR1IxY0FvZ0lDQWdabkpoYldWZlpHbG5JQzB6Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR1IxY0FvZ0lDQWdZMjkyWlhJZ01nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6bzFOd29nSUNBZ0x5OGdjSFZpYkdsaklIQmhjblJwZEdsdmJuTWdQU0JDYjNoTllYQThZWEpqTVRReE1GOVFZWEowYVhScGIyNUxaWGtzSUdGeVl6UXVWV2x1ZEU0eU5UWStLSHNnYTJWNVVISmxabWw0T2lBbmNDY2dmU2tLSUNBZ0lHSjVkR1ZqSURnZ0x5OGdJbkFpQ2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk5ERXpDaUFnSUNBdkx5QmhjM05sY25Rb2RHaHBjeTV3WVhKMGFYUnBiMjV6S0daeWIyMUxaWGtwTG1WNGFYTjBjeXdnSjFCaGNuUnBkR2x2YmlCaVlXeGhibU5sSUcxcGMzTnBibWNuS1FvZ0lDQWdaSFZ3Q2lBZ0lDQmliM2hmYkdWdUNpQWdJQ0JpZFhKNUlERUtJQ0FnSUdGemMyVnlkQ0F2THlCUVlYSjBhWFJwYjI0Z1ltRnNZVzVqWlNCdGFYTnphVzVuQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pReE5Bb2dJQ0FnTHk4Z1lYTnpaWEowS0hSb2FYTXVjR0Z5ZEdsMGFXOXVjeWhtY205dFMyVjVLUzUyWVd4MVpTNXVZWFJwZG1VZ1BqMGdZVzF2ZFc1MExtNWhkR2wyWlN3Z0owbHVjM1ZtWm1samFXVnVkQ0J3WVhKMGFYUnBiMjRnWW1Gc1lXNWpaU2NwQ2lBZ0lDQmtkWEFLSUNBZ0lHSnZlRjluWlhRS0lDQWdJR0Z6YzJWeWRDQXZMeUJDYjNnZ2JYVnpkQ0JvWVhabElIWmhiSFZsQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVElLSUNBZ0lHSStQUW9nSUNBZ1lYTnpaWEowSUM4dklFbHVjM1ZtWm1samFXVnVkQ0J3WVhKMGFYUnBiMjRnWW1Gc1lXNWpaUW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem8wTVRVS0lDQWdJQzh2SUhSb2FYTXVjR0Z5ZEdsMGFXOXVjeWhtY205dFMyVjVLUzUyWVd4MVpTQTlJRzVsZHlCaGNtTTBMbFZwYm5ST01qVTJLSFJvYVhNdWNHRnlkR2wwYVc5dWN5aG1jbTl0UzJWNUtTNTJZV3gxWlM1dVlYUnBkbVVnTFNCaGJXOTFiblF1Ym1GMGFYWmxLUW9nSUNBZ1pIVndDaUFnSUNCaWIzaGZaMlYwQ2lBZ0lDQmhjM05sY25RZ0x5OGdRbTk0SUcxMWMzUWdhR0YyWlNCMllXeDFaUW9nSUNBZ1puSmhiV1ZmWkdsbklDMHlDaUFnSUNCaUxRb2dJQ0FnWkhWd0NpQWdJQ0JzWlc0S0lDQWdJR2x1ZEdOZk1pQXZMeUF6TWdvZ0lDQWdQRDBLSUNBZ0lHRnpjMlZ5ZENBdkx5QnZkbVZ5Wm14dmR3b2dJQ0FnYVc1MFkxOHlJQzh2SURNeUNpQWdJQ0JpZW1WeWJ3b2dJQ0FnWkhWd0NpQWdJQ0JqYjNabGNpQTBDaUFnSUNCaWZBb2dJQ0FnWW05NFgzQjFkQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPalV6Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdZbUZzWVc1alpYTWdQU0JDYjNoTllYQThRV1JrY21WemN5d2dWV2x1ZEU0eU5UWStLSHNnYTJWNVVISmxabWw0T2lBbllpY2dmU2tLSUNBZ0lHSjVkR1ZqSURRZ0x5OGdJbUlpQ2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR1IxY0FvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvME1UY0tJQ0FnSUM4dklHRnpjMlZ5ZENoMGFHbHpMbUpoYkdGdVkyVnpLR1p5YjIwcExtVjRhWE4wY3lBbUppQjBhR2x6TG1KaGJHRnVZMlZ6S0daeWIyMHBMblpoYkhWbExtNWhkR2wyWlNBK1BTQmhiVzkxYm5RdWJtRjBhWFpsTENBblNXNXpkV1ptYVdOcFpXNTBJR0poYkdGdVkyVW5LUW9nSUNBZ1ltOTRYMnhsYmdvZ0lDQWdZblZ5ZVNBeENpQWdJQ0JpZWlCaGNtTXhOREV3WDNKbFpHVmxiVjlpZVY5d1lYSjBhWFJwYjI1ZlltOXZiRjltWVd4elpVQXpDaUFnSUNCbWNtRnRaVjlrYVdjZ01nb2dJQ0FnWW05NFgyZGxkQW9nSUNBZ1lYTnpaWEowSUM4dklFSnZlQ0J0ZFhOMElHaGhkbVVnZG1Gc2RXVUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE1nb2dJQ0FnWWo0OUNpQWdJQ0JpZWlCaGNtTXhOREV3WDNKbFpHVmxiVjlpZVY5d1lYSjBhWFJwYjI1ZlltOXZiRjltWVd4elpVQXpDaUFnSUNCcGJuUmpYekVnTHk4Z01Rb0tZWEpqTVRReE1GOXlaV1JsWlcxZllubGZjR0Z5ZEdsMGFXOXVYMkp2YjJ4ZmJXVnlaMlZBTkRvS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk5ERTNDaUFnSUNBdkx5QmhjM05sY25Rb2RHaHBjeTVpWVd4aGJtTmxjeWhtY205dEtTNWxlR2x6ZEhNZ0ppWWdkR2hwY3k1aVlXeGhibU5sY3lobWNtOXRLUzUyWVd4MVpTNXVZWFJwZG1VZ1BqMGdZVzF2ZFc1MExtNWhkR2wyWlN3Z0owbHVjM1ZtWm1samFXVnVkQ0JpWVd4aGJtTmxKeWtLSUNBZ0lHRnpjMlZ5ZENBdkx5Qkpibk4xWm1acFkybGxiblFnWW1Gc1lXNWpaUW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem8wTVRnS0lDQWdJQzh2SUhSb2FYTXVZbUZzWVc1alpYTW9abkp2YlNrdWRtRnNkV1VnUFNCdVpYY2dZWEpqTkM1VmFXNTBUakkxTmloMGFHbHpMbUpoYkdGdVkyVnpLR1p5YjIwcExuWmhiSFZsTG01aGRHbDJaU0F0SUdGdGIzVnVkQzV1WVhScGRtVXBDaUFnSUNCbWNtRnRaVjlrYVdjZ01nb2dJQ0FnWkhWd0NpQWdJQ0JpYjNoZloyVjBDaUFnSUNCaGMzTmxjblFnTHk4Z1FtOTRJRzExYzNRZ2FHRjJaU0IyWVd4MVpRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0JpTFFvZ0lDQWdaSFZ3Q2lBZ0lDQnNaVzRLSUNBZ0lHbHVkR05mTWlBdkx5QXpNZ29nSUNBZ1BEMEtJQ0FnSUdGemMyVnlkQ0F2THlCdmRtVnlabXh2ZHdvZ0lDQWdabkpoYldWZlpHbG5JREVLSUNBZ0lHUjFjQW9nSUNBZ1kyOTJaWElnTXdvZ0lDQWdZbndLSUNBZ0lHSnZlRjl3ZFhRS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pvMU1Rb2dJQ0FnTHk4Z2NIVmliR2xqSUhSdmRHRnNVM1Z3Y0d4NUlEMGdSMnh2WW1Gc1UzUmhkR1U4VldsdWRFNHlOVFkrS0hzZ2EyVjVPaUFuZENjZ2ZTa0tJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0JpZVhSbFkxOHpJQzh2SUNKMElnb2dJQ0FnWVhCd1gyZHNiMkpoYkY5blpYUmZaWGdLSUNBZ0lHRnpjMlZ5ZENBdkx5QmphR1ZqYXlCSGJHOWlZV3hUZEdGMFpTQmxlR2x6ZEhNS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk5ERTVDaUFnSUNBdkx5QjBhR2x6TG5SdmRHRnNVM1Z3Y0d4NUxuWmhiSFZsSUQwZ2JtVjNJR0Z5WXpRdVZXbHVkRTR5TlRZb2RHaHBjeTUwYjNSaGJGTjFjSEJzZVM1MllXeDFaUzV1WVhScGRtVWdMU0JoYlc5MWJuUXVibUYwYVhabEtRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0JpTFFvZ0lDQWdaSFZ3Q2lBZ0lDQnNaVzRLSUNBZ0lHbHVkR05mTWlBdkx5QXpNZ29nSUNBZ1BEMEtJQ0FnSUdGemMyVnlkQ0F2THlCdmRtVnlabXh2ZHdvZ0lDQWdZbndLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem8xTVFvZ0lDQWdMeThnY0hWaWJHbGpJSFJ2ZEdGc1UzVndjR3g1SUQwZ1IyeHZZbUZzVTNSaGRHVThWV2x1ZEU0eU5UWStLSHNnYTJWNU9pQW5kQ2NnZlNrS0lDQWdJR0o1ZEdWalh6TWdMeThnSW5RaUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qUXhPUW9nSUNBZ0x5OGdkR2hwY3k1MGIzUmhiRk4xY0hCc2VTNTJZV3gxWlNBOUlHNWxkeUJoY21NMExsVnBiblJPTWpVMktIUm9hWE11ZEc5MFlXeFRkWEJ3YkhrdWRtRnNkV1V1Ym1GMGFYWmxJQzBnWVcxdmRXNTBMbTVoZEdsMlpTa0tJQ0FnSUhOM1lYQUtJQ0FnSUdGd2NGOW5iRzlpWVd4ZmNIVjBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPalF5TUFvZ0lDQWdMeThnWlcxcGRDZ25VbVZrWldWdEp5d2dibVYzSUdGeVl6RTBNVEJmY0dGeWRHbDBhVzl1WDNKbFpHVmxiU2g3SUdaeWIyMHNJSEJoY25ScGRHbHZiaXdnWVcxdmRXNTBMQ0JrWVhSaElIMHBLUW9nSUNBZ1puSmhiV1ZmWkdsbklEQUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE1nb2dJQ0FnWTI5dVkyRjBDaUFnSUNCaWVYUmxZeUF5TkNBdkx5QXdlREF3TmpJS0lDQWdJR052Ym1OaGRBb2dJQ0FnWm5KaGJXVmZaR2xuSUMweENpQWdJQ0JqYjI1allYUUtJQ0FnSUdKNWRHVmpJRFlnTHk4Z01IZ3dNREF5Q2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR0o1ZEdWaklESTNJQzh2SUcxbGRHaHZaQ0FpVW1Wa1pXVnRLQ2hoWkdSeVpYTnpMR0ZrWkhKbGMzTXNkV2x1ZERJMU5peGllWFJsVzEwcEtTSUtJQ0FnSUhOM1lYQUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ2JHOW5DaUFnSUNCeVpYUnpkV0lLQ21GeVl6RTBNVEJmY21Wa1pXVnRYMko1WDNCaGNuUnBkR2x2Ymw5aWIyOXNYMlpoYkhObFFETTZDaUFnSUNCcGJuUmpYekFnTHk4Z01Bb2dJQ0FnWWlCaGNtTXhOREV3WDNKbFpHVmxiVjlpZVY5d1lYSjBhWFJwYjI1ZlltOXZiRjl0WlhKblpVQTBDZ29LTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6bzZRWEpqTVRReE1DNWhjbU14TkRFd1gyOXdaWEpoZEc5eVgzSmxaR1ZsYlY5aWVWOXdZWEowYVhScGIyNG9abkp2YlRvZ1lubDBaWE1zSUhCaGNuUnBkR2x2YmpvZ1lubDBaWE1zSUdGdGIzVnVkRG9nWW5sMFpYTXNJR1JoZEdFNklHSjVkR1Z6S1NBdFBpQjJiMmxrT2dwaGNtTXhOREV3WDI5d1pYSmhkRzl5WDNKbFpHVmxiVjlpZVY5d1lYSjBhWFJwYjI0NkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qUXlNeTAwTWprS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2dwQ2lBZ0lDQXZMeUJ3ZFdKc2FXTWdZWEpqTVRReE1GOXZjR1Z5WVhSdmNsOXlaV1JsWlcxZllubGZjR0Z5ZEdsMGFXOXVLQW9nSUNBZ0x5OGdJQ0JtY205dE9pQmhjbU0wTGtGa1pISmxjM01zQ2lBZ0lDQXZMeUFnSUhCaGNuUnBkR2x2YmpvZ1lYSmpOQzVCWkdSeVpYTnpMQW9nSUNBZ0x5OGdJQ0JoYlc5MWJuUTZJR0Z5WXpRdVZXbHVkRTR5TlRZc0NpQWdJQ0F2THlBZ0lHUmhkR0U2SUdGeVl6UXVSSGx1WVcxcFkwSjVkR1Z6TEFvZ0lDQWdMeThnS1RvZ2RtOXBaQ0I3Q2lBZ0lDQndjbTkwYnlBMElEQUtJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0JrZFhCdUlETUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZORE13Q2lBZ0lDQXZMeUJqYjI1emRDQnpaVzVrWlhJZ1BTQnVaWGNnWVhKak5DNUJaR1J5WlhOektGUjRiaTV6Wlc1a1pYSXBDaUFnSUNCMGVHNGdVMlZ1WkdWeUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qUXpNZ29nSUNBZ0x5OGdiR1YwSUdGMWRHaHZjbWw2WldRZ1BTQjBhR2x6TG1GeVl6RTBNVEJmYVhOZmIzQmxjbUYwYjNJb1puSnZiU3dnYzJWdVpHVnlMQ0J3WVhKMGFYUnBiMjRwTG01aGRHbDJaU0E5UFQwZ2RISjFaUW9nSUNBZ1puSmhiV1ZmWkdsbklDMDBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPalF6TUFvZ0lDQWdMeThnWTI5dWMzUWdjMlZ1WkdWeUlEMGdibVYzSUdGeVl6UXVRV1JrY21WemN5aFVlRzR1YzJWdVpHVnlLUW9nSUNBZ2RIaHVJRk5sYm1SbGNnb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpFME1UQXVZV3huYnk1MGN6bzBNeklLSUNBZ0lDOHZJR3hsZENCaGRYUm9iM0pwZW1Wa0lEMGdkR2hwY3k1aGNtTXhOREV3WDJselgyOXdaWEpoZEc5eUtHWnliMjBzSUhObGJtUmxjaXdnY0dGeWRHbDBhVzl1S1M1dVlYUnBkbVVnUFQwOUlIUnlkV1VLSUNBZ0lHWnlZVzFsWDJScFp5QXRNd29nSUNBZ1kyRnNiSE4xWWlCaGNtTXhOREV3WDJselgyOXdaWEpoZEc5eUNpQWdJQ0JwYm5Salh6QWdMeThnTUFvZ0lDQWdaMlYwWW1sMENpQWdJQ0JwYm5Salh6RWdMeThnTVFvZ0lDQWdQVDBLSUNBZ0lHUjFjRzRnTWdvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvME16TUtJQ0FnSUM4dklHbG1JQ2doWVhWMGFHOXlhWHBsWkNrZ2V3b2dJQ0FnWW01NklHRnlZekUwTVRCZmIzQmxjbUYwYjNKZmNtVmtaV1Z0WDJKNVgzQmhjblJwZEdsdmJsOWhablJsY2w5cFpsOWxiSE5sUURRS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk5ETTBDaUFnSUNBdkx5QmpiMjV6ZENCd1MyVjVJRDBnYm1WM0lHRnlZekUwTVRCZlQzQmxjbUYwYjNKUWIzSjBhVzl1UzJWNUtIc2dhRzlzWkdWeU9pQm1jbTl0TENCdmNHVnlZWFJ2Y2pvZ2MyVnVaR1Z5TENCd1lYSjBhWFJwYjI0Z2ZTa0tJQ0FnSUdaeVlXMWxYMlJwWnlBdE5Bb2dJQ0FnWm5KaGJXVmZaR2xuSURRS0lDQWdJR052Ym1OaGRBb2dJQ0FnWm5KaGJXVmZaR2xuSUMwekNpQWdJQ0JqYjI1allYUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZOak1LSUNBZ0lDOHZJSEIxWW14cFl5QnZjR1Z5WVhSdmNsQnZjblJwYjI1QmJHeHZkMkZ1WTJWeklEMGdRbTk0VFdGd1BHRnlZekUwTVRCZlQzQmxjbUYwYjNKUWIzSjBhVzl1UzJWNUxDQmhjbU0wTGxWcGJuUk9NalUyUGloN0lHdGxlVkJ5WldacGVEb2dKMjl3WVNjZ2ZTa0tJQ0FnSUdKNWRHVmpJREV6SUM4dklDSnZjR0VpQ2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR1IxY0FvZ0lDQWdabkpoYldWZlluVnllU0F6Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pRek5Rb2dJQ0FnTHk4Z2FXWWdLSFJvYVhNdWIzQmxjbUYwYjNKUWIzSjBhVzl1UVd4c2IzZGhibU5sY3lod1MyVjVLUzVsZUdsemRITXBJSHNLSUNBZ0lHSnZlRjlzWlc0S0lDQWdJR0oxY25rZ01Rb2dJQ0FnWW5vZ1lYSmpNVFF4TUY5dmNHVnlZWFJ2Y2w5eVpXUmxaVzFmWW5sZmNHRnlkR2wwYVc5dVgyRm1kR1Z5WDJsbVgyVnNjMlZBTXdvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvME16WUtJQ0FnSUM4dklHTnZibk4wSUhKbGJXRnBibWx1WnlBOUlIUm9hWE11YjNCbGNtRjBiM0pRYjNKMGFXOXVRV3hzYjNkaGJtTmxjeWh3UzJWNUtTNTJZV3gxWlFvZ0lDQWdabkpoYldWZlpHbG5JRE1LSUNBZ0lHUjFjQW9nSUNBZ1ltOTRYMmRsZEFvZ0lDQWdZWE56WlhKMElDOHZJRUp2ZUNCdGRYTjBJR2hoZG1VZ2RtRnNkV1VLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXhOREV3TG1Gc1oyOHVkSE02TkRNM0NpQWdJQ0F2THlCaGMzTmxjblFvY21WdFlXbHVhVzVuTG01aGRHbDJaU0ErUFNCaGJXOTFiblF1Ym1GMGFYWmxMQ0FuVUc5eWRHbHZiaUJoYkd4dmQyRnVZMlVnWlhoalpXVmtaV1FuS1FvZ0lDQWdaSFZ3Q2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVElLSUNBZ0lHSStQUW9nSUNBZ1lYTnpaWEowSUM4dklGQnZjblJwYjI0Z1lXeHNiM2RoYm1ObElHVjRZMlZsWkdWa0NpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qUXpPQW9nSUNBZ0x5OGdZWFYwYUc5eWFYcGxaQ0E5SUhSeWRXVUtJQ0FnSUdsdWRHTmZNU0F2THlBeENpQWdJQ0JtY21GdFpWOWlkWEo1SURVS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk5ETTVDaUFnSUNBdkx5QjBhR2x6TG05d1pYSmhkRzl5VUc5eWRHbHZia0ZzYkc5M1lXNWpaWE1vY0V0bGVTa3VkbUZzZFdVZ1BTQnVaWGNnWVhKak5DNVZhVzUwVGpJMU5paHlaVzFoYVc1cGJtY3VibUYwYVhabElDMGdZVzF2ZFc1MExtNWhkR2wyWlNrS0lDQWdJR1p5WVcxbFgyUnBaeUF0TWdvZ0lDQWdZaTBLSUNBZ0lHUjFjQW9nSUNBZ2JHVnVDaUFnSUNCcGJuUmpYeklnTHk4Z016SUtJQ0FnSUR3OUNpQWdJQ0JoYzNObGNuUWdMeThnYjNabGNtWnNiM2NLSUNBZ0lHbHVkR05mTWlBdkx5QXpNZ29nSUNBZ1lucGxjbThLSUNBZ0lHSjhDaUFnSUNCaWIzaGZjSFYwQ2dwaGNtTXhOREV3WDI5d1pYSmhkRzl5WDNKbFpHVmxiVjlpZVY5d1lYSjBhWFJwYjI1ZllXWjBaWEpmYVdaZlpXeHpaVUF6T2dvZ0lDQWdabkpoYldWZlpHbG5JRFVLSUNBZ0lHWnlZVzFsWDJKMWNua2dOZ29LWVhKak1UUXhNRjl2Y0dWeVlYUnZjbDl5WldSbFpXMWZZbmxmY0dGeWRHbDBhVzl1WDJGbWRHVnlYMmxtWDJWc2MyVkFORG9LSUNBZ0lHWnlZVzFsWDJScFp5QTJDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPalEwTWdvZ0lDQWdMeThnWVhOelpYSjBLR0YxZEdodmNtbDZaV1FzSUNkT2IzUWdZWFYwYUc5eWFYcGxaQ0J2Y0dWeVlYUnZjaWNwQ2lBZ0lDQmhjM05sY25RZ0x5OGdUbTkwSUdGMWRHaHZjbWw2WldRZ2IzQmxjbUYwYjNJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk5EUTBDaUFnSUNBdkx5QmpiMjV6ZENCbWNtOXRTMlY1SUQwZ2JtVjNJR0Z5WXpFME1UQmZVR0Z5ZEdsMGFXOXVTMlY1S0hzZ2FHOXNaR1Z5T2lCbWNtOXRMQ0J3WVhKMGFYUnBiMjRnZlNrS0lDQWdJR1p5WVcxbFgyUnBaeUF0TkFvZ0lDQWdabkpoYldWZlpHbG5JQzB6Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR1IxY0FvZ0lDQWdabkpoYldWZlluVnllU0F4Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pVM0NpQWdJQ0F2THlCd2RXSnNhV01nY0dGeWRHbDBhVzl1Y3lBOUlFSnZlRTFoY0R4aGNtTXhOREV3WDFCaGNuUnBkR2x2Ymt0bGVTd2dZWEpqTkM1VmFXNTBUakkxTmo0b2V5QnJaWGxRY21WbWFYZzZJQ2R3SnlCOUtRb2dJQ0FnWW5sMFpXTWdPQ0F2THlBaWNDSUtJQ0FnSUhOM1lYQUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6RTBNVEF1WVd4bmJ5NTBjem8wTkRVS0lDQWdJQzh2SUdGemMyVnlkQ2gwYUdsekxuQmhjblJwZEdsdmJuTW9abkp2YlV0bGVTa3VaWGhwYzNSekxDQW5VR0Z5ZEdsMGFXOXVJR0poYkdGdVkyVWdiV2x6YzJsdVp5Y3BDaUFnSUNCa2RYQUtJQ0FnSUdKdmVGOXNaVzRLSUNBZ0lHSjFjbmtnTVFvZ0lDQWdZWE56WlhKMElDOHZJRkJoY25ScGRHbHZiaUJpWVd4aGJtTmxJRzFwYzNOcGJtY0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZORFEyQ2lBZ0lDQXZMeUJoYzNObGNuUW9kR2hwY3k1d1lYSjBhWFJwYjI1ektHWnliMjFMWlhrcExuWmhiSFZsTG01aGRHbDJaU0ErUFNCaGJXOTFiblF1Ym1GMGFYWmxMQ0FuU1c1emRXWm1hV05wWlc1MElIQmhjblJwZEdsdmJpQmlZV3hoYm1ObEp5a0tJQ0FnSUdSMWNBb2dJQ0FnWW05NFgyZGxkQW9nSUNBZ1lYTnpaWEowSUM4dklFSnZlQ0J0ZFhOMElHaGhkbVVnZG1Gc2RXVUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE1nb2dJQ0FnWWo0OUNpQWdJQ0JoYzNObGNuUWdMeThnU1c1emRXWm1hV05wWlc1MElIQmhjblJwZEdsdmJpQmlZV3hoYm1ObENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTVRReE1DNWhiR2R2TG5Sek9qUTBOd29nSUNBZ0x5OGdkR2hwY3k1d1lYSjBhWFJwYjI1ektHWnliMjFMWlhrcExuWmhiSFZsSUQwZ2JtVjNJR0Z5WXpRdVZXbHVkRTR5TlRZb2RHaHBjeTV3WVhKMGFYUnBiMjV6S0daeWIyMUxaWGtwTG5aaGJIVmxMbTVoZEdsMlpTQXRJR0Z0YjNWdWRDNXVZWFJwZG1VcENpQWdJQ0JrZFhBS0lDQWdJR0p2ZUY5blpYUUtJQ0FnSUdGemMyVnlkQ0F2THlCQ2IzZ2diWFZ6ZENCb1lYWmxJSFpoYkhWbENpQWdJQ0JtY21GdFpWOWthV2NnTFRJS0lDQWdJR0l0Q2lBZ0lDQmtkWEFLSUNBZ0lHeGxiZ29nSUNBZ2FXNTBZMTh5SUM4dklETXlDaUFnSUNBOFBRb2dJQ0FnWVhOelpYSjBJQzh2SUc5MlpYSm1iRzkzQ2lBZ0lDQnBiblJqWHpJZ0x5OGdNeklLSUNBZ0lHSjZaWEp2Q2lBZ0lDQmtkWEFLSUNBZ0lHWnlZVzFsWDJKMWNua2dNQW9nSUNBZ1lud0tJQ0FnSUdKdmVGOXdkWFFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem8xTXdvZ0lDQWdMeThnY0hWaWJHbGpJR0poYkdGdVkyVnpJRDBnUW05NFRXRndQRUZrWkhKbGMzTXNJRlZwYm5ST01qVTJQaWg3SUd0bGVWQnlaV1pwZURvZ0oySW5JSDBwQ2lBZ0lDQmllWFJsWXlBMElDOHZJQ0ppSWdvZ0lDQWdabkpoYldWZlpHbG5JQzAwQ2lBZ0lDQmpiMjVqWVhRS0lDQWdJR1IxY0FvZ0lDQWdabkpoYldWZlluVnllU0F5Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNVFF4TUM1aGJHZHZMblJ6T2pRME9Bb2dJQ0FnTHk4Z1lYTnpaWEowS0hSb2FYTXVZbUZzWVc1alpYTW9abkp2YlNrdVpYaHBjM1J6SUNZbUlIUm9hWE11WW1Gc1lXNWpaWE1vWm5KdmJTa3VkbUZzZFdVdWJtRjBhWFpsSUQ0OUlHRnRiM1Z1ZEM1dVlYUnBkbVVzSUNkSmJuTjFabVpwWTJsbGJuUWdZbUZzWVc1alpTY3BDaUFnSUNCaWIzaGZiR1Z1Q2lBZ0lDQmlkWEo1SURFS0lDQWdJR0o2SUdGeVl6RTBNVEJmYjNCbGNtRjBiM0pmY21Wa1pXVnRYMko1WDNCaGNuUnBkR2x2Ymw5aWIyOXNYMlpoYkhObFFEY0tJQ0FnSUdaeVlXMWxYMlJwWnlBeUNpQWdJQ0JpYjNoZloyVjBDaUFnSUNCaGMzTmxjblFnTHk4Z1FtOTRJRzExYzNRZ2FHRjJaU0IyWVd4MVpRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0JpUGowS0lDQWdJR0o2SUdGeVl6RTBNVEJmYjNCbGNtRjBiM0pmY21Wa1pXVnRYMko1WDNCaGNuUnBkR2x2Ymw5aWIyOXNYMlpoYkhObFFEY0tJQ0FnSUdsdWRHTmZNU0F2THlBeENncGhjbU14TkRFd1gyOXdaWEpoZEc5eVgzSmxaR1ZsYlY5aWVWOXdZWEowYVhScGIyNWZZbTl2YkY5dFpYSm5aVUE0T2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvME5EZ0tJQ0FnSUM4dklHRnpjMlZ5ZENoMGFHbHpMbUpoYkdGdVkyVnpLR1p5YjIwcExtVjRhWE4wY3lBbUppQjBhR2x6TG1KaGJHRnVZMlZ6S0daeWIyMHBMblpoYkhWbExtNWhkR2wyWlNBK1BTQmhiVzkxYm5RdWJtRjBhWFpsTENBblNXNXpkV1ptYVdOcFpXNTBJR0poYkdGdVkyVW5LUW9nSUNBZ1lYTnpaWEowSUM4dklFbHVjM1ZtWm1samFXVnVkQ0JpWVd4aGJtTmxDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1UUXhNQzVoYkdkdkxuUnpPalEwT1FvZ0lDQWdMeThnZEdocGN5NWlZV3hoYm1ObGN5aG1jbTl0S1M1MllXeDFaU0E5SUc1bGR5QmhjbU0wTGxWcGJuUk9NalUyS0hSb2FYTXVZbUZzWVc1alpYTW9abkp2YlNrdWRtRnNkV1V1Ym1GMGFYWmxJQzBnWVcxdmRXNTBMbTVoZEdsMlpTa0tJQ0FnSUdaeVlXMWxYMlJwWnlBeUNpQWdJQ0JrZFhBS0lDQWdJR0p2ZUY5blpYUUtJQ0FnSUdGemMyVnlkQ0F2THlCQ2IzZ2diWFZ6ZENCb1lYWmxJSFpoYkhWbENpQWdJQ0JtY21GdFpWOWthV2NnTFRJS0lDQWdJR0l0Q2lBZ0lDQmtkWEFLSUNBZ0lHeGxiZ29nSUNBZ2FXNTBZMTh5SUM4dklETXlDaUFnSUNBOFBRb2dJQ0FnWVhOelpYSjBJQzh2SUc5MlpYSm1iRzkzQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dNQW9nSUNBZ1pIVndDaUFnSUNCamIzWmxjaUF6Q2lBZ0lDQmlmQW9nSUNBZ1ltOTRYM0IxZEFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekl3TUM1aGJHZHZMblJ6T2pVeENpQWdJQ0F2THlCd2RXSnNhV01nZEc5MFlXeFRkWEJ3YkhrZ1BTQkhiRzlpWVd4VGRHRjBaVHhWYVc1MFRqSTFOajRvZXlCclpYazZJQ2QwSnlCOUtRb2dJQ0FnYVc1MFkxOHdJQzh2SURBS0lDQWdJR0o1ZEdWalh6TWdMeThnSW5RaUNpQWdJQ0JoY0hCZloyeHZZbUZzWDJkbGRGOWxlQW9nSUNBZ1lYTnpaWEowSUM4dklHTm9aV05ySUVkc2IySmhiRk4wWVhSbElHVjRhWE4wY3dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekUwTVRBdVlXeG5ieTUwY3pvME5UQUtJQ0FnSUM4dklIUm9hWE11ZEc5MFlXeFRkWEJ3YkhrdWRtRnNkV1VnUFNCdVpYY2dZWEpqTkM1VmFXNTBUakkxTmloMGFHbHpMblJ2ZEdGc1UzVndjR3g1TG5aaGJIVmxMbTVoZEdsMlpTQXRJR0Z0YjNWdWRDNXVZWFJwZG1VcENpQWdJQ0JtY21GdFpWOWthV2NnTFRJS0lDQWdJR0l0Q2lBZ0lDQmtkWEFLSUNBZ0lHeGxiZ29nSUNBZ2FXNTBZMTh5SUM4dklETXlDaUFnSUNBOFBRb2dJQ0FnWVhOelpYSjBJQzh2SUc5MlpYSm1iRzkzQ2lBZ0lDQmlmQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPalV4Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdkRzkwWVd4VGRYQndiSGtnUFNCSGJHOWlZV3hUZEdGMFpUeFZhVzUwVGpJMU5qNG9leUJyWlhrNklDZDBKeUI5S1FvZ0lDQWdZbmwwWldOZk15QXZMeUFpZENJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU14TkRFd0xtRnNaMjh1ZEhNNk5EVXdDaUFnSUNBdkx5QjBhR2x6TG5SdmRHRnNVM1Z3Y0d4NUxuWmhiSFZsSUQwZ2JtVjNJR0Z5WXpRdVZXbHVkRTR5TlRZb2RHaHBjeTUwYjNSaGJGTjFjSEJzZVM1MllXeDFaUzV1WVhScGRtVWdMU0JoYlc5MWJuUXVibUYwYVhabEtRb2dJQ0FnYzNkaGNBb2dJQ0FnWVhCd1gyZHNiMkpoYkY5d2RYUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeE5ERXdMbUZzWjI4dWRITTZORFV4Q2lBZ0lDQXZMeUJsYldsMEtDZFNaV1JsWlcwbkxDQnVaWGNnWVhKak1UUXhNRjl3WVhKMGFYUnBiMjVmY21Wa1pXVnRLSHNnWm5KdmJTd2djR0Z5ZEdsMGFXOXVMQ0JoYlc5MWJuUXNJR1JoZEdFZ2ZTa3BDaUFnSUNCbWNtRnRaVjlrYVdjZ01Rb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0JqYjI1allYUUtJQ0FnSUdKNWRHVmpJREkwSUM4dklEQjRNREEyTWdvZ0lDQWdZMjl1WTJGMENpQWdJQ0JtY21GdFpWOWthV2NnTFRFS0lDQWdJR052Ym1OaGRBb2dJQ0FnWW5sMFpXTWdOaUF2THlBd2VEQXdNRElLSUNBZ0lITjNZWEFLSUNBZ0lHTnZibU5oZEFvZ0lDQWdZbmwwWldNZ01qY2dMeThnYldWMGFHOWtJQ0pTWldSbFpXMG9LR0ZrWkhKbGMzTXNZV1JrY21WemN5eDFhVzUwTWpVMkxHSjVkR1ZiWFNrcElnb2dJQ0FnYzNkaGNBb2dJQ0FnWTI5dVkyRjBDaUFnSUNCc2IyY0tJQ0FnSUhKbGRITjFZZ29LWVhKak1UUXhNRjl2Y0dWeVlYUnZjbDl5WldSbFpXMWZZbmxmY0dGeWRHbDBhVzl1WDJKdmIyeGZabUZzYzJWQU56b0tJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0JpSUdGeVl6RTBNVEJmYjNCbGNtRjBiM0pmY21Wa1pXVnRYMko1WDNCaGNuUnBkR2x2Ymw5aWIyOXNYMjFsY21kbFFEZ0tDZ292THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk9rRnlZekl3TUM1aWIyOTBjM1J5WVhBb2JtRnRaVG9nWW5sMFpYTXNJSE41YldKdmJEb2dZbmwwWlhNc0lHUmxZMmx0WVd4ek9pQmllWFJsY3l3Z2RHOTBZV3hUZFhCd2JIazZJR0o1ZEdWektTQXRQaUJpZVhSbGN6b0tZbTl2ZEhOMGNtRndPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPalUyTFRVM0NpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvS1FvZ0lDQWdMeThnY0hWaWJHbGpJR0p2YjNSemRISmhjQ2h1WVcxbE9pQkVlVzVoYldsalFubDBaWE1zSUhONWJXSnZiRG9nUkhsdVlXMXBZMEo1ZEdWekxDQmtaV05wYldGc2N6b2dWV2x1ZEU0NExDQjBiM1JoYkZOMWNIQnNlVG9nVldsdWRFNHlOVFlwT2lCQ2IyOXNJSHNLSUNBZ0lIQnliM1J2SURRZ01Rb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpJd01DNWhiR2R2TG5Sek9qVTRDaUFnSUNBdkx5QmhjM05sY25Rb1ZIaHVMbk5sYm1SbGNpQTlQVDBnUjJ4dlltRnNMbU55WldGMGIzSkJaR1J5WlhOekxDQW5UMjVzZVNCa1pYQnNiM2xsY2lCdlppQjBhR2x6SUhOdFlYSjBJR052Ym5SeVlXTjBJR05oYmlCallXeHNJR0p2YjNSemRISmhjQ0J0WlhSb2IyUW5LUW9nSUNBZ2RIaHVJRk5sYm1SbGNnb2dJQ0FnWjJ4dlltRnNJRU55WldGMGIzSkJaR1J5WlhOekNpQWdJQ0E5UFFvZ0lDQWdZWE56WlhKMElDOHZJRTl1YkhrZ1pHVndiRzk1WlhJZ2IyWWdkR2hwY3lCemJXRnlkQ0JqYjI1MGNtRmpkQ0JqWVc0Z1kyRnNiQ0JpYjI5MGMzUnlZWEFnYldWMGFHOWtDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1qQXdMbUZzWjI4dWRITTZOVGtLSUNBZ0lDOHZJR0Z6YzJWeWRDaHVZVzFsTG01aGRHbDJaUzVzWlc1bmRHZ2dQaUF3TENBblRtRnRaU0J2WmlCMGFHVWdZWE56WlhRZ2JYVnpkQ0JpWlNCc2IyNW5aWElnYjNJZ1pYRjFZV3dnZEc4Z01TQmphR0Z5WVdOMFpYSW5LUW9nSUNBZ1puSmhiV1ZmWkdsbklDMDBDaUFnSUNCbGVIUnlZV04wSURJZ01Bb2dJQ0FnYkdWdUNpQWdJQ0JrZFhBS0lDQWdJR0Z6YzJWeWRDQXZMeUJPWVcxbElHOW1JSFJvWlNCaGMzTmxkQ0J0ZFhOMElHSmxJR3h2Ym1kbGNpQnZjaUJsY1hWaGJDQjBieUF4SUdOb1lYSmhZM1JsY2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekl3TUM1aGJHZHZMblJ6T2pZd0NpQWdJQ0F2THlCaGMzTmxjblFvYm1GdFpTNXVZWFJwZG1VdWJHVnVaM1JvSUR3OUlETXlMQ0FuVG1GdFpTQnZaaUIwYUdVZ1lYTnpaWFFnYlhWemRDQmlaU0J6YUc5eWRHVnlJRzl5SUdWeGRXRnNJSFJ2SURNeUlHTm9ZWEpoWTNSbGNuTW5LUW9nSUNBZ2FXNTBZMTh5SUM4dklETXlDaUFnSUNBOFBRb2dJQ0FnWVhOelpYSjBJQzh2SUU1aGJXVWdiMllnZEdobElHRnpjMlYwSUcxMWMzUWdZbVVnYzJodmNuUmxjaUJ2Y2lCbGNYVmhiQ0IwYnlBek1pQmphR0Z5WVdOMFpYSnpDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1qQXdMbUZzWjI4dWRITTZOakVLSUNBZ0lDOHZJR0Z6YzJWeWRDaHplVzFpYjJ3dWJtRjBhWFpsTG14bGJtZDBhQ0ErSURBc0lDZFRlVzFpYjJ3Z2IyWWdkR2hsSUdGemMyVjBJRzExYzNRZ1ltVWdiRzl1WjJWeUlHOXlJR1Z4ZFdGc0lIUnZJREVnWTJoaGNtRmpkR1Z5SnlrS0lDQWdJR1p5WVcxbFgyUnBaeUF0TXdvZ0lDQWdaWGgwY21GamRDQXlJREFLSUNBZ0lHeGxiZ29nSUNBZ1pIVndDaUFnSUNCaGMzTmxjblFnTHk4Z1UzbHRZbTlzSUc5bUlIUm9aU0JoYzNObGRDQnRkWE4wSUdKbElHeHZibWRsY2lCdmNpQmxjWFZoYkNCMGJ5QXhJR05vWVhKaFkzUmxjZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPall5Q2lBZ0lDQXZMeUJoYzNObGNuUW9jM2x0WW05c0xtNWhkR2wyWlM1c1pXNW5kR2dnUEQwZ09Dd2dKMU41YldKdmJDQnZaaUIwYUdVZ1lYTnpaWFFnYlhWemRDQmlaU0J6YUc5eWRHVnlJRzl5SUdWeGRXRnNJSFJ2SURnZ1kyaGhjbUZqZEdWeWN5Y3BDaUFnSUNCd2RYTm9hVzUwSURnZ0x5OGdPQW9nSUNBZ1BEMEtJQ0FnSUdGemMyVnlkQ0F2THlCVGVXMWliMndnYjJZZ2RHaGxJR0Z6YzJWMElHMTFjM1FnWW1VZ2MyaHZjblJsY2lCdmNpQmxjWFZoYkNCMGJ5QTRJR05vWVhKaFkzUmxjbk1LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem8xTVFvZ0lDQWdMeThnY0hWaWJHbGpJSFJ2ZEdGc1UzVndjR3g1SUQwZ1IyeHZZbUZzVTNSaGRHVThWV2x1ZEU0eU5UWStLSHNnYTJWNU9pQW5kQ2NnZlNrS0lDQWdJR2x1ZEdOZk1DQXZMeUF3Q2lBZ0lDQmllWFJsWTE4eklDOHZJQ0owSWdvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekl3TUM1aGJHZHZMblJ6T2pZekNpQWdJQ0F2THlCaGMzTmxjblFvSVhSb2FYTXVkRzkwWVd4VGRYQndiSGt1YUdGelZtRnNkV1VzSUNkVWFHbHpJRzFsZEdodlpDQmpZVzRnWW1VZ1kyRnNiR1ZrSUc5dWJIa2diMjVqWlNjcENpQWdJQ0JoY0hCZloyeHZZbUZzWDJkbGRGOWxlQW9nSUNBZ1luVnllU0F4Q2lBZ0lDQWhDaUFnSUNCaGMzTmxjblFnTHk4Z1ZHaHBjeUJ0WlhSb2IyUWdZMkZ1SUdKbElHTmhiR3hsWkNCdmJteDVJRzl1WTJVS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pvek9Rb2dJQ0FnTHk4Z2NIVmliR2xqSUc1aGJXVWdQU0JIYkc5aVlXeFRkR0YwWlR4RWVXNWhiV2xqUW5sMFpYTStLSHNnYTJWNU9pQW5iaWNnZlNrS0lDQWdJSEIxYzJoaWVYUmxjeUFpYmlJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pvMk5Rb2dJQ0FnTHk4Z2RHaHBjeTV1WVcxbExuWmhiSFZsSUQwZ2JtRnRaUW9nSUNBZ1puSmhiV1ZmWkdsbklDMDBDaUFnSUNCaGNIQmZaMnh2WW1Gc1gzQjFkQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPalF6Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdjM2x0WW05c0lEMGdSMnh2WW1Gc1UzUmhkR1U4UkhsdVlXMXBZMEo1ZEdWelBpaDdJR3RsZVRvZ0ozTW5JSDBwQ2lBZ0lDQndkWE5vWW5sMFpYTWdJbk1pQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNakF3TG1Gc1oyOHVkSE02TmpZS0lDQWdJQzh2SUhSb2FYTXVjM2x0WW05c0xuWmhiSFZsSUQwZ2MzbHRZbTlzQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVE1LSUNBZ0lHRndjRjluYkc5aVlXeGZjSFYwQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNakF3TG1Gc1oyOHVkSE02TlRFS0lDQWdJQzh2SUhCMVlteHBZeUIwYjNSaGJGTjFjSEJzZVNBOUlFZHNiMkpoYkZOMFlYUmxQRlZwYm5ST01qVTJQaWg3SUd0bGVUb2dKM1FuSUgwcENpQWdJQ0JpZVhSbFkxOHpJQzh2SUNKMElnb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpJd01DNWhiR2R2TG5Sek9qWTNDaUFnSUNBdkx5QjBhR2x6TG5SdmRHRnNVM1Z3Y0d4NUxuWmhiSFZsSUQwZ2RHOTBZV3hUZFhCd2JIa0tJQ0FnSUdaeVlXMWxYMlJwWnlBdE1Rb2dJQ0FnWVhCd1gyZHNiMkpoYkY5d2RYUUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeU1EQXVZV3huYnk1MGN6bzBOd29nSUNBZ0x5OGdjSFZpYkdsaklHUmxZMmx0WVd4eklEMGdSMnh2WW1Gc1UzUmhkR1U4VldsdWRFNDRQaWg3SUd0bGVUb2dKMlFuSUgwcENpQWdJQ0J3ZFhOb1lubDBaWE1nSW1RaUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk5qZ0tJQ0FnSUM4dklIUm9hWE11WkdWamFXMWhiSE11ZG1Gc2RXVWdQU0JrWldOcGJXRnNjd29nSUNBZ1puSmhiV1ZmWkdsbklDMHlDaUFnSUNCaGNIQmZaMnh2WW1Gc1gzQjFkQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPalk1Q2lBZ0lDQXZMeUJqYjI1emRDQnpaVzVrWlhJZ1BTQnVaWGNnUVdSa2NtVnpjeWhVZUc0dWMyVnVaR1Z5S1FvZ0lDQWdkSGh1SUZObGJtUmxjZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPalV6Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdZbUZzWVc1alpYTWdQU0JDYjNoTllYQThRV1JrY21WemN5d2dWV2x1ZEU0eU5UWStLSHNnYTJWNVVISmxabWw0T2lBbllpY2dmU2tLSUNBZ0lHSjVkR1ZqSURRZ0x5OGdJbUlpQ2lBZ0lDQmthV2NnTVFvZ0lDQWdZMjl1WTJGMENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk56RUtJQ0FnSUM4dklIUm9hWE11WW1Gc1lXNWpaWE1vYzJWdVpHVnlLUzUyWVd4MVpTQTlJSFJ2ZEdGc1UzVndjR3g1Q2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVEVLSUNBZ0lHSnZlRjl3ZFhRS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pvM013b2dJQ0FnTHk4Z1pXMXBkQ2h1WlhjZ1lYSmpNakF3WDFSeVlXNXpabVZ5S0hzZ1puSnZiVG9nYm1WM0lFRmtaSEpsYzNNb1IyeHZZbUZzTG5wbGNtOUJaR1J5WlhOektTd2dkRzg2SUhObGJtUmxjaXdnZG1Gc2RXVTZJSFJ2ZEdGc1UzVndjR3g1SUgwcEtRb2dJQ0FnWjJ4dlltRnNJRnBsY205QlpHUnlaWE56Q2lBZ0lDQnpkMkZ3Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR1p5WVcxbFgyUnBaeUF0TVFvZ0lDQWdZMjl1WTJGMENpQWdJQ0JpZVhSbFl5QXlPQ0F2THlCdFpYUm9iMlFnSW1GeVl6SXdNRjlVY21GdWMyWmxjaWhoWkdSeVpYTnpMR0ZrWkhKbGMzTXNkV2x1ZERJMU5pa2lDaUFnSUNCemQyRndDaUFnSUNCamIyNWpZWFFLSUNBZ0lHeHZad29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPamMwQ2lBZ0lDQXZMeUJ5WlhSMWNtNGdibVYzSUVKdmIyd29kSEoxWlNrS0lDQWdJR0o1ZEdWaklEY2dMeThnTUhnNE1Bb2dJQ0FnY21WMGMzVmlDZ29LTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpJd01DNWhiR2R2TG5Sek9qcEJjbU15TURBdVlYSmpNakF3WDI1aGJXVW9LU0F0UGlCaWVYUmxjem9LWVhKak1qQXdYMjVoYldVNkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk16a0tJQ0FnSUM4dklIQjFZbXhwWXlCdVlXMWxJRDBnUjJ4dlltRnNVM1JoZEdVOFJIbHVZVzFwWTBKNWRHVnpQaWg3SUd0bGVUb2dKMjRuSUgwcENpQWdJQ0JwYm5Salh6QWdMeThnTUFvZ0lDQWdjSFZ6YUdKNWRHVnpJQ0p1SWdvZ0lDQWdZWEJ3WDJkc2IySmhiRjluWlhSZlpYZ0tJQ0FnSUdGemMyVnlkQ0F2THlCamFHVmpheUJIYkc5aVlXeFRkR0YwWlNCbGVHbHpkSE1LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem80TkFvZ0lDQWdMeThnY21WMGRYSnVJRzVsZHlCVGRHRjBhV05DZVhSbGN6d3pNajRvZEdocGN5NXVZVzFsTG5aaGJIVmxMbTVoZEdsMlpTa0tJQ0FnSUdWNGRISmhZM1FnTWlBd0NpQWdJQ0JrZFhBS0lDQWdJR3hsYmdvZ0lDQWdhVzUwWTE4eUlDOHZJRE15Q2lBZ0lDQTlQUW9nSUNBZ1lYTnpaWEowSUM4dklHbHVkbUZzYVdRZ2MybDZaUW9nSUNBZ2NtVjBjM1ZpQ2dvS0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPanBCY21NeU1EQXVZWEpqTWpBd1gzTjViV0p2YkNncElDMCtJR0o1ZEdWek9ncGhjbU15TURCZmMzbHRZbTlzT2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekl3TUM1aGJHZHZMblJ6T2pRekNpQWdJQ0F2THlCd2RXSnNhV01nYzNsdFltOXNJRDBnUjJ4dlltRnNVM1JoZEdVOFJIbHVZVzFwWTBKNWRHVnpQaWg3SUd0bGVUb2dKM01uSUgwcENpQWdJQ0JwYm5Salh6QWdMeThnTUFvZ0lDQWdjSFZ6YUdKNWRHVnpJQ0p6SWdvZ0lDQWdZWEJ3WDJkc2IySmhiRjluWlhSZlpYZ0tJQ0FnSUdGemMyVnlkQ0F2THlCamFHVmpheUJIYkc5aVlXeFRkR0YwWlNCbGVHbHpkSE1LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem81TkFvZ0lDQWdMeThnY21WMGRYSnVJRzVsZHlCVGRHRjBhV05DZVhSbGN6dzRQaWgwYUdsekxuTjViV0p2YkM1MllXeDFaUzV1WVhScGRtVXBDaUFnSUNCbGVIUnlZV04wSURJZ01Bb2dJQ0FnWkhWd0NpQWdJQ0JzWlc0S0lDQWdJSEIxYzJocGJuUWdPQ0F2THlBNENpQWdJQ0E5UFFvZ0lDQWdZWE56WlhKMElDOHZJR2x1ZG1Gc2FXUWdjMmw2WlFvZ0lDQWdjbVYwYzNWaUNnb0tMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekl3TUM1aGJHZHZMblJ6T2pwQmNtTXlNREF1WVhKak1qQXdYMlJsWTJsdFlXeHpLQ2tnTFQ0Z1lubDBaWE02Q21GeVl6SXdNRjlrWldOcGJXRnNjem9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem8wTndvZ0lDQWdMeThnY0hWaWJHbGpJR1JsWTJsdFlXeHpJRDBnUjJ4dlltRnNVM1JoZEdVOFZXbHVkRTQ0UGloN0lHdGxlVG9nSjJRbklIMHBDaUFnSUNCcGJuUmpYekFnTHk4Z01Bb2dJQ0FnY0hWemFHSjVkR1Z6SUNKa0lnb2dJQ0FnWVhCd1gyZHNiMkpoYkY5blpYUmZaWGdLSUNBZ0lHRnpjMlZ5ZENBdkx5QmphR1ZqYXlCSGJHOWlZV3hUZEdGMFpTQmxlR2x6ZEhNS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pveE1EUUtJQ0FnSUM4dklISmxkSFZ5YmlCMGFHbHpMbVJsWTJsdFlXeHpMblpoYkhWbENpQWdJQ0J5WlhSemRXSUtDZ292THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk9rRnlZekl3TUM1aGNtTXlNREJmZEc5MFlXeFRkWEJ3Ykhrb0tTQXRQaUJpZVhSbGN6b0tZWEpqTWpBd1gzUnZkR0ZzVTNWd2NHeDVPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPalV4Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdkRzkwWVd4VGRYQndiSGtnUFNCSGJHOWlZV3hUZEdGMFpUeFZhVzUwVGpJMU5qNG9leUJyWlhrNklDZDBKeUI5S1FvZ0lDQWdhVzUwWTE4d0lDOHZJREFLSUNBZ0lHSjVkR1ZqWHpNZ0x5OGdJblFpQ2lBZ0lDQmhjSEJmWjJ4dlltRnNYMmRsZEY5bGVBb2dJQ0FnWVhOelpYSjBJQzh2SUdOb1pXTnJJRWRzYjJKaGJGTjBZWFJsSUdWNGFYTjBjd29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPakV4TkFvZ0lDQWdMeThnY21WMGRYSnVJSFJvYVhNdWRHOTBZV3hUZFhCd2JIa3VkbUZzZFdVS0lDQWdJSEpsZEhOMVlnb0tDaTh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pvNlFYSmpNakF3TG1GeVl6SXdNRjlpWVd4aGJtTmxUMllvYjNkdVpYSTZJR0o1ZEdWektTQXRQaUJpZVhSbGN6b0tZWEpqTWpBd1gySmhiR0Z1WTJWUFpqb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeU1EQXVZV3huYnk1MGN6b3hNak10TVRJMENpQWdJQ0F2THlCQVlYSmpOQzVoWW1sdFpYUm9iMlFvZXlCeVpXRmtiMjVzZVRvZ2RISjFaU0I5S1FvZ0lDQWdMeThnY0hWaWJHbGpJR0Z5WXpJd01GOWlZV3hoYm1ObFQyWW9iM2R1WlhJNklFRmtaSEpsYzNNcE9pQmhjbU0wTGxWcGJuUk9NalUySUhzS0lDQWdJSEJ5YjNSdklERWdNUW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPakV5TlFvZ0lDQWdMeThnY21WMGRYSnVJSFJvYVhNdVgySmhiR0Z1WTJWUFppaHZkMjVsY2lrS0lDQWdJR1p5WVcxbFgyUnBaeUF0TVFvZ0lDQWdZMkZzYkhOMVlpQmZZbUZzWVc1alpVOW1DaUFnSUNCeVpYUnpkV0lLQ2dvdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1qQXdMbUZzWjI4dWRITTZPa0Z5WXpJd01DNWhjbU15TURCZmRISmhibk5tWlhKR2NtOXRLR1p5YjIwNklHSjVkR1Z6TENCMGJ6b2dZbmwwWlhNc0lIWmhiSFZsT2lCaWVYUmxjeWtnTFQ0Z1lubDBaWE02Q21GeVl6SXdNRjkwY21GdWMyWmxja1p5YjIwNkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk1UUTRMVEUwT1FvZ0lDQWdMeThnUUdGeVl6UXVZV0pwYldWMGFHOWtLQ2tLSUNBZ0lDOHZJSEIxWW14cFl5QmhjbU15TURCZmRISmhibk5tWlhKR2NtOXRLR1p5YjIwNklFRmtaSEpsYzNNc0lIUnZPaUJCWkdSeVpYTnpMQ0IyWVd4MVpUb2dZWEpqTkM1VmFXNTBUakkxTmlrNklHRnlZelF1UW05dmJDQjdDaUFnSUNCd2NtOTBieUF6SURFS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pveE5UQUtJQ0FnSUM4dklHTnZibk4wSUhOd1pXNWtaWElnUFNCdVpYY2dRV1JrY21WemN5aFVlRzR1YzJWdVpHVnlLUW9nSUNBZ2RIaHVJRk5sYm1SbGNnb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpJd01DNWhiR2R2TG5Sek9qRTFNUW9nSUNBZ0x5OGdZMjl1YzNRZ2MzQmxibVJsY2w5aGJHeHZkMkZ1WTJVZ1BTQjBhR2x6TGw5aGJHeHZkMkZ1WTJVb1puSnZiU3dnYzNCbGJtUmxjaWtLSUNBZ0lHWnlZVzFsWDJScFp5QXRNd29nSUNBZ1pHbG5JREVLSUNBZ0lHTmhiR3h6ZFdJZ1gyRnNiRzkzWVc1alpRb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpJd01DNWhiR2R2TG5Sek9qRTFNZ29nSUNBZ0x5OGdZWE56WlhKMEtITndaVzVrWlhKZllXeHNiM2RoYm1ObExtNWhkR2wyWlNBK1BTQjJZV3gxWlM1dVlYUnBkbVVzSUNkcGJuTjFabVpwWTJsbGJuUWdZWEJ3Y205MllXd25LUW9nSUNBZ1pIVndDaUFnSUNCbWNtRnRaVjlrYVdjZ0xURUtJQ0FnSUdJK1BRb2dJQ0FnWVhOelpYSjBJQzh2SUdsdWMzVm1abWxqYVdWdWRDQmhjSEJ5YjNaaGJBb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpJd01DNWhiR2R2TG5Sek9qRTFNd29nSUNBZ0x5OGdZMjl1YzNRZ2JtVjNYM053Wlc1a1pYSmZZV3hzYjNkaGJtTmxJRDBnYm1WM0lGVnBiblJPTWpVMktITndaVzVrWlhKZllXeHNiM2RoYm1ObExtNWhkR2wyWlNBdElIWmhiSFZsTG01aGRHbDJaU2tLSUNBZ0lHWnlZVzFsWDJScFp5QXRNUW9nSUNBZ1lpMEtJQ0FnSUdSMWNBb2dJQ0FnYkdWdUNpQWdJQ0JwYm5Salh6SWdMeThnTXpJS0lDQWdJRHc5Q2lBZ0lDQmhjM05sY25RZ0x5OGdiM1psY21ac2IzY0tJQ0FnSUdsdWRHTmZNaUF2THlBek1nb2dJQ0FnWW5wbGNtOEtJQ0FnSUdKOENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk1UVTBDaUFnSUNBdkx5QjBhR2x6TGw5aGNIQnliM1psS0daeWIyMHNJSE53Wlc1a1pYSXNJRzVsZDE5emNHVnVaR1Z5WDJGc2JHOTNZVzVqWlNrS0lDQWdJR1p5WVcxbFgyUnBaeUF0TXdvZ0lDQWdZMjkyWlhJZ01nb2dJQ0FnWTJGc2JITjFZaUJmWVhCd2NtOTJaUW9nSUNBZ2NHOXdDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1qQXdMbUZzWjI4dWRITTZNVFUxQ2lBZ0lDQXZMeUJ5WlhSMWNtNGdkR2hwY3k1ZmRISmhibk5tWlhJb1puSnZiU3dnZEc4c0lIWmhiSFZsS1FvZ0lDQWdabkpoYldWZlpHbG5JQzB6Q2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVElLSUNBZ0lHWnlZVzFsWDJScFp5QXRNUW9nSUNBZ1kyRnNiSE4xWWlCZmRISmhibk5tWlhJS0lDQWdJSEpsZEhOMVlnb0tDaTh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pvNlFYSmpNakF3TG1GeVl6SXdNRjloY0hCeWIzWmxLSE53Wlc1a1pYSTZJR0o1ZEdWekxDQjJZV3gxWlRvZ1lubDBaWE1wSUMwK0lHSjVkR1Z6T2dwaGNtTXlNREJmWVhCd2NtOTJaVG9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem94TmpVdE1UWTJDaUFnSUNBdkx5QkFZWEpqTkM1aFltbHRaWFJvYjJRb0tRb2dJQ0FnTHk4Z2NIVmliR2xqSUdGeVl6SXdNRjloY0hCeWIzWmxLSE53Wlc1a1pYSTZJRUZrWkhKbGMzTXNJSFpoYkhWbE9pQmhjbU0wTGxWcGJuUk9NalUyS1RvZ1FtOXZiQ0I3Q2lBZ0lDQndjbTkwYnlBeUlERUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeU1EQXVZV3huYnk1MGN6b3hOamNLSUNBZ0lDOHZJR052Ym5OMElHOTNibVZ5SUQwZ2JtVjNJRUZrWkhKbGMzTW9WSGh1TG5ObGJtUmxjaWtLSUNBZ0lIUjRiaUJUWlc1a1pYSUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeU1EQXVZV3huYnk1MGN6b3hOamdLSUNBZ0lDOHZJSEpsZEhWeWJpQjBhR2x6TGw5aGNIQnliM1psS0c5M2JtVnlMQ0J6Y0dWdVpHVnlMQ0IyWVd4MVpTa0tJQ0FnSUdaeVlXMWxYMlJwWnlBdE1nb2dJQ0FnWm5KaGJXVmZaR2xuSUMweENpQWdJQ0JqWVd4c2MzVmlJRjloY0hCeWIzWmxDaUFnSUNCeVpYUnpkV0lLQ2dvdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1qQXdMbUZzWjI4dWRITTZPa0Z5WXpJd01DNWhjbU15TURCZllXeHNiM2RoYm1ObEtHOTNibVZ5T2lCaWVYUmxjeXdnYzNCbGJtUmxjam9nWW5sMFpYTXBJQzArSUdKNWRHVnpPZ3BoY21NeU1EQmZZV3hzYjNkaGJtTmxPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPakUzTnkweE56Z0tJQ0FnSUM4dklFQmhjbU0wTG1GaWFXMWxkR2h2WkNoN0lISmxZV1J2Ym14NU9pQjBjblZsSUgwcENpQWdJQ0F2THlCd2RXSnNhV01nWVhKak1qQXdYMkZzYkc5M1lXNWpaU2h2ZDI1bGNqb2dRV1JrY21WemN5d2djM0JsYm1SbGNqb2dRV1JrY21WemN5azZJR0Z5WXpRdVZXbHVkRTR5TlRZZ2V3b2dJQ0FnY0hKdmRHOGdNaUF4Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNakF3TG1Gc1oyOHVkSE02TVRjNUNpQWdJQ0F2THlCeVpYUjFjbTRnZEdocGN5NWZZV3hzYjNkaGJtTmxLRzkzYm1WeUxDQnpjR1Z1WkdWeUtRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0JtY21GdFpWOWthV2NnTFRFS0lDQWdJR05oYkd4emRXSWdYMkZzYkc5M1lXNWpaUW9nSUNBZ2NtVjBjM1ZpQ2dvS0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPanBCY21NeU1EQXVYMkpoYkdGdVkyVlBaaWh2ZDI1bGNqb2dZbmwwWlhNcElDMCtJR0o1ZEdWek9ncGZZbUZzWVc1alpVOW1PZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPakU0TWdvZ0lDQWdMeThnY0hKdmRHVmpkR1ZrSUY5aVlXeGhibU5sVDJZb2IzZHVaWEk2SUVGa1pISmxjM01wT2lCVmFXNTBUakkxTmlCN0NpQWdJQ0J3Y205MGJ5QXhJREVLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem8xTXdvZ0lDQWdMeThnY0hWaWJHbGpJR0poYkdGdVkyVnpJRDBnUW05NFRXRndQRUZrWkhKbGMzTXNJRlZwYm5ST01qVTJQaWg3SUd0bGVWQnlaV1pwZURvZ0oySW5JSDBwQ2lBZ0lDQmllWFJsWXlBMElDOHZJQ0ppSWdvZ0lDQWdabkpoYldWZlpHbG5JQzB4Q2lBZ0lDQmpiMjVqWVhRS0lDQWdJR1IxY0FvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekl3TUM1aGJHZHZMblJ6T2pFNE13b2dJQ0FnTHk4Z2FXWWdLQ0YwYUdsekxtSmhiR0Z1WTJWektHOTNibVZ5S1M1bGVHbHpkSE1wSUhKbGRIVnliaUJ1WlhjZ1ZXbHVkRTR5TlRZb01Da0tJQ0FnSUdKdmVGOXNaVzRLSUNBZ0lHSjFjbmtnTVFvZ0lDQWdZbTU2SUY5aVlXeGhibU5sVDJaZllXWjBaWEpmYVdaZlpXeHpaVUF5Q2lBZ0lDQmllWFJsWTE4eElDOHZJREI0TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TUFvZ0lDQWdjM2RoY0FvZ0lDQWdjbVYwYzNWaUNncGZZbUZzWVc1alpVOW1YMkZtZEdWeVgybG1YMlZzYzJWQU1qb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeU1EQXVZV3huYnk1MGN6b3hPRFFLSUNBZ0lDOHZJSEpsZEhWeWJpQjBhR2x6TG1KaGJHRnVZMlZ6S0c5M2JtVnlLUzUyWVd4MVpRb2dJQ0FnWm5KaGJXVmZaR2xuSURBS0lDQWdJR0p2ZUY5blpYUUtJQ0FnSUdGemMyVnlkQ0F2THlCQ2IzZ2diWFZ6ZENCb1lYWmxJSFpoYkhWbENpQWdJQ0J6ZDJGd0NpQWdJQ0J5WlhSemRXSUtDZ292THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk9rRnlZekl3TUM1ZmRISmhibk5tWlhJb2MyVnVaR1Z5T2lCaWVYUmxjeXdnY21WamFYQnBaVzUwT2lCaWVYUmxjeXdnWVcxdmRXNTBPaUJpZVhSbGN5a2dMVDRnWW5sMFpYTTZDbDkwY21GdWMyWmxjam9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem94T0RjS0lDQWdJQzh2SUhCeWIzUmxZM1JsWkNCZmRISmhibk5tWlhJb2MyVnVaR1Z5T2lCQlpHUnlaWE56TENCeVpXTnBjR2xsYm5RNklFRmtaSEpsYzNNc0lHRnRiM1Z1ZERvZ1ZXbHVkRTR5TlRZcE9pQkNiMjlzSUhzS0lDQWdJSEJ5YjNSdklETWdNUW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPakU0T0FvZ0lDQWdMeThnWTI5dWMzUWdjMlZ1WkdWeVgySmhiR0Z1WTJVZ1BTQjBhR2x6TGw5aVlXeGhibU5sVDJZb2MyVnVaR1Z5S1FvZ0lDQWdabkpoYldWZlpHbG5JQzB6Q2lBZ0lDQmpZV3hzYzNWaUlGOWlZV3hoYm1ObFQyWUtJQ0FnSUdSMWNBb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpJd01DNWhiR2R2TG5Sek9qRTRPUW9nSUNBZ0x5OGdZMjl1YzNRZ2NtVmphWEJwWlc1MFgySmhiR0Z1WTJVZ1BTQjBhR2x6TGw5aVlXeGhibU5sVDJZb2NtVmphWEJwWlc1MEtRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0JqWVd4c2MzVmlJRjlpWVd4aGJtTmxUMllLSUNBZ0lITjNZWEFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem94T1RBS0lDQWdJQzh2SUdGemMyVnlkQ2h6Wlc1a1pYSmZZbUZzWVc1alpTNXVZWFJwZG1VZ1BqMGdZVzF2ZFc1MExtNWhkR2wyWlN3Z0owbHVjM1ZtWm1samFXVnVkQ0JpWVd4aGJtTmxJR0YwSUhSb1pTQnpaVzVrWlhJZ1lXTmpiM1Z1ZENjcENpQWdJQ0JtY21GdFpWOWthV2NnTFRFS0lDQWdJR0krUFFvZ0lDQWdZWE56WlhKMElDOHZJRWx1YzNWbVptbGphV1Z1ZENCaVlXeGhibU5sSUdGMElIUm9aU0J6Wlc1a1pYSWdZV05qYjNWdWRBb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpJd01DNWhiR2R2TG5Sek9qRTVNZ29nSUNBZ0x5OGdhV1lnS0hObGJtUmxjaUFoUFQwZ2NtVmphWEJwWlc1MEtTQjdDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUTUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE1nb2dJQ0FnSVQwS0lDQWdJR0o2SUY5MGNtRnVjMlpsY2w5aFpuUmxjbDlwWmw5bGJITmxRRElLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem94T1RRS0lDQWdJQzh2SUhSb2FYTXVZbUZzWVc1alpYTW9jMlZ1WkdWeUtTNTJZV3gxWlNBOUlHNWxkeUJWYVc1MFRqSTFOaWh6Wlc1a1pYSmZZbUZzWVc1alpTNXVZWFJwZG1VZ0xTQmhiVzkxYm5RdWJtRjBhWFpsS1FvZ0lDQWdabkpoYldWZlpHbG5JREFLSUNBZ0lHWnlZVzFsWDJScFp5QXRNUW9nSUNBZ1lpMEtJQ0FnSUdSMWNBb2dJQ0FnYkdWdUNpQWdJQ0JwYm5Salh6SWdMeThnTXpJS0lDQWdJRHc5Q2lBZ0lDQmhjM05sY25RZ0x5OGdiM1psY21ac2IzY0tJQ0FnSUdsdWRHTmZNaUF2THlBek1nb2dJQ0FnWW5wbGNtOEtJQ0FnSUhOM1lYQUtJQ0FnSUdScFp5QXhDaUFnSUNCaWZBb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpJd01DNWhiR2R2TG5Sek9qVXpDaUFnSUNBdkx5QndkV0pzYVdNZ1ltRnNZVzVqWlhNZ1BTQkNiM2hOWVhBOFFXUmtjbVZ6Y3l3Z1ZXbHVkRTR5TlRZK0tIc2dhMlY1VUhKbFptbDRPaUFuWWljZ2ZTa0tJQ0FnSUdKNWRHVmpJRFFnTHk4Z0ltSWlDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUTUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPakU1TkFvZ0lDQWdMeThnZEdocGN5NWlZV3hoYm1ObGN5aHpaVzVrWlhJcExuWmhiSFZsSUQwZ2JtVjNJRlZwYm5ST01qVTJLSE5sYm1SbGNsOWlZV3hoYm1ObExtNWhkR2wyWlNBdElHRnRiM1Z1ZEM1dVlYUnBkbVVwQ2lBZ0lDQnpkMkZ3Q2lBZ0lDQmliM2hmY0hWMENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk1UazFDaUFnSUNBdkx5QjBhR2x6TG1KaGJHRnVZMlZ6S0hKbFkybHdhV1Z1ZENrdWRtRnNkV1VnUFNCdVpYY2dWV2x1ZEU0eU5UWW9jbVZqYVhCcFpXNTBYMkpoYkdGdVkyVXVibUYwYVhabElDc2dZVzF2ZFc1MExtNWhkR2wyWlNrS0lDQWdJR1p5WVcxbFgyUnBaeUF4Q2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVEVLSUNBZ0lHSXJDaUFnSUNCa2RYQUtJQ0FnSUd4bGJnb2dJQ0FnYVc1MFkxOHlJQzh2SURNeUNpQWdJQ0E4UFFvZ0lDQWdZWE56WlhKMElDOHZJRzkyWlhKbWJHOTNDaUFnSUNCaWZBb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpJd01DNWhiR2R2TG5Sek9qVXpDaUFnSUNBdkx5QndkV0pzYVdNZ1ltRnNZVzVqWlhNZ1BTQkNiM2hOWVhBOFFXUmtjbVZ6Y3l3Z1ZXbHVkRTR5TlRZK0tIc2dhMlY1VUhKbFptbDRPaUFuWWljZ2ZTa0tJQ0FnSUdKNWRHVmpJRFFnTHk4Z0ltSWlDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUSUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPakU1TlFvZ0lDQWdMeThnZEdocGN5NWlZV3hoYm1ObGN5aHlaV05wY0dsbGJuUXBMblpoYkhWbElEMGdibVYzSUZWcGJuUk9NalUyS0hKbFkybHdhV1Z1ZEY5aVlXeGhibU5sTG01aGRHbDJaU0FySUdGdGIzVnVkQzV1WVhScGRtVXBDaUFnSUNCemQyRndDaUFnSUNCaWIzaGZjSFYwQ2dwZmRISmhibk5tWlhKZllXWjBaWEpmYVdaZlpXeHpaVUF5T2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekl3TUM1aGJHZHZMblJ6T2pFNU53b2dJQ0FnTHk4Z1pXMXBkQ2h1WlhjZ1lYSmpNakF3WDFSeVlXNXpabVZ5S0hzZ1puSnZiVG9nYzJWdVpHVnlMQ0IwYnpvZ2NtVmphWEJwWlc1MExDQjJZV3gxWlRvZ1lXMXZkVzUwSUgwcEtRb2dJQ0FnWm5KaGJXVmZaR2xuSUMwekNpQWdJQ0JtY21GdFpWOWthV2NnTFRJS0lDQWdJR052Ym1OaGRBb2dJQ0FnWm5KaGJXVmZaR2xuSUMweENpQWdJQ0JqYjI1allYUUtJQ0FnSUdKNWRHVmpJREk0SUM4dklHMWxkR2h2WkNBaVlYSmpNakF3WDFSeVlXNXpabVZ5S0dGa1pISmxjM01zWVdSa2NtVnpjeXgxYVc1ME1qVTJLU0lLSUNBZ0lITjNZWEFLSUNBZ0lHTnZibU5oZEFvZ0lDQWdiRzluQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNakF3TG1Gc1oyOHVkSE02TVRrNENpQWdJQ0F2THlCeVpYUjFjbTRnYm1WM0lFSnZiMndvZEhKMVpTa0tJQ0FnSUdKNWRHVmpJRGNnTHk4Z01IZzRNQW9nSUNBZ1puSmhiV1ZmWW5WeWVTQXdDaUFnSUNCeVpYUnpkV0lLQ2dvdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1qQXdMbUZzWjI4dWRITTZPa0Z5WXpJd01DNWZZWEJ3Y205MllXeExaWGtvYjNkdVpYSTZJR0o1ZEdWekxDQnpjR1Z1WkdWeU9pQmllWFJsY3lrZ0xUNGdZbmwwWlhNNkNsOWhjSEJ5YjNaaGJFdGxlVG9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem95TURBS0lDQWdJQzh2SUhCeWIzUmxZM1JsWkNCZllYQndjbTkyWVd4TFpYa29iM2R1WlhJNklFRmtaSEpsYzNNc0lITndaVzVrWlhJNklFRmtaSEpsYzNNcE9pQlRkR0YwYVdOQ2VYUmxjend6TWo0Z2V3b2dJQ0FnY0hKdmRHOGdNaUF4Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNakF3TG1Gc1oyOHVkSE02TWpBeENpQWdJQ0F2THlCeVpYUjFjbTRnYm1WM0lGTjBZWFJwWTBKNWRHVnpQRE15UGlodmNDNXphR0V5TlRZb2IzQXVZMjl1WTJGMEtHOTNibVZ5TG1KNWRHVnpMQ0J6Y0dWdVpHVnlMbUo1ZEdWektTa3BDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUSUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE1Rb2dJQ0FnWTI5dVkyRjBDaUFnSUNCemFHRXlOVFlLSUNBZ0lHUjFjQW9nSUNBZ2JHVnVDaUFnSUNCcGJuUmpYeklnTHk4Z016SUtJQ0FnSUQwOUNpQWdJQ0JoYzNObGNuUWdMeThnYVc1MllXeHBaQ0J6YVhwbENpQWdJQ0J5WlhSemRXSUtDZ292THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk9rRnlZekl3TUM1ZllXeHNiM2RoYm1ObEtHOTNibVZ5T2lCaWVYUmxjeXdnYzNCbGJtUmxjam9nWW5sMFpYTXBJQzArSUdKNWRHVnpPZ3BmWVd4c2IzZGhibU5sT2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZekl3TUM1aGJHZHZMblJ6T2pJd05Bb2dJQ0FnTHk4Z2NISnZkR1ZqZEdWa0lGOWhiR3h2ZDJGdVkyVW9iM2R1WlhJNklFRmtaSEpsYzNNc0lITndaVzVrWlhJNklFRmtaSEpsYzNNcE9pQlZhVzUwVGpJMU5pQjdDaUFnSUNCd2NtOTBieUF5SURFS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pveU1EVUtJQ0FnSUM4dklHTnZibk4wSUd0bGVTQTlJSFJvYVhNdVgyRndjSEp2ZG1Gc1MyVjVLRzkzYm1WeUxDQnpjR1Z1WkdWeUtRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweUNpQWdJQ0JtY21GdFpWOWthV2NnTFRFS0lDQWdJR05oYkd4emRXSWdYMkZ3Y0hKdmRtRnNTMlY1Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNakF3TG1Gc1oyOHVkSE02TlRVS0lDQWdJQzh2SUhCMVlteHBZeUJoY0hCeWIzWmhiSE1nUFNCQ2IzaE5ZWEE4VTNSaGRHbGpRbmwwWlhNOE16SStMQ0JCY0hCeWIzWmhiRk4wY25WamRENG9leUJyWlhsUWNtVm1hWGc2SUNkaEp5QjlLUW9nSUNBZ2NIVnphR0o1ZEdWeklDSmhJZ29nSUNBZ2MzZGhjQW9nSUNBZ1kyOXVZMkYwQ2lBZ0lDQmtkWEFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTXlNREF1WVd4bmJ5NTBjem95TURZS0lDQWdJQzh2SUdsbUlDZ2hkR2hwY3k1aGNIQnliM1poYkhNb2EyVjVLUzVsZUdsemRITXBJSEpsZEhWeWJpQnVaWGNnVldsdWRFNHlOVFlvTUNrS0lDQWdJR0p2ZUY5c1pXNEtJQ0FnSUdKMWNua2dNUW9nSUNBZ1ltNTZJRjloYkd4dmQyRnVZMlZmWVdaMFpYSmZhV1pmWld4elpVQXlDaUFnSUNCaWVYUmxZMTh4SUM4dklEQjRNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNREF3TURBd01EQXdNQW9nSUNBZ2MzZGhjQW9nSUNBZ2NtVjBjM1ZpQ2dwZllXeHNiM2RoYm1ObFgyRm1kR1Z5WDJsbVgyVnNjMlZBTWpvS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pveU1EY0tJQ0FnSUM4dklISmxkSFZ5YmlCMGFHbHpMbUZ3Y0hKdmRtRnNjeWhyWlhrcExuWmhiSFZsTG1Gd2NISnZkbUZzUVcxdmRXNTBDaUFnSUNCbWNtRnRaVjlrYVdjZ01Bb2dJQ0FnWW05NFgyZGxkQW9nSUNBZ1lYTnpaWEowSUM4dklFSnZlQ0J0ZFhOMElHaGhkbVVnZG1Gc2RXVUtJQ0FnSUdWNGRISmhZM1FnTUNBek1pQXZMeUJ2YmlCbGNuSnZjam9nU1c1a1pYZ2dZV05qWlhOeklHbHpJRzkxZENCdlppQmliM1Z1WkhNS0lDQWdJSE4zWVhBS0lDQWdJSEpsZEhOMVlnb0tDaTh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pvNlFYSmpNakF3TGw5aGNIQnliM1psS0c5M2JtVnlPaUJpZVhSbGN5d2djM0JsYm1SbGNqb2dZbmwwWlhNc0lHRnRiM1Z1ZERvZ1lubDBaWE1wSUMwK0lHSjVkR1Z6T2dwZllYQndjbTkyWlRvS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU15TURBdVlXeG5ieTUwY3pveU1UQUtJQ0FnSUM4dklIQnliM1JsWTNSbFpDQmZZWEJ3Y205MlpTaHZkMjVsY2pvZ1FXUmtjbVZ6Y3l3Z2MzQmxibVJsY2pvZ1FXUmtjbVZ6Y3l3Z1lXMXZkVzUwT2lCVmFXNTBUakkxTmlrNklFSnZiMndnZXdvZ0lDQWdjSEp2ZEc4Z015QXhDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak1qQXdMbUZzWjI4dWRITTZNakV4Q2lBZ0lDQXZMeUJqYjI1emRDQnJaWGtnUFNCMGFHbHpMbDloY0hCeWIzWmhiRXRsZVNodmQyNWxjaXdnYzNCbGJtUmxjaWtLSUNBZ0lHWnlZVzFsWDJScFp5QXRNd29nSUNBZ1puSmhiV1ZmWkdsbklDMHlDaUFnSUNCallXeHNjM1ZpSUY5aGNIQnliM1poYkV0bGVRb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpJd01DNWhiR2R2TG5Sek9qSXhNaTB5TVRZS0lDQWdJQzh2SUdOdmJuTjBJR0Z3Y0hKdmRtRnNRbTk0T2lCQmNIQnliM1poYkZOMGNuVmpkQ0E5SUc1bGR5QkJjSEJ5YjNaaGJGTjBjblZqZENoN0NpQWdJQ0F2THlBZ0lHRndjSEp2ZG1Gc1FXMXZkVzUwT2lCaGJXOTFiblFzQ2lBZ0lDQXZMeUFnSUc5M2JtVnlPaUJ2ZDI1bGNpd0tJQ0FnSUM4dklDQWdjM0JsYm1SbGNqb2djM0JsYm1SbGNpd0tJQ0FnSUM4dklIMHBDaUFnSUNCbWNtRnRaVjlrYVdjZ0xURUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE13b2dJQ0FnWTI5dVkyRjBDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUSUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6SXdNQzVoYkdkdkxuUnpPalUxQ2lBZ0lDQXZMeUJ3ZFdKc2FXTWdZWEJ3Y205MllXeHpJRDBnUW05NFRXRndQRk4wWVhScFkwSjVkR1Z6UERNeVBpd2dRWEJ3Y205MllXeFRkSEoxWTNRK0tIc2dhMlY1VUhKbFptbDRPaUFuWVNjZ2ZTa0tJQ0FnSUhCMWMyaGllWFJsY3lBaVlTSUtJQ0FnSUhWdVkyOTJaWElnTWdvZ0lDQWdZMjl1WTJGMENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqTWpBd0xtRnNaMjh1ZEhNNk1qRTNDaUFnSUNBdkx5QjBhR2x6TG1Gd2NISnZkbUZzY3loclpYa3BMblpoYkhWbElEMGdZWEJ3Y205MllXeENiM2d1WTI5d2VTZ3BDaUFnSUNCemQyRndDaUFnSUNCaWIzaGZjSFYwQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpNakF3TG1Gc1oyOHVkSE02TWpFNENpQWdJQ0F2THlCbGJXbDBLRzVsZHlCaGNtTXlNREJmUVhCd2NtOTJZV3dvZXlCdmQyNWxjam9nYjNkdVpYSXNJSE53Wlc1a1pYSTZJSE53Wlc1a1pYSXNJSFpoYkhWbE9pQmhiVzkxYm5RZ2ZTa3BDaUFnSUNCbWNtRnRaVjlrYVdjZ0xUTUtJQ0FnSUdaeVlXMWxYMlJwWnlBdE1nb2dJQ0FnWTI5dVkyRjBDaUFnSUNCbWNtRnRaVjlrYVdjZ0xURUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ2NIVnphR0o1ZEdWeklEQjRNVGsyT1dZNE5qVWdMeThnYldWMGFHOWtJQ0poY21NeU1EQmZRWEJ3Y205MllXd29ZV1JrY21WemN5eGhaR1J5WlhOekxIVnBiblF5TlRZcElnb2dJQ0FnYzNkaGNBb2dJQ0FnWTI5dVkyRjBDaUFnSUNCc2IyY0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NeU1EQXVZV3huYnk1MGN6b3lNVGtLSUNBZ0lDOHZJSEpsZEhWeWJpQnVaWGNnUW05dmJDaDBjblZsS1FvZ0lDQWdZbmwwWldNZ055QXZMeUF3ZURnd0NpQWdJQ0J5WlhSemRXSUtDZ292THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqT0RndVlXeG5ieTUwY3pvNlFYSmpPRGd1WDJWdWMzVnlaVVJsWm1GMWJIUlBkMjVsY2lncElDMCtJSFp2YVdRNkNsOWxibk4xY21WRVpXWmhkV3gwVDNkdVpYSTZDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak9EZ3VZV3huYnk1MGN6b3hPUW9nSUNBZ0x5OGdjSFZpYkdsaklHbHVhWFJwWVd4cGVtVmtJRDBnUjJ4dlltRnNVM1JoZEdVOFlYSmpOQzVDZVhSbFBpaDdJR3RsZVRvZ0oyRnlZemc0WDI5cEp5QjlLU0F2THlBeElHbG1JR2x1YVhScFlXeHBlbVZrSUNobGVIQnNhV05wZENCdmNpQnBiWEJzYVdOcGRDa0tJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0JpZVhSbFl5QXhNaUF2THlBaVlYSmpPRGhmYjJraUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqT0RndVlXeG5ieTUwY3pveU53b2dJQ0FnTHk4Z2FXWWdLQ0YwYUdsekxtbHVhWFJwWVd4cGVtVmtMbWhoYzFaaGJIVmxJSHg4SUhSb2FYTXVhVzVwZEdsaGJHbDZaV1F1ZG1Gc2RXVXVibUYwYVhabElEMDlQU0F3S1NCN0NpQWdJQ0JoY0hCZloyeHZZbUZzWDJkbGRGOWxlQW9nSUNBZ1luVnllU0F4Q2lBZ0lDQmllaUJmWlc1emRYSmxSR1ZtWVhWc2RFOTNibVZ5WDJsbVgySnZaSGxBTWdvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZemc0TG1Gc1oyOHVkSE02TVRrS0lDQWdJQzh2SUhCMVlteHBZeUJwYm1sMGFXRnNhWHBsWkNBOUlFZHNiMkpoYkZOMFlYUmxQR0Z5WXpRdVFubDBaVDRvZXlCclpYazZJQ2RoY21NNE9GOXZhU2NnZlNrZ0x5OGdNU0JwWmlCcGJtbDBhV0ZzYVhwbFpDQW9aWGh3YkdsamFYUWdiM0lnYVcxd2JHbGphWFFwQ2lBZ0lDQnBiblJqWHpBZ0x5OGdNQW9nSUNBZ1lubDBaV01nTVRJZ0x5OGdJbUZ5WXpnNFgyOXBJZ29nSUNBZ1lYQndYMmRzYjJKaGJGOW5aWFJmWlhnS0lDQWdJR0Z6YzJWeWRDQXZMeUJqYUdWamF5QkhiRzlpWVd4VGRHRjBaU0JsZUdsemRITUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NNE9DNWhiR2R2TG5Sek9qSTNDaUFnSUNBdkx5QnBaaUFvSVhSb2FYTXVhVzVwZEdsaGJHbDZaV1F1YUdGelZtRnNkV1VnZkh3Z2RHaHBjeTVwYm1sMGFXRnNhWHBsWkM1MllXeDFaUzV1WVhScGRtVWdQVDA5SURBcElIc0tJQ0FnSUdKMGIya0tJQ0FnSUdKdWVpQmZaVzV6ZFhKbFJHVm1ZWFZzZEU5M2JtVnlYMkZtZEdWeVgybG1YMlZzYzJWQU5Rb0tYMlZ1YzNWeVpVUmxabUYxYkhSUGQyNWxjbDlwWmw5aWIyUjVRREk2Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpPRGd1WVd4bmJ5NTBjem94TndvZ0lDQWdMeThnY0hWaWJHbGpJRzkzYm1WeUlEMGdSMnh2WW1Gc1UzUmhkR1U4WVhKak5DNUJaR1J5WlhOelBpaDdJR3RsZVRvZ0oyRnlZemc0WDI4bklIMHBDaUFnSUNCcGJuUmpYekFnTHk4Z01Bb2dJQ0FnWW5sMFpXTmZNaUF2THlBaVlYSmpPRGhmYnlJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU00T0M1aGJHZHZMblJ6T2pJNENpQWdJQ0F2THlCcFppQW9JWFJvYVhNdWIzZHVaWEl1YUdGelZtRnNkV1VwSUhzS0lDQWdJR0Z3Y0Y5bmJHOWlZV3hmWjJWMFgyVjRDaUFnSUNCaWRYSjVJREVLSUNBZ0lHSnVlaUJmWlc1emRYSmxSR1ZtWVhWc2RFOTNibVZ5WDJGbWRHVnlYMmxtWDJWc2MyVkFOQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZNVGNLSUNBZ0lDOHZJSEIxWW14cFl5QnZkMjVsY2lBOUlFZHNiMkpoYkZOMFlYUmxQR0Z5WXpRdVFXUmtjbVZ6Y3o0b2V5QnJaWGs2SUNkaGNtTTRPRjl2SnlCOUtRb2dJQ0FnWW5sMFpXTmZNaUF2THlBaVlYSmpPRGhmYnlJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU00T0M1aGJHZHZMblJ6T2pJNUNpQWdJQ0F2THlCMGFHbHpMbTkzYm1WeUxuWmhiSFZsSUQwZ2JtVjNJR0Z5WXpRdVFXUmtjbVZ6Y3loSGJHOWlZV3d1WTNKbFlYUnZja0ZrWkhKbGMzTXBDaUFnSUNCbmJHOWlZV3dnUTNKbFlYUnZja0ZrWkhKbGMzTUtJQ0FnSUdGd2NGOW5iRzlpWVd4ZmNIVjBDZ3BmWlc1emRYSmxSR1ZtWVhWc2RFOTNibVZ5WDJGbWRHVnlYMmxtWDJWc2MyVkFORG9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPakU1Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdhVzVwZEdsaGJHbDZaV1FnUFNCSGJHOWlZV3hUZEdGMFpUeGhjbU0wTGtKNWRHVStLSHNnYTJWNU9pQW5ZWEpqT0RoZmIya25JSDBwSUM4dklERWdhV1lnYVc1cGRHbGhiR2w2WldRZ0tHVjRjR3hwWTJsMElHOXlJR2x0Y0d4cFkybDBLUW9nSUNBZ1lubDBaV01nTVRJZ0x5OGdJbUZ5WXpnNFgyOXBJZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZNekVLSUNBZ0lDOHZJSFJvYVhNdWFXNXBkR2xoYkdsNlpXUXVkbUZzZFdVZ1BTQnVaWGNnWVhKak5DNUNlWFJsS0RFcENpQWdJQ0JpZVhSbFl5QXlNaUF2THlBd2VEQXhDaUFnSUNCaGNIQmZaMnh2WW1Gc1gzQjFkQW9LWDJWdWMzVnlaVVJsWm1GMWJIUlBkMjVsY2w5aFpuUmxjbDlwWmw5bGJITmxRRFU2Q2lBZ0lDQnlaWFJ6ZFdJS0Nnb3ZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpPRGd1WVd4bmJ5NTBjem82UVhKak9EZ3VZWEpqT0RoZmIzZHVaWElvS1NBdFBpQmllWFJsY3pvS1lYSmpPRGhmYjNkdVpYSTZDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak9EZ3VZV3huYnk1MGN6b3pOd29nSUNBZ0x5OGdkR2hwY3k1ZlpXNXpkWEpsUkdWbVlYVnNkRTkzYm1WeUtDa0tJQ0FnSUdOaGJHeHpkV0lnWDJWdWMzVnlaVVJsWm1GMWJIUlBkMjVsY2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZemc0TG1Gc1oyOHVkSE02TVRjS0lDQWdJQzh2SUhCMVlteHBZeUJ2ZDI1bGNpQTlJRWRzYjJKaGJGTjBZWFJsUEdGeVl6UXVRV1JrY21WemN6NG9leUJyWlhrNklDZGhjbU00T0Y5dkp5QjlLUW9nSUNBZ2FXNTBZMTh3SUM4dklEQUtJQ0FnSUdKNWRHVmpYeklnTHk4Z0ltRnlZemc0WDI4aUNpQWdJQ0JoY0hCZloyeHZZbUZzWDJkbGRGOWxlQW9nSUNBZ1lYTnpaWEowSUM4dklHTm9aV05ySUVkc2IySmhiRk4wWVhSbElHVjRhWE4wY3dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZemc0TG1Gc1oyOHVkSE02TXpnS0lDQWdJQzh2SUhKbGRIVnliaUIwYUdsekxtOTNibVZ5TG5aaGJIVmxDaUFnSUNCeVpYUnpkV0lLQ2dvdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak9EZ3VZV3huYnk1MGN6bzZRWEpqT0RndVlYSmpPRGhmYVhOZmIzZHVaWElvY1hWbGNuazZJR0o1ZEdWektTQXRQaUJpZVhSbGN6b0tZWEpqT0RoZmFYTmZiM2R1WlhJNkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqT0RndVlXeG5ieTUwY3pvME1TMDBNZ29nSUNBZ0x5OGdRR0Z5WXpRdVlXSnBiV1YwYUc5a0tIc2djbVZoWkc5dWJIazZJSFJ5ZFdVZ2ZTa0tJQ0FnSUM4dklIQjFZbXhwWXlCaGNtTTRPRjlwYzE5dmQyNWxjaWh4ZFdWeWVUb2dZWEpqTkM1QlpHUnlaWE56S1RvZ1lYSmpOQzVDYjI5c0lIc0tJQ0FnSUhCeWIzUnZJREVnTVFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZemc0TG1Gc1oyOHVkSE02TkRNS0lDQWdJQzh2SUhSb2FYTXVYMlZ1YzNWeVpVUmxabUYxYkhSUGQyNWxjaWdwQ2lBZ0lDQmpZV3hzYzNWaUlGOWxibk4xY21WRVpXWmhkV3gwVDNkdVpYSUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NNE9DNWhiR2R2TG5Sek9qRTNDaUFnSUNBdkx5QndkV0pzYVdNZ2IzZHVaWElnUFNCSGJHOWlZV3hUZEdGMFpUeGhjbU0wTGtGa1pISmxjM00rS0hzZ2EyVjVPaUFuWVhKak9EaGZieWNnZlNrS0lDQWdJR2x1ZEdOZk1DQXZMeUF3Q2lBZ0lDQmllWFJsWTE4eUlDOHZJQ0poY21NNE9GOXZJZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZORFFLSUNBZ0lDOHZJR2xtSUNnaGRHaHBjeTV2ZDI1bGNpNW9ZWE5XWVd4MVpTa2djbVYwZFhKdUlHNWxkeUJoY21NMExrSnZiMndvWm1Gc2MyVXBDaUFnSUNCaGNIQmZaMnh2WW1Gc1gyZGxkRjlsZUFvZ0lDQWdZblZ5ZVNBeENpQWdJQ0JpYm5vZ1lYSmpPRGhmYVhOZmIzZHVaWEpmWVdaMFpYSmZhV1pmWld4elpVQXlDaUFnSUNCaWVYUmxZeUF4TVNBdkx5QXdlREF3Q2lBZ0lDQnlaWFJ6ZFdJS0NtRnlZemc0WDJselgyOTNibVZ5WDJGbWRHVnlYMmxtWDJWc2MyVkFNam9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPakUzQ2lBZ0lDQXZMeUJ3ZFdKc2FXTWdiM2R1WlhJZ1BTQkhiRzlpWVd4VGRHRjBaVHhoY21NMExrRmtaSEpsYzNNK0tIc2dhMlY1T2lBbllYSmpPRGhmYnljZ2ZTa0tJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0JpZVhSbFkxOHlJQzh2SUNKaGNtTTRPRjl2SWdvZ0lDQWdZWEJ3WDJkc2IySmhiRjluWlhSZlpYZ0tJQ0FnSUdGemMyVnlkQ0F2THlCamFHVmpheUJIYkc5aVlXeFRkR0YwWlNCbGVHbHpkSE1LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPalExQ2lBZ0lDQXZMeUJwWmlBb2RHaHBjeTV2ZDI1bGNpNTJZV3gxWlNBOVBUMGdibVYzSUdGeVl6UXVRV1JrY21WemN5Z3BLU0J5WlhSMWNtNGdibVYzSUdGeVl6UXVRbTl2YkNobVlXeHpaU2tLSUNBZ0lHSjVkR1ZqWHpFZ0x5OGdZV1JrY2lCQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCV1RWSVJrdFJDaUFnSUNBOVBRb2dJQ0FnWW5vZ1lYSmpPRGhmYVhOZmIzZHVaWEpmWVdaMFpYSmZhV1pmWld4elpVQTBDaUFnSUNCaWVYUmxZeUF4TVNBdkx5QXdlREF3Q2lBZ0lDQnlaWFJ6ZFdJS0NtRnlZemc0WDJselgyOTNibVZ5WDJGbWRHVnlYMmxtWDJWc2MyVkFORG9LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPakUzQ2lBZ0lDQXZMeUJ3ZFdKc2FXTWdiM2R1WlhJZ1BTQkhiRzlpWVd4VGRHRjBaVHhoY21NMExrRmtaSEpsYzNNK0tIc2dhMlY1T2lBbllYSmpPRGhmYnljZ2ZTa0tJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0JpZVhSbFkxOHlJQzh2SUNKaGNtTTRPRjl2SWdvZ0lDQWdZWEJ3WDJkc2IySmhiRjluWlhSZlpYZ0tJQ0FnSUdGemMyVnlkQ0F2THlCamFHVmpheUJIYkc5aVlXeFRkR0YwWlNCbGVHbHpkSE1LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPalEyQ2lBZ0lDQXZMeUJ5WlhSMWNtNGdibVYzSUdGeVl6UXVRbTl2YkNoMGFHbHpMbTkzYm1WeUxuWmhiSFZsSUQwOVBTQnhkV1Z5ZVNrS0lDQWdJR1p5WVcxbFgyUnBaeUF0TVFvZ0lDQWdQVDBLSUNBZ0lHSjVkR1ZqSURFeElDOHZJREI0TURBS0lDQWdJR2x1ZEdOZk1DQXZMeUF3Q2lBZ0lDQjFibU52ZG1WeUlESUtJQ0FnSUhObGRHSnBkQW9nSUNBZ2NtVjBjM1ZpQ2dvS0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZPa0Z5WXpnNExtRnlZemc0WDJsdWFYUnBZV3hwZW1WZmIzZHVaWElvYm1WM1gyOTNibVZ5T2lCaWVYUmxjeWtnTFQ0Z2RtOXBaRG9LWVhKak9EaGZhVzVwZEdsaGJHbDZaVjl2ZDI1bGNqb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NNE9DNWhiR2R2TG5Sek9qVXdMVFV4Q2lBZ0lDQXZMeUJBWVhKak5DNWhZbWx0WlhSb2IyUW9LUW9nSUNBZ0x5OGdjSFZpYkdsaklHRnlZemc0WDJsdWFYUnBZV3hwZW1WZmIzZHVaWElvYm1WM1gyOTNibVZ5T2lCaGNtTTBMa0ZrWkhKbGMzTXBPaUIyYjJsa0lIc0tJQ0FnSUhCeWIzUnZJREVnTUFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZemc0TG1Gc1oyOHVkSE02TVRrS0lDQWdJQzh2SUhCMVlteHBZeUJwYm1sMGFXRnNhWHBsWkNBOUlFZHNiMkpoYkZOMFlYUmxQR0Z5WXpRdVFubDBaVDRvZXlCclpYazZJQ2RoY21NNE9GOXZhU2NnZlNrZ0x5OGdNU0JwWmlCcGJtbDBhV0ZzYVhwbFpDQW9aWGh3YkdsamFYUWdiM0lnYVcxd2JHbGphWFFwQ2lBZ0lDQnBiblJqWHpBZ0x5OGdNQW9nSUNBZ1lubDBaV01nTVRJZ0x5OGdJbUZ5WXpnNFgyOXBJZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZOVElLSUNBZ0lDOHZJR0Z6YzJWeWRDZ2hLSFJvYVhNdWFXNXBkR2xoYkdsNlpXUXVhR0Z6Vm1Gc2RXVWdKaVlnZEdocGN5NXBibWwwYVdGc2FYcGxaQzUyWVd4MVpTNXVZWFJwZG1VZ1BUMDlJREVwTENBbllXeHlaV0ZrZVY5cGJtbDBhV0ZzYVhwbFpDY3BDaUFnSUNCaGNIQmZaMnh2WW1Gc1gyZGxkRjlsZUFvZ0lDQWdZblZ5ZVNBeENpQWdJQ0JpZWlCaGNtTTRPRjlwYm1sMGFXRnNhWHBsWDI5M2JtVnlYMkp2YjJ4ZlptRnNjMlZBTXdvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZemc0TG1Gc1oyOHVkSE02TVRrS0lDQWdJQzh2SUhCMVlteHBZeUJwYm1sMGFXRnNhWHBsWkNBOUlFZHNiMkpoYkZOMFlYUmxQR0Z5WXpRdVFubDBaVDRvZXlCclpYazZJQ2RoY21NNE9GOXZhU2NnZlNrZ0x5OGdNU0JwWmlCcGJtbDBhV0ZzYVhwbFpDQW9aWGh3YkdsamFYUWdiM0lnYVcxd2JHbGphWFFwQ2lBZ0lDQnBiblJqWHpBZ0x5OGdNQW9nSUNBZ1lubDBaV01nTVRJZ0x5OGdJbUZ5WXpnNFgyOXBJZ29nSUNBZ1lYQndYMmRzYjJKaGJGOW5aWFJmWlhnS0lDQWdJR0Z6YzJWeWRDQXZMeUJqYUdWamF5QkhiRzlpWVd4VGRHRjBaU0JsZUdsemRITUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NNE9DNWhiR2R2TG5Sek9qVXlDaUFnSUNBdkx5QmhjM05sY25Rb0lTaDBhR2x6TG1sdWFYUnBZV3hwZW1Wa0xtaGhjMVpoYkhWbElDWW1JSFJvYVhNdWFXNXBkR2xoYkdsNlpXUXVkbUZzZFdVdWJtRjBhWFpsSUQwOVBTQXhLU3dnSjJGc2NtVmhaSGxmYVc1cGRHbGhiR2w2WldRbktRb2dJQ0FnWW5SdmFRb2dJQ0FnYVc1MFkxOHhJQzh2SURFS0lDQWdJRDA5Q2lBZ0lDQmllaUJoY21NNE9GOXBibWwwYVdGc2FYcGxYMjkzYm1WeVgySnZiMnhmWm1Gc2MyVkFNd29nSUNBZ2FXNTBZMTh4SUM4dklERUtDbUZ5WXpnNFgybHVhWFJwWVd4cGVtVmZiM2R1WlhKZlltOXZiRjl0WlhKblpVQTBPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZOVElLSUNBZ0lDOHZJR0Z6YzJWeWRDZ2hLSFJvYVhNdWFXNXBkR2xoYkdsNlpXUXVhR0Z6Vm1Gc2RXVWdKaVlnZEdocGN5NXBibWwwYVdGc2FYcGxaQzUyWVd4MVpTNXVZWFJwZG1VZ1BUMDlJREVwTENBbllXeHlaV0ZrZVY5cGJtbDBhV0ZzYVhwbFpDY3BDaUFnSUNBaENpQWdJQ0JoYzNObGNuUWdMeThnWVd4eVpXRmtlVjlwYm1sMGFXRnNhWHBsWkFvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZemc0TG1Gc1oyOHVkSE02TlRNS0lDQWdJQzh2SUdGemMyVnlkQ2h1WlhkZmIzZHVaWElnSVQwOUlHNWxkeUJoY21NMExrRmtaSEpsYzNNb0tTd2dKM3BsY205ZllXUmtjbVZ6YzE5dWIzUmZZV3hzYjNkbFpDY3BDaUFnSUNCbWNtRnRaVjlrYVdjZ0xURUtJQ0FnSUdKNWRHVmpYekVnTHk4Z1lXUmtjaUJCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJXVFZJUmt0UkNpQWdJQ0FoUFFvZ0lDQWdZWE56WlhKMElDOHZJSHBsY205ZllXUmtjbVZ6YzE5dWIzUmZZV3hzYjNkbFpBb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpnNExtRnNaMjh1ZEhNNk1UY0tJQ0FnSUM4dklIQjFZbXhwWXlCdmQyNWxjaUE5SUVkc2IySmhiRk4wWVhSbFBHRnlZelF1UVdSa2NtVnpjejRvZXlCclpYazZJQ2RoY21NNE9GOXZKeUI5S1FvZ0lDQWdZbmwwWldOZk1pQXZMeUFpWVhKak9EaGZieUlLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPalUwQ2lBZ0lDQXZMeUIwYUdsekxtOTNibVZ5TG5aaGJIVmxJRDBnYm1WM1gyOTNibVZ5Q2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVEVLSUNBZ0lHRndjRjluYkc5aVlXeGZjSFYwQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpPRGd1WVd4bmJ5NTBjem94T1FvZ0lDQWdMeThnY0hWaWJHbGpJR2x1YVhScFlXeHBlbVZrSUQwZ1IyeHZZbUZzVTNSaGRHVThZWEpqTkM1Q2VYUmxQaWg3SUd0bGVUb2dKMkZ5WXpnNFgyOXBKeUI5S1NBdkx5QXhJR2xtSUdsdWFYUnBZV3hwZW1Wa0lDaGxlSEJzYVdOcGRDQnZjaUJwYlhCc2FXTnBkQ2tLSUNBZ0lHSjVkR1ZqSURFeUlDOHZJQ0poY21NNE9GOXZhU0lLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPalUxQ2lBZ0lDQXZMeUIwYUdsekxtbHVhWFJwWVd4cGVtVmtMblpoYkhWbElEMGdibVYzSUdGeVl6UXVRbmwwWlNneEtRb2dJQ0FnWW5sMFpXTWdNaklnTHk4Z01IZ3dNUW9nSUNBZ1lYQndYMmRzYjJKaGJGOXdkWFFLSUNBZ0lISmxkSE4xWWdvS1lYSmpPRGhmYVc1cGRHbGhiR2w2WlY5dmQyNWxjbDlpYjI5c1gyWmhiSE5sUURNNkNpQWdJQ0JwYm5Salh6QWdMeThnTUFvZ0lDQWdZaUJoY21NNE9GOXBibWwwYVdGc2FYcGxYMjkzYm1WeVgySnZiMnhmYldWeVoyVkFOQW9LQ2k4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NNE9DNWhiR2R2TG5Sek9qcEJjbU00T0M1aGNtTTRPRjkwY21GdWMyWmxjbDl2ZDI1bGNuTm9hWEFvYm1WM1gyOTNibVZ5T2lCaWVYUmxjeWtnTFQ0Z2RtOXBaRG9LWVhKak9EaGZkSEpoYm5ObVpYSmZiM2R1WlhKemFHbHdPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZOVGd0TlRrS0lDQWdJQzh2SUVCaGNtTTBMbUZpYVcxbGRHaHZaQ2dwQ2lBZ0lDQXZMeUJ3ZFdKc2FXTWdZWEpqT0RoZmRISmhibk5tWlhKZmIzZHVaWEp6YUdsd0tHNWxkMTl2ZDI1bGNqb2dZWEpqTkM1QlpHUnlaWE56S1RvZ2RtOXBaQ0I3Q2lBZ0lDQndjbTkwYnlBeElEQUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NNE9DNWhiR2R2TG5Sek9qWXdDaUFnSUNBdkx5QjBhR2x6TGw5bGJuTjFjbVZFWldaaGRXeDBUM2R1WlhJb0tRb2dJQ0FnWTJGc2JITjFZaUJmWlc1emRYSmxSR1ZtWVhWc2RFOTNibVZ5Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpPRGd1WVd4bmJ5NTBjem8yTVFvZ0lDQWdMeThnWVhOelpYSjBLRzVsZHlCaGNtTTBMa0ZrWkhKbGMzTW9WSGh1TG5ObGJtUmxjaWtnUFQwOUlIUm9hWE11YjNkdVpYSXVkbUZzZFdVc0lDZHViM1JmYjNkdVpYSW5LUW9nSUNBZ2RIaHVJRk5sYm1SbGNnb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpnNExtRnNaMjh1ZEhNNk1UY0tJQ0FnSUM4dklIQjFZbXhwWXlCdmQyNWxjaUE5SUVkc2IySmhiRk4wWVhSbFBHRnlZelF1UVdSa2NtVnpjejRvZXlCclpYazZJQ2RoY21NNE9GOXZKeUI5S1FvZ0lDQWdhVzUwWTE4d0lDOHZJREFLSUNBZ0lHSjVkR1ZqWHpJZ0x5OGdJbUZ5WXpnNFgyOGlDaUFnSUNCaGNIQmZaMnh2WW1Gc1gyZGxkRjlsZUFvZ0lDQWdZWE56WlhKMElDOHZJR05vWldOcklFZHNiMkpoYkZOMFlYUmxJR1Y0YVhOMGN3b2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpnNExtRnNaMjh1ZEhNNk5qRUtJQ0FnSUM4dklHRnpjMlZ5ZENodVpYY2dZWEpqTkM1QlpHUnlaWE56S0ZSNGJpNXpaVzVrWlhJcElEMDlQU0IwYUdsekxtOTNibVZ5TG5aaGJIVmxMQ0FuYm05MFgyOTNibVZ5SnlrS0lDQWdJRDA5Q2lBZ0lDQmhjM05sY25RZ0x5OGdibTkwWDI5M2JtVnlDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak9EZ3VZV3huYnk1MGN6bzJNZ29nSUNBZ0x5OGdZWE56WlhKMEtHNWxkMTl2ZDI1bGNpQWhQVDBnYm1WM0lHRnlZelF1UVdSa2NtVnpjeWdwTENBbmVtVnliMTloWkdSeVpYTnpYMjV2ZEY5aGJHeHZkMlZrSnlrS0lDQWdJR1p5WVcxbFgyUnBaeUF0TVFvZ0lDQWdZbmwwWldOZk1TQXZMeUJoWkdSeUlFRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGWk5VaEdTMUVLSUNBZ0lDRTlDaUFnSUNCaGMzTmxjblFnTHk4Z2VtVnliMTloWkdSeVpYTnpYMjV2ZEY5aGJHeHZkMlZrQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpPRGd1WVd4bmJ5NTBjem94TndvZ0lDQWdMeThnY0hWaWJHbGpJRzkzYm1WeUlEMGdSMnh2WW1Gc1UzUmhkR1U4WVhKak5DNUJaR1J5WlhOelBpaDdJR3RsZVRvZ0oyRnlZemc0WDI4bklIMHBDaUFnSUNCcGJuUmpYekFnTHk4Z01Bb2dJQ0FnWW5sMFpXTmZNaUF2THlBaVlYSmpPRGhmYnlJS0lDQWdJR0Z3Y0Y5bmJHOWlZV3hmWjJWMFgyVjRDaUFnSUNCaGMzTmxjblFnTHk4Z1kyaGxZMnNnUjJ4dlltRnNVM1JoZEdVZ1pYaHBjM1J6Q2lBZ0lDQmllWFJsWTE4eUlDOHZJQ0poY21NNE9GOXZJZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZOalFLSUNBZ0lDOHZJSFJvYVhNdWIzZHVaWEl1ZG1Gc2RXVWdQU0J1WlhkZmIzZHVaWElLSUNBZ0lHWnlZVzFsWDJScFp5QXRNUW9nSUNBZ1lYQndYMmRzYjJKaGJGOXdkWFFLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPalkxQ2lBZ0lDQXZMeUJsYldsMEtHNWxkeUJoY21NNE9GOVBkMjVsY25Ob2FYQlVjbUZ1YzJabGNuSmxaQ2g3SUhCeVpYWnBiM1Z6WDI5M2JtVnlPaUJ3Y21WMmFXOTFjeXdnYm1WM1gyOTNibVZ5SUgwcEtRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweENpQWdJQ0JqYjI1allYUUtJQ0FnSUdKNWRHVmpJREk1SUM4dklHMWxkR2h2WkNBaVlYSmpPRGhmVDNkdVpYSnphR2x3VkhKaGJuTm1aWEp5WldRb1lXUmtjbVZ6Y3l4aFpHUnlaWE56S1NJS0lDQWdJSE4zWVhBS0lDQWdJR052Ym1OaGRBb2dJQ0FnYkc5bkNpQWdJQ0J5WlhSemRXSUtDZ292THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqT0RndVlXeG5ieTUwY3pvNlFYSmpPRGd1WVhKak9EaGZjbVZ1YjNWdVkyVmZiM2R1WlhKemFHbHdLQ2tnTFQ0Z2RtOXBaRG9LWVhKak9EaGZjbVZ1YjNWdVkyVmZiM2R1WlhKemFHbHdPZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZOekFLSUNBZ0lDOHZJSFJvYVhNdVgyVnVjM1Z5WlVSbFptRjFiSFJQZDI1bGNpZ3BDaUFnSUNCallXeHNjM1ZpSUY5bGJuTjFjbVZFWldaaGRXeDBUM2R1WlhJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU00T0M1aGJHZHZMblJ6T2pjeENpQWdJQ0F2THlCaGMzTmxjblFvYm1WM0lHRnlZelF1UVdSa2NtVnpjeWhVZUc0dWMyVnVaR1Z5S1NBOVBUMGdkR2hwY3k1dmQyNWxjaTUyWVd4MVpTd2dKMjV2ZEY5dmQyNWxjaWNwQ2lBZ0lDQjBlRzRnVTJWdVpHVnlDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak9EZ3VZV3huYnk1MGN6b3hOd29nSUNBZ0x5OGdjSFZpYkdsaklHOTNibVZ5SUQwZ1IyeHZZbUZzVTNSaGRHVThZWEpqTkM1QlpHUnlaWE56UGloN0lHdGxlVG9nSjJGeVl6ZzRYMjhuSUgwcENpQWdJQ0JwYm5Salh6QWdMeThnTUFvZ0lDQWdZbmwwWldOZk1pQXZMeUFpWVhKak9EaGZieUlLSUNBZ0lHRndjRjluYkc5aVlXeGZaMlYwWDJWNENpQWdJQ0JoYzNObGNuUWdMeThnWTJobFkyc2dSMnh2WW1Gc1UzUmhkR1VnWlhocGMzUnpDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak9EZ3VZV3huYnk1MGN6bzNNUW9nSUNBZ0x5OGdZWE56WlhKMEtHNWxkeUJoY21NMExrRmtaSEpsYzNNb1ZIaHVMbk5sYm1SbGNpa2dQVDA5SUhSb2FYTXViM2R1WlhJdWRtRnNkV1VzSUNkdWIzUmZiM2R1WlhJbktRb2dJQ0FnUFQwS0lDQWdJR0Z6YzJWeWRDQXZMeUJ1YjNSZmIzZHVaWElLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPakUzQ2lBZ0lDQXZMeUJ3ZFdKc2FXTWdiM2R1WlhJZ1BTQkhiRzlpWVd4VGRHRjBaVHhoY21NMExrRmtaSEpsYzNNK0tIc2dhMlY1T2lBbllYSmpPRGhmYnljZ2ZTa0tJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0JpZVhSbFkxOHlJQzh2SUNKaGNtTTRPRjl2SWdvZ0lDQWdZWEJ3WDJkc2IySmhiRjluWlhSZlpYZ0tJQ0FnSUdGemMyVnlkQ0F2THlCamFHVmpheUJIYkc5aVlXeFRkR0YwWlNCbGVHbHpkSE1LSUNBZ0lHSjVkR1ZqWHpJZ0x5OGdJbUZ5WXpnNFgyOGlDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak9EZ3VZV3huYnk1MGN6bzNNd29nSUNBZ0x5OGdkR2hwY3k1dmQyNWxjaTUyWVd4MVpTQTlJRzVsZHlCaGNtTTBMa0ZrWkhKbGMzTW9LUW9nSUNBZ1lubDBaV05mTVNBdkx5QmhaR1J5SUVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZaTlVoR1MxRUtJQ0FnSUdGd2NGOW5iRzlpWVd4ZmNIVjBDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak9EZ3VZV3huYnk1MGN6bzNOQW9nSUNBZ0x5OGdaVzFwZENodVpYY2dZWEpqT0RoZlQzZHVaWEp6YUdsd1VtVnViM1Z1WTJWa0tIc2djSEpsZG1sdmRYTmZiM2R1WlhJNklIQnlaWFpwYjNWeklIMHBLUW9nSUNBZ2NIVnphR0o1ZEdWeklEQjRNelEyWVdFeE5qWWdMeThnYldWMGFHOWtJQ0poY21NNE9GOVBkMjVsY25Ob2FYQlNaVzV2ZFc1alpXUW9ZV1JrY21WemN5a2lDaUFnSUNCemQyRndDaUFnSUNCamIyNWpZWFFLSUNBZ0lHeHZad29nSUNBZ2NtVjBjM1ZpQ2dvS0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZPa0Z5WXpnNExtRnlZemc0WDNSeVlXNXpabVZ5WDI5M2JtVnljMmhwY0Y5eVpYRjFaWE4wS0hCbGJtUnBibWM2SUdKNWRHVnpLU0F0UGlCMmIybGtPZ3BoY21NNE9GOTBjbUZ1YzJabGNsOXZkMjVsY25Ob2FYQmZjbVZ4ZFdWemREb0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NNE9DNWhiR2R2TG5Sek9qYzRMVGM1Q2lBZ0lDQXZMeUJBWVhKak5DNWhZbWx0WlhSb2IyUW9LUW9nSUNBZ0x5OGdjSFZpYkdsaklHRnlZemc0WDNSeVlXNXpabVZ5WDI5M2JtVnljMmhwY0Y5eVpYRjFaWE4wS0hCbGJtUnBibWM2SUdGeVl6UXVRV1JrY21WemN5azZJSFp2YVdRZ2V3b2dJQ0FnY0hKdmRHOGdNU0F3Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpPRGd1WVd4bmJ5NTBjem80TUFvZ0lDQWdMeThnZEdocGN5NWZaVzV6ZFhKbFJHVm1ZWFZzZEU5M2JtVnlLQ2tLSUNBZ0lHTmhiR3h6ZFdJZ1gyVnVjM1Z5WlVSbFptRjFiSFJQZDI1bGNnb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpnNExtRnNaMjh1ZEhNNk9ERUtJQ0FnSUM4dklHRnpjMlZ5ZENodVpYY2dZWEpqTkM1QlpHUnlaWE56S0ZSNGJpNXpaVzVrWlhJcElEMDlQU0IwYUdsekxtOTNibVZ5TG5aaGJIVmxMQ0FuYm05MFgyOTNibVZ5SnlrS0lDQWdJSFI0YmlCVFpXNWtaWElLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPakUzQ2lBZ0lDQXZMeUJ3ZFdKc2FXTWdiM2R1WlhJZ1BTQkhiRzlpWVd4VGRHRjBaVHhoY21NMExrRmtaSEpsYzNNK0tIc2dhMlY1T2lBbllYSmpPRGhmYnljZ2ZTa0tJQ0FnSUdsdWRHTmZNQ0F2THlBd0NpQWdJQ0JpZVhSbFkxOHlJQzh2SUNKaGNtTTRPRjl2SWdvZ0lDQWdZWEJ3WDJkc2IySmhiRjluWlhSZlpYZ0tJQ0FnSUdGemMyVnlkQ0F2THlCamFHVmpheUJIYkc5aVlXeFRkR0YwWlNCbGVHbHpkSE1LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPamd4Q2lBZ0lDQXZMeUJoYzNObGNuUW9ibVYzSUdGeVl6UXVRV1JrY21WemN5aFVlRzR1YzJWdVpHVnlLU0E5UFQwZ2RHaHBjeTV2ZDI1bGNpNTJZV3gxWlN3Z0oyNXZkRjl2ZDI1bGNpY3BDaUFnSUNBOVBRb2dJQ0FnWVhOelpYSjBJQzh2SUc1dmRGOXZkMjVsY2dvZ0lDQWdMeThnYzIxaGNuUmZZMjl1ZEhKaFkzUnpMM05sWTNWeWFYUjVYM1J2YTJWdUwyRnlZemc0TG1Gc1oyOHVkSE02T0RJS0lDQWdJQzh2SUdGemMyVnlkQ2h3Wlc1a2FXNW5JQ0U5UFNCdVpYY2dZWEpqTkM1QlpHUnlaWE56S0Nrc0lDZDZaWEp2WDJGa1pISmxjM05mYm05MFgyRnNiRzkzWldRbktRb2dJQ0FnWm5KaGJXVmZaR2xuSUMweENpQWdJQ0JpZVhSbFkxOHhJQzh2SUdGa1pISWdRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFWazFTRVpMVVFvZ0lDQWdJVDBLSUNBZ0lHRnpjMlZ5ZENBdkx5QjZaWEp2WDJGa1pISmxjM05mYm05MFgyRnNiRzkzWldRS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU00T0M1aGJHZHZMblJ6T2pFNENpQWdJQ0F2THlCd2RXSnNhV01nY0dWdVpHbHVaMDkzYm1WeUlEMGdSMnh2WW1Gc1UzUmhkR1U4WVhKak5DNUJaR1J5WlhOelBpaDdJR3RsZVRvZ0oyRnlZemc0WDNCdkp5QjlLU0F2THlCdmNIUnBiMjVoYkNCMGQyOHRjM1JsY0FvZ0lDQWdhVzUwWTE4d0lDOHZJREFLSUNBZ0lHSjVkR1ZqSURrZ0x5OGdJbUZ5WXpnNFgzQnZJZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZPRE1LSUNBZ0lDOHZJR2xtSUNoMGFHbHpMbkJsYm1ScGJtZFBkMjVsY2k1b1lYTldZV3gxWlNBbUppQjBhR2x6TG5CbGJtUnBibWRQZDI1bGNpNTJZV3gxWlNBaFBUMGdibVYzSUdGeVl6UXVRV1JrY21WemN5Z3BLU0I3Q2lBZ0lDQmhjSEJmWjJ4dlltRnNYMmRsZEY5bGVBb2dJQ0FnWW5WeWVTQXhDaUFnSUNCaWVpQmhjbU00T0Y5MGNtRnVjMlpsY2w5dmQyNWxjbk5vYVhCZmNtVnhkV1Z6ZEY5aFpuUmxjbDlwWmw5bGJITmxRRE1LSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPakU0Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdjR1Z1WkdsdVowOTNibVZ5SUQwZ1IyeHZZbUZzVTNSaGRHVThZWEpqTkM1QlpHUnlaWE56UGloN0lHdGxlVG9nSjJGeVl6ZzRYM0J2SnlCOUtTQXZMeUJ2Y0hScGIyNWhiQ0IwZDI4dGMzUmxjQW9nSUNBZ2FXNTBZMTh3SUM4dklEQUtJQ0FnSUdKNWRHVmpJRGtnTHk4Z0ltRnlZemc0WDNCdklnb2dJQ0FnWVhCd1gyZHNiMkpoYkY5blpYUmZaWGdLSUNBZ0lHRnpjMlZ5ZENBdkx5QmphR1ZqYXlCSGJHOWlZV3hUZEdGMFpTQmxlR2x6ZEhNS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU00T0M1aGJHZHZMblJ6T2pnekNpQWdJQ0F2THlCcFppQW9kR2hwY3k1d1pXNWthVzVuVDNkdVpYSXVhR0Z6Vm1Gc2RXVWdKaVlnZEdocGN5NXdaVzVrYVc1blQzZHVaWEl1ZG1Gc2RXVWdJVDA5SUc1bGR5QmhjbU0wTGtGa1pISmxjM01vS1NrZ2V3b2dJQ0FnWW5sMFpXTmZNU0F2THlCaFpHUnlJRUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRlpOVWhHUzFFS0lDQWdJQ0U5Q2lBZ0lDQWhDaUFnSUNCaGMzTmxjblFnTHk4Z2NHVnVaR2x1WjE5MGNtRnVjMlpsY2w5bGVHbHpkSE1LQ21GeVl6ZzRYM1J5WVc1elptVnlYMjkzYm1WeWMyaHBjRjl5WlhGMVpYTjBYMkZtZEdWeVgybG1YMlZzYzJWQU16b0tJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NNE9DNWhiR2R2TG5Sek9qRTRDaUFnSUNBdkx5QndkV0pzYVdNZ2NHVnVaR2x1WjA5M2JtVnlJRDBnUjJ4dlltRnNVM1JoZEdVOFlYSmpOQzVCWkdSeVpYTnpQaWg3SUd0bGVUb2dKMkZ5WXpnNFgzQnZKeUI5S1NBdkx5QnZjSFJwYjI1aGJDQjBkMjh0YzNSbGNBb2dJQ0FnWW5sMFpXTWdPU0F2THlBaVlYSmpPRGhmY0c4aUNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqT0RndVlXeG5ieTUwY3pvNE5nb2dJQ0FnTHk4Z2RHaHBjeTV3Wlc1a2FXNW5UM2R1WlhJdWRtRnNkV1VnUFNCd1pXNWthVzVuQ2lBZ0lDQm1jbUZ0WlY5a2FXY2dMVEVLSUNBZ0lHRndjRjluYkc5aVlXeGZjSFYwQ2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpPRGd1WVd4bmJ5NTBjem94TndvZ0lDQWdMeThnY0hWaWJHbGpJRzkzYm1WeUlEMGdSMnh2WW1Gc1UzUmhkR1U4WVhKak5DNUJaR1J5WlhOelBpaDdJR3RsZVRvZ0oyRnlZemc0WDI4bklIMHBDaUFnSUNCcGJuUmpYekFnTHk4Z01Bb2dJQ0FnWW5sMFpXTmZNaUF2THlBaVlYSmpPRGhmYnlJS0lDQWdJR0Z3Y0Y5bmJHOWlZV3hmWjJWMFgyVjRDaUFnSUNCaGMzTmxjblFnTHk4Z1kyaGxZMnNnUjJ4dlltRnNVM1JoZEdVZ1pYaHBjM1J6Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpPRGd1WVd4bmJ5NTBjem80TndvZ0lDQWdMeThnWlcxcGRDaHVaWGNnWVhKak9EaGZUM2R1WlhKemFHbHdWSEpoYm5ObVpYSlNaWEYxWlhOMFpXUW9leUJ3Y21WMmFXOTFjMTl2ZDI1bGNqb2dkR2hwY3k1dmQyNWxjaTUyWVd4MVpTd2djR1Z1WkdsdVoxOXZkMjVsY2pvZ2NHVnVaR2x1WnlCOUtTa0tJQ0FnSUdaeVlXMWxYMlJwWnlBdE1Rb2dJQ0FnWTI5dVkyRjBDaUFnSUNCd2RYTm9ZbmwwWlhNZ01IZ3hObUptTVdZNU1TQXZMeUJ0WlhSb2IyUWdJbUZ5WXpnNFgwOTNibVZ5YzJocGNGUnlZVzV6Wm1WeVVtVnhkV1Z6ZEdWa0tHRmtaSEpsYzNNc1lXUmtjbVZ6Y3lraUNpQWdJQ0J6ZDJGd0NpQWdJQ0JqYjI1allYUUtJQ0FnSUd4dlp3b2dJQ0FnY21WMGMzVmlDZ29LTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpnNExtRnNaMjh1ZEhNNk9rRnlZemc0TG1GeVl6ZzRYMkZqWTJWd2RGOXZkMjVsY25Ob2FYQW9LU0F0UGlCMmIybGtPZ3BoY21NNE9GOWhZMk5sY0hSZmIzZHVaWEp6YUdsd09nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpnNExtRnNaMjh1ZEhNNk9USUtJQ0FnSUM4dklIUm9hWE11WDJWdWMzVnlaVVJsWm1GMWJIUlBkMjVsY2lncENpQWdJQ0JqWVd4c2MzVmlJRjlsYm5OMWNtVkVaV1poZFd4MFQzZHVaWElLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPakU0Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdjR1Z1WkdsdVowOTNibVZ5SUQwZ1IyeHZZbUZzVTNSaGRHVThZWEpqTkM1QlpHUnlaWE56UGloN0lHdGxlVG9nSjJGeVl6ZzRYM0J2SnlCOUtTQXZMeUJ2Y0hScGIyNWhiQ0IwZDI4dGMzUmxjQW9nSUNBZ2FXNTBZMTh3SUM4dklEQUtJQ0FnSUdKNWRHVmpJRGtnTHk4Z0ltRnlZemc0WDNCdklnb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpnNExtRnNaMjh1ZEhNNk9UTUtJQ0FnSUM4dklHRnpjMlZ5ZENoMGFHbHpMbkJsYm1ScGJtZFBkMjVsY2k1b1lYTldZV3gxWlN3Z0oyNXZkRjl3Wlc1a2FXNW5YMjkzYm1WeUp5a0tJQ0FnSUdGd2NGOW5iRzlpWVd4ZloyVjBYMlY0Q2lBZ0lDQmlkWEo1SURFS0lDQWdJR0Z6YzJWeWRDQXZMeUJ1YjNSZmNHVnVaR2x1WjE5dmQyNWxjZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZPVFFLSUNBZ0lDOHZJR052Ym5OMElITmxibVJsY2lBOUlHNWxkeUJoY21NMExrRmtaSEpsYzNNb1ZIaHVMbk5sYm1SbGNpa0tJQ0FnSUhSNGJpQlRaVzVrWlhJS0lDQWdJQzh2SUhOdFlYSjBYMk52Ym5SeVlXTjBjeTl6WldOMWNtbDBlVjkwYjJ0bGJpOWhjbU00T0M1aGJHZHZMblJ6T2pFNENpQWdJQ0F2THlCd2RXSnNhV01nY0dWdVpHbHVaMDkzYm1WeUlEMGdSMnh2WW1Gc1UzUmhkR1U4WVhKak5DNUJaR1J5WlhOelBpaDdJR3RsZVRvZ0oyRnlZemc0WDNCdkp5QjlLU0F2THlCdmNIUnBiMjVoYkNCMGQyOHRjM1JsY0FvZ0lDQWdhVzUwWTE4d0lDOHZJREFLSUNBZ0lHSjVkR1ZqSURrZ0x5OGdJbUZ5WXpnNFgzQnZJZ29nSUNBZ1lYQndYMmRzYjJKaGJGOW5aWFJmWlhnS0lDQWdJR0Z6YzJWeWRDQXZMeUJqYUdWamF5QkhiRzlpWVd4VGRHRjBaU0JsZUdsemRITUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NNE9DNWhiR2R2TG5Sek9qazFDaUFnSUNBdkx5QmhjM05sY25Rb2MyVnVaR1Z5SUQwOVBTQjBhR2x6TG5CbGJtUnBibWRQZDI1bGNpNTJZV3gxWlN3Z0oyNXZkRjl3Wlc1a2FXNW5YMjkzYm1WeUp5a0tJQ0FnSUdScFp5QXhDaUFnSUNBOVBRb2dJQ0FnWVhOelpYSjBJQzh2SUc1dmRGOXdaVzVrYVc1blgyOTNibVZ5Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpPRGd1WVd4bmJ5NTBjem94TndvZ0lDQWdMeThnY0hWaWJHbGpJRzkzYm1WeUlEMGdSMnh2WW1Gc1UzUmhkR1U4WVhKak5DNUJaR1J5WlhOelBpaDdJR3RsZVRvZ0oyRnlZemc0WDI4bklIMHBDaUFnSUNCcGJuUmpYekFnTHk4Z01Bb2dJQ0FnWW5sMFpXTmZNaUF2THlBaVlYSmpPRGhmYnlJS0lDQWdJR0Z3Y0Y5bmJHOWlZV3hmWjJWMFgyVjRDaUFnSUNCaGMzTmxjblFnTHk4Z1kyaGxZMnNnUjJ4dlltRnNVM1JoZEdVZ1pYaHBjM1J6Q2lBZ0lDQmllWFJsWTE4eUlDOHZJQ0poY21NNE9GOXZJZ29nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZPVGNLSUNBZ0lDOHZJSFJvYVhNdWIzZHVaWEl1ZG1Gc2RXVWdQU0J6Wlc1a1pYSUtJQ0FnSUdScFp5QXlDaUFnSUNCaGNIQmZaMnh2WW1Gc1gzQjFkQW9nSUNBZ0x5OGdjMjFoY25SZlkyOXVkSEpoWTNSekwzTmxZM1Z5YVhSNVgzUnZhMlZ1TDJGeVl6ZzRMbUZzWjI4dWRITTZNVGdLSUNBZ0lDOHZJSEIxWW14cFl5QndaVzVrYVc1blQzZHVaWElnUFNCSGJHOWlZV3hUZEdGMFpUeGhjbU0wTGtGa1pISmxjM00rS0hzZ2EyVjVPaUFuWVhKak9EaGZjRzhuSUgwcElDOHZJRzl3ZEdsdmJtRnNJSFIzYnkxemRHVndDaUFnSUNCaWVYUmxZeUE1SUM4dklDSmhjbU00T0Y5d2J5SUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NNE9DNWhiR2R2TG5Sek9qazRDaUFnSUNBdkx5QjBhR2x6TG5CbGJtUnBibWRQZDI1bGNpNTJZV3gxWlNBOUlHNWxkeUJoY21NMExrRmtaSEpsYzNNb0tRb2dJQ0FnWW5sMFpXTmZNU0F2THlCaFpHUnlJRUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRlpOVWhHUzFFS0lDQWdJR0Z3Y0Y5bmJHOWlZV3hmY0hWMENpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqT0RndVlXeG5ieTUwY3pvNU9Rb2dJQ0FnTHk4Z1pXMXBkQ2h1WlhjZ1lYSmpPRGhmVDNkdVpYSnphR2x3VkhKaGJuTm1aWEpCWTJObGNIUmxaQ2g3SUhCeVpYWnBiM1Z6WDI5M2JtVnlPaUJ3Y21WMmFXOTFjeXdnYm1WM1gyOTNibVZ5T2lCelpXNWtaWElnZlNrcENpQWdJQ0J6ZDJGd0NpQWdJQ0JqYjI1allYUUtJQ0FnSUhCMWMyaGllWFJsY3lBd2VHWTNaVE0yWWpNM0lDOHZJRzFsZEdodlpDQWlZWEpqT0RoZlQzZHVaWEp6YUdsd1ZISmhibk5tWlhKQlkyTmxjSFJsWkNoaFpHUnlaWE56TEdGa1pISmxjM01wSWdvZ0lDQWdaR2xuSURFS0lDQWdJR052Ym1OaGRBb2dJQ0FnYkc5bkNpQWdJQ0F2THlCemJXRnlkRjlqYjI1MGNtRmpkSE12YzJWamRYSnBkSGxmZEc5clpXNHZZWEpqT0RndVlXeG5ieTUwY3pveE1EQUtJQ0FnSUM4dklHVnRhWFFvYm1WM0lHRnlZemc0WDA5M2JtVnljMmhwY0ZSeVlXNXpabVZ5Y21Wa0tIc2djSEpsZG1sdmRYTmZiM2R1WlhJNklIQnlaWFpwYjNWekxDQnVaWGRmYjNkdVpYSTZJSE5sYm1SbGNpQjlLU2tLSUNBZ0lHSjVkR1ZqSURJNUlDOHZJRzFsZEdodlpDQWlZWEpqT0RoZlQzZHVaWEp6YUdsd1ZISmhibk5tWlhKeVpXUW9ZV1JrY21WemN5eGhaR1J5WlhOektTSUtJQ0FnSUhOM1lYQUtJQ0FnSUdOdmJtTmhkQW9nSUNBZ2JHOW5DaUFnSUNCeVpYUnpkV0lLQ2dvdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak9EZ3VZV3huYnk1MGN6bzZRWEpqT0RndVlYSmpPRGhmWTJGdVkyVnNYMjkzYm1WeWMyaHBjRjl5WlhGMVpYTjBLQ2tnTFQ0Z2RtOXBaRG9LWVhKak9EaGZZMkZ1WTJWc1gyOTNibVZ5YzJocGNGOXlaWEYxWlhOME9nb2dJQ0FnTHk4Z2MyMWhjblJmWTI5dWRISmhZM1J6TDNObFkzVnlhWFI1WDNSdmEyVnVMMkZ5WXpnNExtRnNaMjh1ZEhNNk1UQTFDaUFnSUNBdkx5QjBhR2x6TGw5bGJuTjFjbVZFWldaaGRXeDBUM2R1WlhJb0tRb2dJQ0FnWTJGc2JITjFZaUJmWlc1emRYSmxSR1ZtWVhWc2RFOTNibVZ5Q2lBZ0lDQXZMeUJ6YldGeWRGOWpiMjUwY21GamRITXZjMlZqZFhKcGRIbGZkRzlyWlc0dllYSmpPRGd1WVd4bmJ5NTBjem94TURZS0lDQWdJQzh2SUdGemMyVnlkQ2h1WlhjZ1lYSmpOQzVCWkdSeVpYTnpLRlI0Ymk1elpXNWtaWElwSUQwOVBTQjBhR2x6TG05M2JtVnlMblpoYkhWbExDQW5ibTkwWDI5M2JtVnlKeWtLSUNBZ0lIUjRiaUJUWlc1a1pYSUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NNE9DNWhiR2R2TG5Sek9qRTNDaUFnSUNBdkx5QndkV0pzYVdNZ2IzZHVaWElnUFNCSGJHOWlZV3hUZEdGMFpUeGhjbU0wTGtGa1pISmxjM00rS0hzZ2EyVjVPaUFuWVhKak9EaGZieWNnZlNrS0lDQWdJR2x1ZEdOZk1DQXZMeUF3Q2lBZ0lDQmllWFJsWTE4eUlDOHZJQ0poY21NNE9GOXZJZ29nSUNBZ1lYQndYMmRzYjJKaGJGOW5aWFJmWlhnS0lDQWdJR0Z6YzJWeWRDQXZMeUJqYUdWamF5QkhiRzlpWVd4VGRHRjBaU0JsZUdsemRITUtJQ0FnSUM4dklITnRZWEowWDJOdmJuUnlZV04wY3k5elpXTjFjbWwwZVY5MGIydGxiaTloY21NNE9DNWhiR2R2TG5Sek9qRXdOZ29nSUNBZ0x5OGdZWE56WlhKMEtHNWxkeUJoY21NMExrRmtaSEpsYzNNb1ZIaHVMbk5sYm1SbGNpa2dQVDA5SUhSb2FYTXViM2R1WlhJdWRtRnNkV1VzSUNkdWIzUmZiM2R1WlhJbktRb2dJQ0FnUFQwS0lDQWdJR0Z6YzJWeWRDQXZMeUJ1YjNSZmIzZHVaWElLSUNBZ0lDOHZJSE50WVhKMFgyTnZiblJ5WVdOMGN5OXpaV04xY21sMGVWOTBiMnRsYmk5aGNtTTRPQzVoYkdkdkxuUnpPakU0Q2lBZ0lDQXZMeUJ3ZFdKc2FXTWdjR1Z1WkdsdVowOTNibVZ5SUQwZ1IyeHZZbUZzVTNSaGRHVThZWEpqTkM1QlpHUnlaWE56UGloN0lHdGxlVG9nSjJGeVl6ZzRYM0J2SnlCOUtTQXZMeUJ2Y0hScGIyNWhiQ0IwZDI4dGMzUmxjQW9nSUNBZ1lubDBaV01nT1NBdkx5QWlZWEpqT0RoZmNHOGlDaUFnSUNBdkx5QnpiV0Z5ZEY5amIyNTBjbUZqZEhNdmMyVmpkWEpwZEhsZmRHOXJaVzR2WVhKak9EZ3VZV3huYnk1MGN6b3hNRGNLSUNBZ0lDOHZJSFJvYVhNdWNHVnVaR2x1WjA5M2JtVnlMblpoYkhWbElEMGdibVYzSUdGeVl6UXVRV1JrY21WemN5Z3BDaUFnSUNCaWVYUmxZMTh4SUM4dklHRmtaSElnUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVUZCUVVGQlFVRkJRVmsxU0VaTFVRb2dJQ0FnWVhCd1gyZHNiMkpoYkY5d2RYUUtJQ0FnSUhKbGRITjFZZ289IiwiY2xlYXIiOiJJM0J5WVdkdFlTQjJaWEp6YVc5dUlERXdDaU53Y21GbmJXRWdkSGx3WlhSeVlXTnJJR1poYkhObENnb3ZMeUJBWVd4bmIzSmhibVJtYjNWdVpHRjBhVzl1TDJGc1oyOXlZVzVrTFhSNWNHVnpZM0pwY0hRdlltRnpaUzFqYjI1MGNtRmpkQzVrTG5Sek9qcENZWE5sUTI5dWRISmhZM1F1WTJ4bFlYSlRkR0YwWlZCeWIyZHlZVzBvS1NBdFBpQjFhVzUwTmpRNkNtMWhhVzQ2Q2lBZ0lDQndkWE5vYVc1MElERWdMeThnTVFvZ0lDQWdjbVYwZFhKdUNnPT0ifSwiYnl0ZUNvZGUiOnsiYXBwcm92YWwiOiJDaUFFQUFFZ1VTWWVCQlVmZkhVZ0FBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFIWVhKak9EaGZid0YwQVdJR1kzUnliR1Z1QWdBQ0FZQUJjQWhoY21NNE9GOXdid1JqZEhKc0FRQUlZWEpqT0RoZmIya0RiM0JoQkcxallXa0RhWE56Qkdod1gyRUNiM0FGY21wMWMzUUViR05oY2dnQUFBQUFBQUFBQUFJQVFnRUJBZ0FCQWdCaUJOZjhTcGdDQUFBRVhDZTAvQVI1ZzhOY0JFTlYwcTB4RzBFRTFZSXRCQVJVY3RBRWZYa0VwQVRtOVBoaEJDNjlMVFFFN204dERnUWRYSG9YQk9WNmJoZ0VaYkZvS2dRQk1GbWJCQlFyWDhzRStJT091UVF4aUN2NkJLbk1vVzhFSm1XWHdBUTErQk5mQk5wd0pia0VQeVZuRXdTVnRQbmpCSURNU2FzRUI1WWhaUVRuaVdIYUJQMlVnTmNFc2JIV21nVEJ2dGVKQkR2K0dETUVXWnpScFFSdDZVRm1CQ2p3STljRWwxT0M0Z1JsZlJQc0JMYXVHaVVFaE93VDFRVHNtV0JCQklMbGM4UUVTcGFQandTMVFpRWxCTHV6R2ZNRUJ3SmxUZ1RRRlhKT0JBS2Y3TUFFYzBrelRnVGJmSUx2QlAwc0xHNEVRcVh3WlFTdFQyanFOaG9BamkwRGhBTjFBMllEVndOSEF5Z0REd01BQXVzQzFnTEVBcXNDandKL0Fta0NVd0kzQWlFQ0NBSHpBZDRCdndHZ0FZZ0Jid0ZYQVVJQktnRU9BUDRBN2dEZUFNNEF1d0NpQUl3QWRnQm1BRk1BUkFBMUFDa0FHZ0FPQUFJaVF6RVpGRVF4R0VTSUVLOGpRekVaRkVReEdFU0lFSEFqUXpFWkZFUXhHRVEyR2dHSUVDWWpRekVaRkVReEdFU0lELzRqUXpFWkZFUXhHRVEyR2dHSUQ4c2pRekVaRkVReEdFUTJHZ0dJRDQ4alF6RVpGRVF4R0VRMkdnR0lEMVVvVEZDd0kwTXhHUlJFTVJoRWlBODlLRXhRc0NORE1Sa1VSREVZUkRZYUFUWWFBb2dPS3loTVVMQWpRekVaRkVReEdFUTJHZ0UyR2dLSURnZ29URkN3STBNeEdSUkVNUmhFTmhvQk5ob0NOaG9EaUEzQktFeFFzQ05ETVJrVVJERVlSRFlhQVlnTnBTaE1VTEFqUXpFWkZFUXhHRVNJRFpBb1RGQ3dJME14R1JSRU1SaEVpQTE1S0V4UXNDTkRNUmtVUkRFWVJJZ05XU2hNVUxBalF6RVpGRVF4R0VTSURUb29URkN3STBNeEdSUkVNUmhFTmhvQk5ob0NOaG9ETmhvRWlBeklLRXhRc0NORE1Sa1VSREVZUkRZYUFUWWFBallhQXpZYUJJZ0wzQ05ETVJrVVJERVlSRFlhQVRZYUFqWWFBNGdMUHlORE1Sa1VSREVZUkRZYUFUWWFBallhQXpZYUJJZ0treU5ETVJrVVJERVlSRFlhQVRZYUFqWWFBNGdLUmloTVVMQWpRekVaRkVReEdFUTJHZ0UyR2dJMkdnTTJHZ1NJQ2hFalF6RVpGRVF4R0VRMkdnRTJHZ0kyR2dNMkdnUTJHZ1dJQnNVb1RGQ3dJME14R1JSRU1SaEVOaG9CTmhvQ05ob0ROaG9FTmhvRmlBWkFLRXhRc0NORE1Sa1VSREVZUkRZYUFUWWFBallhQTRnR0JpTkRNUmtVUkRFWVJEWWFBVFlhQWpZYUE0Z0YyQ05ETVJrVVJERVlSRFlhQVRZYUFqWWFBNGdGWkNoTVVMQWpRekVaRkVReEdFUTJHZ0UyR2dLSUJURW9URkN3STBNeEdSUkVNUmhFTmhvQk5ob0NOaG9ETmhvRWlBVDZLRXhRc0NORE1Sa1VSREVZUkRZYUFUWWFBb2dFeWloTVVMQWpRekVaRkVReEdFUTJHZ0UyR2dLSUJLVW9URkN3STBNeEdSUkVNUmhFaUFTUEtFeFFzQ05ETVJrVVJERVlSRFlhQVRZYUFqWWFBellhQklnRVppaE1VTEFqUXpFWkZFUXhHRVEyR2dFMkdnSTJHZ09JQkVJb1RGQ3dJME14R1JSRU1SaEVOaG9CTmhvQ2lBUEpJME14R1JSRU1SaEVOaG9CTmhvQ05ob0RpQU0xSTBNeEdSUkVNUmhFTmhvQk5ob0NOaG9EaUFMV0kwTXhHUlJFTVJoRU5ob0JpQUs3STBNeEdSUkVNUmhFTmhvQk5ob0NOaG9EaUFJNktFeFFzQ05ETVJrVVJERVlSRFlhQVRZYUFqWWFBellhQkRZYUJZZ0JuU2hNVUxBalF6RVpGRVF4R0VTSUFXRW9URkN3STBNeEdSUkVNUmhFTmhvQmlBRkNJME14R1JSRU1SaEVOaG9CaUFFbkkwTXhHUlJFTVJoRU5ob0JpQURxSTBNeEdSUkVNUmhFTmhvQmlBQ2NJME14R1VEOGFERVlGRVFqUXpFQWlBd1NJbE1qRWtTSklpY0taVVVCUkRFQUlpY0taVVFTUkNJbkJXVkZBVUVBRHlJbkJXVkVJbE1qRWtFQUF5TkVpU0pDLy9xS0FRQWlKeEpsUlFGQkFCTWlKeEpsUkNKVEl4SkJBQWVMLzFjQ0FCVkVpU0luRG1WRkFVRUFMaUluRG1WRUYwRUFKU0luRTJWRkFVRUFGU0luRTJWRUZ5SW5EbVZFRnpJR0ZoZE9BZ2dQUkRJR0ZpY1RUR2VKaWdFQUlvai9haUluQ21WRkFVRUFLU0luQ21WRWpBQW5Db3YvWnlJbkJXVkZBVUFBQlNjRkp3ZG5pd0NMLzFDQUJFQ2N4WEJNVUxDSktZd0FRdi9ZaWdFQWlQOHNpLzhpVTBBQUJpY0ZpLzluaVNJbkJXVkZBVUVBRENJbkJXVkVJbE1qRWtILzZpY0ZpLzluaVlvQkFJaisvaWNTaS85bmlZb0JBSWorOGljT2kvOW5pU0luQldWRkFVRUFJQ0luQldWRUlsTWpFa0VBRkNJbkNtVkZBVUVBQzRBSUFBQUFBQUFBQUFHSkp4U0ppZ1VCaVA3RmkvK0kvdXlJL3dtTCs0djhFMFNMKzRnSnhVbUwvYWRFaS8yaFNSVWtEa1FrcjB4TEFhc25CSXY3VUV5L2kveUlDYWFML2FCSkZTUU9SS3NuQkl2OFVFeS9NUUNMKzFDTC9GQ0wvVkNBQVZGUWdBSUFoVkNML2hXQmhRRUlGbGNHQWxDTC9sQ0wvMUFuQmt4UWdBUTBicWVWVEZDd0pSYUppZ01CaVA1SGkvK0kvbTZJL291TC9ZZ0pUVW1ML3FkRWkvNmhTUlVrRGtRa3IweExBYXNuQkl2OVVFeS9JaXRsUkl2K29Va1ZKQTVFcXl0TVp6RUFpLzFRaS81UWdBRlJVSUFDQUdOUWkvOVFKd1pNVUlBRURlNFU5VXhRc0NVV2lURUFpQW56SWxNakVrU0ppZ0VBaVAvdkp3K0wvMmVKaWdNQWlQL2ppLzZBQUtWRUlpY1BaVVVCUVFBeElpY1BaVVFpVXlNU1FRQWxJMFNML1NtTC9vdi9pQVgvaS8yTC9sQW5GVkNMLzFBbkJreFFnQVR5NlppdlRGQ3dpU0pDLzlpS0F3QWlNUUJKaS8wU1FBQU1pd0dJQ1lVaVV5TVNRUUJpSTBTTC9vQUFwVVFuQkl2OVVFbU1BTDFGQVVFQVNJc0F2a1NML3FkQkFENGpSSXNBU2I1RWkvNmhTUlVrRGtRa3IweExBYXRQQWt5L0lpdGxSSXYrb1VrVkpBNUVxeXRNWjR2OWkvNVFKeFZRaS85UUp3Wk1VQ2NaVEZDd2lTSkMvNzhpUXYrYmlnSUFNUUJKaS82QUFLVkVKd1JNVUVtOVJRRkJBRWlMQWI1RWkvNm5RUUErSTBTTEFVbStSSXYrb1VrVkpBNUVKSzlNU3dHclR3Sk12eUlyWlVTTC9xRkpGU1FPUktzclRHZUxBSXYrVUNjVlVJdi9VQ2NHVEZBbkdVeFFzSWtpUXYrL2lnTUJpLzJML29nQUk0bUtCQUdML0l2OWkvNklCMStKSWljUFpVU0ppZ0lCaS82TC8xQW5DRXhRdmtTSmlnSUJNUUFwaS80cGkvOG5Hb2dENkRFQWkvNkwvNGdIam9tS0JBRXhBSXY5aS95SUFyaE1pL3lML1VzRGkvNkwvNGdEeEltS0FnR0wvb3YvVUNjUVRGQkp2VVVCUUFBRUp4cE1pWXNBdmtSTWlZb0RBU0pIQW92K2kvMFNRUUFGSndlTUFJbUwvWXYrVUVtTUFJdi9VQ2NSVEZCSmpBRzlSUUZCQUErTEFiNUVGeU1TUVFBRkp3ZU1BSW1MQUNsUUp4Rk1VRW1NQXIxRkFVRUFENHNDdmtRWEl4SkJBQVVuQjR3QWlTY0xqQUNKaWdNQU1RQ0wvUkpFaS8yTC9sQ0wvMUFuRVV4UUp4YS9pWW9EQURFQWkvMFNSSXY5aS81UWkvOVFKeEZNVUVtOVJRRkJBQVNMQUx4SWlZb0ZBU0l4QUl2N01RQ0wvSWovV0NKVEl4SkhBa0FBTW92N2l3RlFpL3hRSncxTVVFbU1BTDFGQVVFQUdZc0FTYjVFU1l2K3AwUWpqQUtML3FGSkZTUU9SQ1N2cTcrTEFvd0Rpd05FaS8yTC9JZ0JuNHY3aS95TC9Vc0RpLzZMLzRnQ3Fvd0FpWW9GQVNKSmdBQkppL3VML0ZBbkNFeFFTYjFGQVVBQVBvQTVVQUFqQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFGRkJoY25ScGRHbHZiaUJ1YjNRZ1pYaHBjM1J6akFDSml3UytSSXYrcEVFQVBvQTVVZ0FqQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFGRWx1YzNWbVptbGphV1Z1ZENCaVlXeGhibU5sakFDSmkvMHBFa0VBT29BMVZ3QWpBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUVFbHVkbUZzYVdRZ2NtVmpaV2wyWlhLTUFJa3hBRW1NQUl2N0UwRUFob3Y3aXdDTC9JaitEaUpUSXhKSmpBSkpqQU5BQUNxTCs0c0FVSXY4VUNjTlRGQkpqQUc5UlFHTEFvd0RRUUFSaXdHK1JJditwMEVBQXlPTUFvc0NqQU9MQTBBQVFZQThXQUFqQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFGMDl3WlhKaGRHOXlJRzV2ZENCaGRYUm9iM0pwZW1Wa2pBQ0ppLzJML0lnQUZvQURVUUFqVEZDQUNRQUhVM1ZqWTJWemMxQ01BSW1LQWdFcGkvNkwvMUFuQ0V4UXZVVUJRUUFFaS8rTUFJc0FUSW1LQWdBaVJ3U0FBRWNFZ0FSb2NGOXdpLzVRU2IxRkFVQUFCWXNLSnhTL2l3cStUSXdDUkNLTUJpY1VqQUdMQVJkSmpBaUxBaGRKakFrTVFRQlBpLzZMQVZBbkVFeFFTWXdFdlVVQlFBQU1KeGVMLzFDTEJFbThTRXkvaXdTK1RFbE9Bb3dBUkNKWmpBVWlqQWVMQjRzRkRFRUFoNHNBVndJQWl3Y2tDeVJZaS84U1FRQnRJMEVBWHlPTUJvc0dRQUE0aS82TEFsQW5FRXhRU1l3RHZrUWlXUllYZ1FvTVFRQWhpd05KdmtSWEFnQW5GNHYvVUZjQ0FGQkpGU1FLRmxjR0FreFFTd0c4U0wrSml3a2pDQmFMQ2tzQnY0ditURkFuRjR2L1VDY1FUd0pRU2J4SVRMK0ppd2dqQ0JhTUFVTC9PNHNISXdpTUIwTC9jU0pDLzRlS0JnQWlTWXYrZ0FDbFJJdjZpL3RRSndoTVVFbTlSUUZBQUFTTEFpbS9pd0pKdmtTTC9xRkpGU1FPUkNTdlNZd0FxNytMK292OFVJdjdVSXYrVUlBQ0FJSlFpLzlRSndaTVVJQUVJR3Q1UUV4UXNJdjlpL3NUUVFBSGkveUwvWWorbjR2OGkvMVFKd2hNVUVtTUFiMUZBVUFBQklzQktiK0xBVW0rUkl2K29Fa1ZKQTVFaXdDcnY0bUtCQUF4QUl2OEVrU0wvSXY5VUl2K1VDY05URkNMLzcrSmlnTUJJb3YraS8wU1FRQUVKd2RNaVl2OWkvNVFpLzlRSncxTVVFbU1BTDFGQVVBQUJDY0xUSW1MQUw1RWdBQ2xKd3NpVHdKVVRJbUtCQUFpU1RFQWlBT3JJbE1qRWtTTC9vQUFwVVNML0l2OVVFa25DRXhRU2IxRkFVQUFDNHNES2IrTC9JdjlpUDMxaXdOSnZrU0wvcUJKRlNRT1JDU3ZTWXdBcTc4bkJJdjhVRW1NQWIxRkFVQUFCSXNCS2IrTEFVbStSSXYrb0VrVkpBNUVpd0JKVGdPcnZ5SXJaVVNML3FCSkZTUU9SS3NyVEdlTEFvditVQ2NZVUl2L1VDY0dURkNBQlBwRU94dE1VTENKaWdNQU1RQ0wvb0FBcFVSSmkvMVFTVTRDSndoTVVFbTlSUUZFU2I1RWkvNm5SRW0rUkl2K29Va1ZKQTVFSks5SlRnU3J2eWNFVEZCSnZVVUJRUUJGaXdLK1JJditwMEVBT3lORWl3Skp2a1NML3FGSkZTUU9SSXNCU1U0RHE3OGlLMlZFaS82aFNSVWtEa1NySzB4bml3Q0wvbEFuR0ZDTC8xQW5Ca3hRSnh0TVVMQ0pJa0wvd29vRUFDSkhBekVBaS93eEFJdjlpUHBTSWxNakVrY0NRQUF5aS95TEJGQ0wvVkFuRFV4UVNZd0R2VVVCUVFBWml3Tkp2a1JKaS82blJDT01CWXYrb1VrVkpBNUVKSytydjRzRmpBYUxCa1NML0l2OVVFbU1BU2NJVEZCSnZVVUJSRW0rUkl2K3AwUkp2a1NML3FGSkZTUU9SQ1N2U1l3QXE3OG5CSXY4VUVtTUFyMUZBVUVBUllzQ3ZrU0wvcWRCQURzalJJc0NTYjVFaS82aFNSVWtEa1NMQUVsT0E2dS9JaXRsUkl2K29Va1ZKQTVFcXl0TVo0c0JpLzVRSnhoUWkvOVFKd1pNVUNjYlRGQ3dpU0pDLzhLS0JBRXhBRElKRWtTTC9GY0NBQlZKUkNRT1JJdjlWd0lBRlVsRWdRZ09SQ0lyWlVVQkZFU0FBVzZML0dlQUFYT0wvV2NyaS85bmdBRmtpLzVuTVFBbkJFc0JVSXYvdnpJRFRGQ0wvMUFuSEV4UXNDY0hpU0tBQVc1bFJGY0NBRWtWSkJKRWlTS0FBWE5sUkZjQ0FFa1ZnUWdTUklraWdBRmtaVVNKSWl0bFJJbUtBUUdMLzRnQVI0bUtBd0V4QUl2OVN3R0lBTGRKaS8rblJJdi9vVWtWSkE1RUpLK3JpLzFPQW9nQXdraUwvWXYraS8rSUFER0ppZ0lCTVFDTC9vdi9pQUNyaVlvQ0FZditpLytJQUg2SmlnRUJKd1NMLzFCSnZVVUJRQUFES1V5Sml3QytSRXlKaWdNQmkvMkkvK0JKaS82SS85cE1pLytuUkl2OWkvNFRRUUFwaXdDTC82RkpGU1FPUkNTdlRFc0JxeWNFaS8xUVRMK0xBWXYvb0VrVkpBNUVxeWNFaS81UVRMK0wvWXYrVUl2L1VDY2NURkN3SndlTUFJbUtBZ0dML292L1VBRkpGU1FTUkltS0FnR0wvb3YvaVAvbmdBRmhURkJKdlVVQlFBQURLVXlKaXdDK1JGY0FJRXlKaWdNQmkvMkwvb2oveFl2L2kvMVFpLzVRZ0FGaFR3SlFUTCtML1l2K1VJdi9VSUFFR1duNFpVeFFzQ2NIaVNJbkRHVkZBVUVBQ1NJbkRHVkVGMEFBRVNJcVpVVUJRQUFFS2pJSlp5Y01KeFpuaVlqLzJTSXFaVVNKaWdFQmlQL09JaXBsUlFGQUFBTW5DNGtpS21WRUtSSkJBQU1uQzRraUttVkVpLzhTSndzaVR3SlVpWW9CQUNJbkRHVkZBVUVBSFNJbkRHVkVGeU1TUVFBU0l4UkVpLzhwRTBRcWkvOW5Kd3duRm1lSklrTC82NG9CQUlqL2RqRUFJaXBsUkJKRWkvOHBFMFFpS21WRUtvdi9aNHYvVUNjZFRGQ3dpWWovVlRFQUlpcGxSQkpFSWlwbFJDb3BaNEFFTkdxaFpreFFzSW1LQVFDSS96WXhBQ0lxWlVRU1JJdi9LUk5FSWljSlpVVUJRUUFKSWljSlpVUXBFeFJFSndtTC8yY2lLbVZFaS85UWdBUVd2eCtSVEZDd2lZaisvaUluQ1dWRkFVUXhBQ0luQ1dWRVN3RVNSQ0lxWlVRcVN3Sm5Kd2twWjB4UWdBVDM0MnMzU3dGUXNDY2RURkN3aVlqK3l6RUFJaXBsUkJKRUp3a3BaNGs9IiwiY2xlYXIiOiJDb0VCUXc9PSJ9LCJjb21waWxlckluZm8iOnsiY29tcGlsZXIiOiJwdXlhIiwiY29tcGlsZXJWZXJzaW9uIjp7Im1ham9yIjo0LCJtaW5vciI6NywicGF0Y2giOjAsImNvbW1pdEhhc2giOm51bGx9fSwiZXZlbnRzIjpbeyJuYW1lIjoiQ29udHJvbGxlckNoYW5nZWQiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiKGFkZHJlc3MsYWRkcmVzcykiLCJzdHJ1Y3QiOiJhcmMxNjQ0X2NvbnRyb2xsZXJfY2hhbmdlZF9ldmVudCIsIm5hbWUiOiIwIiwiZGVzYyI6bnVsbH1dfSx7Im5hbWUiOiJDb250cm9sbGVyVHJhbnNmZXIiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiKGFkZHJlc3MsYWRkcmVzcyxhZGRyZXNzLHVpbnQyNTYsYnl0ZSxieXRlW10sYnl0ZVtdKSIsInN0cnVjdCI6ImFyYzE2NDRfY29udHJvbGxlcl90cmFuc2Zlcl9ldmVudCIsIm5hbWUiOiIwIiwiZGVzYyI6bnVsbH1dfSx7Im5hbWUiOiJDb250cm9sbGVyUmVkZWVtIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6IihhZGRyZXNzLGFkZHJlc3MsdWludDI1NixieXRlLGJ5dGVbXSkiLCJzdHJ1Y3QiOiJhcmMxNjQ0X2NvbnRyb2xsZXJfcmVkZWVtX2V2ZW50IiwibmFtZSI6IjAiLCJkZXNjIjpudWxsfV19LHsibmFtZSI6Iklzc3VlIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6IihhZGRyZXNzLGFkZHJlc3MsdWludDI1NixieXRlW10pIiwic3RydWN0IjoiYXJjMTQxMF9wYXJ0aXRpb25faXNzdWUiLCJuYW1lIjoiMCIsImRlc2MiOm51bGx9XX0seyJuYW1lIjoiSXNzdWUiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiKGFkZHJlc3MsdWludDI1NixieXRlW10pIiwic3RydWN0IjoiYXJjMTU5NF9pc3N1ZV9ldmVudCIsIm5hbWUiOiIwIiwiZGVzYyI6bnVsbH1dfSx7Im5hbWUiOiJSZWRlZW0iLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiKGFkZHJlc3MsdWludDI1NixieXRlW10pIiwic3RydWN0IjoiYXJjMTU5NF9yZWRlZW1fZXZlbnQiLCJuYW1lIjoiMCIsImRlc2MiOm51bGx9XX0seyJuYW1lIjoiVHJhbnNmZXIiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiKGFkZHJlc3MsYWRkcmVzcyxhZGRyZXNzLHVpbnQyNTYsYnl0ZVtdKSIsInN0cnVjdCI6ImFyYzE0MTBfcGFydGl0aW9uX3RyYW5zZmVyIiwibmFtZSI6IjAiLCJkZXNjIjpudWxsfV19LHsibmFtZSI6ImFyYzIwMF9UcmFuc2ZlciIsImRlc2MiOm51bGwsImFyZ3MiOlt7InR5cGUiOiJhZGRyZXNzIiwic3RydWN0IjpudWxsLCJuYW1lIjoiZnJvbSIsImRlc2MiOm51bGx9LHsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJ0byIsImRlc2MiOm51bGx9LHsidHlwZSI6InVpbnQyNTYiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJ2YWx1ZSIsImRlc2MiOm51bGx9XX0seyJuYW1lIjoiYXJjMjAwX0FwcHJvdmFsIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJvd25lciIsImRlc2MiOm51bGx9LHsidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJzcGVuZGVyIiwiZGVzYyI6bnVsbH0seyJ0eXBlIjoidWludDI1NiIsInN0cnVjdCI6bnVsbCwibmFtZSI6InZhbHVlIiwiZGVzYyI6bnVsbH1dfSx7Im5hbWUiOiJSZWRlZW0iLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiKGFkZHJlc3MsYWRkcmVzcyx1aW50MjU2LGJ5dGVbXSkiLCJzdHJ1Y3QiOiJhcmMxNDEwX3BhcnRpdGlvbl9yZWRlZW0iLCJuYW1lIjoiMCIsImRlc2MiOm51bGx9XX0seyJuYW1lIjoiYXJjODhfT3duZXJzaGlwVHJhbnNmZXJyZWQiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InByZXZpb3VzX293bmVyIiwiZGVzYyI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6Im5ld19vd25lciIsImRlc2MiOm51bGx9XX0seyJuYW1lIjoiYXJjODhfT3duZXJzaGlwUmVub3VuY2VkIiwiZGVzYyI6bnVsbCwiYXJncyI6W3sidHlwZSI6ImFkZHJlc3MiLCJzdHJ1Y3QiOm51bGwsIm5hbWUiOiJwcmV2aW91c19vd25lciIsImRlc2MiOm51bGx9XX0seyJuYW1lIjoiYXJjODhfT3duZXJzaGlwVHJhbnNmZXJSZXF1ZXN0ZWQiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InByZXZpb3VzX293bmVyIiwiZGVzYyI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InBlbmRpbmdfb3duZXIiLCJkZXNjIjpudWxsfV19LHsibmFtZSI6ImFyYzg4X093bmVyc2hpcFRyYW5zZmVyQWNjZXB0ZWQiLCJkZXNjIjpudWxsLCJhcmdzIjpbeyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6InByZXZpb3VzX293bmVyIiwiZGVzYyI6bnVsbH0seyJ0eXBlIjoiYWRkcmVzcyIsInN0cnVjdCI6bnVsbCwibmFtZSI6Im5ld19vd25lciIsImRlc2MiOm51bGx9XX1dLCJ0ZW1wbGF0ZVZhcmlhYmxlcyI6e30sInNjcmF0Y2hWYXJpYWJsZXMiOnt9fQ==";
    }

}
