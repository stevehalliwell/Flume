// Copyright (c) AIR Pty Ltd. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AIR.Flume
{
    internal class Injector
    {
        private const string INJECT = "Inject";
        private readonly FlumeServiceContainer _container;

        private Dictionary<Type, InjectMethodAndArgumentsSet> _cachedInjectSets =
            new Dictionary<Type, InjectMethodAndArgumentsSet>();

        public Injector(FlumeServiceContainer container)
        {
            _container = container;
        }

        internal void InjectDependencies(IDependent dependent)
        {
            if (_container == null) {
                Debug.LogWarning(
                    "Skipping Injection. " +
                    "No UnityServiceContainer container found in scene.");
                return;
            }

            var dependentType = dependent.GetType();

            if (_cachedInjectSets.TryGetValue(dependentType, out var injectMethodAndArguments))
            {
                foreach (var injectMethod in injectMethodAndArguments)
                {
                    injectMethod.method.Invoke(dependent, injectMethod.args);
                }
                return;
            }

            var publicMethods = dependentType.GetMethods();
            var privateMethods = dependentType.GetMethods(
                BindingFlags.NonPublic | BindingFlags.Instance);
            var methods = publicMethods
                .Concat(privateMethods)
                .Distinct();

            injectMethodAndArguments = new InjectMethodAndArgumentsSet();

            foreach (var injectMethod in methods) {
                if (injectMethod.Name != INJECT) continue;
                var injectArgTypes = injectMethod
                    .GetParameters()
                    .Select(p => p.ParameterType)
                    .ToArray();

                var dependentServices = new List<object>();
                foreach (var injectArgType in injectArgTypes) {
                    var service = _container.Resolve(injectArgType, dependent);
                    dependentServices.Add(service);
                }

                var depArrayArgs = dependentServices.ToArray();
                injectMethod.Invoke(dependent, depArrayArgs);

                injectMethodAndArguments.Add(new InjectMethodAndArguments(injectMethod, depArrayArgs));
            }

            _cachedInjectSets[dependentType] = injectMethodAndArguments;
        }

        public class InjectMethodAndArgumentsSet : List<InjectMethodAndArguments> { }

        public class InjectMethodAndArguments
        {
            public readonly MethodInfo method;
            public readonly object[] args;

            public InjectMethodAndArguments(MethodInfo method, object[] args)
            {
                this.method = method;
                this.args = args;
            }
        }
    }
}