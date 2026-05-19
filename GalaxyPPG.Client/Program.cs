using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.ServiceModel;
using GalaxyPPG.Common;

namespace GalaxyPPG.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Unesite ID ucesnika (npr. P01): ");
            string participantId = Console.ReadLine();

            string datasetPath = ConfigurationManager.AppSettings["DatasetPath"];
            string csvPath = Path.Combine(datasetPath, participantId, "PolarH10", "ECG.csv");

            if (!File.Exists(csvPath))
            {
                Console.WriteLine($"Fajl nije pronadjen: {csvPath}");
                return;
            }

            int batchSize = int.Parse(ConfigurationManager.AppSettings["BatchSize"] ?? "20");

            List<string> rejectedRows = new List<string>();

            ChannelFactory<IGalaxyPPGService> factory = null;
            IGalaxyPPGService proxy = null;

            try
            {
                factory = new ChannelFactory<IGalaxyPPGService>("GalaxyPPGService");
                proxy = factory.CreateChannel();

                SessionMeta meta = new SessionMeta
                {
                    ParticipantId = participantId,
                    DeviceId = "PolarH10",
                    SampleRateHz = 130,
                    TimestampOffsetMs = 0
                };

                proxy.StartSession(meta);
                Console.WriteLine("[CLIENT] Sesija pokrenuta.");

                List<EcgSample> batch = new List<EcgSample>();
                int rowIndex = 0;
                int batchNumber = 0;
                int totalSent = 0;

                using (StreamReader reader = new StreamReader(csvPath))
                {
                    string header = reader.ReadLine();

                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        string[] cols = line.Split(',');

                        try
                        {
                            if (cols.Length < 3)
                                throw new Exception("Nedovoljan broj kolona");

                            EcgSample sample = new EcgSample
                            {
                                TimestampMs = ParseLong(cols[0]),   // phoneTimestamp
                                EcgMicroV = ParseDouble(cols[2]),   // ecg (cols[1] je sensorTimestamp)
                                HeartRate = null,
                                IBI_ms = null,
                                AccX = null,
                                AccY = null,
                                AccZ = null,
                                ParticipantId = participantId,
                                RowIndex = rowIndex
                            };

                            batch.Add(sample);

                            if (batch.Count == batchSize)
                            {
                                proxy.PushBatch(batch);
                                batchNumber++;
                                totalSent += batch.Count;
                                Console.WriteLine($"[CLIENT] Blok {batchNumber} poslat ({batch.Count} uzoraka)");
                                batch.Clear();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[CLIENT] Red {rowIndex} odbijen: {ex.Message}");
                            rejectedRows.Add($"{rowIndex},{line},{ex.Message}");
                        }

                        rowIndex++;
                    }

                    if (batch.Count > 0)
                    {
                        proxy.PushBatch(batch);
                        batchNumber++;
                        totalSent += batch.Count;
                        Console.WriteLine($"[CLIENT] Poslednji blok {batchNumber} poslat ({batch.Count} uzoraka)");
                        batch.Clear();
                    }
                }

                proxy.EndSession();
                factory.Close();

                Console.WriteLine($"\n[CLIENT] Prenos zavrsen. Ukupno blokova: {batchNumber}, uzoraka: {totalSent}");
                Console.WriteLine($"[CLIENT] Odbijenih redova na klijentu: {rejectedRows.Count}");

                if (rejectedRows.Count > 0)
                {
                    File.WriteAllLines("rejected_client.csv", rejectedRows);
                    Console.WriteLine("[CLIENT] Odbijeni redovi upisani u rejected_client.csv");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLIENT] Greska: {ex.Message}");
                (proxy as IClientChannel)?.Abort();
                factory?.Abort();
            }
        }

        static long ParseLong(string s)
        {
            if (string.IsNullOrWhiteSpace(s) || s.Trim().ToLower() == "nan")
                throw new Exception("NaN vrednost u obaveznom polju");
            return long.Parse(s.Trim(), CultureInfo.InvariantCulture);
        }

        static double? ParseDouble(string s)
        {
            if (string.IsNullOrWhiteSpace(s) || s.Trim().ToLower() == "nan")
                return null;
            return double.Parse(s.Trim(), CultureInfo.InvariantCulture);
        }
    }
}