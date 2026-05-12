using System.Runtime.Serialization;
using System.ServiceModel;

namespace GalaxyPPG.Common
{
    [ServiceContract]
    public interface IGalaxyPPGService
    {
        [OperationContract]
        void StartSession(SessionMeta meta);

        [OperationContract]
        void PushSample(EcgSample sample);

        [OperationContract]
        void EndSession();
    }

    [DataContract]
    public class SessionMeta
    {
        [DataMember] public string ParticipantId { get; set; }
        [DataMember] public string DeviceId { get; set; }
        [DataMember] public double SampleRateHz { get; set; }
        [DataMember] public long TimestampOffsetMs { get; set; }
    }

    [DataContract]
    public class EcgSample
    {
        [DataMember] public long TimestampMs { get; set; }
        [DataMember] public double? EcgMicroV { get; set; }
        [DataMember] public double? HeartRate { get; set; }
        [DataMember] public double? IBI_ms { get; set; }
        [DataMember] public double? AccX { get; set; }
        [DataMember] public double? AccY { get; set; }
        [DataMember] public double? AccZ { get; set; }
        [DataMember] public string ParticipantId { get; set; }
        [DataMember] public int RowIndex { get; set; }
    }

    [DataContract]
    public class DataFormatFault
    {
        [DataMember] public string Message { get; set; }
    }

    [DataContract]
    public class ValidationFault
    {
        [DataMember] public string Message { get; set; }
    }
}