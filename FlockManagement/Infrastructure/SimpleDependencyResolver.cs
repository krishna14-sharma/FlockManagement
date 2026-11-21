using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;

namespace FlockManagement.Web.Infrastructure
{
    /// <summary>
    /// Minimal constructor-injection container (no third-party package needed).
    /// Interfaces are registered with a factory; concrete classes (controllers, services)
    /// are built through their public constructor with the most parameters.
    /// Replaceable by Unity/Autofac later without touching controllers or services.
    /// </summary>
    public class SimpleDependencyResolver : IDependencyResolver
    {
        private readonly Dictionary<Type, Func<SimpleDependencyResolver, object>> _factories =
            new Dictionary<Type, Func<SimpleDependencyResolver, object>>();
        private readonly Dictionary<Type, Type> _mappings = new Dictionary<Type, Type>();
        private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();
        private readonly object _sync = new object();

        public SimpleDependencyResolver Register<TService>(Func<SimpleDependencyResolver, TService> factory)
            where TService : class
        {
            _factories[typeof(TService)] = r => factory(r);
            return this;
        }

        public SimpleDependencyResolver Register<TService, TImplementation>() where TImplementation : TService
        {
            _mappings[typeof(TService)] = typeof(TImplementation);
            return this;
        }

        public SimpleDependencyResolver Singleton<TService>(TService instance) where TService : class
        {
            _singletons[typeof(TService)] = instance;
            return this;
        }

        public T Resolve<T>() where T : class
        {
            return (T)Resolve(typeof(T), new HashSet<Type>());
        }

        public object GetService(Type serviceType)
        {
            // MVC asks for many optional services (IControllerFactory, IViewPageActivator...).
            // Return null for anything we do not know so MVC uses its defaults.
            if (!CanResolve(serviceType)) return null;
            return Resolve(serviceType, new HashSet<Type>());
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            var single = GetService(serviceType);
            return single == null ? Enumerable.Empty<object>() : new[] { single };
        }

        private bool CanResolve(Type type)
        {
            if (_singletons.ContainsKey(type) || _factories.ContainsKey(type) || _mappings.ContainsKey(type)) return true;
            return typeof(IController).IsAssignableFrom(type) && !type.IsAbstract;
        }

        private object Resolve(Type type, HashSet<Type> chain)
        {
            object instance;
            if (_singletons.TryGetValue(type, out instance)) return instance;

            Func<SimpleDependencyResolver, object> factory;
            if (_factories.TryGetValue(type, out factory)) return factory(this);

            Type implementation;
            if (_mappings.TryGetValue(type, out implementation)) type = implementation;

            if (type.IsInterface || type.IsAbstract)
                throw new InvalidOperationException("No registration found for " + type.FullName + ".");

            if (!chain.Add(type))
                throw new InvalidOperationException("Circular dependency detected while building " + type.FullName + ".");

            try
            {
                var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .OrderByDescending(c => c.GetParameters().Length)
                    .FirstOrDefault();
                if (ctor == null) throw new InvalidOperationException(type.FullName + " has no public constructor.");

                var args = ctor.GetParameters().Select(p => Resolve(p.ParameterType, chain)).ToArray();
                return ctor.Invoke(args);
            }
            finally
            {
                chain.Remove(type);
            }
        }

        public object SyncRoot { get { return _sync; } }
    }
}
