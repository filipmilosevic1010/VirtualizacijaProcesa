using System;
using System.IO;
using System.ServiceModel;
using GalaxyPPG.Common;

namespace GalaxyPPG.Service
{
    public class GalaxyPPGService : IGalaxyPPGService
    {
        private StreamWriter _writer;
        private FileStream _fileStream;
        private SessionMeta _currentMeta;
        private long _lastTimestampMs = long.MinValue;

        public void StartSession(SessionMeta meta)
        {
            _currentMeta = meta;
            _lastTimestampMs = long.MinValue;

            string dir = Path.Combine("Data", meta.ParticipantId, "PolarH10",
                DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(dir);

            string filePath = Path.Combine(dir, "session.csv");
            _fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write);
            _writer = new StreamWriter(_fileStream);

            _writer.WriteLine("TimestampMs,EcgMicroV,HeartRate,IBI_ms,AccX,AccY,AccZ,ParticipantId,RowIndex");
            _writer.Flush();

            Console.WriteLine($"[SERVER] Sesija started: {meta.ParticipantId}");
        }

        public void PushSample(EcgSample sample)
        {
            if (_writer == null) return;

            if (sample.TimestampMs <= _lastTimestampMs)
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Message = $"TimestampMs nije monoton: {sample.TimestampMs}" },
                    new FaultReason("ValidationFault"));

            _lastTimestampMs = sample.TimestampMs;

            if (sample.EcgMicroV.HasValue &&
                (sample.EcgMicroV < -5000 || sample.EcgMicroV > 5000))
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Message = $"EcgMicroV van opsega: {sample.EcgMicroV}" },
                    new FaultReason("ValidationFault"));

            if (sample.HeartRate.HasValue &&
                (sample.HeartRate < 30 || sample.HeartRate > 220))
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Message = $"HeartRate van opsega: {sample.HeartRate}" },
                    new FaultReason("ValidationFault"));

            _writer.WriteLine($"{sample.TimestampMs},{sample.EcgMicroV},{sample.HeartRate}," +
                $"{sample.IBI_ms},{sample.AccX},{sample.AccY},{sample.AccZ}," +
                $"{sample.ParticipantId},{sample.RowIndex}");
            _writer.Flush();
        }

        public void EndSession()
        {
            Console.WriteLine("[SERVER] Sesija zavrsena.");
            _writer?.Dispose();
            _fileStream?.Dispose();
            _writer = null;
            _fileStream = null;
        }
    }
}