using GalaxyPPG.Common;
using System;
using System.ServiceModel;

namespace GalaxyPPG.Service
{
    class Program
    {
        static void Main(string[] args)
        {
            ServiceHost host = new ServiceHost(typeof(GalaxyPPGService));

            try
            {
                host.Open();
                Console.WriteLine("[SERVER] Servis je pokrenut. Pritisnite Enter za zaustavljanje...");
                Console.ReadLine();
                host.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER] Greska: {ex.Message}");
                host.Abort();
            }
        }
    }
}