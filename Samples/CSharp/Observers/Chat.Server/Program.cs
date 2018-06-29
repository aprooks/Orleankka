﻿using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

using Orleans;
using Orleans.Hosting;
using Orleans.Configuration;

using Orleankka.Cluster;

namespace Example
{
    using ClusterOptions = Orleans.Configuration.ClusterOptions;

    class Program
    {
        const string DemoClusterId = "localhost-demo";
        const int LocalhostSiloPort = 11111;
        const int LocalhostGatewayPort = 30000;
        static readonly IPAddress LocalhostSiloAddress = IPAddress.Loopback;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Running demo. Booting cluster might take some time ...\n");

            var host = new SiloHostBuilder()
                .Configure<ClusterOptions>(options => options.ClusterId = DemoClusterId)
                .UseDevelopmentClustering(options => options.PrimarySiloEndpoint = new IPEndPoint(LocalhostSiloAddress, LocalhostSiloPort))
                .ConfigureEndpoints(LocalhostSiloAddress, LocalhostSiloPort, LocalhostGatewayPort)
                .ConfigureApplicationParts(x => x
                    .AddApplicationPart(Assembly.GetExecutingAssembly())
                    .AddApplicationPart(typeof(Join).Assembly)
                    .WithCodeGeneration())
                .UseOrleankka()
                .Build();

            await host.StartAsync();

            Console.WriteLine("Finished booting cluster...");
            Console.ReadLine();
        }
    }
}