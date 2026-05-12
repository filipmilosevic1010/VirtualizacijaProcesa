using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.ServiceModel;
using GalaxyPPG.Common;

namespace GalaxyPPG.Service
{
    public class GalaxyPPGService : IGalaxyPPGService, IDisposable
    {
        // Delegati
        public delegate void TransferStartedHandler(string participantId);
        public delegate void SampleReceivedHandler(EcgSample sample);
        public delegate void TransferCompletedHandler(string participantId, int totalBatches);
        public delegate void WarningRaisedHandler(string warning);
        public delegate void BatchReceivedHandler(int batchNumber, int batchSize);

        // Dogadjaji
        public event TransferStartedHandler OnTransferStarted;
        public event SampleReceivedHandler OnSampleReceived;
        public event TransferCompletedHandler OnTransferCompleted;
        public event WarningRaisedHandler OnWarningRaised;
        public event BatchReceivedHandler OnBatchReceived;

        private StreamWriter _writer;
        private StreamWriter _rejectWriter;
        private FileStream _fileStream;
        private FileStream _rejectFileStream;
        private SessionMeta _currentMeta;
        private long _lastTimestampMs = long.MinValue;
        private bool _disposed = false;
        private int _batchCount = 0;

        // Pragovi iz konfiguracije
        private readonly double _ecgSpikeThreshold;
        private readonly double _hrMin;
        private readonly double _hrMax;
        private readonly double _accMotionThreshold;

        public GalaxyPPGService()
        {
            _ecgSpikeThreshold = double.Parse(ConfigurationManager.AppSettings["EcgSpikeThresholdMicroV"] ?? "1000");
            _hrMin = double.Parse(ConfigurationManager.AppSettings["HrMinBpm"] ?? "30");
            _hrMax = double.Parse(ConfigurationManager.AppSettings["HrMaxBpm"] ?? "220");
            _accMotionThreshold = double.Parse(ConfigurationManager.AppSettings["AccMotionThreshold"] ?? "2.0");
        }

        public void StartSession(SessionMeta meta)
        {
            _currentMeta = meta;
            _lastTimestampMs = long.MinValue;
            _batchCount = 0;

            string dir = Path.Combine("Data", meta.ParticipantId, "PolarH10",
                DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(dir);

            string filePath = Path.Combine(dir, "session.csv");
            _fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write);
            _writer = new StreamWriter(_fileStream);
            _writer.WriteLine("TimestampMs,EcgMicroV,HeartRate,IBI_ms,AccX,AccY,AccZ,ParticipantId,RowIndex");
            _writer.Flush();

            string rejectPath = Path.Combine(dir, "rejects.csv");
            _rejectFileStream = new FileStream(rejectPath, FileMode.Append, FileAccess.Write);
            _rejectWriter = new StreamWriter(_rejectFileStream);
            _rejectWriter.WriteLine("RowIndex,Razlog,OriginalniRed");
            _rejectWriter.Flush();

            OnTransferStarted?.Invoke(meta.ParticipantId);
            Console.WriteLine($"[SERVER] Sesija pokrenuta: {meta.ParticipantId}");
        }

        public void PushBatch(List<EcgSample> batch)
        {
            if (_writer == null) return;

            _batchCount++;

            foreach (var sample in batch)
            {
                try
                {
                    if (sample.TimestampMs <= _lastTimestampMs)
                    {
                        string razlog = $"TimestampMs nije monoton: {sample.TimestampMs}";
                        _rejectWriter.WriteLine($"{sample.RowIndex},{razlog},{sample.TimestampMs}");
                        _rejectWriter.Flush();
                        OnWarningRaised?.Invoke(razlog);
                        continue;
                    }

                    _lastTimestampMs = sample.TimestampMs;

                    if (sample.EcgMicroV.HasValue &&
                        (sample.EcgMicroV < -5000 || sample.EcgMicroV > 5000))
                    {
                        string razlog = $"EcgMicroV van opsega: {sample.EcgMicroV}";
                        _rejectWriter.WriteLine($"{sample.RowIndex},{razlog},{sample.EcgMicroV}");
                        _rejectWriter.Flush();
                        OnWarningRaised?.Invoke(razlog);
                        continue;
                    }

                    if (sample.HeartRate.HasValue &&
                        (sample.HeartRate < _hrMin || sample.HeartRate > _hrMax))
                    {
                        string razlog = $"HeartRate van opsega: {sample.HeartRate}";
                        _rejectWriter.WriteLine($"{sample.RowIndex},{razlog},{sample.HeartRate}");
                        _rejectWriter.Flush();
                        OnWarningRaised?.Invoke(razlog);
                        continue;
                    }

                    _writer.WriteLine($"{sample.TimestampMs},{sample.EcgMicroV},{sample.HeartRate}," +
                        $"{sample.IBI_ms},{sample.AccX},{sample.AccY},{sample.AccZ}," +
                        $"{sample.ParticipantId},{sample.RowIndex}");
                    _writer.Flush();

                    OnSampleReceived?.Invoke(sample);
                }
                catch (Exception ex)
                {
                    _rejectWriter.WriteLine($"{sample.RowIndex},{ex.Message},{sample.TimestampMs}");
                    _rejectWriter.Flush();
                }
            }

            OnBatchReceived?.Invoke(_batchCount, batch.Count);
            Console.WriteLine($"[SERVER] Blok {_batchCount} primljen ({batch.Count} uzoraka)");
        }

        public void EndSession()
        {
            OnTransferCompleted?.Invoke(_currentMeta?.ParticipantId, _batchCount);
            Console.WriteLine($"[SERVER] Prenos zavrsen. Ukupno blokova: {_batchCount}");
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _writer?.Dispose();
                    _fileStream?.Dispose();
                    _rejectWriter?.Dispose();
                    _rejectFileStream?.Dispose();
                    _writer = null;
                    _fileStream = null;
                    _rejectWriter = null;
                    _rejectFileStream = null;
                }
                _disposed = true;
            }
        }

        ~GalaxyPPGService()
        {
            Dispose(false);
        }
    }
}