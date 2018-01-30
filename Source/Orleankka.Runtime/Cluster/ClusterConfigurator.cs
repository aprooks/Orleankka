using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using Orleans;
using Orleans.CodeGeneration;
using Orleans.Hosting;
using Orleans.Internals;
using Orleans.Runtime.Configuration;
using Orleans.Streams;

namespace Orleankka.Cluster
{
    using Core;
    using Core.Streams;
    using Behaviors;
    using Utility;

    public sealed class ClusterConfigurator
    {
        readonly ActorInterfaceRegistry registry =
             new ActorInterfaceRegistry();

        readonly HashSet<string> conventions = 
             new HashSet<string>();

        readonly ActorInvocationPipeline pipeline = 
             new ActorInvocationPipeline();
        
        IActorRefMiddleware middleware;

        /// <summary>
        /// Registers global actor middleware (interceptor). This middleware will be used for every actor 
        /// which doesn't specify an individual middleware via call to <see cref="ActorMiddleware(Type,IActorMiddleware) "/>.
        /// </summary>
        /// <param name="global">The middleware.</param>
        public ClusterConfigurator ActorMiddleware(IActorMiddleware global)
        {
            pipeline.Register(global);
            return this;
        }

        /// <summary>
        /// Registers type-based actor middleware (interceptor). 
        /// The middleware is inherited by all subclasses.
        /// </summary>
        /// <param name="type">The actor type (could be the base class)</param>
        /// <param name="middleware">The middleware.</param>
        public ClusterConfigurator ActorMiddleware(Type type, IActorMiddleware middleware)
        {
            pipeline.Register(type, middleware);
            return this;
        }

        /// <summary>
        /// Registers global <see cref="ActorRef"/> middleware (interceptor)
        /// </summary>
        /// <param name="middleware">The middleware.</param>
        public ClusterConfigurator ActorRefMiddleware(IActorRefMiddleware middleware)
        {
            Requires.NotNull(middleware, nameof(middleware));

            if (this.middleware != null)
                throw new InvalidOperationException("ActorRef middleware has been already registered");

            this.middleware = middleware;
            return this;
        }

        public ClusterConfigurator HandlerNamingConventions(params string[] conventions)
        {
            Requires.NotNull(conventions, nameof(conventions));

            if (conventions.Length == 0)
                throw new ArgumentException("conventions are empty", nameof(conventions));

            Array.ForEach(conventions, x => this.conventions.Add(x));

            return this;
        }

        internal void Configure(ISiloHostBuilder builder, IServiceCollection services)
        {
            var cluster = GetClusterConfiguration(services);

            RegisterAssemblies(builder);
            RegisterInterfaces();
            RegisterTypes();
            
            var persistentStreamProviders = RegisterStreamProviders(cluster);
            RegisterStreamSubscriptions(cluster, persistentStreamProviders);

            RegisterBehaviors();
            RegisterDependencies(services);
        }

        void RegisterAssemblies(ISiloHostBuilder builder) => 
            registry.Register(builder.GetApplicationPartManager(), x => x.ActorTypes());

        static ClusterConfiguration GetClusterConfiguration(IServiceCollection services)
        {
            if (!(services.SingleOrDefault(service =>
                service.ServiceType == typeof(ClusterConfiguration)).ImplementationInstance is ClusterConfiguration configuration))
                throw new InvalidOperationException("Cannot configure Orleankka before cluster configuration is set");

            return configuration;
        }

        void RegisterDependencies(IServiceCollection services)
        {
            services.AddSingleton<IActorSystem>(sp => sp.GetService<ClusterActorSystem>());

            services.AddSingleton(sp => new ClusterActorSystem(
                sp.GetService<IStreamProviderManager>(), 
                sp.GetService<IGrainFactory>(), 
                pipeline, middleware));

            services.AddSingleton<Func<MethodInfo, InvokeMethodRequest, IGrain, string>>(DashboardIntegration.Format);
        }

        void RegisterInterfaces() => ActorInterface.Register(registry.Mappings);

        void RegisterTypes() => ActorType.Register(pipeline, registry.Assemblies, conventions.Count > 0 ? conventions.ToArray() : null);

        static IEnumerable<string> RegisterStreamProviders(ClusterConfiguration configuration)
        {
            Type GetType(string partialTypeName)
            {
                var type = Type.GetType(partialTypeName);
                if (type != null) 
                    return type;

                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = a.GetType(partialTypeName);
                    if (type != null)
                        return type;
                }
                
                return null ;
            }

            if (!configuration.Globals.ProviderConfigurations.TryGetValue(ProviderCategoryConfiguration.STREAM_PROVIDER_CATEGORY_NAME, out ProviderCategoryConfiguration pcc))
                return Array.Empty<string>();

            var persistentStreamProviders = new List<string>();
            var allStreamProviders = pcc.Providers.ToArray();
            
            foreach (var each in allStreamProviders)
            {                
                var provider = each.Value;
             
                var type = GetType(provider.Type);
                if (type.IsPersistentStreamProvider())
                {
                    persistentStreamProviders.Add(each.Key);
                    continue;
                }

                pcc.Providers.Remove(each.Key);

                var properties = provider.Properties.ToDictionary(k => k.Key, v => v.Value);
                var spc = new StreamProviderConfiguration(provider.Name, type, properties);
                spc.Register(configuration);
            }

            return persistentStreamProviders.ToArray();
        }

        void RegisterBehaviors()
        {
            foreach (var actor in registry.Assemblies.SelectMany(x => x.ActorTypes()))
                Behavior.Register(actor);
        }

        static void RegisterStreamSubscriptions(ClusterConfiguration cluster, IEnumerable<string> persistentStreamProviders)
        {
            foreach (var actor in ActorType.Registered())
                StreamSubscriptionMatcher.Register(actor.Name, actor.Subscriptions());

            const string id = "stream-subscription-boot";

            var properties = new Dictionary<string, string>
            {
                ["providers"] = string.Join(";", persistentStreamProviders)
            };

            cluster.Globals.RegisterStorageProvider<StreamSubscriptionBootstrapper>(id, properties);
        }
    }

    public static class SiloHostBuilderExtension
    {
        public static ISiloHostBuilder ConfigureOrleankka(this ISiloHostBuilder builder) => 
            ConfigureOrleankka(builder, new ClusterConfigurator());

        public static ISiloHostBuilder ConfigureOrleankka(this ISiloHostBuilder builder, Func<ClusterConfigurator, ClusterConfigurator> configure) => 
            ConfigureOrleankka(builder, configure(new ClusterConfigurator()));

        public static ISiloHostBuilder ConfigureOrleankka(this ISiloHostBuilder builder, ClusterConfigurator cfg) => 
            builder
            .ConfigureServices(services => cfg.Configure(builder, services))
            .ConfigureApplicationParts(apm => apm
                .AddApplicationPart(typeof(IClientEndpoint).Assembly)
                .WithCodeGeneration());
    }
}